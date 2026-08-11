/*
 * Project-owned HRC heads-up tree-building candidate.
 *
 * Preflop sizes come from the HU tab in Sizes_for_hrc_script.xlsx. This file
 * has not been validated in HRC. Load it only in a two-player, non-straddled
 * configuration. The script uses the SB and BB helpers and does not assume a
 * particular numeric button index.
 *
 * HU action meanings:
 * - RFI / SB: the initial SB/button open;
 * - RFI / BB: the BB raise after an SB completion;
 * - 3bet / BB: the BB 3-bet versus an SB open;
 * - 3bet / SB: the SB limp-reraise versus a BB raise;
 * - 4bet / SB and BB: the corresponding alternating 4-bet lines; and
 * - 5-bet and later: all-in only.
 *
 * SB completion is unavailable at an effective stack of 5bb or less.
 */


const HU_STACK_GRID = [
    1, 1.5, 2, 2.5, 3, 3.5, 4, 4.5, 5, 5.5, 6, 6.5, 7, 7.5,
    8, 8.5, 9, 9.5, 10, 10.5, 11, 11.5, 12, 12.5, 13, 13.5, 14,
    14.5, 15, 15.5, 16, 16.5, 17, 17.5, 18, 18.5, 19, 19.5, 20,
    20.5, 21, 21.5, 22, 22.5, 23, 23.5, 24, 24.5, 25, 26, 27, 28,
    29, 30, 32.5, 35, 37.5, 40, 42.5, 45, 47.5, 50, 55, 60, 65,
    70, 75, 80
];


const HU_RFI_SB = [
    "allin", "allin", "allin", "allin", "allin", "allin", "allin",
    "allin", "allin", "allin", "allin", "allin", "allin", "allin",
    "2", "2", "2", "2", "2", "2", "2", "2", "2", "2", "2",
    "2", "2", "2", "2", "2", "2", "2", "2", "2", "2", "2",
    "2", "2", "2", "2", "2", "2", "2", "2", "2", "2", "2",
    "2", "2", "2", "2", "2", "2", "2", "2", "2", "2", "2.25",
    "2.25", "2.25", "2.25", "2.25", "2.25", "2.5", "2.5", "2.5",
    "2.5", "2.5"
];


const HU_RFI_BB = [
    "allin", "allin", "allin", "allin", "allin", "allin", "1.93",
    "1.97", "2", "2.03", "2.07", "2.1", "2.13,3.4", "2.17,3.5",
    "2.2,3.6", "2.23,3.7", "2.27,3.8", "2.3,3.9", "2.33,4",
    "2.37,4.1", "2.4,4.2", "2.43,4.3", "2.47,4.4", "2.5,4.5",
    "2.53,4.6", "2.57,4.7", "2.6,4.8", "2.63,4.9", "2.67,5",
    "2.7,5.1", "2.73,5.2", "2.77,5.3", "2.8,5.4", "2.83,5.5",
    "2.87,5.6", "2.9,5.7", "2.93,5.8", "2.97,5.9", "3,6", "3,6.1",
    "3,6.2", "3,6.3", "3,6.4", "3,6.5", "3,6.6", "3,6.7", "3,6.8",
    "3,6.9", "3,7", "3,7", "3,7", "3,7", "3,7", "3,7", "3.5,7.5",
    "3.5,8", "4,9", "4,10", "4.5", "4.5", "4.5", "4.5", "5", "5",
    "5", "5", "5", "5"
];


const HU_THREEBET_SB = [
    "allin", "allin", "allin", "allin", "allin", "allin", "allin",
    "allin", "allin", "allin", "allin", "allin", "allin", "allin",
    "allin", "allin", "allin", "allin", "allin", "allin", "allin",
    "allin", "allin", "allin", "allin", "allin", "allin", "allin",
    "allin", "allin", "allin", "allin", "allin", "allin", "allin",
    "allin", "allin", "allin", "7.5", "7.5", "7.5", "7.5", "7.5",
    "7.5", "7.5", "7.5", "7.5", "7.5", "7.5", "7.5", "7.5", "7.5",
    "7.5", "7.5", "8.5", "8.5", "10", "10", "10.5", "11", "11.5",
    "12", "12.5", "13", "13.5", "14", "14.5", "15"
];


