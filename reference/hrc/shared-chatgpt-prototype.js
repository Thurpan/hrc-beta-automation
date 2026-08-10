/*
 * HRC STACK-DEPENDENT PREFLOP + ADVANCED POSTFLOP SCRIPT
 *
 * PRELFOP:
 * - RFI / 3-bet / 4-bet sizing depends on effective stack.
 * - Effective stack uses only players who have not folded.
 * - Up to 2 callers are allowed versus an open.
 *
 * SQUEEZES:
 * - Base size comes from the normal 3-bet table.
 *
 * Squeezer vs FIRST caller effective stack >= 40bb:
 *      1 caller  -> +1.0bb
 *      2 callers -> +1.5bb total
 *
 * Squeezer vs FIRST caller effective stack < 40bb:
 *      1 caller  -> +0.5bb
 *      2 callers -> +1.0bb total
 *
 * - 5-bet+ = all-in.
 * - 40% preflop all-in threshold.
 * - Always offer all-in as a preflop option.
 *
 * POSTFLOP:
 * - Mimics the supplied HRC Advanced Postflop UI setup.
 *
 * Script is stateless:
 * all game-state information is recalculated from ctx.
 */


// =====================================================================
// PREFLOP CONFIGURATION
// =====================================================================


//Effective-stack spreadsheet columns, in big blinds.
const PREFLOP_STACK_GRID = [
	5, 7.5, 10, 12.5, 15, 17.5, 20, 22.5, 25,
	30, 35, 40, 45, 50, 60, 70, 80, 100
];


//40% all-in threshold.
let PREFLOP_ALLIN_THRESHOLD = 0.40;


//-1 is explicitly interpreted by THIS SCRIPT as:
//always add all-in.
let PREFLOP_ADD_ALLIN_SPR = -1;


//Flatting rules.
let ALLOWED_FLATS_PER_RAISE = {
	2: 2, //open / 2-bet: up to TWO flats
	3: 1, //3-bet: up to one flat
	4: 1, //4-bet: up to one flat
	5: 0, //5-bet: no normal flats
	6: 0
};


let ALLOW_COLD_CALLS = false;


//Closing-action calls retain the behavior of the default script.
let ALLOW_FLATS_CLOSING_ACTION = true;


// =====================================================================
// RFI TABLES
// =====================================================================


//BB
const RFI_BB = [
	"allin", "allin",
	"2.5", "2.5", "2.5", "2.5",
	"3", "3", "3", "3", "3", "3",
	"3", "3", "3", "3", "3", "3"
];


//SB
const RFI_SB = [
	"allin", "allin",
	"2.5", "2.5", "2.5", "2.5", "2.5", "2.5", "2.5",
	"3", "3", "3", "3", "3", "3", "3", "3", "3"
];


//BTN
const RFI_BTN = [
	"allin", "allin",
	"2", "2", "2", "2", "2",
	"2.1", "2.1", "2.1", "2.1",
	"2.25", "2.25", "2.25",
	"2.5", "2.5", "2.5", "2.5"
];


//UTG through CO
const RFI_UTG_CO = [
	"allin", "allin",
	"2", "2", "2", "2", "2", "2", "2",
	"2.1", "2.1", "2.1", "2.1", "2.1",
	"2.25", "2.25", "2.25", "2.25"
];


// =====================================================================
// 3-BET TABLES
// =====================================================================


// ---------------------------------------------------------------------
// SB vs BB / BB vs SB
// ---------------------------------------------------------------------

const THREEBET_BLIND_VS_BLIND_VS_2_5 = [
	"allin", "allin", "allin", "allin",
	"5", "5.5", "6", "6", "6.5",
	"6.5", "6.5", "7", "7.5", "8",
	"8.5", "8.5", "8.5", "8.5"
];


const THREEBET_BLIND_VS_BLIND_VS_3 = [
	"allin", "allin", "allin", "allin",
	"6", "6.5", "7", "7", "7.5",
	"7.5", "7.5", "8", "8", "8.5",
	"9", "9", "9", "9"
];


// ---------------------------------------------------------------------
// BB
// ---------------------------------------------------------------------

const THREEBET_BB_VS_2_2_1 = [
	"allin", "allin", "allin", "allin", "allin",
	"5.5", "5.5", "6", "6.5",
	"7", "7.5", "8", "8.5", "9",
	"10", "11", "11.5", "12"
];


