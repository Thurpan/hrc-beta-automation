# HRC Job Observer Core

## Status

This directory contains a package-private pure Java 17 feasibility core, an
[offline Eclipse Jobs adapter](eclipse-adapter/README.md), and an
[offline local transport](local-transport/README.md). An
[offline runtime assembly](runtime-assembly/README.md) joins those layers
through their package-private contracts. An
[offline OSGi lifecycle owner](osgi-lifecycle/README.md) tests registration,
startup, cleanup, and a disabled activator. An
[offline simpleconfigurator planner](osgi-packaging/README.md) produces only an
in-memory proposal for the recorded baseline. An
[offline Equinox start-level fixture](equinox-startlevel-fixture/README.md)
tests listener publication before a synthetic level-5 producer in isolated
fresh JVMs. An
[offline Windows bootstrap module](windows-bootstrap/README.md) tests owned
32-byte secret-buffer generation and wiping, exact applied pipe-DACL read-back,
two-sided process identity, bounded one-shot framing, synthetic distinct-
process frame exchange, rejection of a wrong live child, a canonical HMAC-bound
endpoint descriptor, and eight phase- and role-bound messages. It also tests a
capacity-one asynchronous in-memory publisher and a one-shot broker across the
broker harness process and long-lived synthetic observer and controller child
modes. The publisher returns a store-affine, coalesced exact-removal lease.
The broker executes all four exchanges and fail-closed claim-versus-revoke
arbitration. The module also tests an internal descriptor-file publisher and an
independent reader against caller-supplied, already-existing protected local
NTFS directories. This existing-directory seam is not production persistence.
A separate primitive retains and verifies one caller-supplied local artefact.
The module has no trusted release manifest, secure initial pipe-name handoff,
dedicated production role executables, crash containment, or connection to
the Java layers. This
component is not the standalone runner or an installable HRC plug-in. It has no
OSGi manifest, enabled activator, live listener registration, installer, or HRC
runtime entry point. Its offline adapter, runtime, and lifecycle builds accept
an HRC installation path solely to resolve and hash public API provider JARs.

The core has never been installed, loaded, attached to, or run with HRC. Its
offline tests add no HRC observation and do not change the `TO CONFIRM`
feasibility verdict. The dirty HRC tabs `*Hand 7` and `*From Hand 7` remain
protected; do not restart HRC or consume the authorised smoke for this core.
The transport opens IPv4 loopback sockets only during offline tests.

## Implemented scope

`ObserverCoordinator` provides three in-memory operations:

- `arm` accepts one unmatched operation intent with a bounded, process-local
  monotonic timeout;
- `accept` correlates callback-captured `SCHEDULED`, `RUNNING`, and `DONE`
  inputs; and
- `expire` faults a pending arm after its deadline.

`ReplayBuffer` owns positive per-session sequence numbers and returns immutable,
ordered replay windows through `replayAfter`. A replay window reports `OK`,
`GAP`, or `CURSOR_AHEAD`; eviction can never be mistaken for a complete replay.

`LocalObserverServer` implements a bounded version `1` request-response
protocol for authentication, session checks, arm requests, and checkpoint
replay. `ObserverCheckpoint` validates cursor-bound replay and projects core and
callback health.

`OrderedObserverTransportControl` supplies those values in the offline runtime
assembly. It places checkpoints and arm operations in the callback mailbox's
single ticket order. A checkpoint follows every lower-ticket callback and
combines one core replay/fault snapshot with authoritative post-action mailbox
health. An accepted or idempotent arm requires a second ordered marker. That
marker drains callbacks admitted before arm completion, verifies the same arm
is still pending, and starts a fresh observer-local lease. The core emits an
`ARM_CONFIRMED` event with that final opaque deadline. This offline result does
not yet authorise HRC input; a future controller must enforce its own
round-trip and pre-input margin inside that lease.