const HU_THREEBET_BB = [
    "allin", "allin", "allin", "allin", "allin", "allin", "allin",
    "allin", "allin", "allin", "allin", "allin", "allin", "allin",
    "3.8", "3.85,4.13", "3.9,4.25", "3.95,4.38", "4,4.5",
    "4.05,4.63", "4.1,4.75", "4.15,4.88", "4.2,5", "4.25,5.13",
    "4.3,5.25", "4.35,5.38", "4.4,5.5", "4.45,5.63", "4.5,5.75",
    "4.55,5.88", "4.6,6", "4.65,6.13", "4.7,6.25", "4.75,6.38",
    "4.8,6.5", "4.85,6.63", "4.9,6.75", "4.95,6.88", "5,7",
    "5.05,7.13", "5.1,7.25", "5.15,7.38", "5.2,7.5", "5.25,7.63",
    "5.3,7.75", "5.35,7.88", "5.4,8", "5.45,8.13", "5.5,8.25",
    "5.6,8.5", "5.7,8.75", "5.8,9", "5.9,9.25", "6,9.5", "6,10",
    "6,10", "6,10", "8", "8", "8", "8", "8", "8", "9", "9", "9",
    "9", "10"
];


const HU_FOURBET_SB = [
    "allin", "allin", "allin", "allin", "allin", "allin", "allin",
    "allin", "allin", "allin", "allin", "allin", "allin", "allin",
    "allin", "allin", "allin", "allin", "allin", "allin", "allin",
    "allin", "allin", "allin", "allin", "allin", "allin", "allin",
    "allin", "allin", "allin", "allin", "allin", "allin", "allin",
    "allin", "allin", "allin", "allin", "allin", "allin", "allin",
    "allin", "allin", "allin", "allin", "allin", "allin", "allin",
    "allin", "allin", "allin", "allin", "allin", "allin", "allin",
    "allin", "allin", "allin", "allin", "allin", "18", "18", "20",
    "20", "20", "20", "22.5"
];


const HU_FOURBET_BB = [
    "allin", "allin", "allin", "allin", "allin", "allin", "allin",
    "allin", "allin", "allin", "allin", "allin", "allin", "allin",
    "allin", "allin", "allin", "allin", "allin", "allin", "allin",
    "allin", "allin", "allin", "allin", "allin", "allin", "allin",
    "allin", "allin", "allin", "allin", "allin", "allin", "allin",
    "allin", "allin", "allin", "allin", "allin", "allin", "allin",
    "allin", "allin", "allin", "allin", "allin", "allin", "allin",
    "allin", "allin", "allin", "allin", "allin", "allin", "allin",
    "allin", "allin", "allin", "allin", "allin", "allin", "allin",
    "allin", "allin", "28", "29", "30"
];


const HU_PREFLOP_SIZING_TABLES = [
    {name: "HU_RFI_SB", values: HU_RFI_SB},
    {name: "HU_RFI_BB", values: HU_RFI_BB},
    {name: "HU_THREEBET_SB", values: HU_THREEBET_SB},
    {name: "HU_THREEBET_BB", values: HU_THREEBET_BB},
    {name: "HU_FOURBET_SB", values: HU_FOURBET_SB},
    {name: "HU_FOURBET_BB", values: HU_FOURBET_BB}
];


const HU_SUPPORTED_PLAYER_COUNT = 2;
const PREFLOP_ALLIN_THRESHOLD = 0.50;
const PREFLOP_ADD_ALLIN_SPR = -1;
const HU_ALLOWED_FLATS_PER_RAISE = {2: 1, 3: 1, 4: 1};
const PREFLOP_SB_COMPLETION_CUTOFF_BB = 5;


const POSTFLOP_HU_FLOP_BET = [0.25, 0.40, 0.67, 1.00];
const POSTFLOP_HU_FLOP_RAISE = [0.33, 0.75];
const POSTFLOP_HU_FLOP_DONK = [0.33, 0.75];
const POSTFLOP_HU_TURN_BET = [0.40, 0.80, 1.60];
const POSTFLOP_HU_TURN_RAISE = [0.40, 0.80, 1.60];
const POSTFLOP_HU_TURN_DONK = [0.33, 0.75];
const POSTFLOP_HU_RIVER_BET = [0.40, 0.80, 1.60, 3.20];
const POSTFLOP_HU_RIVER_RAISE = [0.50, 1.00, 2.50];
const POSTFLOP_HU_RIVER_DONK = [0.40, 0.80, 1.60, 3.20];
const POSTFLOP_LOW_SPR_HU = 2.5;
const POSTFLOP_LOW_SPR_HU_BET = [0.10, 0.25, 0.40, 0.67, 1.00, 1.50];
const POSTFLOP_LOW_SPR_HU_RAISE = [0.10, 0.25, 0.40, 0.67, 1.00];
const POSTFLOP_ADD_ALLIN_SPR = 5.0;
let POSTFLOP_BETS_PER_STREET = null;
const POSTFLOP_ALLOW_DONK = false;
const POSTFLOP_ALLOW_DONK_PREV_AGGRESSION = true;