const THREEBET_BB_VS_2_25 = [
	"allin", "allin", "allin", "allin", "allin",
	"6", "6", "6", "6.5",
	"7", "8", "8.5", "9", "9.5",
	"10.5", "11.5", "12", "12.5"
];


const THREEBET_BB_VS_2_5 = [
	"allin", "allin", "allin", "allin", "allin",
	"6", "6", "6.5", "7",
	"7.5", "8", "8.5", "9", "10",
	"11", "12", "12.5", "13"
];


// ---------------------------------------------------------------------
// SB
// ---------------------------------------------------------------------

const THREEBET_SB_VS_2_2_1 = [
	"allin", "allin", "allin", "allin", "allin",
	"5", "5", "5", "5.5",
	"6", "6.5", "7.5", "8", "8.5",
	"9", "9.5", "10", "10"
];


const THREEBET_SB_VS_2_25 = [
	"allin", "allin", "allin", "allin", "allin",
	"5", "5", "5.5", "6",
	"6.5", "7", "8", "8.5", "9",
	"9.5", "10", "10.5", "11"
];


const THREEBET_SB_VS_2_5 = [
	"allin", "allin", "allin", "allin", "allin",
	"5.5", "5.5", "6", "6.5",
	"7", "7.5", "8.5", "9", "9.5",
	"10", "10.5", "11", "11.5"
];


// ---------------------------------------------------------------------
// IP
// ---------------------------------------------------------------------

const THREEBET_IP_VS_2_2_1 = [
	"allin", "allin", "allin", "allin",
	"4.5", "5", "5", "5", "5",
	"5.5", "6", "6", "6", "6.5",
	"7", "7.5", "7.5", "7.5"
];


const THREEBET_IP_VS_2_25 = [
	"allin", "allin", "allin", "allin",
	"5", "5", "5", "5", "5.5",
	"5.5", "6", "6.5", "6.5", "7",
	"7.5", "7.5", "8", "8"
];


const THREEBET_IP_VS_2_5 = [
	"allin", "allin", "allin", "allin",
	"5", "5.5", "5.5", "5.5", "6",
	"6", "6.5", "7", "7", "7.5",
	"7.5", "8", "8", "8"
];


// =====================================================================
// 4-BET TABLES
// =====================================================================


//BB vs SB / SB vs BB
const FOURBET_BLIND_VS_BLIND = [
	"allin", "allin", "allin", "allin", "allin",
	"allin", "allin", "allin", "allin", "allin",
	"2x", "2x", "2x",
	"2.1x",
	"2.2x",
	"2.25x",
	"2.5x",
	"2.5x"
];


//Generic IP
const FOURBET_IP = [
	"allin", "allin", "allin", "allin", "allin",
	"allin", "allin", "allin", "allin", "allin",
	"2x", "2x", "2x",
	"2.1x", "2.1x", "2.1x", "2.1x", "2.1x"
];


//Generic OOP
const FOURBET_OOP = [
	"allin", "allin", "allin", "allin", "allin",
	"allin", "allin", "allin", "allin", "allin",
	"2.1x",
	"2.25x", "2.25x", "2.25x", "2.25x",
	"2.5x", "2.5x", "2.5x"
];


// =====================================================================
// POSTFLOP CONFIGURATION
// =====================================================================


// ---------------------------------------------------------------------
// HEADS-UP NORMAL SPR
// ---------------------------------------------------------------------

let POSTFLOP_HU_FLOP_BET   = [0.25, 0.40, 0.67, 1.00];
let POSTFLOP_HU_FLOP_RAISE = [0.33, 0.75];
let POSTFLOP_HU_FLOP_DONK  = [0.33, 0.75];

let POSTFLOP_HU_TURN_BET   = [0.40, 0.80, 1.60];
let POSTFLOP_HU_TURN_RAISE = [0.40, 0.80, 1.60];
let POSTFLOP_HU_TURN_DONK  = [0.33, 0.75];

let POSTFLOP_HU_RIVER_BET   = [0.40, 0.80, 1.60, 3.20];
let POSTFLOP_HU_RIVER_RAISE = [0.50, 1.00, 2.50];
let POSTFLOP_HU_RIVER_DONK  = [0.40, 0.80, 1.60, 3.20];


// ---------------------------------------------------------------------
// MULTIWAY NORMAL SPR
// ---------------------------------------------------------------------

