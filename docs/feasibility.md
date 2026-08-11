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
| HRC Beta availability | HRC Beta was installed and running when inspected on 10 August 2026. Its main window title was `HRC Pro [Beta]`. | Inspected the running `hrc.exe` process and its executable path and window title. | CONFIRMED for that inspection. Current process state is TO CONFIRM. |
| HRC Beta version | The executable does not expose a file version or product version. The version in the HRC interface has not been inspected. | Inspected the `hrc.exe` version metadata. | TO CONFIRM |
| Accessibility Insights availability | No executable was present in the two standard `Program Files` locations checked. Other installation methods were not checked. | Checked the standard 64-bit and 32-bit installation paths. | TO CONFIRM |
| Microsoft Inspect availability | The x64 `inspect.exe` is available in Windows Kits `10.0.26100.0`. Its file version is `7.2.0.0`. | Inspected the installed Windows Kits executable and its version metadata. | CONFIRMED |
| Read-only HRC window capture | Codex can capture the current HRC window directly by using its live window handle. Euan does not need to send each screenshot manually. | A direct capture showed the open CI `10.0` Nash Calculation dialog and the HRC Progress pane. | CONFIRMED for discovery only; this does not identify or operate controls. |
| Codex activation side effect | A Codex discovery activation call restored HRC from its maximised state and changed its window bounds. A later native focus-only call preserved the current bounds. | Compared the HRC window rectangle before and after each focus method during the `HU-2` run. | CONFIRMED for the discovery tool only; do not make that activation call part of automation. |
| HRC window state on 11 August | HRC was restored and sized close to the full work area. It was not maximised. No resize, maximise, or activation action was issued during the control-map run. | Euan supplied a full-screen screenshot that showed the restored title-bar state. The control log contained no window-state action. | CONFIRMED for that run. Do not infer current or maximised state from capture dimensions. |
| Coordinate-target failure | An unverified coordinate intended for the hand-tab close control selected the tree root row instead. No setting or output changed. The later close used a point that first showed the exact `Close` tooltip. | Compared the expected target with the captured cursor position and the resulting HRC state during the `HU-2` run. | CONFIRMED for discovery; raw coordinates are not a safe automation path. |

## Corrected context

| Previous statement | Observed evidence | Resolution |
| --- | --- | --- |
| The licensed host used an AMD Ryzen 9 5950X processor. | `EM-3960X` reports an AMD Ryzen Threadripper 3960X 24-Core Processor. | Use the observed host and processor. The earlier 5950X reference is incorrect for this host. |
| Post-Finish Hand Settings contained `Tree Statistics and Abstractions`. | Hand Settings showed `Hand Data`, `Equity Model`, `Treeconfig`, and `Engine`. No `Tree Statistics and Abstractions` page was visible. | Do not require tree statistics for this workflow. |

`Tree Statistics and Abstractions` is visible inside the pre-Finish Betting
Setup page. This does not contradict the post-Finish Hand Settings finding.

## Installed NatTable inspection on 11 August 2026

This is static evidence from the installed HRC components. It is separate from
the live UI evidence below and does not by itself prove that HRC receives a key
sequence or changes the intended cell.

- The installed calculator component
  `net.holdemresources.calculator_4.1.1.202607211244.jar` builds the Stacks and
  Blinds player grid with Eclipse Nebula NatTable `2.5.0` from installed
  dependency `org.eclipse.nebula.widgets.nattable.core_2.5.0.202411280718.jar`
  on an SWT Canvas.
- Its top-left cell is a combo with `Auto`, `HU`, and `3-max` through `10-max`.
  The NatTable has a SelectionLayer plus the default selection and edit
  bindings.
- The installed SelectionLayer starts without a selected cell. The HRC focus
  listener changes the cell painter but does not initialise a selection. This
  is consistent with the earlier observation that Home, arrows, and F2 had no
  visible effect immediately after the grid first received focus.
- The installed bindings map `Ctrl+A` to Select All, `Ctrl+Home` to movement to
  the origin, Space and F2 to cell editing, arrows and Tab to cell movement,
  Enter to vertical movement, and `Ctrl+C` to raw selected-cell copy.
- The static structure supports a focus, `Ctrl+A`, `Ctrl+Home`, Space or F2
  bootstrap hypothesis. A live check was still required because the installed
  bytecode cannot establish the dialog's Tab route, current focus, displayed
  values, or HRC's resulting state.

### Installed Nash and export dialog inspection

These findings are also static evidence from calculator plug-in `4.1.1`. Every
proposed control route remains TO CONFIRM LIVE.

- Nash Calculation uses the exact native shell title `Nash Calculation` and
  explicitly gives `OK` initial focus. Enter immediately after opening the
  dialog is therefore unsafe because it can submit a calculation.
- Its two-column NatTable contains `CFR Algorithm`, `Scope`, `Run Sampling`,
  `Samples (mio.)`, `CI Target`, `Reset Regret`, and `Reset Strategies`. The
  corresponding configured values use combo, integer, double, and checkbox
  editors. Reset Regret and Reset Strategies are mutually exclusive, and a
  submitted reset choice is one-shot.
- Nash retains most accepted settings across openings. A runner must read and
  explicitly verify every required value rather than trust initial defaults or
  values left by the previous calculation.
- The Nash NatTable has the same default selection, edit, and raw-copy
  bindings. Static inspection therefore supports a future focus, `Ctrl+A`,
  `Ctrl+Home`, row movement, and `Ctrl+C` read-back probe. Escape can cancel an
  active cell editor, but live evidence shows that it does not dismiss Nash
  Calculation. A separate verified Cancel route is required. The probe must
  never press Enter. The exact native focus target, copied values, and Cancel
  route are not yet live evidence.
- Export Strategies has an exact in-dialog title and instruction, but static
  inspection did not establish a matching native shell caption. It must be
  identified by ownership plus the exact descendant title, message, scope
  choices, and controls.
- Its scope choices are `Manual Selection`, `Complete Export`, `All Strategies,
  Limited depth`, and `Selected Spot, Limited Depth`. Scope, Depth,
  PrettyPrint JSON, and Node Filter Threshold are retained settings and may
  change even when the dialog is cancelled. Every value must be read and
  verified on every export.
- In the inspected `4.1.1` bytecode, `Complete Export` selects unlimited depth.
  The visible Depth spinner is only semantically applied to the two Limited
  Depth modes. The demonstrated value `16` can still be read and preserved, but
  it does not limit a Complete Export in this version.
- Complete Export selects `*.zip Archived Json`. If the target exists, HRC can
  show `Confirm save as` and ask whether to replace it. The workflow must
  preflight exact absence, select Cancel on any overwrite prompt, and stop.
  It must never confirm replacement.
- Static export status includes the job name `Exporting ranges to <filename>`
  and OK, cancellation, and error results. Their accessible live presentation
  remains TO CONFIRM.

## Data-preserving NatTable bootstrap on 11 August 2026

This live check did not select a table size, change a row or stack, advance the
wizard, finish a tree, submit a calculation, or write a file.

- HRC began on `Home`. The named `New: Monte Carlo Hand` link opened Basic Hand
  Data. The setup displayed the previously used five-player rows and the same
  `10/20/30/40/50 bb` values, showing that opening a new setup can retain prior
  inputs.
- Starting from the newly opened page with Next shown as the default button,
  successive Tab presses visibly reached Cancel, the information text,
  clipboard, eraser, yellow right arrow, and yellow left arrow. The seventh
  press had no visible outline.
- At that seventh stop, `Ctrl+A` placed the visible black selection border on
  the cell displaying `Auto`. `Ctrl+Home` left that cell selected. This directly
  established the non-coordinate Tab and selection bootstrap for this setup.
- Space opened the player-count list. The accessibility tree exposed list ID
  `11606852` and named selectable items `Auto`, `HU`, and `3-max` through
  `10-max`. The provider still incorrectly reported background Range edit
  `69008` as focused.
