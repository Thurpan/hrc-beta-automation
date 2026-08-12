using System;
using System.IO;
using System.Security.Principal;

namespace HrcJobObserver.WindowsBootstrap;

/// <summary>Non-secret immutable identity expected at a bootstrap channel.</summary>
internal sealed record BootstrapBinding
{
    internal BootstrapBinding(
        uint processId,
        ulong creationTimeFileTime,
        string imagePath,
        string userSid,
        string logonSid,
        uint tokenSessionId,
        uint processSessionId)
    {
        if (processId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId));
        }

        if (creationTimeFileTime == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(creationTimeFileTime));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(userSid);
        ArgumentException.ThrowIfNullOrWhiteSpace(logonSid);
        if (!Path.IsPathFullyQualified(imagePath))
        {
            throw new ArgumentException(
                "The process image path must be absolute.",
                nameof(imagePath));
        }

        string canonicalUserSid = CanonicaliseSid(userSid, nameof(userSid));
        string canonicalLogonSid = CanonicaliseSid(logonSid, nameof(logonSid));

        if (tokenSessionId != processSessionId)
        {
            throw new ArgumentException("The binding session identifiers differ.");
        }

        ProcessId = processId;
        CreationTimeFileTime = creationTimeFileTime;
        ImagePath = imagePath;
        UserSid = canonicalUserSid;
        LogonSid = canonicalLogonSid;
        TokenSessionId = tokenSessionId;
        ProcessSessionId = processSessionId;
    }

    internal uint ProcessId { get; }

    internal ulong CreationTimeFileTime { get; }

    internal string ImagePath { get; }

    internal string UserSid { get; }

    internal string LogonSid { get; }

    internal uint TokenSessionId { get; }

    internal uint ProcessSessionId { get; }

    internal bool Matches(ProcessIdentityLease candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return ProcessId == candidate.ProcessId &&
            CreationTimeFileTime == candidate.CreationTimeFileTime &&
            TokenSessionId == candidate.TokenSessionId &&
            ProcessSessionId == candidate.ProcessSessionId &&
            string.Equals(
                ImagePath,
                candidate.ImagePath,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(UserSid, candidate.UserSid, StringComparison.Ordinal) &&
            string.Equals(LogonSid, candidate.LogonSid, StringComparison.Ordinal);
    }

    internal bool SemanticallyEquals(BootstrapBinding candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return ProcessId == candidate.ProcessId &&
            CreationTimeFileTime == candidate.CreationTimeFileTime &&
            TokenSessionId == candidate.TokenSessionId &&
            ProcessSessionId == candidate.ProcessSessionId &&
            string.Equals(
                ImagePath,
                candidate.ImagePath,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(UserSid, candidate.UserSid, StringComparison.Ordinal) &&
            string.Equals(LogonSid, candidate.LogonSid, StringComparison.Ordinal);
    }

    private static string CanonicaliseSid(string value, string parameterName)
    {
        try
        {
            SecurityIdentifier sid = new(value);
            string canonical = sid.Value;
            if (!string.Equals(value, canonical, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The SID must use its canonical string form.",
                    parameterName);
            }

            return canonical;
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException(
                "The binding contains an invalid SID.",
                parameterName,
                exception);
        }
    }
}