let POSTFLOP_MW_FLOP_BET   = [0.33, 0.75];
let POSTFLOP_MW_FLOP_RAISE = [0.33, 0.75];
let POSTFLOP_MW_FLOP_DONK  = [0.33, 0.75];

let POSTFLOP_MW_TURN_BET   = [0.40, 0.80, 1.60];
let POSTFLOP_MW_TURN_RAISE = [0.50, 1.00];
let POSTFLOP_MW_TURN_DONK  = [0.33, 0.75];

let POSTFLOP_MW_RIVER_BET   = [0.50, 1.00, 2.00];
let POSTFLOP_MW_RIVER_RAISE = [0.75, 1.60];
let POSTFLOP_MW_RIVER_DONK  = [0.50, 1.00];


// ---------------------------------------------------------------------
// LOW SPR
// ---------------------------------------------------------------------

let POSTFLOP_LOW_SPR_HU = 2.5;
let POSTFLOP_LOW_SPR_MW = 1.5;


let POSTFLOP_LOW_SPR_HU_BET =
	[0.10, 0.25, 0.40, 0.67, 1.00, 1.50];


let POSTFLOP_LOW_SPR_HU_RAISE =
	[0.10, 0.25, 0.40, 0.67, 1.00];


let POSTFLOP_LOW_SPR_MW_BET =
	[0.10, 0.25, 0.40, 0.67];


let POSTFLOP_LOW_SPR_MW_RAISE =
	[0.20, 0.40];


//Add all-in at SPR <= 5.
let POSTFLOP_ADD_ALLIN_SPR = 5.0;


//Limited donks.
let POSTFLOP_ALLOW_DONK = false;
let POSTFLOP_ALLOW_DONK_PREV_AGGRESSION = true;


//Final regular betting round.
let POSTFLOP_FORCE_CHECKDOWN_AFTER = {
	2: RIVER,
	3: RIVER,
	4: TURN
};


// =====================================================================
// GENERAL HELPERS
// =====================================================================


function uniqueSizings(sizings) {

	let result = [];

	for (let sizing of sizings) {

		if (result.indexOf(sizing) < 0)
			result.push(sizing);
	}

	return result;
}


// =====================================================================
// STACK HELPERS
// =====================================================================


//Starting stack:
//
// active chips + dead chips + remaining chips
function getPlayerStartingStack(ctx, player) {

	let state =
		ctx.getPotState();

	return (
		state.getChipsActive(player) +
		state.getChipsDead(player) +
		state.getChipsRemaining(player)
	);
}


//Effective stack for a specified player versus
//the DEEPEST opponent who has not folded.
function getEffectiveStackForPlayer(ctx, player) {

	let state =
		ctx.getPotState();

	let playerStack =
		getPlayerStartingStack(
			ctx,
			player
		);

	let deepestOpponentStack = 0;

	let opponentFound = false;


	for (
		let p = 0;
		p < ctx.getNumberOfPlayers();
		p++
	) {

		if (p == player)
			continue;

		if (state.hasPlayerFolded(p))
			continue;


		let opponentStack =
			getPlayerStartingStack(
				ctx,
				p
			);


		if (
			!opponentFound ||
			opponentStack >
				deepestOpponentStack
		) {

			deepestOpponentStack =
				opponentStack;

			opponentFound = true;
		}
	}


	if (!opponentFound)
		return playerStack;


	return Math.min(
		playerStack,
		deepestOpponentStack
	);
}


function getEffectiveStack(ctx) {

	return getEffectiveStackForPlayer(
		ctx,
		ctx.getActivePlayer()
	);
}


// =====================================================================
// HISTORICAL OPEN EFFECTIVE STACK
// =====================================================================


//Reconstructs the effective stack that existed when
//the original open raise occurred.
//
//Players who had already folded before the open are excluded.
function getHistoricalOpenEffectiveStack(
	ctx,
	opener,
	foldedBeforeOpen
) {

	let openerStack =
		getPlayerStartingStack(
			ctx,
			opener
		);

	let deepestOpponentStack = 0;

	let opponentFound = false;


	for (
		let p = 0;
		p < ctx.getNumberOfPlayers();
		p++
	) {

		if (p == opener)
			continue;

		if (foldedBeforeOpen[p] === true)
			continue;


		let opponentStack =
			getPlayerStartingStack(
				ctx,
				p
			);


		if (
			!opponentFound ||
			opponentStack >
				deepestOpponentStack
		) {

			deepestOpponentStack =
				opponentStack;

			opponentFound = true;
		}
	}


	if (!opponentFound)
		return openerStack;


	return Math.min(
		openerStack,
		deepestOpponentStack
	);
}


