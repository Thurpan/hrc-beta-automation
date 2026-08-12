# Offline Windows bootstrap primitives

## Status

This directory contains an internal `net8.0-windows` class library and a
dependency-free console test harness. It is a source/test-only feasibility
module. It is not a broker, controller, descriptor publisher, installer,
standalone runner, Java bridge, or HRC integration.

The module has never been loaded into, attached to, or run with HRC. Most tests
use the test-harness process as both named-pipe endpoints. Two tests launch the
harness as synthetic child peers. They add cross-process
process-identity and framing evidence only. They add no bearer-token,
endpoint-publication, Java, Eclipse callback, HRC UI, or runtime-terminal
evidence. Feasibility remains `TO CONFIRM`.

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
access trustees—the bound account SID and `SYSTEM`—then reads the applied
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
descriptor, authenticate a future executable by hash, or connect to the Java
transport. The production library does not launch processes; the test harness
launches only its own synthetic child mode. The module contains no HRC path,
component, private configuration, licence data, poker data, network client,
registry access, or environment-secret input.

The DACL admits the bound account and `SYSTEM`; exact peer identity is checked
after connection. A same-account process that discovers the pipe name could
therefore connect first and cause denial of service before being rejected.
Random naming, secure name handoff, lifecycle ownership, and dedicated-role
protocol testing remain required.

One synthetic child test asserts distinct parent and child PIDs and nonzero
creation identities. Each endpoint validates the other endpoint's complete
process binding before exchanging fixed public request and response bytes. A
second test keeps the expected child live while a distinct wrong child connects
and confirms server-side rejection.

The harness's only test-control argument is the fixed child mode. When launched
through `dotnet.exe`, the absolute harness assembly path is also a public host
argument. Redirected stdin carries the public parent PID and pipe name. The
child environment is cleared except for minimal .NET host controls, and
redirected output is counted without being recorded; a successful child must
write zero bytes. Normal cleanup is explicit and awaited. Test-failure disposal
performs kill-and-bounded-wait cleanup through the retained process object and
fails if termination is not confirmed. This is not kill-on-close containment
and does not prove cleanup after abrupt parent termination.

These tests do not prove a production broker, controller, or observer exchange.
They do not implement executable hashing, secure endpoint-name delivery,
bearer-token transfer, acknowledgement, revocation, or Java integration.

## Offline validation

Run:

```powershell
& .\src\HrcJobObserver\windows-bootstrap\build.ps1
```

The build uses the installed .NET SDK with the `net8.0-windows` targeting pack,
clears NuGet package sources, isolates library and harness intermediates, and
keeps generated output under the ignored `build/` directory. A targeted source
scan rejects selected networking, environment, console, registry, HRC, and
HoldemResources symbols. Process launch is forbidden in production source and
permitted only in the exact test-harness source.

The 16 tests cover current-process identity and invalid PIDs; exact binding,
all identity-field mismatch paths, and SID validation; secret generation,
copying, disposal, and wiping; bounded round-trip framing; first-instance
collision; server-side and client-side peer
identity rejection; accept and operation timeout with channel poisoning;
one-shot operation enforcement; malformed receive framing; exact applied-DACL
readback; invalid frame bounds; two-sided synthetic parent/child identity and
frame exchange; and server-side rejection of a distinct live child.

The current result is 16/16. This is offline Windows-primitives evidence only.

Still unvalidated: a production broker/controller/observer exchange,
bearer-token transfer, secure endpoint-name delivery, protocol acknowledgement
and revocation, executable-hash policy, crash-contained child cleanup,
LocalAppData descriptor creation and reparse-point defence, Java integration,
OSGi startup, installation, rollback, HRC runtime use, and every standalone-
runner action.
