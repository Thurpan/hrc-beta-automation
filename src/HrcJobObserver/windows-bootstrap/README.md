# Offline Windows bootstrap primitives

## Status

This directory contains an internal `net8.0-windows` class library and a
dependency-free console test harness. It is a source/test-only feasibility
module. It contains an in-memory publication store, an offline guarded
descriptor-file publication seam, an independent file reader, and a one-shot
synthetic broker session. It also contains a one-file artefact-identity
primitive, a protected app-local artefact-set primitive, an out-of-band pinned
release-manifest seam, an internal test-harness-only containment primitive, and
a separate no-CRT native fixture with a strict structural PE audit. An audited
native-release composite and a dedicated containment primitive now bind and
launch only that exact one-file synthetic fixture. They remain source/test-only
and ineligible for trusted launch.
The file seam operates only in a caller-supplied, already-existing protected
directory. It is not production descriptor
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
runtime-terminal evidence. Native-fixture tests use only project-owned source
and generated files. Feasibility remains `TO CONFIRM`.
The dirty HRC tabs `*Hand 7` and `*From Hand 7` remain protected. Do not install
or activate this module, restart HRC, or consume the authorised smoke while
those resources remain unresolved.

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

`ReleaseManifestV1` parses one out-of-band canonical binary manifest with magic
`HRCREL01`. Its closed version-1 policy admits only two exact role and
deployment pairs. `SyntheticTestHarness` pairs with
`FrameworkDependentSnapshot`. `SyntheticNativeFixture` pairs with
`NativeNoCrtSystem32Fixture`. Both use `WinX64`. The native profile requires
exactly one artefact named `HrcJobObserver.NativeFixture.exe`. These are policy
labels. They do not prove actual runtime or loaded-module selection. The
manifest records one exact designated executable, 1 through 128 artefact
entries, and the protected artefact-set canonical digest.

`PinnedReleaseArtifactSetLease.Open` copies the manifest bytes and the caller's
expected 32-byte pin before it uses them. It computes SHA-256 over the domain
`HRC-BETA-OBSERVER-RELEASE-MANIFEST-PIN-V1\0` and the complete owned manifest.
It compares that digest with the owned expected pin in fixed time before any
structural parse. The parser requires exact closed policy values, a zero
reserved field, canonical printable ASCII Windows filenames, strict ordinal
ordering, no duplicate or case-colliding names, exact lengths and SHA-256
values, exact inclusion of the designated executable, and no trailing bytes.

After authentication and parsing, the composite opens the exact
`TrustedArtifactSetLease` described by the entries. It compares the manifest's
artefact-set digest with `TrustedArtifactSetLease.CopyManifestSha256`, then runs
one final `RevalidateExactSet`. The successful composite retains the exact
member identities, validated manifest pin, and artefact-set digest. Copies
returned to callers are independent. `IsEligibleForTrustedLaunch` is always
false. On any failure, the composite disposes a partially opened set and wipes
its owned manifest, pin, and temporary digest copies.

Keep the release manifest out of the protected application directory and exact
artefact set. Including it would create self-reference and an unexpected entry.
The caller supplies the out-of-band manifest bytes and owns the pin provenance.
A sibling manifest, a pin derived from that manifest, or a pin compiled into an
artefact covered by the same circular policy does not establish independent
trust. The seam supplies no signature, release provenance, freshness, rollback
protection, trusted installer policy, member file ACL, shared-runtime trust,
loader atomicity, launch integration, production role, private handoff, role-
bound `READY`, Java integration, or HRC runtime evidence. One absolute deadline
and caller cancellation govern cooperative checks. They do not hard-preempt
blocking native calls.

Legacy harness-containment checkpoint `2a56de1` uses
`ContainedHarnessProcess`. It launches exactly the current generated harness
apphost in one of two fixed public modes: `Exit` or `Block`. It supplies an exact
non-null `lpApplicationName`, a fixed command line, an empty Unicode environment,
the current executable directory, no inherited handles, and no standard I/O
handles. These join three legacy IPC child modes, for five fixed public child
modes in total. It creates an unnamed, non-inheritable Job Object and requires
exact read-back of `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`.

The launcher assigns the Job with `PROC_THREAD_ATTRIBUTE_JOB_LIST` during
suspended `CreateProcessW`. Before exact `ResumeThread`, it requires one exact
Job PID and verifies a retained `ProcessIdentityLease` plus the exact image
path. One caller-supplied absolute monotonic deadline and cancellation token
govern cooperative checks around synchronous native launch calls. They do not
hard-preempt them. A post-resume deadline check rejects late success.

