# Offline Windows bootstrap primitives

## Status

This directory contains an internal `net8.0-windows` class library and a
dependency-free console test harness. It is a source/test-only feasibility
module. It contains an in-memory publication store and a one-shot synthetic
broker session. It is not a production broker, controller, descriptor-file
publisher, installer, standalone runner, Java bridge, or HRC integration.

The module has never been loaded into, attached to, or run with HRC. Most tests
use the test-harness process as both named-pipe endpoints or exercise the
descriptor and protocol codecs in memory. Two tests launch the harness as
synthetic child peers. They add cross-process process-identity and fixed
public-frame evidence. Broker tests launch persistent synthetic observer and
controller child roles. They transfer a generated bearer token only through
authenticated protected pipes. The public descriptor reaches the controller
through test-control input. The module adds no Java, Eclipse callback, HRC UI,
or runtime-terminal evidence. Feasibility remains `TO CONFIRM`.

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
positive timeouts through the shared 30-second maximum. The client connection
uses an asynchronous local pipe with identification-only impersonation. It
requires an explicit bounded timeout and accepts caller cancellation. The
client validates the complete server process identity before it returns. The
same operation deadline remains active during PID lookup, process capture,
identity comparison, and final result publication. The server applies the same
rule from accept through final authenticated-peer publication.

Accept, send, and receive operations also accept caller cancellation. Disposal
cancels the channel lifetime before it closes the pipe and process handles.
This unblocks a pending accept or receive so its owning worker can await it with
a separate bound. Any admitted operation cancellation, end of file, malformed
received frame, or I/O failure disposes the channel. A failed or completed
direction cannot be retried.

Native peer-PID and DACL reads retain the `SafePipeHandle` with
`DangerousAddRef` until the native call completes. Concurrent disposal can
therefore close the managed channel without invalidating an admitted native
handle use.

`BootstrapDescriptor` defines canonical, bounded, non-secret endpoint metadata.
It binds the publication and broker identifiers, publication nonce, IPv4
loopback endpoint, claim-pipe name, and exact observer and broker process
identities. The observer and broker must be distinct processes in the same
user, logon, token session, and process session. Creation and verification both
enforce a caller-supplied maximum lifetime. The descriptor carries a
domain-separated HMAC-SHA256 tag. Parsing validates and canonicalises structure
only. Authentication becomes meaningful only after a controller has securely
claimed the bearer token and verifies the HMAC, exact bindings, and half-open
validity window.

`BootstrapProtocol` defines eight type- and role-bound messages for four
one-shot request-response exchanges:

1. `PublishRequest` and `PublishAck` between observer and broker.
1. `ClaimRequest` and `ClaimGrant` between controller and broker.
1. `ClaimReceipt` and `ClaimFinalAck` on a separate controller-to-broker
   receipt channel.
1. `RevokeRequest` and `RevokeAck` between observer and broker.

Every decoder requires the expected phase, sender role, and receiver role. It
rejects trailing bytes and non-canonical or malformed fields. Decoding takes
ownership of the complete source frame and wipes it on success or failure.
Secret-bearing messages and encoded frames own their mutable buffers and wipe
them on disposal. A domain-separated HMAC-SHA256 receipt proof binds token
possession to the publication identifier, descriptor digest, controller nonce,
and receipt nonce. The final acknowledgement is a distinct message; receipt
generation alone does not confirm that the broker accepted it.

`InMemoryBootstrapPublicationStore` accepts at most one canonical descriptor.
It clones the encoding on insertion and again for each read. Each read returns
an independently owned, wipeable snapshot. Insertion returns an opaque
registration object. Removal requires the exact registration reference. An old
owner therefore cannot remove a later equal publication.

`BootstrapBrokerSession` binds one observer process, one controller process,
and the current broker process. The roles must be distinct processes in one
user, logon, token session, and process session. The session accepts one
publish request and creates one descriptor. It sends the publish
acknowledgement only after the descriptor is visible in the injected store.

The broker starts one claim worker and one revoke worker. A single lock selects
the first valid transcript. The winner must remove the exact store registration
before the broker sends a grant or revocation acknowledgement. The broker
explicitly cancels the losing worker and drains it within the unchanged
deadlines. It then disposes the losing one-shot pipe. A cancelled in-flight
pipe fails closed in its worker. An independently completed losing failure
remains terminal.

