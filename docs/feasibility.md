# HRC Beta feasibility evidence

## Evidence rules

Record only direct observations from the licensed host. Use `TBD` for missing
information and `TO CONFIRM` for an observation that requires confirmation.
Use `CONFIRMED` for a direct observation that does not require further
confirmation.

Do not record licence data, unnecessary poker data, or assumptions as facts.

## Environment findings

| Item | Observation | Evidence method | Status |
| --- | --- | --- | --- |
| Licensed host | `EM-3960X` | Read the `COMPUTERNAME` environment value on 10 August 2026. | CONFIRMED |
| Processor | AMD Ryzen Threadripper 3960X 24-Core Processor | Queried `Win32_Processor` on the licensed host. | CONFIRMED |
| Windows version | Microsoft Windows 11 Pro for Workstations, version `10.0.26200`, build `26200` | Queried `Win32_OperatingSystem` on the licensed host. | CONFIRMED |
| HRC Beta availability | HRC Beta is installed and running. Its main window title is `HRC Pro [Beta]`. | Inspected the running `hrc.exe` process and its executable path and window title. | CONFIRMED |
| HRC Beta version | The executable does not expose a file version or product version. The version in the HRC interface has not been inspected. | Inspected the `hrc.exe` version metadata. | TO CONFIRM |
| Accessibility Insights availability | No executable was present in the two standard `Program Files` locations checked. Other installation methods were not checked. | Checked the standard 64-bit and 32-bit installation paths. | TO CONFIRM |
| Microsoft Inspect availability | The x64 `inspect.exe` is available in Windows Kits `10.0.26100.0`. Its file version is `7.2.0.0`. | Inspected the installed Windows Kits executable and its version metadata. | CONFIRMED |
| Read-only HRC window capture | Codex can capture the current HRC window directly by using its live window handle. Euan does not need to send each screenshot manually. | A direct capture showed the open CI `10.0` Nash Calculation dialog and the HRC Progress pane. | CONFIRMED for discovery only; this does not identify or operate controls. |

## Corrected context

| Previous statement | Observed evidence | Resolution |
| --- | --- | --- |
| The licensed host used an AMD Ryzen 9 5950X processor. | `EM-3960X` reports an AMD Ryzen Threadripper 3960X 24-Core Processor. | Use the observed host and processor. The earlier 5950X reference is incorrect for this host. |
| Post-Finish Hand Settings contained `Tree Statistics and Abstractions`. | Hand Settings showed `Hand Data`, `Equity Model`, `Treeconfig`, and `Engine`. No `Tree Statistics and Abstractions` page was visible. | Do not require tree statistics for this workflow. |

## Selected workflow

- Workflow: Create and rename one true heads-up Monte Carlo tree, queue two
  Nash calculations, queue a Viewer save, and verify the saved output.
- Selection reason: This is the smallest equal-stack setup in the generated
  simulation run order.
- Selected inputs: Two players with `1 bb` starting stacks from the generated
  simulation run order.
- New output filename: `HU-1.hrcv`. The current hand is still unsaved as
  `*Hand 1`.
- Expected cost and duration: Tree creation completed during the observation
  session. Euan reports that both Nash calculations can take a long time. Exact
  durations remain TO CONFIRM.

## Required queue sequence

After tree creation, queue these operations without waiting for the previous
operation to finish:

1. Rename the tree to `HU-1` for this setup. Use the equivalent ordered stack
   name for other table sizes, such as `5m-10-30-30-20-12.5`.
1. Open Nash Calculation with `Alt+R`. Use `HRC 4.0 (Default)`, Full Tree,
   Until CI value is reached, and CI Target `10.0`. Keep Reset Regret and Reset
   Strategies clear. Queue the operation with OK.
1. Open Nash Calculation again. Keep the same algorithm, scope, and sampling
   mode. Set CI Target to `1.0`. Select Reset Strategies and keep Reset Regret
   clear. Queue the operation with OK.
1. Queue a Viewer save as an `.hrcv` file under
   `\\VAULT\sims\Preflop\HU`. Use the corresponding table-size folder for
   other workflows, such as `5m`.
1. Start the next simulation.

The required sequence is Euan's workflow definition. Queue behaviour and
completion remain TO CONFIRM through observed testing.

## Control map

