using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace HrcJobObserver.WindowsBootstrap;

internal enum BootstrapRole : byte
{
    Broker = 1,
    Observer = 2,
    Controller = 3,
}

internal enum BootstrapMessageType : byte
{
    PublishRequest = 1,
    PublishAck = 2,
    ClaimRequest = 3,
    ClaimGrant = 4,
    ClaimReceipt = 5,
    ClaimFinalAck = 6,
    RevokeRequest = 7,
    RevokeAck = 8,
}

/// <summary>
/// Owns one encoded protocol frame. Disposal wipes the complete frame because
/// some message types contain the bearer token or a token-possession proof.
/// </summary>
internal sealed class SensitiveFrame : IDisposable
{
    private byte[]? bytes;

    internal SensitiveFrame(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        this.bytes = bytes;
    }

    internal ReadOnlyMemory<byte> Bytes => bytes ??
        throw new ObjectDisposedException(nameof(SensitiveFrame));

    public void Dispose()
    {
        byte[]? owned = bytes;
        bytes = null;
        if (owned is not null)
        {
            CryptographicOperations.ZeroMemory(owned);
        }
    }
}

/// <summary>
/// Owns a token-possession proof. Disposal wipes the complete proof buffer.
/// </summary>
internal sealed class ClaimReceiptProof : IDisposable
{
    private byte[]? bytes;

    internal ClaimReceiptProof(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        BootstrapBinary.ValidateExact32(bytes, nameof(bytes));
        this.bytes = bytes;
    }

    internal ReadOnlySpan<byte> Bytes => bytes ??
        throw new ObjectDisposedException(nameof(ClaimReceiptProof));

    public void Dispose()
    {
        byte[]? owned = bytes;
        bytes = null;
        if (owned is not null)
        {
            CryptographicOperations.ZeroMemory(owned);
        }
    }
}

internal sealed class PublishRequest : IDisposable
{
    private readonly byte[] publicationNonce;
    private SecretBuffer? token;

    internal PublishRequest(
        Guid requestId,
        ReadOnlySpan<byte> publicationNonce,
        ObserverTransportEndpoint endpoint,
        ReadOnlySpan<byte> token)
    {
        BootstrapBinary.ValidateGuid(requestId, nameof(requestId));
        ArgumentNullException.ThrowIfNull(endpoint);
        RequestId = requestId;
        this.publicationNonce = BootstrapBinary.CopyValue32(
            publicationNonce,
            nameof(publicationNonce));
        Endpoint = endpoint;
        this.token = SecretBuffer.CreateOwned(token);
    }

    internal Guid RequestId { get; }
    internal ReadOnlySpan<byte> PublicationNonce => publicationNonce;
    internal ObserverTransportEndpoint Endpoint { get; }
    internal SecretBuffer Token => token ??
        throw new ObjectDisposedException(nameof(PublishRequest));

    internal void DisposeSecret()
    {
        SecretBuffer? owned = token;
        token = null;
        owned?.Dispose();
    }

    public void Dispose()
    {
        DisposeSecret();
    }
}

internal sealed class PublishAck
{
    internal PublishAck(
        Guid requestId,
        Guid publicationId,
        ReadOnlySpan<byte> descriptorDigest,
        ReadOnlySpan<byte> descriptor,
        string revokePipeName)
    {
        ValidateCommon(requestId, publicationId, descriptorDigest);
        if (descriptor.Length is < 1 or > BootstrapDescriptor.MaximumEncodedLength)
        {
            throw new ArgumentException("The descriptor length is invalid.", nameof(descriptor));
        }

        ProtectedNamedPipe.ValidateName(revokePipeName);
        BootstrapDescriptor parsed = BootstrapDescriptor.Parse(descriptor);
        byte[] actualDigest = parsed.ComputeDigest();
        try
        {
            if (parsed.PublicationId != publicationId ||
                !CryptographicOperations.FixedTimeEquals(
                    actualDigest,
                    descriptorDigest))
            {
                throw new ArgumentException(
                    "The acknowledgement descriptor identity is inconsistent.",
                    nameof(descriptor));
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(actualDigest);
        }

        RequestId = requestId;
        PublicationId = publicationId;
        this.descriptorDigest = descriptorDigest.ToArray();
        this.descriptor = descriptor.ToArray();
        RevokePipeName = revokePipeName;
    }

    internal Guid RequestId { get; }
    internal Guid PublicationId { get; }
    internal ReadOnlySpan<byte> DescriptorDigest => descriptorDigest;
    internal ReadOnlySpan<byte> Descriptor => descriptor;
    internal string RevokePipeName { get; }

    private readonly byte[] descriptorDigest;
    private readonly byte[] descriptor;

    private static void ValidateCommon(
        Guid requestId,
        Guid publicationId,
        ReadOnlySpan<byte> descriptorDigest)
    {
        BootstrapBinary.ValidateGuid(requestId, nameof(requestId));
        BootstrapBinary.ValidateGuid(publicationId, nameof(publicationId));
        BootstrapBinary.ValidateExact32(descriptorDigest, nameof(descriptorDigest));
    }
}

internal sealed class ClaimRequest
{
    internal ClaimRequest(
        Guid requestId,
        Guid publicationId,
        ReadOnlySpan<byte> descriptorDigest,
        ReadOnlySpan<byte> controllerNonce)
    {
        BootstrapBinary.ValidateGuid(requestId, nameof(requestId));
        BootstrapBinary.ValidateGuid(publicationId, nameof(publicationId));
        this.descriptorDigest = BootstrapBinary.CopyExact32(
            descriptorDigest,
            nameof(descriptorDigest));
        this.controllerNonce = BootstrapBinary.CopyValue32(
            controllerNonce,
            nameof(controllerNonce));
        RequestId = requestId;
        PublicationId = publicationId;
    }

