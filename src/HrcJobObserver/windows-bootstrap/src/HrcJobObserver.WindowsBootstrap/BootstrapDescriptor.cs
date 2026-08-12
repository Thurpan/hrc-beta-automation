using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace HrcJobObserver.WindowsBootstrap;

/// <summary>
/// Canonical endpoint metadata that is HMAC-bound to a bearer token. The token
/// is never retained and authentication is meaningful only after token claim.
/// </summary>
internal sealed class BootstrapDescriptor
{
    internal const int MaximumEncodedLength = 4_096;
    internal const int NonceLength = BootstrapBinary.Value32Length;
    internal const int AuthenticationTagLength = BootstrapBinary.Value32Length;

    private const int MinimumEncodedLength = 209;
    private const int MaximumImagePathBytes = 1_024;
    private const int MaximumSidBytes = 184;
    private const byte FormatVersion = (byte)'1';
    private static readonly DateTimeOffset LatestTimestamp =
        DateTimeOffset.FromUnixTimeMilliseconds(
            DateTimeOffset.MaxValue.ToUnixTimeMilliseconds());
    private static readonly TimeSpan MaximumRepresentableLifetime =
        LatestTimestamp - DateTimeOffset.UnixEpoch;
    private static ReadOnlySpan<byte> AuthenticationDomain =>
        "HRC-BETA-OBSERVER-BOOTSTRAP-DESCRIPTOR-HMAC-V1\0"u8;

    private readonly byte[] nonce;
    private readonly byte[] authenticationTag;

    private BootstrapDescriptor(
        DateTimeOffset createdUtc,
        DateTimeOffset expiresUtc,
        Guid publicationId,
        Guid brokerInstanceId,
        byte[] nonce,
        ObserverTransportEndpoint endpoint,
        string claimPipeName,
        BootstrapBinding observerBinding,
        BootstrapBinding brokerBinding,
        byte[] authenticationTag)
    {
        CreatedUtc = createdUtc;
        ExpiresUtc = expiresUtc;
        PublicationId = publicationId;
        BrokerInstanceId = brokerInstanceId;
        this.nonce = nonce;
        Endpoint = endpoint;
        ClaimPipeName = claimPipeName;
        ObserverBinding = observerBinding;
        BrokerBinding = brokerBinding;
        this.authenticationTag = authenticationTag;
    }

    internal DateTimeOffset CreatedUtc { get; }

    internal DateTimeOffset ExpiresUtc { get; }

    internal Guid PublicationId { get; }

    internal Guid BrokerInstanceId { get; }

    internal ReadOnlySpan<byte> Nonce => nonce;

    internal ObserverTransportEndpoint Endpoint { get; }

    internal string ClaimPipeName { get; }

    internal BootstrapBinding ObserverBinding { get; }

    internal BootstrapBinding BrokerBinding { get; }

    internal ReadOnlySpan<byte> AuthenticationTag => authenticationTag;

