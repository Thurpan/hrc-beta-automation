const assert = require("node:assert/strict");
const crypto = require("node:crypto");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const vm = require("node:vm");


const PREFLOP = 0;
const FLOP = 1;
const TURN = 2;
const RIVER = 3;
const FOLD = 0;
const CHECK = 1;
const CALL = 2;
const RAISE = 3;


const candidatePath = path.resolve(
    __dirname,
    "../../scripts/hrc/tree-building-hu-candidate.js",
);
const candidateSource = fs.readFileSync(candidatePath, "utf8");
const exportSource = `
globalThis.__hu = {
    HU_STACK_GRID,
    HU_PREFLOP_SIZING_TABLES,
    getPlayerStartingStack,
    getEffectiveStackForPlayer,
    getStackBucketIndex,
    getCurrentStackBucketIndex,
    resolveFixedBbCell,
    getFirstRaiseInfo,
    normalizeAndUniqueSizings,
    applyAllinThreshold,
    getSizingsPreflop,
    canFlatCallPreflop,
    getSizingsPostflop,
    canFlatCallPostflop,
    hasNextStreetBetting,
    get preflopAllinThreshold() { return PREFLOP_ALLIN_THRESHOLD; },
    get postflopBetsPerStreet() { return POSTFLOP_BETS_PER_STREET; },
    setPostflopBetsPerStreet(value) { POSTFLOP_BETS_PER_STREET = value; },
};
`;
const sandbox = {
    PREFLOP,
    FLOP,
    TURN,
    RIVER,
    FOLD,
    CHECK,
    CALL,
    RAISE,
};

vm.createContext(sandbox);
vm.runInContext(candidateSource + exportSource, sandbox, {
    filename: candidatePath,
});

const hu = sandbox.__hu;


const threeToSixCandidatePath = path.resolve(
    __dirname,
    "../../scripts/hrc/tree-building-3m-6m-candidate.js",
);
const threeToSixCandidateSource = fs.readFileSync(
    threeToSixCandidatePath,
    "utf8",
);
const threeToSixExportSource = `
globalThis.__threeToSixPostflop = {
    getSizingsPostflop,
    canFlatCallPostflop,
    hasNextStreetBetting,
    get postflopBetsPerStreet() { return POSTFLOP_BETS_PER_STREET; },
    setPostflopBetsPerStreet(value) { POSTFLOP_BETS_PER_STREET = value; },
};
`;
const threeToSixSandbox = {
    PREFLOP,
    FLOP,
    TURN,
    RIVER,
    FOLD,
    CHECK,
    CALL,
    RAISE,
};

vm.createContext(threeToSixSandbox);
vm.runInContext(
    threeToSixCandidateSource + threeToSixExportSource,
    threeToSixSandbox,
    {filename: threeToSixCandidatePath},
);

const threeToSix = threeToSixSandbox.__threeToSixPostflop;


function makeAction(player, actionType, amount = 0, street = PREFLOP) {
    return {
        getActionType: () => actionType,
        getAmount: () => amount,
        getPlayer: () => player,
        getStreet: () => street,
    };
}


function makePotState({
    stacks = [100, 100],
    active,
    dead,
    remaining,
    folded,
    allIn,
} = {}) {
    const chipsActive = active ?? Array(stacks.length).fill(0);
    const chipsDead = dead ?? Array(stacks.length).fill(0);
    const playersFolded = folded ?? Array(stacks.length).fill(false);
    const chipsRemaining = remaining ?? stacks.map(
        (stack, player) => (
            stack - chipsActive[player] - chipsDead[player]
        ),
    );
    const playersAllIn = allIn ?? chipsRemaining.map((chips) => chips === 0);

    return {
        getChipsActive: (player) => chipsActive[player],
        getChipsDead: (player) => chipsDead[player],
        getChipsRemaining: (player) => chipsRemaining[player],
        hasPlayerFolded: (player) => playersFolded[player],
        isPlayerAllIn: (player) => playersAllIn[player],
        countPlayersLive: () => stacks.filter(
            (_, player) => !playersFolded[player] && !playersAllIn[player],
        ).length,
    };
}


