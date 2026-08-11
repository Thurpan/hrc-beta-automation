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
| Codex activation side effect | A Codex discovery activation call restored HRC from its maximised state and changed its window bounds. A later native focus-only call preserved the current bounds. | Compared the HRC window rectangle before and after each focus method during the `HU-2` run. | CONFIRMED for the discovery tool only; do not make that activation call part of automation. |
| Current HRC window state | HRC was restored and sized close to the full work area. It was not maximised. No resize, maximise, or activation action was issued during the 11 August control-map run. | Euan supplied a full-screen screenshot that showed the restored title-bar state. The control log contained no window-state action. | CONFIRMED for this run. Do not infer maximised state from capture dimensions. |
| Coordinate-target failure | An unverified coordinate intended for the hand-tab close control selected the tree root row instead. No setting or output changed. The later close used a point that first showed the exact `Close` tooltip. | Compared the expected target with the captured cursor position and the resulting HRC state during the `HU-2` run. | CONFIRMED for discovery; raw coordinates are not a safe automation path. |

## Corrected context

| Previous statement | Observed evidence | Resolution |
| --- | --- | --- |
| The licensed host used an AMD Ryzen 9 5950X processor. | `EM-3960X` reports an AMD Ryzen Threadripper 3960X 24-Core Processor. | Use the observed host and processor. The earlier 5950X reference is incorrect for this host. |
| Post-Finish Hand Settings contained `Tree Statistics and Abstractions`. | Hand Settings showed `Hand Data`, `Equity Model`, `Treeconfig`, and `Engine`. No `Tree Statistics and Abstractions` page was visible. | Do not require tree statistics for this workflow. |

`Tree Statistics and Abstractions` is visible inside the pre-Finish Betting
Setup page. This does not contradict the post-Finish Hand Settings finding.

## Idle control-map discovery on 11 August 2026

This discovery created one disposable, unsaved two-node hand. It did not submit
a Nash calculation or write an output file. The resulting `*Hand 6` tab remains
open because its required Viewer and strategy outputs do not exist.

- The named `New: Monte Carlo Hand` Home link exposed ID `3342566`. The first
  semantic click returned an unknown outcome. A refreshed retry opened Hand
  Setup.
- Hand Setup exposed `Next` as a named button with ID `268476`. Semantic click,
  Tab, and Enter did not reach the owned dialog. Euan clicked `Next` manually.
- Betting Setup exposed a `Scripting` tab and a `Script:` edit with ID `334118`.
  The tab items shared one repeated element index in the discovery provider.
- The script-picker folder button had no accessible name. Its ID was `334110`,
  which differs from the earlier session value `1903002`. Semantic invocation
  failed. A one-use screenshot-located click opened the standard `Open` dialog.
- The `Open` dialog exposed both candidate filenames, `File name`, `Open`, and
  `Cancel`. `Alt+N` visibly focused `File name`. The reported focused element
  incorrectly remained the background search box. Typing the exact HU filename
  and pressing Enter loaded the candidate.
- HRC loaded the file from `C:\Projects\hrc-beta-automation\scripts\hrc`.
  Its SHA-256 hash matched the candidate in this worktree.
- The loaded candidate changed Total Nodes from `16` to `2`. Expanded Preview
  showed `R 2.00 SB PRE` with one child, `C 1.00 BB PRE`. No SB completion
  branch was present.
- Enter invoked the default `Finish` button and created unsaved `*Hand 6`.
  `Rename Hand` exposed a labelled edit and named buttons. Escape cancelled it.
- `Alt+R` opened Nash Calculation. Only `OK` and `Cancel` were exposed. The
  algorithm, scope, sampling, CI, and reset controls remained absent from the
  accessibility tree. The current CI value was `1.0`. Escape did not close the
  dialog. A screenshot-located `Cancel` click closed it without submission.