    internal Guid RequestId { get; }
    internal Guid PublicationId { get; }
    internal ReadOnlySpan<byte> DescriptorDigest => descriptorDigest;
    internal ReadOnlySpan<byte> ControllerNonce => controllerNonce;

    private readonly byte[] descriptorDigest;
    private readonly byte[] controllerNonce;
}

internal sealed class ClaimGrant : IDisposable
{
    private SecretBuffer? token;

    internal ClaimGrant(
        Guid requestId,
        Guid publicationId,
        ReadOnlySpan<byte> descriptorDigest,
        ReadOnlySpan<byte> controllerNonce,
        ReadOnlySpan<byte> receiptNonce,
        string receiptPipeName,
        ReadOnlySpan<byte> token)
    {
        RequestId = RequireGuid(requestId, nameof(requestId));
        PublicationId = RequireGuid(publicationId, nameof(publicationId));
        this.descriptorDigest = BootstrapBinary.CopyExact32(
            descriptorDigest,
            nameof(descriptorDigest));
        this.controllerNonce = BootstrapBinary.CopyValue32(
            controllerNonce,
            nameof(controllerNonce));
        this.receiptNonce = BootstrapBinary.CopyValue32(
            receiptNonce,
            nameof(receiptNonce));
        ProtectedNamedPipe.ValidateName(receiptPipeName);
        ReceiptPipeName = receiptPipeName;
        this.token = SecretBuffer.CreateOwned(token);
    }

    internal Guid RequestId { get; }
    internal Guid PublicationId { get; }
    internal ReadOnlySpan<byte> DescriptorDigest => descriptorDigest;
    internal ReadOnlySpan<byte> ControllerNonce => controllerNonce;
    internal ReadOnlySpan<byte> ReceiptNonce => receiptNonce;
    internal string ReceiptPipeName { get; }
    internal SecretBuffer Token => token ??
        throw new ObjectDisposedException(nameof(ClaimGrant));

    private readonly byte[] descriptorDigest;
    private readonly byte[] controllerNonce;
    private readonly byte[] receiptNonce;

    internal void DisposeSecret()
    {
        SecretBuffer? owned = token;
        token = null;
        owned?.Dispose();
    }

    public void Dispose()
    {
        DisposeSecret();
    }

    private static Guid RequireGuid(Guid value, string name)
    {
        BootstrapBinary.ValidateGuid(value, name);
        return value;
    }
}

internal sealed class ClaimReceipt : IDisposable
{
    private byte[]? possessionProof;

    internal ClaimReceipt(
        Guid requestId,
        Guid publicationId,
        ReadOnlySpan<byte> descriptorDigest,
        ReadOnlySpan<byte> controllerNonce,
        ReadOnlySpan<byte> receiptNonce,
        ReadOnlySpan<byte> possessionProof)
    {
        BootstrapBinary.ValidateGuid(requestId, nameof(requestId));
        BootstrapBinary.ValidateGuid(publicationId, nameof(publicationId));
        BootstrapBinary.ValidateExact32(descriptorDigest, nameof(descriptorDigest));
        BootstrapBinary.ValidateValue32(controllerNonce, nameof(controllerNonce));
        BootstrapBinary.ValidateValue32(receiptNonce, nameof(receiptNonce));
        BootstrapBinary.ValidateExact32(possessionProof, nameof(possessionProof));
        RequestId = requestId;
        PublicationId = publicationId;
        this.descriptorDigest = descriptorDigest.ToArray();
        this.controllerNonce = controllerNonce.ToArray();
        this.receiptNonce = receiptNonce.ToArray();
        this.possessionProof = possessionProof.ToArray();
    }