// =====================================================================
// STACK BUCKET LOOKUP
// =====================================================================


//Uses the NEXT HIGHER spreadsheet column.
//
//Examples:
//
// 5bb    -> 5
// 6bb    -> 7.5
// 8bb    -> 10
// 18bb   -> 20
// 28bb   -> 30
// 75bb   -> 80
// >100bb -> 100
function getStackBucketIndex(
	ctx,
	effectiveStack
) {

	for (
		let i = 0;
		i < PREFLOP_STACK_GRID.length;
		i++
	) {

		let stackLimit =
			ctx.sizingBigBlinds(
				PREFLOP_STACK_GRID[i]
			);


		if (effectiveStack <= stackLimit)
			return i;
	}


	return PREFLOP_STACK_GRID.length - 1;
}


function getCurrentStackBucketIndex(ctx) {

	return getStackBucketIndex(
		ctx,
		getEffectiveStack(ctx)
	);
}


// =====================================================================
// RFI HELPERS
// =====================================================================


function getRfiTable(ctx, player) {

	if (
		player ==
		ctx.getPlayerIndexBigBlind()
	) {
		return RFI_BB;
	}


	if (
		player ==
		ctx.getPlayerIndexSmallBlind()
	) {
		return RFI_SB;
	}


	if (
		player ==
		ctx.getPlayerIndexButton()
	) {
		return RFI_BTN;
	}


	//All other positions:
	//UTG through CO.
	return RFI_UTG_CO;
}


//Find the original preflop open raise and reconstruct
//the corresponding table RFI size.
function getOriginalOpenInfo(ctx) {

	let actions =
		Array.from(
			ctx.getActionSequence(PREFLOP)
		);


	let foldedBeforeOpen = [];


	for (
		let p = 0;
		p < ctx.getNumberOfPlayers();
		p++
	) {

		foldedBeforeOpen[p] = false;
	}


	for (let action of actions) {

		let actionType =
			action.getActionType();

		let actionPlayer =
			action.getPlayer();


		if (actionType == FOLD) {

			foldedBeforeOpen[
				actionPlayer
			] = true;

			continue;
		}


		if (actionType == RAISE) {

			let effectiveStack =
				getHistoricalOpenEffectiveStack(
					ctx,
					actionPlayer,
					foldedBeforeOpen
				);


			let bucket =
				getStackBucketIndex(
					ctx,
					effectiveStack
				);


			let table =
				getRfiTable(
					ctx,
					actionPlayer
				);


			return {
				player: actionPlayer,
				value: table[bucket]
			};
		}
	}


	return null;
}


// =====================================================================
// SQUEEZE HELPERS
// =====================================================================


//Finds the FIRST caller after the original preflop raise.
//
//Any calls before the first raise, such as a blind completion,
//are intentionally ignored.
function getFirstCallerAfterOpen(ctx) {

	let actions =
		Array.from(
			ctx.getActionSequence(PREFLOP)
		);

	let openFound = false;


	for (let action of actions) {

		let actionType =
			action.getActionType();


		if (!openFound) {

			if (actionType == RAISE)
				openFound = true;

			continue;
		}


		if (actionType == CALL) {

			return action.getPlayer();
		}
	}


	return -1;
}


//Returns the squeeze adjustment in BB.
//
//1 caller:
//   >=40bb -> +1.0bb
//   <40bb  -> +0.5bb
//
//2 callers:
//   >=40bb -> +1.5bb total
//   <40bb  -> +1.0bb total
//
//The 40bb comparison is specifically:
//
// min(
//     squeezer starting stack,
//     FIRST caller starting stack
// )
function getSqueezeAdjustmentBb(ctx) {

	let callers =
		ctx.getFlatCallCount();


	//No caller = ordinary 3-bet.
	if (callers <= 0)
		return 0;


	let firstCaller =
		getFirstCallerAfterOpen(ctx);


	if (firstCaller < 0)
		return 0;


	let squeezer =
		ctx.getActivePlayer();


	let squeezerStack =
		getPlayerStartingStack(
			ctx,
			squeezer
		);


	let callerStack =
		getPlayerStartingStack(
			ctx,
			firstCaller
		);


	let effectiveStack =
		Math.min(
			squeezerStack,
			callerStack
		);


	let fortyBb =
		ctx.sizingBigBlinds(40);


	let atLeast40bb =
		effectiveStack >= fortyBb;


	//One caller.
	if (callers == 1) {

		if (atLeast40bb)
			return 1.0;

		return 0.5;
	}


	//Two callers.
	//
	//Open-raise flatting is limited to 2,
	//so this is also the fallback for any
	//unexpected callers >= 2.
	if (atLeast40bb)
		return 1.5;


	return 1.0;
}