- `Ctrl+Alt+S` opened the standard `Save As` dialog at the HU folder. This run
  retained `*.hrcv Viewer Save`; the earlier run defaulted to Complete Save.
  The file type must therefore be verified on every save. Escape cancelled.
- The chord `Ctrl+H`, then `E` opened Export Strategies. The scope combo, Depth
  spinner, range tree, and buttons were exposed. `PrettyPrint JSON` and
  `Node Filter Threshold %` were not exposed as reliably named controls.
  Escape cancelled without creating an archive.
- `Ctrl+F4` did not close the hand tab or produce `Save Resource`. A durable
  hand-tab close target remains unproven.

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
- Viewer-only close filenames: `HU-1.5.hrcv` and `HU-1.5.zip`. Both files were
  present and non-empty after `Don't Save` closed the unsaved `*HU-1.5` tab.
  No matching `.hrcz` file was present in the HU folder.
- Shallow-tree follow-up: `HU-2` used the HU candidate from source commit
  `9b24166`. Hand Setup reported two nodes at equal `2 bb` stacks. A later
  preview of the identical candidate showed an SB raise to `2.00 BB` with only
  a BB call of `1.00 BB`. No SB completion branch was present. This confirms
  shallow-completion suppression at equal `2 bb` in HU. The inclusive `5 bb`
  boundary, the first supported stack above it, dynamic post-fold behaviour,
  and all multiway behaviour remain TO CONFIRM.
- `HU-2` outputs: `HU-2.hrcv` was `9,015` bytes and `HU-2.zip` was `3,301`
  bytes. Both persisted after Viewer-only closure. No `HU-2.hrcz` file was
  present.
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
   other workflows, such as `5m`. Save As can retain its previous type or
   default to `*.hrcz Complete Save`. Select `*.hrcv Viewer Save` and confirm
   the `.hrcv` extension before Save.
6. After the queued operations finish successfully, open `Hand` →
   `Export Strategies`. Use `Complete Export`, Depth `16`, clear
   `PrettyPrint JSON`, and set `Node Filter Threshold %` to `0.1`. Save
   `HU-1.zip` with `*.zip Archived Json` in the HU folder.
7. Start the next simulation.

After step 6, verify the new Viewer file and strategy archive. Both files must
exist and must not be empty. Close the completed hand tab before step 7. HRC
shows `Save Resource` with `Save '<simulation-name>'?`. Confirm that both output
base filenames and the prompt name match the completed simulation. Only then
select `Don't Save` and confirm that only `Home` remains. Stop on any mismatch.

The required sequence is Euan's workflow definition. The second Nash dialog
opened while CI 10 Progress was visible. After submission, CI 1 Progress
appeared. The demonstration was too fast to establish queue order, calculation
completion, or failure states.

## Control map