function assertSupportedConfiguration(ctx) {
    if (ctx.getNumberOfPlayers() != HU_SUPPORTED_PLAYER_COUNT) {
        throw new Error(
            "This HRC sizing candidate supports heads-up configurations only."
        );
    }

    if (ctx.getPlayerIndexSmallBlind() == ctx.getPlayerIndexBigBlind()) {
        throw new Error("The HU small blind and big blind must be different players.");
    }
}


function parseFixedBbCell(cell) {
    if (cell == "allin")
        return null;

    let tokens = String(cell).split(",");
    if (tokens.length < 1 || tokens.length > 2) {
        throw new Error("Invalid HU sizing cell: " + cell);
    }

    let values = [];
    for (let token of tokens) {
        let value = Number(token);
        if (!isFinite(value) || value <= 0) {
            throw new Error("Invalid HU sizing value: " + cell);
        }
        values.push(value);
    }
    return values;
}


function assertHuTables() {
    for (let table of HU_PREFLOP_SIZING_TABLES) {
        if (table.values.length != HU_STACK_GRID.length) {
            throw new Error(
                table.name + " must contain one value for each HU stack bucket."
            );
        }

        for (let cell of table.values)
            parseFixedBbCell(cell);
    }
}


assertHuTables();


function normalizeAndUniqueSizings(ctx, sizings) {
    let minimum = ctx.sizingMinimum();
    let allin = ctx.sizingAllIn();
    let result = [];

    for (let sizing of sizings) {
        if (typeof sizing != "number" || !isFinite(sizing)) {
            throw new Error("A sizing callback produced a non-finite amount.");
        }
        if (sizing < 0)
            continue;

        let normalized = Math.min(Math.max(sizing, minimum), allin);
        if (result.indexOf(normalized) < 0)
            result.push(normalized);
    }

    return result;
}


function getPlayerStartingStack(ctx, player) {
    let state = ctx.getPotState();
    return state.getChipsActive(player) +
        state.getChipsDead(player) +
        state.getChipsRemaining(player);
}


// Convert a stack or threshold, not an action size, to HRC amount units.
function amountFromBigBlinds(ctx, amount) {
    return Number(ctx.getSizeBigBlind()) * amount;
}


function getEffectiveStackForPlayer(ctx, player) {
    let state = ctx.getPotState();
    let playerStack = getPlayerStartingStack(ctx, player);
    let opponentStack = null;

    for (let p = 0; p < ctx.getNumberOfPlayers(); p++) {
        if (p == player || state.hasPlayerFolded(p))
            continue;
        opponentStack = getPlayerStartingStack(ctx, p);
        break;
    }

    if (opponentStack == null) {
        throw new Error("HU effective stack requested without an opponent.");
    }

    return Math.min(playerStack, opponentStack);
}


function getStackBucketIndex(ctx, effectiveStack) {
    for (let i = 0; i < HU_STACK_GRID.length; i++) {
        // sizingBigBlinds() calculates an action for the current node. It is
        // not a raw big-blind-to-amount conversion.
        let stackValue = amountFromBigBlinds(ctx, HU_STACK_GRID[i]);
        if (effectiveStack == stackValue)
            return i;
    }

    throw new Error(
        "HU effective stack does not match a configured workbook column: " +
        effectiveStack
    );
}


function getCurrentStackBucketIndex(ctx) {
    return getStackBucketIndex(
        ctx,
        getEffectiveStackForPlayer(ctx, ctx.getActivePlayer())
    );
}


function resolveFixedBbCell(ctx, cell) {
    if (cell == "allin")
        return [ctx.sizingAllIn()];

    let values = parseFixedBbCell(cell);
    let sizings = [];
    for (let value of values)
        sizings.push(ctx.sizingBigBlinds(value));
    return sizings;
}


function getSizingsFromTable(ctx, table) {
    if (table == null)
        throw new Error("A HU sizing table is required.");

    return resolveFixedBbCell(
        ctx,
        table[getCurrentStackBucketIndex(ctx)]
    );
}


function getPreflopActions(ctx) {
    return Array.from(ctx.getActionSequence(PREFLOP));
}


