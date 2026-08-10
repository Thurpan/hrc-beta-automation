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
  Nash calculations, queue a Viewer save, export the strategies, close the
  completed tab, and continue to the next simulation.
- Selection reason: This is the smallest equal-stack setup in the generated
  simulation run order.
- Selected inputs: Two players with `1 bb` starting stacks from the generated
  simulation run order.
- Viewer output filename: `HU-1.hrcv`. The demonstration also created an
  unintended `HU-1.hrcz` Complete Save. It remains untouched.
- Strategy export filename: `HU-1.zip`. The demonstration created this file in
  the same HU folder as the Viewer output. The saved `HU-1.hrcz` tab was then
  closed.
- Expected cost and duration: The small demonstration calculations transitioned
  quickly. Euan reports that production calculations can take a long time.
  Exact production durations remain TO CONFIRM.

## Required workflow sequence

After tree creation, submit steps 2 through 5 without waiting for the previous
operation to finish:

1. Create the tree for the next setup in the simulation run order.
2. Rename the tree to `HU-1` for this setup. Use the equivalent ordered stack
   name for other table sizes, such as `5m-10-30-30-20-12.5`.
3. Open Nash Calculation with `Alt+R`. Use `HRC 4.0 (Default)`, Full Tree,
   Until CI value is reached, and CI Target `10.0`. Keep Reset Regret and Reset
   Strategies clear. Queue the operation with OK.
4. Open Nash Calculation again. Keep the same algorithm, scope, and sampling
   mode. Set CI Target to `1.0`. Select Reset Strategies and keep Reset Regret
   clear. Queue the operation with OK.
5. Queue a Viewer save as an `.hrcv` file under
   `\\VAULT\sims\Preflop\HU`. Use the corresponding table-size folder for
   other workflows, such as `5m`. Save As defaults to
   `*.hrcz Complete Save`. Select `*.hrcv Viewer Save` and confirm the `.hrcv`
   extension before Save.
6. After the queued operations finish successfully, open `Hand` →
   `Export Strategies`. Use `Complete Export`, Depth `16`, clear
   `PrettyPrint JSON`, and set `Node Filter Threshold %` to `0.1`. Save
   `HU-1.zip` with `*.zip Archived Json` in the HU folder.
7. Start the next simulation.

After step 6, verify the new Viewer file and strategy archive. Both files must
exist and must not be empty. Close the completed hand tab before step 7.

The required sequence is Euan's workflow definition. The second Nash dialog
opened while CI 10 Progress was visible. After submission, CI 1 Progress
appeared. The demonstration was too fast to establish queue order, calculation
completion, or failure states.

## Control map

