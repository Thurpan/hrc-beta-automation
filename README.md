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

The earlier 5950X portfolio reference was incorrect for this host. Local
machine evidence and Euan's licence confirmation supersede that reference.

## Boundaries

- Prove one small non-overwriting workflow before broader design.
- Do not create unattended automation during feasibility discovery.
- Do not use blind coordinate clicks.
- Do not overwrite or delete existing HRC data.
- Do not expose licence data, poker data, or other sensitive information.
- Do not install software or start an expensive calculation unless Euan has
  authorised the current run or batch.
- Do not choose an application-automation language or framework before
  feasibility is proven.
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
   `5m-10-30-30-20-12.5`.
3. Queue a full-tree Nash calculation with `HRC 4.0 (Default)`. Run until the
   confidence interval (CI) reaches `10.0`. Keep Reset Regret and Reset
   Strategies clear.
4. Queue a second full-tree Nash calculation with `HRC 4.0 (Default)`. Run
   until CI reaches `1.0`. Select Reset Strategies and keep Reset Regret clear.
5. Queue a Viewer Save as an `.hrcv` file under
   `\\VAULT\sims\Preflop\<table-group>`. Save As can retain the previously
   selected type or open with `*.hrcz Complete Save`. Before every save, verify
   the destination, select `*.hrcv Viewer Save`, and confirm the simulation
   filename and `.hrcv` extension. Example folders include `HU` and `5m`.
6. After the queued operations finish successfully, export the strategies
   through `Hand` → `Export Strategies`. Use `Complete Export`, Depth `16`,
   clear `PrettyPrint JSON`, and set `Node Filter Threshold %` to `0.1`. Save
   `<simulation-name>.zip` in the same table-group folder with
   `*.zip Archived Json`. In inspected HRC `4.1.1`, Complete Export is
   unlimited-depth and does not consume the visible Depth setting; still set
   and read back `16` to match the required operator workflow.
7. Move to the next simulation and repeat the workflow.

Before step 1, verify that neither exact target
`<simulation-name>.hrcv` nor `<simulation-name>.zip` already exists. Stop and
choose a new unique simulation name if either target exists. Recheck the exact
target immediately before each Save. If HRC shows any overwrite prompt, select
Cancel and stop; never replace an existing output.

After tree creation, submit steps 2 through 5 without waiting for the previous
operation to finish. The two Nash calculations can take a long time. Wait for
these queued operations to finish successfully before step 6.

After step 6, verify the new Viewer file and strategy archive. Both files must
exist and must not be empty. Then close the completed hand tab before step 7.
HRC shows a `Save Resource` prompt because Viewer Save does not save the
editable hand. Confirm that the prompt names the expected completed simulation.
Only then select `Don't Save` and confirm that HRC returns to `Home`. Stop on
any filename or prompt mismatch.

Treat an operation as queued only when HRC shows it in the expected order.
Stop the workflow if an operation fails or the Viewer output cannot be
verified. Also stop if the strategy archive cannot be verified or the
completed tab cannot be closed safely.

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
created non-empty strategy-export archives and verified Viewer-only tab
closure. The supervised `HU-2` run created non-empty `.hrcv` and `.zip` files,
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

These checks prove the keyboard portions of the supervised route,
different-valid-value entry, visual read-back, one rejected-input recovery,
exact candidate load, current equal-`2 bb` Preview, and observed Nash
configuration and non-submitting close routes. They do not yet provide
machine-readable stack or Nash read-back. They also do not establish a reliable
foreground and focus contract or a durable Finish operation. The live provider
continued to disagree with visible focus. Continue supervised discovery of
Finish and machine-readable Nash read-back. Then validate an authorised,
controlled submission and its post-states. Follow it with export, tab close,
`Don't Save`, and Progress completion or failure. Do not add the project-owned
automation runner until feasibility has a supported verdict. Retain separate
Preview checks for other table sizes and boundary stacks. Verify the Save As
destination, Viewer type, filename, and extension every time.

## Definition of done

Feasibility discovery is complete when observed evidence supports a clear
verdict for the representative workflow. The evidence must cover rename, both
Nash configurations, queue order, completion or failure, Viewer save, and
saved-output verification. It must also cover strategy export, strategy-archive
verification, and Viewer-only hand-tab closure before the next simulation.

A feasible workflow must complete the full lifecycle once. Do not repeat a
long-running calculation only to obtain a second feasibility sample.

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