On start failure and disposal, the launcher closes its last Job handle before
it waits for the exact retained process under a separate fixed five-second
cleanup bound. Concurrent disposal coalesces. This primitive launches no
production role and accepts no caller-defined mode, argument, environment,
working directory, or standard handle.

`HrcJobObserver.NativeFixture.exe` is a separate project-owned, test-only AMD64
PE. Its C source uses no header or C runtime. It imports exactly
`GetCommandLineW`, `ExitProcess`, and `Sleep` from `KERNEL32.dll`. It accepts
only `--native-exit` and `--native-block`; any other argument exits with code
`87`. The Exit role returns `0`. The Block role sleeps indefinitely and is used
only by the audited native-containment tests.

The fixture embeds an exact 510-byte neutral-language Windows manifest. It has
one `amd64` `win32` identity named `HrcBetaAutomation.NativeFixture`, version
`1.0.0.0`. Its requested execution level is `asInvoker` with
`uiAccess=false`. It contains no dependent assembly or file element. The build
checks the XML topology and validates it with the recorded Windows SDK manifest
tool before linking it as resource type `24`, identifier `1`, language `0`.

The build invokes the recorded MSVC `14.44.35207` and Windows SDK
`10.0.26100.0` paths in a cleared environment. It supplies only `SystemRoot`,
`WINDIR`, a closed `PATH`, and build-local `TEMP` and `TMP`. Each native tool
has a 60-second execution bound and a separate 10-second kill-and-wait cleanup
bound. The two builds use distinct empty temporary and output directories. LF
line endings are pinned for the C, RC, and manifest inputs. `/NODEFAULTLIB`,
the explicit entry point, `/Brepro`, `/CETCOMPAT`, and
`/DEPENDENTLOADFLAG:0x800` are fixed link inputs. The two 4,096-byte outputs
must be byte-identical before one becomes the test fixture.

The PE header records Windows GUI subsystem version `6.02`. This is an image-
compatibility value, not the complete runtime contract. Windows supports
`DependentLoadFlags` only from Windows 10 RS1. Treat RS1 as the minimum runtime
floor despite the lower subsystem version. The licensed Windows 11 host
satisfies that floor; no cross-machine runtime result is claimed.

`NativeFixturePeAudit` owns bounded copies of the image, exact manifest, and
caller-supplied SHA-256. It authenticates the image digest in fixed time before
structural parsing. The exact pinned-host golden SHA-256 is
`3c9bee49acfffaea7f3fae2692900b47eef0e41e61e4ae7b14e2b1884a05fe34`.
The exact REPRO identity is
`3ba123e6d4167f80d4f2e48f9e4eb33f2e58547e66f7ac1ac9da2692de334c5b`.
These values record checkpoint `fb9ba23` on the pinned host. They do not prove
toolchain or signer provenance and do not guarantee a cross-machine rebuild.

The structural audit requires PE32+ AMD64 headers and exact `.text`, `.rdata`,
`.pdata`, and `.rsrc` sections. No section may be both writable and executable.
It checks every data-directory entry, exact `KERNEL32.dll` import metadata,
matching import lookup and address slots, exact names and hints, and no ordinal
import. The 0x148-byte load configuration must contain only
`DependentLoadFlags=0x0800`; `GuardFlags` and all other admitted fields remain
zero. The audit checks the exact COFFGRP, REPRO, and extended-DLL-
characteristics debug records, neutral manifest resource and bytes, exception
and unwind record, and PE checksum. All raw section ranges must be contiguous
through end of file. Certificates, base relocations, raw gaps, and overlays are
forbidden.

The bounded runtime test starts Exit and an invalid argument with no shell or
redirected standard handles. Its environment contains only `SystemRoot`,
`WINDIR`, System32-only `PATH`, and build-local `TEMP` and `TMP`. It confirms
exit codes `0` and `87`. A timeout kills the retained exact process and waits
again under a separate bound. At historical checkpoint `fb9ba23`, the Block
role was not started because native Job containment was not integrated yet.

Checkpoint `64043e5` adds `AuditedNativeFixtureReleaseLease` and passes 102/102.
The composite accepts only the exact native release-manifest profile. It copies
the 4,096-byte executable through the retained file handle, audits that owned
snapshot, binds the audit SHA-256 to the authenticated manifest entry, and
performs final exact-set revalidation. It retains the pinned release and PE
audit together. It remains ineligible for trusted launch. The caller still
owns the out-of-band manifest pin's provenance.