// =====================================================================
// 3-BET TABLE SIZING HELPER
// =====================================================================


//Selects the normal 3-bet from the table,
//then adds the squeeze adjustment when applicable.
function getThreeBetSizingFromTable(
	ctx,
	table
) {

	let bucket =
		getCurrentStackBucketIndex(ctx);


	let value =
		table[bucket];


	//An all-in table entry remains all-in.
	if (value == "allin") {

		return [
			ctx.sizingAllIn()
		];
	}


	let baseSizeBb =
		Number(value);


	let squeezeAdjustmentBb =
		getSqueezeAdjustmentBb(ctx);


	let finalSizeBb =
		baseSizeBb +
		squeezeAdjustmentBb;


	return Array.from(
		ctx.sizingsPreflop(
			finalSizeBb + "bb"
		)
	);
}


// =====================================================================
// 4-BET TABLE SIZING HELPER
// =====================================================================


function getMultiplierSizingFromTable(
	ctx,
	table
) {

	let bucket =
		getCurrentStackBucketIndex(ctx);


	let value =
		table[bucket];


	if (value == "allin") {

		return [
			ctx.sizingAllIn()
		];
	}


	return Array.from(
		ctx.sizingsPreflop(value)
	);
}


// =====================================================================
// PREFLOP ALL-IN THRESHOLD
// =====================================================================


//Same threshold calculation as the default script.
//
//A normal sizing is converted to all-in when it is
//at least 40% of the way from the player's currently
//active chips to the all-in raise size.
function applyAllinThreshold(
	ctx,
	sizings
) {

	let sizeallin =
		ctx.sizingAllIn();


	let activechips =
		ctx.getPotState()
			.getChipsActive(
				ctx.getActivePlayer()
			);


	let thresholdchips =
		activechips +
		(
			sizeallin -
			activechips
		) *
		PREFLOP_ALLIN_THRESHOLD;


	return sizings.map(
		sizing =>
			sizing >= thresholdchips ?
				sizeallin :
				sizing
	);
}


// =====================================================================
// MAIN PREFLOP METHOD
// =====================================================================


function getSizingsPreflop(ctx) {

	let bets =
		1 + ctx.getBetCount();


	let sizings = [];


	switch (bets) {

		case 2:
			//Open / RFI.
			sizings =
				getSizingsOpening(ctx);
			break;


		case 3:
			//3-bet / squeeze.
			sizings =
				getSizings3Bets(ctx);
			break;


		case 4:
			//4-bet.
			sizings =
				getSizings4Bets(ctx);
			break;


		case 5:
			//5-bet.
			sizings =
				getSizings5Bets(ctx);
			break;


		default:
			//6-bet+.
			return [
				ctx.sizingAllIn()
			];
	}


	/*
	 * -1 means always add all-in.
	 *
	 * This is OUR explicit sentinel handling.
	 * We are not relying on HRC itself to give
	 * -1 a special meaning.
	 */
	if (
		PREFLOP_ADD_ALLIN_SPR < 0 ||
		ctx.getStackPotRatio() <=
			PREFLOP_ADD_ALLIN_SPR
	) {

		sizings.push(
			ctx.sizingAllIn()
		);
	}


	//Apply the 40% threshold after the squeeze
	//adjustment, then remove duplicate all-ins.
	return uniqueSizings(
		applyAllinThreshold(
			ctx,
			sizings
		)
	);
}


// =====================================================================
// RFI
// =====================================================================


function getSizingsOpening(ctx) {

	let player =
		ctx.getActivePlayer();


	let table =
		getRfiTable(
			ctx,
			player
		);


	let bucket =
		getCurrentStackBucketIndex(ctx);


	let value =
		table[bucket];


	if (value == "allin") {

		return [
			ctx.sizingAllIn()
		];
	}


	return Array.from(
		ctx.sizingsPreflop(
			value + "bb"
		)
	);
}


