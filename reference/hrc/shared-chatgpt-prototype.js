/*
 * Stack-dependent preflop sizing script.
 *
 * Based on HRC default_example.js.
 *
 * Effective stack:
 *   min(
 *       active player's starting stack,
 *       deepest starting stack among opponents who have not folded
 *   )
 *
 * Starting stack:
 *   chipsActive + chipsDead + chipsRemaining
 *
 * Postflop behaviour is unchanged from the default script.
 */


// =====================================================================
// Start of Preflop configuration
// =====================================================================


// ---------------------------------------------------------------------
// 3-bet effective-stack columns, in big blinds
// ---------------------------------------------------------------------

let THREE_BET_STACKS = [
	5, 7.5, 10, 12.5, 15, 17.5, 20, 22.5, 25,
	30, 35, 40, 45, 50, 60, 70, 80, 100
];


// ---------------------------------------------------------------------
// BB vs SB open
// ---------------------------------------------------------------------

let THREE_BET_BB_VS_SB_2_5 = [
	"allin", "allin", "allin", "allin",
	5, 5.5, 6, 6, 6.5,
	6.5, 6.5, 7, 7.5, 8, 8.5, 8.5, 8.5, 8.5
];

let THREE_BET_BB_VS_SB_3_0 = [
	"allin", "allin", "allin", "allin",
	6, 6.5, 7, 7, 7.5,
	7.5, 7.5, 8, 8, 8.5, 9, 9, 9, 9
];


// ---------------------------------------------------------------------
// BB vs non-SB open
// ---------------------------------------------------------------------

let THREE_BET_BB_VS_2_0_2_1 = [
	"allin", "allin", "allin", "allin", "allin",
	5.5, 5.5, 6, 6.5,
	7, 7.5, 8, 8.5, 9, 10, 11, 11.5, 12
];

let THREE_BET_BB_VS_2_25 = [
	"allin", "allin", "allin", "allin", "allin",
	6, 6, 6, 6.5,
	7, 8, 8.5, 9, 9.5, 10.5, 11.5, 12, 12.5
];

let THREE_BET_BB_VS_2_5 = [
	"allin", "allin", "allin", "allin", "allin",
	6, 6, 6.5, 7,
	7.5, 8, 8.5, 9, 10, 11, 12, 12.5, 13
];


// ---------------------------------------------------------------------
// SB 3-bets
// ---------------------------------------------------------------------

let THREE_BET_SB_VS_2_0_2_1 = [
	"allin", "allin", "allin", "allin", "allin",
	5, 5, 5.5, 5.5,
	6, 6.5, 7.5, 8, 8.5, 9, 9.5, 10, 10
];

let THREE_BET_SB_VS_2_25 = [
	"allin", "allin", "allin", "allin", "allin",
	5, 5, 5.5, 6,
	6.5, 7, 8, 8.5, 9, 9.5, 10, 10.5, 11
];

let THREE_BET_SB_VS_2_5 = [
	"allin", "allin", "allin", "allin", "allin",
	5.5, 5.5, 6, 6.5,
	7, 7.5, 8.5, 9, 9.5, 10, 10.5, 11, 11.5
];


// ---------------------------------------------------------------------
// IP 3-bets
// ---------------------------------------------------------------------

let THREE_BET_IP_VS_2_0_2_1 = [
	"allin", "allin", "allin", "allin",
	4.5, 5, 5, 5, 5,
	5.5, 6, 6, 6, 6.5, 7, 7.5, 7.5, 7.5
];

let THREE_BET_IP_VS_2_25 = [
	"allin", "allin", "allin", "allin",
	5, 5, 5, 5, 5.5,
	5.5, 6, 6.5, 6.5, 7, 7.5, 7.5, 8, 8
];

let THREE_BET_IP_VS_2_5 = [
	"allin", "allin", "allin", "allin",
	5, 5.5, 5.5, 5.5, 6,
	6, 6.5, 7, 7, 7.5, 7.5, 8, 8, 8
];