Committed checkpoint `70e0d77` adds
`ContainedAuditedNativeFixtureProcess`. The wrapper
launches only that audited one-file no-CRT fixture in the closed Exit or Block
mode. It requires Windows 10 version 1709 build 16299 or later. It validates
the x64 `CREATE_PROCESS_DEBUG_INFO` and `DEBUG_EVENT` ABI, requires an AMD64
process through `IsWow64Process2`, and rejects every other platform result.

The wrapper creates one unnamed, non-inheritable Job Object. It applies and
reads back exactly `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`. It supplies the Job
through `PROC_THREAD_ATTRIBUTE_JOB_LIST` at process creation and later requires
one exact Job PID. It passes no inherited or standard handles and uses an empty
Unicode environment.

Process creation uses the exact canonical DOS executable path as non-null
`lpApplicationName`, command line argument zero, and the exact canonical DOS
application directory as the working directory. A direct volume-GUID
`CreateProcessW` attempt was rejected on this licensed host. The wrapper
therefore launches through the canonical DOS locator while retaining and
checking its volume-GUID identity. This is a host observation, not a general
Windows compatibility claim.

`TrustedArtifactLaunchNamespaceLease` opens every directory from the fixed-
drive root through the application directory. Each handle omits
`FILE_SHARE_DELETE`, rejects reparses, and binds its final DOS path, volume-GUID
path, volume serial number, and 128-bit `FILE_ID`. The retained executable
handle continues to deny new data-write and delete access. Revalidation checks
the complete ancestor chain and executable around every critical launch step.

The wrapper creates the process with `DEBUG_ONLY_THIS_PROCESS` so that the
initial `CREATE_PROCESS_DEBUG_EVENT` arrives before user mode. It requires the
exact created PID and TID and compares the event's process and thread handles
with the `PROCESS_INFORMATION` handles. It adopts the event's direct image-file
handle. That handle must match the retained executable's exact length, SHA-256,
128-bit `FILE_ID`, volume serial number, and volume-GUID path. This handle binds
the main image only.

Committed checkpoint `2512c6a` closes that event handle, continues the create-
process event, and pumps real startup `LOAD_DLL` events. Every startup event
must identify the exact created PID and initial TID. The pump admits at most 32
events. Exact initial-thread enforcement and the 32-event cap are fail-closed
host and fixture compatibility policy. They are not general Windows loader
contracts.

Exactly one debugger-supplied `KERNEL32` `LOAD_DLL` file handle must match the
contemporaneously retained native System32 `kernel32.dll`. The comparison
covers file identity, length, volume serial number, volume-GUID path, and
SHA-256 bytes. The wrapper duplicates and retains that matching debug-event
handle. It rejects a second match.

The exact initial first-chance breakpoint is the startup barrier. The wrapper
calls `SuspendThread` while that event is outstanding and requires prior count
`0`. It then continues the breakpoint, detaches the debugger, and requires
`CheckRemoteDebuggerPresent=false`. After final identity, namespace, exact-set,
system-module, AMD64, platform, and liveness checks, `ResumeThread` must report
prior count `1`. A final deadline check rejects late post-resume success.

Follow-up checkpoint `cc77b9b` makes every post-creation failed launch
explicitly call `TerminateJobObject` with the unique nonzero failed-launch code
`0xE0435243`. It then closes the last Job handle before it continues any
outstanding debug event. The `AfterInitialBreakpointOwned` fault uses the Exit
role and observes that exact forced code instead of its natural exit code `0`.
This directly closes the former pre-entry cleanup window.

The complete create, debug, detach, and initial-resume transaction runs on one
fresh dedicated operating-system thread. `ExecutionContext` flow is suppressed
before it starts, and the caller joins it non-abandonably. Debug cleanup is
creator-thread-affine and non-abandonable. It continues owned events, closes
typed debug-event file handles, and resolves detach or exact process exit
before it can return.

After debugger detachment, failure and disposal close the last Job handle and
wait for the exact retained process under a fixed five-second bound. If that
wait cannot prove exit, `NativeFixtureProcessReaper` retains the exact process
handle, namespace, audited release, expected System32 identity, and loaded-
module evidence until the exact handle signals. It does not use PID lookup,
cancellation, a deadline, or another termination action. The wrapper retains
both system-module authorities through its process lifetime. If
`WaitForSingleObject` fails, the reaper retains all authority indefinitely and
records terminal uncertainty. The build wrapper applies a separate 180-second
watchdog to the complete .NET validation process.