function makeContext(overrides = {}) {
    const state = overrides.state ?? makePotState();
    const actions = overrides.actions ?? [];
    const fullActions = overrides.fullActions ?? actions;
    const lastRaiseAction = overrides.lastRaiseAction ?? [
        ...actions,
    ].reverse().find((action) => action.getActionType() === RAISE) ?? null;

    return {
        getActionSequence: () => actions,
        getActionSequenceFull: () => fullActions,
        getActivePlayer: () => overrides.activePlayer ?? 0,
        getBetCount: () => overrides.betCount ?? 1,
        getFlatCallCount: () => overrides.flatCallCount ?? 0,
        getLastRaiseAction: () => lastRaiseAction,
        getNumberOfPlayers: () => overrides.numberOfPlayers ?? 2,
        getPlayerIndexBigBlind: () => overrides.bigBlind ?? 1,
        getPlayerIndexButton: () => overrides.button ?? 0,
        getPlayerIndexSmallBlind: () => overrides.smallBlind ?? 0,
        getPotState: () => state,
        getStackPotRatio: () => overrides.spr ?? 10,
        getStreet: () => overrides.street ?? PREFLOP,
        isClosingAction: () => overrides.closingAction ?? false,
        isDonkBet: () => overrides.donk ?? false,
        isPlayerInPosition: () => false,
        sizingAllIn: () => overrides.allin ?? 100,
        sizingBigBlinds: (amount) => amount * (overrides.bbUnit ?? 1),
        sizingMinimum: () => overrides.minimum ?? 0,
        sizingPot: (fraction) => fraction * (overrides.potUnit ?? 100),
    };
}


function makeThreeToSixHeadsUpContext(overrides = {}) {
    const state = overrides.state ?? makePotState({
        stacks: [100, 100, 100],
        folded: [false, false, true],
    });

    return makeContext({
        ...overrides,
        state,
        numberOfPlayers: 3,
    });
}


function tablePayload() {
    return {
        stackGrid: Array.from(hu.HU_STACK_GRID),
        huTables: Object.fromEntries(
            Array.from(
                hu.HU_PREFLOP_SIZING_TABLES,
                (table) => [table.name, Array.from(table.values)],
            ),
        ),
    };
}


test("matches all 408 reviewed HU workbook policy cells", () => {
    const payload = tablePayload();
    const manifestHash = crypto
        .createHash("sha256")
        .update(JSON.stringify(payload))
        .digest("hex");

    assert.equal(payload.stackGrid.length, 68);
    assert.equal(Object.keys(payload.huTables).length, 6);
    assert.equal(manifestHash, (
        "d87c67f1e069f03b00618e360cd594d3554dee2d0128ce7df66072224282f6a9"
    ));
});


test("validates the HU cell grammar and workbook row composition", () => {
    const expected = {
        HU_RFI_SB: {allin: 14, single: 54, pair: 0},
        HU_RFI_BB: {allin: 6, single: 16, pair: 46},
        HU_THREEBET_SB: {allin: 38, single: 30, pair: 0},
        HU_THREEBET_BB: {allin: 14, single: 12, pair: 42},
        HU_FOURBET_SB: {allin: 61, single: 7, pair: 0},
        HU_FOURBET_BB: {allin: 65, single: 3, pair: 0},
    };

    for (const table of hu.HU_PREFLOP_SIZING_TABLES) {
        const counts = {allin: 0, single: 0, pair: 0};
        for (const cell of table.values) {
            if (cell === "allin") {
                counts.allin++;
                continue;
            }

            const tokens = String(cell).split(",");
            assert.ok(tokens.length === 1 || tokens.length === 2, cell);
            for (const token of tokens)
                assert.ok(Number.isFinite(Number(token)), cell);
            counts[tokens.length === 1 ? "single" : "pair"]++;
        }
        assert.deepEqual(counts, expected[table.name], table.name);
    }
});


