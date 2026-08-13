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
A protected app-local artefact-set primitive composes retained identities into
one exact directory snapshot. An out-of-band pinned release-manifest seam
admits closed synthetic framework-dependent and one-file no-CRT native
`win-x64` profiles. An audited composite binds the retained native executable
bytes to the strict PE audit. A dedicated containment primitive launches only
that exact synthetic fixture through atomic Job assignment and a pre-user-mode
debug-event image-handle check. The native module aggregate and audited launcher
now require policy bytes and a pin authenticated through a standalone
`HRCOSM01` seam. A pure `HRCNLP01` seam authenticates one closed synthetic
native release manifest and one module policy under an outer pin. A retained,
read-only fixed-leaf selector authenticates that package from an already-
existing protected local NTFS directory. Each layer remains ineligible for
trusted launch. The module has no independently provisioned outer-pin issuer or
rotation policy, trusted provisioning of the selector root and leaf, trusted
writer, installer or updater, secure initial pipe-name handoff, dedicated
production role executables, production-role containment integration, or
connection to the Java layers.
This component is not the standalone runner or an installable HRC plug-in. It
has no
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
tests. The Windows bootstrap history was 77/77 before containment: 20 primitive
tests, 8 descriptor and protocol tests, 27 broker and in-memory-store tests, 11
filesystem tests, 5 single-file artefact-identity tests, and 6 protected app-
local artefact-set tests. Checkpoint `2a56de1` added 5 harness-containment tests
for 82/82. Checkpoint `d4cd474` added 6 pinned release-manifest tests for 88/88.
Checkpoint `fb9ba23` added 7 native-fixture tests for 95/95. Checkpoint
`64043e5` added 7 audited native-release binding tests for 102/102. Committed
checkpoint `70e0d77` adds 5 audited native-containment cases and passes
107/107. Committed checkpoint `2512c6a` extends those 5 cases with real startup
module-load evidence. Follow-up checkpoint `cc77b9b` closes the failed-launch
pre-entry cleanup window. Checkpoint `8bd853a` adds the standalone initial
three-member startup aggregate within the same 3 module tests. Integration
falsified that profile: four containment cases rejected an unexpected fourth
`LOAD_DLL` event before identity validation, producing 106/110. A separate
bounded, read-only `Process.Modules` probe implicated System32 `apphelp.dll`.
Checkpoint `445d02a` closes the host-observed profile over NTDLL, KERNEL32,
KernelBase, and Apphelp. Its later runs directly bind the fourth debugger
`hFile` to the current System32 `apphelp.dll`. Three consecutive exact Release
runs of `445d02a` each passed 110/110 and left no native-fixture child residue.
Checkpoint `4d7781b` adds the
standalone authenticated `HRCOSM01` policy. Checkpoint `66c6e87` makes that
policy and its independently supplied pin mandatory for the aggregate and
native launcher. Direct Release validation of `66c6e87` passes 110/110 with no
native-fixture child residue. The current
split is 20 primitive tests, 8
descriptor and protocol tests, 27 broker and in-memory-store tests, 11
filesystem tests, 5 single-file artefact-identity tests, 6 protected app-local
artefact-set tests, 6 pinned release-manifest tests, 7 native-fixture tests, 7
audited native-release binding tests, 5 harness-containment tests, 3 native
system-module identity tests, and 5 audited native-containment tests. The
broker and in-memory-store tests cover
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

The single-file artefact-identity tests cover one caller-supplied canonical DOS
path on a fixed local drive and Mount Manager volume. The primitive retains a
read handle that denies new data-write and delete access, but not attribute or
extended-attribute access. It verifies the default stream's expected length and
SHA-256, a single link, reparse ancestors and leaf, final path, volume serial
number, and 128-bit `FILE_ID`. Revalidation only detects drift. It does not make
a later path-based launch atomic.

The protected app-local artefact-set tests require one caller-supplied canonical
DOS directory on local NTFS. The root has an exact protected DACL for the
current process account and `SYSTEM`. The set accepts 1 through 128 one-level,
printable ASCII filenames with exact case. Every directory entry must be
expected. An extra PDB, `.runtimeconfig.dev.json`, or subdirectory fails the
scan. Each expected default stream is retained with its length, SHA-256, volume
serial number, and 128-bit `FILE_ID` under one absolute deadline.