// =====================================================================
// 3-BETS / SQUEEZES
// =====================================================================


function getSizings3Bets(ctx) {

	let player =
		ctx.getActivePlayer();


	let openInfo =
		getOriginalOpenInfo(ctx);


	if (openInfo == null)
		return [];


	let raiser =
		openInfo.player;


	let openSize =
		openInfo.value;


	/*
	 * If the RFI table says the opener is all-in,
	 * there is no ordinary 3-bet table size.
	 *
	 * The main preflop method will still add the
	 * all-in option.
	 */
	if (openSize == "allin")
		return [];


	// -------------------------------------------------------------
	// BB vs SB
	// -------------------------------------------------------------

	if (
		player ==
			ctx.getPlayerIndexBigBlind() &&
		raiser ==
			ctx.getPlayerIndexSmallBlind()
	) {

		if (openSize == "2.5") {

			return getThreeBetSizingFromTable(
				ctx,
				THREEBET_BLIND_VS_BLIND_VS_2_5
			);
		}


		if (openSize == "3") {

			return getThreeBetSizingFromTable(
				ctx,
				THREEBET_BLIND_VS_BLIND_VS_3
			);
		}


		return [];
	}


	// -------------------------------------------------------------
	// SB vs BB
	// -------------------------------------------------------------

	if (
		player ==
			ctx.getPlayerIndexSmallBlind() &&
		raiser ==
			ctx.getPlayerIndexBigBlind()
	) {

		if (openSize == "2.5") {

			return getThreeBetSizingFromTable(
				ctx,
				THREEBET_BLIND_VS_BLIND_VS_2_5
			);
		}


		if (openSize == "3") {

			return getThreeBetSizingFromTable(
				ctx,
				THREEBET_BLIND_VS_BLIND_VS_3
			);
		}


		return [];
	}


	// -------------------------------------------------------------
	// Normal BB / SB / IP tables
	// -------------------------------------------------------------

	let table2To21;

	let table225;

	let table25;


	//BB
	if (
		player ==
		ctx.getPlayerIndexBigBlind()
	) {

		table2To21 =
			THREEBET_BB_VS_2_2_1;

		table225 =
			THREEBET_BB_VS_2_25;

		table25 =
			THREEBET_BB_VS_2_5;
	}


	//SB
	else if (
		player ==
		ctx.getPlayerIndexSmallBlind()
	) {

		table2To21 =
			THREEBET_SB_VS_2_2_1;

		table225 =
			THREEBET_SB_VS_2_25;

		table25 =
			THREEBET_SB_VS_2_5;
	}


	//All other players use IP table.
	else {

		table2To21 =
			THREEBET_IP_VS_2_2_1;

		table225 =
			THREEBET_IP_VS_2_25;

		table25 =
			THREEBET_IP_VS_2_5;
	}


	//vs 2bb or 2.1bb.
	if (
		openSize == "2" ||
		openSize == "2.1"
	) {

		return getThreeBetSizingFromTable(
			ctx,
			table2To21
		);
	}


	//vs 2.25bb.
	if (openSize == "2.25") {

		return getThreeBetSizingFromTable(
			ctx,
			table225
		);
	}


	//vs 2.5bb.
	if (openSize == "2.5") {

		return getThreeBetSizingFromTable(
			ctx,
			table25
		);
	}


	return [];
}


// =====================================================================
// 4-BETS
// =====================================================================


function getSizings4Bets(ctx) {

	let player =
		ctx.getActivePlayer();


	let lastRaise =
		ctx.getLastRaiseAction();


	let raiser =
		lastRaise.getPlayer();


	// -------------------------------------------------------------
	// BB vs SB
	// -------------------------------------------------------------

	if (
		player ==
			ctx.getPlayerIndexBigBlind() &&
		raiser ==
			ctx.getPlayerIndexSmallBlind()
	) {

		return getMultiplierSizingFromTable(
			ctx,
			FOURBET_BLIND_VS_BLIND
		);
	}


	// -------------------------------------------------------------
	// SB vs BB
	// -------------------------------------------------------------

	if (
		player ==
			ctx.getPlayerIndexSmallBlind() &&
		raiser ==
			ctx.getPlayerIndexBigBlind()
	) {

		return getMultiplierSizingFromTable(
			ctx,
			FOURBET_BLIND_VS_BLIND
		);
	}


	// -------------------------------------------------------------
	// Generic IP / OOP
	// -------------------------------------------------------------

	let inPosition =
		ctx.isPlayerInPosition(
			player,
			raiser
		);


	if (inPosition) {

		return getMultiplierSizingFromTable(
			ctx,
			FOURBET_IP
		);
	}


	return getMultiplierSizingFromTable(
		ctx,
		FOURBET_OOP
	);
}