`BootstrapDescriptor` provides a bounded canonical endpoint model for distinct
observer and broker processes in one user, logon, and session. It enforces an
explicit maximum lifetime and a domain-separated HMAC-SHA256 tag. Structural
parsing is not authentication; verification requires the securely claimed
token and the exact expected process bindings.

`BootstrapProtocol` models four one-shot exchanges with eight exact message and
role pairs: publication, claim grant, separate claim receipt plus final
acknowledgement, and revocation. Its decoder is phase-bound and wipes the owned
input frame. Secret-bearing messages and frames own and wipe their buffers. A
domain-separated receipt HMAC proves token possession within the model.

`InMemoryBootstrapPublicationStore` is the asynchronous reference publisher.
It owns one canonical descriptor at a time and returns independent wipeable
snapshots. Successful publication returns a store-affine opaque lease. The
lease coalesces exact removal and caches its terminal result. Cross-store
checks and exact entry identity prevent an old owner from removing a later
equal publication.

`FileBootstrapPublicationStore` publishes only the public canonical descriptor
as `endpoint-v1.bin` in a caller-supplied protected directory. It binds the
expected owner to the current process account SID and requires an exact DACL
for that account and `SYSTEM`. This DACL does not isolate logon sessions for the
same account. A retained directory handle rejects reparse points, proves a
local NTFS volume, and pins the namespace by denying delete sharing. Publication
uses a random `CREATE_NEW` temporary file, exact flush and read-back checks, and
path, volume, DACL, and file-identity checks. Native retained-root rename uses
no replacement. The final name must reopen as the same file identity.
The retained publication handle denies new write and delete access until exact
removal. The store checks the fixed name-to-file identity again before it
returns the lease.

The file lease uses POSIX handle deletion and bounded retained-directory
enumeration to prove exact absence. It preserves and rejects an ABA replacement.
An indeterminate terminal removal forbids store reuse but permits
operating-system handle cleanup. `FileBootstrapPublicationReader` returns
independent wipeable snapshots after the same directory and final-file checks.
Parsing remains structural only. The file never contains the bearer token.
Deadlines and cancellation are cooperative around synchronous operations.

`BootstrapBrokerSession` binds distinct observer, controller, and broker
processes in one security context. The synthetic harness runs the broker in its
main process and uses long-lived observer and controller child modes.
Publication becomes visible before its acknowledgement. Claim and revoke
workers validate their exact transcript before arbitration. The winner removes
the exact publication before a grant or revocation acknowledgement. An already-
completed malformed loser is terminal. The selected loser is cancelled and
drained within the unchanged bound.

The broker uses absolute monotonic publication and session deadlines from an
injected `TimeProvider`. The publication deadline is capped by the remaining
session budget. These deadlines are cooperative budget checks. They do not
hard-preempt an arbitrary blocking native call. A claim uses a separate receipt
proof and final acknowledgement. The broker disposes its grant and accepted
proof copies and wipes its retained token before the final acknowledgement.
Revocation wipes the retained token before its acknowledgement. `DisposeAsync`
cancels and awaits a running session. `RunAsync` remains the authoritative
protocol-failure channel; `DisposeAsync` separately reports cancellation-
request and cleanup failures. Terminal cleanup coalesces non-abandonable exact
removal, wipes the token, and attempts every pipe close. A faulted or unknown
removal cannot claim absence. Removal verified only after its deadline still
fails the session before terminal acknowledgement. The first uncertainty is
terminal; the session does not retry or republish. These are synthetic offline
properties, not production process or Java integration evidence.

## Correlation and failure invariants

- At most one arm is unmatched. A second request receives `BUSY` without
  changing observer state.
- Repeating the same request ID, operation, name, and timeout is idempotent.
  Reusing the ID with any different value faults the session. Each successful
  request-bound confirmation renews and records a fresh observer-local lease.
- A `SCHEDULED` input must match the injected operation profile's exact bundle,
  version, class, and public Job name. Profiles are injected only; the core has
  no API that bypasses the repository's installed-component identity gate.
