# Offline Windows bootstrap primitives

## Status

This directory contains an internal `net8.0-windows` class library and a
dependency-free console test harness. It is a source/test-only feasibility
module. It is not a broker, controller, descriptor publisher, installer,
standalone runner, Java bridge, or HRC integration.

The module has never been loaded into, attached to, or run with HRC. Its tests
use the test-harness process as both named-pipe endpoints. They add no HRC UI,
Eclipse callback, cross-process, or runtime-terminal evidence. Feasibility
remains `TO CONFIRM`.

## Implemented scope

`ProcessIdentityLease` opens and retains one Windows process handle. It records
the PID, raw creation `FILETIME`, absolute image path, account SID, logon SID,
token session ID, and process session ID. A match requires every field and a
still-live retained process object, so a recycled PID is not accepted.

`SecretBuffer` generates exactly 32 cryptographically random bytes, rejects the
all-zero value, never converts the secret to a string, and wipes its owned
array on disposal. Managed-runtime, native API, and kernel copies outside that
array are not claimed to be wipeable.

`ProtectedNamedPipe` creates a random or validated first-instance local byte
pipe through Win32. It requests a protected DACL containing exactly two full-
access trustees—the current account SID and `SYSTEM`—then reads the applied
DACL back from the pipe handle and requires its canonical form to match. Remote
clients are rejected.

Both server and client query the peer PID from the connected pipe, capture a
process identity lease, and require the exact expected PID, creation time,
image, account SID, logon SID, and session. Each authenticated connection
permits at most one send and one receive. Frames contain a four-byte
little-endian length and 1 through 8,192 payload bytes. Operations accept only
positive timeouts through 30 seconds. Any admitted operation cancellation,
EOF, malformed received frame, or I/O failure disposes the channel; a failed or
completed direction cannot be retried.

## Security and integration boundary

This module does not transfer the observer bearer token or define the bootstrap
protocol. It does not publish endpoint metadata, create a LocalAppData
descriptor, authenticate a future executable by hash, launch a process, or
connect to the Java transport. It contains no HRC path, component, private
configuration, licence data, poker data, network client, registry access, or
environment-secret input.

The DACL admits the current account and `SYSTEM`; exact peer identity is checked
after connection. A same-account process that discovers the pipe name could
therefore connect first and cause denial of service before being rejected.
Random naming, secure name handoff, lifecycle ownership, and independent
process testing remain required.

All current pipe tests run between tasks in one process. They exercise the
actual Windows pipe, token, process, and security-descriptor APIs, but they do
not prove an independent broker/controller/observer exchange, cross-process
handle lifetime, executable identity policy, safe teardown during process
exit, or token ownership across a protocol.

## Offline validation

Run:

```powershell
& .\src\HrcJobObserver\windows-bootstrap\build.ps1
```

The build uses the installed .NET SDK with the `net8.0-windows` targeting pack,
clears NuGet package sources, isolates library and harness intermediates, and
keeps generated output under the ignored `build/` directory. A targeted source
scan rejects selected networking, environment, console, process-launch,
registry, HRC, and HoldemResources symbols.

The 14 tests cover current-process identity and invalid PIDs; exact binding,
all identity-field mismatch paths, and SID validation; secret generation,
copying, disposal, and wiping; bounded round-trip framing; first-instance
collision; server-side and client-side peer
identity rejection; accept and operation timeout with channel poisoning;
one-shot operation enforcement; malformed receive framing; exact applied-DACL
readback; and invalid frame bounds.

The current result is 14/14. This is offline Windows-primitives evidence only.

Still unvalidated: independent cross-process peers, token-transfer protocol,
broker/controller ownership, secure endpoint-name handoff, non-secret
descriptor creation and reparse-point defence, Java integration, OSGi startup,
installation, rollback, HRC runtime use, and every standalone-runner action.
