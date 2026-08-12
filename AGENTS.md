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

## Exact-status transport safety

- Keep the observer endpoint on IPv4 loopback. Do not treat loopback as process
  identity, same-user access control, encryption, confidentiality, or the
  HRC-control lease.
- Generate a fresh cryptographically random 32-byte bearer token for each
  observer start. Transfer the token and endpoint only through a validated
  same-user protected mechanism. Never commit, persist, log, or echo the token.
- Treat `src/HrcJobObserver/windows-bootstrap/` as source/test-only. Its
  same-process harness proves an exact applied protected DACL, process identity
  checks from both pipe endpoints, and bounded one-shot frame operations only.
  It does not prove a token-transfer protocol, independent cross-process
  operation, Java integration, or HRC runtime use.
- Do not publish or transfer a real observer token through the Windows seam
  until token-copy ownership, wiping, acknowledgement, revocation, secure name
  delivery, independent-process tests, and Java lifecycle integration are
  implemented and validated. Do not reuse a channel after an I/O failure or
  timeout. The pipe is not the system-wide HRC-control lease.
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
- Do not unload observer code unless listener removal, mailbox drainage,
  transport shutdown, and all in-flight control calls have completed cleanly.
- Do not enable the OSGi activator or create an installable Bundle while the
  recorded registration-plus-baseline race and provider-level listener-drain
  gap remain unresolved.
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