- Escape closed the list. All five rows, five chip values, and five BB values
  remained unchanged. No item was activated in this run, so table-size
  selection effects were still TO CONFIRM at the end of run 12. Run 13 later
  confirmed the HU row-removal and retained-stack effect described below.
- Two semantic attempts to activate the named Cancel button did not reach the
  cached target. `Alt+C` and Escape did not dismiss Basic Hand Data. `Alt+F4`
  closed the unsaved Hand Setup and returned to `Home` without a prompt.

At the end of run 12, this confirmed one live, non-coordinate route to focus
the NatTable and open the player-count list. It had not yet confirmed the route
through a standalone runner, selection of a different table size, row creation
or removal, retained-value handling after a selection, or an end-to-end route
from this bootstrap to stack entry and read-back. Run 13 later confirmed the HU
selection and its immediate row-removal and retained-stack effects.

## HU table-size selection effect on 11 August 2026

This follow-up changed only the disposable, unsaved Hand Setup. It did not
advance the wizard, finish a tree, submit a calculation, or write a file.

- HRC again opened Basic Hand Data with `Auto` and the retained five-player
  rows: `HJ 1000 / 10.0 bb`, `CO 2000 / 20.0 bb`, `BU 3000 / 30.0 bb`,
  `SB 4000 / 40.0 bb`, and `BB 5000 / 50.0 bb`.
- From that newly opened page, seven Tab presses followed by `Ctrl+A`,
  `Ctrl+Home`, and Space again selected the player-count cell and opened the
  list without a pointer. The list ID was `6359478`, different from the prior
  observed IDs.
- With `Auto` current, one Down press visibly selected `HU`. The editor showed
  `HU` while the list remained open, but all five rows were still present.
- Enter committed the selection and closed the list. HRC removed `HJ`, `CO`,
  and `BU`; the remaining rows were exactly `SB 4000 / 40.0 bb` and
  `BB 5000 / 50.0 bb`.
- `Alt+F4` closed the unsaved Hand Setup and returned to `Home` without a
  prompt.

This confirms one supervised, non-coordinate table-size selection and its
immediate row-removal effect. In this transition HRC retained the prior blind
stacks rather than resetting them. Automation must therefore overwrite and
read back every active seat after selecting a table size. At the end of this
run, multiway row creation, different-valid-value entry, cell read-back,
rejected-input handling, and delivery through a standalone runner remained
TO CONFIRM. Run 14 later confirmed the supervised HU edit and visual-validation
behaviour described below.

## HU stack entry and rejected-input handling on 11 August 2026

This follow-up changed only a disposable, unsaved Hand Setup. It did not
advance the wizard, finish a tree, submit a calculation, or write a file.

- Opening another new setup showed `Auto`, but HRC retained only the two rows
  left by the earlier HU selection: `SB 4000 / 40.0 bb` and
  `BB 5000 / 50.0 bb`. The selector label and retained row state therefore
  cannot be assumed to describe the same reset state.
- Seven Tab presses, `Ctrl+A`, `Ctrl+Home`, Space, one Down press, and Enter
  again selected and committed `HU` without a pointer.
- From the selected HU cell, Down selected the SB row label and Right selected
  SB Chips. `F2` opened an unnamed transient editor with `4000` selected.
- Typing the fabricated test value `4100` and pressing Enter committed it,
  visibly recalculated the row as `41.0 bb`, and opened BB Chips with `5000`
  selected. Typing `5100` and pressing Enter committed `51.0 bb` and opened
  the blank next-row Chips editor.
- Escape cancelled the blank editor. No third row was added; the visible rows
  remained exactly `SB 4100 / 41.0 bb` and `BB 5100 / 51.0 bb`.
- Returning to SB Chips and entering the deliberately invalid text `abc`
  displayed it in red. Enter did not commit or advance, no modal appeared, and
  the derived BB value stayed `41.0`. Escape cancelled the editor and restored
  the visible `4100 / 41.0 bb` value.
- Transient edit IDs changed during the sequence, including `1185980`,
  `1251516`, and `1382588`. The provider continued to report background Range
  edit `69008` as focused even while the stack editor was visibly active.
- `Alt+F4` closed Hand Setup and returned to `Home` without a prompt.

This confirms the combined supervised, non-coordinate route from a newly
opened page through HU selection and two different-valid-value stack commits.
It also confirms visual derived-value read-back, the final-row advance into a
blank editor, safe cancellation without adding a row, and one non-numeric
rejection-and-recovery path. It does not provide machine-readable stack-cell
read-back or prove the foreground and focus assertions needed by a standalone
runner. Multiway choice effects and standalone delivery remain TO CONFIRM.

## Idle control-map discovery on 11 August 2026

This discovery created one disposable, unsaved two-node hand. It did not submit
a Nash calculation or write an output file. The resulting `*Hand 6` tab was
still open at the end of that run because its required Viewer and strategy
outputs did not exist. Its later disposition is TO CONFIRM.

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

In a same-day follow-up, Euan pressed `Alt+N` while Basic Hand Data was open.
Hand Setup advanced to Betting Setup. A read-only capture immediately afterward
confirmed Betting Setup, with `Back` enabled, `Next` disabled, and `Finish`
enabled. Codex issued no input during this confirmation. This establishes the
keyboard route and supersedes the earlier missing-route blocker; the earlier
run still required Euan's manual pointer click because `Alt+N` had not yet been
tested. Delivery and post-state detection through the standalone runner remain
TO CONFIRM.

## Stack-grid keyboard discovery on 11 August 2026

This discovery reused the existing unsaved five-player setup. It did not add or
remove a player row, change a stack, finish a tree, submit a calculation, or
write a file. HRC remained in its restored, non-maximised window. The discovery
ended on Basic Hand Data with the same five visible stack values.

- `Alt+B` returned from Betting Setup to Basic Hand Data. `Alt+N` advanced back
  to Betting Setup. The provider continued to report background Range edit
  `69008` as focused instead of a Hand Setup control.
- A semantic click on the exposed `Back` button did not change the page. A
  secondary accessibility action also failed. These failures reinforce that
  the owned Hand Setup dialog cannot yet be driven reliably through the current
  semantic target.
- On Basic Hand Data, the accessibility tree exposed unnamed panes and toolbar
  buttons but no stack-grid cells. After returning from Betting Setup, the
  visible Tab order began with Next, Cancel, the information text, then the four
  unnamed toolbar buttons in icon order: clipboard, eraser, yellow right arrow,
  and yellow left arrow. Later Tab stops, reverse-Tab, `F6`, `Ctrl+Home`, and
  `F2` did not establish a visible, repeatable route into the grid.
- For discovery only, a screenshot-located click opened the `Auto` player-count
  selector. While open, the accessibility tree exposed a list with session-only
  ID `95687566` and named selectable items `Auto`, `HU`, `3-max`, `4-max`,
  `5-max`, `6-max`, `7-max`, `8-max`, `9-max`, and `10-max`. No choice was
  activated. Escape closed the list. The post-close display remained `Auto`,
  and the existing five rows and all five stack values were visibly preserved.
  At the end of that earlier run, a durable non-coordinate route to focus the
  selector remained TO CONFIRM, as did the effect of selecting a table size.
  The later data-preserving NatTable run established one focus-and-open route
  but did not activate a choice.
- After Escape closed that list, Space reopened it without a pointer action.
  `Alt+Down` and `F4` did not reopen it. The reopened list had ID `18224586`,
  confirming that the numeric list ID changed within the same session. Escape
  closed it again without activating a choice.
- `Alt+N` advanced to Betting Setup and `Alt+B` returned to Basic Hand Data,
  preserving the rows and stacks. Space then did not open the selector. This
  page cycle therefore did not establish a repeatable selector-focus route.