test("resolves every HU workbook cell as fixed big-blind sizes", () => {
    const ctx = makeContext({allin: 100000, bbUnit: 100});

    for (const table of hu.HU_PREFLOP_SIZING_TABLES) {
        for (let index = 0; index < table.values.length; index++) {
            const cell = table.values[index];
            const expected = cell === "allin"
                ? [100000]
                : String(cell).split(",").map(Number).map((value) => value * 100);

            assert.deepEqual(
                Array.from(hu.resolveFixedBbCell(ctx, cell)),
                expected,
                `${table.name} at ${hu.HU_STACK_GRID[index]}bb`,
            );
        }
    }
});


test("uses every exact HU effective-stack bucket", () => {
    for (let index = 0; index < hu.HU_STACK_GRID.length; index++) {
        const stack = hu.HU_STACK_GRID[index];
        const ctx = makeContext({
            state: makePotState({stacks: [stack, stack]}),
            allin: stack,
        });
        assert.equal(hu.getStackBucketIndex(ctx, stack), index);
    }

    const scaled = makeContext({bbUnit: 5000});
    assert.equal(hu.getStackBucketIndex(scaled, 20 * 5000), 38);
    assert.throws(
        () => hu.getStackBucketIndex(makeContext(), 31),
        /does not match a configured workbook column/,
    );
});


test("calculates the HU effective stack from the sole opponent", () => {
    const ctx = makeContext({
        state: makePotState({stacks: [80, 20]}),
    });
    assert.equal(hu.getEffectiveStackForPlayer(ctx, 0), 20);
    assert.equal(hu.getEffectiveStackForPlayer(ctx, 1), 20);

    const reconstructed = makeContext({
        state: makePotState({
            stacks: [80, 20],
            active: [10, 5],
            dead: [2, 1],
            remaining: [68, 14],
        }),
    });
    assert.equal(hu.getPlayerStartingStack(reconstructed, 0), 80);
    assert.equal(hu.getPlayerStartingStack(reconstructed, 1), 20);

    const allinOpponent = makeContext({
        state: makePotState({
            stacks: [80, 20],
            active: [0, 20],
            allIn: [false, true],
        }),
    });
    assert.equal(hu.getEffectiveStackForPlayer(allinOpponent, 0), 20);

    const foldedOpponent = makeContext({
        state: makePotState({folded: [false, true]}),
    });
    assert.throws(
        () => hu.getEffectiveStackForPlayer(foldedOpponent, 0),
        /without an opponent/,
    );
});


test("routes the initial SB open and BB raise after an SB completion", () => {
    const sbOpen = makeContext({
        activePlayer: 0,
        state: makePotState({stacks: [20, 20], active: [0.5, 1]}),
        allin: 20,
        minimum: 2,
    });
    assert.deepEqual(Array.from(hu.getSizingsPreflop(sbOpen)), [2, 20]);

    const bbAfterLimp = makeContext({
        activePlayer: 1,
        actions: [makeAction(0, CALL, 1)],
        state: makePotState({stacks: [20, 20], active: [1, 1]}),
        allin: 20,
        minimum: 2,
    });
    assert.deepEqual(
        Array.from(hu.getSizingsPreflop(bbAfterLimp)),
        [3, 6, 20],
    );

    const invalidBbOpen = makeContext({
        activePlayer: 1,
        state: makePotState({stacks: [20, 20], active: [0.5, 1]}),
        allin: 20,
    });
    assert.throws(
        () => hu.getSizingsPreflop(invalidBbOpen),
        /requires an SB completion/,
    );
});


test("normalises the 4bb BB workbook raise after applying the threshold", () => {
    const ctx = makeContext({
        activePlayer: 1,
        actions: [makeAction(0, CALL, 1)],
        state: makePotState({stacks: [4, 4], active: [1, 1]}),
        allin: 4,
        minimum: 2,
    });

    assert.deepEqual(Array.from(hu.getSizingsPreflop(ctx)), [2, 4]);
});


