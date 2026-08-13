# Project instructions

## Scope and authority

Keep this repository focused on HRC Beta Automation. This repository owns implementation truth after its creation.

Treat `README.md` as the project boundary. Treat `docs/feasibility.md` as the source of truth for observed feasibility evidence.

Do not copy unverified portfolio claims into implementation decisions. Record a mismatch and preserve uncertainty when external context conflicts with local evidence.

## Licensed-machine constraint

Run HRC Beta only on the licensed host `EM-3960X`. The host reports an AMD Ryzen Threadripper 3960X processor.

Do not copy HRC Beta, its licence material, or its private configuration to another computer. Do not expose licence data in files, logs, screenshots, commits, or completion reports.

## Owner authorisation scope

On 12 August 2026, Euan attested in the project conversation that HRC Beta's
owner personally authorised him to do anything with HRC for this project if the
use is not commercial. Euan confirmed that this project is personal and wholly
non-commercial. Treat this owner-granted oral permission as authority for the
local accessibility runner, read-only component inspection and hashing, the
project-owned startup status observer, and the HRC interaction needed to
validate them on the licensed host.

This attestation does not establish a vendor-supported API, warranty, or
technical result. It does not permit commercial use, licence sharing,
redistribution of HRC components, unnecessary strategy-data access, or copying
HRC to another computer. Stop and obtain a new scope decision if the project
becomes commercial or the proposed mechanism materially exceeds the recorded
personal automation and exact-status design. Preserve every technical and data
safety gate below.

## Discovery-first rules

- Inspect accessibility before choosing an automation method.
- Prefer accessible controls, supported automation patterns, and keyboard paths.
- Do not use blind coordinate clicks.
- Stop at a critical control that cannot be identified or operated safely.
- Preserve unknown facts as `TBD` or `TO CONFIRM`.
- Record only observed evidence in `docs/feasibility.md`.
- Do not add application-automation source code, dependencies, or build
  commands before feasibility is proven.
- A minimal exact-status observer may be implemented and tested offline as a
  feasibility instrument before the verdict. Keep it separate from the runner.
  Do not install or activate it in HRC until its scope, build, rollback, event
  schema, and protected-resource prerequisites are validated.
- An explicitly requested HRC tree-building candidate can be developed offline
  under `scripts/hrc/`. Keep it labelled unvalidated until HRC verifies it on
  the licensed host.

## HRC data safety

- Use a new output filename for every simulation.
- Never overwrite or delete existing HRC data.
- Normalise hand-tab bases for uniqueness by removing at most one leading dirty
  `*`, then one terminal `.hrcv` or `.hrcz` suffix case-insensitively. Compare
  with ordinal case-insensitive semantics. Requested simulation names must match
  `^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$`, have no HRC suffix, not end in `.`, and
  not use a Windows reserved device base, including before a dot.
- Do not reveal poker data that is not necessary for feasibility evidence.
- Ask Euan before using poker inputs when no clearly safe non-overwriting
  workflow is available.
- Ask Euan before starting an expensive calculation unless the current request
  explicitly authorises the run or batch. One batch authorisation covers its
  specified simulations.
- Verify saved output without changing existing output.
- Stage Viewer output under `\\VAULT\sims\Preflop\<table-group>` inside a
  validated, exclusively owned staging namespace. Atomic reservation and high
  entropy are necessary but do not prove exclusive ownership. Acquire a
  validated system-wide HRC-control lease before the first automated HRC input
  and hold it through both canonical promotions, target-tab closure, and final
  state verification. Prohibit manual input, a second HRC process, another
  runner, and other automation in that scope. A high-entropy filename in the
  shared folder is not isolation.
  Do not guess or create a missing table-size folder. After explicit Job success
  and stable non-empty staging metadata, promote with fail-if-exists semantics
  to the simulation filename.
- Before every Viewer save, verify the destination folder, selected
  `*.hrcv Viewer Save` type, exact staging filename, and `.hrcv` extension. Do
  not rely on the file type retained from an earlier Save As session.