The domain-separated canonical manifest digest binds the designated executable
and the ordinally sorted exact names, lengths, and SHA-256 values. Revalidation
scans the exact entry set before and after it revalidates every retained member.
The retained protected root still permits new child creation. The result is a
snapshot and detection control only. A race remains between the final
revalidation and a later path-based loader action.

`ReleaseManifestV1` accepts an out-of-band canonical binary that starts with
`HRCREL01`. Its closed policy admits only two role and deployment pairs: the
synthetic test harness with a framework-dependent snapshot and the synthetic
native fixture with the no-CRT System32-policy profile. Both use the `win-x64`
target-runtime label. The native profile requires the exact one-file
`HrcJobObserver.NativeFixture.exe` set. These labels do not prove runtime or
loaded-module selection.
`PinnedReleaseArtifactSetLease` owns copies of the supplied manifest bytes and
expected pin. It computes a domain-separated SHA-256 pin and compares it in
fixed time before structural parsing.

The parser requires closed role, deployment, and runtime values; a zero reserved
field; one exact designated executable; 1 through 128 strictly ordinally sorted
canonical file entries; no duplicate or case-colliding name; exact lengths and
SHA-256 values; one protected artefact-set manifest digest; and no trailing
bytes. The composite opens the exact protected set, binds its computed canonical
digest to the authenticated manifest, and performs a final exact-set
revalidation. The returned lease retains every member identity, the validated
manifest pin, and the artefact-set digest. It is explicitly ineligible for
trusted launch. Failure disposes a partially opened set and wipes owned
temporary manifest and digest copies.

Keep the manifest out of the protected application directory and exact artefact
set. Inclusion would create self-reference and an unexpected entry. The caller
supplies the out-of-band manifest bytes and owns the pin provenance. A sibling
manifest, a pin derived from that manifest, or a pin compiled into an artefact
covered by the same circular policy does not establish independent trust. The
seam supplies no signature, release provenance, freshness, rollback protection,
trusted installer policy, member file ACL, shared-runtime trust, loader
atomicity, launch integration, production role, private handoff, role-bound
`READY`, Java integration, or HRC runtime evidence. Cooperative deadline and
cancellation checks do not hard-preempt blocking native calls.

Six tests cover exact owned-copy retention and final revalidation,
authentication before structural parsing, noncanonical wire rejection,
protected artefact-set digest binding with failure cleanup, one absolute
operation budget, and a fixed golden identity.

Checkpoint `64043e5` adds seven audited native-release binding cases. They
cover closed-profile and golden-identity checks, bounded exact retained-handle
snapshots, late-failure wiping, authenticated native open and revalidation,
and partial-failure cleanup. `AuditedNativeFixtureReleaseLease` accepts only the
one-file native profile. It audits the retained executable handle's owned
4,096-byte snapshot and binds that audit digest to the authenticated manifest
entry. It remains ineligible for trusted launch, and the caller still owns the
manifest pin's provenance.

The five tests from legacy harness-containment checkpoint `2a56de1` use
internal `ContainedHarnessProcess` code. It launches exactly the current
generated harness apphost in one of two fixed public modes: `Exit` or `Block`.
These join three legacy IPC child modes, for five fixed public child modes in
total. The build guard rejects managed `ProcessStartInfo` or `Process.Start`
launch outside the legacy harness `Program.cs`. It admits exactly two native
`CreateProcessW` call sites in production source: `ContainedHarnessProcess`
and `ContainedAuditedNativeFixtureProcess`.
The native launch supplies an exact non-null `lpApplicationName`, a fixed
command line, an empty Unicode environment, the current executable directory,
no inherited handles, and no standard I/O handles. An unnamed, non-
inheritable Job Object retains exactly `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`.
`PROC_THREAD_ATTRIBUTE_JOB_LIST` assigns the child before its suspended initial
thread can run. Before exact `ResumeThread`, the launcher requires a singleton
Job PID and checks the retained process identity and image path.