- For discovery only, a screenshot-located click focused the existing HJ Chips
  cell. Once a grid cell had focus, Up and Down moved between player rows, and
  Left and Right moved between columns.
- `Ctrl+Home` selected the cell displaying `Auto`. Down selected the first player row,
  and Right selected its first Chips cell. `Ctrl+End` selected the bottom-right
  grid cell.
- `F2` entered edit mode and selected the current value. A single pointer click
  on a populated value did the same. Escape cancelled editing without changing
  the value.
- The no-change test typed `1000` into the already-`1000` HJ Chips cell and
  pressed Enter. HRC accepted the same value, moved to the CO Chips cell, and
  selected its visible `2000` value for editing. Escape left CO unchanged. The
  visible values remained HJ `1000`, CO `2000`, BU `3000`, SB `4000`, and BB
  `5000` chips.

At the end of this earlier check, the evidence confirmed supervised keyboard
movement, edit mode, same-value commit, advance, and cell-editor cancellation
only after an existing grid cell had focus. It did not then prove a durable
non-coordinate entry target, blank-row handling, different-valid-value entry,
value read-back, rejected-input validation, or safe operation through a
standalone runner. Run 14 later confirmed the combined supervised HU path,
visual read-back, blank-row cancellation, and one invalid-input recovery.

## Five-player setup discovery on 11 August 2026

This discovery stopped at a Script Error. It did not finish a tree, submit a
Nash calculation, or write an output file.

- Basic Hand Data showed `HJ 10.0 bb / 1000 chips`, `CO 20.0 bb / 2000
  chips`, `BU 30.0 bb / 3000 chips`, `SB 40.0 bb / 4000 chips`, and `BB 50.0
  bb / 5000 chips` in that order.
- The small blind was `50`, the big blind was `100`, and Antes was `0`.
  Straddle was `Off`, SkipSB was clear, and Moving BU was selected.
- Euan added each extra player by selecting an empty cell in the BB column.
  HRC populated the player row and position. The yellow arrow buttons were not
  used for this operation.
- Euan edited each BB cell separately. After manual cell activation, Tab moved
  one cell right and Enter moved one row down.
- Selecting the HJ `10.0` cell exposed a transient unnamed edit with session ID
  `6690946`. The discovery provider incorrectly reported background edit
  `69008` as focused.
- `Alt+N` advanced the five-player setup to Betting Setup.
- Hand Mode displayed `Monte Carlo [Advanced, max. 4 players]`. Euan explained
  that this limit concerns some postflop calculations. The direct observation
  proves only that HRC accepted five preflop rows and advanced.
- Before the project script loaded, Betting Setup showed Total Nodes `448527`,
  Total Tree Size `3.1GB`, and HRC available `165.8GB / 166.3GB`. These values
  belong to the default setup, not the project candidate.
- Scripting exposed the `Script:` edit with session ID `858296`. The unnamed
  picker used session ID `464974`, a third observed value for that control.
- The standard `Open` dialog opened at
  `C:\Projects\hrc-beta-automation\scripts\hrc`. It exposed
  `tree-building-3m-6m-candidate.js` as item ID `0`, `File name:` as `1148`,
  and `Open` as `1`. Euan used `Alt+N`, entered the exact filename, and pressed
  Enter.
- The loaded file and the then-current pre-correction worktree candidate had
  the same SHA-256 hash,
  `128110cc73abd5bfd45167d426935e8d43923ae8648deffbc0251f4d03178782`.
  HRC showed `Error: Effective stack does not match a configured workbook
  column: 100000`. The Script Error OK button had session ID `859030`.
- After the error was dismissed, Scripting showed `[Errors]`, Total Nodes `0`,
  Total Tree Size `0.00GB`, and disabled Finish.

Offline analysis found that `100000` equals the supported `10 bb` stack for
this `50/100` setup in HRC amount units. The candidates used
`sizingBigBlinds()` as a raw unit conversion even though the API defines it as
a decision-point action-sizing helper. The project candidates now use the
nominal big blind for state, history, and threshold comparisons. Regression
tests cover the observed five-player stack vector and a deliberately divergent
action-sizing helper.
At the time of the failing run, the candidate under
`C:\Projects\hrc-beta-automation` had SHA-256
`128110cc73abd5bfd45167d426935e8d43923ae8648deffbc0251f4d03178782`.
The corrected worktree candidate has SHA-256
`fa2612bd1d3b01a8aa6419fc3697450cf708adff73fc6d085e2223ff605d7c63`.
Euan reported loading the corrected worktree candidate for the retest. A
contemporaneous read-only capture showed `tree-building-3m-6m-candidate.js`
without `[Errors]`, Total Nodes `1815589`, Total Tree Size `12.3GB`, and enabled
Finish. HRC available was `165.7GB / 166.3GB`. For the reported corrected load,
this confirms that the candidate passed script evaluation and tree estimation
for the observed setup.
Finish was not selected, and no calculation or file write occurred.

The following path-scoped Preview evidence was then observed:

- The root showed `HJ R 2.00/R 10.0`, `CO R 2.00/R 20.0`,
  `BU R 2.10/R 30.0`, and `SB C 0.50/R 3.00/R 40.0`. Every row was `PRE`.
- After `HJ R 2.00`, CO showed `C 2.00/3B 5.00/3B 20.0`; BU showed
  `C 2.00/3B 5.50/3B 30.0`; SB showed `C 1.50/3B 7.50/3B 40.0`; and BB showed
  `C 1.00/3B 10.0` after the intervening folds.
- After `HJ R 2.00, CO C 2.00`, the displayed one-caller squeeze sizes were
  `BU 3B 6.00`, `SB 3B 8.00`, and `BB 3B 6.00`. After BU also called, SB showed
  `3B 8.50/3B 40.0`, BB showed `3B 8.00/3B 30.0`, and neither blind showed a
  third call.
- The other ordinary roots showed the expected calls and re-raises:
  `CO R 2.00` produced BU `3B 5.50`, SB `3B 7.50`, and BB `3B 5.50`;
  `BU R 2.10` produced SB `3B 7.50` and BB `3B 7.00`; and `SB R 3.00`
  produced BB `C 2.00/3B 8.00/3B 40.0`. Their displayed all-in alternatives
  matched the current effective stacks.
- After `SB C 0.50`, BB showed `X 0.00/R 3.00/R 40.0`. This is an
  above-cutoff completion example at the configured `40 bb` SB stack.
- After the non-ordinary `HJ R 10.0`, later seats showed calls and only their
  legal all-in re-raise. BB showed only `C 9.00` when the all-in opener was the
  sole remaining opponent.
- The ordinary path `HJ R 2.00, CO 3B 5.00` showed SB
  `4B 11.3/4B 40.0`. After SB `4B 11.3`, BB showed only `5B 40.0`, HJ could
  call `8.00`, and CO could call `6.25` or `5B 20.0`. After HJ called, CO's
  call option disappeared and only `5B 20.0` remained.
- The observed low-SPR flop rows were `X 0.00/B 1.00/B 1.38/B 2.20/B 3.69/
  B 5.50/B 8.00` heads-up and `X 0.00/B 1.00/B 1.88/B 3.00/B 5.03/B 8.00`
  three-way.

A read-only comparison found that every listed path matched the current
candidate's workbook-derived manifest and legal-normalisation rules. This is
not exhaustive validation of the `1815589`-node tree. It does not validate
unexpanded branches, the `5 bb` completion boundary, the `>=40 bb` squeeze
boundary, other stacks or table sizes, later streets, Finish, Nash, or output.
The full loaded path and hash remained unexposed; provenance is Euan-reported.
Where HRC suppressed an illegal sizing, Preview confirms only the visible legal
tree, not the candidate's raw callback return.