| Lifecycle step | Visible label | Accessible name | Control type | Automation ID | Supported patterns | Keyboard path | Required action | Observable success | Observable failure | Safe to automate |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Configure stacks | `Stacks and Blinds` | Empty for both stack fields | Edit | `2036322` and `1642500` during this session; stability TO CONFIRM | Value, Text, and LegacyIAccessible | Both fields are focusable. Exact Tab order is TO CONFIRM. | Change both starting stacks from `80.0` to `1`. | The fields accepted `1`. The created HU table showed the expected shallow stacks. | TBD | TO CONFIRM: the fields have no accessible names and their numeric IDs have not been shown stable. |
| Select scripting | `Scripting` | `Scripting` | Tab item | Empty | SelectionItem and LegacyIAccessible | TO CONFIRM | Select the Scripting tab. | The `Script:` field and script controls appeared. | TBD | TO CONFIRM |
| Open script picker | Icon button beside `Script:` | Empty | Button | `1903002` during this session; stability TO CONFIRM | Invoke and LegacyIAccessible | No access key was exposed. | Open the script file picker. | The standard `Open` dialog appeared. | TBD | NO: no stable name, identifier, or keyboard path has been observed. |
| Select script file | `tree-building-hu-candidate.js`; `Open` | `tree-building-hu-candidate.js`; `Open` | List item; button | `0` for the file item; Open button not inspected | SelectionItem, Value, and LegacyIAccessible for the file item | The item was keyboard-focusable. The complete keyboard path is TO CONFIRM. | Select the HU candidate and open it. | HRC returned to Hand Setup. No script or tree error was visible in the captured states. | A script or tree error must stop the workflow. | TO CONFIRM |
| Finish tree setup | `Finish` | `Finish` | Button | TO CONFIRM | TO CONFIRM | TO CONFIRM | Finish tree creation after the estimate completes. | Hand Setup closed and an unsaved `*Hand 1` tab opened with the strategy and table views. | TBD | TO CONFIRM |
| Rename | `Hand`; `Rename Hand`; `Rename to:`; `OK` | TO CONFIRM | TO CONFIRM | TO CONFIRM | TO CONFIRM | `Ctrl+H, R` | Open Rename Hand, replace the current name with `HU-1`, and select OK. | The tab changed from `*Hand 2` to `*HU-1`. Later Progress text used `HU-1`. | TBD | TO CONFIRM: the visible path and shortcut are observed, but accessible properties are not. |
| Submit CI 10 | `Run Nash Calculation`; `Nash Calculation`; `OK` | TO CONFIRM | TO CONFIRM | TO CONFIRM | TO CONFIRM | `Alt+R` | Select `HRC 4.0 (Default)`, Full Tree, Until CI value is reached, CI Target `10.0`, Reset Regret clear, and Reset Strategies clear. Select OK. | Progress showed `HU-1: Monte Carlo Sampling` and `MC-CFR [Target CI < 10.00]`. | A rejected or failed submission must be distinguishable. | TO CONFIRM: submission and running state are visually observed, but accessible properties and failure state are not. |
| Submit CI 1 | `Run Nash Calculation`; `Reset Strategies`; `OK` | TO CONFIRM | TO CONFIRM | TO CONFIRM | TO CONFIRM | `Alt+R` | Keep the same algorithm, scope, and sampling mode. Set CI Target to `1.0`, select Reset Strategies, keep Reset Regret clear, and select OK. | The dialog opened while CI 10 Progress was visible. After OK, Progress showed `MC-CFR [Target CI < 1.00]`. | A rejected or failed submission must be distinguishable. | TO CONFIRM: the submission transition is observed, but durable queue order and accessible properties are not. |
| Detect running | `Progress`; `HU-1: Monte Carlo Sampling`; `MC-CFR [Target CI < 10.00]`; `MC-CFR [Target CI < 1.00]` | TO CONFIRM | TO CONFIRM | TO CONFIRM | TO CONFIRM | TO CONFIRM | Read the Progress pane without changing the operation. | The operation name, target CI, activity bar, and stop button were visible. | TBD | TO CONFIRM: the visible running state is observed, but accessible state is not. |
| Detect completion or failure | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TO CONFIRM |
| Viewer save | `File`; `Save As`; `Save as type:`; `*.hrcv Viewer Save`; `Save` | TO CONFIRM | TO CONFIRM | TO CONFIRM | TO CONFIRM | `Ctrl+Alt+S` opens Save As. | Open Save As, select `*.hrcv Viewer Save`, confirm `HU-1.hrcv`, browse to `\\VAULT\sims\Preflop\HU`, and select Save. | Selecting Viewer Save changed the proposed extension from `.hrcz` to `.hrcv`. HRC returned to the main view after Save. | The default `*.hrcz Complete Save` can create the wrong output type if it is not changed. | TO CONFIRM: the visible flow is observed, but accessible properties and long-run queue behaviour are not. |
| Verify saved output | `HU-1.hrcv` | Not applicable | File | Not applicable | Not applicable | Not applicable | Verify the new file without modifying another output. | `\\VAULT\sims\Preflop\HU\HU-1.hrcv` existed with a non-zero size of 5,506 bytes at 23:10 BST. | The file is absent, empty, or saved elsewhere. | YES for read-only verification of this exact new file. |
| Submit strategy export | `Hand`; `Export Strategies`; `Complete Export`; `Depth:`; `PrettyPrint JSON`; `Node Filter Threshold %`; `OK` | TO CONFIRM | TO CONFIRM | TO CONFIRM | TO CONFIRM | The menu shows `Ctrl+H, E`; shortcut operation is TO CONFIRM. | Change the initial Depth value from `2` to `16`. Keep `Complete Export`, clear `PrettyPrint JSON`, keep the threshold at `0.1`, and select OK. Save `HU-1.zip` under `\\VAULT\sims\Preflop\HU` with `*.zip Archived Json`. | HRC returned to the source tab. The new archive was present at the selected path. | No explicit export-success or failure message was captured. | TO CONFIRM: the visible flow is observed, but accessible properties and failure states are not. |
| Verify strategy archive | `HU-1.zip` | Not applicable | File | Not applicable | Not applicable | Not applicable | Verify the new archive without opening or modifying it. | `\\VAULT\sims\Preflop\HU\HU-1.zip` existed with a non-zero size of 1,816 bytes at 23:18 BST. | The file is absent, empty, or saved elsewhere. | YES for read-only metadata verification of this exact new file. |
| Close completed hand tab | Source tab `HU-1.hrcz`; `Home` | TO CONFIRM | TO CONFIRM | TO CONFIRM | TO CONFIRM | TO CONFIRM | Close the completed hand tab after the Viewer output and strategy archive are verified. | The `HU-1.hrcz` tab disappeared and only `Home` remained. | A prompt, error, or tab that remains open must stop the transition. | TO CONFIRM: the close input and accessible properties were not captured. The demonstrated tab had an earlier Complete Save. |
| Start next simulation | `Hand`; `Start New Calculation` | TO CONFIRM | TO CONFIRM | TO CONFIRM | TO CONFIRM | `Ctrl+W, H` | Start the next simulation only after both outputs are verified and the completed tab is closed. | TBD | TBD | TO CONFIRM: the menu item and shortcut are visible, but the action was not demonstrated. |

