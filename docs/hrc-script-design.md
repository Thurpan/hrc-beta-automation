# HRC script design and decision record

## Status

This document records the current inputs for the first project HRC tree-building
script. It does not contain an approved or validated script.

The design combines three sources:

- Euan's decisions in this project;
- the current sizing workbook; and
- the [shared HRC-GPT prototype](https://chatgpt.com/share/6a799f62-3738-83eb-9798-a1a36aafd84a).

The shared page was reviewed on 10 August 2026. The reviewed candidate is
identified by share ID `6a799f62-3738-83eb-9798-a1a36aafd84a`. Re-review the
page before relying on these findings if its content changes.

The final JavaScript block from that review is stored verbatim in
[`shared-chatgpt-prototype.js`](../reference/hrc/shared-chatgpt-prototype.js).
It is a reference snapshot, not an approved script to load into HRC.

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
| [`stack_size_options.txt`](../data/stack-sizes/stack_size_options.txt) | Generated five-player stack combinations for future automation input. |
| [`generate_stack_sizes.py`](../scripts/generate_stack_sizes.py) | Recreates `stack_size_options.txt` from the configured stack options. |

The workbook was inspected without changing its cells. It contains one sheet,
one table, and no formulas. Its used range is `A1:S39`.

The workbook has these 18 stack columns, in big blinds:

```text
5, 7.5, 10, 12.5, 15, 17.5, 20, 22.5, 25,
30, 35, 40, 45, 50, 60, 70, 80, 100
```

It contains rules for opens, 3-bets, 4-bets, and 5-bets or later. The 3-bet
section separates blind-versus-blind, BB, SB, and in-position cases.

The generator uses the same set of 18 stack values. Therefore, a configuration
from `stack_size_options.txt` always produces an effective stack that matches a
workbook column exactly. The generator omits a setup with only one largest
stack. Under the agreed convention, capping that stack at the next-largest
stack produces the same effective stacks for every active player.

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

The prototype also removes two numeric all-in rules inherited from HRC's
default example. The workbook can therefore determine the intended preflop
action instead of the default `0.37` replacement threshold or the default
preflop SPR threshold of `7`.

These strengths do not make the prototype ready to load. Several numeric rules
conflict with the current workbook, and several inherited rules have not been
approved.

## Workbook and prototype differences

The current workbook differs from values transcribed in the shared prototype.
Resolve the following differences in favour of the workbook unless Euan
changes the workbook or gives a later decision.

Ranges in this table refer only to consecutive listed workbook columns. They
do not define behaviour for an effective stack between those columns.

| Area | Current workbook | Shared prototype | Required treatment |
| --- | --- | --- | --- |
| SB open at 7.5 bb | All-in. | `2.5bb`, because only stacks at or below 7 bb are all-in. | Use the workbook. |
| BB open or isolation | All-in at 5 and 7.5 bb; `2.5bb` from 10 to 17.5 bb; `3bb` from 20 bb. | A fixed `3.5bb` branch after an SB completion. | Separate the intended BB rule from limp-reraise handling. |
| BTN open at 30 and 35 bb | `2.1bb`. | `2.25bb`. | Use the workbook. |
| BTN open at 40 and 45 bb | `2.25bb`. | `2.25bb`. | Values agree. |
| UTG through CO open at 50 bb | `2.1bb`. | `2.25bb`. | Use the workbook. |
| UTG through CO open from 60 bb | `2.25bb`. | `2.25bb`. | Values agree. |
| Blind-versus-blind 4-bet | All-in through 30 bb; `2x` at 35 to 45 bb; `2.1x` at 50 bb; `2.2x` at 60 bb; `2.25x` at 70 bb; `2.5x` at 80 and 100 bb. | A simplified stack band and IP or OOP rule. | Use the workbook tiers. |
| In-position 4-bet | All-in through 30 bb; `2x` at 35 to 45 bb; `2.1x` from 50 bb. | All-in below 50 bb; `2x` below 70 bb; `2.1x` from 70 bb. | Use the workbook tiers. |
| Out-of-position 4-bet | All-in through 30 bb; `2.1x` at 35 bb; `2.25x` at 40 to 60 bb; `2.5x` from 70 bb. | All-in below 50 bb; `2.25x` below 70 bb; `2.5x` from 70 bb. | Use the workbook tiers. |

`Sheet1!P29` contains `75` for the in-position 3-bet row against a `2.25x`
open at 60 bb. The shared prototype uses `7.5`, which is consistent with the
nearby scale. This cell is `TO CONFIRM`. Do not change or implement it as
`7.5` without Euan's approval.

## Decisions required before the first script

Confirm these items before treating a generated tree as correct:

| Decision | Current input or prototype behaviour | Status |
| --- | --- | --- |
| Effective-stack basis | Active player capped by the largest non-folded opponent. Recalculate at every decision. | Agreed |
| Supported table sizes | The generator supplies five-player configurations. The required script scope is not yet confirmed. | TBD |
| Position mapping | Map every position for each supported player count. In a hand configured heads-up, the prototype gives the SB branch priority when BTN is also SB. Folds in a larger hand do not change fixed player indices. | TBD |
| Straddles | The prototype has not been designed or validated for straddles. | TBD |
| Maximum active players | HRC can force folds at this UI limit. A forced fold can change the effective stack and sizing bucket. | TBD |
| Effective stack outside the workbook columns | The prototype maps below 5 bb to 5 bb, rounds intermediate values up, and maps above 100 bb to 100 bb. Generated project configurations use exact column values. Decide whether any other value must fail or use a fallback. | TBD |
| Opening sizes | The workbook is the current numeric input. The shared prototype differs at several boundaries. | Current workbook |
| BB isolation after an SB limp | Use the workbook BB row. The prototype instead uses a fixed `3.5bb`. | Current workbook |
| Other limps and isolation raises | The workbook has no generic non-blind OOP limp-reraise category. Decide whether the first script permits non-SB limps or overlimps and define adjustments if it does. | TBD |
| SB limp-reraise | The prototype permits this action and uses the inherited `9.2bb + 1.0x` expression. Decide whether to allow it and which size applies. | TBD |
| 3-bet sizes | The workbook is the current numeric input. `Sheet1!P29` remains unresolved. | `P29` TO CONFIRM |
| Squeeze sizes | Uses the ordinary 3-bet table without a caller adjustment. | TBD |
| 3-bet table selection by open size | The prototype compares the absolute open raise-to size in bb. BB versus SB uses the `2.5bb` table through `2.75bb`, then the `3bb` table. Other branches use the `2bb` table through `2.1bb`, the `2.25bb` table through `2.25bb`, then the `2.5bb` table. | TBD |
| 4-bet sizes | The workbook is the current numeric input. | Current workbook |
| 5-bets and later | Euan specified all-in only in the shared chat. The workbook and prototype agree. | Agreed |
| Preflop calls | The prototype allows one ordinary flat against opens, 3-bets, and 4-bets; disables cold calls; and allows the SB limp. Its closing-action exception runs first, so it permits a closing call even when cold or above the flat cap. | TBD |
| Preflop all-in additions | The prototype removes the default replacement threshold and preflop SPR all-in rule so the workbook controls the action. | Current candidate |
| Postflop sizes | Retains the official default geometric hints, flop `33%`, SPR `5` all-in, donk, and checkdown rules. | TBD |
| Postflop calls | The prototype omits `canFlatCallPostflop()`. HRC therefore defaults to allowing every postflop flat call, which can materially increase the tree. | TBD |
| Postflop horizon | Retains the default last-betting-street rule by `countPlayersLive()`. | TBD |
| Postflop abstractions | HRC selects these in the UI. Required flop, turn, and river bucket counts are not set. | TBD |
| All-in size in a multiway state | Uses HRC's `sizingAllIn()` after the project metric selects the rule. HRC's opponent choice is undocumented. If it differs from the project convention, HRC can clamp the requested size and the policy might not be representable exactly. | TO CONFIRM in HRC |
| Parser and legal normalisation | If the inherited SB limp-reraise branch remains, confirm its mixed-unit expression. Confirm minimum-raise, all-in clamp, and duplicate-size behaviour for all rules. | TO CONFIRM in HRC |

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
1. Test blind-versus-blind, in-position, and out-of-position 4-bets.
1. Compare representative returned sizes with their exact workbook cells.
1. Verify `Sheet1!P29` only after Euan confirms its intended value.
1. Inspect a branch where the maximum active-player limit forces a deep player
   to fold. Verify the resulting effective stack and sizing bucket.

Finally, load the reviewed script into a new disposable HRC setup on
`EM-3960X`. Inspect the tree estimate and preview. Do not start an expensive
calculation without Euan's approval.