The current accessibility capture exposed Preview tab ID `661798`, tree ID
`989272`, named column headers, and selectable action-only tree items. It did
not expose each item's amount, player, or street in the item name. The provider
again reported background edit `69008` as focused. Screenshot-located expansion
was sufficient for supervised discovery but is not a durable automation path.
The HRC-tested pre-conversion HU candidate had SHA-256
`8fc4d2d79aefee249db4ea3cbecb2516f19b7a2bfbfcf85f3f12a6e23e54db6a`.
The current HU candidate has SHA-256
`e127ed9285d4f77253ad3c9ad3ac45afdb105f7d930ed3c45208d604fce845ec`.
It needs a small HRC Preview recheck without a Nash calculation.

## Selected workflow

- Workflow: Create and rename one true heads-up Monte Carlo tree, queue two
  Nash calculations, queue a Viewer save, export the strategies, close the
  completed tab, and continue to the next simulation.
- Selection reason: This is the smallest equal-stack setup in the generated
  simulation run order.
- Selected inputs: Two players with `1 bb` starting stacks from the generated
  simulation run order.
- Viewer output filename: `HU-1.hrcv`. The demonstration also created an
  unintended `HU-1.hrcz` Complete Save. It was present and unmodified when
  last checked; its later state is TO CONFIRM.
- Strategy export filename: `HU-1.zip`. The demonstration created this file in
  the same HU folder as the Viewer output. The saved `HU-1.hrcz` tab was then
  closed.
- Viewer-only close filenames: `HU-1.5.hrcv` and `HU-1.5.zip`. Both files were
  present and non-empty after `Don't Save` closed the unsaved `*HU-1.5` tab.
  No matching `.hrcz` file was present in the HU folder.
- Shallow-tree follow-up: `HU-2` used the pre-conversion HU candidate from
  source commit `9b24166`. Hand Setup reported two nodes at equal `2 bb`
  stacks. A later preview of the same revision showed an SB raise to `2.00 BB`
  with only a BB call of `1.00 BB`. No SB completion branch was present. This
  confirms shallow-completion suppression for that revision at equal `2 bb`.
  The current HU candidate needs a Preview recheck. Its inclusive `5 bb`
  boundary and the first supported stack above it remain TO CONFIRM. Multiway
  evidence is limited to the representative five-player paths recorded above;
  other stacks, table sizes, boundaries, dynamic post-fold cases, later
  streets, and unexpanded branches remain TO CONFIRM.
- `HU-2` outputs: `HU-2.hrcv` was `9,015` bytes and `HU-2.zip` was `3,301`
  bytes. Both persisted after Viewer-only closure. No `HU-2.hrcz` file was
  present.
- Expected cost and duration: The small demonstration calculations transitioned
  quickly. Euan reports that production calculations can take a long time.
  Exact production durations remain TO CONFIRM.

## Required workflow sequence

After tree creation, submit steps 2 through 5 without waiting for the previous
operation to finish:

1. Create the tree for the next setup in the simulation run order. Select the
   required table size, overwrite every active seat's stack, and read back the
   exact position order and values. Do not rely on prior inputs being reset.
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
   `HU-1.zip` with `*.zip Archived Json` in the HU folder. In inspected HRC
   `4.1.1`, Complete Export is unlimited-depth and does not consume the visible
   Depth setting; set and read back `16` to preserve the required workflow.
7. Start the next simulation.