    /// <summary>
    /// Creates an HMAC-bound descriptor without retaining or copying the
    /// caller-owned token. The caller remains responsible for wiping that token.
    /// </summary>
    internal static BootstrapDescriptor Create(
        DateTimeOffset createdUtc,
        DateTimeOffset expiresUtc,
        Guid publicationId,
        Guid brokerInstanceId,
        ReadOnlySpan<byte> nonce,
        ObserverTransportEndpoint endpoint,
        string claimPipeName,
        BootstrapBinding observerBinding,
        BootstrapBinding brokerBinding,
        ReadOnlySpan<byte> token,
        TimeSpan maximumLifetime)
    {
        ValidateTimes(createdUtc, expiresUtc);
        BootstrapBinary.ValidateGuid(publicationId, nameof(publicationId));
        BootstrapBinary.ValidateGuid(brokerInstanceId, nameof(brokerInstanceId));
        ValidateMaximumLifetime(maximumLifetime);
        if (expiresUtc - createdUtc > maximumLifetime)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiresUtc),
                "The descriptor exceeds the caller's maximum lifetime.");
        }

        byte[]? ownedNonce = BootstrapBinary.CopyValue32(nonce, nameof(nonce));
        byte[]? tag = null;
        try
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            ValidateClaimPipeName(claimPipeName);
            ArgumentNullException.ThrowIfNull(observerBinding);
            ArgumentNullException.ThrowIfNull(brokerBinding);
            ValidateSharedSecurityContext(observerBinding, brokerBinding);
            BootstrapBinary.ValidateValue32(token, nameof(token));

            BootstrapDescriptor unsigned = new(
                createdUtc,
                expiresUtc,
                publicationId,
                brokerInstanceId,
                ownedNonce,
                endpoint,
                claimPipeName,
                observerBinding,
                brokerBinding,
                new byte[AuthenticationTagLength]);
            byte[] canonicalUnsigned = unsigned.EncodeUnsigned();
            byte[] authenticated = new byte[
                AuthenticationDomain.Length + canonicalUnsigned.Length];
            try
            {
                AuthenticationDomain.CopyTo(authenticated);
                canonicalUnsigned.CopyTo(
                    authenticated,
                    AuthenticationDomain.Length);
                tag = HMACSHA256.HashData(token, authenticated);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(canonicalUnsigned);
                CryptographicOperations.ZeroMemory(authenticated);
            }

            BootstrapDescriptor result = new(
                createdUtc,
                expiresUtc,
                publicationId,
                brokerInstanceId,
                ownedNonce,
                endpoint,
                claimPipeName,
                observerBinding,
                brokerBinding,
                tag);
            ownedNonce = null;
            tag = null;
            return result;
        }
        finally
        {
            if (ownedNonce is not null)
            {
                CryptographicOperations.ZeroMemory(ownedNonce);
            }

            if (tag is not null)
            {
                CryptographicOperations.ZeroMemory(tag);
            }
        }
    }

    /// <summary>
    /// Parses and canonicalises structure only. Authentication and freshness
    /// require a later <see cref="Verify"/> call with the caller-owned token.
    /// </summary>
    internal static BootstrapDescriptor Parse(ReadOnlySpan<byte> encoded)
    {
        if (encoded.Length is < MinimumEncodedLength or > MaximumEncodedLength)
        {
            throw new FormatException(
                "The bootstrap descriptor length is invalid.");
        }

        BootstrapDescriptor? descriptor = null;
        byte[]? nonce = null;
        byte[]? tag = null;
        try
        {
            BootstrapBinaryReader reader = new(encoded);
            if (!reader.ReadBytes(8, "descriptor magic")
                .SequenceEqual("HRCBDESC"u8))
            {
                throw new FormatException(
                    "The bootstrap descriptor magic is invalid.");
            }

            if (reader.ReadByte("descriptor version") != FormatVersion)
            {
                throw new FormatException(
                    "The bootstrap descriptor version is unsupported.");
            }

            reader.RequireZero(7, "descriptor reserved bytes");
            DateTimeOffset createdUtc = DecodeTimestamp(
                reader.ReadInt64("created timestamp"),
                "created timestamp");
            DateTimeOffset expiresUtc = DecodeTimestamp(
                reader.ReadInt64("expiry timestamp"),
                "expiry timestamp");
            ValidateTimes(createdUtc, expiresUtc);

            Guid publicationId = reader.ReadGuid("publication identifier");
            Guid brokerInstanceId = reader.ReadGuid(
                "broker instance identifier");
            nonce = BootstrapBinary.CopyValue32(
                reader.ReadBytes(NonceLength, "publication nonce"),
                "publication nonce");
            if (reader.ReadByte("endpoint protocol") !=
                (byte)('0' + ObserverTransportEndpoint.ProtocolVersion))
            {
                throw new FormatException(
                    "The endpoint protocol version is unsupported.");
            }

            reader.RequireZero(1, "endpoint reserved byte");
            int port = reader.ReadUInt16("endpoint port");
            Guid sessionId = reader.ReadGuid("endpoint session identifier");
            ObserverTransportEndpoint endpoint = new(port, sessionId);
            string claimPipeName = reader.ReadUtf8(
                120,
                "claim pipe name");
            ValidateClaimPipeName(claimPipeName);
            BootstrapBinding observerBinding = ReadBinding(
                ref reader,
                "observer binding");
            BootstrapBinding brokerBinding = ReadBinding(
                ref reader,
                "broker binding");
            ValidateSharedSecurityContext(observerBinding, brokerBinding);
            tag = BootstrapBinary.CopyExact32(
                reader.ReadBytes(
                    AuthenticationTagLength,
                    "descriptor authentication tag"),
                "descriptor authentication tag");
            reader.RequireEnd();

            descriptor = new BootstrapDescriptor(
                createdUtc,
                expiresUtc,
                publicationId,
                brokerInstanceId,
                nonce,
                endpoint,
                claimPipeName,
                observerBinding,
                brokerBinding,
                tag);
            byte[] canonical = descriptor.EncodeCanonical();
            try
            {
                if (!canonical.AsSpan().SequenceEqual(encoded))
                {
                    throw new FormatException(
                        "The bootstrap descriptor is not canonical.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(canonical);
            }

            nonce = null;
            tag = null;
            return descriptor;
        }
        catch (ArgumentException exception)
        {
            throw new FormatException(
                "The bootstrap descriptor contains an invalid field.",
                exception);
        }
        finally
        {
            if (nonce is not null)
            {
                CryptographicOperations.ZeroMemory(nonce);
            }

            if (tag is not null)
            {
                CryptographicOperations.ZeroMemory(tag);
            }
        }
    }

    /// <summary>
    /// After secure token claim, validates the HMAC, exact peer bindings,
    /// caller lifetime policy, and half-open validity window [created, expiry).
    /// The caller retains ownership of the token. This method cannot itself
    /// establish that token delivery or peer authentication was secure.
    /// </summary>
    internal bool Verify(
        ReadOnlySpan<byte> token,
        BootstrapBinding expectedObserverBinding,
        BootstrapBinding expectedBrokerBinding,
        DateTimeOffset currentTimeUtc,
        TimeSpan maximumLifetime)
    {
        BootstrapBinary.ValidateValue32(token, nameof(token));
        ArgumentNullException.ThrowIfNull(expectedObserverBinding);
        ArgumentNullException.ThrowIfNull(expectedBrokerBinding);
        ValidateCanonicalTimestamp(currentTimeUtc, nameof(currentTimeUtc));
        ValidateMaximumLifetime(maximumLifetime);

        byte[] unsigned = EncodeUnsigned();
        byte[] authenticated = new byte[
            AuthenticationDomain.Length + unsigned.Length];
        Span<byte> expectedTag = stackalloc byte[AuthenticationTagLength];
        try
        {
            AuthenticationDomain.CopyTo(authenticated);
            unsigned.CopyTo(authenticated, AuthenticationDomain.Length);
            if (!HMACSHA256.TryHashData(
                    token,
                    authenticated,
                    expectedTag,
                    out int bytesWritten) ||
                bytesWritten != AuthenticationTagLength)
            {
                throw new CryptographicException(
                    "Computing the descriptor authentication tag failed.");
            }

            bool tagMatches = CryptographicOperations.FixedTimeEquals(
                expectedTag,
                authenticationTag);
            bool lifetimeAccepted = ExpiresUtc - CreatedUtc <= maximumLifetime;
            bool current = currentTimeUtc >= CreatedUtc &&
                currentTimeUtc < ExpiresUtc;
            return tagMatches &&
                lifetimeAccepted &&
                current &&
                ObserverBinding.SemanticallyEquals(expectedObserverBinding) &&
                BrokerBinding.SemanticallyEquals(expectedBrokerBinding);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(unsigned);
            CryptographicOperations.ZeroMemory(authenticated);
            CryptographicOperations.ZeroMemory(expectedTag);
        }
    }

    internal byte[] EncodeCanonical()
    {
        byte[] unsigned = EncodeUnsigned();
        byte[] encoded = new byte[unsigned.Length + AuthenticationTagLength];
        try
        {
            unsigned.CopyTo(encoded, 0);
            authenticationTag.CopyTo(encoded, unsigned.Length);
            if (encoded.Length > MaximumEncodedLength)
            {
                throw new InvalidOperationException(
                    "The bootstrap descriptor exceeds its encoded limit.");
            }

            return encoded;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(encoded);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(unsigned);
        }
    }

    internal byte[] ComputeDigest()
    {
        byte[] canonical = EncodeCanonical();
        try
        {
            return SHA256.HashData(canonical);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(canonical);
        }
    }

    private byte[] EncodeUnsigned()
    {
        using MemoryStream output = new(MaximumEncodedLength);
        output.Write("HRCBDESC"u8);
        output.WriteByte(FormatVersion);
        output.Write(stackalloc byte[7]);
        BootstrapBinary.WriteInt64(
            output,
            CreatedUtc.ToUnixTimeMilliseconds());
        BootstrapBinary.WriteInt64(
            output,
            ExpiresUtc.ToUnixTimeMilliseconds());
        BootstrapBinary.WriteGuid(output, PublicationId);
        BootstrapBinary.WriteGuid(output, BrokerInstanceId);
        output.Write(nonce);
        output.WriteByte((byte)('0' + ObserverTransportEndpoint.ProtocolVersion));
        output.WriteByte(0);
        BootstrapBinary.WriteUInt16(output, checked((ushort)Endpoint.Port));
        BootstrapBinary.WriteGuid(output, Endpoint.SessionId);
        WriteUtf8(output, ClaimPipeName, 120, nameof(ClaimPipeName));
        WriteBinding(output, ObserverBinding);
        WriteBinding(output, BrokerBinding);
        byte[] encoded = output.ToArray();
        if (encoded.Length + AuthenticationTagLength > MaximumEncodedLength)
        {
            CryptographicOperations.ZeroMemory(encoded);
            throw new InvalidOperationException(
                "The bootstrap descriptor exceeds its encoded limit.");
        }

        return encoded;
    }

    private static BootstrapBinding ReadBinding(
        ref BootstrapBinaryReader reader,
        string fieldName)
    {
        uint processId = reader.ReadUInt32(fieldName + " process identifier");
        ulong creationTime = reader.ReadUInt64(fieldName + " creation time");
        uint tokenSessionId = reader.ReadUInt32(
            fieldName + " token session identifier");
        uint processSessionId = reader.ReadUInt32(
            fieldName + " process session identifier");
        string imagePath = reader.ReadUtf8(
            MaximumImagePathBytes,
            fieldName + " image path");
        string userSid = reader.ReadUtf8(
            MaximumSidBytes,
            fieldName + " user SID");
        string logonSid = reader.ReadUtf8(
            MaximumSidBytes,
            fieldName + " logon SID");
        return new BootstrapBinding(
            processId,
            creationTime,
            imagePath,
            userSid,
            logonSid,
            tokenSessionId,
            processSessionId);
    }

    private static void WriteBinding(
        Stream output,
        BootstrapBinding binding)
    {
        BootstrapBinary.WriteUInt32(output, binding.ProcessId);
        BootstrapBinary.WriteUInt64(output, binding.CreationTimeFileTime);
        BootstrapBinary.WriteUInt32(output, binding.TokenSessionId);
        BootstrapBinary.WriteUInt32(output, binding.ProcessSessionId);
        WriteUtf8(
            output,
            binding.ImagePath,
            MaximumImagePathBytes,
            nameof(binding.ImagePath));
        WriteUtf8(
            output,
            binding.UserSid,
            MaximumSidBytes,
            nameof(binding.UserSid));
        WriteUtf8(
            output,
            binding.LogonSid,
            MaximumSidBytes,
            nameof(binding.LogonSid));
    }

    private static void WriteUtf8(
        Stream output,
        string value,
        int maximumBytes,
        string parameterName)
    {
        byte[] encoded = BootstrapBinary.EncodeUtf8(
            value,
            maximumBytes,
            parameterName);
        try
        {
            BootstrapBinary.WriteUInt16(output, checked((ushort)encoded.Length));
            output.Write(encoded);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encoded);
        }
    }

    private static DateTimeOffset DecodeTimestamp(
        long unixMilliseconds,
        string fieldName)
    {
        if (unixMilliseconds < 0)
        {
            throw new FormatException($"The {fieldName} is invalid.");
        }

        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new FormatException(
                $"The {fieldName} is invalid.",
                exception);
        }
    }

    private static void ValidateTimes(
        DateTimeOffset createdUtc,
        DateTimeOffset expiresUtc)
    {
        ValidateCanonicalTimestamp(createdUtc, nameof(createdUtc));
        ValidateCanonicalTimestamp(expiresUtc, nameof(expiresUtc));
        if (createdUtc < DateTimeOffset.UnixEpoch)
        {
            throw new ArgumentOutOfRangeException(nameof(createdUtc));
        }

        if (expiresUtc <= createdUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiresUtc),
                "The expiry must be later than creation.");
        }
    }

    private static void ValidateCanonicalTimestamp(
        DateTimeOffset value,
        string parameterName)
    {
        if (value.Offset != TimeSpan.Zero ||
            value.Ticks % TimeSpan.TicksPerMillisecond != 0)
        {
            throw new ArgumentException(
                "The timestamp must be UTC with whole-millisecond precision.",
                parameterName);
        }
    }

    private static void ValidateMaximumLifetime(TimeSpan maximumLifetime)
    {
        if (maximumLifetime <= TimeSpan.Zero ||
            maximumLifetime > MaximumRepresentableLifetime)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumLifetime));
        }
    }

    private static void ValidateClaimPipeName(string claimPipeName)
    {
        ProtectedNamedPipe.ValidateName(claimPipeName);
        if (!claimPipeName.IsNormalized(NormalizationForm.FormC))
        {
            throw new ArgumentException(
                "The claim pipe name must use canonical Unicode.",
                nameof(claimPipeName));
        }
    }

    private static void ValidateSharedSecurityContext(
        BootstrapBinding observerBinding,
        BootstrapBinding brokerBinding)
    {
        if (observerBinding.ProcessId == brokerBinding.ProcessId ||
            !string.Equals(
                observerBinding.UserSid,
                brokerBinding.UserSid,
                StringComparison.Ordinal) ||
            !string.Equals(
                observerBinding.LogonSid,
                brokerBinding.LogonSid,
                StringComparison.Ordinal) ||
            observerBinding.TokenSessionId != brokerBinding.TokenSessionId ||
            observerBinding.ProcessSessionId != brokerBinding.ProcessSessionId)
        {
            throw new ArgumentException(
                "The observer and broker must be distinct processes in one " +
                "user, logon, and session.");
        }
    }
}