One absolute monotonic deadline and caller cancellation govern cooperative
checks around synchronous native launch calls. They do not hard-preempt them.
The launcher rejects a late success after resume. Start failure and disposal
close the last held Job handle, then wait for the retained exact process under
a separate fixed five-second cleanup bound. Concurrent disposal coalesces. The
tests cover normal exit, explicit last-Job-
handle closure that kills a blocking child, no managed entry before a pre-
resume fault, late-deadline cleanup, and concurrent disposal with an admitted
exact-process wait. The managed-entry assertion applies to the legacy apphost
checkpoint, not the audited no-CRT fixture.

The suite does not directly terminate its parent. Kill-on-close semantics
support cleanup when the final Job handle closes, but abrupt-parent-death and
crash behaviour remain unexercised. The proof has no artefact-set trust
integration, release provenance, shared-runtime or loader trust, production
roles, private handoff, role-bound `READY`, token transfer, Java or HRC
integration, sandbox, or same-user hostile-process defence. The protected set
still allows new child creation and remains snapshot and detection only.

Checkpoint `fb9ba23` adds `HrcJobObserver.NativeFixture.exe`, a project-owned
4,096-byte AMD64 PE with no C runtime. It imports only `GetCommandLineW`,
`ExitProcess`, and `Sleep` from `KERNEL32.dll`. Its source defines
`--native-exit`, `--native-block`, and invalid-argument exit code `87`. The
exact embedded neutral-language Windows manifest declares one `amd64` `win32`
identity, `asInvoker`, and `uiAccess=false`, with no dependency or file.

The recorded MSVC `14.44.35207` and Windows SDK `10.0.26100.0` paths build the
fixture twice in separate closed temporary and output directories. The build
requires byte-identical results. The image records subsystem version `6.02`
and `DependentLoadFlags=0x0800`. That flag requires Windows 10 RS1 or later, so
the subsystem value is not the effective runtime floor.

`NativeFixturePeAudit` authenticates a caller-supplied SHA-256 before structural
parsing. It requires the exact PE32+ headers, four fixed sections, complete
directory table, `KERNEL32.dll` descriptor, matching import lookup and address
slots, load configuration, debug records, neutral manifest resource, exception
record, checksum, contiguous raw layout, and no certificate, relocation, gap,
or overlay. The observed pinned-host golden SHA-256 is
`3c9bee49acfffaea7f3fae2692900b47eef0e41e61e4ae7b14e2b1884a05fe34`.
This value is checkpoint evidence only. It is not toolchain or signer
provenance and does not guarantee a cross-machine rebuild.

The bounded runtime test launches Exit and an invalid argument in a cleared
five-variable environment, without a shell or redirected standard handles. It
confirms exit codes `0` and `87` and uses bounded kill-and-wait timeout cleanup.
Historical checkpoint `fb9ba23` did not launch the source-defined Block role
before native Job containment existed. The audit does not prove machine-code
semantics. The fixture has no Control Flow Guard instrumentation. `/CETCOMPAT`
does not prove Control-flow Enforcement Technology enforcement. The evidence
does not prove System32 or KnownDLL trust.

The embedded Windows manifest is not a native `HRCREL01` release-manifest
binding by itself. The separate native profile and audited composite remain
ineligible for trusted launch.

Committed checkpoint `70e0d77` adds five audited native-containment cases. It
launches only the exact synthetic one-file no-CRT fixture. It requires
Windows 10 version 1709 build 16299 or later, x64 debug ABI layouts, an AMD64
process, exact `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` read-back, creation-time
`PROC_THREAD_ATTRIBUTE_JOB_LIST` assignment, and one exact Job PID.

The process uses exact canonical DOS values for `lpApplicationName` and the
working directory. A direct volume-GUID `CreateProcessW` attempt was rejected
on this licensed host. The implementation therefore retains and verifies the
volume-GUID identity but launches through the canonical DOS locator. This is a
host fact, not a general Windows compatibility result. Handles for the complete
fixed-drive-root-to-application-directory chain omit `FILE_SHARE_DELETE`,
reject reparses, and pin the namespace. The executable handle also remains
retained.

