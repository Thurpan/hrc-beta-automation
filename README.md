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

The project is in accessibility inspection and first-workflow definition. The
licensed host is `EM-3960X`, which reports an AMD Ryzen Threadripper 3960X
processor.

The earlier 5950X portfolio reference was incorrect for this host. Local
machine evidence and Euan's licence confirmation supersede that reference.

## Boundaries

- Prove one small disposable workflow before broader design.
- Do not create unattended automation during feasibility discovery.
- Do not use blind coordinate clicks.
- Do not overwrite or delete existing HRC data.
- Do not expose licence data, poker data, or other sensitive information.
- Do not install software or start an expensive calculation without Euan's approval.
- Do not choose an implementation language or framework before feasibility is proven.

## Current next action

Inspect HRC Beta with Microsoft Inspect. Select and test one small workflow
through configuration, start, running detection, completion or failure
detection, save, and saved-output verification.

## Definition of done

Feasibility discovery is complete when observed evidence supports a clear
verdict for one workflow. The evidence must cover its controls, states,
keyboard paths, safety, test runs, blockers, and next action.

A feasible workflow must complete the full lifecycle once. Repeat it once when
the workflow is quick and safe.

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
