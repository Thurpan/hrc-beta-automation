# HRC Beta Automation

## Purpose

This project establishes one dependable local automation workflow for HRC Beta.
The workflow must use observable controls and states on the licensed Windows
computer.

## Ownership and collaboration

This is Euan's personal project. It is not organised as a community project and
does not have a general contribution process.

Anyone may fork the repository under the MIT Licence. If you want to help with
this repository, contact Euan before starting. Do not open a pull request
without agreeing the work with him first.

## Current phase

The project is in accessibility inspection and first-workflow validation. The
licensed host is `EM-3960X`, which reports an AMD Ryzen Threadripper 3960X
processor.

Euan attests that HRC Beta's owner personally authorised any use required by
this project provided it remains non-commercial. Euan confirms that this
project is personal and wholly non-commercial. Feasibility work may therefore
resume within that scope. The immediate implementation target is a minimal
startup observer designed to expose exact terminal results to a standalone
controller with bounded replay scoped to one observer process and session. It
does not preserve events across observer restart.

The earlier 5950X portfolio reference was incorrect for this host. Local
machine evidence and Euan's licence confirmation supersede that reference.

## Boundaries

- Keep all HRC work personal and non-commercial under Euan's attested oral
  permission from HRC Beta's owner. The current
  [HRC v4+ EULA](https://www.holdemresources.net/legal/eula/hrc_v4) licenses the
  Product in unmodified binary form, limits usage to the provided launcher,
  permits component use only through the user interface accessible via the
  provided launcher, and prohibits
  automated UI or memory scraping and reverse engineering without consent,
  subject to applicable law.
- Prove one small non-overwriting workflow before broader design.
- Do not create unattended automation during feasibility discovery.
- Do not use blind coordinate clicks.
- Do not overwrite or delete existing HRC data.
- Do not expose licence data, poker data, or other sensitive information.
- Do not install software or start an expensive calculation unless Euan has
  authorised the current run or batch.
- Do not choose a standalone runner language or framework before feasibility is
  proven. A minimal, uninstalled exact-status observer is allowed as a
  feasibility instrument because no external state can distinguish Nash
  success from cancellation. Keep it separate from the future runner.
- Keep HRC-required JavaScript tree-building candidates separate from
  application automation.

## Required representative workflow

Use this lifecycle for each simulation:

1. Create the tree for the next setup in the simulation run order. Select the
   required table size, overwrite every active seat's stack, and read back the
   exact position order and values. Do not rely on a new setup or a table-size
   change to reset prior inputs. If the final stack commit opens the next blank
   row, cancel that editor and verify that no extra player was added.
2. Rename the hand through `Hand` → `Rename Hand`. Use names such as `HU-1` or
   `5m-10-30-30-20-12.5`. Compare hand-tab base names independently of their
   leading dirty `*`: require the requested base to differ from the active base
   and to be absent from every open hand-tab base before renaming.
3. Queue a full-tree Nash calculation with `HRC 4.0 (Default)`. Run until the
   confidence interval (CI) reaches `10.0`. Keep Reset Regret and Reset
   Strategies clear.
4. Queue a second full-tree Nash calculation with `HRC 4.0 (Default)`. Run
   until CI reaches `1.0`. Select Reset Strategies and keep Reset Regret clear.
5. Queue a Viewer Save under `\\VAULT\sims\Preflop\<table-group>` using a new
   high-entropy filename inside a validated, exclusively owned staging
   namespace. Save As can retain the previously
   selected type or open with `*.hrcz Complete Save`. Before every save, verify
   the destination, select `*.hrcv Viewer Save`, and confirm the exact lowercase
   staging filename and `.hrcv` extension. After identity-matched Job success
   and new/non-empty/stable staging metadata, promote it with fail-if-exists
   semantics to `<simulation-name>.hrcv`. Example folders include `HU` and `5m`.
6. After the queued operations finish successfully, export the strategies
   through `Hand` → `Export Strategies`. Use `Complete Export`, Depth `16`,
   clear `PrettyPrint JSON`, and set `Node Filter Threshold %` to `0.1`. Save to
   a new high-entropy staging filename in the same exclusively owned namespace.
   Require the exact two-filter list, select/read `*.zip Archived Json`, and
   verify the lowercase `.zip` staging filename. After identity-matched Job
   success and new/non-empty/stable staging metadata, promote it with
   fail-if-exists semantics to `<simulation-name>.zip`. In the inspected
   calculator plug-in `4.1.1`, Complete Export is
   unlimited-depth and does not consume the visible Depth setting; still set
   and read back `16` to match the required operator workflow. The visible ZIP
   type is not a sufficient format oracle: an installed retained-index defect
   can write plain text to the `.zip` path while the Job reports success. A
   ZIP-only dialog is a stop condition.
7. Move to the next simulation and repeat the workflow.

For Rename uniqueness, normalise each hand-tab base by removing at most one
leading dirty `*`, then one terminal `.hrcv` or `.hrcz` suffix
case-insensitively; do not remove embedded suffix text. Compare bases with
ordinal case-insensitive semantics. A requested simulation name has no HRC
suffix, matches `^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$`, does not end in `.`, and is
not a Windows reserved device base (including before a dot). This policy covers
the installed `100` UTF-16-unit limit and the output filename constraints.

Before step 1, reserve and verify that neither canonical target
`<simulation-name>.hrcv` nor `<simulation-name>.zip` exists. Stop and choose a
new unique simulation name if either exists. Atomically reserve a private staging
namespace under the table-group folder, validate its exclusive ownership, and
generate absent high-entropy names inside it. Acquire a validated system-wide
HRC-control lease before the first automated HRC input and hold it through both
canonical promotions, target-tab closure, and final state verification. The
lease must exclude another runner, all manual HRC interaction, a second HRC
process, and other automation in scope; atomic reservation and high entropy alone
are insufficient. Recheck the staging target immediately before the
corresponding HRC Save, and recheck the canonical target immediately before
fail-if-exists promotion. If either exclusivity guard is unavailable, HRC shows
an overwrite prompt, or any unexpected Job, dialog, file, or input transition
appears, select Cancel when safe and stop; never replace an existing output.

Installed-component inspection shows that those checks do not close the final
write race: Viewer Save finishes with replace-existing semantics, and Complete
Export opens its final export target with create-and-truncate semantics. The
standalone runner must therefore keep HRC away from the canonical final names.
It must write each output inside a validated, exclusively owned staging
namespace. Only after the identity-matched HRC Job succeeds and the staged file
is new, stable, and non-empty may it promote to the exact simulation filename
with a fail-if-exists operation. That publication design remains an
implementation-gate blocker. Do not inspect ZIP contents, delete a partial file,
or claim overwrite prevention from preflight alone.

The export-format defect needs a separate live guard. Under the current no-
content-inspection rule, neither a `.zip` extension, non-empty metadata, nor a
successful Export Job proves that the file is an archive. Installed inspection
shows that a fresh Hand Setup hand remains on the two-filter helper after Nash;
accepting the actual staging export with ZIP selected updates the hidden index
before Job submission. Cancel does not update it. Do not consume the reserved
smoke until the two-filter list, selected ZIP value, and accepted staging route
are safely readable through the standalone design.

All installed-component findings are conditional on the exact eight-component
filename and SHA-256 set recorded in `docs/feasibility.md`, resolved from the
active `hrc.exe` process's own installation `plugins` directory. The set covers
the calculator, NatTable, JFace, SWT, Commons Compress, Eclipse Core Jobs,
Eclipse UI, and Eclipse UI Workbench. The application version itself remains
unconfirmed. A hash match is necessary but does not prove live focus, retained
preferences, or runtime state. Any process, path, filename, or hash mismatch
must stop the runner and reopen feasibility validation.

After tree creation, submit steps 2 through 5 without waiting for the previous
operation to finish. The two Nash calculations can take a long time. Wait for
these queued operations to finish successfully before step 6.

Before the workflow, treat every pre-existing hand-editor tab as protected and
snapshot its stable identity, title, and dirty state. After step 6, verify the
canonical Viewer file and intended strategy archive whose ZIP mode was proved by
the two-filter/selected-ZIP guard. Both canonical files must exist and must not
be empty. Immediately before closure, require the exact hand-editor set to equal
the protected identities plus exactly one expected completed simulation. Then
close only that completed tab before step 7. HRC shows a `Save Resource` prompt
because Viewer Save does not save the editable hand. Confirm that the prompt
names the expected completed simulation. Only then select `Don't Save`; require
the post-close set to equal exactly the unchanged protected identities, with no
addition or replacement. Explicitly activate `Home` and verify its page. Stop on
any filename, prompt, tab-set, or Home-state mismatch.

Do not treat a visible name or ordering as an exact queue identity. Both Nash
Jobs use the same public `<hand-name>: Monte Carlo Sampling` name. Installed
inspection proves that same-hand Jobs serialise, but successful and cancelled
items can both disappear into the same idle Progress state. The current UI-only
architecture therefore cannot meet the terminal-success requirement. Stop the
workflow if any operation fails or lacks an identity-matched successful result,
if either output cannot be verified, or if the completed tab cannot be closed
safely.

No later hand or file state repairs this gap. Installed inspection shows that
achieved CI is job-local; successful, cancelled, and failed runs all retain
incremental strategy/sample state, update the hand, mark it dirty, and trigger
the same editor refresh. Viewer Save serialises that current state without
checking predecessor results. Sample count, dirty state, tooltip text, Viewer
metadata, and command state are therefore not Nash-success oracles.

## HRC tree-building candidates

Two standalone, offline-reviewed candidates live under `scripts/hrc/`:

- [`tree-building-3m-6m-candidate.js`](scripts/hrc/tree-building-3m-6m-candidate.js)
  for non-straddled configurations with three through six players; and
- [`tree-building-hu-candidate.js`](scripts/hrc/tree-building-hu-candidate.js)
  for a true two-player configuration.

They use separate preflop policies from the sizing workbook and a shared
postflop policy. The pre-conversion HU candidate loaded successfully and
created a `1 bb` heads-up tree on `EM-3960X`. A `2 bb` follow-up used the
shallow-completion fix from `9b24166`. HRC reported two nodes. During a later
inspection of that revision, expanded Preview showed `R 2.00 SB PRE` with
exactly one child, `C 1.00 BB PRE`; no SB completion branch was present. This
directly confirms that revision's below-cutoff behaviour at equal `2 bb`. Its
SHA-256 was
`8fc4d2d79aefee249db4ea3cbecb2516f19b7a2bfbfcf85f3f12a6e23e54db6a`.
The current HU candidate has SHA-256
`e127ed9285d4f77253ad3c9ad3ac45afdb105f7d930ed3c45208d604fce845ec`.
The exact worktree file and hash were verified before a later supervised load.
HRC showed the expected basename without `[Errors]`, reported two nodes, and
expanded Preview showed the same `R 2.00 SB PRE` → `C 1.00 BB PRE` path with no
SB completion branch. This directly revalidates the current candidate at equal
`2 bb`. The inclusive `5 bb` boundary and the first supported stack above it
remain unverified for HU. Multiway HRC evidence is limited to the
representative five-player paths described below; other stacks, table sizes,
boundaries, dynamic post-fold cases, later streets, and unexpanded branches
remain unverified.

A short HU demonstration covered rename, both Nash submissions, an accidental
Complete Save, a corrected Viewer Save, and output verification. Later runs
created non-empty `.zip`-named strategy-export files and verified Viewer-only
tab closure. The supervised `HU-2` run created non-empty `.hrcv` and `.zip` files,
created no matching `.hrcz` file, and returned to `Home` after `Don't Save`.
Long-run queue behaviour, completion or failure detection, and the remaining
tree policy are unverified. A supervised five-player setup displayed the visible
order `HJ`, `CO`, `BU`, `SB`, `BB`, showed stacks of `10`, `20`, `30`, `40`,
and `50` bb, and advanced to Betting Setup. The HRC-tested pre-correction
candidate then stopped with `Error: Effective stack does not match a configured
workbook column: 100000`; Finish remained disabled. Its SHA-256 was
`128110cc73abd5bfd45167d426935e8d43923ae8648deffbc0251f4d03178782`.
The reported amount is the supported `10 bb` stack in HRC units. The corrected
candidate converts state values with the nominal big blind and has SHA-256
`fa2612bd1d3b01a8aa6419fc3697450cf708adff73fc6d085e2223ff605d7c63`.
Offline regression tests pass. After Euan reported loading the corrected
candidate, a live capture showed its basename without `[Errors]`. HRC reported
`1815589` nodes and `12.3GB`, and enabled Finish. The capture did not expose the
full loaded path. Path-scoped Preview inspection covered the visible root
actions and representative opening, 3-bet, 4-bet, 5-bet, squeeze, call-cap,
SB-completion, and low-SPR flop paths. The displayed values matched the
candidate's workbook-derived policy for this `10/20/30/40/50 bb` five-player
setup. This is not exhaustive validation of the tree. Other stacks, table
sizes, boundaries, later streets, Finish, calculations, and output remain
unverified.

## Current next action

The owner-authorisation gate is satisfied by Euan's direct attestation. The
official HRC v4+ EULA licenses the Product in unmodified binary form, limits
usage to the provided launcher, permits component use only through the UI
accessible via that launcher, expressly prohibits automated scraping of the UI
or memory, and prohibits decompilation, reverse engineering, or modification
without the licensor's consent, subject to applicable law. The published
scripting API is limited to tree-building decisions and exposes no Nash, Job,
save, export, or terminal-status callback. An exhaustive 12 August 2026 review
of the official documentation index and its ten listed articles, full
changelog, public scripting Javadoc, FAQ/download material, and relevant
release posts found no public supported lifecycle API, CLI/headless mode,
unattended scheduler, or durable calculation-status log. This is absence from
the reviewed public corpus, not proof that a private or partner interface does
not exist.

The project now has an
[offline exact-status correlation core](src/HrcJobObserver/README.md), an
[offline Eclipse Jobs adapter](src/HrcJobObserver/eclipse-adapter/README.md),
an [offline bearer-token loopback transport](src/HrcJobObserver/local-transport/README.md),
and an
[offline ordered runtime assembly](src/HrcJobObserver/runtime-assembly/README.md).
An [offline OSGi lifecycle owner](src/HrcJobObserver/osgi-lifecycle/README.md)
tests manager registration and ordered cleanup behind a disabled activator. An
[offline simpleconfigurator planner](src/HrcJobObserver/osgi-packaging/README.md)
produces an in-memory proposal only. An
[offline Equinox start-level fixture](src/HrcJobObserver/equinox-startlevel-fixture/README.md)
tests listener-before-producer ordering in isolated fresh JVMs. An
[offline Windows bootstrap module](src/HrcJobObserver/windows-bootstrap/README.md)
tests owned 32-byte secret-buffer generation and wiping, exact applied pipe-
DACL read-back, two-sided process identity, bounded one-shot framing, synthetic
distinct-process frame exchange, rejection of a wrong live child, a canonical
HMAC-bound endpoint descriptor, and eight phase- and role-bound bootstrap
messages. It also tests a capacity-one ABA-safe in-memory publication store and
a one-shot broker across the broker harness process and long-lived synthetic
observer and controller child modes. The in-memory store implements an
asynchronous publisher contract. Successful publication returns a store-affine
lease that coalesces exact removal. The module also contains an internal file
publisher and an independent reader for a caller-supplied, already-existing
protected local NTFS directory. This is an offline existing-directory seam, not
production descriptor persistence. A separate artefact-identity primitive
retains and verifies one caller-supplied local file. A protected app-local
artefact-set primitive composes those retained identities into one exact
directory snapshot. An out-of-band pinned release-manifest seam admits exact
synthetic framework-dependent and one-file no-CRT native `win-x64` profiles.
An audited composite binds the native profile, retained executable bytes, and
strict PE audit. A dedicated containment primitive launches only that exact
synthetic fixture through atomic kill-on-close Job assignment and a pre-user-
mode debug-event image-handle check. Every layer remains ineligible for trusted
launch. The existing internal test-harness containment primitive remains
separate. The
broker executes all four exchanges, serialises claim and revoke, rejects an
already-completed malformed loser, caps
the publication budget by the remaining absolute session deadline, and wipes
its token copies before the final or revocation acknowledgement. Its
asynchronous disposal coalesces cancellation and non-abandonable cleanup.
`RunAsync` remains the authoritative protocol-failure channel; `DisposeAsync`
separately reports cancellation-request and cleanup failures. A faulted or
unknown removal cannot claim absence. Removal verified only after its deadline
still fails the session before terminal acknowledgement. The deadline checks
are cooperative and do not hard-preempt an arbitrary blocking native call. The
current suites pass 30 core tests, 34 adapter tests, 25 transport tests, 10
joined-assembly tests, 14 lifecycle tests, 13 packaging tests, and 110 Windows
bootstrap tests. Committed checkpoint `64043e5` passes 102/102. Committed
checkpoint `70e0d77` adds 5 audited native-containment cases and passes
107/107. Committed checkpoint `2512c6a` extends those cases with real startup
module-load evidence. Follow-up checkpoint `cc77b9b` closes the failed-launch
pre-entry cleanup window. Checkpoint `8bd853a` adds a standalone three-member
startup module aggregate within the same 3 module tests. Its initial profile
was falsified at integration: four containment cases rejected an unexpected
fourth `LOAD_DLL` event before identity validation, so the Release result was
106/110. A separate bounded, read-only `Process.Modules` probe implicated
System32 `apphelp.dll`. Checkpoint `445d02a` closes the host-observed profile
over NTDLL, KERNEL32, KernelBase, and Apphelp. Its later runs directly bind the
fourth debugger `hFile` to the current System32 `apphelp.dll`. Three consecutive
direct Release runs pass 110/110 on that exact checkpoint, with no native-
fixture child residue after any run. The Windows
total is 20 primitive tests, 8 descriptor and
protocol tests, 27 broker and in-memory-store tests, 11 filesystem tests, 5
single-file artefact-identity tests, 6 protected app-local artefact-set tests,
6 pinned release-manifest tests, 7 native-fixture tests, 7 audited native-
release binding tests, 5 harness-containment tests, 3 native system-module
identity tests, and 5 audited native-containment tests. The start-level fixture
passes 12/12 prerequisite tests,
18/18 recorded-row tests, and 9/9 observer-failure tests.

The transport implements bounded protocol version `1`, validates cursor-bound
checkpoint replay, and serialises only allow-listed event primitives. The
offline assembly now provides the real ordered `ObserverTransportControl`:
callbacks, checkpoints, and arms share one mailbox sequence. A second post-arm
marker drains callbacks admitted around an arm, verifies request ownership,
and starts a new observer-local lease. Every successfully confirmed exact
idempotent retry renews that lease. `ARM_CONFIRMED` records each confirmed
lease. The joined tests exercise this control through an actual loopback socket
in one JVM. The response is not yet authority for HRC input. The future
controller must enforce a local round-trip and pre-input margin within the
lease. The lifecycle implements synthetic manager registration, two bounded
baseline scans, startup callback admission, rollback, and ordered shutdown. Its public
activator remains deliberately disabled. The project still does not implement
Windows known-folder resolution, protected LocalAppData hierarchy provisioning
and provenance, stale or crash recovery, secure initial pipe-name delivery,
dedicated production observer, broker, and controller executables, a
trusted installer or release policy that independently supplies canonical
manifest bytes and pin provenance for each complete production artefact set,
production release integration and module-identity closure, a direct abrupt-
parent-death containment test, an activatable manifest, controller ownership,
private handoff, role-bound `READY`, Java-to-Windows integration, or persistence
across restart.
The offline adapter, runtime, and lifecycle builds read and hash public provider
JARs from the HRC installation. A separate read-only inspection supplied the
configuration facts to the in-memory planner. None of these layers has
interacted with the running HRC process, its UI, or real Eclipse callback
delivery. They do not yet make
HRC terminal results available to a controller and do not change the
feasibility verdict.

The read-only static audit records the calculator at level `5,false`, after a
proposed level-4 observer, on the normal clean-launch route to framework level
6. Only the exact calculator archive among all 191 configured artefacts and
their embedded JARs defines or literally refers to the Nash, Viewer Save, and
Export Job classes. Their exact class hashes are recorded in
`docs/feasibility.md`. The fixture proves public Equinox ordering for synthetic
Bundles with the recorded provider arrangement. This evidence does not prove
live HRC activation and does not prevent arbitrary `Bundle.loadClass`,
reflection, or another early activation route. The observer must remain loaded
until final framework shutdown; dynamic stop, update, uninstall, refresh, and
republish are prohibited by policy, not proved safe.

The Windows harness now executes the four protected-pipe exchanges through a
synthetic three-process arrangement and a capacity-one asynchronous in-memory
publisher. It proves publication visibility before acknowledgement, a
store-affine coalesced lease, exact removal before a grant or revocation
acknowledgement, bounded loser drainage, and a publication budget capped by the
remaining session deadline. Adversarial tests cover publication and disposal
interleavings, synchronous re-entry, callback and removal failures, and unknown
removal status. They require non-abandonable cleanup. Faulted or unknown
removal cannot claim publication absence, and late verified removal still
fails before terminal acknowledgement. The deadline checks do not hard-preempt
arbitrary blocking native calls. This remains offline synthetic evidence.

The file seam reserves `endpoint-v1.bin` without replacement and never writes
the bearer token. It requires an exact current-account-plus-`SYSTEM` DACL. It
does not provide logon-SID isolation. A retained directory handle rejects
reparse points, proves a local NTFS volume, and pins the namespace. Publication
uses a random `CREATE_NEW` temporary file, exact flush and read-back checks,
retained-root native rename, and final file-identity validation. The publisher
retains a handle that denies new write and delete access. The store
checks the fixed name-to-file identity again before returning its lease. POSIX
handle deletion and bounded retained-directory enumeration prove exact absence.
An indeterminate removal forbids reuse and cannot claim absence. The reader
returns an independent wipeable structural snapshot. Cooperative checks do not
hard-preempt synchronous native calls. The 11 filesystem tests include real
junction rejection, ABA and identity replacement, namespace pinning, bounded
multi-page enumeration, retained-root cross-directory rename, collision,
deadline, cancellation, and late-removal cases.

The five single-file artefact-identity tests verify one default data stream
through a caller-supplied canonical DOS path on a fixed local drive and Mount
Manager volume. A retained read handle denies new data-write and delete access,
but not attribute or extended-attribute access. The primitive checks expected
length and SHA-256, a single link, reparse ancestors and leaf, final path,
volume serial number, and 128-bit `FILE_ID`. Revalidation is detection-only; it
does not make a later path-based process launch atomic.

The six protected app-local artefact-set tests require a caller-supplied
canonical DOS directory on local NTFS. Its exact protected DACL admits only the
current process account and `SYSTEM`. The set accepts 1 through 128 one-level,
printable ASCII filenames and requires exact case. Every entry must be expected;
an extra PDB, `.runtimeconfig.dev.json`, or subdirectory fails the scan. Each
expected default stream is retained with its length, SHA-256, volume serial
number, and 128-bit `FILE_ID` under one absolute deadline.

The domain-separated canonical manifest digest binds the designated executable
and the ordinally sorted exact names, lengths, and SHA-256 values. Revalidation
scans the exact entry set before and after it revalidates every retained member.
The protected root still permits new child creation. This primitive is a
snapshot and detection control, not namespace isolation. A race remains between
the final revalidation and a later path-based loader action.

Checkpoint `d4cd474` adds an internal out-of-band release-manifest seam. The
canonical binary starts with `HRCREL01`. Checkpoint `64043e5` keeps the original
synthetic test-harness and framework-dependent snapshot pair and adds one
synthetic native-fixture and no-CRT System32-policy pair. Both use the
`win-x64` target-runtime policy label. The native profile requires the exact
one-file `HrcJobObserver.NativeFixture.exe` set. These are policy labels, not
proof of runtime or loaded-module selection. The seam owns copies of the
supplied manifest and expected pin. It computes a domain-separated SHA-256 pin
and authenticates it in fixed time before it parses the structure.

The parser requires closed role, deployment, and runtime values; a zero reserved
field; one exact designated executable; 1 through 128 strictly ordinally sorted
canonical file entries; no duplicate or case-colliding name; exact lengths and
SHA-256 values; one protected artefact-set manifest digest; and no trailing
bytes. The composite opens the exact protected set described by those entries.
It binds the set's computed canonical digest to the authenticated manifest and
performs a final exact-set revalidation. The successful lease retains every
member identity, the validated manifest pin, and the artefact-set digest. It is
explicitly ineligible for trusted launch. Failure disposes a partially opened
set and wipes owned temporary manifest and digest copies.

Keep the release manifest out of the protected application directory and exact
artefact set. Inclusion would create self-reference and an unexpected entry.
The caller supplies the out-of-band manifest bytes and owns the pin provenance.
A sibling manifest, a pin derived from that manifest, or a pin compiled into an
artefact covered by the same circular policy does not establish independent
trust. This seam supplies no signature, release provenance, freshness, rollback
protection, trusted installer policy, member file ACL, shared-runtime trust,
loader atomicity, launch integration, production role, private handoff, role-
bound `READY`, Java integration, or HRC runtime evidence. Its deadline and
cancellation checks are cooperative and do not hard-preempt blocking native
calls.

The six pinned release-manifest tests cover exact owned-copy retention and final
revalidation, authentication before structural parsing, noncanonical wire
rejection, protected artefact-set digest binding with failure cleanup, one
absolute operation budget, and a fixed golden identity.

Checkpoint `64043e5` adds seven audited native-release binding tests and passes
102/102. They cover the two closed manifest profiles and their golden
identities, bounded exact byte copies through the retained executable handle,
late-failure wiping, authenticated audited-native open and revalidation, and
partial-failure cleanup. `AuditedNativeFixtureReleaseLease` accepts only the
exact one-file native profile. It audits the retained handle's owned 4,096-byte
snapshot, binds the audit SHA-256 to the authenticated manifest entry, and
revalidates the complete set before return. It does not establish independent
manifest-pin provenance and remains ineligible for trusted launch.

The five tests from legacy harness-containment checkpoint `2a56de1` exercise
the internal `ContainedHarnessProcess`. It launches exactly the current
generated harness apphost in one of two fixed public modes: `Exit` or `Block`.
These join three legacy IPC child modes, for five fixed public child modes in
total. The build guard rejects managed `ProcessStartInfo` or `Process.Start`
launch outside the legacy harness `Program.cs`. It admits exactly two native
`CreateProcessW` call sites in production source: `ContainedHarnessProcess`
and `ContainedAuditedNativeFixtureProcess`. The legacy launcher
passes an exact non-null `lpApplicationName`, a fixed command line, an empty
Unicode environment, the current executable directory, no inherited handles,
and no standard I/O handles. The unnamed Job handle is
non-inheritable. The launcher sets and reads back exactly
`JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`, assigns the process through
`PROC_THREAD_ATTRIBUTE_JOB_LIST`, and creates its initial thread suspended.
Before exact `ResumeThread`, it requires a singleton Job PID and verifies a
retained `ProcessIdentityLease` and the exact image path.

One absolute monotonic deadline and caller cancellation govern cooperative
checks around synchronous native launch calls. They do not hard-preempt them.
A post-resume late success is rejected. Start failure and disposal close the
last held Job handle, then wait for the exact retained process under a separate
fixed five-second cleanup bound. Concurrent disposal coalesces. Tests cover
normal exit, explicit last-Job-handle closure
killing a blocking child, no managed child-entry event before a pre-resume
fault, late-deadline cleanup after resume, and concurrent disposal with
`WaitForExitAsync`. These managed-entry assertions apply to the legacy apphost
checkpoint, not the audited no-CRT fixture.

The suite does not terminate its parent abruptly. Windows kill-on-close
semantics support cleanup when the final Job handle closes, but direct abrupt-
parent-death and crash behaviour remain unexercised. The primitive has no
artefact-set trust integration, release provenance, shared-runtime or loader
trust, production roles, private handoff, role-bound `READY`, token transfer,
Java or HRC integration, sandbox, or same-user hostile-process defence. The
protected artefact-set root still permits new child creation and remains a
snapshot and detection control only.

Checkpoint `fb9ba23` adds a project-owned no-CRT AMD64 PE fixture. Its only
imports are `GetCommandLineW`, `ExitProcess`, and `Sleep` from `KERNEL32.dll`.
The source defines `--native-exit`, `--native-block`, and an invalid-argument
exit code of `87`. Its exact 510-byte neutral-language embedded Windows
manifest declares one `amd64` `win32` identity, `asInvoker`, and
`uiAccess=false`. It declares no dependent assembly or file.

The build invokes the recorded MSVC `14.44.35207` and Windows SDK
`10.0.26100.0` paths with a cleared environment. It uses separate temporary and
output directories for two builds and requires their 4,096-byte executables to
be byte-identical. The PE uses subsystem version `6.02` and
`DependentLoadFlags=0x0800`. That load flag requires Windows 10 RS1 or later,
so the subsystem value does not lower the effective runtime floor below RS1.

The strict structural audit authenticates the caller-supplied file SHA-256
before parsing. It then requires the exact PE32+ headers, four fixed sections,
data directories, import descriptor, matching import lookup and address slots,
load configuration, three debug records, neutral manifest resource, exception
record, checksum, contiguous raw layout, and absence of certificates,
relocations, gaps, and overlays. The observed pinned-host golden SHA-256 is
`3c9bee49acfffaea7f3fae2692900b47eef0e41e61e4ae7b14e2b1884a05fe34`.
This is exact checkpoint evidence, not toolchain or signer provenance or a
cross-machine rebuild guarantee.

The bounded runtime test launches Exit and the invalid argument with a cleared
five-variable environment, no shell, and no redirected standard handles. It
confirms exit codes `0` and `87` and performs bounded kill-and-wait cleanup on a
timeout. The source-defined Block role is not launched before native Job
containment exists. The structural audit does not prove machine-code semantics.
The fixture has no Control Flow Guard instrumentation, and `/CETCOMPAT` does
not prove Control-flow Enforcement Technology enforcement. The evidence does
not prove System32 or KnownDLL trust, toolchain or signer provenance, or
cross-machine reproducibility.

The embedded Windows manifest is not a native `HRCREL01` release-manifest
binding by itself. The separate authenticated native profile and audited
composite remain ineligible for trusted launch.

Committed checkpoint `70e0d77` adds five audited native-containment tests and
passes 107/107. It launches only the exact synthetic one-file no-CRT
fixture. It requires Windows 10 version 1709 build 16299 or later, x64 debug
ABI layouts, an AMD64 process, exact
`JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` read-back, Job-list assignment at process
creation, and one exact Job PID.

Launch uses the exact canonical DOS executable for non-null
`lpApplicationName`, command line argument zero, and working directory. A
direct volume-GUID `CreateProcessW` attempt was rejected on this licensed host.
The implementation therefore uses the canonical DOS locator while retaining
and checking its volume-GUID identity. This is a host observation, not a
general Windows compatibility claim. Handles for every directory from the
fixed-drive root through the application directory omit `FILE_SHARE_DELETE`,
reject reparses, and pin the complete DOS-to-volume namespace. The executable
handle remains retained throughout the wrapper lifetime.

`DEBUG_ONLY_THIS_PROCESS` exposes the exact initial
`CREATE_PROCESS_DEBUG_EVENT` before user mode. The launcher compares the
event's process and thread handles with the creation handles. It authenticates
the event's direct image-file handle by length, SHA-256, 128-bit `FILE_ID`,
volume identity, and volume-GUID path against the retained executable. This
handle binds the main image only.

Checkpoint `8bd853a` adds a standalone aggregate for the initially observed
NTDLL, KERNEL32, and KernelBase order. Integration produced 106/110 because
four real containment cases rejected an unexpected fourth `LOAD_DLL` event
before identity validation. A separate bounded, read-only `Process.Modules`
probe implicated System32 `apphelp.dll`. Checkpoint `445d02a` therefore closes
this host/build/fixture profile over NTDLL, KERNEL32, KernelBase, and Apphelp.
Its later runs directly bind the fourth debugger `hFile` to the current
System32 `apphelp.dll`.

After authenticating the `CREATE_PROCESS_DEBUG_EVENT` image handle, the
launcher continues that event. It then accepts exactly four `LOAD_DLL` events
in the stated order, followed by the exact initial first-chance breakpoint.
Every startup event must identify the exact created PID and initial TID. The
pump admits at most five events. Each load requires a valid `hFile` and a
nonzero base. The aggregate duplicates the borrowed handle and matches the
next contemporaneously retained System32 file by identity, length, volume
identity, volume-GUID path, and SHA-256 bytes. Each module base must differ
from the main-image base and every earlier module base.

The four expected System32 files use `FileShareRead`-only retained handles.
They can defer file replacement or Windows servicing for the aggregate
lifetime. The current host observation for System32 `apphelp.dll` is 666,784
bytes, SHA-256
`53E7D1ABA3FF4A0D0DEF2DF44777B4C0CA6BB352E8283E8B238E1881B45C8AFE`,
and file version `10.0.26100.8457`. Two hard links were observed in System32
and WinSxS. Authenticode was observed as valid for `Microsoft Windows`.
Apphelp is host/build/fixture appcompat-loader policy, not a static fixture
import. The primitive self-baselines the current System32 file. It establishes
no signer, Microsoft, build, freshness, rollback, or appcompat-policy
provenance.

The exact initial first-chance breakpoint is the startup barrier. While that
event remains outstanding, `SuspendThread` must report prior count `0`. The
launcher then continues the breakpoint, detaches, requires no remote debugger,
and revalidates the sealed aggregate. The final `ResumeThread` must report
prior count `1`. The full aggregate stays retained through the wrapper and
process lifetime. Failure cleanup retains partial or full aggregate evidence
until exact process exit. The detached reaper receives it when bounded cleanup
cannot prove that exit.

Follow-up checkpoint `cc77b9b` explicitly calls `TerminateJobObject` with the
unique nonzero failed-launch code `0xE0435243` for every post-creation failed
launch. It then closes the last Job handle before it continues any outstanding
debug event. The `AfterInitialBreakpointOwned` fault uses the Exit role and
observes that exact forced code instead of its natural exit code `0`. This
directly closes the former pre-entry cleanup window.

The create, debug, detach, and initial-resume transaction runs on a fresh
dedicated operating-system thread with `ExecutionContext` flow suppressed. The
caller joins it non-abandonably. Failure cleanup resolves the thread-affine
debug session non-abandonably. After detachment, cleanup closes the last Job
handle and waits on the exact process handle. If its bounded wait cannot prove
exit, a detached process reaper retains all launch authority, including the
partial or full startup-module aggregate, until that exact handle signals. If
`WaitForSingleObject` fails, the reaper retains that authority indefinitely and
records terminal uncertainty.
The build wrapper independently bounds the complete .NET validation process to
180 seconds.

Checkpoint `445d02a` extends the same five real containment cases; it does not
add another registration. They now cover the extended AMD64 debug ABI, the
exact 14-stage successful containment-hook sequence, all 16 injected launch stages,
failure after each partial or full module capture, a late deadline after the
Apphelp capture while its load event remains owned, and a post-resume late
deadline. They also cover aggregate revalidation and disposal, the forced
pre-entry failure exit, and prior containment behaviour. Baseline and final
reaper assertions show only that no retained or terminal reaper state remained
at each assertion time. They do not prove that the reaper was never used. The
3 system-module cases and same 5 containment cases keep the total at 110.
Three consecutive direct Release runs pass 110/110 with no child residue.

The tests do not terminate the parent abruptly. The initial breakpoint is not
a direct entry sentinel. No debug-event file handle proves section, mapping,
or executed-page identity. The evidence proves no KnownDLL provenance, no
global System32 namespace closure, and no complete dependency or general
loader closure. It does not establish trusted or production
launch, a production role, private handoff, role-bound `READY`, Java
integration, or HRC runtime behaviour.

Define a trusted installer or release policy that supplies canonical manifest
bytes and independent pin provenance. Supply an out-of-band trusted OS and
module policy; further self-baselined module enumeration cannot provide that
trust. Close the production namespace and complete runtime-module, loader, and
dependency closure before using the synthetic proof in dedicated roles. Keep
the current containment proof separate until those roles integrate it. Complete
that runtime loader boundary before private initial handoff and role-bound
`READY`.
Then add guarded Windows known-folder resolution, protected LocalAppData
hierarchy provisioning and provenance, and stale or crash recovery around the
existing-directory seam. Do not connect this seam to Java or open the
standalone-runner gate until those boundaries pass. Then enforce
the recorded configuration, provider, class, and start-level gates in a
deterministic JAR, manifest, guarded install, and rollback design. Extend the
active-process runtime identity gate for all added providers before live use.
Refuse controller admission when observer publication is absent or invalid;
Equinox can continue after observer activation failure. Do not install the
observer or restart HRC while the dirty tabs `*Hand 7` and `*From Hand 7`
remain protected. Resolve those resources explicitly before the first clean-
start observer validation. Reserve the authorised smoke until the runtime
observer and standalone control path are ready.

Installed-component inspection identified the Stacks and Blinds surface as an
Eclipse Nebula NatTable with default selection and edit bindings. A live check
then established a keyboard bootstrap: from the newly opened Basic Hand Data
page, pressing `Tab` seven times reached the otherwise invisible grid focus
stop; `Ctrl+A` visibly selected the cell displaying `Auto`; `Ctrl+Home`
preserved that origin; and Space opened the complete named player-count list.

A second disposable check used the same route, pressed `Down` once to select
`HU`, and committed it with `Enter`. HRC removed `HJ`, `CO`, and `BU`, but retained
the previous blind rows as `SB 4000 / 40.0 bb` and `BB 5000 / 50.0 bb`. The
setup was closed without advancing. This proves one non-coordinate table-size
selection and also proves that a size change cannot be treated as a stack
reset. Every active seat must be overwritten and read back before advancing.

A third disposable check completed that supervised keyboard path. A new setup
reopened as `Auto` while retaining only the earlier `SB` and `BB` rows and their
values. The same bootstrap selected `HU`; `Down`, `Right`, and `F2` then opened
SB Chips. Enter committed fabricated test values `4100` and `5100`, visibly
recalculated them as `41.0 bb` and `51.0 bb`, and advanced through the two
active rows. The final commit opened the blank next-row Chips editor; Escape
cancelled it without adding a row. A deliberately invalid `abc` value stayed
red in the editor, did not commit or advance on Enter, and Escape restored
`4100 / 41.0 bb`. The setup was then closed without advancing or writing data.

A fourth disposable check demonstrated one supervised, non-coordinate path to
the unresolved unnamed script picker and one immediate same-setup reopen after
cancellation. After `Alt+N` opened Betting Setup with a visible focus rectangle
on `Back`, four Tab presses reached the `Preflop` tab; two Right presses selected
`Scripting`; and two more Tab presses reached the first folder button beside
`Script:`. Space opened the standard `Open` dialog in this worktree's
`scripts/hrc` folder. Escape restored the visible focus rectangle to the same
folder button, and Space opened the dialog a second time. Both dialogs were
cancelled; no script was loaded and no tree or file was created.

A fifth disposable check repeated the HU selection and stack-entry route with
equal `2 bb` stacks, then followed the same Scripting and Open-dialog path. The
exact worktree candidate hash was checked before its filename was typed and
opened. HRC displayed the expected basename without `[Errors]`, reported two
nodes, and enabled Finish. Expanded Preview directly showed
`R 2.00 SB PRE` with only `C 1.00 BB PRE`. Valid Tab and Space input did not
activate Finish and appeared to reach the background window; one current-frame
screenshot-located Finish click was used for discovery only and created
`*Hand 7`. A standalone runner must not use that coordinate path.

On `*Hand 7`, a supervised non-submitting Nash probe established that `Alt+R`
opens Nash Calculation with OK initially focused. Tab moved the visible focus
rectangle to Cancel; Space invoked it and closed the dialog without submission.
From OK, `Shift+Tab`, `Ctrl+A`, mandatory `Ctrl+Home`, then Right entered the
settings value column. `F2` exposed the exact algorithm, scope, and sampling
choices. The CI editor accepted and committed a change from `1.0` to `10.0`.
The correct grid route also displayed the required second-run state with CI
`1.0`, no Reset Regret checkmark, and a Reset Strategies checkmark. `Alt+F4`
closed the dialog without submission. After the CI `10.0` probe, reopening
showed CI `1.0` and both reset boxes visually clear, so the observed CI edit was
not retained. Omitting `Ctrl+Home` once left both reset cells under ambiguous
selection styling. This did not establish either checkbox value or show both
reset modes active. Every value therefore requires explicit read-back before
submission. No Nash calculation was submitted and no file was written.

A sixth non-submitting check established exact per-cell Nash read-back. After
the same mandatory grid bootstrap, `Ctrl+C` returned `HRC 4.0 (Default)`,
`Full Tree`, `Until CI value is reached`, `1.0`, `false`, and `false` from the
six currently visible value cells. Space changed Reset Strategies from raw
`false` to `true`; while it displayed a checkmark, per-cell copies returned the
required reset pair `Reset Regret = false`, `Reset Strategies = true`. Space
then restored Reset Strategies to raw `false` before `Alt+F4` closed the dialog
without submission. One whole-grid `Ctrl+A`, `Ctrl+C` attempt copied only
`CFR Algorithm`, so the currently supported route must navigate and validate
each cell. The separate CI `10.0` edit was not copied after editing. Progress
remained idle and no file was written.

A seventh disposable check established a non-coordinate Finish action.
`Ctrl+W`, then `H` opened Hand Setup from active `*Hand 7`; that setup retained
the prior equal-`2 bb` rows and HU script. After HU was explicitly selected and
`Alt+N` reached the two-node estimate, read-only native enumeration found exactly
one owned `Hand Setup` `#32770` and one visible enabled `Button` with raw caption
`&Finish`. The discovery provider could not target its indexed element and HRC
remained unchanged. One guarded `SendMessageTimeout(BM_CLICK)` then closed the
wizard. Compared with the pre-action editor-tab set, HRC added exactly one
accessible hand-editor tab, `*From Hand 7`, alongside `*Hand 7`. No error
appeared and Progress remained idle. The retained inputs and resulting name show
that `Ctrl+W`, then `H` can inherit active-hand state and is not yet a clean next-
simulation route. No Nash calculation ran and no file was written.

An eighth non-writing check inspected the remaining lifecycle dialogs on active
`*From Hand 7`. `Ctrl+H`, then `R` opened the fully labelled Rename Hand dialog
with `From Hand 7` visibly selected. The provider's semantic value action
returned an unknown outcome; a fresh observation proved that the name was
unchanged, so it was not retried. The provider reported a background edit as
focused despite the visible selection, and Escape cancelled the dialog.
`Ctrl+Alt+S` then opened Save As at `\\VAULT\sims\Preflop\HU` with
`From Hand 7.hrcv` proposed and `*.hrcv Viewer Save` selected. The provider
reported Search as focused and did not expose the selected type text
machine-readably, so Escape cancelled without saving. `Ctrl+H`, then `E` opened
Export Strategies with retained Complete Export, Depth `2`, PrettyPrint JSON
clear, and threshold `0.1`; a semantic scope action had an unknown outcome, and
fresh observation showed no change before Escape cancelled. The File menu had
no hand-close command. Both dirty tabs remained open, Progress stayed idle, and
read-only checks confirmed that neither corresponding exact `From Hand 7`
output target existed.

These checks prove the keyboard portions of the supervised route,
different-valid-value entry, visual read-back, one rejected-input recovery,
exact candidate load, current equal-`2 bb` Preview, and observed Nash
configuration, per-cell machine-readable Nash read-back, exact Reset Strategies
verification, and non-submitting close routes. They do not yet provide
machine-readable stack read-back or a reliable native foreground and focus
contract for keyboard steps. The native Finish action and successful exact-one-
hand-editor-tab set delta are now confirmed without coordinates. Its guards do
not depend on keyboard focus. The live provider continued to disagree with
visible focus and could not operate the Rename or Export controls or expose the
Viewer type strongly enough for a safe standalone write.

Before any further pre-runner Nash OK action, use static, non-submitting, and
prior demonstration evidence to define machine-readable candidate detectors and
stop rules for accepted, rejected, queued, running, cancelled, completed, and
failed states. Resolve the remaining Rename, Viewer Save, Export, exact
tab-close, `Save Resource`, `Don't Save`, and next-simulation controls through
non-writing or Cancel-only probes. Owner authorisation now covers the required
live validation, but do not submit until the exact-status observer and all
pre-submit safety gates are ready.

Reserve the one authorised equal-`2 bb` HU lifecycle smoke for the project-owned
runner after feasibility supports the implementation gate. In that workflow,
require a job-identity-matched accepted, queued, running, or explicit successful-
terminal state after each Nash submission, while preserving CI `10.0` before CI
`1.0`. Queue Viewer Save immediately without waiting for Nash completion. Wait
for both Nash jobs and Viewer Save to finish successfully before strategy
export. Disappearance or idle alone is never success. Verify both new non-empty
outputs before the exact matching `Save Resource` prompt and `Don't Save`.
Treat every pre-existing hand-editor tab as protected. Require exactly that set
plus the completed simulation immediately before close, and exactly the
unchanged protected set afterwards, with no additions or replacements.
Explicitly activate `Home`, and then validate the next-simulation transition.
Negative states not encountered during that smoke
remain `TO CONFIRM`; any unrecognised state stops it. Do not add the runner until
feasibility has a supported verdict. Retain separate Preview checks for other
table sizes and boundary stacks. Verify the Save As destination, Viewer type,
filename, and extension every time.

## Definition of done

Feasibility discovery is complete when observed evidence supports a clear
verdict for the representative workflow. The evidence must cover rename, both
Nash configurations, queue order, completion or failure, Viewer save, and
saved-output verification. It must also cover strategy export, strategy-archive
verification, and Viewer-only hand-tab closure before the next simulation.

A feasible workflow must complete the full lifecycle once. Do not repeat a
long-running calculation only to obtain a second feasibility sample.

Project completion additionally requires an owner-authorised, project-owned
runner independent of Codex Computer Use; explicit terminal success and failure
for every critical Job; fail-if-exists output publication; the authorised tiny
HU lifecycle from Home back to Home; a safe next-simulation transition; focused
tests; an operator runbook; a clean worktree; reviewable pushed commits; and a
validated fast-forward merge to `main`. None of those implementation outcomes
is implied by the current discovery evidence.

## Authority

This repository owns implementation truth for HRC Beta Automation.
[`docs/feasibility.md`](docs/feasibility.md) is the source of truth for observed
discovery evidence.

Portfolio records provide planning context. When portfolio context conflicts
with verified local evidence, record the mismatch here and use the local
evidence.

## Licence

The repository is available under the [MIT Licence](LICENSE). HRC Beta itself
and its licence remain the property of their respective owner and are not
distributed by this project.
