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

## Corrected context

| Previous statement | Observed evidence | Resolution |
| --- | --- | --- |
| The licensed host used an AMD Ryzen 9 5950X processor. | `EM-3960X` reports an AMD Ryzen Threadripper 3960X 24-Core Processor. | Use the observed host and processor. The earlier 5950X reference is incorrect for this host. |

## Selected workflow

- Workflow: Create one true heads-up Monte Carlo tree with
  `tree-building-hu-candidate.js`.
- Selection reason: This is the smallest equal-stack setup in the generated
  simulation run order.
- Disposable inputs: Two players with `1 bb` starting stacks. Other hand
  settings remain TO CONFIRM.
- New output filename: TBD. The current hand is unsaved as `*Hand 1`.
- Expected cost and duration: Tree creation completed during the observation
  session. The exact duration, node estimate, memory estimate, and calculation
  cost remain TO CONFIRM.

## Control map

| Lifecycle step | Visible label | Accessible name | Control type | Automation ID | Supported patterns | Keyboard path | Required action | Observable success | Observable failure | Safe to automate |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Configure stacks | `Stacks and Blinds` | Empty for both stack fields | Edit | `2036322` and `1642500` during this session; stability TO CONFIRM | Value, Text, and LegacyIAccessible | Both fields are focusable. Exact Tab order is TO CONFIRM. | Change both starting stacks from `80.0` to `1`. | The fields accepted `1`. The created HU table showed the expected shallow stacks. | TBD | TO CONFIRM: the fields have no accessible names and their numeric IDs have not been shown stable. |
| Select scripting | `Scripting` | `Scripting` | Tab item | Empty | SelectionItem and LegacyIAccessible | TO CONFIRM | Select the Scripting tab. | The `Script:` field and script controls appeared. | TBD | TO CONFIRM |
| Open script picker | Icon button beside `Script:` | Empty | Button | `1903002` during this session; stability TO CONFIRM | Invoke and LegacyIAccessible | No access key was exposed. | Open the script file picker. | The standard `Open` dialog appeared. | TBD | NO: no stable name, identifier, or keyboard path has been observed. |
| Select script file | `tree-building-hu-candidate.js`; `Open` | `tree-building-hu-candidate.js`; `Open` | List item; button | `0` for the file item; Open button not inspected | SelectionItem, Value, and LegacyIAccessible for the file item | The item was keyboard-focusable. The complete keyboard path is TO CONFIRM. | Select the HU candidate and open it. | HRC returned to Hand Setup. No script or tree error was visible in the captured states. | A script or tree error must stop the workflow. | TO CONFIRM |
| Finish tree setup | `Finish` | `Finish` | Button | TO CONFIRM | TO CONFIRM | TO CONFIRM | Finish tree creation after the estimate completes. | Hand Setup closed and an unsaved `*Hand 1` tab opened with the strategy and table views. | TBD | TO CONFIRM |
| Start | Green play button | `Run Nash Calculation (Alt+R)` | Button | Empty | Invoke and LegacyIAccessible | `Alt+R` | Start the calculation only after cost approval. | TBD | TBD | TO CONFIRM: the action has not been performed. |
| Detect running | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TO CONFIRM |
| Detect completion or failure | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TO CONFIRM |
| Save | Save and Save As toolbar buttons | `Save (Ctrl+S)`; `Save As (Ctrl+Alt+S)` | Button | TO CONFIRM | TO CONFIRM | `Ctrl+S`; `Ctrl+Alt+S` | Save to a new disposable filename. | TBD | TBD | TO CONFIRM: save has not been performed. |
| Verify saved output | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TO CONFIRM |

## Observable states

| State | Visible evidence | Accessible evidence | Distinguishable | Notes |
| --- | --- | --- | --- | --- |
| Configured | Hand Setup closed. An unsaved `*Hand 1` tab opened with strategy, range, and HU table views. Progress showed no active operation. | The tree exposed `*Hand 1`, `Strategy Table`, `Hand Settings`, and `Run Nash Calculation (Alt+R)`. | CONFIRMED | Tree creation completed. The calculation was not started. |
| Running | TBD | TBD | TO CONFIRM | TBD |
| Completed | TBD | TBD | TO CONFIRM | TBD |
| Failed | TBD | TBD | TO CONFIRM | TBD |
| Saved | The current hand has an asterisk and remains unsaved. | Save and Save As controls are exposed with keyboard shortcuts. | TO CONFIRM | No save action was performed. |
| Saved output verified | TBD | TBD | TO CONFIRM | TBD |

## Test runs

| Run | Date and time | Disposable filename | Result | Observed duration | Evidence | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 10 August 2026, 22:40 BST | TBD | NOT RUN | TBD | HU `1 bb` tree creation succeeded. The calculation was not started. | The current hand remains unsaved as `*Hand 1`. |
| 2 | TBD | TBD | NOT RUN | TBD | TBD | Run only when quick and safe. |

## Blockers

- The button used to open the script picker exposes Button and Invoke, but its
  accessible name and access key are empty. Its numeric Automation ID has not
  been shown stable. A supported keyboard path or stable identifier is
  required before this critical step is safe to automate.
- The tree node estimate and memory estimate were not captured.
- Running, completion, failure, save, and saved-output verification have not
  been observed.

## Verdict

- Feasibility: TO CONFIRM
- Confidence: TO CONFIRM
- Basis: The selected HU tree was configured and created without a visible
  error. The start and save controls expose keyboard shortcuts. The full
  lifecycle remains untested, and the script-picker control does not yet have
  a safe durable target.

## Next action

Inspect `Tree Statistics and Abstractions` for the existing disposable hand.
Record the node and memory estimates without changing the hand. Then identify
a supported keyboard path or stable identifier for the unnamed script-picker
button. Do not start the calculation until Euan reviews the estimate and
explicitly approves the cost.