This is exact synthetic fixture evidence only. The initial breakpoint is not a
direct entry sentinel. Neither debug-event file handle proves section, mapping,
or executed-page identity. The evidence proves no KnownDLL, Microsoft, or
signer provenance, no global System32 namespace closure, and no general loader
or dependency closure. The application directory still permits new child
creation. A new-child ABA is harmless under this exact one-file fixture policy
because the audited image has no app-local dependency. This does not
generalise to another image. The wrapper supplies no trusted installer or
manifest-pin provenance, trusted or production launch, production role,
private handoff, role-bound `READY`, Java integration, or HRC runtime evidence.

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

This module has three synthetic legacy IPC child modes, including the persistent
observer and controller modes used for the four broker exchanges. Those
ordinary broker and pipe library paths do not launch processes. The legacy
`ContainedHarnessProcess` source launches only the current generated harness
apphost in either of its two fixed containment modes. All five modes are public on the
process command line. Control input for the three IPC modes contains only public
process identifiers, pipe names, descriptor and test values, and bounded
delays. The token travels only on protected protocol pipes. The cleared child
environment contains no secret. Each child writes no standard output or error.

The file seam does not resolve a Windows known folder. It does not provision or
prove a LocalAppData hierarchy, recover stale or crash-left publications, or
deliver an initial pipe name securely. It does not authenticate a future
executable by hash or connect to the Java transport. The persistent roles are
test modes in one harness executable. They are not separate production role
executables. The module contains no HRC path, component, private configuration,
licence data, poker data, network client, registry access, or environment-secret
input. The fixed file contains only the public canonical descriptor, not the
bearer token.

The protected set and pinned manifest bind only caller-declared synthetic
app-local files. The manifest pin is independently supplied only as an API
condition; the caller must establish its provenance. Neither primitive binds or
selects a shared .NET runtime. They provide no member file ACL, signature,
freshness, rollback protection, installer trust, launch atomicity, launched-
process identity, production role executable, production-role containment
integration, private handoff, role-bound `READY`, Java integration, or HRC
runtime evidence.

The fixture's embedded Windows manifest is not a native `HRCREL01` release-
manifest binding by itself. The separate native profile, retained-handle PE-
audit composite, and containment wrapper remain explicitly ineligible for
trusted launch. The source guard and structural audit do not prove machine-code
semantics. The image has no Control Flow Guard instrumentation, and
`/CETCOMPAT` does not prove Control-flow Enforcement Technology enforcement.
The build records tool paths but supplies no toolchain or signer provenance.
The import and load-policy profile does not prove System32 or KnownDLL module
identity. The debug event supplies no kernel section-object identity or direct
entry sentinel. The fixture supplies no production role, private handoff,
role-bound `READY`, Java integration, or HRC runtime evidence.

The DACL admits the bound account and `SYSTEM`; exact peer identity is checked
after connection. A same-account process that discovers the pipe name could
therefore connect first and cause denial of service before being rejected.
Random naming, secure name handoff, and lifecycle ownership remain required.

One synthetic child test asserts distinct parent and child PIDs and nonzero
creation identities. Each endpoint validates the other endpoint's complete
process binding before exchanging fixed public request and response bytes. A
second test keeps the expected child live while a distinct wrong child connects
and confirms server-side rejection.

The legacy IPC path selects one of three fixed child modes. When launched
through `dotnet.exe`, the absolute harness assembly path is also a public host
argument. Redirected input carries the role command. The child environment is
cleared except for minimal .NET host controls. The native containment path adds
two fixed apphost-only modes and does not redirect standard handles. Redirected
IPC output is counted without being recorded. A successful IPC child must write
zero bytes. Normal cleanup is explicit and awaited. Test-failure disposal
performs
kill-and-bounded-wait cleanup through the retained process object and fails if
termination is not confirmed. This is not kill-on-close containment and does
not prove cleanup after abrupt parent termination.

The legacy harness-containment primitive from checkpoint `2a56de1` uses a
native suspended apphost launch. Its tests cover normal exit, explicit last-
Job-handle closure that kills a blocking child, no managed child-entry event
before an injected pre-resume fault, late-deadline
cleanup after resume, and coalesced concurrent disposal with an admitted
`WaitForExitAsync`. The suite does not terminate its parent abruptly. Windows
kill-on-close semantics support cleanup when the final Job handle closes, but
direct abrupt-parent-death and crash behaviour remain unexercised. The managed-
entry assertion applies to this legacy apphost checkpoint, not the audited no-
CRT fixture.