- Nash names must satisfy the repository's canonical simulation-name policy.
  Viewer and Export names use a separately bounded, Windows-safe staging-leaf
  rule; the later runner remains responsible for proving the private staging
  path, lease, uniqueness, and exact destination.
- Correlated Jobs are tracked by Java reference identity in an `IdentityHashMap`.
  The raw object can pass through the adapter's bounded in-process mailbox. It
  never enters an emitted event, generated equality, hashing, string
  representation, log, serialisation, or transport.
- Multiple already-correlated Jobs may remain queued or running, including two
  Nash Jobs with the same public name. Each receives a distinct positive ID in
  the observer session.
- Only a known `OK`, `CANCEL`, or `ERROR` result with a usable plug-in identity
  can produce a trusted terminal event. A projectable `DONE` with an unknown
  status, omitted plug-in identity, missing required `RUNNING`, or an existing
  observer fault is emitted as an explicitly rejected terminal projection and
  cannot be used to advance a workflow.
- Any ambiguity latches the first observer fault and rejects new arms. Valid
  later `RUNNING` and projectable `DONE` inputs for already tracked Jobs are
  retained as rejected evidence. Other invalid, duplicate, incomplete, or
  out-of-order callbacks latch or preserve the fault without promising a
  terminal projection.
- The core bounds remembered requests, Jobs, and replay events. This first
  feasibility implementation deliberately retains a small number of Job
  identities strongly; production lifetime and retention require a later
  design after feasibility is positive.

## Data boundary

Events contain only session/sequence/time metadata, request and operation
identity, a per-session numeric Job ID, the bounded Job bundle/version/class and
public name, user/system flags, terminal severity/OK/code/plug-in primitives,
and enumerated fault reasons.

Status messages, exception objects, exception text, stack traces, strategies,
licence material, and unrelated HRC memory are excluded. Necessary public Job
names can contain a hand or staging-output name and must remain local. They
cross the local transport as sensitive plaintext. The transport serialises only
the allow-listed primitives described in its README.

The HRC bundle/version/class/name recognisers used by the tests come from the
version-specific static findings in [`../../docs/feasibility.md`](../../docs/feasibility.md).
They are not a public or vendor-supported API. Core static findings remain
conditional on the exact eight-component fingerprint and active-process path
check. The start-level findings also depend on the recorded `config.ini`,
`bundles.info`, `hrc.ini`, provider JAR, and Job-class hashes. Live adapter use
remains blocked until the active-process and startup gates deliberately cover
every added provider.

The static audit found the calculator to be the only configured artefact, or
embedded JAR within one, that defines or literally refers to the exact Nash,
Viewer Save, and Export Job classes. The calculator is recorded at level
`5,false`; normal Eclipse startup advances to level 6 before launching its
application. This supports listener publication by a level-4 observer on that
normal route. It does not prevent arbitrary `Bundle.loadClass`, reflection, or
another early activation mechanism. Exact rows, hashes, class provenance, and
the remaining boundary are recorded in the feasibility evidence.

## Offline validation

Run on the licensed development host:

```powershell
& .\src\HrcJobObserver\build.ps1
```

The script uses the existing Android Studio JBR compiler and runtime, compiles
with `javac --release 17 -proc:none -Xlint:all -Werror`, and runs the
dependency-free test harness with assertions enabled. It shares a named local
build lock with the adapter script so concurrent validation cannot clean either
script's fixed ignored output tree mid-build. It does not use or copy HRC's
Java runtime or components.

The current 30-test core harness covers input invariants, name and profile
filtering, arm idempotency, busy and expiry handling, callback-time and
wrap-safe deadlines, reference identity, all three operation profiles, two
same-name Nash Jobs, normal and rejected terminal paths, callback-time
ordering, post-fault evidence, status minimisation, atomic core checkpoints,
replay ordering, gaps, cursor bounds, transactionality, immutability, and
synchronised reader/writer access.