// ---------------------------------------------------------------------
// Flatting rules - unchanged from default script
// ---------------------------------------------------------------------

let ALLOWED_FLATS_PER_RAISE = {
	2: 1, //opens: 1 flat
	3: 1, //3-bets: 1 flat
	4: 1, //4-bets: 1 flat
	5: 0,
	6: 0
};

let ALLOW_COLD_CALLS = false;
let ALLOW_FLATS_CLOSING_ACTION = true;


// =====================================================================
// Start of Postflop configuration
// =====================================================================


//Primary geometric betting hint
let POSTFLOP_PRIMARY_HINT_HEADS_UP = [0.5, 0.75];
let POSTFLOP_PRIMARY_HINT_MULTIWAY = [0.6, 0.8];

//Additional options for flop sizings
let POSTFLOP_ADD_FLOP_BET_POT = [0.33];
let POSTFLOP_ADD_FLOP_CBET_POT = [];

//Some additional Postflop Settings
let POSTFLOP_ADD_ALLIN_SPR = 5;
let POSTFLOP_ALLOW_DONK = false;
let POSTFLOP_ALLOW_DONK_PREV_AGGRESSION = true;

let POSTFLOP_FORCE_CHECKDOWN_AFTER = {
	2: RIVER,
	3: RIVER,
	4: TURN,
	5: FLOP
};


// =====================================================================
// EFFECTIVE STACK HELPERS
// =====================================================================


//Returns a player's starting stack in HRC chip units.
function getPlayerStartingStack(ctx, player) {
	let state = ctx.getPotState();

	return state.getChipsActive(player) +
		state.getChipsDead(player) +
		state.getChipsRemaining(player);
}


//Returns the current player's effective stack against the deepest
//opponent who has not folded.
function getEffectiveStack(ctx) {
	let state = ctx.getPotState();
	let player = ctx.getActivePlayer();

	let playerStack = getPlayerStartingStack(ctx, player);
	let deepestOpponentStack = 0;
	let opponentFound = false;

	for (let p = 0; p < ctx.getNumberOfPlayers(); p++) {
		if (p == player)
			continue;

		if (state.hasPlayerFolded(p))
			continue;

		let opponentStack = getPlayerStartingStack(ctx, p);

		if (!opponentFound || opponentStack > deepestOpponentStack) {
			deepestOpponentStack = opponentStack;
			opponentFound = true;
		}
	}

	if (!opponentFound)
		return playerStack;

	return Math.min(playerStack, deepestOpponentStack);
}


//Returns the appropriate Excel column.
//
// Example:
// 18bb effective -> 20bb column
// 28bb effective -> 30bb column
// 100bb+          -> 100bb column
function getThreeBetStackColumn(ctx) {
	let effectiveStack = getEffectiveStack(ctx);

	for (let i = 0; i < THREE_BET_STACKS.length; i++) {
		let stackLimit = ctx.sizingBigBlinds(THREE_BET_STACKS[i]);

		if (effectiveStack <= stackLimit)
			return i;
	}

	return THREE_BET_STACKS.length - 1;
}


function sizingFromThreeBetTable(ctx, table) {
	let column = getThreeBetStackColumn(ctx);
	let size = table[column];

	if (size == "allin")
		return [ctx.sizingAllIn()];

	let regularSize = ctx.sizingBigBlinds(size);
	let allinSize = ctx.sizingAllIn();

	if (regularSize >= allinSize)
		return [allinSize];

	return [regularSize];
}


function capSizingAtAllIn(ctx, sizing) {
	let allinSize = ctx.sizingAllIn();

	if (sizing >= allinSize)
		return allinSize;

	return sizing;
}


// =====================================================================
// PREFLOP SIZINGS
// =====================================================================