The containment proof is not integrated with `TrustedArtifactSetLease`, the
broker roles, or token transport. It has no independently trusted release
provenance, shared-runtime or loader trust, production roles, private handoff,
role-bound `READY`, Java or HRC integration, sandbox, or same-user hostile-
process defence. The protected artefact-set root still permits new child
creation and remains snapshot and detection only.

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
HoldemResources symbols. Its launch rule rejects managed `ProcessStartInfo` or
`Process.Start` outside the legacy test-harness `Program.cs`. It admits exactly two native `CreateProcessW` call sites in production source:
`ContainedHarnessProcess` and `ContainedAuditedNativeFixtureProcess`. The scan
is not a broad native-launch ban.

Before the .NET validation, the script validates the exact native source,
resource definition, and manifest. It runs the recorded compiler, linker,
resource compiler, and manifest tool with the closed environment and bounded
cleanup described above. It builds the native fixture twice, requires literal
byte equality, and runs its Exit and invalid-argument roles. The .NET harness
then authenticates and audits the resulting PE before its bounded runtime test.

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

Six pinned release-manifest cases cover exact owned-copy retention and final
revalidation, authentication before structural parsing, noncanonical wire
rejection, protected artefact-set digest binding with failure cleanup, one
absolute operation budget, and a fixed golden identity.

Seven native-fixture cases cover exact authenticated PE ownership; header,
section, directory, import, load-policy, debug, resource, exception, checksum,
and overlay rejection; and bounded Exit and invalid-argument runtime. Mutation
tests recalculate the PE checksum and caller digest so each structural guard is
tested beyond digest authentication. Historical checkpoint `fb9ba23` did not
launch the source-defined Block role before containment integration.

Seven audited native-release binding cases cover the two closed manifest
profiles and golden identities, exact retained-handle byte-copy ownership,
late-failure wiping, authenticated PE-audit binding and revalidation, and
partial-failure cleanup. Checkpoint `64043e5` passes 102/102 after adding these
cases to the historical 95.

Five legacy harness-containment cases cover normal exit, explicit Job-close
termination of a blocking child, a pre-resume fault, post-resume late-deadline
cleanup, and coalesced concurrent disposal with exact-process exit observation.

The existing five audited native-containment cases now cover the extended AMD64
debug ABI, exact startup order, all 13 injected launch stages, pre-resume and
post-resume late deadlines, retained evidence revalidation, the forced pre-
entry failure exit, and the prior containment and disposal behaviour. Baseline
and final reaper assertions show only that no retained or terminal reaper state
remained at each assertion time. They do not prove that the reaper was never
used.

The result before containment was 77/77. Checkpoint `2a56de1` passed 82/82 after
adding 5 legacy harness-containment tests. Checkpoint `d4cd474` passed 88/88: 20 primitive
tests, 8 descriptor and protocol tests, 27 broker and in-memory-store tests, 11
filesystem tests, 5 artefact-identity tests, 6 protected app-local artefact-set
tests, 6 pinned release-manifest tests, and 5 containment tests. Checkpoint
`fb9ba23` passes 95/95 after adding 7 native-fixture tests. Checkpoint
`64043e5` passes 102/102 after adding 7 audited native-release binding tests.
Committed checkpoint `70e0d77` passes 107/107 after adding 5 audited native-
containment tests. Committed checkpoint `2512c6a` extends those same 5 cases
with real startup module-load evidence. Follow-up checkpoint `cc77b9b` closes
the failed-launch pre-entry cleanup window. Release validation passes 110/110
on the exact `cc77b9b` snapshot, with no native-fixture child left running.
The 3 native system-module identity cases were added between `70e0d77` and
`2512c6a`. This is offline
Windows model, codec, primitive, publication-seam, artefact-identity, artefact-
set, pinned-manifest, synthetic-broker, test-harness containment, and native-
fixture evidence only.

Define a trusted installer or release policy that supplies canonical manifest
bytes and independent pin provenance. Keep the existing containment proof
separate until dedicated production roles integrate it. Close production
namespace and complete production runtime-module, loader, and dependency
closure before private initial name handoff and role-bound `READY`.

Still unvalidated: production observer, broker, and controller executables;
secure pipe-name delivery; known-folder resolution; LocalAppData hierarchy
provisioning and provenance; stale and crash recovery; production descriptor
persistence; a trusted installer or release policy that supplies canonical
manifest bytes and independent pin provenance for each complete production
artefact set; and production-role containment integration. Still unvalidated
are trusted pin provenance, production namespace, complete runtime-module and
dependency closure, a direct abrupt-parent-death containment test, private
handoff and role-bound
`READY`, Java integration, OSGi startup, installation, rollback, HRC runtime
use, and every standalone-runner action.