The adapter filters before reading public name, Bundle, flags, or result and
adds a fixed-capacity mailbox with a non-waiting callback hand-off. The current
offline results are 30/30 core tests, 34/34 adapter tests, 25/25 transport
tests, 10/10 runtime assembly tests, 14/14 lifecycle tests, and 13/13 packaging
tests. The Windows bootstrap result is 71/71: 20 primitive tests, 8 descriptor
and protocol tests, 27 broker and in-memory-store tests, 11 filesystem tests,
and 5 artefact-identity tests. The broker and in-memory-store tests cover
asynchronous publication,
store-affine coalesced removal, cross-store and ABA defence, exact role context,
all four cross-process exchanges, and
claim/revoke races. They also cover a completed malformed loser, transcript and
proof rejection, the combined absolute deadline, asynchronous cleanup, unknown
removal status, adversarial publication and disposal interleavings, fault
preservation, wiping, and name release. The runtime tests cover the ordered
checkpoint, two-marker arm control, and fresh lease renewal for an exact
idempotent retry.

The filesystem tests cover retained-root rename without replacement, exact
public-byte publication and removal, independent reader snapshots, capacity and
collision paths, deadline and cancellation boundaries, late verified removal,
namespace pinning, bounded multi-page enumeration, file-identity and ABA
replacement, and real fixed-leaf and root junction rejection.

The artefact-identity tests cover one caller-supplied canonical DOS path on a
fixed local drive and Mount Manager volume. The primitive retains a read handle
that denies new data-write and delete access, but not attribute or extended-
attribute access. It verifies the default stream's expected length and SHA-256,
a single link, reparse ancestors and leaf, final path, volume serial number, and
128-bit `FILE_ID`. Revalidation only detects drift. It does not make a later
path-based launch atomic.

One verified file does not bind mutable sibling files, including a framework-
dependent apphost's DLL, `.deps.json`, `.runtimeconfig.json`, or selected .NET
runtime. This slice proves no trusted release manifest, artefact provenance,
file ACL, signature, launch atomicity, launched-process identity, production
role executables, containment, private handoff, role-bound `READY`, Java
integration, or HRC runtime behaviour.

The isolated Equinox fixture passes 12/12 prerequisite-scenario tests, 18/18
recorded-row-scenario tests, and 9/9 observer-failure-scenario tests. It uses
fresh framework storage and hash-pinned installed providers. In the recorded
arrangement, the level-4 observer sees Core Jobs resolved and non-persistent,
and Core Runtime active and persistent. It publishes before the synthetic
level-5 producer schedules one real Eclipse Job. The failure scenario proves
that Equinox can advance after observer activation failure. Publication absence
independently refuses the synthetic controller and prevents the Job.

The fixture's no-runtime-unload result is a policy model. It rejects restart,
republish, update, uninstall, and refresh, and keeps the observer loaded until
final framework shutdown. It does not prove provider-level listener drainage
for dynamic Bundle changes or safe live HRC unload.

The lifecycle tests cover synthetic manager registration, bounded baseline
scans, startup callback admission, ordered health checks, publication rollback,
and shutdown drainage. Its public activator remains disabled. The packaging
tests validate only in-memory bytes and cannot install the proposal.

Still unvalidated in HRC: OSGi resolution and activation, listener registration
and removal, real Eclipse callback delivery, secure token and endpoint
provisioning, Windows known-folder resolution, LocalAppData hierarchy
provisioning and provenance, stale and crash recovery, production descriptor
persistence, secure initial pipe-name delivery, dedicated production bootstrap
executables, complete artefact-set identity, crash containment, Java-to-Windows
integration, packaging, startup, installation, rollback, safe final shutdown,
runtime correlation, and every standalone-runner operation.
The normal clean-start evidence does not validate arbitrary early class loading
or a different HRC startup route.