    internal Guid RequestId { get; }
    internal Guid PublicationId { get; }
    internal ReadOnlySpan<byte> DescriptorDigest => descriptorDigest;
    internal ReadOnlySpan<byte> ControllerNonce => controllerNonce;
    internal ReadOnlySpan<byte> ReceiptNonce => receiptNonce;
    internal ReadOnlySpan<byte> PossessionProof => possessionProof ??
        throw new ObjectDisposedException(nameof(ClaimReceipt));

    private readonly byte[] descriptorDigest;
    private readonly byte[] controllerNonce;
    private readonly byte[] receiptNonce;

    public void Dispose()
    {
        byte[]? owned = possessionProof;
        possessionProof = null;
        if (owned is not null)
        {
            CryptographicOperations.ZeroMemory(owned);
        }
    }
}

internal sealed class ClaimFinalAck : ClaimTranscript
{
    internal ClaimFinalAck(
        Guid requestId,
        Guid publicationId,
        ReadOnlySpan<byte> descriptorDigest,
        ReadOnlySpan<byte> controllerNonce,
        ReadOnlySpan<byte> receiptNonce)
        : base(
            requestId,
            publicationId,
            descriptorDigest,
            controllerNonce,
            receiptNonce)
    {
    }
}

internal sealed class RevokeRequest : RevokeTranscript
{
    internal RevokeRequest(
        Guid requestId,
        Guid publicationId,
        ReadOnlySpan<byte> descriptorDigest,
        ReadOnlySpan<byte> revocationNonce)
        : base(requestId, publicationId, descriptorDigest, revocationNonce)
    {
    }
}

internal sealed class RevokeAck : RevokeTranscript
{
    internal RevokeAck(
        Guid requestId,
        Guid publicationId,
        ReadOnlySpan<byte> descriptorDigest,
        ReadOnlySpan<byte> revocationNonce)
        : base(requestId, publicationId, descriptorDigest, revocationNonce)
    {
    }
}

internal abstract class ClaimTranscript
{
    protected ClaimTranscript(
        Guid requestId,
        Guid publicationId,
        ReadOnlySpan<byte> descriptorDigest,
        ReadOnlySpan<byte> controllerNonce,
        ReadOnlySpan<byte> receiptNonce)
    {
        BootstrapBinary.ValidateGuid(requestId, nameof(requestId));
        BootstrapBinary.ValidateGuid(publicationId, nameof(publicationId));
        RequestId = requestId;
        PublicationId = publicationId;
        this.descriptorDigest = BootstrapBinary.CopyExact32(
            descriptorDigest,
            nameof(descriptorDigest));
        this.controllerNonce = BootstrapBinary.CopyValue32(
            controllerNonce,
            nameof(controllerNonce));
        this.receiptNonce = BootstrapBinary.CopyValue32(
            receiptNonce,
            nameof(receiptNonce));
    }

    internal Guid RequestId { get; }
    internal Guid PublicationId { get; }
    internal ReadOnlySpan<byte> DescriptorDigest => descriptorDigest;
    internal ReadOnlySpan<byte> ControllerNonce => controllerNonce;
    internal ReadOnlySpan<byte> ReceiptNonce => receiptNonce;

    private readonly byte[] descriptorDigest;
    private readonly byte[] controllerNonce;
    private readonly byte[] receiptNonce;
}

internal abstract class RevokeTranscript
{
    protected RevokeTranscript(
        Guid requestId,
        Guid publicationId,
        ReadOnlySpan<byte> descriptorDigest,
        ReadOnlySpan<byte> revocationNonce)
    {
        BootstrapBinary.ValidateGuid(requestId, nameof(requestId));
        BootstrapBinary.ValidateGuid(publicationId, nameof(publicationId));
        RequestId = requestId;
        PublicationId = publicationId;
        this.descriptorDigest = BootstrapBinary.CopyExact32(
            descriptorDigest,
            nameof(descriptorDigest));
        this.revocationNonce = BootstrapBinary.CopyValue32(
            revocationNonce,
            nameof(revocationNonce));
    }

    internal Guid RequestId { get; }
    internal Guid PublicationId { get; }
    internal ReadOnlySpan<byte> DescriptorDigest => descriptorDigest;
    internal ReadOnlySpan<byte> RevocationNonce => revocationNonce;