Before step 1, verify that neither exact target `HU-1.hrcv` nor `HU-1.zip`
exists. Use the corresponding unique base name for each later simulation. Stop
and choose a new unique name if either target exists. Recheck the exact target
immediately before each Save. If HRC shows an overwrite prompt, select Cancel
and stop; never replace an existing output.

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
| Start tree setup | `New: Monte Carlo Hand` | `New: Monte Carlo Hand` | Link | `3342566` | TO CONFIRM | `Hand` → `Start New Calculation` shows `Ctrl+W, H`; operation TO CONFIRM | Open a new Monte Carlo hand from `Home`. | Hand Setup opened on Basic Hand Data after one refreshed retry in the earlier run and on the first named-link activation in later runs. | The first earlier semantic click returned an unknown outcome and left `Home` visible. New setups retained prior values; the latest showed `Auto` with only the prior SB and BB rows. | TO CONFIRM: the named Home link works, but retry handling, state reset, and the next-simulation route remain unproven. |
| Configure HU stacks | `Stacks and Blinds` | Empty for both stack fields | Edit | `2036322` and `1642500` during this session; stability TO CONFIRM | Value, Text, and LegacyIAccessible | Both fields are focusable. Exact Tab order is TO CONFIRM. | Change both starting stacks from `80.0` to `1`. | The fields accepted `1`. The created HU table showed the expected shallow stacks. | TBD | TO CONFIRM: the fields have no accessible names and their numeric IDs have not been shown stable. |
| Select table size or add multiway player rows | `Auto`; `HU`; `3-max` through `10-max`; empty BB-column cells | The open selector exposed every named table-size item; empty cells remained unnamed | Selector list and list items while open; table cell and transient edit for the earlier manual method | Open-list IDs `95687566`, `18224586`, `11606852`, and `6359478`; closed selector and empty-cell IDs TBD | The open items were selectable; Space opened the list after the non-coordinate NatTable bootstrap | From the newly opened page: press `Tab` seven times, `Ctrl+A`, `Ctrl+Home`, Space. For the observed HU case only, press `Down` once from `Auto`, then `Enter`; other choices and arrow counts remain TO CONFIRM. Escape cancels without selection. | Select the required table size. Then overwrite every active seat and verify the exact row count, position order, and values before advancing. | In two follow-ups, one `Down` press selected `HU` and Enter committed it. The first reduced five retained rows to SB and BB; the next setup reopened as `Auto` while retaining those two rows. Earlier manual empty-cell selection populated `HJ`, `CO`, `BU`, `SB`, `BB`. | A missing, extra, retained, reset, or misordered row or value must stop the workflow. | NO: HU selection and its row-removal effect are observed, but multiway choice effects, machine-readable row/value verification, and standalone delivery remain untested. Numeric list IDs changed. |
| Configure active stacks | `BB`; `Chips`; `Auto`; `HU` | Active edits were unnamed | Table cell; transient edit | `6690946` in the earlier run; `1185980`, `1251516`, and `1382588` during the HU run; stability disproven | TO CONFIRM | After the HU commit, Down selected SB, Right selected Chips, and `F2` opened the editor. Enter committed and moved down. After the last active row, Enter opened the blank next-row editor; Escape cancelled it. | Overwrite each active stack. After the last commit, cancel the blank-row editor. Verify the exact row count, positions, chip values, and derived BB values before advancing. | `4100` and `5100` committed as `41.0 bb` and `51.0 bb`. Enter advanced through both rows; Escape cancelled the blank third row without adding it. | Invalid `abc` stayed red; Enter did not commit or advance and the derived value stayed `41.0`. Escape restored `4100`. Any other mismatch must stop. The provider still reported background Range edit `69008` as focused. | NO: the combined supervised HU keyboard path, visual read-back, and one invalid-input recovery are observed, but machine-readable cell verification, multiway operation, foreground/focus assertions, and standalone delivery remain unproven. |
| Advance Hand Setup | `Next` | `&Next` | Button | `268476` in the earlier session; stability TO CONFIRM | TO CONFIRM | `Alt+N` | After validating all inputs and confirming Basic Hand Data is open, press `Alt+N`. | Euan confirmed that `Alt+N` advanced Hand Setup to Betting Setup. A read-only capture confirmed the resulting page. | Earlier semantic clicks, Tab, and Enter did not change the page. Any unchanged or unexpected page must stop the workflow. | TO CONFIRM through the target runner: the supervised keyboard route works, but reliable dialog focus, key delivery, and post-state detection are unproven. |
| Cancel Hand Setup | `Cancel` | `Cancel` | Button | `727278` in the NatTable run; stability TO CONFIRM | TO CONFIRM | `Alt+F4` while the owned Hand Setup is active | Abort a disposable or invalid setup without creating a hand. | `Alt+F4` closed the unsaved setup and returned to `Home` without a prompt. | Two cached named-target attempts could not be activated; `Alt+C` and Escape did not dismiss the dialog. Any unexpected prompt or window must stop. | TO CONFIRM through the target runner: one keyboard close worked, but exact owned-dialog and foreground assertions are required. |
| Select scripting | `Scripting` | `Scripting` | Tab item | Parent tab ID `334064`; item ID empty | SelectionItem and LegacyIAccessible in the earlier inspection | TO CONFIRM | Select the Scripting tab. | The `Script:` field and script controls appeared. | All visible tab items shared one element index in the 11 August provider. | TO CONFIRM: visual selection worked for discovery, but no durable semantic target is proven. |
| Open script picker | Folder icon beside `Script:` | Empty | Button | `1903002`, `334110`, and `464974` in three sessions; `Script:` edit `334118` and `858296` in two sessions | Invoke and LegacyIAccessible in the earlier inspection | No access key was exposed. | Open the script file picker. | A screenshot-located discovery click opened the standard `Open` dialog. | Semantic invocation failed. The numeric ID changed in every inspected session. | NO: no stable name, identifier, or keyboard path has been observed. |
| Select script file | Both candidate filenames; `File name:`; `Open` | Same as visible labels | List item; edit; button | Multiway item `0`; HU item `1`; filename edit `1148`; Open `1`; Cancel `2` in the observed dialogs | SelectionItem and Value for standard dialog controls; exact set TO CONFIRM | `Alt+N`, type exact filename, Enter | Select the applicable candidate and open it. | The pre-conversion HU file loaded and changed Total Nodes to `2`. After the reported corrected multiway load, HRC showed no `[Errors]` and produced a non-zero estimate. | A wrong path, missing file, Script Error, or unchanged estimate must stop the workflow. | TO CONFIRM: the keyboard route worked, but durable focus detection and the unnamed picker remain unresolved. |
| Detect tree-script error | `Script Error`; error text; `OK`; `[Errors]` | The exact error and OK were exposed | Dialog; text; button | Error text `924558`; OK `859030` in this session | TO CONFIRM | TO CONFIRM | Record the exact error and stop before Finish. | Not applicable | The five-player candidate reported `Error: Effective stack does not match a configured workbook column: 100000`; Finish was disabled and Total Nodes was `0`. | TO CONFIRM: the visible failure is distinguishable, but durable automated detection is unproven. |
| Verify tree preview | `Preview`; `Action`; `Amt [BB]`; `Player`; `Street` | The current tree exposed selectable action-only items. Amount, player, and street were visible but absent from item names. | Tab; tree; tree items | HU tree `923428` earlier; current Preview tab `661798`; current tree `989272`; stability TO CONFIRM | Tree items were selectable; a durable expand operation remains TO CONFIRM. | TO CONFIRM | Expand and inspect the documented candidate paths before Finish. | The equal-`2 bb` HU path and the listed five-player multiway paths were directly observed. The multiway values matched the current candidate manifest for this setup. | Any unexpected branch, amount, player, or street must stop the workflow. | NO for automation: supervised screenshot expansion worked, but provider focus was wrong and the accessible item names omitted three required columns. Evidence remains path-scoped. |
| Finish tree setup | `Finish` | `Finish` | Button | `268480` | TO CONFIRM | Enter while `Finish` is the visible default | Finish tree creation after the estimate completes. | Hand Setup closed and unsaved `*Hand 6` opened. | A script error, disabled Finish, or unchanged Hand Setup must stop the workflow. | TO CONFIRM: Enter worked for the two-node test, but explicit failure handling remains unproven. |
| Rename | `Hand`; `Rename Hand`; `Rename to:`; `OK`; `Cancel` | Same as visible labels | Menu item; edit; buttons | Menu command `143`; edit `793498`; OK `8263114`; Cancel `1445454` | TO CONFIRM | `Ctrl+H, R`; Escape cancelled the dialog | Open Rename Hand, replace the current name with `HU-1`, and select OK. | The production demonstration changed `*Hand 2` to `*HU-1`. The 11 August inspection opened the labelled dialog and cancelled without a rename. | A rejected value or unchanged tab must be detected. | TO CONFIRM: controls are named, but setting the value and verifying rejection remain untested through the target runner. |
| Submit CI 10 | `Run Nash Calculation`; `Nash Calculation`; `OK` | Only `OK` and `Cancel` were exposed on 11 August | Dialog; buttons; other control types missing live; installed NatTable schema known statically | OK `662418`; Cancel `859034`; configuration IDs missing | TO CONFIRM LIVE; static NatTable copy and edit bindings exist | `Alt+R` opens the dialog. OK receives initial focus, so Enter is unsafe. A verified NatTable focus and copy route is TO CONFIRM LIVE. | Explicitly read and set `HRC 4.0 (Default)`, Full Tree, Until CI value is reached, CI Target `10.0`, Reset Regret clear, and Reset Strategies clear. Only then select OK. | The earlier demonstration showed Progress with `MC-CFR [Target CI < 10.00]`. | A rejected or failed submission must be distinguishable. No Nash grid probe may begin until a durable Cancel route is established. | NO: static inspection exposes a promising grid route, but no live safe read/write or Cancel path exists yet. |
| Submit CI 1 | `Run Nash Calculation`; `Reset Strategies`; `OK` | Only `OK` and `Cancel` were exposed on 11 August | Dialog; buttons; other control types missing live; installed NatTable schema known statically | OK `662418`; Cancel `859034`; configuration IDs missing | TO CONFIRM LIVE; static NatTable copy and edit bindings exist | `Alt+R` opens the dialog. A verified NatTable focus and copy route is TO CONFIRM LIVE. | Explicitly read and keep the same algorithm, scope, and sampling mode. Set CI Target to `1.0`, select the one-shot Reset Strategies option, keep Reset Regret clear, read back every value, and only then select OK. | The earlier demonstration showed Progress with `MC-CFR [Target CI < 1.00]`. The 11 August dialog retained CI `1.0`. | Escape did not close the dialog after keyboard inspection. A screenshot-located Cancel closed it without submission. No Nash grid probe may begin until a durable Cancel route is established. | NO: static inspection exposes a promising grid route, but no live safe read/write or Cancel path exists yet. |
| Detect running | `Progress`; `HU-1: Monte Carlo Sampling`; `MC-CFR [Target CI < 10.00]`; `MC-CFR [Target CI < 1.00]` | TO CONFIRM | TO CONFIRM | TO CONFIRM | TO CONFIRM | TO CONFIRM | Read the Progress pane without changing the operation. | The operation name, target CI, activity bar, and stop button were visible. | TBD | TO CONFIRM: the visible running state is observed, but accessible state is not. |
| Detect completion or failure | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TO CONFIRM |
| Viewer save | `File`; `Save As`; `File name:`; `Save as type:`; `*.hrcv Viewer Save`; `Save` | Standard dialog labels were exposed | Dialog; edits; combo box; buttons | Filename `1001`; type host `FileTypeControlHost`; Save `1`; Cancel `2` | Standard dialog patterns; exact set TO CONFIRM | `Ctrl+Alt+S` opens Save As; Escape cancelled | Preflight the exact `.hrcv` and matching `.zip` targets as absent. Open Save As, select `*.hrcv Viewer Save`, confirm the simulation filename with an `.hrcv` extension, browse to the applicable table-size folder, recheck the exact `.hrcv` target, and select Save. | `HU-1.5.hrcv` was submitted earlier. On 11 August, Save As opened at the HU folder with `*.hrcv Viewer Save` retained and was cancelled. | A prior run defaulted to `*.hrcz Complete Save`. The type can vary and must be read before Save. Cancel and stop on any existing target or overwrite prompt. | TO CONFIRM: the standard dialog is strong, but type selection and long-run queue behaviour remain untested through the target runner. |
| Verify Viewer output | `<simulation-name>.hrcv` | Not applicable | File | Not applicable | Not applicable | Not applicable | Verify the new file without opening or modifying it. | `HU-1.hrcv`, `HU-1.5.hrcv`, and `HU-2.hrcv` existed at the required HU path with non-zero sizes. | The file is absent, empty, or saved elsewhere. | YES for read-only metadata verification of these exact new files. |
| Submit strategy export | `Hand`; `Export Strategies`; `Complete Export`; `Depth:`; `PrettyPrint JSON`; `Node Filter Threshold %`; `OK` | Scope and some settings were unnamed; Depth and buttons were exposed | Combo box; spinner; edit; tree; buttons; settings NatTable known statically | Scope `1055578`; Depth spinner `334010`; Depth edit `334742`; OK `596422`; Cancel `399476` | TO CONFIRM LIVE; static native-control and NatTable routes exist | `Ctrl+H`, then `E` opened the dialog; Escape cancelled. Static alternate menu route and settings read-back remain TO CONFIRM LIVE. | Preflight and recheck the exact `.zip` target as absent. Explicitly select and read back Complete Export, set and read back visible Depth `16`, clear PrettyPrint JSON, keep threshold `0.1`, and select OK. Save `<simulation-name>.zip` in the applicable table-size folder with `*.zip Archived Json`. | The shortcut opened the dialog with the expected retained values and expanded two-node range tree. Earlier runs created non-empty archives. Static inspection says Complete Export is unlimited-depth despite the visible Depth value. | Settings may persist after Cancel. `PrettyPrint JSON` and threshold were not reliably named live. No explicit export-success or failure message was captured. Cancel and stop on any existing target or overwrite prompt. | NO until every required setting, focus target, retained-value read-back, and failure state has a live durable path. |
| Verify strategy archive | `<simulation-name>.zip` | Not applicable | File | Not applicable | Not applicable | Not applicable | Verify the new archive without opening or modifying it. | `HU-1.zip`, `HU-1.5.zip`, and `HU-2.zip` existed at the required HU path with non-zero sizes. | The file is absent, empty, or saved elsewhere. | YES for read-only metadata verification of these exact new files. |
| Close completed hand tab | `Close`; `Save Resource`; `Save '<simulation-name>'?`; `Don't Save`; `Home` | `Save Resource`; `Save 'HU-2'?`; `Save`; `Don't Save`; `Cancel` in the `HU-2` session | The three dialog buttons exposed class `Button` and UIA control type `Pane` in the `HU-2` session. | Numeric session values only; stability TO CONFIRM | `Don't Save` did not expose InvokePattern in the `HU-2` session. | `Ctrl+F4` had no effect on `*Hand 6` | Close the unsaved hand tab after both outputs are verified. Confirm that both output base filenames and the prompt name match the completed simulation. Only then select `Don't Save`. | `Don't Save` closed `*HU-1.5` and `*HU-2`. Only `Home` remained, and both output files persisted in each Viewer-only run. | The effects of `Save` and `Cancel` are TO CONFIRM. Any filename or prompt mismatch must stop the transition without discarding the hand. | TO CONFIRM: the semantic prompt and button names were accessible once, but a supported durable operation and tab-close target remain unproven. |
| Start next simulation | `Hand`; `Start New Calculation` | TO CONFIRM | TO CONFIRM | TO CONFIRM | TO CONFIRM | `Ctrl+W, H` | Start the next simulation only after both outputs are verified and the completed tab is closed. | The Home link opened Hand Setup during isolated discovery. | TBD | TO CONFIRM: the `Ctrl+W, H` route and the end-to-end post-close transition were not demonstrated. |