- Stage the strategy export as a new high-entropy `.zip` inside the same
  exclusively owned staging namespace. After the required format guard,
  explicit Job success, and stable non-empty staging metadata, promote it with
  fail-if-exists semantics so the canonical base filename is the simulation name.
- Do not treat an HRC existence prompt or preflight check as overwrite
  prevention. Installed Viewer Save can replace a race-created target, and
  Complete Export can truncate one. Before unattended writes are implemented,
  validate an exclusively owned staging namespace, an exclusive HRC-control
  lease, and a fail-if-exists promotion to each canonical simulation filename.
  The lease must exclude another runner, all manual HRC interaction, a second
  HRC process, and other automation in scope. Never delete partial output
  automatically. Stop before writing if either exclusivity guard cannot be
  acquired or proved, or if any unexpected Job, dialog, file, or input
  transition appears.
- Do not infer archive format from the visible `*.zip` type, filename extension,
  non-empty metadata, or successful Export Job. The inspected calculator
  plug-in `4.1.1` has a hidden retained format index that can write plain text to
  the `.zip` path.
  For a fresh Hand Setup hand, require the actual export Save As to expose both
  `*.zip Archived Json` and `*.txt Plain Text`, explicitly select and read back
  ZIP, and then accept that real staging export. Cancel does not reset the hidden
  index. Stop on a ZIP-only dialog or any filter mismatch.
- Verify the Viewer output and intended strategy archive before closing the
  completed hand tab. Require a separately proved ZIP format state; metadata
  alone is insufficient. Do not inspect strategy contents unless the task
  requires it and Euan authorises the inspection.
- Do not restart HRC while protected dirty tabs are open. Static reset behaviour
  does not prove those resources can be recovered after restart. Stop until
  their safe disposition is explicitly established.
- When HRC shows `Save Resource` during Viewer-only tab closure, select
  `Don't Save` only when the verified `.hrcv` and `.zip` base filenames match
  the simulation named in the prompt. Stop without discarding the hand if any
  filename, prompt, or available action differs from the observed workflow.
  Treat every pre-existing hand-editor tab as protected and snapshot its stable
  identity, title, and dirty state before the simulation. Immediately before
  closure require the exact hand-editor set to equal those protected identities
  plus exactly one expected completed simulation. Afterwards require the set to
  equal exactly the unchanged protected identities, with no addition or
  replacement.

## Installed-component identity gate

Before relying on any version-specific static finding, resolve the `plugins`
directory from the active `hrc.exe` process's own installation. Verify there the
exact calculator, NatTable, JFace, SWT, Commons Compress, Eclipse Core Jobs,
Eclipse UI, and Eclipse UI Workbench filenames and SHA-256 values recorded in
`docs/feasibility.md`. The HRC application version is not independently
confirmed. A hash match is necessary but does not prove live focus, retained
preferences, or runtime state. Any process, path, filename, or hash mismatch
must stop the runner and reopen feasibility validation.

The offline adapter also compiles against Equinox Common and Eclipse OSGi.
Their recorded hashes are compile-provider evidence only. Do not activate a
live observer until both providers are deliberately added to, and verified
through, the active-process identity gate in `docs/feasibility.md`.

Before changing startup configuration, verify the exact baseline `config.ini`,
`bundles.info`, and `hrc.ini` hashes recorded in `docs/feasibility.md`. Verify
the baseline rows and provider hashes. After a guarded install, separately
verify the deterministic target file hashes, the exact inserted observer row,
all preserved baseline rows, and the Job-producer class hashes. Stop on any
unexpected source or target difference.

## Exact-status transport safety

- Keep the observer endpoint on IPv4 loopback. Do not treat loopback as process
  identity, same-user access control, encryption, confidentiality, or the
  HRC-control lease.
- Generate a fresh cryptographically random 32-byte bearer token for each
  observer start. Transfer the token and endpoint only through a validated
  same-user protected mechanism. Never commit, persist, log, or echo the token.