| Lifecycle step | Visible label | Accessible name | Control type | Automation ID | Supported patterns | Keyboard path | Required action | Observable success | Observable failure | Safe to automate |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Configure stacks | `Stacks and Blinds` | Empty for both stack fields | Edit | `2036322` and `1642500` during this session; stability TO CONFIRM | Value, Text, and LegacyIAccessible | Both fields are focusable. Exact Tab order is TO CONFIRM. | Change both starting stacks from `80.0` to `1`. | The fields accepted `1`. The created HU table showed the expected shallow stacks. | TBD | TO CONFIRM: the fields have no accessible names and their numeric IDs have not been shown stable. |
| Select scripting | `Scripting` | `Scripting` | Tab item | Empty | SelectionItem and LegacyIAccessible | TO CONFIRM | Select the Scripting tab. | The `Script:` field and script controls appeared. | TBD | TO CONFIRM |
| Open script picker | Icon button beside `Script:` | Empty | Button | `1903002` during this session; stability TO CONFIRM | Invoke and LegacyIAccessible | No access key was exposed. | Open the script file picker. | The standard `Open` dialog appeared. | TBD | NO: no stable name, identifier, or keyboard path has been observed. |
| Select script file | `tree-building-hu-candidate.js`; `Open` | `tree-building-hu-candidate.js`; `Open` | List item; button | `0` for the file item; Open button not inspected | SelectionItem, Value, and LegacyIAccessible for the file item | The item was keyboard-focusable. The complete keyboard path is TO CONFIRM. | Select the HU candidate and open it. | HRC returned to Hand Setup. No script or tree error was visible in the captured states. | A script or tree error must stop the workflow. | TO CONFIRM |
| Finish tree setup | `Finish` | `Finish` | Button | TO CONFIRM | TO CONFIRM | TO CONFIRM | Finish tree creation after the estimate completes. | Hand Setup closed and an unsaved `*Hand 1` tab opened with the strategy and table views. | TBD | TO CONFIRM |
| Rename | TBD | TBD | TBD | TBD | TBD | TBD | Rename the tree to `HU-1`. | The document and queued operations use the required simulation name. | TBD | TO CONFIRM |
| Queue CI 10 | `Nash Calculation`; `OK` | TO CONFIRM | TO CONFIRM | TO CONFIRM | TO CONFIRM | `Alt+R` opens Nash Calculation. | Select `HRC 4.0 (Default)`, Full Tree, Until CI value is reached, CI Target `10.0`, Reset Regret clear, and Reset Strategies clear. Select OK. | The CI 10 operation appears in the queue. | A rejected or failed queue operation is distinguishable. | TO CONFIRM: the dialog configuration is observed, but OK and queue state are not. |
| Queue CI 1 | `Nash Calculation`; `Reset Strategies`; `OK` | TO CONFIRM | TO CONFIRM | TO CONFIRM | TO CONFIRM | `Alt+R` opens Nash Calculation. | Keep the same algorithm, scope, and sampling mode. Set CI Target to `1.0`, select Reset Strategies, keep Reset Regret clear, and select OK. | The CI 1 operation appears after CI 10 in the queue. | A rejected or failed queue operation is distinguishable. | TO CONFIRM: the dialog configuration is observed, but OK and queue state are not. |
| Detect running | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TO CONFIRM |
| Detect completion or failure | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TO CONFIRM |
| Queue Viewer save | Viewer save; filename and destination controls | TBD | TBD | TBD | TBD | TBD | Queue `HU-1.hrcv` to `\\VAULT\sims\Preflop\HU`. | The Viewer save appears after both calculations in the queue. | A rejected or failed save operation is distinguishable. | TO CONFIRM |
| Verify saved output | `HU-1.hrcv` | TBD | TBD | TBD | TBD | TBD | Verify the new file without modifying another output. | The expected `.hrcv` file exists in the HU folder and can be identified safely. | The file is absent, incomplete, or saved elsewhere. | TO CONFIRM |

## Observable states

| State | Visible evidence | Accessible evidence | Distinguishable | Notes |
| --- | --- | --- | --- | --- |
| Configured | Hand Setup closed. An unsaved `*Hand 1` tab opened with strategy, range, and HU table views. Progress showed no active operation. | The tree exposed `*Hand 1`, `Strategy Table`, `Hand Settings`, and `Run Nash Calculation (Alt+R)`. | CONFIRMED | Tree creation completed. The calculation was not started. |
| Queued | TBD | TBD | TO CONFIRM | Rename, CI 10, CI 1 with Reset Strategies, and Viewer save must be queued in that order. |
| Running | TBD | TBD | TO CONFIRM | TBD |
| CI 10 completed | TBD | TBD | TO CONFIRM | Completion must be distinguishable from failure before CI 1 is relied on. |
| CI 1 completed | TBD | TBD | TO CONFIRM | Reset Strategies must apply only to this operation. |
| Viewer saved | TBD | TBD | TO CONFIRM | The save must follow both calculations in the queue. |
| Failed | TBD | TBD | TO CONFIRM | TBD |
| Saved | The current hand has an asterisk and remains unsaved. | Save and Save As controls are exposed with keyboard shortcuts. | TO CONFIRM | No save action was performed. |
| Saved output verified | TBD | TBD | TO CONFIRM | TBD |

## Test runs

| Run | Date and time | Planned output | Result | Observed duration | Evidence | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 10 August 2026, 22:40 BST | `HU-1.hrcv` planned | NOT RUN | TBD | HU `1 bb` tree creation succeeded. The two required Nash dialog configurations were supplied separately. | The current hand remains unsaved as `*Hand 1`. No queue operation was observed. |

## Blockers

- The button used to open the script picker exposes Button and Invoke, but its
  accessible name and access key are empty. Its numeric Automation ID has not
  been shown stable. A supported keyboard path or stable identifier is
  required before this critical step is safe to automate.
- Rename and Viewer save controls have not been inspected.
- Queue acceptance, queue ordering, running, completion, failure, Viewer save,
  and saved-output verification have not been observed.

## Verdict

- Feasibility: TO CONFIRM
- Confidence: TO CONFIRM
- Basis: The selected HU tree was configured and created without a visible
  error. The required Nash configurations are defined. The queued lifecycle
  remains untested, and the script-picker control does not yet have a safe
  durable target.

## Next action

Inspect the rename, Nash Calculation, queue, and Viewer save controls for the
existing HU hand. Record their accessible properties and observable
queue states. Do not select OK in a Nash dialog until Euan confirms that this
specific `HU-1` test should start the long-running calculations.