## Observable states

| State | Visible evidence | Accessible evidence | Distinguishable | Notes |
| --- | --- | --- | --- | --- |
| Configured | Hand Setup closed. An unsaved `*Hand 1` tab opened with strategy, range, and HU table views. Progress showed no active operation. | The tree exposed `*Hand 1`, `Strategy Table`, `Hand Settings`, and `Run Nash Calculation (Alt+R)`. | CONFIRMED | Tree creation completed. The calculation was not started. |
| Five-player inputs accepted | Basic Hand Data showed `HJ`, `CO`, `BU`, `SB`, and `BB` with `10`, `20`, `30`, `40`, and `50` bb. `Alt+N` opened Betting Setup. | The transient stack editor was unnamed and provider focus data was wrong. | CONFIRMED visually | This confirms the manual setup only. It does not confirm safe stack automation or a multiway tree. |
| HU 2bb shallow preview verified | Expanded Preview showed `R 2.00 SB PRE` with exactly one child, `C 1.00 BB PRE`. | The preview tree exposed root `R` and child `C`. | CONFIRMED for the pre-conversion revision at equal `2 bb` | No SB completion branch was present. The current HU candidate needs a Preview recheck. This does not validate the `5 bb` boundary or multiway behaviour. |
| Multiway retest estimate | Scripting showed `tree-building-3m-6m-candidate.js` without `[Errors]`, Total Nodes `1815589`, Total Tree Size `12.3GB`, and enabled Finish. | No accessibility tree was available in the contemporaneous capture. | CONFIRMED visually for the observed five-player setup | Euan reported loading the corrected worktree candidate. At the time of this estimate capture, Preview had not yet been inspected and Finish was not selected. The following row records the later Preview inspection. |
| Multiway representative Preview | The root and selected opening, squeeze, 3-bet, 4-bet, 5-bet, call-cap, SB-completion, and low-SPR flop paths were expanded. | Preview tab `661798`; tree `989272`; named headers; selectable action-only items. | CONFIRMED visually for the listed paths | Every displayed value matched the current candidate manifest for the reported corrected load. This is not exhaustive tree validation, and the accessible item names omitted amount, player, and street. |
| Player-count choices exposed | Pressing `Tab` seven times from the newly opened Basic Hand Data page, then `Ctrl+A` and `Ctrl+Home`, selected the cell displaying `Auto`; Space opened `Auto`, `HU`, and table sizes `3-max` through `10-max`. Escape closed the list without changing the setup. | Every item had a distinct accessible name and was selectable. The latest list ID in that run was `11606852`; earlier openings used `95687566` and `18224586`. Provider focus still pointed to the background Range edit. | CONFIRMED for one non-coordinate focus-and-open route | Run 12 did not activate a choice. Run 13 later confirmed HU row removal and retained blind stacks; other choice effects and standalone delivery remain TO CONFIRM. |
| HU table-size selection committed | From the retained five-player setup, the keyboard route selected `HU`. Before Enter, the list remained open and five rows remained; after Enter, only `SB 4000 / 40.0 bb` and `BB 5000 / 50.0 bb` remained. | The open list exposed `HU` as a named selectable item. Its latest list ID was `6359478`; provider focus still pointed to the background Range edit. | CONFIRMED for one supervised non-coordinate selection | At the end of run 13, multiway selection effects, active-seat overwrite/read-back, and standalone delivery remained TO CONFIRM. Run 14 later confirmed supervised HU overwrite and visual read-back; machine-readable verification remains TO CONFIRM. |
| Multiway stack keyboard edit | With HJ Chips focused, `F2` exposed `1000`; typing the same value and pressing Enter moved to CO Chips with `2000` selected. Escape preserved CO. | Stack cells were absent from the accessibility tree, and the provider continued to report background edit `69008` as focused. | CONFIRMED after an existing cell was focused | This earlier observation alone confirmed post-focus movement, edit mode, same-value commit, advance, and cancel without a net value change. Run 14 later confirmed the supervised HU route from initial grid focus through different-valid-value entry. Standalone foreground/focus assertions and machine-readable read-back remain TO CONFIRM. |
| HU stack values committed and rejected input recovered | The combined keyboard route committed `SB 4100 / 41.0 bb` and `BB 5100 / 51.0 bb`; Escape cancelled the blank third-row editor. Invalid `abc` stayed red and did not commit on Enter; Escape restored `4100 / 41.0 bb`. | Transient editors were unnamed and changed IDs. The provider incorrectly reported background Range edit `69008` as focused. | CONFIRMED visually for this supervised HU path | This proves different-valid-value entry, derived visual read-back, blank-row cancellation, and one non-numeric recovery. Machine-readable cell verification, multiway operation, and standalone delivery remain TO CONFIRM. |
| Renamed | The tab changed from `*Hand 2` to `*HU-1`. | TO CONFIRM | CONFIRMED visually | Progress later used the `HU-1` name. |
| Queued | No persistent queue list was visible in the captured states. | TBD | TO CONFIRM | The CI 1 dialog opened while CI 10 was visible, but the small operation transitioned quickly. |
| CI 10 running | Progress showed `HU-1: Monte Carlo Sampling` and target CI `< 10.00`. | TBD | CONFIRMED visually | A red stop button and activity bar were visible. |
| CI 1 running | Progress showed `HU-1: Monte Carlo Sampling` and target CI `< 1.00`. | TBD | CONFIRMED visually | Reset Strategies was selected in the submitted dialog. |
| CI 10 no longer displayed | The CI 10 line was replaced by the CI 1 line. | TBD | CONFIRMED visually | The reason for the transition is TO CONFIRM. No explicit successful-completion marker was captured. |
| No operation displayed | Progress later showed `No operations to display at this time.` | TBD | CONFIRMED visually | This text alone does not distinguish success from failure. |
| Viewer saved | The Save As dialog accepted `HU-1.5.hrcv` with `*.hrcv Viewer Save`. | File existence was verified separately. | CONFIRMED | Viewer Save returned to the still-unsaved `*HU-1.5` tab. |
| Tree-script failure | Script Error showed `Error: Effective stack does not match a configured workbook column: 100000`. After dismissal, Scripting showed `[Errors]`, zero nodes, and disabled Finish. | The exact error text and OK button were exposed in the inspected tree. | CONFIRMED for this failure | No tree, calculation, or output followed. Generic failure detection remains TO CONFIRM. |
| Calculation or output failed | TBD | TBD | TO CONFIRM | No Nash, Viewer Save, or export failure has been observed. |
| Complete Save | The first Save As used the default `*.hrcz Complete Save` in error. The tab changed to `HU-1.hrcz`. | TBD | CONFIRMED visually | The unintended file was present and unmodified when last checked; later state is TO CONFIRM. |
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
| 2 | 10 August 2026, 23:08–23:10 BST | `HU-1.hrcv`; unintended `HU-1.hrcz` | PARTIAL DEMONSTRATION | Progress changed to no operation displayed within the observation period. Explicit completion and production duration remain TO CONFIRM. | The demonstration renamed `*Hand 2` to `*HU-1`, submitted both Nash configurations, showed both running targets, made an accidental Complete Save, corrected it with Viewer Save, and verified the Viewer file. | This observation began with `*Hand 2` and is separate from run 1. Long-run queue order and explicit calculation success or failure remain unconfirmed. The unintended Complete Save was present and unmodified when last checked; later state is TO CONFIRM. |
| 3 | 10 August 2026, 23:18 BST | `HU-1.zip` | PARTIAL DEMONSTRATION | The export and close transition completed within the observation period. | The demonstration kept Complete Export, changed Depth from `2` to `16`, kept PrettyPrint JSON clear, and kept the threshold at `0.1`. It saved a non-empty archive and then closed the source tab. | The source tab was `HU-1.hrcz` from run 2. The archive contents and close behaviour after a Viewer-only save remain unverified. |
| 4 | 10 August 2026, 23:35–23:36 BST | `HU-1.5.hrcv`; `HU-1.5.zip` | PARTIAL DEMONSTRATION | Viewer Save submission, non-empty output creation, and Viewer-only tab closure were observed. | The demonstration began on `*HU-1.5`, submitted Viewer Save and strategy export, selected `Don't Save` in the close prompt, and returned to `Home`. Both files were non-empty after close, and no matching `.hrcz` file was present. | Euan reported that rename and both Nash runs were already complete before observation. Their completion was not independently observed. File contents were not opened. |
| 5 | 10–11 August 2026, ending 00:13 BST | `HU-2.hrcv`; `HU-2.zip` | PARTIAL DEMONSTRATION | The two-node calculations returned to idle during the supervised observation. No explicit calculation-success marker appeared. | The pre-conversion HU candidate from `9b24166` created an equal-stack `2 bb` tree. Hand Setup reported two nodes. The run renamed the hand, submitted CI `10.0`, submitted CI `1.0` with Reset Strategies, created both non-empty outputs, verified no matching `.hrcz`, selected `Don't Save` on the exact `HU-2` prompt, and returned to `Home`. | Run 5 did not inspect Preview or confirm the cutoff; run 6 later confirmed the equal-`2 bb` case for that revision. Euan assisted with strategy export. The archive contents were not opened. Codex-specific window activation changed the HRC bounds and was discontinued. An unverified coordinate selected the root row instead of the tab close control; the later close point was verified by its exact tooltip before use. |
| 6 | 11 August 2026 | None | ACCESSIBILITY DISCOVERY | No Nash operation or file write occurred. | HRC remained in a restored, near-full-size window. The same pre-conversion HU candidate loaded through the standard Open dialog. Hand Setup reported two nodes. Expanded Preview showed `R 2.00 SB PRE` with only `C 1.00 BB PRE`. Enter finished to `*Hand 6`. Rename, Nash, Save As, and Export Strategies were opened for inspection and cancelled. | Euan manually selected Hand Setup Next after programmatic input failed. A later same-day follow-up confirmed `Alt+N` as the keyboard route. Nash settings remained inaccessible. `Ctrl+F4` did not close the hand. The unsaved `*Hand 6` was still open at the end of run 6; its later disposition is TO CONFIRM. The current HU candidate needs a small Preview recheck. |
| 7 | 11 August 2026 | None | SCRIPT ERROR | No tree was finished and no Nash operation or file write occurred. | Euan configured five rows as `HJ 10`, `CO 20`, `BU 30`, `SB 40`, and `BB 50` bb. `Alt+N` advanced to Betting Setup. Loading the then-byte-identical pre-correction multiway candidate produced `Error: Effective stack does not match a configured workbook column: 100000`. | The pre-script default estimate was `448527` nodes and `3.1GB`; it was not a candidate result. After the error, Total Nodes was `0` and Finish was disabled. Offline regression coverage was added, but the corrected candidate was still HRC-unverified at the end of run 7. |
| 8 | 11 August 2026 | None | TREE ESTIMATE AND PARTIAL PREVIEW | No tree was finished and no Nash operation or file write occurred. | Euan reported loading the corrected worktree candidate. A contemporaneous capture showed its basename without `[Errors]`, Total Nodes `1815589`, Total Tree Size `12.3GB`, and enabled Finish. Root and representative deeper Preview paths matched the current candidate manifest. | The prior `100000` error did not recur. Preview evidence is path-scoped, Finish was not selected, and the visible basename did not expose the full loaded path. Other stacks, table sizes, boundaries, later streets, and unexpanded paths remain TO CONFIRM. |
| 9 | 11 August 2026 | None | ACCESSIBILITY DISCOVERY | No row, stack, calculation, tree, or file was changed. | `Alt+B` and `Alt+N` moved between the two setup pages. After a one-use screenshot-located focus, arrows, `Ctrl+Home`, `F2`, Enter, and Escape supported an observed grid-edit sequence. A same-value HJ Chips commit advanced to CO Chips; all five values remained unchanged. | No durable non-coordinate entry into the grid was found in run 9. Stack cells were absent from the accessibility tree, Tab routes failed, provider focus remained wrong, and blank-row creation and different-valid-value entry were not tested. The setup was open on Basic Hand Data at the end of the run. |
| 10 | 11 August 2026 | None | ACCESSIBILITY DISCOVERY | No selector choice, row, stack, calculation, tree, or file was changed. | Tab visibly reached Next, Cancel, information text, and four unnamed toolbar buttons, but traversal did not establish a visible, repeatable route to the selector or stack grid. A one-use screenshot-located click opened `Auto`; the accessibility tree exposed named selectable choices from `HU` through `10-max`. Escape closed the list. | Run 10 found no durable semantic or keyboard route to open the selector, and no choice was activated. The existing five rows and `10/20/30/40/50 bb` values remained unchanged. Hand Setup was open on Basic Hand Data at the end of the run. |
| 11 | 11 August 2026 | None | ACCESSIBILITY DISCOVERY | No selector choice, row, stack, finished tree, calculation, or file was changed. | After the screenshot-located open and Escape from run 10, Space reopened the player-count list while the cell displaying `Auto` remained current; `Alt+Down` and `F4` did not. Its list ID changed. Escape closed it. `Alt+N` and `Alt+B` cycled the setup pages and preserved the five inputs, but Space then did not reopen the list. | Space was confirmed as a post-focus list-opening action in run 11 only. The page cycle did not provide repeatable selector focus, no choice was activated, and selection effects remained untested. Hand Setup was open on Basic Hand Data at the end of the run. |
| 12 | 11 August 2026 | None | ACCESSIBILITY DISCOVERY | No selector choice, row, stack, finished tree, calculation, or file was changed. | Static installed-component inspection identified the NatTable selection bootstrap. In a separate live check, the named Home link opened Basic Hand Data with the previous five inputs retained. Pressing `Tab` seven times, then `Ctrl+A`, `Ctrl+Home`, and Space selected the cell displaying `Auto` and opened every named player-count choice without a pointer. Escape closed the list and preserved all inputs. | This superseded the missing initial selector-focus route from runs 10 and 11 for one supervised setup. No choice was activated. Two cached named-target attempts, `Alt+C`, and Escape did not dismiss Hand Setup; `Alt+F4` returned safely to `Home`. Selection effects and retained-value handling were unproven at the end of run 12; runs 13–14 later confirmed the supervised HU effects. Standalone delivery remains unproven. |
| 13 | 11 August 2026 | None | ACCESSIBILITY DISCOVERY | The disposable in-memory row set changed; no stack was edited, and no tree, calculation, or file was created. | The confirmed keyboard bootstrap opened the selector. Down visibly selected `HU`; Enter committed it and reduced the retained five-player setup to `SB 4000 / 40.0 bb` and `BB 5000 / 50.0 bb`. `Alt+F4` returned to `Home` without advancing or prompting. | This proves one supervised non-coordinate table-size selection and shows that the change retained prior blind stacks. At the end of run 13, active-seat overwrite/read-back and rejected-input handling remained unproven; run 14 later confirmed supervised visual HU handling. Multiway choice effects, machine-readable verification, and standalone operation remain TO CONFIRM. |
| 14 | 11 August 2026 | None | ACCESSIBILITY DISCOVERY | The disposable in-memory stack values changed; no wizard advance, tree, calculation, or file was created. | A new setup showed `Auto` with the retained SB/BB row set. The confirmed keyboard route selected HU, entered `4100` and `5100`, visibly read back `41.0 bb` and `51.0 bb`, and cancelled the blank next-row editor. Invalid `abc` stayed red and uncommitted; Escape restored `4100`. `Alt+F4` returned to `Home`. | This proves the combined supervised HU keyboard path, two different-valid-value commits, visual derived-value checks, blank-row cancellation, and one rejected-input recovery. It does not prove machine-readable cell verification, multiway choice/edit behaviour, reliable focus metadata, or standalone operation. |