// =====================================================================
// 5-BETS
// =====================================================================


function getSizings5Bets(ctx) {

	return [
		ctx.sizingAllIn()
	];
}


// =====================================================================
// POSTFLOP HELPERS
// =====================================================================


function getPostflopPotSizings(
	ctx,
	sizes
) {

	let sizings = [];


	for (let size of sizes) {

		sizings.push(
			ctx.sizingPot(size)
		);
	}


	return sizings;
}


// =====================================================================
// POSTFLOP SIZINGS
// =====================================================================


function getSizingsPostflop(ctx) {

	let player =
		ctx.getActivePlayer();


	let livePlayers =
		ctx.getPotState()
			.countPlayersLive();


	let headsUp =
		livePlayers == 2;


	let facingBet =
		ctx.getBetCount() > 0;


	let donk =
		ctx.isDonkBet();


	// -------------------------------------------------------------
	// LIMITED DONKS
	// -------------------------------------------------------------

	if (
		donk &&
		!POSTFLOP_ALLOW_DONK
	) {

		let previousAggression =
			Array.from(
				ctx.getActionSequenceFull()
			).findIndex(
				action =>
					action.getPlayer() ==
						player &&
					action.getActionType() ==
						RAISE
			) >= 0;


		if (
			!POSTFLOP_ALLOW_DONK_PREV_AGGRESSION ||
			!previousAggression
		) {

			return [];
		}
	}


	let spr =
		ctx.getStackPotRatio();


	let lowSpr;


	if (headsUp) {

		lowSpr =
			spr <=
			POSTFLOP_LOW_SPR_HU;
	}

	else {

		lowSpr =
			spr <=
			POSTFLOP_LOW_SPR_MW;
	}


	let sizes = [];


	// =============================================================
	// LOW-SPR OVERRIDE
	// =============================================================

	if (lowSpr) {


		// ---------------------------------------------------------
		// HEADS-UP LOW SPR
		// ---------------------------------------------------------

		if (headsUp) {

			if (facingBet) {

				sizes =
					POSTFLOP_LOW_SPR_HU_RAISE;
			}

			else {

				sizes =
					POSTFLOP_LOW_SPR_HU_BET;
			}
		}


		// ---------------------------------------------------------
		// MULTIWAY LOW SPR
		// ---------------------------------------------------------

		else {

			if (facingBet) {

				sizes =
					POSTFLOP_LOW_SPR_MW_RAISE;
			}

			else {

				sizes =
					POSTFLOP_LOW_SPR_MW_BET;
			}
		}
	}


	// =============================================================
	// NORMAL SPR
	// =============================================================

	else {


		// ---------------------------------------------------------
		// HEADS-UP
		// ---------------------------------------------------------

		if (headsUp) {


			//FLOP
			if (
				ctx.getStreet() ==
				FLOP
			) {

				if (facingBet) {

					sizes =
						POSTFLOP_HU_FLOP_RAISE;
				}


				else if (donk) {

					sizes =
						POSTFLOP_HU_FLOP_DONK;
				}


				else {

					sizes =
						POSTFLOP_HU_FLOP_BET;
				}
			}


			//TURN
			else if (
				ctx.getStreet() ==
				TURN
			) {

				if (facingBet) {

					sizes =
						POSTFLOP_HU_TURN_RAISE;
				}


				else if (donk) {

					sizes =
						POSTFLOP_HU_TURN_DONK;
				}


				else {

					sizes =
						POSTFLOP_HU_TURN_BET;
				}
			}


			//RIVER
			else if (
				ctx.getStreet() ==
				RIVER
			) {

				if (facingBet) {

					sizes =
						POSTFLOP_HU_RIVER_RAISE;
				}


				else if (donk) {

					sizes =
						POSTFLOP_HU_RIVER_DONK;
				}


				else {

					sizes =
						POSTFLOP_HU_RIVER_BET;
				}
			}
		}


		// ---------------------------------------------------------
		// MULTIWAY
		// ---------------------------------------------------------

		else {


			//FLOP
			if (
				ctx.getStreet() ==
				FLOP
			) {

				if (facingBet) {

					sizes =
						POSTFLOP_MW_FLOP_RAISE;
				}


				else if (donk) {

					sizes =
						POSTFLOP_MW_FLOP_DONK;
				}


				else {

					sizes =
						POSTFLOP_MW_FLOP_BET;
				}
			}


			//TURN
			else if (
				ctx.getStreet() ==
				TURN
			) {

				if (facingBet) {

					sizes =
						POSTFLOP_MW_TURN_RAISE;
				}


				else if (donk) {

					sizes =
						POSTFLOP_MW_TURN_DONK;
				}


				else {

					sizes =
						POSTFLOP_MW_TURN_BET;
				}
			}


			//RIVER
			else if (
				ctx.getStreet() ==
				RIVER
			) {

				if (facingBet) {

					sizes =
						POSTFLOP_MW_RIVER_RAISE;
				}


				else if (donk) {

					sizes =
						POSTFLOP_MW_RIVER_DONK;
				}


				else {

					sizes =
						POSTFLOP_MW_RIVER_BET;
				}
			}
		}
	}


	let sizings =
		getPostflopPotSizings(
			ctx,
			sizes
		);


	//Add all-in whenever SPR <= 5.
	if (
		spr <=
		POSTFLOP_ADD_ALLIN_SPR
	) {

		sizings.push(
			ctx.sizingAllIn()
		);
	}


	return uniqueSizings(
		sizings
	);
}