test("routes both HU 3-bet lines", () => {
    const bbThreeBet = makeContext({
        activePlayer: 1,
        betCount: 2,
        actions: [makeAction(0, RAISE, 2)],
        state: makePotState({stacks: [20, 20], active: [2, 1]}),
        allin: 20,
        minimum: 3,
    });
    assert.deepEqual(
        Array.from(hu.getSizingsPreflop(bbThreeBet)),
        [5, 7, 20],
    );

    const sbLimpReraise = makeContext({
        activePlayer: 0,
        betCount: 2,
        actions: [
            makeAction(0, CALL, 1),
            makeAction(1, RAISE, 6),
        ],
        state: makePotState({stacks: [20, 20], active: [1, 6]}),
        allin: 20,
        minimum: 11,
    });
    assert.deepEqual(
        Array.from(hu.getSizingsPreflop(sbLimpReraise)),
        [11, 20],
    );
});


test("routes both HU 4-bet lines", () => {
    const sbFourBet = makeContext({
        activePlayer: 0,
        betCount: 3,
        actions: [
            makeAction(0, RAISE, 2.5),
            makeAction(1, RAISE, 9),
        ],
        state: makePotState({stacks: [70, 70], active: [2.5, 9]}),
        allin: 70,
        minimum: 15.5,
    });
    assert.deepEqual(
        Array.from(hu.getSizingsPreflop(sbFourBet)),
        [20, 70],
    );

    const bbFourBet = makeContext({
        activePlayer: 1,
        betCount: 3,
        actions: [
            makeAction(0, CALL, 1),
            makeAction(1, RAISE, 5),
            makeAction(0, RAISE, 15),
        ],
        state: makePotState({stacks: [80, 80], active: [15, 5]}),
        allin: 80,
        minimum: 25,
    });
    assert.deepEqual(
        Array.from(hu.getSizingsPreflop(bbFourBet)),
        [30, 80],
    );
});


test("rejects malformed HU 3-bet and 4-bet action topology", () => {
    const malformedThreeBet = makeContext({
        activePlayer: 1,
        betCount: 2,
        actions: [
            makeAction(0, CALL, 1),
            makeAction(1, RAISE, 5),
        ],
        state: makePotState({stacks: [40, 40], active: [1, 5]}),
        allin: 40,
    });
    assert.throws(
        () => hu.getSizingsPreflop(malformedThreeBet),
        /HU 3-bet decision has an invalid action line/,
    );

    const malformedFourBet = makeContext({
        activePlayer: 0,
        betCount: 3,
        actions: [
            makeAction(0, RAISE, 2.5),
            makeAction(1, RAISE, 9),
            makeAction(0, RAISE, 22),
        ],
        state: makePotState({stacks: [70, 70], active: [22, 9]}),
        allin: 70,
    });
    assert.throws(
        () => hu.getSizingsPreflop(malformedFourBet),
        /HU 4-bet decision has an invalid previous raise/,
    );
});


test("returns one all-in action for 5-bets and every later raise", () => {
    for (const betCount of [4, 5, 6, 12]) {
        const ctx = makeContext({
            betCount,
            state: makePotState({stacks: [20, 20]}),
            allin: 20,
        });
        assert.deepEqual(Array.from(hu.getSizingsPreflop(ctx)), [20]);
    }

    const shallow = makeContext({
        activePlayer: 0,
        state: makePotState({stacks: [1, 1], active: [0.5, 1]}),
        allin: 1,
        minimum: 1,
    });
    assert.deepEqual(Array.from(hu.getSizingsPreflop(shallow)), [1]);

    const alternatingFiveBet = makeContext({
        activePlayer: 1,
        betCount: 4,
        actions: [
            makeAction(0, RAISE, 2.5),
            makeAction(1, RAISE, 9),
            makeAction(0, RAISE, 22),
        ],
        state: makePotState({stacks: [100, 100], active: [22, 9]}),
        allin: 100,
        minimum: 35,
    });
    assert.deepEqual(
        Array.from(hu.getSizingsPreflop(alternatingFiveBet)),
        [100],
    );
});