| Lifecycle step | Visible label | Accessible name | Control type | Automation ID | Supported patterns | Keyboard path | Required action | Observable success | Observable failure | Safe to automate |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Start tree setup | `New: Monte Carlo Hand` | `New: Monte Carlo Hand` | Link | `3342566` | TO CONFIRM | `Hand` → `Start New Calculation` shows `Ctrl+W, H`; operation TO CONFIRM | Open a new Monte Carlo hand from `Home`. | Hand Setup opened on Basic Hand Data after one refreshed retry. | The first semantic click returned an unknown outcome and left `Home` visible. | TO CONFIRM: the named Home link worked after refresh, but retry handling and the next-simulation route remain unproven. |
| Configure stacks | `Stacks and Blinds` | Empty for both stack fields | Edit | `2036322` and `1642500` during this session; stability TO CONFIRM | Value, Text, and LegacyIAccessible | Both fields are focusable. Exact Tab order is TO CONFIRM. | Change both starting stacks from `80.0` to `1`. | The fields accepted `1`. The created HU table showed the expected shallow stacks. | TBD | TO CONFIRM: the fields have no accessible names and their numeric IDs have not been shown stable. |
| Advance Hand Setup | `Next` | `&Next` | Button | `268476` | TO CONFIRM | Tab and Enter did not reach the owned dialog through the discovery tool | Advance from Basic Hand Data to Betting Setup after validating all inputs. | Euan selected Next manually. Betting Setup appeared with Total Nodes `16`. | Semantic click failed twice. Keyboard input did not change the page. | NO with the inspected control channel: this critical transition required manual input. |
| Select scripting | `Scripting` | `Scripting` | Tab item | Parent tab ID `334064`; item ID empty | SelectionItem and LegacyIAccessible in the earlier inspection | TO CONFIRM | Select the Scripting tab. | The `Script:` field and script controls appeared. | All visible tab items shared one element index in the 11 August provider. | TO CONFIRM: visual selection worked for discovery, but no durable semantic target is proven. |
| Open script picker | Folder icon beside `Script:` | Empty | Button | `1903002` in the earlier session; `334110` on 11 August | Invoke and LegacyIAccessible in the earlier inspection | No access key was exposed. | Open the script file picker. | A fresh screenshot-located discovery click opened the standard `Open` dialog. | Semantic invocation failed. The numeric ID changed between sessions. | NO: no stable name, identifier, or keyboard path has been observed. |
| Select script file | `tree-building-hu-candidate.js`; `File name:`; `Open` | Same as visible labels | List item; edit; button | File item `1` in this dialog; filename edit `1148`; Open `1`; Cancel `2` | SelectionItem and Value for standard dialog controls; exact set TO CONFIRM | `Alt+N`, type exact filename, Enter | Select the HU candidate and open it. | The filename was visibly present before Enter. HRC returned to Hand Setup, showed the basename in `Script:`, and changed Total Nodes to `2`. | A wrong path, missing file, script error, or unchanged tree estimate must stop the workflow. | TO CONFIRM: the keyboard route worked, but the discovery provider reported the wrong background focused element. Validate with a standalone UIA runner. |
| Verify shallow preview | `Preview`; `Action`; `Amt [BB]`; `Player`; `Street` | Preview tree exposed `R` and child `C` | Tab; tree; tree items | Parent tab `334064`; tree `923428` | TO CONFIRM | TO CONFIRM | Expand the root and inspect every branch before Finish. | At equal `2 bb`, Preview showed `R 2.00 SB PRE` with exactly one child, `C 1.00 BB PRE`. No SB completion branch was present. | Any unexpected branch, amount, player, or street must stop the workflow. | TO CONFIRM: the read-only evidence is direct at equal `2 bb`, but durable expansion and all other stack cases remain unproven. |
| Finish tree setup | `Finish` | `Finish` | Button | `268480` | TO CONFIRM | Enter while `Finish` is the visible default | Finish tree creation after the estimate completes. | Hand Setup closed and unsaved `*Hand 6` opened. | A script error, disabled Finish, or unchanged Hand Setup must stop the workflow. | TO CONFIRM: Enter worked for the two-node test, but explicit failure handling remains unproven. |
| Rename | `Hand`; `Rename Hand`; `Rename to:`; `OK`; `Cancel` | Same as visible labels | Menu item; edit; buttons | Menu command `143`; edit `793498`; OK `8263114`; Cancel `1445454` | TO CONFIRM | `Ctrl+H, R`; Escape cancelled the dialog | Open Rename Hand, replace the current name with `HU-1`, and select OK. | The production demonstration changed `*Hand 2` to `*HU-1`. The 11 August inspection opened the labelled dialog and cancelled without a rename. | A rejected value or unchanged tab must be detected. | TO CONFIRM: controls are named, but setting the value and verifying rejection remain untested through the target runner. |
| Submit CI 10 | `Run Nash Calculation`; `Nash Calculation`; `OK` | Only `OK` and `Cancel` were exposed on 11 August | Dialog; buttons; other control types missing | OK `662418`; Cancel `859034`; configuration IDs missing | TO CONFIRM | `Alt+R` opens the dialog | Select `HRC 4.0 (Default)`, Full Tree, Until CI value is reached, CI Target `10.0`, Reset Regret clear, and Reset Strategies clear. Select OK. | The earlier demonstration showed Progress with `MC-CFR [Target CI < 10.00]`. | A rejected or failed submission must be distinguishable. | NO with the inspected provider: the configuration controls and their values are not accessible. |
| Submit CI 1 | `Run Nash Calculation`; `Reset Strategies`; `OK` | Only `OK` and `Cancel` were exposed on 11 August | Dialog; buttons; other control types missing | OK `662418`; Cancel `859034`; configuration IDs missing | TO CONFIRM | `Alt+R` opens the dialog | Keep the same algorithm, scope, and sampling mode. Set CI Target to `1.0`, select Reset Strategies, keep Reset Regret clear, and select OK. | The earlier demonstration showed Progress with `MC-CFR [Target CI < 1.00]`. The 11 August dialog retained CI `1.0`. | Escape did not close the dialog after keyboard inspection. A screenshot-located Cancel closed it without submission. | NO with the inspected provider: the CI and reset controls cannot be read or set safely. |
| Detect running | `Progress`; `HU-1: Monte Carlo Sampling`; `MC-CFR [Target CI < 10.00]`; `MC-CFR [Target CI < 1.00]` | TO CONFIRM | TO CONFIRM | TO CONFIRM | TO CONFIRM | TO CONFIRM | Read the Progress pane without changing the operation. | The operation name, target CI, activity bar, and stop button were visible. | TBD | TO CONFIRM: the visible running state is observed, but accessible state is not. |
| Detect completion or failure | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TO CONFIRM |
| Viewer save | `File`; `Save As`; `File name:`; `Save as type:`; `*.hrcv Viewer Save`; `Save` | Standard dialog labels were exposed | Dialog; edits; combo box; buttons | Filename `1001`; type host `FileTypeControlHost`; Save `1`; Cancel `2` | Standard dialog patterns; exact set TO CONFIRM | `Ctrl+Alt+S` opens Save As; Escape cancelled | Open Save As, select `*.hrcv Viewer Save`, confirm the simulation filename with an `.hrcv` extension, browse to the applicable table-size folder, and select Save. | `HU-1.5.hrcv` was submitted earlier. On 11 August, Save As opened at the HU folder with `*.hrcv Viewer Save` retained and was cancelled. | A prior run defaulted to `*.hrcz Complete Save`. The type can vary and must be read before Save. | TO CONFIRM: the standard dialog is strong, but type selection and long-run queue behaviour remain untested through the target runner. |
| Verify Viewer output | `<simulation-name>.hrcv` | Not applicable | File | Not applicable | Not applicable | Not applicable | Verify the new file without opening or modifying it. | `HU-1.hrcv`, `HU-1.5.hrcv`, and `HU-2.hrcv` existed at the required HU path with non-zero sizes. | The file is absent, empty, or saved elsewhere. | YES for read-only metadata verification of these exact new files. |
| Submit strategy export | `Hand`; `Export Strategies`; `Complete Export`; `Depth:`; `PrettyPrint JSON`; `Node Filter Threshold %`; `OK` | Scope and some settings were unnamed; Depth and buttons were exposed | Combo box; spinner; edit; tree; buttons; other control types missing | Scope `1055578`; Depth spinner `334010`; Depth edit `334742`; OK `596422`; Cancel `399476` | TO CONFIRM | `Ctrl+H`, then `E` opened the dialog; Escape cancelled | Change the initial Depth value from `2` to `16`. Keep `Complete Export`, clear `PrettyPrint JSON`, keep the threshold at `0.1`, and select OK. Save `<simulation-name>.zip` in the applicable table-size folder with `*.zip Archived Json`. | The shortcut opened the dialog with the expected defaults and expanded two-node range tree. Earlier runs created non-empty archives. | `PrettyPrint JSON` and threshold were not reliably named. No explicit export-success or failure message was captured. | NO with the inspected provider until every required setting and failure state has a durable read/write path. |
| Verify strategy archive | `<simulation-name>.zip` | Not applicable | File | Not applicable | Not applicable | Not applicable | Verify the new archive without opening or modifying it. | `HU-1.zip`, `HU-1.5.zip`, and `HU-2.zip` existed at the required HU path with non-zero sizes. | The file is absent, empty, or saved elsewhere. | YES for read-only metadata verification of these exact new files. |
| Close completed hand tab | `Close`; `Save Resource`; `Save '<simulation-name>'?`; `Don't Save`; `Home` | `Save Resource`; `Save 'HU-2'?`; `Save`; `Don't Save`; `Cancel` in the `HU-2` session | The three dialog buttons exposed class `Button` and UIA control type `Pane` in the `HU-2` session. | Numeric session values only; stability TO CONFIRM | `Don't Save` did not expose InvokePattern in the `HU-2` session. | `Ctrl+F4` had no effect on `*Hand 6` | Close the unsaved hand tab after both outputs are verified. Confirm that both output base filenames and the prompt name match the completed simulation. Only then select `Don't Save`. | `Don't Save` closed `*HU-1.5` and `*HU-2`. Only `Home` remained, and both output files persisted in each Viewer-only run. | The effects of `Save` and `Cancel` are TO CONFIRM. Any filename or prompt mismatch must stop the transition without discarding the hand. | TO CONFIRM: the semantic prompt and button names were accessible once, but a supported durable operation and tab-close target remain unproven. |
| Start next simulation | `Hand`; `Start New Calculation` | TO CONFIRM | TO CONFIRM | TO CONFIRM | TO CONFIRM | `Ctrl+W, H` | Start the next simulation only after both outputs are verified and the completed tab is closed. | The Home link opened Hand Setup during isolated discovery. | TBD | TO CONFIRM: the `Ctrl+W, H` route and the end-to-end post-close transition were not demonstrated. |