A claim uses a separate receipt pipe. The broker validates the complete
receipt transcript and token-possession proof. It disposes the grant token,
encoded grant, accepted proof, and retained broker token before it sends the
final acknowledgement. A revocation wipes the retained broker token before it
sends the revocation acknowledgement. The first timeout, cancellation,
transcript error, proof error, I/O uncertainty, or store-ownership failure is
terminal. The session does not retry or republish.

The broker derives fixed absolute deadlines from an injected `TimeProvider`.
Later phases receive only the remaining duration, subject to the pipe's
30-second operation limit. A phase cannot reset the session or publication
deadline.

## Security and integration boundary

This module runs the four protocol exchanges between an in-process broker and
persistent synthetic observer and controller children. The production library
does not launch processes. The harness launches only its own fixed child modes.
Each fixed mode is public on the process command line. Role commands travel on
redirected standard input. Those commands contain only the broker PID, public
pipe name, public descriptor, test flags, and bounded delays. The token travels
only on protected protocol pipes. The cleared child environment contains no
secret. Each child must write zero bytes to standard output and standard error.

The in-memory store is not a descriptor filesystem writer or reader. The
module does not deliver an initial pipe name securely, create a LocalAppData
descriptor, authenticate a future executable by hash, or connect to the Java
transport. The persistent roles are test modes in one harness executable. They
are not separate production role executables. The module contains no HRC path,
component, private configuration, licence data, poker data, network client,
registry access, or environment-secret input.

The DACL admits the bound account and `SYSTEM`; exact peer identity is checked
after connection. A same-account process that discovers the pipe name could
therefore connect first and cause denial of service before being rejected.
Random naming, secure name handoff, and lifecycle ownership remain required.

One synthetic child test asserts distinct parent and child PIDs and nonzero
creation identities. Each endpoint validates the other endpoint's complete
process binding before exchanging fixed public request and response bytes. A
second test keeps the expected child live while a distinct wrong child connects
and confirms server-side rejection.

The harness arguments select one of three fixed child modes. When launched
through `dotnet.exe`, the absolute harness assembly path is also a public host
argument. Redirected input carries the role command. The child environment is
cleared except for minimal .NET host controls. Redirected output is counted
without being recorded. A successful child must write zero bytes. Normal
cleanup is explicit and awaited. Test-failure disposal performs
kill-and-bounded-wait cleanup through the retained process object and fails if
termination is not confirmed. This is not kill-on-close containment and does
not prove cleanup after abrupt parent termination.

The broker tests prove the four exchanges only for the synthetic harness roles
and in-memory store. They do not prove production executable separation,
executable hashing, secure initial pipe-name delivery, persisted publication,
crash containment, or Java integration.

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

The first 28 tests cover current-process identity and invalid PIDs; exact binding,
all identity-field mismatch paths, and SID validation; secret generation,
copying, disposal, and wiping; bounded round-trip framing; first-instance
collision; server-side and client-side peer
identity rejection; accept and operation timeout with channel poisoning;
bounded and cancellable client connection; disposal during pending accept and
receive on both endpoints; exact pipe-name release after each disposal path;
deadline and caller-cancellation enforcement during delayed synchronous peer
authentication; one-shot operation enforcement; malformed receive framing;
exact applied-DACL readback; invalid frame bounds; two-sided synthetic
parent/child identity and frame exchange; and server-side rejection of a
distinct live child. Eight tests cover canonical descriptor round trips and
ownership; HMAC, binding, freshness, and maximum-lifetime checks; malformed
and non-canonical descriptor rejection; all eight message and role pairs;
canonical protocol headers and bodies; the domain-separated claim-receipt
proof; malformed semantic fields; and owned token, proof, message, and frame
wiping.

Twelve broker and store tests cover canonical clone ownership and wiping;
capacity-one admission; exact-reference removal and ABA defence; distinct role
and common-security-context enforcement; cross-process publish, claim, separate
receipt, final acknowledgement, and revocation; claim and revoke races in both
directions and from simultaneous release; rejection of an already-completed
competing semantic mismatch before any acknowledgement; transcript and proof
rejection; injected absolute-deadline expiry; cancellation; occupied-store
cleanup; and one-shot pipe-name release. Every persistent synthetic child has
an explicit exit status. Its standard output and standard error must remain
empty.

The current result is 40/40. This is offline Windows model, codec, primitive,
in-memory-store, and synthetic broker evidence only.

Still unvalidated: production observer, broker, and controller executables;
secure pipe-name delivery; descriptor persistence; executable-hash policy;
crash-contained child cleanup; LocalAppData
descriptor creation and reparse-point defence; Java integration; OSGi startup;
installation; rollback; HRC runtime use; and every standalone-runner action.