function getFirstRaiseInfo(ctx) {
    let actions = getPreflopActions(ctx);
    let smallBlindCompleted = false;

    for (let action of actions) {
        let actionType = action.getActionType();
        let player = action.getPlayer();

        if (actionType == FOLD)
            continue;

        if (
            actionType == CALL &&
            player == ctx.getPlayerIndexSmallBlind()
        ) {
            smallBlindCompleted = true;
            continue;
        }

        if (actionType == RAISE) {
            return {
                player: player,
                smallBlindCompleted: smallBlindCompleted
            };
        }
    }

    return null;
}


function hasSmallBlindCompletionBeforeRaise(ctx) {
    let actions = getPreflopActions(ctx);
    for (let action of actions) {
        if (action.getActionType() == RAISE)
            return false;
        if (
            action.getActionType() == CALL &&
            action.getPlayer() == ctx.getPlayerIndexSmallBlind()
        ) {
            return true;
        }
    }
    return false;
}


function getSizingsOpening(ctx) {
    let player = ctx.getActivePlayer();

    if (player == ctx.getPlayerIndexSmallBlind())
        return getSizingsFromTable(ctx, HU_RFI_SB);

    if (player == ctx.getPlayerIndexBigBlind()) {
        if (!hasSmallBlindCompletionBeforeRaise(ctx)) {
            throw new Error("A HU BB opening size requires an SB completion.");
        }
        return getSizingsFromTable(ctx, HU_RFI_BB);
    }

    throw new Error("A HU opening decision has an invalid active player.");
}


function getSizings3Bets(ctx) {
    let firstRaise = getFirstRaiseInfo(ctx);
    if (firstRaise == null)
        throw new Error("A HU 3-bet decision has no original raise.");

    let player = ctx.getActivePlayer();
    let smallBlind = ctx.getPlayerIndexSmallBlind();
    let bigBlind = ctx.getPlayerIndexBigBlind();

    if (
        player == bigBlind &&
        firstRaise.player == smallBlind &&
        !firstRaise.smallBlindCompleted
    ) {
        return getSizingsFromTable(ctx, HU_THREEBET_BB);
    }

    if (
        player == smallBlind &&
        firstRaise.player == bigBlind &&
        firstRaise.smallBlindCompleted
    ) {
        return getSizingsFromTable(ctx, HU_THREEBET_SB);
    }

    throw new Error("A HU 3-bet decision has an invalid action line.");
}


function getSizings4Bets(ctx) {
    let firstRaise = getFirstRaiseInfo(ctx);
    if (firstRaise == null)
        throw new Error("A HU 4-bet decision has no original raise.");

    let actions = getPreflopActions(ctx);
    let lastRaise = null;
    for (let action of actions) {
        if (action.getActionType() == RAISE)
            lastRaise = action;
    }

    if (lastRaise == null || lastRaise.getPlayer() == ctx.getActivePlayer()) {
        throw new Error("A HU 4-bet decision has an invalid previous raise.");
    }

    let player = ctx.getActivePlayer();
    let smallBlind = ctx.getPlayerIndexSmallBlind();
    let bigBlind = ctx.getPlayerIndexBigBlind();

    if (
        player == smallBlind &&
        firstRaise.player == smallBlind &&
        !firstRaise.smallBlindCompleted
    ) {
        return getSizingsFromTable(ctx, HU_FOURBET_SB);
    }

    if (
        player == bigBlind &&
        firstRaise.player == bigBlind &&
        firstRaise.smallBlindCompleted
    ) {
        return getSizingsFromTable(ctx, HU_FOURBET_BB);
    }

    throw new Error("A HU 4-bet decision has an invalid action line.");
}


function applyAllinThreshold(ctx, sizings) {
    let allin = ctx.sizingAllIn();
    let active = ctx.getPotState().getChipsActive(ctx.getActivePlayer());
    let threshold = active + (allin - active) * PREFLOP_ALLIN_THRESHOLD;

    return sizings.map(
        sizing => sizing >= threshold ? allin : sizing
    );
}


function getSizingsPreflop(ctx) {
    assertSupportedConfiguration(ctx);

    let raiseNumber = 1 + ctx.getBetCount();
    let sizings;

    if (raiseNumber == 2)
        sizings = getSizingsOpening(ctx);
    else if (raiseNumber == 3)
        sizings = getSizings3Bets(ctx);
    else if (raiseNumber == 4)
        sizings = getSizings4Bets(ctx);
    else
        sizings = [ctx.sizingAllIn()];

    if (
        PREFLOP_ADD_ALLIN_SPR < 0 ||
        ctx.getStackPotRatio() <= PREFLOP_ADD_ALLIN_SPR
    ) {
        sizings.push(ctx.sizingAllIn());
    }

    return normalizeAndUniqueSizings(
        ctx,
        applyAllinThreshold(ctx, sizings)
    );
}