function getSizingsPreflop(ctx) {
	let bets = 1 + ctx.getBetCount();

	switch (bets) {
		case 2: //open raise
			return getSizingsOpening(ctx);

		case 3: //3-bet
			return getSizings3Bets(ctx);

		case 4: //4-bet
			return getSizings4Bets(ctx);

		case 5: //5-bet
			return getSizings5Bets(ctx);

		default: //6-bets+
			return [ctx.sizingAllIn()];
	}
}


// ---------------------------------------------------------------------
// OPEN RAISES
// ---------------------------------------------------------------------

function getSizingsOpening(ctx) {
	let player = ctx.getActivePlayer();
	let effectiveStack = getEffectiveStack(ctx);


	// -------------------------------------------------------------
	// SB
	//
	// <= 7bb   = all-in
	// >7-<26   = 2.5bb
	// >=26bb   = 3bb
	//
	// 23-26bb uses 2.5bb to fill the gap in the supplied ranges.
	// -------------------------------------------------------------

	if (player == ctx.getPlayerIndexSmallBlind()) {
		if (effectiveStack <= ctx.sizingBigBlinds(7))
			return [ctx.sizingAllIn()];

		if (effectiveStack < ctx.sizingBigBlinds(26))
			return [ctx.sizingBigBlinds(2.5)];

		return [ctx.sizingBigBlinds(3)];
	}


	// -------------------------------------------------------------
	// BTN
	//
	// <= 8bb   = all-in
	// >8-<21   = 2bb
	// 21-<26   = 2.1bb
	// 26-<50   = 2.25bb
	// >=50bb   = 2.5bb
	//
	// 15-21bb uses 2bb to fill the gap in the supplied ranges.
	// -------------------------------------------------------------

	if (player == ctx.getPlayerIndexButton()) {
		if (effectiveStack <= ctx.sizingBigBlinds(8))
			return [ctx.sizingAllIn()];

		if (effectiveStack < ctx.sizingBigBlinds(21))
			return [ctx.sizingBigBlinds(2)];

		if (effectiveStack < ctx.sizingBigBlinds(26))
			return [ctx.sizingBigBlinds(2.1)];

		if (effectiveStack < ctx.sizingBigBlinds(50))
			return [ctx.sizingBigBlinds(2.25)];

		return [ctx.sizingBigBlinds(2.5)];
	}


	// -------------------------------------------------------------
	// BB
	//
	// There is no normal BB RFI.
	// This keeps the default 3.5bb sizing for the rare case where
	// BB is raising after an SB completion.
	// -------------------------------------------------------------

	if (player == ctx.getPlayerIndexBigBlind())
		return [ctx.sizingBigBlinds(3.5)];


	// -------------------------------------------------------------
	// CO through UTG
	//
	// <= 8bb   = all-in
	// >8-<26   = 2bb
	// 26-<50   = 2.1bb
	// >=50bb   = 2.25bb
	// -------------------------------------------------------------

	if (effectiveStack <= ctx.sizingBigBlinds(8))
		return [ctx.sizingAllIn()];

	if (effectiveStack < ctx.sizingBigBlinds(26))
		return [ctx.sizingBigBlinds(2)];

	if (effectiveStack < ctx.sizingBigBlinds(50))
		return [ctx.sizingBigBlinds(2.1)];

	return [ctx.sizingBigBlinds(2.25)];
}


// ---------------------------------------------------------------------
// 3-BETS
// ---------------------------------------------------------------------