test("uses 50% before legal normalisation and always adds preflop all-in", () => {
    const state = makePotState({active: [10, 0]});
    const ctx = makeContext({state, allin: 100, minimum: 60});

    assert.equal(hu.preflopAllinThreshold, 0.50);
    assert.deepEqual(
        Array.from(hu.applyAllinThreshold(ctx, [54.99, 55, 80])),
        [54.99, 100, 100],
    );
    assert.deepEqual(
        Array.from(
            hu.normalizeAndUniqueSizings(
                ctx,
                hu.applyAllinThreshold(ctx, [50]),
            ),
        ),
        [60],
    );
});


test("clamps and deduplicates HU sizings at both legal bounds", () => {
    const ctx = makeContext({minimum: 10, allin: 100});

    assert.deepEqual(
        Array.from(hu.normalizeAndUniqueSizings(
            ctx,
            [-1, 1, 10, 150, 100],
        )),
        [10, 100],
    );
    assert.deepEqual(
        Array.from(hu.normalizeAndUniqueSizings(ctx, [3, 7, 100])),
        [10, 100],
    );
});


test("enforces HU preflop call topology", () => {
    assert.equal(hu.canFlatCallPreflop(makeContext({
        activePlayer: 0,
        betCount: 1,
    })), true);
    assert.equal(hu.canFlatCallPreflop(makeContext({
        activePlayer: 1,
        betCount: 1,
        actions: [makeAction(0, CALL, 1)],
    })), false);
    const cappedCallCases = [
        {
            label: "BB calls the SB open",
            activePlayer: 1,
            betCount: 2,
            actions: [makeAction(0, RAISE, 2)],
        },
        {
            label: "SB calls the BB raise after completing",
            activePlayer: 0,
            betCount: 2,
            actions: [
                makeAction(0, CALL, 1),
                makeAction(1, RAISE, 5),
            ],
        },
        {
            label: "SB calls the BB 3-bet",
            activePlayer: 0,
            betCount: 3,
            actions: [
                makeAction(0, RAISE, 2),
                makeAction(1, RAISE, 7),
            ],
        },
        {
            label: "BB calls the SB limp-reraise",
            activePlayer: 1,
            betCount: 3,
            actions: [
                makeAction(0, CALL, 1),
                makeAction(1, RAISE, 5),
                makeAction(0, RAISE, 15),
            ],
        },
        {
            label: "BB calls the SB 4-bet",
            activePlayer: 1,
            betCount: 4,
            actions: [
                makeAction(0, RAISE, 2),
                makeAction(1, RAISE, 7),
                makeAction(0, RAISE, 18),
            ],
        },
        {
            label: "SB calls the BB 4-bet after completing",
            activePlayer: 0,
            betCount: 4,
            actions: [
                makeAction(0, CALL, 1),
                makeAction(1, RAISE, 5),
                makeAction(0, RAISE, 15),
                makeAction(1, RAISE, 35),
            ],
        },
    ];

    for (const callCase of cappedCallCases) {
        assert.equal(
            hu.canFlatCallPreflop(makeContext(callCase)),
            true,
            callCase.label,
        );
        assert.equal(
            hu.canFlatCallPreflop(makeContext({
                ...callCase,
                flatCallCount: 1,
            })),
            false,
            `${callCase.label} after the one-call cap`,
        );
    }

    assert.equal(hu.canFlatCallPreflop(makeContext({
        activePlayer: 0,
        betCount: 3,
        actions: [makeAction(1, RAISE, 7)],
    })), false);
    assert.equal(hu.canFlatCallPreflop(makeContext({
        activePlayer: 0,
        betCount: 5,
        closingAction: true,
        actions: [makeAction(0, RAISE, 2)],
    })), true);
    assert.equal(hu.canFlatCallPreflop(makeContext({
        activePlayer: 0,
        betCount: 5,
        closingAction: false,
        actions: [makeAction(0, RAISE, 2)],
    })), false);
});


