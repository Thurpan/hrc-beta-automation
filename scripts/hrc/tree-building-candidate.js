/*
 * Project-owned HRC tree-building candidate.
 *
 * Derived from reference/hrc/shared-chatgpt-prototype.js.
 * Source snapshot SHA-256:
 * f39e83006039b26f27beed4c7f0f8e08d6929cfcf31d3b5deeadd2a448448f37
 *
 * This file is not a verbatim copy and has not been validated in HRC.
 * Project changes are recorded in docs/hrc-script-design.md.
 * Load it only in a five-player, non-straddled configuration.
 *
 * HRC STACK-DEPENDENT PREFLOP + ADVANCED POSTFLOP SCRIPT
 *
 * PREFLOP:
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
 * - 50% preflop all-in threshold.
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


//The generated configurations contain five seats. Folds can reduce the
//number of players still in the hand without changing this configured count.
const SUPPORTED_PLAYER_COUNT = 5;


//50% all-in threshold.
let PREFLOP_ALLIN_THRESHOLD = 0.50;


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


//Permit one non-cold closing call against a 5-bet or later all-in.
//This exception never overrides the open, 3-bet, or 4-bet caps.
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


const PREFLOP_SIZING_TABLES = [
	{ name: "RFI_BB", values: RFI_BB },
	{ name: "RFI_SB", values: RFI_SB },
	{ name: "RFI_BTN", values: RFI_BTN },
	{ name: "RFI_UTG_CO", values: RFI_UTG_CO },
	{
		name: "THREEBET_BLIND_VS_BLIND_VS_2_5",
		values: THREEBET_BLIND_VS_BLIND_VS_2_5
	},
	{
		name: "THREEBET_BLIND_VS_BLIND_VS_3",
		values: THREEBET_BLIND_VS_BLIND_VS_3
	},
	{ name: "THREEBET_BB_VS_2_2_1", values: THREEBET_BB_VS_2_2_1 },
	{ name: "THREEBET_BB_VS_2_25", values: THREEBET_BB_VS_2_25 },
	{ name: "THREEBET_BB_VS_2_5", values: THREEBET_BB_VS_2_5 },
	{ name: "THREEBET_SB_VS_2_2_1", values: THREEBET_SB_VS_2_2_1 },
	{ name: "THREEBET_SB_VS_2_25", values: THREEBET_SB_VS_2_25 },
	{ name: "THREEBET_SB_VS_2_5", values: THREEBET_SB_VS_2_5 },
	{ name: "THREEBET_IP_VS_2_2_1", values: THREEBET_IP_VS_2_2_1 },
	{ name: "THREEBET_IP_VS_2_25", values: THREEBET_IP_VS_2_25 },
	{ name: "THREEBET_IP_VS_2_5", values: THREEBET_IP_VS_2_5 },
	{ name: "FOURBET_BLIND_VS_BLIND", values: FOURBET_BLIND_VS_BLIND },
	{ name: "FOURBET_IP", values: FOURBET_IP },
	{ name: "FOURBET_OOP", values: FOURBET_OOP }
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


//The source HRC screenshot leaves "Bets per Street" blank in both columns.
//null therefore means no numeric cap. If a number is configured later, only
//all-in raises are returned after that many bets or raises on the street.
let POSTFLOP_BETS_PER_STREET = null;


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


function assertSupportedConfiguration(ctx) {

	if (
		ctx.getNumberOfPlayers() !=
			SUPPORTED_PLAYER_COUNT
	) {

		throw new Error(
			"This HRC sizing candidate supports five-player " +
			"configurations only."
		);
	}

}


function assertPreflopSizingTables() {

	for (let table of PREFLOP_SIZING_TABLES) {

		if (
			table.values.length !=
				PREFLOP_STACK_GRID.length
		) {

			throw new Error(
				table.name +
				" must contain one value for each stack bucket."
			);
		}
	}
}


//Table shape is static. Validate it once when HRC loads the script rather
//than repeating the same work at every tree node.
assertPreflopSizingTables();


function normalizeAndUniqueSizings(ctx, sizings) {

	let minimum =
		ctx.sizingMinimum();

	let allin =
		ctx.sizingAllIn();

	let result = [];


	for (let sizing of sizings) {

		if (typeof sizing != "number" || !isFinite(sizing)) {

			throw new Error(
				"A sizing callback produced a non-finite amount."
			);
		}


		//HRC discards negative sizes. Mirror that behaviour before
		//normalisation so duplicate legal actions can be removed here.
		if (sizing < 0)
			continue;


		let normalized =
			Math.min(
				Math.max(sizing, minimum),
				allin
			);


		if (result.indexOf(normalized) < 0)
			result.push(normalized);
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


	if (!opponentFound) {

		throw new Error(
			"Effective stack requested without a non-folded opponent."
		);
	}


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


	if (!opponentFound) {

		throw new Error(
			"Historical effective stack requested without an opponent."
		);
	}


	return Math.min(
		openerStack,
		deepestOpponentStack
	);
}


// =====================================================================
// STACK BUCKET LOOKUP
// =====================================================================


//Every generated starting stack is an exact spreadsheet value. The project
//effective stack is the minimum of two such values, so it must also match a
//spreadsheet column exactly. Fail instead of silently rounding an unsupported
//configuration.
function getStackBucketIndex(
	ctx,
	effectiveStack
) {

	for (
		let i = 0;
		i < PREFLOP_STACK_GRID.length;
		i++
	) {

		let stackValue =
			ctx.sizingBigBlinds(
				PREFLOP_STACK_GRID[i]
			);


		if (effectiveStack == stackValue)
			return i;
	}


	throw new Error(
		"Effective stack does not match a configured workbook column: " +
		effectiveStack
	);
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


//Find the original preflop open raise, reconstruct its expected table size,
//and compare that size with the raise-to amount HRC actually recorded.
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


			let expectedValue =
				table[bucket];


			let actualAmount =
				Number(
					action.getAmount()
				);


			if (
				!isFinite(actualAmount) ||
				actualAmount < 0
			) {

				throw new Error(
					"The original raise has an invalid recorded amount."
				);
			}


			let expectedAmount = null;


			if (expectedValue != "allin") {

				expectedAmount =
					ctx.sizingBigBlinds(
						Number(expectedValue)
					);
			}


			return {
				player: actionPlayer,
				expectedValue: expectedValue,
				actualAmount: actualAmount,
				isConfiguredOrdinary:
					expectedAmount != null &&
					actualAmount == expectedAmount
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
//at least 50% of the way from the player's currently
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

	assertSupportedConfiguration(ctx);

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
			sizings = [
				ctx.sizingAllIn()
			];
			break;
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


	//Apply the 50% threshold after the squeeze adjustment. Mirror HRC's
	//minimum/all-in normalisation before removing duplicate legal actions.
	return normalizeAndUniqueSizings(
		ctx,
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


	if (openInfo == null) {

		throw new Error(
			"A 3-bet decision has no original preflop raise."
		);
	}


	let raiser =
		openInfo.player;


	let openSize =
		openInfo.expectedValue;


	/*
	 * Use an ordinary 3-bet table only when the actual raise-to amount matches
	 * the reconstructed workbook open. A configured or optional all-in, a
	 * legally normalised open, or any unsupported size receives only the
	 * global all-in response added by getSizingsPreflop().
	 *
	 * The main preflop method will still add the
	 * all-in option.
	 */
	if (!openInfo.isConfiguredOrdinary)
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


