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
   `\\VAULT\sims\Preflop\<table-group>`. Save As defaults to
   `*.hrcz Complete Save`; select `*.hrcv Viewer Save` and confirm the `.hrcv`
   extension before Save. Example folders include `HU` and `5m`.
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
postflop policy. The HU candidate loaded successfully and created a `1 bb`
heads-up tree on `EM-3960X`. This result proves only script loading and tree
creation. A short HU demonstration covered rename, both Nash submissions, an
accidental Complete Save, a corrected Viewer Save, and output verification. A
follow-up demonstration created a non-empty strategy-export archive and closed
the saved hand tab. A Viewer-only follow-up preserved verified `.hrcv` and
`.zip` files after `Don't Save` closed the unsaved hand. No matching `.hrcz`
file was present. Long-run queue behaviour, completion or failure detection,
and the tree policy remain unverified. The three-through-six-player candidate
has not been validated inside HRC.

## Current next action

Map accessible properties and safe automation paths for rename, both Nash
submissions, Viewer Save, strategy export, the `Save Resource` prompt, and
`Don't Save`. Then validate queue order and explicit completion or failure
detection on a separately authorised long-running test.

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