## Observable states

| State | Visible evidence | Accessible evidence | Distinguishable | Notes |
| --- | --- | --- | --- | --- |
| Configured | Hand Setup closed. An unsaved `*Hand 1` tab opened with strategy, range, and HU table views. Progress showed no active operation. | The tree exposed `*Hand 1`, `Strategy Table`, `Hand Settings`, and `Run Nash Calculation (Alt+R)`. | CONFIRMED | Tree creation completed. The calculation was not started. |
| HU 2bb shallow preview verified | Expanded Preview showed `R 2.00 SB PRE` with exactly one child, `C 1.00 BB PRE`. | The preview tree exposed root `R` and child `C`. | CONFIRMED for equal `2 bb` | No SB completion branch was present. This does not validate the `5 bb` boundary or multiway behaviour. |
| Renamed | The tab changed from `*Hand 2` to `*HU-1`. | TO CONFIRM | CONFIRMED visually | Progress later used the `HU-1` name. |
| Queued | No persistent queue list was visible in the captured states. | TBD | TO CONFIRM | The CI 1 dialog opened while CI 10 was visible, but the small operation transitioned quickly. |
| CI 10 running | Progress showed `HU-1: Monte Carlo Sampling` and target CI `< 10.00`. | TBD | CONFIRMED visually | A red stop button and activity bar were visible. |
| CI 1 running | Progress showed `HU-1: Monte Carlo Sampling` and target CI `< 1.00`. | TBD | CONFIRMED visually | Reset Strategies was selected in the submitted dialog. |
| CI 10 no longer displayed | The CI 10 line was replaced by the CI 1 line. | TBD | CONFIRMED visually | The reason for the transition is TO CONFIRM. No explicit successful-completion marker was captured. |
| No operation displayed | Progress later showed `No operations to display at this time.` | TBD | CONFIRMED visually | This text alone does not distinguish success from failure. |
| Viewer saved | The Save As dialog accepted `HU-1.5.hrcv` with `*.hrcv Viewer Save`. | File existence was verified separately. | CONFIRMED | Viewer Save returned to the still-unsaved `*HU-1.5` tab. |
| Failed | TBD | TBD | TO CONFIRM | TBD |
| Complete Save | The first Save As used the default `*.hrcz Complete Save` in error. The tab changed to `HU-1.hrcz`. | TBD | CONFIRMED visually | The unintended file remains untouched. |
| Viewer output verified | The new `.hrcv` file exists at the required HU path and has non-zero size. | Read-only file metadata returned the expected path, size, and timestamp. | CONFIRMED | File contents were not opened or modified. |
| Strategy archive created | The export dialog accepted the required settings and `HU-1.zip` path. HRC returned to the source tab. | File existence was verified separately. | CONFIRMED for non-zero file creation only | No explicit export-success message was visible. |
| Strategy archive metadata verified | The new `.zip` file exists at the required HU path and has non-zero size. | Read-only file metadata returned the expected path, size, and timestamp. | CONFIRMED | The archive was not opened. Its structure, JSON content, and strategy completeness remain unverified. |
| Viewer-only close prompt | `Save Resource` asked `Save 'HU-1.5'?` and later `Save 'HU-2'?`. Both prompts showed `Save`, `Don't Save`, and `Cancel`. | The `HU-2` prompt and button names were readable through UI Automation. `Don't Save` did not expose InvokePattern. | CONFIRMED visually; accessible operation TO CONFIRM | Viewer Save and strategy export did not clear the leading asterisk on either unsaved tab. |
| Completed tab closed | `Don't Save` was selected. The source tab disappeared and only `Home` remained. | File metadata was verified after the close. | CONFIRMED | The `.hrcv` and `.zip` files persisted. No matching `.hrcz` file was present. |
| Next simulation started | TBD | TBD | TO CONFIRM | The transition to the next simulation was not demonstrated. |