test("matches all normal-SPR HU postflop rows", () => {
    const matrices = [
        [FLOP, "bet", [25, 40, 67, 100]],
        [FLOP, "raise", [33, 75]],
        [FLOP, "donk", [33, 75]],
        [TURN, "bet", [40, 80, 160]],
        [TURN, "raise", [40, 80, 160]],
        [TURN, "donk", [33, 75]],
        [RIVER, "bet", [40, 80, 160, 320]],
        [RIVER, "raise", [50, 100, 250]],
        [RIVER, "donk", [40, 80, 160, 320]],
    ];

    for (const [street, action, expected] of matrices) {
        const donk = action === "donk";
        const ctx = makeContext({
            betCount: action === "raise" ? 1 : 0,
            street,
            spr: 10,
            allin: 1000,
            donk,
            fullActions: donk ? [makeAction(0, RAISE, 2)] : [],
        });
        assert.deepEqual(
            Array.from(hu.getSizingsPostflop(ctx)),
            expected,
            `${street} ${action}`,
        );
    }
});


test("matches HU low-SPR and postflop all-in boundaries", () => {
    const lowBet = makeContext({
        betCount: 0,
        street: TURN,
        spr: 2.5,
        allin: 1000,
    });
    const lowRaise = makeContext({
        betCount: 1,
        street: TURN,
        spr: 2.5,
        allin: 1000,
    });
    const addAllin = makeContext({
        betCount: 0,
        street: FLOP,
        spr: 5,
        allin: 1000,
    });
    const noAllin = makeContext({
        betCount: 0,
        street: FLOP,
        spr: 5.01,
        allin: 1000,
    });

    assert.deepEqual(
        Array.from(hu.getSizingsPostflop(lowBet)),
        [10, 25, 40, 67, 100, 150, 1000],
    );
    assert.deepEqual(
        Array.from(hu.getSizingsPostflop(lowRaise)),
        [10, 25, 40, 67, 100, 1000],
    );
    assert.deepEqual(
        Array.from(hu.getSizingsPostflop(addAllin)),
        [25, 40, 67, 100, 1000],
    );
    assert.deepEqual(
        Array.from(hu.getSizingsPostflop(noAllin)),
        [25, 40, 67, 100],
    );
});


test("deduplicates HU low-SPR sizes after legal clamping", () => {
    const collision = makeContext({
        betCount: 0,
        street: TURN,
        spr: 0.3,
        minimum: 20,
        allin: 30,
    });

    assert.deepEqual(
        Array.from(hu.getSizingsPostflop(collision)),
        [20, 25, 30],
    );
});


test("enforces Limited donks before a future bets-per-street cap", () => {
    const denied = makeContext({
        betCount: 0,
        street: FLOP,
        spr: 10,
        allin: 100,
        donk: true,
        fullActions: [],
    });
    const allowed = makeContext({
        betCount: 0,
        street: RIVER,
        spr: 2.5,
        allin: 1000,
        donk: true,
        fullActions: [makeAction(0, RAISE, 2)],
    });

    assert.deepEqual(Array.from(hu.getSizingsPostflop(denied)), []);
    assert.deepEqual(
        Array.from(hu.getSizingsPostflop(allowed)),
        [10, 25, 40, 67, 100, 150, 1000],
    );

    hu.setPostflopBetsPerStreet(0);
    try {
        assert.deepEqual(Array.from(hu.getSizingsPostflop(denied)), []);
    }
    finally {
        hu.setPostflopBetsPerStreet(null);
    }
});


test("keeps blank HU bets-per-street unlimited and supports a future cap", () => {
    const unlimited = makeContext({
        betCount: 5,
        street: FLOP,
        spr: 10,
        allin: 1000,
    });
    const capped = makeContext({
        betCount: 2,
        street: FLOP,
        spr: 10,
        allin: 100,
    });

    assert.equal(hu.postflopBetsPerStreet, null);
    assert.deepEqual(
        Array.from(hu.getSizingsPostflop(unlimited)),
        [33, 75],
    );

    hu.setPostflopBetsPerStreet(2);
    try {
        assert.deepEqual(Array.from(hu.getSizingsPostflop(capped)), [100]);
    }
    finally {
        hu.setPostflopBetsPerStreet(null);
    }
});