## Blockers

- `Alt+N` is a confirmed supervised path for Hand Setup Next. The target runner
  still must focus the owned dialog, deliver the shortcut, and detect Betting
  Setup reliably; earlier automated keyboard input reached the background
  window.
- The non-coordinate NatTable bootstrap now covers HU selection and both active
  stack edits end to end in a supervised run. Two different values committed,
  their derived BB values were checked visually, the blank next-row editor was
  cancelled without adding a row, and one non-numeric value was rejected and
  safely cancelled. New setups still retain prior row/value state independently
  of the `Auto` selector label. The provider reported the wrong background
  focus, transient editor IDs changed, and no machine-readable stack-cell
  verification was established. Multiway choice/edit effects, foreground and
  focus assertions, and standalone delivery are not safe for automation.
- The script-picker button has no accessible name or access key. Its numeric ID
  changed in every inspected session. Semantic invocation failed.
- After Euan reported loading the corrected 3–6-max candidate, HRC passed
  runtime evaluation and produced a non-zero tree estimate in the observed
  five-player setup. The capture exposed only the basename. The inspected
  Preview paths matched the current candidate manifest, but unexpanded paths,
  other stacks and table sizes, boundary cases, and later streets remain
  unverified. Preview item names omitted amount, player, and street, and provider
  focus data was wrong. Durable automated validation remains blocked.