## Test runs

| Run | Date and time | Planned output | Result | Observed duration | Evidence | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 10 August 2026, 22:40 BST | None | TREE CREATED | TBD | HU `1 bb` tree creation succeeded and opened `*Hand 1`. | No calculation or save was performed in this observation. |
| 2 | 10 August 2026, 23:08–23:10 BST | `HU-1.hrcv`; unintended `HU-1.hrcz` | PARTIAL DEMONSTRATION | Progress changed to no operation displayed within the observation period. Explicit completion and production duration remain TO CONFIRM. | The demonstration renamed `*Hand 2` to `*HU-1`, submitted both Nash configurations, showed both running targets, made an accidental Complete Save, corrected it with Viewer Save, and verified the Viewer file. | This observation began with `*Hand 2` and is separate from run 1. Long-run queue order and explicit calculation success or failure remain unconfirmed. The unintended Complete Save remains untouched. |
| 3 | 10 August 2026, 23:18 BST | `HU-1.zip` | PARTIAL DEMONSTRATION | The export and close transition completed within the observation period. | The demonstration kept Complete Export, changed Depth from `2` to `16`, kept PrettyPrint JSON clear, and kept the threshold at `0.1`. It saved a non-empty archive and then closed the source tab. | The source tab was `HU-1.hrcz` from run 2. The archive contents and close behaviour after a Viewer-only save remain unverified. |
| 4 | 10 August 2026, 23:35–23:36 BST | `HU-1.5.hrcv`; `HU-1.5.zip` | PARTIAL DEMONSTRATION | Viewer Save submission, non-empty output creation, and Viewer-only tab closure were observed. | The demonstration began on `*HU-1.5`, submitted Viewer Save and strategy export, selected `Don't Save` in the close prompt, and returned to `Home`. Both files were non-empty after close, and no matching `.hrcz` file was present. | Euan reported that rename and both Nash runs were already complete before observation. Their completion was not independently observed. File contents were not opened. |
| 5 | 10–11 August 2026, ending 00:13 BST | `HU-2.hrcv`; `HU-2.zip` | PARTIAL DEMONSTRATION | The two-node calculations returned to idle during the supervised observation. No explicit calculation-success marker appeared. | The HU candidate from `9b24166` created an equal-stack `2 bb` tree. Hand Setup reported two nodes. The run renamed the hand, submitted CI `10.0`, submitted CI `1.0` with Reset Strategies, created both non-empty outputs, verified no matching `.hrcz`, selected `Don't Save` on the exact `HU-2` prompt, and returned to `Home`. | Run 5 did not inspect Preview or confirm the cutoff; run 6 later confirmed the equal-`2 bb` case. Euan assisted with strategy export. The archive contents were not opened. Codex-specific window activation changed the HRC bounds and was discontinued. An unverified coordinate selected the root row instead of the tab close control; the later close point was verified by its exact tooltip before use. |
| 6 | 11 August 2026 | None | ACCESSIBILITY DISCOVERY | No Nash operation or file write occurred. | HRC remained in a restored, near-full-size window. The identical HU candidate loaded through the standard Open dialog. Hand Setup reported two nodes. Expanded Preview showed `R 2.00 SB PRE` with only `C 1.00 BB PRE`. Enter finished to `*Hand 6`. Rename, Nash, Save As, and Export Strategies were opened for inspection and cancelled. | Euan manually selected Hand Setup Next after programmatic input failed. Nash settings remained inaccessible. `Ctrl+F4` did not close the hand. The unsaved `*Hand 6` remains open because it has no verified outputs. |