function getSizings3Bets(ctx) {
	let player = ctx.getActivePlayer();
	let lastRaise = ctx.getLastRaiseAction();
	let raiser = lastRaise.getPlayer();
	let openSize = lastRaise.getAmount();


	// -------------------------------------------------------------
	// BB vs SB
	// -------------------------------------------------------------

	if (player == ctx.getPlayerIndexBigBlind() &&
		raiser == ctx.getPlayerIndexSmallBlind()) {

		//SB's normal opens in this script are either 2.5bb or 3bb.
		if (openSize <= ctx.sizingBigBlinds(2.75))
			return sizingFromThreeBetTable(
				ctx,
				THREE_BET_BB_VS_SB_2_5
			);

		return sizingFromThreeBetTable(
			ctx,
			THREE_BET_BB_VS_SB_3_0
		);
	}


	// -------------------------------------------------------------
	// SB vs BB iso after an SB limp.
	//
	// This isn't covered by the supplied Excel table, because the
	// default BB iso is 3.5bb. Preserve the original default-script
	// rule for this special limp/iso branch.
	// -------------------------------------------------------------

	if (player == ctx.getPlayerIndexSmallBlind() &&
		raiser == ctx.getPlayerIndexBigBlind() &&
		openSize > ctx.sizingBigBlinds(2.5)) {

		let defaultSizes =
			Array.from(ctx.sizingsPreflop("9.2bb + 1.0x"));

		let allinSize = ctx.sizingAllIn();

		return defaultSizes.map(
			size => size >= allinSize ? allinSize : size
		);
	}


	// -------------------------------------------------------------
	// BB vs other positions
	// -------------------------------------------------------------

	if (player == ctx.getPlayerIndexBigBlind()) {
		if (openSize <= ctx.sizingBigBlinds(2.1))
			return sizingFromThreeBetTable(
				ctx,
				THREE_BET_BB_VS_2_0_2_1
			);

		if (openSize <= ctx.sizingBigBlinds(2.25))
			return sizingFromThreeBetTable(
				ctx,
				THREE_BET_BB_VS_2_25
			);

		return sizingFromThreeBetTable(
			ctx,
			THREE_BET_BB_VS_2_5
		);
	}


	// -------------------------------------------------------------
	// SB vs other positions
	// -------------------------------------------------------------

	if (player == ctx.getPlayerIndexSmallBlind()) {
		if (openSize <= ctx.sizingBigBlinds(2.1))
			return sizingFromThreeBetTable(
				ctx,
				THREE_BET_SB_VS_2_0_2_1
			);

		if (openSize <= ctx.sizingBigBlinds(2.25))
			return sizingFromThreeBetTable(
				ctx,
				THREE_BET_SB_VS_2_25
			);

		return sizingFromThreeBetTable(
			ctx,
			THREE_BET_SB_VS_2_5
		);
	}


	// -------------------------------------------------------------
	// IP 3-bets
	// -------------------------------------------------------------

	if (openSize <= ctx.sizingBigBlinds(2.1))
		return sizingFromThreeBetTable(
			ctx,
			THREE_BET_IP_VS_2_0_2_1
		);

	if (openSize <= ctx.sizingBigBlinds(2.25))
		return sizingFromThreeBetTable(
			ctx,
			THREE_BET_IP_VS_2_25
		);

	return sizingFromThreeBetTable(
		ctx,
		THREE_BET_IP_VS_2_5
	);
}


// ---------------------------------------------------------------------
// 4-BETS
//
// IP:
// <50bb     = all-in
// 50-<70bb  = 2x
// >=70bb    = 2.1x
//
// OOP:
// <50bb     = all-in
// 50-<70bb  = 2.25x
// >=70bb    = 2.5x
// ---------------------------------------------------------------------

function getSizings4Bets(ctx) {
	let player = ctx.getActivePlayer();
	let lastRaise = ctx.getLastRaiseAction();
	let raiser = lastRaise.getPlayer();

	let effectiveStack = getEffectiveStack(ctx);

	if (effectiveStack < ctx.sizingBigBlinds(50))
		return [ctx.sizingAllIn()];

	let inPosition =
		ctx.isPlayerInPosition(player, raiser);

	let multiplier;

	if (effectiveStack < ctx.sizingBigBlinds(70)) {
		if (inPosition)
			multiplier = 2;
		else
			multiplier = 2.25;
	}
	else {
		if (inPosition)
			multiplier = 2.1;
		else
			multiplier = 2.5;
	}

	let sizing =
		Math.floor(lastRaise.getAmount() * multiplier);

	return [capSizingAtAllIn(ctx, sizing)];
}


