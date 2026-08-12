using System;

namespace HrcJobObserver.WindowsBootstrap;

/// <summary>Canonical non-secret metadata for the observer's loopback endpoint.</summary>
internal sealed record ObserverTransportEndpoint
{
    internal const byte ProtocolVersion = 1;
    internal const string Address = "127.0.0.1";

    internal ObserverTransportEndpoint(int port, Guid sessionId)
    {
        if (port is < 1 or > 65_535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "The observer session identifier must not be empty.",
                nameof(sessionId));
        }

        Port = port;
        SessionId = sessionId;
    }

    internal int Port { get; }

    internal Guid SessionId { get; }
}
