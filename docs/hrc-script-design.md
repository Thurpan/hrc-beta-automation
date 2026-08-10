# HRC script design and decision record

## Status

This document records the current inputs and decisions for the project HRC
tree-building scripts. Separate 3–6-max and heads-up (HU) candidates are ready
for offline review, but neither candidate has been validated in HRC.

The design combines three sources:

- Euan's decisions in this project;
- the current sizing workbook; and
- the [shared HRC-GPT prototype](https://chatgpt.com/share/6a799f62-3738-83eb-9798-a1a36aafd84a).

The shared page and its refreshed 1,926-line prototype were reviewed on 10
August 2026. The final JavaScript block from that review is stored verbatim in
[`shared-chatgpt-prototype.js`](../reference/hrc/shared-chatgpt-prototype.js).
Its SHA-256 is
`f39e83006039b26f27beed4c7f0f8e08d6929cfcf31d3b5deeadd2a448448f37`.
It remains an unapproved reference and has not been loaded into HRC. Re-review
the shared page before relying on these findings if its content changes.

The project-owned working files are:

- [`tree-building-3m-6m-candidate.js`](../scripts/hrc/tree-building-3m-6m-candidate.js)
  for configured table sizes from three through six players; and
- [`tree-building-hu-candidate.js`](../scripts/hrc/tree-building-hu-candidate.js)
  for a true two-player configuration.

Both are standalone HRC scripts. The 3–6-max candidate derives from the
archived prototype. The HU candidate uses the HU workbook tab and shares the
same reviewed postflop policy. Both use the agreed 50% all-in threshold.

Use [`hrc-scripting.md`](hrc-scripting.md) for the documented HRC API,
execution model, and product limits. The shared prototype is useful working
material, but it is generated advice rather than an official API contract.

No script has been loaded into HRC. No tree or calculation has been created.
All runtime behaviour remains unverified on the licensed host `EM-3960X`.

## Source boundaries

Apply these rules when sources disagree:

1. Follow Euan's direct decisions for poker policy.
1. Use the workbook as the current numeric sizing input.
1. Use the public HRC API documentation for technical capabilities and legal
   normalisation.
1. Use the shared prototype only as historical implementation input. Apply
   project changes to a project-owned working candidate.

Do not silently replace a workbook value with a value from the prototype.
Record each correction and its authority. `3m-6m!P29` was corrected from
`75` to `7.5` under Euan's instruction to fix the audited prototype issues.
The corrected value follows the surrounding 50 bb, 60 bb, and 70 bb sequence
of `7`, `7.5`, and `7.5`.

## Project artefacts

The simulation inputs and run-order artefacts are stored together:

| Artefact | Purpose |
| --- | --- |
| [`Sizes_for_hrc_script.xlsx`](../data/stack-sizes/Sizes_for_hrc_script.xlsx) | Current position, action, and effective-stack sizing matrix. |
| [`simulation_run_order.txt`](../data/stack-sizes/simulation_run_order.txt) | Generated heads-up, three-player, and five-player simulation run order. |
| [`generate_simulation_run_order.py`](../scripts/generate_simulation_run_order.py) | Recreates `simulation_run_order.txt` from the configured run-order batches. |
| [`shared-chatgpt-prototype.js`](../reference/hrc/shared-chatgpt-prototype.js) | Verbatim shared-thread snapshot. Refresh it only from the shared source. Do not apply project fixes. |
| [`tree-building-3m-6m-candidate.js`](../scripts/hrc/tree-building-3m-6m-candidate.js) | Standalone 3–6-max working candidate. |
| [`tree-building-hu-candidate.js`](../scripts/hrc/tree-building-hu-candidate.js) | Standalone HU working candidate. |
| [`test_tree_building_3m_6m_candidate.js`](../tests/hrc/test_tree_building_3m_6m_candidate.js) | Offline 3–6-max regression tests. |
| [`test_tree_building_hu_candidate.js`](../tests/hrc/test_tree_building_hu_candidate.js) | Offline HU regression tests. |

The workbook contains two sheets and two tables. `3m-6m!A1:S39` contains the
multiway policy. `HU!A1:BQ11` contains the HU policy. Cell `3m-6m!P29`
contains numeric value `7.5`.

The 3–6-max sheet has these 18 stack columns, in big blinds:

```text
5, 7.5, 10, 12.5, 15, 17.5, 20, 22.5, 25,
30, 35, 40, 45, 50, 60, 70, 80, 100
```

It contains rules for opens, 3-bets, 4-bets, and 5-bets or later. The 3-bet
section separates blind-versus-blind, BB, SB, and in-position cases.

The HU sheet has 68 exact stack columns from 1 bb through 80 bb. It contains
six policy rows: SB and BB rows for opens, 3-bets, and 4-bets. A cell can
contain `allin`, one fixed-bb size, or two comma-separated fixed-bb sizes.

The generator writes the batches in this order:

1. Heads-up equal-stack setups from 1 to 80 big blinds.
1. Three-player setups from the first four-option priority list.
1. Five-player setups from the first three-option priority list.
1. Three-player setups from the full 18-option list.
1. Five-player setups from the seven-option core list.
1. Five-player setups from the full 18-option list.

Each batch uses its own configured option order to calculate setup priority.
The generator omits an exact setup if an earlier batch already contains it.
The number of hyphen-delimited stacks identifies the player count.

The final three-player and five-player batches use the workbook's 18 stack
values. Therefore, each multiway effective stack matches a workbook column.
The heads-up batch uses the HU sheet's 68 exact stack columns.

The generator also omits a setup with only one largest stack. Under the agreed
convention, capping that stack at the next-largest stack produces the same
effective stacks for every active player. For heads-up setups, this rule leaves
one equal-stack simulation for each configured size.

## Effective-stack convention

The effective stack is dynamic and specific to the active player. Recalculate
it at every decision.

For each player, calculate the total stack as follows:

```text
player total = active chips + dead chips + remaining chips
```

Then calculate the active player's effective stack:

```text
largest non-folded opponent total = maximum total of every other player who has not folded
effective stack = minimum of the active player total and largest non-folded opponent total
effective stack in bb = effective stack / nominal big blind
```

This definition has these consequences:

- Exclude a folded opponent even when that player contributed chips to the pot.
- Include a non-folded all-in opponent.
- Recalculate after each fold because the largest non-folded opponent can change.
- Use the largest non-folded opponent, not the shortest opponent or last raiser.
- Use the last raiser only to classify the action and position when required.
- Do not use `countPlayersLive()` to find non-folded opponents. It excludes
  all-in players.
- Treat a state with no non-folded opponent as invalid. Do not send it to a
  zero-stack sizing bucket.

Example:

| State | Active player | Non-folded opponent totals | Effective stack |
| --- | --- | --- | ---: |
| CO 100, BTN 100, SB 10, BB 10 | CO | 100, 10, 10 | 100 bb |
| CO folds; BTN 100, SB 10, BB 10 remain | BTN | 10, 10 | 10 bb |
| CO 100, BTN 100, SB 10, BB 10 | SB | 100, 100, 10 | 10 bb |

This metric is separate from HRC's `sizingAllIn()` and
`getStackPotRatio()`. Use `sizingAllIn()` as HRC's legal raise-to cap. Do not
use either HRC value to select the project stack bucket.

The refreshed shared thread specifies one narrowly scoped exception for the
squeeze increment. Its 40 bb threshold uses the smaller of the squeezer's total
stack and the first caller's total stack. This pairwise threshold is not the
project effective stack defined above. Use it only to select the agreed squeeze
increment; the ordinary 3-bet base size still uses the project effective stack.

## Archived shared prototype assessment

The shared prototype contains a useful starting structure for the
effective-stack helper. It adds active, dead, and remaining chips for each
player. It excludes only folded opponents and therefore retains non-folded
all-in opponents. It also recalculates the value for each callback.

Do not copy the helper unchanged. When no non-folded opponent exists, the
prototype returns the active player's full stack. The project convention
treats that state as invalid and requires an explicit guard.

Other useful implementation patterns are:

- return arrays consistently from sizing callbacks;
- copy Java arrays with `Array.from(...)` before JavaScript array operations;
- select one sizing branch from the current decision context; and
- avoid mutable state between callback evaluations.

The archived prototype reconstructs the opener's project effective stack at
the time of the first raise. It derives the players who had already folded from
the preflop action sequence. It then looks up the opener's expected RFI table
value instead of reading the actual raise amount. This can misclassify an
optional all-in open as the ordinary table open.

The archived prototype applies a 40% preflop all-in replacement threshold. It
also uses a script-owned `-1` sentinel to add all-in at every preflop decision.
These statements remain historical facts about the archived source.

## Working candidates status

The working candidates preserve the archived source as provenance and apply
project policy in separate files. Their offline tests pass, but a passing test
suite does not prove HRC runtime behaviour.

| Area | Archived prototype | Working candidates | Authority | Validation status |
| --- | --- | --- | --- | --- |
| Preflop all-in replacement | 40% of the distance from active chips to HRC's all-in raise-to size. | 50% of that distance. | Euan's direct decision. | Offline boundary tests pass; HRC unverified. |
| Workbook cell `P29` | Uses `7.5`; workbook previously contained `75`. | Uses corrected workbook value `7.5`. | Euan's instruction to fix the audited issues. | Workbook and all 396 embedded table cells compare equal. |
| Effective-stack terminal state | Falls back to the player's full stack. | Throws when no non-folded opponent exists. | Agreed project convention. | Offline error-path test passes. |
| Stack buckets | Rounds up and caps unsupported values. | Requires an exact workbook stack column. | Workbook values are exact policy inputs. | All 18 multiway and 68 HU columns, plus invalid values, are tested. |
| Supported setup | Does not guard the player count or straddles. | One candidate accepts 3–6 players. The other accepts exactly two. Both require a non-straddled setup. | Euan's two-script decision and API limits. | Both guards are tested; straddles require UI verification. |
| Multiway open classification | Reconstructs only the expected workbook open. | Compares `IPlayerAction.getAmount()` with the expected open. Non-matching opens receive only the all-in response. | Fix for optional all-in misclassification. | Ordinary, all-in, normalised, scaled-unit, and invalid-amount tests pass. |
| HU action classification | No separate HU policy. | Routes by the full action line. It distinguishes the SB open from an SB completion followed by a BB raise. | Euan's confirmed HU row meanings. | Both 3-bet and both 4-bet lines are tested. |
| Preflop calls | Closing action can bypass caller and cold-call limits. | Multiway keeps hard caps of two, one, and one. HU permits only the sole opponent. Both allow one non-cold closing response to a 5-bet or later all-in. | Fix for reachable excess-call branches. | Call topology and later all-in responses are tested. |
| Blind-versus-blind 4-bet | Selects by current player and last raiser only. | Also requires the original opener to be a blind. | Fix for non-blind squeeze lines. | Genuine and false-positive lines are tested. |
| Legal-size duplicates | De-duplicates before legal normalisation. | Mirrors minimum/all-in clamping, then de-duplicates. | HRC normalisation contract. | Minimum and all-in collisions are tested. |
| Postflop calls | Relies on HRC's default `true`. | Returns `true` explicitly. | Public [`ITreeBuildingScript`](https://www.holdemresources.net/s/updatesites/hrc/latest/scripting/javadoc/net/holdemresources/scripting/treescripts/api/ITreeBuildingScript.html) default and explicit project choice. | Offline callback test passes. |
| Bets per street | Omits cap logic. | Uses `null` for the blank screenshot field. A future numeric value produces all-in only after the cap. | Linked screenshot and HRC release notes. | Blank and numeric-cap paths are tested. |

In both candidates, the 50% rule applies to the requested size before legal
normalisation. For example, a requested size below the threshold can remain
non-all-in even when HRC raises it above the threshold to satisfy the legal
minimum. This preserves the archived default-script ordering while changing
only the agreed percentage.

## Workbook alignment

The corrected workbook is the numeric source of truth. The 3–6-max candidate
contains 18 arrays with 324 cells. Two blind-versus-blind rows reuse the same
arrays. Together with the 5-bet row, all 396 populated multiway policy cells
match the candidate.

The HU candidate contains six arrays with 408 cells. Its stack grid and policy
manifest match all 68 columns in `HU!B1:BQ11`. Formula-backed workbook cells
are embedded by their displayed values. The workbook has no HU 5-bet row, so
the agreed 5-bet-and-later all-in rule is explicit in the script.

`3m-6m!P29`, IP 3-bet versus a `2.25x` open at 60 bb, is `7.5`.

## Decisions and runtime checks

Do not treat an offline-tested decision as HRC-validated:

| Decision | Working candidate behaviour | Status |
| --- | --- | --- |
| Effective-stack basis | Cap the active player by the largest non-folded opponent. Recalculate at every decision. Include all-in opponents. | Agreed; offline tested; HRC unverified. |
| Supported setup | Use the 3–6-max candidate for three through six configured players. Use the HU candidate for exactly two. | Current project scope; both guards tested. |
| 3–6-max position mapping | Use BB, SB, and BTN helpers explicitly. Use the shared UTG–CO row for every remaining seat. | Every position at 3m, 4m, 5m, and 6m is tested offline. |
| HU position mapping | Use only the SB and BB helpers. Do not depend on a numeric button index. | Offline tested; HRC's two-player helper values remain `TO CONFIRM`. |
| Straddles | Do not use either candidate with a straddled setup. The API does not expose a reliable straddle detector. | Manual UI precondition. |
| Maximum active players | A UI-forced fold can change the effective stack and sizing bucket. | `TO CONFIRM` in HRC. |
| Effective stack outside workbook columns | Throw instead of rounding or capping. | Both grids are tested offline. |
| Opening sizes | Use the RFI rows from the applicable workbook sheet. | All embedded cells match; HRC unverified. |
| BB raise after an SB completion | Use the workbook BB RFI row. In HU this is the only meaning of `RFI / BB`. | Confirmed by Euan; offline tested; HRC unverified. |
| Other limps and isolation raises | Permit only the SB completion before a voluntary raise. | Current scope; policy expansion remains `TBD`. |
| SB limp-reraise | In 3–6-max, treat the BB raise as the original open. In HU, use `3bet / SB`. | Confirmed HU meaning; offline tested; HRC unverified. |
| 3-bet sizes | Use the applicable workbook rows, including `3m-6m!P29 = 7.5`. | All embedded cells match; HRC unverified. |
| Squeeze sizes | Use the project effective stack for the base table. Use `min(squeezer total, first-caller total)` for the separate 40 bb increment threshold. Ignore calls before the original raise. | Agreed; offline tested; HRC unverified. |
| Multiway 3-bet selection | Compare the recorded first raise amount with its expected workbook open. Give a non-matching open only the global all-in response. | Corrected and offline tested; HRC unverified. |
| HU 3-bet selection | Route by the first raiser and whether the SB completed before that raise. Do not classify comma-pair or legally normalised raises as scalar open categories. | Corrected and offline tested; HRC unverified. |
| 4-bet sizes | Multiway selects blind-versus-blind, IP, or OOP rows. HU selects the SB or BB fixed-bb row from the action line. | Both candidates are offline tested; HRC unverified. |
| 5-bets and later | Return all-in only in both candidates. | Agreed; multiple later raise levels tested. |
| Preflop calls | Multiway permits two calls versus an open and one versus a 3-bet or 4-bet. HU permits only the sole opponent. Reject cold calls. Permit one non-cold closing response to a 5-bet or later all-in. | Corrected and offline tested; HRC unverified. |
| Preflop all-in additions | Always offer all-in. Replace a requested size at 50% of the distance from active chips to HRC's all-in raise-to size. | 50% agreed; offline tested; HRC unverified. |
| Postflop sizes | Use the screenshot's HU and multiway fixed pot fractions. Replace normal rows with low-SPR rows at HU SPR `<= 2.5` and multiway SPR `<= 1.5`. Add all-in at SPR `<= 5`. | All matrix rows and boundaries tested offline. |
| Limited donks | Allow a donk only when the player made a previous bet or raise. Use the low-SPR bet row when low SPR applies. | Screenshot-aligned and offline tested. |
| Postflop calls | Return `true` explicitly. | Matches the public API default; offline tested. |
| Bets per street | Keep `null`, which represents the blank HU and multiway screenshot cells. A future numeric value allows only all-in after the cap. | Blank policy confirmed from source screenshot. |
| Postflop horizon | Continue through river with two or three players able to act and through turn with four. Use `countPlayersLive()`, which excludes all-ins. | Screenshot-aligned and offline tested. |
| Postflop abstractions | The screenshot shows flop `1024`, turn `256`, and river `256`. The HRC UI owns these settings. | Must be configured and verified in HRC. |
| HRC SPR and all-in semantics | Use HRC's `getStackPotRatio()` and `sizingAllIn()`. The public API does not define the controlling opponent in every multiway state. | `TO CONFIRM` in HRC. |
| Parser and legal normalisation | Multiway uses `bb` and `x` rules. HU uses fixed-bb amounts, including comma-pair cells. Apply the 50% test before minimum/all-in clamping, then de-duplicate. | Offline tested; inspect exact HRC preview. |

The effective-stack convention is not pending. It applies to all project stack
references. HRC's SPR and legal all-in helpers remain separate runtime inputs.

## Implementation boundaries

Both implementations must keep these concepts separate:

- derive the active player's project effective stack;
- map that value to an agreed workbook column;
- classify the current action as an open, isolation raise, 3-bet, squeeze,
  4-bet, or later raise;
- select the position and prior-open category;
- return the one configured size set for that node; and
- let HRC enforce minimum raises and its legal all-in cap.

The HU candidate must classify the full action line. It must not reuse the
multiway open-size category router or squeeze logic. The HU workbook 4-bet
values are fixed-bb amounts, not previous-raise multipliers.

Do not use HRC's legal normalisation to conceal a missing or incorrect rule.
Inspect the tree preview for the returned and normalised sizes.

Tree size is a material risk. Each candidate combines an unconditional preflop
all-in option, multiple postflop sizes, low-SPR alternatives, an SPR `5`
postflop all-in, an unlimited bets-per-street field, and unrestricted postflop
flat calls. The source screenshot estimates 19,888,053 nodes and 131.7 GB with
flop, turn, and river abstractions of `1024`, `256`, and `256`. Recalculate the
estimate after loading the candidate. Do not start a calculation from this
estimate without Euan's approval.

## Validation cases

The offline suite verifies:

- the archived source hash;
- the 100/100/10/10 effective-stack change after the deep opponent folds;
- inclusion of non-folded all-in opponents and rejection of a missing opponent;
- every exact workbook stack column and rejection of an intermediate value;
- all embedded preflop table cells and the corrected `P29` value;
- recorded ordinary, optional all-in, mandatory all-in, legally changed, and
  invalid opens;
- both blind-versus-blind 3-bet directions;
- one- and two-caller squeeze rules around the 40 bb pairwise threshold,
  including a shallow all-in table entry;
- hard call caps, cold-call rejection, and later all-in closing responses;
- genuine and false-positive blind-versus-blind 4-bet lines in both directions;
- the 50% requested-size boundary and legal-size de-duplication;
- every HU and multiway postflop matrix;
- Limited donks, blank and numeric bets-per-street paths, and all SPR
  boundaries; and
- all configured postflop horizons, including an all-in participant.

The HU suite also verifies:

- all 68 stack columns and all 408 workbook policy cells;
- the `allin`, single-size, and comma-pair cell grammar;
- the SB open and the BB raise after an SB completion;
- both HU 3-bet lines and both HU 4-bet lines;
- 5-bet and later all-in-only behaviour;
- the 4 bb `1.93bb` request normalising to the legal `2bb` raise;
- the 20 bb `7.5bb` limp-reraise normalising to `11bb` without becoming
  all-in; and
- the complete shared HU postflop policy.

Complete these checks in HRC on `EM-3960X`:

1. Verify a non-straddled disposable setup for the candidate under test.
1. Test the 3–6-max candidate once at each configured table size.
1. Test the HU candidate in a true two-player setup.
1. Load only the applicable candidate without starting a calculation.
1. Verify the tree estimate completes without a script error.
1. Inspect every position and preflop bet level in the tree preview.
1. In HU, verify that HRC reports the expected SB/button and BB helpers.
1. In HU, inspect both sizes from representative comma-pair cells.
1. Inspect the 100/100/10/10 example before and after the deep CO folds.
1. Verify HRC's multiway `sizingAllIn()` and `getStackPotRatio()` choices.
1. Verify minimum raises, incomplete raises, and duplicate removal.
1. Verify forced folds from the maximum active-player setting.
1. Compare the postflop node estimate with the source screenshot.
1. Record observable evidence in `feasibility.md`.

Do not start an expensive calculation without Euan's approval. Save only to a
new disposable filename.