- Treat `src/HrcJobObserver/windows-bootstrap/` as source/test-only. Committed
  checkpoint `64043e5` passes 102/102 tests. Committed checkpoint `70e0d77`
  adds 5 audited native-containment cases and passes 107/107. The total
  comprises 20 primitive tests, 8
  descriptor and protocol tests, 27 broker and in-memory-store tests, 11
  filesystem tests, 5 single-file artefact-identity tests, 6 protected app-
  local artefact-set tests, 6 pinned release-manifest tests, 7 native-fixture
  tests, 7 audited native-release binding tests, 5 harness-containment tests,
  and 5 audited native-containment tests. It proves
  exact applied protected-DACL read-back, two-sided process identity, bounded
  one-shot frames, fixed
  public-frame exchange with a synthetic child, rejection of a wrong live
  child, a canonical HMAC-bound descriptor model, and eight phase- and role-
  bound message codecs. It also proves a capacity-one asynchronous in-memory
  publisher with independent wipeable snapshots and a store-affine, coalesced
  exact-removal lease. The lease provides cross-store and ABA defence.
  A one-shot broker runs all four exchanges across the broker harness process
  and long-lived synthetic observer and controller child modes. It enforces
  exact role bindings, one common security context, a publication budget capped
  by the remaining absolute session deadline, claim-versus-revoke arbitration,
  and terminal rejection of an already-completed malformed loser. Its coalesced
  asynchronous disposal waits for non-abandonable exact removal. `RunAsync`
  remains the authoritative protocol-failure channel; `DisposeAsync`
  separately reports cancellation-request and cleanup failures. A faulted or
  unknown removal cannot claim absence. Removal verified only after its
  deadline still fails the session before terminal acknowledgement.
  Deadline checks are cooperative. They do not hard-preempt an arbitrary
  blocking native call.
  Adversarial tests cover cancellation, disposal and publication interleavings,
  synchronous re-entry, throwing cancellation callbacks, and combined protocol
  and cleanup failures.
  The internal file publisher and independent reader operate only in a
  caller-supplied, already-existing protected local NTFS directory. They bind
  the expected owner to the current process account SID and require an exact DACL
  for that account and `SYSTEM`. This does not isolate logon sessions for the
  same account. Their retained directory handle intentionally denies delete
  sharing and pins the namespace. They reserve only the fixed public
  `endpoint-v1.bin` descriptor and never write the bearer token. Publication is
  capacity one and no-overwrite. It uses a random `CREATE_NEW` temporary file,
  exact flush and read-back validation, path, volume, DACL, and file-identity
  checks, and retained-root `NtSetInformationFile` rename without replacement.
  The retained publication handle denies new write and delete access until
  exact removal. Recheck the fixed name-to-file identity before returning its
  lease.
  Exact removal uses POSIX handle deletion and bounded retained-directory
  enumeration. Terminal removal uncertainty preserves and rejects an ABA
  replacement, forbids reuse, and permits operating-system handle cleanup. The
  reader returns independent wipeable structural snapshots. Filesystem tests
  include real fixed-leaf and root junction rejection, retained-root
  cross-directory rename, namespace pinning, bounded multi-page enumeration,
  ABA and identity replacement, collision, cancellation, deadline, and late-removal
  paths.
  The artefact-identity primitive accepts one caller-supplied canonical DOS
  path on a fixed local drive and Mount Manager volume. It opens the default
  data stream with a retained read handle that denies new data-write and delete
  access, but not attribute or extended-attribute access. It verifies the
  expected length and SHA-256, a single link, no reparse ancestor or leaf, the
  final path, volume serial number, and 128-bit `FILE_ID`. Revalidation detects
  later path, identity, length, or digest drift. It does not make a later
  path-based process launch atomic.
  The protected app-local artefact-set primitive requires one caller-supplied
  canonical DOS directory on local NTFS. The root must have an exact protected
  DACL for the current process account and `SYSTEM`. It accepts 1 through 128
  expected default-stream files. Each expectation uses one printable ASCII
  Windows filename with exact case, an expected length, and an expected SHA-256.
  Every directory entry must be expected. An unexpected PDB,
  `.runtimeconfig.dev.json`, or subdirectory fails the scan. Every member is
  pinned through the single-file lease, including its volume serial number and
  128-bit `FILE_ID`, under one caller-supplied absolute deadline. A domain-
  separated canonical manifest digest binds the designated executable and the
  ordinally sorted exact names, lengths, and SHA-256 values. Revalidation scans
  the exact entry set before and after it revalidates every retained member.
  The retained root allows new child creation. The set is therefore a snapshot
  and detection control only. A race remains between the last revalidation and
  a later path-based loader action.
  The internal pinned release-manifest seam accepts one out-of-band canonical
  binary manifest with magic `HRCREL01`. Version 1 admits only two exact role
  and deployment pairs: the synthetic test harness with a framework-dependent
  snapshot, and the synthetic native fixture with the no-CRT System32-policy
  profile. Both use the `win-x64` target-runtime policy label. The native
  profile requires the exact one-file `HrcJobObserver.NativeFixture.exe` set.
  These values are policy data, not observations of runtime or module
  selection. The seam owns copies of the supplied manifest bytes and expected
  pin. It computes the domain-separated SHA-256 pin and compares it in fixed
  time before structural parsing.
  Structural parsing requires closed role, deployment, and runtime values; a
  zero reserved field; one exact designated executable; 1 through 128 strictly
  ordinally sorted canonical file entries; no duplicate or case-colliding name;
  exact lengths and SHA-256 values; one protected artefact-set manifest digest;
  and no trailing bytes. The composite opens the exact protected artefact set,
  binds its computed canonical digest to the authenticated manifest, and
  performs a final exact-set revalidation. A successful lease retains the exact
  member identities, validated manifest pin, and artefact-set digest. It is
  explicitly ineligible for trusted launch. Failure disposes a partially opened
  set and wipes owned temporary manifest and digest copies.
  Keep the release manifest out of the protected application directory and its
  exact artefact set. Including it would create self-reference and an unexpected
  directory entry. The caller supplies the out-of-band manifest bytes and owns
  the pin provenance. A sibling manifest, a pin derived from that manifest, or
  a pin compiled into an artefact covered by the same circular policy does not
  establish independent trust. The seam supplies no signature, release
  provenance, freshness, rollback protection, trusted installer policy, member
  file ACL, shared-runtime trust, loader atomicity, launch integration,
  production role, private handoff, role-bound `READY`, known-folder resolution,
  protected LocalAppData hierarchy provisioning or provenance, stale or crash
  recovery, production descriptor persistence, secure initial pipe-name
  delivery, Java integration, or HRC runtime evidence. One caller-supplied
  absolute deadline and cancellation token govern cooperative checks. They do
  not hard-preempt blocking native calls.
  Six tests cover exact owned-copy retention and final revalidation,
  authentication before structural parsing, noncanonical wire rejection,
  protected artefact-set digest binding with failure cleanup, one absolute
  operation budget, and a fixed golden identity.
  Checkpoint `64043e5` adds 7 audited native-release binding cases. They cover
  the two closed manifest profiles and their golden identities, bounded exact
  byte snapshots from the retained executable handle, late-failure wiping, and
  the native composite's authentication, PE-audit binding, revalidation, and
  partial-failure cleanup. `AuditedNativeFixtureReleaseLease` accepts only the
  exact one-file native profile. It copies the 4,096-byte image through the
  retained file handle, audits that owned snapshot, binds the audit digest to
  the authenticated manifest digest, and revalidates the exact set. It remains
  explicitly ineligible for trusted launch. The caller still owns manifest-pin
  provenance.
  Legacy harness-containment checkpoint `2a56de1` uses the internal test-
  harness-only `ContainedHarnessProcess`. It launches exactly the current
  generated apphost in one of two fixed public `Exit` or `Block` modes.
  These join three legacy IPC child modes, for five fixed public child modes in
  total. The build guard rejects managed `ProcessStartInfo` or `Process.Start`
  launch outside the legacy harness `Program.cs`. It admits exactly two native
  `CreateProcessW` call sites in production source: `ContainedHarnessProcess`
  and `ContainedAuditedNativeFixtureProcess`.
  It supplies an exact non-null `lpApplicationName` and fixed command line, an
  empty Unicode environment, the current executable directory, no inherited
  handles, and no standard I/O handles. It creates an unnamed, non-inheritable
  Job Object and reads back only `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`. It uses
  `PROC_THREAD_ATTRIBUTE_JOB_LIST` with suspended process creation. Before the
  exact `ResumeThread`, it requires one exact Job PID and checks a retained
  `ProcessIdentityLease` plus the image path. One absolute monotonic deadline
  and caller cancellation govern cooperative checks around synchronous native
  launch calls, including late-success rejection after resume. They do not
  hard-preempt those calls. Start failure and disposal close the last held Job
  handle before they wait for the exact retained process under a separate fixed
  five-second cleanup bound. Concurrent disposal coalesces.
  The five legacy harness-containment tests cover normal exit, explicit last-
  Job-handle closure that kills a blocking child, no managed entry before a
  pre-resume fault, late post-resume deadline cleanup, and concurrent disposal
  with an admitted exact-process exit wait.
  The suite does not terminate its parent abruptly. Kill-on-close semantics
  support cleanup when the final Job handle closes, but direct abrupt-parent-
  death and crash behaviour remain unexercised. This proof has no artefact-set
  trust integration, release provenance, shared-runtime or loader trust,
  production roles, private handoff, role-bound `READY`, token transfer, Java
  integration, HRC integration, sandbox, or same-user hostile-process defence.
  Checkpoint `fb9ba23` adds a project-owned, test-only AMD64 PE fixture with no
  C runtime. It imports exactly `GetCommandLineW`, `ExitProcess`, and `Sleep`
  from `KERNEL32.dll`. Its exact embedded neutral-language Windows manifest
  declares one `amd64` `win32` identity and `asInvoker` with `uiAccess=false`.
  It declares no dependent assembly or file. The build uses the recorded MSVC
  `14.44.35207` and Windows SDK `10.0.26100.0` paths in a cleared environment,
  gives each of two builds a separate temporary directory, and requires their
  4,096-byte outputs to be byte-identical. The PE records subsystem version
  `6.02`. `DependentLoadFlags=0x0800` requires Windows 10 RS1 or later, so
  `6.02` is not the effective runtime floor for this fixture.
  The strict structural audit authenticates a caller-supplied SHA-256 before
  parsing. On this pinned host, the observed golden SHA-256 is
  `3c9bee49acfffaea7f3fae2692900b47eef0e41e61e4ae7b14e2b1884a05fe34`.
  Treat that value as exact checkpoint evidence only. It is not signer or
  toolchain provenance and does not guarantee a cross-machine rebuild.
  The audit requires the exact PE32+ headers, four non-writable-executable
  sections, complete directory table, import descriptor and matching lookup
  and address slots, load configuration, debug records, neutral manifest
  resource, exception record, checksum, contiguous raw layout, and no
  certificate, relocation, gap, or overlay. The fixture defines fixed
  `--native-exit` and `--native-block` arguments; any other argument exits with
  code `87`. Bounded closed-environment runtime checks exercise Exit and the
  invalid-argument result. They do not launch Block before native Job
  containment exists.
  The embedded Windows manifest is not a native `HRCREL01` release-manifest
  binding by itself. Checkpoint `64043e5` supplies a separate authenticated
  native `HRCREL01` profile and retained-handle PE-audit composite. The fixture
  and composite remain ineligible for trusted launch. The evidence supplies no
  machine-code proof, Control Flow Guard instrumentation, Control-flow
  Enforcement Technology enforcement, trusted manifest-pin provenance,
  toolchain or signer provenance, System32 or KnownDLL module-identity proof,
  production role, private handoff, Java integration, or HRC runtime evidence.
  Committed checkpoint `70e0d77` launches only this exact synthetic one-file
  no-CRT fixture. It requires x64 debug ABI layouts and
  Windows 10 version 1709 build 16299 or later. It creates an unnamed Job with
  exact `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` read-back and assigns it through
  `PROC_THREAD_ATTRIBUTE_JOB_LIST`. It then requires exactly one Job PID and an
  AMD64 process.
  Launch uses exact canonical DOS `lpApplicationName` and working-directory
  values. A direct volume-GUID `CreateProcessW` attempt was rejected on this
  licensed host, so volume-GUID launch is not used. Treat that as a host fact,
  not a general Windows compatibility result. The launcher retains handles for
  every directory from the fixed-drive root through the application directory.
  Those handles omit `FILE_SHARE_DELETE`, reject reparses, and preserve each
  DOS and volume-GUID identity. The exact executable handle remains retained.
  `DEBUG_ONLY_THIS_PROCESS` supplies the initial `CREATE_PROCESS_DEBUG_EVENT`
  before user mode. The launcher compares its process and thread handles with
  the creation handles. It authenticates the event's direct image-file handle
  by exact length, SHA-256, 128-bit `FILE_ID`, volume identity, and volume-GUID
  path against the retained executable.
  The initial thread must have a prior `SuspendThread` count of `0`. The
  launcher continues the event, detaches, requires no remaining remote
  debugger, revalidates every retained identity, and requires the final
  `ResumeThread` prior count to be `1`. The complete create, debug, detach, and
  initial resume transaction runs on a fresh dedicated operating-system thread
  with `ExecutionContext` flow suppressed. The caller joins that thread non-
  abandonably. Failure cleanup resolves the thread-affine debug session non-
  abandonably. After detachment, cleanup closes the final Job handle and uses
  the exact process handle. If the bounded wait cannot prove exit, a detached
  process reaper retains all process, namespace, and audit authority until that
  exact handle signals. If `WaitForSingleObject` fails, that authority is
  retained indefinitely and terminal uncertainty is recorded. The build
  wrapper gives the validation process a separate 180-second outer watchdog.
  Five real cases cover the Windows and AMD64 ABI gate, exact Exit result `0`,
  the blocking role and application-directory pin through explicit Job close,
  all 9 injected launch stages, a late post-resume deadline, and coalesced
  concurrent disposal. The explicit Job-close result is not a direct abrupt-
  parent-death or crash test. The debug event is not a direct entry sentinel.
  The event file handle is not kernel section-object identity. The application
  directory still admits new children. A new-child ABA is harmless only for
  this exact one-file, no-app-local-dependency fixture policy; it is not a
  general loader-closure result. There is no System32 or KnownDLL module-
  identity proof, trusted installer or pin provenance, production role,
  private handoff, role-bound `READY`, Java integration, or HRC evidence.
