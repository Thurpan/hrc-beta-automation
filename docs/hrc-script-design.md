# HRC script design and decision record

## Status

This document records the current inputs for the first project HRC tree-building
script. It does not contain an approved or validated script.

The design combines three sources:

- Euan's decisions in this project;
- the current sizing workbook; and
- the [shared HRC-GPT prototype](https://chatgpt.com/share/6a799f62-3738-83eb-9798-a1a36aafd84a).

The shared page and its refreshed 1,926-line candidate were reviewed on 10
August 2026. The final JavaScript block from that review is stored verbatim in
[`shared-chatgpt-prototype.js`](../reference/hrc/shared-chatgpt-prototype.js).
Its SHA-256 is
`f39e83006039b26f27beed4c7f0f8e08d6929cfcf31d3b5deeadd2a448448f37`.
It remains an unapproved reference and has not been loaded into HRC. Re-review
the shared page before relying on these findings if its content changes.

Use [`hrc-scripting.md`](hrc-scripting.md) for the documented HRC API,
execution model, and product limits. The shared prototype is useful working
material, but it is generated advice rather than an official API contract.

No script has been loaded into HRC. No tree or calculation has been created.
All runtime behaviour remains unverified on the licensed host.

## Source boundaries

Apply these rules when sources disagree:

1. Follow Euan's direct decisions for poker policy.
1. Use the workbook as the current numeric sizing input.
1. Use the public HRC API documentation for technical capabilities and legal
   normalisation.
1. Use the shared prototype only as an implementation candidate.

Do not silently replace a workbook value with a value from the prototype. Mark
an apparent workbook error as `TO CONFIRM` until Euan decides the intended
value. For numeric conflicts, this review treats the repository workbook as the
later artefact than the values entered in the shared chat.

## Project artefacts

The stack-size planning artefacts are stored together:

| Artefact | Purpose |
| --- | --- |
| [`Sizes_for_hrc_script.xlsx`](../data/stack-sizes/Sizes_for_hrc_script.xlsx) | Current position, action, and effective-stack sizing matrix. |
| [`stack_size_options.txt`](../data/stack-sizes/stack_size_options.txt) | Generated heads-up, three-player, and five-player simulation run order. |
| [`generate_stack_sizes.py`](../scripts/generate_stack_sizes.py) | Recreates `stack_size_options.txt` from the configured run-order batches. |

The workbook was inspected without changing its cells. It contains one sheet,
one table, and no formulas. Its used range is `A1:S39`.

The workbook has these 18 stack columns, in big blinds:

```text
5, 7.5, 10, 12.5, 15, 17.5, 20, 22.5, 25,
30, 35, 40, 45, 50, 60, 70, 80, 100
```

It contains rules for opens, 3-bets, 4-bets, and 5-bets or later. The 3-bet
section separates blind-versus-blind, BB, SB, and in-position cases.

The generator writes the batches in this order:

1. Heads-up equal-stack setups from 1 to 80 big blinds.
1. Three-player setups from the first four-option priority list.
1. Five-player setups from the first three-option priority list.
1. Three-player setups from the seven-option core list.
1. Five-player setups from the seven-option core list.
1. Three-player setups from the full 18-option list.
1. Five-player setups from the full 18-option list.

Each batch uses its own configured option order to calculate setup priority.
The generator omits an exact setup if an earlier batch already contains it.
The number of hyphen-delimited stacks identifies the player count.

The final three-player and five-player batches use the workbook's 18 stack
values. Therefore, each multiway effective stack matches a workbook column.
The heads-up batch uses its own stack range and is not limited to those columns.

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

## Shared prototype assessment

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

The refreshed candidate reconstructs the opener's project effective stack at
the time of the first raise. It derives the players who had already folded from
the preflop action sequence. It then looks up the opener's expected RFI table
value instead of reading the actual raise amount. This can misclassify an
optional all-in open as the ordinary table open and must be preview-tested.

The candidate applies a 40% preflop all-in replacement threshold. It also uses
a script-owned `-1` sentinel to add all-in at every preflop decision. These
rules can replace or supplement the workbook-selected size and are not yet
approved.

The refreshed RFI, 3-bet, and 4-bet arrays now match the workbook values in
every area previously listed as different, except for `Sheet1!P29`. Matching
numeric transcriptions do not approve the surrounding logic.

## Workbook alignment and remaining conflict

The workbook remains the numeric source of truth. One numeric conflict remains:

| Area | Workbook | Refreshed prototype | Required treatment |
| --- | ---: | ---: | --- |
| `Sheet1!P29`: IP 3-bet versus a `2.25x` open at 60 bb | `75` | `7.5` | `TO CONFIRM`; do not silently correct the workbook. |

## Decisions required before the first script

Confirm these items before treating a generated tree as correct:

| Decision | Current input or prototype behaviour | Status |
| --- | --- | --- |
| Effective-stack basis | Active player capped by the largest non-folded opponent. Recalculate at every decision. | Agreed |
| Supported table sizes | The run order supplies heads-up, three-player, and five-player configurations. The required HRC script scope is not yet confirmed. | TBD |
| Position mapping | Map every position for each supported player count. In a hand configured heads-up, the prototype gives the SB branch priority when BTN is also SB. Folds in a larger hand do not change fixed player indices. | TBD |
| Straddles | The prototype has not been designed or validated for straddles. | TBD |
| Maximum active players | HRC can force folds at this UI limit. A forced fold can change the effective stack and sizing bucket. | TBD |
| Effective stack outside the workbook columns | The prototype maps below 5 bb to 5 bb, rounds intermediate values up, and maps above 100 bb to 100 bb. Multiway configurations use exact column values, but the heads-up run order includes other values. Confirm whether those values use this mapping or must fail. | TBD |
| Opening sizes | The refreshed RFI arrays match the workbook rows. The workbook remains authoritative. | Current workbook; candidate logic unapproved |
| BB isolation after an SB limp | After an SB completion, the refreshed prototype selects the workbook-aligned BB RFI row. | Current workbook; candidate logic unapproved |
| Other limps and isolation raises | The prototype permits only the SB to complete before a raise. It does not permit non-SB limps or overlimps. | TBD |
| SB limp-reraise | After the SB completes and the BB raises, the prototype treats the BB raise as the original open. An SB re-raise uses the blind-versus-blind 3-bet table selected from the reconstructed BB RFI value. | TBD |
| 3-bet sizes | The workbook is the current numeric input. `Sheet1!P29` remains unresolved. | `P29` TO CONFIRM |
| Squeeze sizes | The ordinary 3-bet base uses the project effective stack. For the increment only, the agreed pairwise threshold is `min(squeezer total, first-caller total)`. At 40 bb or more, add `1bb` for one caller or `1.5bb` for two or more; below 40 bb, add `0.5bb` or `1bb`. The first caller is the first call after the original raise, so earlier limps or completions are ignored. An all-in table entry remains all-in. | Agreed squeeze-specific rule; runtime unverified |
| 3-bet table selection by open size | The prototype does not inspect the actual raise amount. It reconstructs the opener's effective stack when the first raise occurred, looks up the corresponding RFI value, and matches exact categories: blind-versus-blind `2.5` or `3`; other branches `2` or `2.1`, `2.25`, or `2.5`. | TBD; optional all-in opens are a known risk |
| 4-bet sizes | The workbook is the current numeric input. | Current workbook |
| 5-bets and later | Euan specified all-in only in the shared chat. The workbook and prototype agree. | Agreed |
| Preflop calls | The prototype permits up to two ordinary flats against an open, one against a 3-bet, one against a 4-bet, and no normal flats against later raises. It disables cold calls and allows the SB completion. Its closing-action exception runs first, so it can permit a call that is cold or above the flat cap. | Two callers versus an open agreed; remaining semantics TBD |
| Preflop all-in additions | The prototype always adds all-in and converts a returned size to all-in when it reaches 40% of the distance from the active chips to HRC's all-in raise size. This can add or replace actions not selected by the workbook. | Current candidate; approval required |
| Postflop sizes | The prototype uses custom HU and multiway pot-fraction arrays by street and by bet, raise, or donk. It applies low-SPR overrides at HU SPR at or below `2.5` and multiway SPR at or below `1.5`, then adds all-in at SPR at or below `5`. | Current candidate; runtime unverified |
| Postflop calls | The prototype omits `canFlatCallPostflop()`. HRC therefore defaults to allowing every postflop flat call, which can materially increase the tree. | TBD |
| Postflop horizon | With `countPlayersLive()` equal to two or three, later-street betting continues through the river; with four, through the turn. Any unlisted count returns `false`. Because HRC excludes all-in players from this count, a multiway pot can select the HU branch. | TBD |
| Postflop abstractions | HRC selects these in the UI. Required flop, turn, and river bucket counts are not set. | TBD |
| All-in size in a multiway state | Uses HRC's `sizingAllIn()` after the project metric selects the rule. HRC's opponent choice is undocumented. If it differs from the project convention, HRC can clamp the requested size and the policy might not be representable exactly. | TO CONFIRM in HRC |
| Parser and legal normalisation | The prototype builds fixed `bb` strings for opens, 3-bets, and squeezes and uses `x` strings for 4-bets. Confirm parsing, minimum raises, all-in clamping, and duplicate normalisation. Its de-duplication runs before HRC legal normalisation. | TO CONFIRM in HRC |

The effective-stack convention is not pending. It applies regardless of the
choices in this table.

## Implementation boundaries

The first implementation must keep these concepts separate:

- derive the active player's project effective stack;
- map that value to an agreed workbook column;
- classify the current action as an open, isolation raise, 3-bet, squeeze,
  4-bet, or later raise;
- select the position and prior-open category;
- return the one configured size set for that node; and
- let HRC enforce minimum raises and its legal all-in cap.

Do not use HRC's legal normalisation to conceal a missing or incorrect rule.
Inspect the tree preview for the returned and normalised sizes.

Tree size is a material risk. The candidate combines an unconditional preflop
all-in option, multiple postflop sizes, low-SPR alternatives, an SPR `5`
postflop all-in, and unrestricted postflop flat calls. Estimate the tree before
starting any calculation.

## Validation cases

Validate the effective-stack helper independently before validating poker
sizes:

1. Test every active player in the 100/100/10/10 example.
1. Set CO as folded. Verify that BTN changes from 100 bb to 10 bb.
1. Set a shallow opponent as folded. Verify that the largest non-folded
   opponent still controls the cap.
1. Mark an opponent all-in without folding. Verify that the opponent remains
   eligible.
1. Give the active player the shortest stack. Verify that the active player's
   own total caps the result.
1. Test the guard for a state with no non-folded opponent.

Validate the rule lookup separately:

1. Test a value below, on, and above every workbook boundary.
1. Test every opening position.
1. Test ordinary 3-bets and squeezes separately.
1. Reconstruct an opener's effective stack before and after later players fold.
   Verify the historical value remains the value at the original raise.
1. Select an optional all-in open and verify that the next node does not
   misclassify it as the ordinary RFI table size.
1. Test one- and two-caller squeeze increments immediately below, at, and above
   the 40 bb pairwise threshold. Verify that an earlier limp or completion is
   ignored when identifying the first caller.
1. Test blind-versus-blind, in-position, and out-of-position 4-bets.
1. Compare representative returned sizes with their exact workbook cells.
1. Verify `Sheet1!P29` only after Euan confirms its intended value.
1. Verify two ordinary flats against an open and one against a 3-bet or 4-bet.
   Test the closing-action exception separately because it can override the
   cold-call check and flat cap.
1. Test the always-added preflop all-in option, the 40% replacement boundary,
   and de-duplication after replacement.
1. Test every HU and multiway postflop bet, raise, and donk matrix by street.
   Test the exact HU SPR `2.5`, multiway SPR `1.5`, and all-in SPR `5`
   boundaries.
1. Test postflop live-player counts of two, three, four, and an unlisted count.
   Include a multiway state with an all-in player.
1. Inspect a branch where the maximum active-player limit forces a deep player
   to fold. Verify the resulting effective stack and sizing bucket.

Finally, load the reviewed script into a new disposable HRC setup on
`EM-3960X`. Inspect the tree estimate and preview. Do not start an expensive
calculation without Euan's approval.