## Observable states

| State | Visible evidence | Accessible evidence | Distinguishable | Notes |
| --- | --- | --- | --- | --- |
| Configured | Hand Setup closed. An unsaved `*Hand 1` tab opened with strategy, range, and HU table views. Progress showed no active operation. | The tree exposed `*Hand 1`, `Strategy Table`, `Hand Settings`, and `Run Nash Calculation (Alt+R)`. | CONFIRMED | Tree creation completed. The calculation was not started. |
| Renamed | The tab changed from `*Hand 2` to `*HU-1`. | TO CONFIRM | CONFIRMED visually | Progress later used the `HU-1` name. |
| Queued | No persistent queue list was visible in the captured states. | TBD | TO CONFIRM | The CI 1 dialog opened while CI 10 was visible, but the small operation transitioned quickly. |
| CI 10 running | Progress showed `HU-1: Monte Carlo Sampling` and target CI `< 10.00`. | TBD | CONFIRMED visually | A red stop button and activity bar were visible. |
| CI 1 running | Progress showed `HU-1: Monte Carlo Sampling` and target CI `< 1.00`. | TBD | CONFIRMED visually | Reset Strategies was selected in the submitted dialog. |
| CI 10 no longer displayed | The CI 10 line was replaced by the CI 1 line. | TBD | CONFIRMED visually | The reason for the transition is TO CONFIRM. No explicit successful-completion marker was captured. |
| No operation displayed | Progress later showed `No operations to display at this time.` | TBD | CONFIRMED visually | This text alone does not distinguish success from failure. |
| Viewer saved | The Save As dialog accepted `HU-1.hrcv` with `*.hrcv Viewer Save`. | File existence was verified separately. | CONFIRMED | The open tab remained `HU-1.hrcz` after Viewer Save. |
| Failed | TBD | TBD | TO CONFIRM | TBD |
| Complete Save | The first Save As used the default `*.hrcz Complete Save` in error. The tab changed to `HU-1.hrcz`. | TBD | CONFIRMED visually | The unintended file remains untouched. |
| Viewer output verified | The new `.hrcv` file exists at the required HU path and has non-zero size. | Read-only file metadata returned the expected path, size, and timestamp. | CONFIRMED | File contents were not opened or modified. |
| Strategy archive created | The export dialog accepted the required settings and `HU-1.zip` path. HRC returned to the source tab. | File existence was verified separately. | CONFIRMED for non-zero file creation only | No explicit export-success message was visible. |
| Strategy archive metadata verified | The new `.zip` file exists at the required HU path and has non-zero size. | Read-only file metadata returned the expected path, size, and timestamp. | CONFIRMED | The archive was not opened. Its structure, JSON content, and strategy completeness remain unverified. |
| Completed tab closed | The source tab disappeared and only `Home` remained. | TO CONFIRM | CONFIRMED visually | No prompt or error was captured. The source tab was the saved `HU-1.hrcz` tab. |
| Next simulation started | TBD | TBD | TO CONFIRM | The transition to the next simulation was not demonstrated. |

