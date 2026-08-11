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

1. Create the tree for the next setup in the simulation run order.
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
   `*.zip Archived Json`.
7. Move to the next simulation and repeat the workflow.

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
It needs a small HRC Preview recheck; no Nash run is required. The inclusive
`5 bb` boundary, the first supported stack above it, dynamic post-fold
behaviour, and all multiway behaviour remain unverified in HRC.

A short HU demonstration covered rename, both Nash submissions, an accidental
Complete Save, a corrected Viewer Save, and output verification. Later runs
created non-empty strategy-export archives and verified Viewer-only tab
closure. The supervised `HU-2` run created non-empty `.hrcv` and `.zip` files,
created no matching `.hrcz` file, and returned to `Home` after `Don't Save`.
Long-run queue behaviour, completion or failure detection, and the remaining
tree policy are unverified. The three-through-six-player candidate has not
produced a tree inside HRC. A supervised five-player setup displayed the visible
order `HJ`, `CO`, `BU`, `SB`, `BB`, showed stacks of `10`, `20`, `30`, `40`,
and `50` bb, and advanced to Betting Setup. The HRC-tested pre-correction
candidate then stopped with `Error: Effective stack does not match a configured
workbook column: 100000`; Finish remained disabled. Its SHA-256 was
`128110cc73abd5bfd45167d426935e8d43923ae8648deffbc0251f4d03178782`.
The reported amount is the supported `10 bb` stack in HRC units. The corrected
candidate converts state values with the nominal big blind and has SHA-256
`fa2612bd1d3b01a8aa6419fc3697450cf708adff73fc6d085e2223ff605d7c63`.
Offline regression tests pass. It still requires an HRC tree-estimate and
Preview check.

## Current next action

Reload the corrected
[`tree-building-3m-6m-candidate.js`](scripts/hrc/tree-building-3m-6m-candidate.js)
in the current five-player setup. Verify its `fa2612bd...` SHA-256 first. Do not
select the unchanged copy under `C:\Projects\hrc-beta-automation`. Confirm that
HRC produces a tree estimate without an error, then inspect Preview without
selecting Finish. Next, find durable paths for creating player rows, targeting
and reading stack cells, and opening the unnamed script picker. Retain the
existing Nash, export, tab-close, and Progress-state blockers. Verify the Save
As destination, Viewer type, filename, and extension every time.

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