- Treat descriptor parsing as structural validation only. After a secure token
  claim, require its HMAC, exact observer and broker bindings, freshness, and
  caller-supplied maximum lifetime to verify before use.
- Do not publish or transfer a real observer token through the Windows seam
  until a trusted installer or release policy independently supplies the
  canonical manifest bytes and pin provenance for each complete production
  artefact set,
  dedicated roles enter validated kill-on-close Job Object containment at
  process creation, private initial name delivery and role-bound `READY` are
  validated, and known-folder resolution, protected LocalAppData hierarchy
  provisioning and provenance, stale and crash recovery, and Java lifecycle
  integration are implemented and validated. Keep the atomic containment proof
  as a separate boundary until dedicated production roles integrate it. Resolve
  trusted installer policy and pin provenance, production application
  namespace, runtime-module identity, and loader trust before the private
  handoff and `READY` boundary. Keep the audited synthetic containment proof
  separate until dedicated production roles integrate it. Complete the
  production gate before private handoff or role-bound `READY` work.
  The existing-directory seam, in-memory store, and synthetic broker do not
  prove those runtime properties.
  Do not reuse a channel after an I/O failure or timeout. The pipe is not the
  system-wide HRC-control lease.
- Do not treat the explicit Job-handle-close test as a direct abrupt-parent-
  death or crash test. Require a separate production-role validation before
  relying on containment after abrupt parent termination.