test("makes HU postflop calls and street horizon explicit", () => {
    const turn = makeContext({street: TURN});
    const river = makeContext({street: RIVER});
    const oneLive = makeContext({
        street: TURN,
        state: makePotState({folded: [false, true]}),
    });

    assert.equal(hu.canFlatCallPostflop(turn), true);
    assert.equal(hu.hasNextStreetBetting(turn), true);
    assert.equal(hu.hasNextStreetBetting(river), false);
    assert.equal(hu.hasNextStreetBetting(oneLive), false);
    assert.deepEqual(Array.from(hu.getSizingsPostflop(oneLive)), []);
});


test("matches the 3m-6m candidate across shared HU postflop vectors", () => {
    const sharedVectors = [
        {label: "flop bet", betCount: 0, street: FLOP, spr: 10},
        {label: "turn raise", betCount: 1, street: TURN, spr: 10},
        {
            label: "river donk after aggression",
            betCount: 0,
            street: RIVER,
            spr: 10,
            donk: true,
            fullActions: [makeAction(0, RAISE, 2)],
        },
        {
            label: "denied flop donk",
            betCount: 0,
            street: FLOP,
            spr: 10,
            donk: true,
            fullActions: [],
        },
        {label: "low-SPR bet", betCount: 0, street: TURN, spr: 2.5},
        {label: "low-SPR raise", betCount: 1, street: TURN, spr: 2.5},
        {label: "all-in boundary", betCount: 0, street: FLOP, spr: 5},
        {label: "above all-in boundary", betCount: 0, street: FLOP, spr: 5.01},
        {
            label: "legal-bound collision",
            betCount: 0,
            street: TURN,
            spr: 0.3,
            minimum: 20,
            allin: 30,
        },
        {label: "unlimited fifth raise", betCount: 5, street: FLOP, spr: 10},
    ];

    for (const vector of sharedVectors) {
        const huResult = Array.from(
            hu.getSizingsPostflop(makeContext(vector)),
        );
        const threeToSixResult = Array.from(
            threeToSix.getSizingsPostflop(
                makeThreeToSixHeadsUpContext(vector),
            ),
        );
        assert.deepEqual(huResult, threeToSixResult, vector.label);
    }

    hu.setPostflopBetsPerStreet(2);
    threeToSix.setPostflopBetsPerStreet(2);
    try {
        const capped = {betCount: 2, street: FLOP, spr: 10, allin: 100};
        assert.deepEqual(
            Array.from(hu.getSizingsPostflop(makeContext(capped))),
            Array.from(threeToSix.getSizingsPostflop(
                makeThreeToSixHeadsUpContext(capped),
            )),
        );
    }
    finally {
        hu.setPostflopBetsPerStreet(null);
        threeToSix.setPostflopBetsPerStreet(null);
    }

    for (const street of [FLOP, TURN, RIVER]) {
        const huContext = makeContext({street});
        const threeToSixContext = makeThreeToSixHeadsUpContext({street});
        assert.equal(
            hu.canFlatCallPostflop(huContext),
            threeToSix.canFlatCallPostflop(threeToSixContext),
            `postflop call on street ${street}`,
        );
        assert.equal(
            hu.hasNextStreetBetting(huContext),
            threeToSix.hasNextStreetBetting(threeToSixContext),
            `street horizon on street ${street}`,
        );
    }
});


test("accepts only a two-player HRC configuration", () => {
    const supported = makeContext({
        state: makePotState({stacks: [80, 80]}),
        allin: 80,
    });
    assert.doesNotThrow(() => hu.getSizingsPreflop(supported));
    assert.throws(
        () => hu.getSizingsPreflop(makeContext({numberOfPlayers: 3})),
        /supports heads-up configurations only/,
    );
    assert.throws(
        () => hu.getSizingsPreflop(makeContext({
            smallBlind: 1,
            bigBlind: 1,
        })),
        /small blind and big blind must be different/,
    );
});