- Nash Calculation exposed only OK and Cancel. The required algorithm, scope,
  sampling, CI, Reset Regret, and Reset Strategies controls were absent from
  the accessibility tree. Static inspection shows that OK initially owns focus,
  most accepted settings persist, and reset choices are one-shot. Every value
  must be read back on both openings; a verified grid-focus and Cancel route is
  still missing.
- Export Strategies did not expose reliably named PrettyPrint JSON and
  threshold controls. Its settings can persist after Cancel. Static inspection
  shows that Complete Export is unlimited-depth even though Depth remains
  visible. Safe targeting and read-back for every required setting are
  unproven.
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
  `2 bb` Preview of the pre-conversion HU revision directly showed the SB raise
  to `2.00 BB` with only the BB call. No SB completion branch was present. This
  confirms that revision's HU rule at `2 bb`; the current HU candidate needs a
  Preview recheck.
  The five-player row order, manual stack entry, and `Alt+N` transition were
  observed. The HRC-tested pre-correction multiway candidate stopped with the
  exact `100000` Script Error. After Euan reported loading the corrected
  worktree candidate, HRC displayed its basename without `[Errors]`, produced a
  `1815589`-node estimate, and enabled Finish. The capture did not expose the
  full loaded path. Root and representative deeper Preview paths matched the
  current candidate manifest for this five-player setup. This is path-scoped
  evidence, not exhaustive validation of the tree.
  A separate supervised HU discovery joined the non-coordinate table-size
  bootstrap to two different-valid-value stack commits, visual derived-value
  checks, blank-row cancellation, and one rejected-input recovery. It did not
  establish machine-readable cell verification or standalone focus safety.
  The `5 bb` boundary, the first supported stack above it, and dynamic post-fold
  behaviour remain unconfirmed. Long-run queue behaviour, explicit completion
  or failure detection, and several critical accessible targets also remain
  unconfirmed.

## Next action

Establish machine-readable NatTable cell read-back and exact foreground/focus
assertions for the confirmed HU route. Validate one multiway table-size choice
as a separate row-creation and multi-row edit case. Revalidate both routes
through the future standalone driver rather than treating Codex key delivery
as production proof. Also find a durable path for the unnamed script picker.
Retain separate Preview checks for other table sizes, boundary stacks, later
streets, and unexpanded paths. Retain the Nash, export, hand-tab close, and
`Don't Save` blockers. Verify the Save As destination, Viewer type, filename,
and extension on every save. Then map accessible Progress states that
distinguish queue order, successful completion, and failure. Do not start an
automated or long-running test until these controls are mapped and Euan
authorises that specific test.