- Treat public Job names and staging filenames as sensitive local plaintext.
  Base64URL encoding does not protect them.
- Do not activate the transport until the offline-tested ordered mailbox
  barrier is packaged and runtime-validated to supply replay, core fault state,
  callback health, and the adapter's authoritative first-failure latch.
- Treat `GAP`, `CURSOR_AHEAD`, session mismatch, a non-actionable checkpoint, a
  rejected event, observer fault, callback failure, transport failure, and lost
  continuity as terminal automation stop conditions. Never reset the cursor or
  adopt a session automatically.
- After a lost arm response, reuse only the same request UUID with identical
  operation, Job name, and timeout in the same observer session.
- Treat `ARM_ACCEPTED` as preparation only. Require the matching
  `ARM_CONFIRMED`, then enforce a controller-local round-trip and pre-input
  margin within that confirmed observer lease before any HRC input. A late or
  indeterminate response is a stop condition.
- Treat observer monotonic values and deadlines as opaque to other processes.
  Do not compare them with controller clocks or carry them across restart.
- Treat the offline Equinox start-level fixture as synthetic evidence only. It
  proves listener publication before a synthetic level-5 producer in isolated
  fresh JVMs. It does not prove HRC runtime activation or prevent arbitrary
  `Bundle.loadClass` or reflective early activation.