// ---------------------------------------------------------------------
// 5-BETS
//
// Always all-in
// ---------------------------------------------------------------------

function getSizings5Bets(ctx) {
	return [ctx.sizingAllIn()];
}


// =====================================================================
// POSTFLOP - unchanged from default
// =====================================================================

function getSizingsPostflop(ctx) {
	let player = ctx.getActivePlayer();

	if (!POSTFLOP_ALLOW_DONK && ctx.isDonkBet()) {
		if (!POSTFLOP_ALLOW_DONK_PREV_AGGRESSION ||
			Array.from(ctx.getActionSequenceFull())
				.findIndex(
					pa =>
						pa.getPlayer() == player &&
						pa.getActionType() == RAISE
				) < 0)
			return [];
	}

	let sizings =
		ctx.getPotState().countPlayersLive() == 2 ?
			POSTFLOP_PRIMARY_HINT_HEADS_UP.map(
				hint => ctx.sizingGeometricHint(hint)
			) :
			POSTFLOP_PRIMARY_HINT_MULTIWAY.map(
				hint => ctx.sizingGeometricHint(hint)
			);

	if (ctx.getStreet() == FLOP &&
		ctx.getBetCount() == 0) {

		sizings.push(
			...POSTFLOP_ADD_FLOP_BET_POT.map(
				s => ctx.sizingPot(s)
			)
		);

		let raise = ctx.getLastRaiseAction();

		if (raise != null &&
			raise.getPlayer() == player) {

			sizings.push(
				...POSTFLOP_ADD_FLOP_CBET_POT.map(
					s => ctx.sizingPot(s)
				)
			);
		}
	}

	if (ctx.getStackPotRatio() <= POSTFLOP_ADD_ALLIN_SPR)
		sizings.push(ctx.sizingAllIn());

	return sizings;
}


// =====================================================================
// FLATTING RULES
// =====================================================================

function canFlatCallPreflop(ctx) {
	let bets = ctx.getBetCount();

	if (bets == 1)
		return ctx.getActivePlayer() ==
			ctx.getPlayerIndexSmallBlind();

	if (ALLOW_FLATS_CLOSING_ACTION &&
		isClosingActionPreflop(ctx))
		return true;

	if (!ALLOW_COLD_CALLS &&
		isColdCall(ctx))
		return false;

	if (ALLOWED_FLATS_PER_RAISE[bets] == undefined)
		return false;

	return ctx.getFlatCallCount() <
		ALLOWED_FLATS_PER_RAISE[bets];
}


//Tests if a call by the current player would be closing the action
function isClosingActionPreflop(ctx) {
	let player = ctx.getActivePlayer();

	if (ctx.getBetCount() == 1)
		return player == ctx.getPlayerIndexBigBlind();

	let maxactive = 0;
	let state = ctx.getPotState();
	let otherplayers = [];

	for (let p = 0; p < ctx.getNumberOfPlayers(); p++) {
		if (!state.hasPlayerFolded(p) &&
			p != player) {

			otherplayers.push(p);

			maxactive = Math.max(
				maxactive,
				state.getChipsActive(p)
			);
		}
	}

	for (let p of otherplayers) {
		if (!state.isPlayerAllIn(p) &&
			state.getChipsActive(p) < maxactive)
			return false;
	}

	return true;
}


function hasNextStreetBetting(ctx) {
	let live = ctx.getPotState().countPlayersLive();

	if (POSTFLOP_FORCE_CHECKDOWN_AFTER[live] == undefined)
		return false;

	return ctx.getStreet() <
		POSTFLOP_FORCE_CHECKDOWN_AFTER[live];
}


//Tests for 3+bets whether the current player had a previous
//action on the current street.
function isColdCall(ctx) {
	if (ctx.getBetCount() <= 2)
		return false;

	let actions = Array.from(ctx.getActionSequence());

	for (let action of actions) {
		if (action.getPlayer() ==
			ctx.getActivePlayer())
			return false;
	}

	return true;
}
