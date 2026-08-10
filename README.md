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
1. Rename the tree. Use names such as `HU-1` or
   `5m-10-30-30-20-12.5`.
1. Queue a full-tree Nash calculation with `HRC 4.0 (Default)`. Run until the
   confidence interval (CI) reaches `10.0`. Keep Reset Regret and Reset
   Strategies clear.
1. Queue a second full-tree Nash calculation with `HRC 4.0 (Default)`. Run
   until CI reaches `1.0`. Select Reset Strategies and keep Reset Regret clear.
1. Queue a Viewer save as an `.hrcv` file under
   `\\VAULT\sims\Preflop\<table-group>`. Example folders include `HU` and
   `5m`.
1. Move to the next simulation and repeat the workflow.

After tree creation, queue steps 2 through 5 without waiting for the previous
operation to finish. The two Nash calculations can take a long time.

Treat an operation as queued only when HRC shows it in the expected order.
Stop the workflow if an operation fails or the Viewer output cannot be
verified.

## HRC tree-building candidates

Two standalone, offline-reviewed candidates live under `scripts/hrc/`:

- [`tree-building-3m-6m-candidate.js`](scripts/hrc/tree-building-3m-6m-candidate.js)
  for non-straddled configurations with three through six players; and
- [`tree-building-hu-candidate.js`](scripts/hrc/tree-building-hu-candidate.js)
  for a true two-player configuration.

They use separate preflop policies from the sizing workbook and a shared
postflop policy. The HU candidate loaded successfully and created a `1 bb`
heads-up tree on `EM-3960X`. This result proves only script loading and tree
creation. The tree policy and calculation lifecycle remain unverified. The
three-through-six-player candidate has not been validated inside HRC.

## Current next action

Map the remaining controls and states for rename, both Nash queue operations,
Viewer save, queue progress, completion or failure, and saved-output
verification. Then test one authorised `HU-1` lifecycle end to end.

## Definition of done

Feasibility discovery is complete when observed evidence supports a clear
verdict for the representative workflow. The evidence must cover rename, both
Nash configurations, queue order, completion or failure, Viewer save, and
saved-output verification.

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