## Test runs

| Run | Date and time | Planned output | Result | Observed duration | Evidence | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 10 August 2026, 22:40 BST | None | TREE CREATED | TBD | HU `1 bb` tree creation succeeded and opened `*Hand 1`. | No calculation or save was performed in this observation. |
| 2 | 10 August 2026, 23:08–23:10 BST | `HU-1.hrcv`; unintended `HU-1.hrcz` | PARTIAL DEMONSTRATION | Progress changed to no operation displayed within the observation period. Explicit completion and production duration remain TO CONFIRM. | The demonstration renamed `*Hand 2` to `*HU-1`, submitted both Nash configurations, showed both running targets, made an accidental Complete Save, corrected it with Viewer Save, and verified the Viewer file. | This observation began with `*Hand 2` and is separate from run 1. Long-run queue order and explicit calculation success or failure remain unconfirmed. The unintended Complete Save remains untouched. |
| 3 | 10 August 2026, 23:18 BST | `HU-1.zip` | PARTIAL DEMONSTRATION | The export and close transition completed within the observation period. | The demonstration kept Complete Export, changed Depth from `2` to `16`, kept PrettyPrint JSON clear, and kept the threshold at `0.1`. It saved a non-empty archive and then closed the source tab. | The source tab was `HU-1.hrcz` from run 2. The archive contents and close behaviour after a Viewer-only save remain unverified. |

## Blockers

- The button used to open the script picker exposes Button and Invoke, but its
  accessible name and access key are empty. Its numeric Automation ID has not
  been shown stable. A supported keyboard path or stable identifier is
  required before this critical step is safe to automate.
- Rename, Nash, Viewer Save, Export Strategies, and tab-close controls have not
  had their accessible properties inspected.
- Long-run queue order and explicit completion or failure states have not been
  observed.
- The demonstrated tab close followed an accidental Complete Save. Closing a
  hand that has only a Viewer Save might produce a prompt. This path has not
  been demonstrated.
- The transition to the next simulation has not been demonstrated.

## Verdict

- Feasibility: TO CONFIRM
- Confidence: TO CONFIRM
- Basis: The selected HU tree was configured and created without a visible
  error. Rename, both Nash submissions, running targets, Viewer Save, and
  read-only output verification were observed. Strategy-export submission,
  non-zero archive creation, read-only archive verification, and source-tab
  closure were also observed. Long-run queue behaviour, explicit completion or
  failure detection, Viewer-only tab closure, and safe accessible targets
  remain unconfirmed. The script-picker control does not yet have a safe
  durable target.

## Next action

Inspect the accessible properties of Rename Hand, Nash Calculation, Progress,
Save As, Export Strategies, and the hand-tab close control without starting
another calculation. Map a safe path for selecting `*.hrcv Viewer Save`.
Confirm the close behaviour after a Viewer-only save. Do not start an automated
or long-running test until the controls are mapped and Euan authorises that
specific test.