- Permit controller admission only after observer listener registration and
  endpoint publication. Observer activation failure does not stop Equinox from
  advancing to later start levels, so missing or invalid publication must
  independently refuse the controller and all HRC input.
- Do not stop, restart, update, uninstall, refresh, or republish the observer in
  a running framework. Keep it loaded until final framework shutdown. The
  offline no-runtime-unload policy does not prove provider-level listener
  drainage for dynamic Bundle changes. At final shutdown, require ordered
  admission closure, listener removal, mailbox drainage, transport shutdown,
  and completion of all in-flight control calls.
- Do not enable the OSGi activator or create an installable Bundle until the
  exact clean-launch configuration, provider set, Job-producer provenance, and
  start-level route are enforced as pre-live gates. The normal recorded route
  is not proof against a different activation mechanism.
- Do not treat the in-memory simpleconfigurator proposal as an installer. A
  future installer must verify exact source and target hashes, write through a
  guarded transaction, preserve a unique backup, and prove rollback.

## One workflow pattern

Validate one representative workflow pattern first. The pattern can then run
for multiple simulations in the user-specified order.

Map tree creation, rename, both Nash configurations, queue order, running,
completion or failure, Viewer save, saved-output verification, strategy export,
strategy-archive verification, Viewer-only hand-tab closure, and transition to
the next simulation.