function isBlindVersusBlind(ctx, playerA, playerB) {

	let smallBlind =
		ctx.getPlayerIndexSmallBlind();

	let bigBlind =
		ctx.getPlayerIndexBigBlind();


	return (
		(
			playerA == smallBlind &&
			playerB == bigBlind
		) ||
		(
			playerA == bigBlind &&
			playerB == smallBlind
		)
	);
}


function getSizings4Bets(ctx) {

	let player =
		ctx.getActivePlayer();


	let lastRaise =
		ctx.getLastRaiseAction();


	if (lastRaise == null) {

		throw new Error(
			"A 4-bet decision has no previous raise."
		);
	}


	let raiser =
		lastRaise.getPlayer();


	let openInfo =
		getOriginalOpenInfo(ctx);


	if (openInfo == null) {

		throw new Error(
			"A 4-bet decision has no original preflop raise."
		);
	}


	let originalOpenWasBlind =
		openInfo.player ==
			ctx.getPlayerIndexSmallBlind() ||
		openInfo.player ==
			ctx.getPlayerIndexBigBlind();


	// -------------------------------------------------------------
	// Genuine blind-versus-blind opening line
	// -------------------------------------------------------------

	if (
		originalOpenWasBlind &&
		isBlindVersusBlind(
			ctx,
			player,
			raiser
		)
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

	assertSupportedConfiguration(ctx);

	let player =
		ctx.getActivePlayer();


	//The source HRC UI uses players who can still act for its HU/multiway
	//columns. countPlayersLive() intentionally excludes all-in players.
	let playersAbleToAct =
		ctx.getPotState()
			.countPlayersLive();


	let headsUp =
		playersAbleToAct == 2;


	let facingBet =
		ctx.getBetCount() > 0;


	let donk =
		!facingBet &&
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


	if (
		POSTFLOP_BETS_PER_STREET != null &&
		ctx.getBetCount() >=
			POSTFLOP_BETS_PER_STREET
	) {

		return normalizeAndUniqueSizings(
			ctx,
			[ctx.sizingAllIn()]
		);
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


	return normalizeAndUniqueSizings(
		ctx,
		sizings
	);
}


//Keep the documented ITreeBuildingScript default and make the project choice
//explicit instead of relying on an omitted callback.
function canFlatCallPostflop(ctx) {

	assertSupportedConfiguration(ctx);

	return true;
}


// =====================================================================
// PREFLOP FLATTING RULES
// =====================================================================


function canFlatCallPreflop(ctx) {

	assertSupportedConfiguration(ctx);

	let bets =
		ctx.getBetCount();


	//Before any raise, only SB may complete.
	if (bets == 1) {

		return (
			ctx.getActivePlayer() ==
			ctx.getPlayerIndexSmallBlind()
		);
	}


	let coldCall =
		isColdCall(ctx);


	//No cold calls of 3-bets+.
	if (
		!ALLOW_COLD_CALLS &&
		coldCall
	) {

		return false;
	}


	let flatCallCap =
		ALLOWED_FLATS_PER_RAISE[bets];


	if (flatCallCap == undefined) {

		if (bets >= 5)
			flatCallCap = 0;
		else
			return false;
	}


	let flatCallCount =
		ctx.getFlatCallCount();


	if (
		flatCallCount <
			flatCallCap
	) {

		return true;
	}


	//A 5-bet or later action is all-in in this policy. Permit exactly one
	//non-cold call when it closes the action, but never use closing action to
	//bypass the hard two/one/one caller caps against earlier raises.
	return (
		ALLOW_FLATS_CLOSING_ACTION &&
		bets >= 5 &&
		flatCallCount == 0 &&
		!coldCall &&
		ctx.isClosingAction()
	);
}


// =====================================================================
// FINAL BETTING ROUND
// =====================================================================


function hasNextStreetBetting(ctx) {

	assertSupportedConfiguration(ctx);

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
				ctx.getActivePlayer() &&
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