// =====================================================================
// PREFLOP FLATTING RULES
// =====================================================================


function canFlatCallPreflop(ctx) {

	let bets =
		ctx.getBetCount();


	//Before any raise, only SB may complete.
	if (bets == 1) {

		return (
			ctx.getActivePlayer() ==
			ctx.getPlayerIndexSmallBlind()
		);
	}


	//Preserve default-script closing-action exception.
	if (
		ALLOW_FLATS_CLOSING_ACTION &&
		isClosingActionPreflop(ctx)
	) {

		return true;
	}


	//No cold calls of 3-bets+.
	if (
		!ALLOW_COLD_CALLS &&
		isColdCall(ctx)
	) {

		return false;
	}


	if (
		ALLOWED_FLATS_PER_RAISE[bets] ==
		undefined
	) {

		return false;
	}


	return (
		ctx.getFlatCallCount() <
		ALLOWED_FLATS_PER_RAISE[bets]
	);
}


// =====================================================================
// CLOSING ACTION
// =====================================================================


function isClosingActionPreflop(ctx) {

	let player =
		ctx.getActivePlayer();


	if (ctx.getBetCount() == 1) {

		return (
			player ==
			ctx.getPlayerIndexBigBlind()
		);
	}


	let maxactive = 0;


	let state =
		ctx.getPotState();


	let otherplayers = [];


	for (
		let p = 0;
		p < ctx.getNumberOfPlayers();
		p++
	) {

		if (
			!state.hasPlayerFolded(p) &&
			p != player
		) {

			otherplayers.push(p);


			maxactive =
				Math.max(
					maxactive,
					state.getChipsActive(p)
				);
		}
	}


	for (let p of otherplayers) {

		if (
			!state.isPlayerAllIn(p) &&
			state.getChipsActive(p) <
				maxactive
		) {

			return false;
		}
	}


	return true;
}


// =====================================================================
// FINAL BETTING ROUND
// =====================================================================


function hasNextStreetBetting(ctx) {

	let live =
		ctx.getPotState()
			.countPlayersLive();


	if (
		POSTFLOP_FORCE_CHECKDOWN_AFTER[live] ==
		undefined
	) {

		return false;
	}


	return (
		ctx.getStreet() <
		POSTFLOP_FORCE_CHECKDOWN_AFTER[live]
	);
}


// =====================================================================
// COLD CALL CHECK
// =====================================================================


function isColdCall(ctx) {

	if (ctx.getBetCount() <= 2)
		return false;


	let actions =
		Array.from(
			ctx.getActionSequence()
		);


	for (let action of actions) {

		if (
			action.getPlayer() ==
			ctx.getActivePlayer()
		) {

			return false;
		}
	}


	return true;
}
