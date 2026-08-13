# Offline Windows bootstrap primitives

## Status

This directory contains an internal `net8.0-windows` class library and a
dependency-free console test harness. It is a source/test-only feasibility
module. It contains an in-memory publication store, an offline guarded
descriptor-file publication seam, an independent file reader, and a one-shot
synthetic broker session. It also contains a one-file artefact-identity
primitive and a protected app-local artefact-set primitive. The file seam
operates only in a caller-supplied,
already-existing protected directory. It is not production descriptor
persistence, a production broker or controller, an installer, a standalone
runner, a Java bridge, or HRC integration.

The module has never been loaded into, attached to, or run with HRC. Most tests
use the test-harness process as both named-pipe endpoints or exercise the
descriptor and protocol codecs in memory. Two tests launch the harness as
synthetic child peers. They add cross-process process-identity and fixed
public-frame evidence. Broker tests launch persistent synthetic observer and
controller child roles. They transfer a generated bearer token only through
authenticated protected pipes. The public descriptor reaches the controller
through test-control input. File-publication tests use temporary protected NTFS
directories outside HRC. The module adds no Java, Eclipse callback, HRC UI, or
runtime-terminal evidence. Feasibility remains `TO CONFIRM`.

## Implemented scope

`ProcessIdentityLease` opens and retains one Windows process handle. It records
the PID, raw creation `FILETIME`, absolute image path, account SID, logon SID,
token session ID, and process session ID. A match requires every field and a
still-live retained process object, so a recycled PID is not accepted.

`TrustedArtifactIdentity` accepts one caller-supplied canonical DOS file path on
a fixed local drive and Mount Manager volume. It opens the default data stream
with a retained read handle. The handle denies new data-write and delete access,
but not attribute or extended-attribute access. The primitive checks the
expected length and SHA-256, a single link, no reparse ancestor or leaf, the
final handle path, volume serial number, and 128-bit `FILE_ID`.

`TrustedArtifactLease.RevalidateCurrentPath` reopens the path and detects path,
identity, length, or digest drift. It is detection-only. It does not make a
later path-based process launch atomic.

`TrustedArtifactSetLease` requires one caller-supplied canonical DOS directory
on local NTFS. The root must have an exact protected DACL for the current
process account and `SYSTEM`. The caller supplies 1 through 128 expected files.
Each expected entry is one exact-case printable ASCII Windows filename with an
expected default-stream length and SHA-256. Every directory entry must be an
expected file. An extra PDB, `.runtimeconfig.dev.json`, or subdirectory fails
the scan.

The set retains every file through `TrustedArtifactLease`. Each lease pins its
length, digest, volume serial number, and 128-bit `FILE_ID`. One caller-supplied
absolute deadline covers enumeration, member validation, and manifest
calculation. A domain-separated canonical digest binds the designated
executable and the ordinally sorted exact names, lengths, and SHA-256 values.
`RevalidateExactSet` scans the exact entry set before and after it revalidates
every retained member.

The retained root allows new child creation. The set is therefore a snapshot
and detection control only. A race remains between the last revalidation and a
later path-based loader action.

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

`InMemoryBootstrapPublicationStore` is the asynchronous reference publisher.
It accepts at most one canonical descriptor, clones the encoding on insertion
and again for each read, and returns independently owned, wipeable snapshots.
Successful publication returns a store-affine opaque lease. The lease
coalesces concurrent exact-removal calls and caches their terminal result, so
an old owner cannot remove a later equal publication.

`FileBootstrapPublicationStore` is an internal offline publisher for one
caller-supplied, already-existing directory. The caller supplies the expected
owner SID. The store requires that SID to equal the current process account
SID. It requires a local NTFS directory with an exact protected DACL for the
current account and `SYSTEM`. This is account-level protection; the seam does
not isolate separate logon sessions for the same account. It rejects reparse
points and retains the validated directory handle. That handle deliberately
denies delete sharing and pins the directory namespace until disposal.

The file store reserves the fixed public name `endpoint-v1.bin`. It accepts at
most one canonical public descriptor and never writes the bearer token. It
creates a random temporary file with `CREATE_NEW`, writes and flushes the exact
canonical bytes, and validates the bytes, DACL, path, volume, and file identity.
It promotes the file with native `NtSetInformationFile` using
`FileRenameInformation` class 10, the retained directory as `RootDirectory`,
and no replacement. It then reopens the final name and requires the same file
identity. The retained publication handle denies new write and delete access
until exact removal. The store checks the final name-to-file identity again
immediately before it returns the lease.

The store-affine file lease removes only its retained file identity. Removal
uses POSIX handle deletion and bounded enumeration through the retained
directory to prove the exact name absent. An indeterminate terminal removal
forbids store reuse and cannot claim absence. An ABA replacement remains
preserved and rejected. Disposal can still release the retained
operating-system handles. `FileBootstrapPublicationReader` independently
validates the same existing directory and returns an independently owned,
wipeable snapshot.
The reader proves structure and canonical encoding only; it does not
authenticate the descriptor.

File publication applies cooperative cancellation and deadline checks around
its synchronous operations. Removal applies cooperative deadline checks. These
checks do not hard-preempt a blocking native call.

`BootstrapBrokerSession` binds one observer process, one controller process,
and the current broker process. The roles must be distinct processes in one
user, logon, token session, and process session. The session accepts one
publish request and creates one descriptor. It sends the publish
acknowledgement only after the descriptor is visible through the injected
publisher. Its process-local monotonic publication budget is capped by the
remaining session budget rather than restarting time for the store.

