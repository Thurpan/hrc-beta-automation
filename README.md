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
produces an in-memory proposal only. The current suites pass 30 core tests,
34 adapter tests, 25 transport tests, 10 joined-assembly tests, 14 lifecycle
tests, and 13 packaging tests.

The transport implements bounded protocol version `1`, validates cursor-bound
checkpoint replay, and serialises only allow-listed event primitives. The
offline assembly now provides the real ordered `ObserverTransportControl`:
callbacks, checkpoints, and arms share one mailbox sequence. A second post-arm
marker drains callbacks admitted around an arm, verifies request ownership,
and starts a new observer-local lease. Every successfully confirmed exact
idempotent retry renews that lease. `ARM_CONFIRMED` records each confirmed
lease. The
joined tests exercise this control through an actual loopback socket in one
JVM. The response is not yet authority for HRC input: the future controller
must enforce a local round-trip and pre-input margin within the lease. The
lifecycle implements synthetic manager registration, two bounded baseline
scans, startup callback admission, rollback, and ordered shutdown. Its public
activator remains deliberately disabled. The project still does not implement
secure token or endpoint provisioning, an activatable manifest, controller
ownership, cross-process proof, or persistence across restart.
The offline adapter, runtime, and lifecycle builds read and hash public provider
JARs from the HRC installation. A separate read-only inspection supplied the
configuration facts to the in-memory planner. None of these layers has
interacted with the running HRC process, its UI, or real Eclipse callback
delivery. They do not yet make
HRC terminal results available to a controller and do not change the
feasibility verdict.

Next, close the two lifecycle blockers before creating an installable Bundle.
The public Eclipse APIs do not make listener registration plus `find(null)`
atomic, and listener removal does not prove provider-level callback drainage.
Implement secure same-user token and endpoint publication through a reviewed
Windows native seam. Then add a deterministic JAR, manifest, guarded install,
and rollback design. Extend the active-process runtime identity gate for the
adapter's Equinox Common and Eclipse OSGi providers before live use. Do not
install it or restart HRC while the dirty tabs `*Hand 7` and
`*From Hand 7` remain protected. Resolve those resources explicitly before the
first clean-start observer validation. Reserve the authorised smoke until the
runtime observer and standalone control path are ready.

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