`DEBUG_ONLY_THIS_PROCESS` exposes the initial
`CREATE_PROCESS_DEBUG_EVENT` before user mode. Its direct image-file handle
must match the retained executable's length, SHA-256, 128-bit `FILE_ID`, volume
identity, and volume-GUID path. The process and thread handles must match the
creation handles. That event file handle binds the main image only.

Checkpoint `8bd853a` adds a standalone aggregate for the initially observed
NTDLL, KERNEL32, and KernelBase order. Integration produced 106/110 because
four real containment cases rejected an unexpected fourth `LOAD_DLL` event
before identity validation. A separate bounded, read-only `Process.Modules`
probe implicated System32 `apphelp.dll`. Checkpoint `445d02a` therefore closes
the host/build/fixture profile over NTDLL, KERNEL32, KernelBase, and Apphelp.
Its later runs directly bind the fourth debugger `hFile` to the current
System32 `apphelp.dll`.

Checkpoint `4d7781b` adds a fixed 250-byte, little-endian `HRCOSM01` policy.
It binds the `SyntheticNativeFixture` profile, AMD64, Win32NT, exact Windows
10.0 build, and four ordered exact-case filenames, lengths, and SHA-256 digests.
Its domain-separated SHA-256 pin uses
`HRC-BETA-OBSERVER-NATIVE-SYSTEM-MODULE-POLICY-PIN-V1\0`. Authentication occurs
before structural parsing. The owned policy, expected pin, computed pin, and
module digests are wiped on disposal or failure. Exact host revalidation
requires x64 process and operating-system architectures and the exact policy
build. This is authenticated policy data only. It supplies no issuer,
signature, freshness, rollback protection, or servicing authority.

Checkpoint `66c6e87` requires the policy and pin for every aggregate and audited
native launch. The aggregate authenticates and revalidates the policy before it
reads System32. It rejects a current module whose length or digest differs from
policy and retains the validated pin. The policy and aggregate both expose
`IsEligibleForTrustedLaunch` as `false`. The launcher owns and wipes dedicated-
thread input copies and opens this policy-bound aggregate before
`CreateProcessW`. The wrapper, cleanup,
and detached reaper retain the bound aggregate with the other launch authority.

Checkpoint `9d947ce` adds the pure `NativeLaunchPolicyPackageV1` seam. Its
canonical `HRCNLP01` wire uses big-endian integers and totals 440 through 38,667
bytes. The fixed 92-byte header contains the eight-byte magic, 16-bit closed
`SyntheticNativeFixture` profile value `1`, zero 16-bit reserved field, nonzero
64-bit generation, 32-bit release length from 98 through 38,325, 32-bit module-
policy length fixed at 250, and two 32-byte nested pins. Canonical `HRCREL01`
and `HRCOSM01` bytes follow. The outer domain-
separated SHA-256 pin uses
`HRC-BETA-OBSERVER-NATIVE-LAUNCH-POLICY-PACKAGE-PIN-V1\0`. The package compares
that pin in fixed time before structural parsing. It then independently
authenticates each nested document against its header pin. Authentication is
relative only to the caller-supplied outer pin; it supplies no issuer,
signature, release provenance, or protected pin origin. It admits only the
synthetic native-fixture release, no-CRT System32 deployment, `win-x64` runtime
label, and synthetic native system-module profile. Generation is opaque
nonzero metadata only; it supplies no freshness or rollback rule. The seam is
pure and performs no filesystem or live-host access. It owns and wipes its byte
and pin copies and exposes `IsEligibleForTrustedLaunch` as `false`.

Checkpoint `a4e1a9d` adds the offline, read-only
`NativeLaunchPolicyPackageFileLease`. It accepts one caller-supplied, already-
existing canonical DOS root, an expected owner SID that must equal the current
process user, and the external outer package pin. It selects only the exact-
case fixed leaf `native-launch-policy-v1.bin`. The non-reparse root must be on a
fixed-drive, local NTFS Mount Manager volume that reports
`FILE_SUPPORTS_POSIX_UNLINK_RENAME`, with the exact protected owner and DACL for
that user and `SYSTEM`. Its retained handle allows read and write sharing but
denies delete sharing, pinning the root namespace. Canonical root
spelling is compared ordinally without case sensitivity; this does not establish
the on-disk case of each root component. Unrelated sibling entries are allowed
and remain outside the fixed-leaf boundary; a case-colliding fixed-leaf name is
rejected.