The broker starts one claim worker and one revoke worker. A single lock selects
the first valid transcript. The winner must remove the exact publication lease
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
deadline. `DisposeAsync` cancels and awaits a running session. The caller of
`RunAsync` remains the authoritative protocol-failure channel; disposal
independently surfaces cancellation-request or cleanup failures. Terminal
cleanup starts non-abandonable exact removal, then wipes the token and attempts
every pipe close before awaiting the removal result. Primary and cleanup
failures remain independently observable.

## Security and integration boundary

This module runs the four protocol exchanges between an in-process broker and
persistent synthetic observer and controller children. The production library
does not launch processes. The harness launches only its own fixed child modes.
Each fixed mode is public on the process command line. Role commands travel on
redirected standard input. Those commands contain only the broker PID, public
pipe name, public descriptor, test flags, and bounded delays. The token travels
only on protected protocol pipes. The cleared child environment contains no
secret. Each child must write zero bytes to standard output and standard error.

The file seam does not resolve a Windows known folder. It does not provision or
prove a LocalAppData hierarchy, recover stale or crash-left publications, or
deliver an initial pipe name securely. It does not authenticate a future
executable by hash or connect to the Java transport. The persistent roles are
test modes in one harness executable. They are not separate production role
executables. The module contains no HRC path, component, private configuration,
licence data, poker data, network client, registry access, or environment-secret
input. The fixed file contains only the public canonical descriptor, not the
bearer token.

The protected set snapshot binds the caller-declared app-local files. It has no
independently trusted release manifest that authenticates the complete
production artefact set and its canonical digest. It also does not bind or
select a shared .NET runtime. The proof does not include member file ACLs,
signatures, launch atomicity, launched-process identity, production role
executables, containment, private handoff, role-bound `READY`, Java integration,
or HRC runtime behaviour.

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
and in-memory store. The file store is not integrated with those exchanges.
These tests do not prove production executable separation, executable hashing,
secure initial pipe-name delivery, production persistence, crash containment,
or Java integration.

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

The first 20 tests cover current-process identity and invalid PIDs; exact binding,
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

The remaining 27 broker and store tests cover canonical clone ownership and wiping;
capacity-one admission; exact-reference removal and ABA defence; distinct role
and common-security-context enforcement; cross-process publish, claim, separate
receipt, final acknowledgement, and revocation; claim and revoke races in both
directions and from simultaneous release; rejection of an already-completed
competing semantic mismatch before any acknowledgement; transcript and proof
rejection; injected absolute-deadline expiry; cancellation; occupied-store
cleanup; and one-shot pipe-name release. Every persistent synthetic child has
an explicit exit status. Its standard output and standard error must remain
empty.

The asynchronous tests cover explicit publish status, store-affine leases,
cross-store isolation, coalesced exact removal, and cached synchronous removal
failure. They cover cancellation before commit, disposal before `RunAsync`,
disposal during a blocked publish, and rollback when a commit returns after
disposal. Ordinary cancellation publishes a cancelled task after successful
cleanup. An unknown removal result retains the publication and prevents a
terminal grant or revocation acknowledgement. A post-commit removal fault
remains visible through coalesced disposal and does not claim absence. A
start-bound deadline capture failure enters cleanup, faults `RunAsync`, and
releases the publish pipe name. The publication deadline is capped by the
remaining session budget. Its probe expires that combined budget while a fresh
publication budget would remain valid. Cancellation cleanup releases both
protocol pipe names before a blocked exact removal resolves. A publisher can
synchronously re-enter `DisposeAsync` after commit without deadlock. The run
and disposal tasks are published first, and exact removal runs once. Legacy
cross-store removal is rejected in both directions without removing either
store's publication. A synchronous exception from a lifetime-cancellation
callback faults coalesced disposal without stopping run cancellation, exact
removal, token wiping, or protocol-pipe cleanup. Publisher, protocol, and
removal failures remain observable together.

Eleven filesystem cases cover exact public-byte round trips, independent reader
snapshots, capacity and collision handling, malformed and wrongly secured
state, ABA replacement, identity replacement, cancellation, deadlines, late
verified removal, namespace pinning, bounded multi-page enumeration, real
fixed-leaf and root junction rejection, and retained-root cross-directory
rename without replacement.

Five artefact-identity cases cover exact identity and digest retention, invalid
paths and content expectations, real reparse and multi-link rejection, a
pre-existing writable mapping, and the mutable-sibling boundary.

Six protected app-local artefact-set cases cover exact retention and
revalidation, incomplete and unexpected entry rejection, every member's
identity expectations, canonical manifest binding, operation bounds, and
protected-root guards.

The current result is 77/77: 20 primitive tests, 8 descriptor and protocol
tests, 27 broker and in-memory-store tests, 11 filesystem tests, and 5 artefact-
identity tests, and 6 protected app-local artefact-set tests. This is offline
Windows model, codec, primitive, publication-seam, artefact-identity,
artefact-set, and synthetic broker evidence only.

Next, prove that dedicated roles enter kill-on-close Job Object containment
atomically at process creation. Keep this as a separate proof. The protected
application namespace and shared-runtime trust remain unresolved. Complete
those boundaries before private initial name handoff and role-bound `READY`.

Still unvalidated: production observer, broker, and controller executables;
secure pipe-name delivery; known-folder resolution; LocalAppData hierarchy
provisioning and provenance; stale and crash recovery; production descriptor
persistence; an independently trusted release manifest that authenticates each
complete production artefact set and its canonical digest; shared .NET runtime
selection; atomic kill-on-close Job Object containment; private handoff and
role-bound `READY`; Java
integration; OSGi startup; installation; rollback; HRC runtime use; and every
standalone-runner action.