    private readonly byte[] descriptorDigest;
    private readonly byte[] revocationNonce;
}

/// <summary>
/// Canonical codec for four one-shot bootstrap exchanges. It defines message
/// syntax only; it does not implement role orchestration or state transitions.
/// </summary>
internal static class BootstrapProtocol
{
    internal const byte ProtocolVersion = 1;
    internal const int MaximumFrameLength = ProtectedNamedPipe.MaximumFrameBytes;
    private const int HeaderLength = 16;
    private const int MaximumPipeNameBytes = 120;
    private static ReadOnlySpan<byte> HeaderMagic => "HRCJOBP1"u8;
    private static ReadOnlySpan<byte> ReceiptDomain =>
        "HRC-BETA-OBSERVER-CLAIM-RECEIPT-HMAC-V1\0"u8;

    internal static SensitiveFrame Encode(PublishRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return EncodeMessage(
            BootstrapMessageType.PublishRequest,
            BootstrapRole.Observer,
            BootstrapRole.Broker,
            output =>
            {
                BootstrapBinary.WriteGuid(output, value.RequestId);
                output.Write(value.PublicationNonce);
                BootstrapBinary.WriteUInt16(
                    output,
                    checked((ushort)value.Endpoint.Port));
                BootstrapBinary.WriteGuid(output, value.Endpoint.SessionId);
                output.Write(value.Token.Bytes);
            });
    }

    internal static SensitiveFrame Encode(PublishAck value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidateAck(value.RequestId, value.PublicationId, value.DescriptorDigest);
        byte[] pipe = EncodePipeName(value.RevokePipeName);
        try
        {
            if (value.Descriptor.Length is < 1 or > BootstrapDescriptor.MaximumEncodedLength)
            {
                throw new ArgumentException("The descriptor length is invalid.", nameof(value));
            }

            return EncodeMessage(
                BootstrapMessageType.PublishAck,
                BootstrapRole.Broker,
                BootstrapRole.Observer,
                output =>
                {
                    BootstrapBinary.WriteGuid(output, value.RequestId);
                    BootstrapBinary.WriteGuid(output, value.PublicationId);
                    output.Write(value.DescriptorDigest);
                    BootstrapBinary.WriteUInt16(output, checked((ushort)value.Descriptor.Length));
                    output.Write(value.Descriptor);
                    WriteEncodedText(output, pipe);
                });
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pipe);
        }
    }

    internal static SensitiveFrame Encode(ClaimRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidateClaim(
            value.RequestId,
            value.PublicationId,
            value.DescriptorDigest,
            value.ControllerNonce);
        return EncodeMessage(
            BootstrapMessageType.ClaimRequest,
            BootstrapRole.Controller,
            BootstrapRole.Broker,
            output => WriteClaimPrefix(
                output,
                value.RequestId,
                value.PublicationId,
                value.DescriptorDigest,
                value.ControllerNonce));
    }