function isColdCall(ctx) {
    if (ctx.getBetCount() <= 2)
        return false;

    for (let action of getPreflopActions(ctx)) {
        if (
            action.getPlayer() == ctx.getActivePlayer() &&
            (
                action.getActionType() == CALL ||
                action.getActionType() == RAISE
            )
        ) {
            return false;
        }
    }

    return true;
}


function canFlatCallPreflop(ctx) {
    assertSupportedConfiguration(ctx);

    let bets = ctx.getBetCount();
    if (bets == 1) {
        let player = ctx.getActivePlayer();
        return (
            player == ctx.getPlayerIndexSmallBlind() &&
            getEffectiveStackForPlayer(ctx, player) >
                amountFromBigBlinds(
                    ctx,
                    PREFLOP_SB_COMPLETION_CUTOFF_BB
                )
        );
    }

    if (isColdCall(ctx))
        return false;

    let cap = HU_ALLOWED_FLATS_PER_RAISE[bets];
    if (cap != undefined)
        return ctx.getFlatCallCount() < cap;

    return (
        bets >= 5 &&
        ctx.getFlatCallCount() == 0 &&
        ctx.isClosingAction()
    );
}


function getNormalPostflopSizes(street, facingBet, donk) {
    if (street == FLOP) {
        if (facingBet)
            return POSTFLOP_HU_FLOP_RAISE;
        if (donk)
            return POSTFLOP_HU_FLOP_DONK;
        return POSTFLOP_HU_FLOP_BET;
    }

    if (street == TURN) {
        if (facingBet)
            return POSTFLOP_HU_TURN_RAISE;
        if (donk)
            return POSTFLOP_HU_TURN_DONK;
        return POSTFLOP_HU_TURN_BET;
    }

    if (street == RIVER) {
        if (facingBet)
            return POSTFLOP_HU_RIVER_RAISE;
        if (donk)
            return POSTFLOP_HU_RIVER_DONK;
        return POSTFLOP_HU_RIVER_BET;
    }

    return [];
}


function hasPreviousAggression(ctx, player) {
    return Array.from(ctx.getActionSequenceFull()).findIndex(
        action => (
            action.getPlayer() == player &&
            action.getActionType() == RAISE
        )
    ) >= 0;
}


function getSizingsPostflop(ctx) {
    assertSupportedConfiguration(ctx);

    let state = ctx.getPotState();
    if (state.countPlayersLive() != 2)
        return [];

    let player = ctx.getActivePlayer();
    let facingBet = ctx.getBetCount() > 0;
    let donk = !facingBet && ctx.isDonkBet();

    if (
        donk &&
        !POSTFLOP_ALLOW_DONK &&
        (
            !POSTFLOP_ALLOW_DONK_PREV_AGGRESSION ||
            !hasPreviousAggression(ctx, player)
        )
    ) {
        return [];
    }

    if (
        POSTFLOP_BETS_PER_STREET != null &&
        ctx.getBetCount() >= POSTFLOP_BETS_PER_STREET
    ) {
        return normalizeAndUniqueSizings(ctx, [ctx.sizingAllIn()]);
    }

    let spr = ctx.getStackPotRatio();
    let sizes;

    if (spr <= POSTFLOP_LOW_SPR_HU) {
        sizes = facingBet ?
            POSTFLOP_LOW_SPR_HU_RAISE :
            POSTFLOP_LOW_SPR_HU_BET;
    }
    else {
        sizes = getNormalPostflopSizes(ctx.getStreet(), facingBet, donk);
    }

    let sizings = [];
    for (let size of sizes)
        sizings.push(ctx.sizingPot(size));

    if (spr <= POSTFLOP_ADD_ALLIN_SPR)
        sizings.push(ctx.sizingAllIn());

    return normalizeAndUniqueSizings(ctx, sizings);
}


function canFlatCallPostflop(ctx) {
    assertSupportedConfiguration(ctx);
    return true;
}


function hasNextStreetBetting(ctx) {
    assertSupportedConfiguration(ctx);
    if (ctx.getPotState().countPlayersLive() != 2)
        return false;
    return ctx.getStreet() < RIVER;
}