The leaf must repeat that exact protected owner and DACL and be a non-reparse
regular default-stream file with one link, stable bounded metadata, the same
volume and final path, and the enumerated 128-bit `FILE_ID`. One caller-supplied
absolute monotonic deadline and cancellation token govern cooperative checks;
they do not hard-preempt a blocking native call. The selector copies the
external pin before filesystem work,
reads 440 through 38,667 exact bytes, authenticates the domain-separated outer
pin before package parsing, and retains the root and leaf handles, authenticated
package, exact bytes, and pin. Revalidation binds the exact leaf case, identity,
metadata, bytes, and domain authentication. The leaf handle requests read and
security-control access and shares only reads. Successful admission therefore
rejects an ordinary pre-existing writable handle or writable mapping and denies
ordinary new data-write and delete opens while retained; it does not block
attribute, extended-attribute, or security-descriptor changes. Revalidation
detects relevant ACL or attribute drift rather than relying on the share mode to
prevent it. These controls make no guarantee against privileged, kernel, or
raw-volume modification. The selector remains ineligible for trusted launch and
does not launch either nested policy.

After authenticating the `CREATE_PROCESS_DEBUG_EVENT` image handle, the
launcher continues that event. It then accepts exactly four `LOAD_DLL` events
in the stated order, followed by the exact initial first-chance breakpoint.
Every event must use the exact created PID and initial TID. The pump admits at
most five events. Each load must provide a valid `hFile` and nonzero base. The
aggregate duplicates the borrowed handle and matches the next current
System32 file by identity, length, volume identity, volume-GUID path, and
SHA-256 bytes. Each module base must differ from the main-image base and all
earlier module bases.

The four expected System32 files use `FileShareRead`-only retained handles.
They can defer replacement or Windows servicing for the aggregate lifetime.
The current host observation for System32 `apphelp.dll` is 666,784 bytes,
SHA-256
`53E7D1ABA3FF4A0D0DEF2DF44777B4C0CA6BB352E8283E8B238E1881B45C8AFE`,
and file version `10.0.26100.8457`. Two hard links were observed in System32
and WinSxS. Authenticode was observed as valid for `Microsoft Windows`.
Apphelp is host/build/fixture appcompat-loader policy, not a static fixture
import. At checkpoint `445d02a`, the primitive self-baselined the current
System32 file. Checkpoint `66c6e87` instead requires that current file to match
the authenticated policy. Neither checkpoint proves signer, Microsoft, build,
freshness, rollback, or appcompat-policy provenance.

The exact initial first-chance breakpoint is the startup barrier. While that
event remains outstanding, `SuspendThread` must report prior count `0`. The
launcher then continues the breakpoint, detaches, proves that no remote
debugger remains, revalidates the sealed aggregate, and requires `ResumeThread`
to report prior count `1`. The full aggregate remains retained through the
wrapper and process lifetime. Failure cleanup retains partial or full aggregate
evidence until exact process exit. The detached reaper receives it when bounded
cleanup cannot prove that exit.

Follow-up checkpoint `cc77b9b` explicitly calls `TerminateJobObject` with the
unique nonzero failed-launch code `0xE0435243` for every post-creation failed
launch. It then closes the last Job handle before it continues any outstanding
debug event. The `AfterInitialBreakpointOwned` fault uses the Exit role and
observes that exact forced code instead of its natural exit code `0`. This
directly closes the former pre-entry cleanup window.

The full debug transaction runs on a fresh dedicated operating-system thread
with `ExecutionContext` flow suppressed. The caller joins it non-abandonably.
Debug cleanup is thread-affine and non-abandonable. After detachment, cleanup
closes the last Job handle and waits on the exact process handle. A detached
reaper retains all authority if the bounded wait cannot prove exit. That
authority remains until the exact handle signals. If
`WaitForSingleObject` fails, the reaper retains the authority indefinitely and
records terminal uncertainty. The build wrapper places a separate 180-second
outer watchdog around the .NET validation process.