    internal static SensitiveFrame Encode(ClaimGrant value)
    {
        ArgumentNullException.ThrowIfNull(value);
        byte[] pipe = EncodePipeName(value.ReceiptPipeName);
        try
        {
            return EncodeMessage(
                BootstrapMessageType.ClaimGrant,
                BootstrapRole.Broker,
                BootstrapRole.Controller,
                output =>
                {
                    WriteClaimPrefix(
                        output,
                        value.RequestId,
                        value.PublicationId,
                        value.DescriptorDigest,
                        value.ControllerNonce);
                    output.Write(value.ReceiptNonce);
                    WriteEncodedText(output, pipe);
                    output.Write(value.Token.Bytes);
                });
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pipe);
        }
    }

    internal static SensitiveFrame Encode(ClaimReceipt value) =>
        EncodeClaimReceiptLike(
            BootstrapMessageType.ClaimReceipt,
            BootstrapRole.Controller,
            BootstrapRole.Broker,
            value.RequestId,
            value.PublicationId,
            value.DescriptorDigest,
            value.ControllerNonce,
            value.ReceiptNonce,
            value.PossessionProof);

    internal static SensitiveFrame Encode(ClaimFinalAck value) =>
        EncodeClaimReceiptLike(
            BootstrapMessageType.ClaimFinalAck,
            BootstrapRole.Broker,
            BootstrapRole.Controller,
            value.RequestId,
            value.PublicationId,
            value.DescriptorDigest,
            value.ControllerNonce,
            value.ReceiptNonce,
            ReadOnlySpan<byte>.Empty);

    internal static SensitiveFrame Encode(RevokeRequest value) =>
        EncodeRevokeLike(
            BootstrapMessageType.RevokeRequest,
            BootstrapRole.Observer,
            BootstrapRole.Broker,
            value.RequestId,
            value.PublicationId,
            value.DescriptorDigest,
            value.RevocationNonce);

    internal static SensitiveFrame Encode(RevokeAck value) =>
        EncodeRevokeLike(
            BootstrapMessageType.RevokeAck,
            BootstrapRole.Broker,
            BootstrapRole.Observer,
            value.RequestId,
            value.PublicationId,
            value.DescriptorDigest,
            value.RevocationNonce);

    /// <summary>
    /// Decodes one caller-transferred frame and always wipes that complete
    /// source array. The caller must not use the array after this call starts.
    /// </summary>
    internal static object DecodeOwned(
        byte[] encoded,
        BootstrapMessageType expectedType,
        BootstrapRole expectedSender,
        BootstrapRole expectedReceiver)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        try
        {
            try
            {
                BootstrapBinaryReader reader = ReadHeader(
                    encoded,
                    out BootstrapMessageType type);
                if (type != expectedType)
                {
                    throw new FormatException(
                        "The bootstrap message type is not expected in this phase.");
                }

                ValidateRoles(expectedType, expectedSender, expectedReceiver);
                object? value = null;
                try
                {
                    value = type switch
                    {
                        BootstrapMessageType.PublishRequest => ReadPublishRequest(ref reader),
                        BootstrapMessageType.PublishAck => ReadPublishAck(ref reader),
                        BootstrapMessageType.ClaimRequest => ReadClaimRequest(ref reader),
                        BootstrapMessageType.ClaimGrant => ReadClaimGrant(ref reader),
                        BootstrapMessageType.ClaimReceipt => ReadClaimReceipt(ref reader),
                        BootstrapMessageType.ClaimFinalAck => ReadClaimFinalAck(ref reader),
                        BootstrapMessageType.RevokeRequest => ReadRevokeRequest(ref reader),
                        BootstrapMessageType.RevokeAck => ReadRevokeAck(ref reader),
                        _ => throw new FormatException(
                            "The bootstrap message type is unsupported."),
                    };
                    reader.RequireEnd();
                    object result = value;
                    value = null;
                    return result;
                }
                finally
                {
                    if (value is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(
                    "The bootstrap message contains an invalid field.",
                    exception);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encoded);
        }
    }

    internal static ClaimReceiptProof ComputeClaimReceiptProof(
        ReadOnlySpan<byte> token,
        Guid publicationId,
        ReadOnlySpan<byte> descriptorDigest,
        ReadOnlySpan<byte> controllerNonce,
        ReadOnlySpan<byte> receiptNonce)
    {
        BootstrapBinary.ValidateValue32(token, nameof(token));
        BootstrapBinary.ValidateGuid(publicationId, nameof(publicationId));
        BootstrapBinary.ValidateExact32(descriptorDigest, nameof(descriptorDigest));
        BootstrapBinary.ValidateValue32(controllerNonce, nameof(controllerNonce));
        BootstrapBinary.ValidateValue32(receiptNonce, nameof(receiptNonce));

        byte[] input = new byte[
            ReceiptDomain.Length + BootstrapBinary.UuidLength +
            (3 * BootstrapBinary.Value32Length)];
        try
        {
            ReceiptDomain.CopyTo(input);
            using MemoryStream writer = new(input, writable: true);
            writer.Position = ReceiptDomain.Length;
            BootstrapBinary.WriteGuid(writer, publicationId);
            writer.Write(descriptorDigest);
            writer.Write(controllerNonce);
            writer.Write(receiptNonce);
            return new ClaimReceiptProof(HMACSHA256.HashData(token, input));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
        }
    }

    internal static bool VerifyClaimReceiptProof(
        ReadOnlySpan<byte> token,
        Guid publicationId,
        ReadOnlySpan<byte> descriptorDigest,
        ReadOnlySpan<byte> controllerNonce,
        ReadOnlySpan<byte> receiptNonce,
        ReadOnlySpan<byte> proof)
    {
        if (proof.Length != BootstrapBinary.Value32Length)
        {
            return false;
        }

        using ClaimReceiptProof expected = ComputeClaimReceiptProof(
            token,
            publicationId,
            descriptorDigest,
            controllerNonce,
            receiptNonce);
        return CryptographicOperations.FixedTimeEquals(expected.Bytes, proof);
    }

    private static SensitiveFrame EncodeMessage(
        BootstrapMessageType type,
        BootstrapRole sender,
        BootstrapRole receiver,
        Action<Stream> writeBody)
    {
        ValidateRoles(type, sender, receiver);
        using MemoryStream body = new(MaximumFrameLength);
        try
        {
            writeBody(body);
            if (body.Length > ushort.MaxValue ||
                body.Length + HeaderLength > MaximumFrameLength)
            {
                throw new ArgumentException(
                    "The bootstrap message exceeds its encoded limit.");
            }

            byte[] result = new byte[HeaderLength + checked((int)body.Length)];
            try
            {
                using MemoryStream output = new(result, writable: true);
                output.Write(HeaderMagic);
                output.WriteByte(ProtocolVersion);
                output.WriteByte((byte)type);
                output.WriteByte((byte)sender);
                output.WriteByte((byte)receiver);
                BootstrapBinary.WriteUInt16(output, 0);
                BootstrapBinary.WriteUInt16(
                    output,
                    checked((ushort)body.Length));
                body.Position = 0;
                body.CopyTo(output);
                return new SensitiveFrame(result);
            }
            catch
            {
                CryptographicOperations.ZeroMemory(result);
                throw;
            }
        }
        finally
        {
            if (body.TryGetBuffer(out ArraySegment<byte> segment))
            {
                CryptographicOperations.ZeroMemory(segment.AsSpan());
            }
        }
    }

    private static BootstrapBinaryReader ReadHeader(
        ReadOnlySpan<byte> encoded,
        out BootstrapMessageType type)
    {
        if (encoded.Length is < HeaderLength or > MaximumFrameLength)
        {
            throw new FormatException("The bootstrap frame length is invalid.");
        }

        BootstrapBinaryReader reader = new(encoded);
        if (!reader.ReadBytes(HeaderMagic.Length, "protocol magic").SequenceEqual(HeaderMagic))
        {
            throw new FormatException("The bootstrap protocol magic is invalid.");
        }

        if (reader.ReadByte("protocol version") != ProtocolVersion)
        {
            throw new FormatException("The bootstrap protocol version is unsupported.");
        }

        type = (BootstrapMessageType)reader.ReadByte("message type");
        BootstrapRole sender = (BootstrapRole)reader.ReadByte("sender role");
        BootstrapRole receiver = (BootstrapRole)reader.ReadByte("receiver role");
        if (reader.ReadUInt16("flags") != 0)
        {
            throw new FormatException("The bootstrap protocol flags must be zero.");
        }

        int bodyLength = reader.ReadUInt16("body length");
        if (bodyLength != reader.Remaining)
        {
            throw new FormatException("The bootstrap body length is invalid.");
        }

        ValidateRoles(type, sender, receiver);
        return reader;
    }

    private static PublishRequest ReadPublishRequest(ref BootstrapBinaryReader reader)
    {
        Guid request = reader.ReadGuid("request identifier");
        byte[] nonce = BootstrapBinary.CopyValue32(
            reader.ReadBytes(BootstrapBinary.Value32Length, "publication nonce"),
            "publication nonce");
        int port = reader.ReadUInt16("endpoint port");
        Guid session = reader.ReadGuid("endpoint session identifier");
        byte[] secret = BootstrapBinary.CopyValue32(
            reader.ReadBytes(SecretBuffer.Length, "bearer token"),
            "bearer token");
        try
        {
            return new PublishRequest(
                request,
                nonce,
                new ObserverTransportEndpoint(port, session),
                secret);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    private static PublishAck ReadPublishAck(ref BootstrapBinaryReader reader)
    {
        Guid request = reader.ReadGuid("request identifier");
        Guid publication = reader.ReadGuid("publication identifier");
        byte[] digest = reader.ReadBytes(32, "descriptor digest").ToArray();
        int length = reader.ReadUInt16("descriptor length");
        if (length is < 1 or > BootstrapDescriptor.MaximumEncodedLength)
        {
            throw new FormatException("The descriptor length is invalid.");
        }

        byte[] descriptor = reader.ReadBytes(length, "descriptor").ToArray();
        string pipe = ReadPipeName(ref reader, "revoke pipe name");
        return new PublishAck(request, publication, digest, descriptor, pipe);
    }

    private static ClaimRequest ReadClaimRequest(ref BootstrapBinaryReader reader)
    {
        ReadClaimPrefix(ref reader, out Guid request, out Guid publication, out byte[] digest, out byte[] nonce);
        return new ClaimRequest(request, publication, digest, nonce);
    }

    private static ClaimGrant ReadClaimGrant(ref BootstrapBinaryReader reader)
    {
        ReadClaimPrefix(ref reader, out Guid request, out Guid publication, out byte[] digest, out byte[] controller);
        byte[] receipt = BootstrapBinary.CopyValue32(
            reader.ReadBytes(32, "receipt nonce"),
            "receipt nonce");
        string pipe = ReadPipeName(ref reader, "receipt pipe name");
        byte[] secret = BootstrapBinary.CopyValue32(
            reader.ReadBytes(SecretBuffer.Length, "bearer token"),
            "bearer token");
        try
        {
            return new ClaimGrant(
                request,
                publication,
                digest,
                controller,
                receipt,
                pipe,
                secret);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    private static ClaimReceipt ReadClaimReceipt(ref BootstrapBinaryReader reader)
    {
        ReadReceiptPrefix(ref reader, out Guid request, out Guid publication, out byte[] digest, out byte[] controller, out byte[] receipt);
        byte[] proof = reader.ReadBytes(32, "possession proof").ToArray();
        try
        {
            return new ClaimReceipt(
                request,
                publication,
                digest,
                controller,
                receipt,
                proof);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(proof);
        }
    }

    private static ClaimFinalAck ReadClaimFinalAck(ref BootstrapBinaryReader reader)
    {
        ReadReceiptPrefix(ref reader, out Guid request, out Guid publication, out byte[] digest, out byte[] controller, out byte[] receipt);
        return new ClaimFinalAck(request, publication, digest, controller, receipt);
    }

    private static RevokeRequest ReadRevokeRequest(ref BootstrapBinaryReader reader)
    {
        ReadRevokePrefix(ref reader, out Guid request, out Guid publication, out byte[] digest, out byte[] nonce);
        return new RevokeRequest(request, publication, digest, nonce);
    }

    private static RevokeAck ReadRevokeAck(ref BootstrapBinaryReader reader)
    {
        ReadRevokePrefix(ref reader, out Guid request, out Guid publication, out byte[] digest, out byte[] nonce);
        return new RevokeAck(request, publication, digest, nonce);
    }

    private static SensitiveFrame EncodeClaimReceiptLike(
        BootstrapMessageType type,
        BootstrapRole sender,
        BootstrapRole receiver,
        Guid request,
        Guid publication,
        ReadOnlySpan<byte> digest,
        ReadOnlySpan<byte> controller,
        ReadOnlySpan<byte> receipt,
        ReadOnlySpan<byte> proof)
    {
        ValidateClaim(request, publication, digest, controller);
        BootstrapBinary.ValidateValue32(receipt, nameof(receipt));
        int expectedProofLength = type == BootstrapMessageType.ClaimReceipt
            ? BootstrapBinary.Value32Length
            : 0;
        if (proof.Length != expectedProofLength)
        {
            throw new ArgumentException("The possession proof length is invalid.", nameof(proof));
        }

        byte[] ownedDigest = digest.ToArray();
        byte[] ownedController = controller.ToArray();
        byte[] ownedReceipt = receipt.ToArray();
        byte[] ownedProof = proof.ToArray();
        try
        {
            return EncodeMessage(type, sender, receiver, output =>
            {
                WriteClaimPrefix(
                    output,
                    request,
                    publication,
                    ownedDigest,
                    ownedController);
                output.Write(ownedReceipt);
                output.Write(ownedProof);
            });
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ownedDigest);
            CryptographicOperations.ZeroMemory(ownedController);
            CryptographicOperations.ZeroMemory(ownedReceipt);
            CryptographicOperations.ZeroMemory(ownedProof);
        }
    }

    private static SensitiveFrame EncodeRevokeLike(
        BootstrapMessageType type,
        BootstrapRole sender,
        BootstrapRole receiver,
        Guid request,
        Guid publication,
        ReadOnlySpan<byte> digest,
        ReadOnlySpan<byte> nonce)
    {
        ValidateClaim(request, publication, digest, nonce);
        byte[] ownedDigest = digest.ToArray();
        byte[] ownedNonce = nonce.ToArray();
        try
        {
            return EncodeMessage(type, sender, receiver, output =>
            {
                BootstrapBinary.WriteGuid(output, request);
                BootstrapBinary.WriteGuid(output, publication);
                output.Write(ownedDigest);
                output.Write(ownedNonce);
            });
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ownedDigest);
            CryptographicOperations.ZeroMemory(ownedNonce);
        }
    }

    private static void WriteClaimPrefix(
        Stream output,
        Guid request,
        Guid publication,
        ReadOnlySpan<byte> digest,
        ReadOnlySpan<byte> controller)
    {
        ValidateClaim(request, publication, digest, controller);
        BootstrapBinary.WriteGuid(output, request);
        BootstrapBinary.WriteGuid(output, publication);
        output.Write(digest);
        output.Write(controller);
    }

    private static void ReadClaimPrefix(
        ref BootstrapBinaryReader reader,
        out Guid request,
        out Guid publication,
        out byte[] digest,
        out byte[] controller)
    {
        request = reader.ReadGuid("request identifier");
        publication = reader.ReadGuid("publication identifier");
        digest = reader.ReadBytes(32, "descriptor digest").ToArray();
        controller = BootstrapBinary.CopyValue32(
            reader.ReadBytes(32, "controller nonce"),
            "controller nonce");
    }

    private static void ReadReceiptPrefix(
        ref BootstrapBinaryReader reader,
        out Guid request,
        out Guid publication,
        out byte[] digest,
        out byte[] controller,
        out byte[] receipt)
    {
        ReadClaimPrefix(ref reader, out request, out publication, out digest, out controller);
        receipt = BootstrapBinary.CopyValue32(
            reader.ReadBytes(32, "receipt nonce"),
            "receipt nonce");
    }

    private static void ReadRevokePrefix(
        ref BootstrapBinaryReader reader,
        out Guid request,
        out Guid publication,
        out byte[] digest,
        out byte[] nonce)
    {
        request = reader.ReadGuid("request identifier");
        publication = reader.ReadGuid("publication identifier");
        digest = reader.ReadBytes(32, "descriptor digest").ToArray();
        nonce = BootstrapBinary.CopyValue32(
            reader.ReadBytes(32, "revocation nonce"),
            "revocation nonce");
    }

    private static void ValidateClaim(
        Guid request,
        Guid publication,
        ReadOnlySpan<byte> digest,
        ReadOnlySpan<byte> nonce)
    {
        BootstrapBinary.ValidateGuid(request, nameof(request));
        BootstrapBinary.ValidateGuid(publication, nameof(publication));
        if (digest.Length != 32)
        {
            throw new ArgumentException("The descriptor digest must be 32 bytes.", nameof(digest));
        }

        BootstrapBinary.ValidateValue32(nonce, nameof(nonce));
    }

    private static void ValidateAck(Guid request, Guid publication, ReadOnlySpan<byte> digest)
    {
        BootstrapBinary.ValidateGuid(request, nameof(request));
        BootstrapBinary.ValidateGuid(publication, nameof(publication));
        if (digest.Length != 32)
        {
            throw new ArgumentException("The descriptor digest must be 32 bytes.", nameof(digest));
        }
    }

    private static void ReadRoles(
        BootstrapMessageType type,
        out BootstrapRole sender,
        out BootstrapRole receiver)
    {
        (sender, receiver) = type switch
        {
            BootstrapMessageType.PublishRequest => (BootstrapRole.Observer, BootstrapRole.Broker),
            BootstrapMessageType.PublishAck => (BootstrapRole.Broker, BootstrapRole.Observer),
            BootstrapMessageType.ClaimRequest => (BootstrapRole.Controller, BootstrapRole.Broker),
            BootstrapMessageType.ClaimGrant => (BootstrapRole.Broker, BootstrapRole.Controller),
            BootstrapMessageType.ClaimReceipt => (BootstrapRole.Controller, BootstrapRole.Broker),
            BootstrapMessageType.ClaimFinalAck => (BootstrapRole.Broker, BootstrapRole.Controller),
            BootstrapMessageType.RevokeRequest => (BootstrapRole.Observer, BootstrapRole.Broker),
            BootstrapMessageType.RevokeAck => (BootstrapRole.Broker, BootstrapRole.Observer),
            _ => throw new FormatException("The bootstrap message type is unsupported."),
        };
    }

    private static void ValidateRoles(
        BootstrapMessageType type,
        BootstrapRole sender,
        BootstrapRole receiver)
    {
        ReadRoles(type, out BootstrapRole expectedSender, out BootstrapRole expectedReceiver);
        if (sender != expectedSender || receiver != expectedReceiver)
        {
            throw new FormatException("The bootstrap message role pair is invalid.");
        }
    }

    private static byte[] EncodePipeName(string name)
    {
        ProtectedNamedPipe.ValidateName(name);
        byte[] encoded = Encoding.ASCII.GetBytes(name);
        if (encoded.Length is < 1 or > MaximumPipeNameBytes)
        {
            CryptographicOperations.ZeroMemory(encoded);
            throw new ArgumentException("The pipe name length is invalid.", nameof(name));
        }

        return encoded;
    }

    private static void WriteEncodedText(Stream output, byte[] value)
    {
        BootstrapBinary.WriteUInt16(output, checked((ushort)value.Length));
        output.Write(value);
    }

    private static string ReadPipeName(ref BootstrapBinaryReader reader, string field)
    {
        int length = reader.ReadUInt16(field + " length");
        ReadOnlySpan<byte> encoded = reader.ReadBytes(length, field);
        if (length is < 1 or > MaximumPipeNameBytes)
        {
            throw new FormatException($"The {field} length is invalid.");
        }

        string value = Encoding.ASCII.GetString(encoded);
        try
        {
            ProtectedNamedPipe.ValidateName(value);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException($"The {field} is invalid.", exception);
        }

        return value;
    }
}