## Blockers

- Hand Setup Next exposed a name and ID, but semantic and keyboard input did
  not reach the owned dialog. The 11 August run required manual input.
- The script-picker button has no accessible name or access key. Its numeric ID
  changed between sessions. Semantic invocation failed.
- Nash Calculation exposed only OK and Cancel. The required algorithm, scope,
  sampling, CI, Reset Regret, and Reset Strategies controls were absent from
  the accessibility tree. The first run cannot rely on the retained CI value.
- Export Strategies did not expose reliably named PrettyPrint JSON and
  threshold controls. Safe targeting for every required setting is unproven.
- `Ctrl+F4` did not close the hand. A durable tab-close target and supported
  operation for `Don't Save` remain unproven.
- Long-run queue order and explicit completion or failure states have not been
  observed.
- The transition to the next simulation has not been demonstrated.

## Verdict

- Feasibility: TO CONFIRM
- Confidence: TO CONFIRM
- Basis: The selected HU tree was configured and created without a visible
  error. Rename, both Nash submissions, running targets, Viewer Save, and
  read-only output verification were observed. Strategy-export submission,
  non-zero archive creation, read-only archive verification, and source-tab
  closure were also observed. The Viewer-only close prompt, `Don't Save`
  result, and persistence of both output files were observed. A separate
  `2 bb` HU preview directly showed the SB raise to `2.00 BB` with only the BB call;
  no SB completion branch was present. This confirms the HU rule at `2 bb`.
  The `5 bb` boundary, the first supported stack above it, dynamic post-fold
  behaviour, and all multiway behaviour remain unconfirmed. Long-run queue
  behaviour, explicit completion or failure detection, and several critical
  accessible targets also remain unconfirmed.

## Next action

Find durable, non-coordinate paths for Hand Setup Next, the unnamed script
picker, every Nash setting, Nash Cancel, the poorly named Export Strategies
settings, the hand-tab close target, and `Don't Save`. Verify the Save As
destination, Viewer type, filename, and extension on every save. Then map
accessible Progress states that distinguish queue order, successful completion,
and failure. Do not start an automated or long-running test until these controls
are mapped and Euan authorises that specific test.