Checkpoints `4d7781b` and `66c6e87` extend the same 3 system-module and 5
containment cases without new registrations. The tests construct a synthetic
current-host policy from the four observed lengths and digests and derive its
pin in the harness. This does not establish independent trust. Tests cover the
fixed 250-byte golden policy and domain pin, authentication before parsing,
canonical rejection, owned-copy retention after caller-side wiping and
disposal, exact host facts, deadlines, and aggregate and wrapper pin retention.
Correctly re-pinned wrong NTDLL length and wrong Apphelp digest policies fail
before `CreateProcessW`; the Apphelp case
also exercises cleanup after three expected members opened. The containment
cases continue to cover the extended AMD64 debug ABI, the exact 14-stage
successful containment-hook sequence, all 16 injected launch stages, failure after
each partial or full module capture, a late deadline after Apphelp capture while
its load event remains owned, and a post-resume late deadline. They also cover
aggregate revalidation and disposal, the forced pre-entry failure exit, and
prior containment behaviour. Baseline and final reaper assertions
show only that no retained or terminal reaper state remained at each assertion
time. They do not prove that the reaper was never used. Checkpoint `9d947ce`
adds 4 focused pure package cases. They cover an independently encoded golden
identity, outer authentication before parsing, canonical and nested-profile
rejection, nested pin authentication, owned-copy and disposal behaviour,
cancellation, deadlines, and clean recovery after failure. The checkpoint also
preserves, through the existing release-manifest case, standalone release-
manifest authentication and two-way owned artefact-copy disposal. The overall
Release count is 114. Direct Release validation of
`9d947ce` passes 114/114 with no child residue. Checkpoint `a4e1a9d` adds 4
focused file-selector cases. They cover the independent fixed leaf and golden
package, pin ownership before filesystem or caller mutation, retained
authentication and revalidation, canonical-root/path guards, exact leaf case,
ACL, identity, metadata, byte, and domain-pin checks, allowed siblings, read-
only sharing,
authentication precedence, bounds, replacement and reparse rejection,
cancellation and deadline rollback, borrowed-snapshot wiping, recovery, and
disposal release. Direct Release validation of `a4e1a9d` passes 118/118 with no
native-fixture child residue. The overall Release count is 118.

They do not directly terminate the parent. The initial breakpoint is not a
direct entry sentinel. No debug-event file handle proves section, mapping, or
executed-page identity. The evidence proves no KnownDLL provenance, no global
System32 namespace closure, and no complete dependency or general loader
closure. It does not establish trusted or production launch, a production role,
private handoff, role-bound `READY`, Java integration, or HRC runtime evidence.

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
executables, a trusted installer or release policy that supplies canonical
manifest bytes and independent pin provenance for each complete production
artefact set, trusted pin provenance, production namespace, complete runtime-
module and dependency closure, a direct abrupt-parent-death containment test,
private handoff, role-bound
`READY`, Java-to-Windows integration, packaging, startup, installation,
rollback, safe final shutdown, runtime correlation, and every standalone-runner
operation.
The normal clean-start evidence does not validate arbitrary early class loading
or a different HRC startup route.

Define an independently provisioned outer-pin issuer and rotation policy. Add
trusted provisioning of the canonical selector root and exact fixed leaf, a
freshness and rollback floor, trusted writer, installer, and updater transactions
with crash recovery, and servicing coordination. Checkpoint `a4e1a9d` supplies
only an offline, read-only fixed-leaf selector around the pure package
authenticator. It does not provision its root, leaf, owner SID, or external pin;
write or update the package; compose the package into the existing audited
launcher; launch either nested policy; or provide production, HRC, or runner
integration. Its exact ACL does not isolate hostile processes running as the
same user. The
synthetic current-host test policy and
further self-baselined enumeration do not provide that trust. Keep
the current containment proof separate until dedicated production
roles integrate it. Close the production namespace and complete production
runtime-module, loader, and dependency closure before private handoff and role-
bound `READY`. Pass those gates before any Java or HRC integration.