After selecting a table size, overwrite and read back every active seat. If
committing the final active stack opens the next blank-row editor, cancel that
editor and verify that no extra player row was added before advancing.

Do not design beyond this repeatable workflow until the representative
lifecycle has a supported feasibility verdict.

## Validation expectations

Map the visible label, accessible name, control type, automation ID, supported patterns, keyboard path, required action, observable outcomes, and safety for each step.

Test the full lifecycle manually once. Do not repeat a long calculation only
for feasibility evidence.

Stop and record the exact blocker when any critical step requires blind clicking. Stop when completion cannot be distinguished from failure. Stop when saved output cannot be verified.

Validate changed Markdown before each commit. Review the exact diff. Keep scaffold and observed evidence in separate commits when discovery changes the documentation.

## Source-of-truth boundaries

- Store project purpose and boundaries in `README.md`.
- Store operating instructions in `AGENTS.md`.
- Store observed discovery evidence in `docs/feasibility.md`.
- Store verbatim external HRC script snapshots under `reference/hrc/`.
- Do not apply project changes to an external snapshot.
- Store project-owned HRC tree-building candidates under `scripts/hrc/`.
- Store the project-owned exact-status feasibility observer under
  `src/HrcJobObserver/`. Do not treat it as the standalone runner.
- Keep licence material, HRC private configuration, and sensitive poker data outside Git.
- Keep future implementation claims out of the repository until evidence supports them.
