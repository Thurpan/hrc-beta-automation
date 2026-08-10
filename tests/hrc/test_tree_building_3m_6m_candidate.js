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
    "../../scripts/hrc/tree-building-3m-6m-candidate.js",
);

const candidateSource = fs.readFileSync(candidatePath, "utf8");
const referencePath = path.resolve(
    __dirname,
    "../../reference/hrc/shared-chatgpt-prototype.js",
);
const exportSource = `
globalThis.__hrc = {
    PREFLOP_STACK_GRID,
    PREFLOP_SIZING_TABLES,
    THREEBET_IP_VS_2_25,
    getPlayerStartingStack,
    getEffectiveStackForPlayer,
    getHistoricalOpenEffectiveStack,
    getStackBucketIndex,
    getOriginalOpenInfo,
    getSqueezeAdjustmentBb,
    normalizeAndUniqueSizings,
    applyAllinThreshold,
    getSizingsPreflop,
    getSizingsPostflop,
    canFlatCallPreflop,
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

const hrc = sandbox.__hrc;


test("preserves the archived shared-thread snapshot", () => {
    const referenceHash = crypto
        .createHash("sha256")
        .update(fs.readFileSync(referencePath))
        .digest("hex");

    assert.equal(
        referenceHash,
        "f39e83006039b26f27beed4c7f0f8e08d6929cfcf31d3b5deeadd2a448448f37",
    );
});


function makeAction(player, actionType, amount = 0, street = PREFLOP) {
    return {
        getActionType: () => actionType,
        getAmount: () => amount,
        getPlayer: () => player,
        getStreet: () => street,
    };
}


function makePotState({
    stacks = [100, 100, 100, 100, 100],
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

    const playersAllIn = allIn ?? chipsRemaining.map(
        (chips) => chips === 0,
    );

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
        getNumberOfPlayers: () => overrides.numberOfPlayers ?? 5,
        getPlayerIndexBigBlind: () => overrides.bigBlind ?? 4,
        getPlayerIndexButton: () => overrides.button ?? 2,
        getPlayerIndexSmallBlind: () => overrides.smallBlind ?? 3,
        getPotState: () => state,
        getStackPotRatio: () => overrides.spr ?? 10,
        getStreet: () => overrides.street ?? PREFLOP,
        isClosingAction: () => overrides.closingAction ?? false,
        isDonkBet: () => overrides.donk ?? false,
        isPlayerInPosition: (player, raiser) => (
            overrides.isPlayerInPosition?.(player, raiser) ?? false
        ),
        sizingAllIn: () => overrides.allin ?? 100,
        sizingBigBlinds: (amount) => amount * (overrides.bbUnit ?? 1),
        sizingMinimum: () => overrides.minimum ?? 0,
        sizingPot: (fraction) => fraction * (overrides.potUnit ?? 100),
        sizingsPreflop: (text) => {
            if (text.endsWith("bb")) {
                return [
                    Number.parseFloat(text) * (overrides.bbUnit ?? 1),
                ];
            }

            if (text.endsWith("x")) {
                return [Number.parseFloat(text) * 10];
            }

            throw new Error(`Unsupported mock preflop sizing: ${text}`);
        },
    };
}


test("uses 50% of the distance from active chips to all-in", () => {
    const state = makePotState({
        active: [10, 0, 0, 0, 0],
    });
    const ctx = makeContext({state, allin: 100});

    assert.equal(hrc.preflopAllinThreshold, 0.50);
    assert.deepEqual(
        Array.from(hrc.applyAllinThreshold(ctx, [54.99, 55, 80])),
        [54.99, 100, 100],
    );
});


test("calculates dynamic effective stacks from non-folded opponents", () => {
    const beforeFold = makeContext({
        activePlayer: 0,
        state: makePotState({
            stacks: [100, 10, 100, 10, 10],
            folded: [false, true, false, false, false],
        }),
    });

    const afterFold = makeContext({
        activePlayer: 2,
        state: makePotState({
            stacks: [100, 10, 100, 10, 10],
            folded: [true, true, false, false, false],
        }),
    });

    assert.equal(hrc.getEffectiveStackForPlayer(beforeFold, 0), 100);
    assert.equal(hrc.getEffectiveStackForPlayer(afterFold, 2), 10);
});


test("calculates effective stacks at the 3m and 6m boundaries", () => {
    const threeHanded = makeContext({
        numberOfPlayers: 3,
        activePlayer: 0,
        button: 0,
        smallBlind: 1,
        bigBlind: 2,
        state: makePotState({stacks: [100, 40, 10]}),
    });
    const sixHanded = makeContext({
        numberOfPlayers: 6,
        activePlayer: 0,
        button: 3,
        smallBlind: 4,
        bigBlind: 5,
        state: makePotState({stacks: [100, 20, 30, 40, 50, 60]}),
    });

    assert.equal(hrc.getEffectiveStackForPlayer(threeHanded, 0), 40);
    assert.equal(hrc.getEffectiveStackForPlayer(sixHanded, 0), 60);
});


test("includes an all-in opponent in the effective stack", () => {
    const ctx = makeContext({
        activePlayer: 0,
        state: makePotState({
            stacks: [100, 40, 10, 10, 10],
            active: [0, 40, 0, 0, 0],
            folded: [false, false, true, true, true],
            allIn: [false, true, false, false, false],
        }),
    });

    assert.equal(hrc.getEffectiveStackForPlayer(ctx, 0), 40);
});


test("rejects an effective stack without a non-folded opponent", () => {
    const ctx = makeContext({
        state: makePotState({
            folded: [false, true, true, true, true],
        }),
    });

    assert.throws(
        () => hrc.getEffectiveStackForPlayer(ctx, 0),
        /without a non-folded opponent/,
    );
});


test("requires every effective stack to match an exact workbook bucket", () => {
    const bbUnit = 5000;
    const ctx = makeContext({bbUnit});

    hrc.PREFLOP_STACK_GRID.forEach((stack, index) => {
        assert.equal(
            hrc.getStackBucketIndex(ctx, stack * bbUnit),
            index,
        );
    });

    assert.throws(
        () => hrc.getStackBucketIndex(ctx, 6 * bbUnit),
        /does not match a configured workbook column/,
    );

    const unsupportedCallback = makeContext({
        activePlayer: 2,
        betCount: 1,
        state: makePotState({stacks: [6, 6, 6, 6, 6]}),
        allin: 6,
    });

    assert.throws(
        () => hrc.getSizingsPreflop(unsupportedCallback),
        /does not match a configured workbook column/,
    );
});


test("keeps all preflop tables aligned to the 18 stack columns", () => {
    assert.equal(hrc.PREFLOP_STACK_GRID.length, 18);

    for (const table of hrc.PREFLOP_SIZING_TABLES) {
        assert.equal(table.values.length, 18, table.name);
    }

    assert.equal(hrc.THREEBET_IP_VS_2_25[14], "7.5");
});


test("matches the reviewed workbook-derived table manifest", () => {
    const tablePayload = Array.from(
        hrc.PREFLOP_SIZING_TABLES,
        (table) => [table.name, Array.from(table.values)],
    );
    const tableHash = crypto
        .createHash("sha256")
        .update(JSON.stringify(tablePayload))
        .digest("hex");

    assert.equal(
        tableHash,
        "de418802b7f73bdc4796897d4abbe2f04e9610c4d7db5910dccd35181c1a23d1",
    );
});


test("always offers all-in alongside a deep configured opening size", () => {
    const ctx = makeContext({
        activePlayer: 2,
        betCount: 1,
        state: makePotState(),
        allin: 100,
    });

    assert.deepEqual(
        Array.from(hrc.getSizingsPreflop(ctx)),
        [2.5, 100],
    );
});


test("routes every five-player opening position at 100bb", () => {
    const expectedByPlayer = new Map([
        [0, 2.25],
        [1, 2.25],
        [2, 2.5],
        [3, 3],
        [4, 3],
    ]);

    for (const [activePlayer, expectedSize] of expectedByPlayer) {
        const ctx = makeContext({
            activePlayer,
            betCount: 1,
            state: makePotState(),
            allin: 100,
        });

        assert.deepEqual(
            Array.from(hrc.getSizingsPreflop(ctx)),
            [expectedSize, 100],
        );
    }
});


test("routes opening positions for every supported table size", () => {
    const configurations = [
        {
            count: 3,
            button: 0,
            smallBlind: 1,
            bigBlind: 2,
            expected: [2.5, 3, 3],
            shallowExpected: [2, 2.5, 2.5],
        },
        {
            count: 4,
            button: 1,
            smallBlind: 2,
            bigBlind: 3,
            expected: [2.25, 2.5, 3, 3],
            shallowExpected: [2, 2, 2.5, 2.5],
        },
        {
            count: 5,
            button: 2,
            smallBlind: 3,
            bigBlind: 4,
            expected: [2.25, 2.25, 2.5, 3, 3],
            shallowExpected: [2, 2, 2, 2.5, 2.5],
        },
        {
            count: 6,
            button: 3,
            smallBlind: 4,
            bigBlind: 5,
            expected: [2.25, 2.25, 2.25, 2.5, 3, 3],
            shallowExpected: [2, 2, 2, 2, 2.5, 2.5],
        },
    ];

    for (const configuration of configurations) {
        for (const stack of [10, 100]) {
            const stacks = Array(configuration.count).fill(stack);
            for (
                let activePlayer = 0;
                activePlayer < configuration.count;
                activePlayer++
            ) {
                const ctx = makeContext({
                    numberOfPlayers: configuration.count,
                    button: configuration.button,
                    smallBlind: configuration.smallBlind,
                    bigBlind: configuration.bigBlind,
                    activePlayer,
                    betCount: 1,
                    state: makePotState({stacks}),
                    allin: stack,
                });
                const expected = stack === 10
                    ? configuration.shallowExpected[activePlayer]
                    : configuration.expected[activePlayer];

                assert.deepEqual(
                    Array.from(hrc.getSizingsPreflop(ctx)),
                    [expected, stack],
                    `${configuration.count}m player ${activePlayer} at ${stack}bb`,
                );
            }
        }
    }
});


test("routes nonblind IP 3-bets in 4m and 6m configurations", () => {
    const configurations = [
        {
            label: "4m BTN versus CO",
            count: 4,
            button: 1,
            smallBlind: 2,
            bigBlind: 3,
            opener: 0,
            activePlayer: 1,
        },
        {
            label: "6m HJ versus UTG",
            count: 6,
            button: 3,
            smallBlind: 4,
            bigBlind: 5,
            opener: 0,
            activePlayer: 1,
        },
    ];

    for (const configuration of configurations) {
        const stacks = Array(configuration.count).fill(60);
        const active = Array(configuration.count).fill(0);
        active[configuration.opener] = 2.25;
        active[configuration.smallBlind] = 0.5;
        active[configuration.bigBlind] = 1;

        const ctx = makeContext({
            numberOfPlayers: configuration.count,
            button: configuration.button,
            smallBlind: configuration.smallBlind,
            bigBlind: configuration.bigBlind,
            activePlayer: configuration.activePlayer,
            betCount: 2,
            actions: [makeAction(configuration.opener, RAISE, 2.25)],
            state: makePotState({stacks, active}),
            allin: 60,
        });

        assert.deepEqual(
            Array.from(hrc.getSizingsPreflop(ctx)),
            [7.5, 60],
            configuration.label,
        );
    }
});


test("uses corrected P29 through the full IP 3-bet callback", () => {
    const ctx = makeContext({
        activePlayer: 1,
        betCount: 2,
        actions: [makeAction(0, RAISE, 2.25)],
        state: makePotState({
            stacks: [60, 60, 60, 60, 60],
            active: [2.25, 0, 0, 0, 0],
        }),
        allin: 60,
    });

    assert.deepEqual(Array.from(hrc.getSizingsPreflop(ctx)), [7.5, 60]);
});


test("returns all-in only for every 5-bet and later raise", () => {
    for (const betCount of [4, 5, 6, 7, 12]) {
        for (const stack of hrc.PREFLOP_STACK_GRID) {
            const ctx = makeContext({
                activePlayer: 0,
                betCount,
                state: makePotState({
                    stacks: [stack, stack, stack, stack, stack],
                }),
                allin: stack,
            });

            assert.deepEqual(
                Array.from(hrc.getSizingsPreflop(ctx)),
                [stack],
            );
        }
    }
});


test("routes both blind-versus-blind 3-bet directions", () => {
    const cases = [
        {
            label: "BB versus a 2.5bb SB open",
            stack: 20,
            activePlayer: 4,
            actions: [makeAction(3, RAISE, 2.5)],
            active: [0, 0, 0, 2.5, 1],
            expected: [6, 20],
        },
        {
            label: "BB versus a 3bb SB open",
            stack: 30,
            activePlayer: 4,
            actions: [makeAction(3, RAISE, 3)],
            active: [0, 0, 0, 3, 1],
            expected: [7.5, 30],
        },
        {
            label: "SB versus a 2.5bb BB open",
            stack: 15,
            activePlayer: 3,
            actions: [
                makeAction(3, CALL, 1),
                makeAction(4, RAISE, 2.5),
            ],
            active: [0, 0, 0, 1, 2.5],
            expected: [5, 15],
        },
        {
            label: "SB versus a 3bb BB open",
            stack: 20,
            activePlayer: 3,
            actions: [
                makeAction(3, CALL, 1),
                makeAction(4, RAISE, 3),
            ],
            active: [0, 0, 0, 1, 3],
            expected: [7, 20],
        },
    ];

    for (const testCase of cases) {
        const ctx = makeContext({
            activePlayer: testCase.activePlayer,
            betCount: 2,
            actions: testCase.actions,
            state: makePotState({
                stacks: Array(5).fill(testCase.stack),
                active: testCase.active,
            }),
            allin: testCase.stack,
        });

        assert.deepEqual(
            Array.from(hrc.getSizingsPreflop(ctx)),
            testCase.expected,
            testCase.label,
        );
    }
});


test("uses the ordinary 3-bet table only for the recorded ordinary open", () => {
    const ordinaryOpen = makeAction(0, RAISE, 2);
    const state = makePotState({
        stacks: [20, 100, 100, 100, 100],
        active: [2, 0, 0, 0, 0],
    });
    const ctx = makeContext({
        activePlayer: 1,
        betCount: 2,
        actions: [ordinaryOpen],
        state,
        allin: 100,
    });

    const openInfo = hrc.getOriginalOpenInfo(ctx);

    assert.equal(openInfo.expectedValue, "2");
    assert.equal(openInfo.actualAmount, 2);
    assert.equal(openInfo.isConfiguredOrdinary, true);
    assert.deepEqual(
        Array.from(hrc.getSizingsPreflop(ctx)),
        [7.5, 100],
    );
});


test("does not misclassify an optional all-in open", () => {
    const allinOpen = makeAction(0, RAISE, 20);
    const state = makePotState({
        stacks: [20, 100, 100, 100, 100],
        active: [20, 0, 0, 0, 0],
        allIn: [true, false, false, false, false],
    });
    const ctx = makeContext({
        activePlayer: 1,
        betCount: 2,
        actions: [allinOpen],
        state,
        allin: 100,
    });

    const openInfo = hrc.getOriginalOpenInfo(ctx);

    assert.equal(openInfo.expectedValue, "2");
    assert.equal(openInfo.actualAmount, 20);
    assert.equal(openInfo.isConfiguredOrdinary, false);
    assert.deepEqual(Array.from(hrc.getSizingsPreflop(ctx)), [100]);
});


test("classifies ordinary opens in scaled HRC chip units", () => {
    const bbUnit = 5000;
    const ordinaryOpen = makeAction(0, RAISE, 2 * bbUnit);
    const ctx = makeContext({
        activePlayer: 1,
        betCount: 2,
        actions: [ordinaryOpen],
        state: makePotState({
            stacks: [20, 100, 100, 100, 100].map(
                (stack) => stack * bbUnit,
            ),
            active: [2 * bbUnit, 0, 0, 0, 0],
        }),
        allin: 100 * bbUnit,
        bbUnit,
    });

    assert.equal(hrc.getOriginalOpenInfo(ctx).isConfiguredOrdinary, true);
    assert.deepEqual(
        Array.from(hrc.getSizingsPreflop(ctx)),
        [7.5 * bbUnit, 100 * bbUnit],
    );
});


test("treats every non-ordinary open as all-in response only", () => {
    const mandatoryAllin = makeContext({
        activePlayer: 1,
        betCount: 2,
        actions: [makeAction(0, RAISE, 5)],
        state: makePotState({
            stacks: [5, 100, 100, 100, 100],
            active: [5, 0, 0, 0, 0],
            allIn: [true, false, false, false, false],
        }),
    });
    const effectiveAllinWithChipsBehind = makeContext({
        activePlayer: 1,
        betCount: 2,
        actions: [makeAction(0, RAISE, 20)],
        state: makePotState({
            stacks: [100, 100, 20, 20, 20],
            active: [20, 0, 0, 0, 0],
            allIn: [false, false, false, false, false],
        }),
    });
    const legallyChangedOpen = makeContext({
        activePlayer: 1,
        betCount: 2,
        actions: [makeAction(0, RAISE, 2.1)],
        state: makePotState({
            stacks: [20, 100, 100, 100, 100],
            active: [2.1, 0, 0, 0, 0],
        }),
    });

    assert.deepEqual(
        Array.from(hrc.getSizingsPreflop(mandatoryAllin)),
        [100],
    );
    assert.deepEqual(
        Array.from(hrc.getSizingsPreflop(effectiveAllinWithChipsBehind)),
        [100],
    );
    assert.deepEqual(
        Array.from(hrc.getSizingsPreflop(legallyChangedOpen)),
        [100],
    );
});


test("rejects invalid or missing recorded opening actions", () => {
    const invalidAmount = makeContext({
        activePlayer: 1,
        betCount: 2,
        actions: [makeAction(0, RAISE, Number.NaN)],
        state: makePotState({stacks: [20, 100, 100, 100, 100]}),
    });
    const missingOpen = makeContext({
        activePlayer: 1,
        betCount: 2,
        actions: [],
    });

    assert.throws(
        () => hrc.getSizingsPreflop(invalidAmount),
        /invalid recorded amount/,
    );
    assert.throws(
        () => hrc.getSizingsPreflop(missingOpen),
        /has no original preflop raise/,
    );
});


test("preserves the opener's historical bucket after later folds", () => {
    const ctx = makeContext({
        activePlayer: 3,
        betCount: 2,
        actions: [
            makeAction(0, RAISE, 2.25),
            makeAction(1, FOLD),
            makeAction(2, FOLD),
        ],
        state: makePotState({
            stacks: [100, 100, 100, 10, 10],
            active: [2.25, 0, 0, 0, 0],
            folded: [false, true, true, false, false],
        }),
        allin: 10,
    });

    const openInfo = hrc.getOriginalOpenInfo(ctx);

    assert.equal(openInfo.expectedValue, "2.25");
    assert.equal(openInfo.isConfiguredOrdinary, true);
});


test("applies squeeze increments at the 40bb pairwise boundary", () => {
    const actions = [
        makeAction(0, RAISE, 2),
        makeAction(1, CALL, 2),
    ];
    const below = makeContext({
        activePlayer: 4,
        flatCallCount: 1,
        actions,
        state: makePotState({stacks: [100, 39.9, 100, 100, 100]}),
    });
    const atBoundary = makeContext({
        activePlayer: 4,
        flatCallCount: 1,
        actions,
        state: makePotState({stacks: [100, 40, 100, 100, 100]}),
    });

    assert.equal(hrc.getSqueezeAdjustmentBb(below), 0.5);
    assert.equal(hrc.getSqueezeAdjustmentBb(atBoundary), 1.0);
});


test("uses the first post-open caller for every squeeze increment", () => {
    const actions = [
        makeAction(3, CALL, 1),
        makeAction(0, RAISE, 2),
        makeAction(1, CALL, 2),
        makeAction(2, CALL, 2),
    ];
    const below = makeContext({
        activePlayer: 4,
        flatCallCount: 2,
        actions,
        state: makePotState({stacks: [100, 30, 100, 100, 100]}),
    });
    const above = makeContext({
        activePlayer: 4,
        flatCallCount: 2,
        actions,
        state: makePotState({stacks: [100, 40, 30, 100, 100]}),
    });
    const noCaller = makeContext({
        activePlayer: 4,
        flatCallCount: 0,
        actions: [makeAction(0, RAISE, 2)],
    });

    assert.equal(hrc.getSqueezeAdjustmentBb(below), 1.0);
    assert.equal(hrc.getSqueezeAdjustmentBb(above), 1.5);
    assert.equal(hrc.getSqueezeAdjustmentBb(noCaller), 0);
});


test("applies the squeeze increment before the 50% replacement", () => {
    const ctx = makeContext({
        activePlayer: 4,
        betCount: 2,
        flatCallCount: 1,
        actions: [
            makeAction(0, RAISE, 2.1),
            makeAction(1, CALL, 2.1),
        ],
        state: makePotState({
            stacks: [50, 50, 50, 50, 50],
            active: [2.1, 2.1, 0, 0.5, 1],
        }),
        allin: 18,
    });

    assert.deepEqual(Array.from(hrc.getSizingsPreflop(ctx)), [18]);
});


test("keeps a shallow all-in table entry unchanged for a squeeze", () => {
    const ctx = makeContext({
        activePlayer: 4,
        betCount: 2,
        flatCallCount: 1,
        actions: [
            makeAction(0, RAISE, 2.25),
            makeAction(1, CALL, 2.25),
        ],
        state: makePotState({
            stacks: [100, 100, 100, 100, 10],
            active: [2.25, 2.25, 0, 0.5, 1],
        }),
        allin: 10,
    });

    assert.deepEqual(Array.from(hrc.getSizingsPreflop(ctx)), [10]);
});


test("enforces hard caller caps even when the action closes", () => {
    const openCap = makeContext({
        betCount: 2,
        flatCallCount: 2,
        closingAction: true,
    });
    const threeBetCap = makeContext({
        activePlayer: 0,
        betCount: 3,
        flatCallCount: 1,
        closingAction: true,
        actions: [
            makeAction(0, RAISE, 2),
            makeAction(1, RAISE, 8),
        ],
    });

    assert.equal(hrc.canFlatCallPreflop(openCap), false);
    assert.equal(hrc.canFlatCallPreflop(threeBetCap), false);
});


test("rejects cold calls and permits one closing 5-bet response", () => {
    const coldCall = makeContext({
        activePlayer: 2,
        betCount: 3,
        flatCallCount: 0,
        closingAction: true,
        actions: [
            makeAction(0, RAISE, 2),
            makeAction(1, RAISE, 8),
        ],
    });
    const closingFiveBetCall = makeContext({
        activePlayer: 0,
        betCount: 5,
        flatCallCount: 0,
        closingAction: true,
        actions: [
            makeAction(0, RAISE, 2),
            makeAction(1, RAISE, 8),
            makeAction(0, RAISE, 20),
            makeAction(1, RAISE, 100),
        ],
    });

    assert.equal(hrc.canFlatCallPreflop(coldCall), false);
    assert.equal(hrc.canFlatCallPreflop(closingFiveBetCall), true);
});


test("applies caller caps at every preflop raise level", () => {
    const priorAction = [makeAction(0, RAISE, 2)];

    for (const [betCount, allowedFlats] of [
        [2, [0, 1]],
        [3, [0]],
        [4, [0]],
    ]) {
        for (const flatCallCount of allowedFlats) {
            assert.equal(
                hrc.canFlatCallPreflop(makeContext({
                    activePlayer: 0,
                    betCount,
                    flatCallCount,
                    actions: priorAction,
                })),
                true,
            );
        }

        const cap = betCount === 2 ? 2 : 1;
        assert.equal(
            hrc.canFlatCallPreflop(makeContext({
                activePlayer: 0,
                betCount,
                flatCallCount: cap,
                closingAction: true,
                actions: priorAction,
            })),
            false,
        );
    }

    for (const betCount of [5, 6, 7, 12]) {
        assert.equal(
            hrc.canFlatCallPreflop(makeContext({
                activePlayer: 0,
                betCount,
                flatCallCount: 0,
                closingAction: true,
                actions: priorAction,
            })),
            true,
        );
        assert.equal(
            hrc.canFlatCallPreflop(makeContext({
                activePlayer: 2,
                betCount,
                flatCallCount: 0,
                closingAction: true,
                actions: priorAction,
            })),
            false,
        );
    }
});


test("uses the blind 4-bet table only for a blind opening line", () => {
    const blindLine = makeContext({
        activePlayer: 3,
        betCount: 3,
        actions: [
            makeAction(3, RAISE, 3),
            makeAction(4, RAISE, 9),
        ],
        state: makePotState({
            stacks: [50, 50, 50, 50, 50],
            active: [0, 0, 0, 3, 9],
        }),
        allin: 100,
    });
    const squeezedNonBlindLine = makeContext({
        activePlayer: 3,
        betCount: 3,
        actions: [
            makeAction(2, RAISE, 2.25),
            makeAction(3, CALL, 2.25),
            makeAction(4, RAISE, 12),
        ],
        state: makePotState({
            stacks: [50, 50, 50, 50, 50],
            active: [0, 0, 2.25, 2.25, 12],
        }),
        allin: 100,
        isPlayerInPosition: () => false,
    });
    const genericIpLine = makeContext({
        activePlayer: 0,
        betCount: 3,
        actions: [
            makeAction(0, RAISE, 2.25),
            makeAction(4, RAISE, 12),
        ],
        state: makePotState({
            stacks: [60, 60, 60, 60, 60],
            active: [2.25, 0, 0, 0, 12],
        }),
        allin: 100,
        isPlayerInPosition: () => true,
    });
    const reverseBlindLine = makeContext({
        activePlayer: 4,
        betCount: 3,
        actions: [
            makeAction(3, CALL, 1),
            makeAction(4, RAISE, 3),
            makeAction(3, RAISE, 9),
        ],
        state: makePotState({
            stacks: [50, 50, 50, 50, 50],
            active: [0, 0, 0, 9, 3],
        }),
        allin: 100,
    });

    assert.deepEqual(
        Array.from(hrc.getSizingsPreflop(blindLine)),
        [21, 100],
    );
    assert.deepEqual(
        Array.from(hrc.getSizingsPreflop(squeezedNonBlindLine)),
        [22.5, 100],
    );
    assert.deepEqual(
        Array.from(hrc.getSizingsPreflop(genericIpLine)),
        [21, 100],
    );
    assert.deepEqual(
        Array.from(hrc.getSizingsPreflop(reverseBlindLine)),
        [21, 100],
    );
});


test("normalises legal sizing bounds before removing duplicates", () => {
    const ctx = makeContext({minimum: 10, allin: 100});

    assert.deepEqual(
        Array.from(
            hrc.normalizeAndUniqueSizings(ctx, [-1, 1, 10, 150, 100]),
        ),
        [10, 100],
    );
});


test("applies the 50% rule to the requested size before legal normalisation", () => {
    const state = makePotState({active: [10, 0, 0, 0, 0]});
    const ctx = makeContext({state, minimum: 60, allin: 100});
    const thresholded = hrc.applyAllinThreshold(ctx, [50]);

    assert.deepEqual(
        Array.from(hrc.normalizeAndUniqueSizings(ctx, thresholded)),
        [60],
    );
});


test("keeps the screenshot's blank Bets per Street setting unlimited", () => {
    const state = makePotState({
        folded: [false, false, true, true, true],
    });
    const ctx = makeContext({
        state,
        activePlayer: 0,
        betCount: 5,
        street: FLOP,
        spr: 10,
        allin: 1000,
    });

    assert.equal(hrc.postflopBetsPerStreet, null);
    assert.deepEqual(
        Array.from(hrc.getSizingsPostflop(ctx)),
        [33, 75],
    );
});


test("matches every normal-SPR postflop screenshot matrix", () => {
    const matrices = [
        ["HU flop bet", 2, FLOP, "bet", [25, 40, 67, 100]],
        ["HU flop raise", 2, FLOP, "raise", [33, 75]],
        ["HU flop donk", 2, FLOP, "donk", [33, 75]],
        ["HU turn bet", 2, TURN, "bet", [40, 80, 160]],
        ["HU turn raise", 2, TURN, "raise", [40, 80, 160]],
        ["HU turn donk", 2, TURN, "donk", [33, 75]],
        ["HU river bet", 2, RIVER, "bet", [40, 80, 160, 320]],
        ["HU river raise", 2, RIVER, "raise", [50, 100, 250]],
        ["HU river donk", 2, RIVER, "donk", [40, 80, 160, 320]],
        ["MW flop bet", 3, FLOP, "bet", [33, 75]],
        ["MW flop raise", 3, FLOP, "raise", [33, 75]],
        ["MW flop donk", 3, FLOP, "donk", [33, 75]],
        ["MW turn bet", 3, TURN, "bet", [40, 80, 160]],
        ["MW turn raise", 3, TURN, "raise", [50, 100]],
        ["MW turn donk", 3, TURN, "donk", [33, 75]],
        ["MW river bet", 3, RIVER, "bet", [50, 100, 200]],
        ["MW river raise", 3, RIVER, "raise", [75, 160]],
        ["MW river donk", 3, RIVER, "donk", [50, 100]],
    ];

    for (const [label, livePlayers, street, action, expected] of matrices) {
        const folded = livePlayers === 2
            ? [false, false, true, true, true]
            : [false, false, false, true, true];
        const isDonk = action === "donk";
        const ctx = makeContext({
            state: makePotState({folded}),
            activePlayer: 0,
            betCount: action === "raise" ? 1 : 0,
            street,
            spr: 10,
            allin: 1000,
            donk: isDonk,
            fullActions: isDonk ? [makeAction(0, RAISE, 2)] : [],
        });

        assert.deepEqual(
            Array.from(hrc.getSizingsPostflop(ctx)),
            expected,
            label,
        );
    }
});


test("enforces Limited donks and uses low-SPR bet sizes for them", () => {
    const state = makePotState({
        folded: [false, false, true, true, true],
    });
    const denied = makeContext({
        state,
        activePlayer: 0,
        betCount: 0,
        street: FLOP,
        spr: 10,
        allin: 1000,
        donk: true,
        fullActions: [],
    });
    const lowSprAllowed = makeContext({
        state,
        activePlayer: 0,
        betCount: 0,
        street: RIVER,
        spr: 2.5,
        allin: 1000,
        donk: true,
        fullActions: [makeAction(0, RAISE, 2)],
    });

    assert.deepEqual(Array.from(hrc.getSizingsPostflop(denied)), []);
    assert.deepEqual(
        Array.from(hrc.getSizingsPostflop(lowSprAllowed)),
        [10, 25, 40, 67, 100, 150, 1000],
    );
});


test("applies each postflop SPR boundary inclusively", () => {
    const headsUpState = makePotState({
        folded: [false, false, true, true, true],
    });
    const multiwayState = makePotState({
        folded: [false, false, false, true, true],
    });
    const headsUpLow = makeContext({
        state: headsUpState,
        betCount: 0,
        street: TURN,
        spr: 2.5,
        allin: 1000,
    });
    const multiwayLow = makeContext({
        state: multiwayState,
        betCount: 0,
        street: TURN,
        spr: 1.5,
        allin: 1000,
    });
    const headsUpLowRaise = makeContext({
        state: headsUpState,
        betCount: 1,
        street: TURN,
        spr: 2.5,
        allin: 1000,
    });
    const multiwayLowRaise = makeContext({
        state: multiwayState,
        betCount: 1,
        street: TURN,
        spr: 1.5,
        allin: 1000,
    });
    const addAllinAtFive = makeContext({
        state: headsUpState,
        betCount: 0,
        street: FLOP,
        spr: 5,
        allin: 1000,
    });
    const noAllinAboveFive = makeContext({
        state: headsUpState,
        betCount: 0,
        street: FLOP,
        spr: 5.01,
        allin: 1000,
    });

    assert.deepEqual(
        Array.from(hrc.getSizingsPostflop(headsUpLow)),
        [10, 25, 40, 67, 100, 150, 1000],
    );
    assert.deepEqual(
        Array.from(hrc.getSizingsPostflop(multiwayLow)),
        [10, 25, 40, 67, 1000],
    );
    assert.deepEqual(
        Array.from(hrc.getSizingsPostflop(headsUpLowRaise)),
        [10, 25, 40, 67, 100, 1000],
    );
    assert.deepEqual(
        Array.from(hrc.getSizingsPostflop(multiwayLowRaise)),
        [20, 40, 1000],
    );
    assert.deepEqual(
        Array.from(hrc.getSizingsPostflop(addAllinAtFive)),
        [25, 40, 67, 100, 1000],
    );
    assert.deepEqual(
        Array.from(hrc.getSizingsPostflop(noAllinAboveFive)),
        [25, 40, 67, 100],
    );
});


test("supports a future postflop cap with all-in only after the cap", () => {
    const state = makePotState({
        folded: [false, false, true, true, true],
    });
    const ctx = makeContext({
        state,
        activePlayer: 0,
        betCount: 2,
        street: FLOP,
        spr: 10,
        allin: 100,
    });

    const forbiddenDonkAtZeroCap = makeContext({
        state,
        activePlayer: 0,
        betCount: 0,
        street: FLOP,
        spr: 10,
        allin: 100,
        donk: true,
        fullActions: [],
    });

    hrc.setPostflopBetsPerStreet(2);

    try {
        assert.deepEqual(Array.from(hrc.getSizingsPostflop(ctx)), [100]);

        hrc.setPostflopBetsPerStreet(0);
        assert.deepEqual(
            Array.from(hrc.getSizingsPostflop(forbiddenDonkAtZeroCap)),
            [],
        );
    } finally {
        hrc.setPostflopBetsPerStreet(null);
    }
});


test("deduplicates low-SPR sizes after minimum and all-in clamping", () => {
    const state = makePotState({
        folded: [false, false, true, true, true],
    });
    const ctx = makeContext({
        state,
        activePlayer: 0,
        betCount: 0,
        street: TURN,
        spr: 0.3,
        minimum: 20,
        allin: 30,
    });

    assert.deepEqual(
        Array.from(hrc.getSizingsPostflop(ctx)),
        [20, 25, 30],
    );
});


test("treats two players able to act plus an all-in player as heads-up", () => {
    const state = makePotState({
        folded: [false, false, false, true, true],
        allIn: [false, false, true, false, false],
    });
    const ctx = makeContext({
        state,
        activePlayer: 0,
        betCount: 0,
        street: FLOP,
        spr: 10,
        allin: 1000,
    });

    assert.deepEqual(
        Array.from(hrc.getSizingsPostflop(ctx)),
        [25, 40, 67, 100],
    );
});


test("makes postflop calls and configured street horizons explicit", () => {
    const twoWayTurn = makeContext({
        street: TURN,
        state: makePotState({
            folded: [false, false, true, true, true],
        }),
    });
    const fourWayTurn = makeContext({
        street: TURN,
        state: makePotState({
            folded: [false, false, false, false, true],
        }),
    });
    const twoWayRiver = makeContext({
        street: RIVER,
        state: makePotState({
            folded: [false, false, true, true, true],
        }),
    });
    const threeWayTurn = makeContext({
        street: TURN,
        state: makePotState({
            folded: [false, false, false, true, true],
        }),
    });
    const threeWayRiver = makeContext({
        street: RIVER,
        state: makePotState({
            folded: [false, false, false, true, true],
        }),
    });
    const fourWayFlop = makeContext({
        street: FLOP,
        state: makePotState({
            folded: [false, false, false, false, true],
        }),
    });
    const fiveWayFlop = makeContext({
        street: FLOP,
        state: makePotState(),
    });
    const sixWayFlop = makeContext({
        numberOfPlayers: 6,
        button: 3,
        smallBlind: 4,
        bigBlind: 5,
        street: FLOP,
        state: makePotState({stacks: Array(6).fill(100)}),
    });
    const twoAblePlusAllin = makeContext({
        street: TURN,
        state: makePotState({
            folded: [false, false, false, true, true],
            allIn: [false, false, true, false, false],
        }),
    });

    assert.equal(hrc.canFlatCallPostflop(twoWayTurn), true);
    assert.equal(hrc.hasNextStreetBetting(twoWayTurn), true);
    assert.equal(hrc.hasNextStreetBetting(twoWayRiver), false);
    assert.equal(hrc.hasNextStreetBetting(threeWayTurn), true);
    assert.equal(hrc.hasNextStreetBetting(fourWayTurn), false);
    assert.equal(hrc.hasNextStreetBetting(threeWayRiver), false);
    assert.equal(hrc.hasNextStreetBetting(fourWayFlop), true);
    assert.equal(hrc.hasNextStreetBetting(fiveWayFlop), false);
    assert.equal(hrc.hasNextStreetBetting(sixWayFlop), false);
    assert.equal(hrc.hasNextStreetBetting(twoAblePlusAllin), true);
});


test("accepts 3m through 6m and rejects other configured player counts", () => {
    for (const numberOfPlayers of [3, 4, 5, 6]) {
        const stacks = Array(numberOfPlayers).fill(100);
        const ctx = makeContext({
            numberOfPlayers,
            activePlayer: 0,
            button: Math.max(0, numberOfPlayers - 3),
            smallBlind: numberOfPlayers - 2,
            bigBlind: numberOfPlayers - 1,
            state: makePotState({stacks}),
            allin: 100,
        });

        assert.doesNotThrow(() => hrc.getSizingsPreflop(ctx));
    }

    for (const numberOfPlayers of [2, 7]) {
        const ctx = makeContext({numberOfPlayers});
        assert.throws(
            () => hrc.getSizingsPreflop(ctx),
            /supports three- through six-player configurations only/,
        );
    }
});
