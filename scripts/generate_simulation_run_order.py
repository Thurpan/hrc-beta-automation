from itertools import product
from math import prod
from pathlib import Path

HEADS_UP_STACK_OPTIONS = (
    1, 1.5, 2, 2.5, 3, 3.5, 4, 4.5, 5, 5.5,
    6, 6.5, 7, 7.5, 8, 8.5, 9, 9.5, 10, 10.5,
    11, 11.5, 12, 12.5, 13, 13.5, 14, 14.5, 15, 15.5,
    16, 16.5, 17, 17.5, 18, 18.5, 19, 19.5, 20, 20.5,
    21, 21.5, 22, 22.5, 23, 23.5, 24, 24.5, 25,
    26, 27, 28, 29, 30,
    32.5, 35, 37.5, 40, 42.5, 45, 47.5, 50,
    55, 60, 65, 70, 75, 80,
)

CORE_STACK_OPTIONS = (10, 12.5, 15, 20, 30, 40, 100)
FULL_STACK_OPTIONS = (
    10, 12.5, 15, 20, 30, 40, 100, 50, 7.5,
    5, 17.5, 22.5, 35, 45, 60, 25, 70, 80,
)

RUN_ORDER_BATCHES = (
    (2, HEADS_UP_STACK_OPTIONS),
    (3, (10, 20, 15, 30)),
    (5, (10, 20, 15)),
    (3, CORE_STACK_OPTIONS),
    (5, CORE_STACK_OPTIONS),
    (3, FULL_STACK_OPTIONS),
    (5, FULL_STACK_OPTIONS),
)

SIMULATION_RUN_ORDER_FILE = (
    Path(__file__).resolve().parents[1]
    / "data"
    / "stack-sizes"
    / "simulation_run_order.txt"
)


def build_stack_sort_order(stack_options):
    """Map each distinct stack option to its one-based priority rank."""

    if not stack_options:
        raise ValueError("A run-order batch must contain at least one stack option")
    if len(stack_options) != len(set(stack_options)):
        raise ValueError("A run-order batch contains duplicate stack options")

    return {stack: rank for rank, stack in enumerate(stack_options, start=1)}


def stack_sort_key(stacks, stack_sort_order):
    """Sort by setup index, then by stack ranks to break equal-index ties."""

    stack_ranks = tuple(stack_sort_order[stack] for stack in stacks)
    return prod(stack_ranks), stack_ranks


def generate_setups(player_count, stack_options):
    """Generate one batch in the priority order of its stack options."""

    if player_count < 2:
        raise ValueError("A run-order batch must contain at least two players")

    stack_sort_order = build_stack_sort_order(stack_options)

    # A sole largest stack is equivalent to one capped at the next-largest stack.
    setups = (
        setup
        for setup in product(stack_options, repeat=player_count)
        if setup.count(max(setup)) > 1
    )

    return sorted(
        setups,
        key=lambda setup: stack_sort_key(setup, stack_sort_order),
    )


def generate_simulation_run_order(batches):
    """Append ordered batches while keeping only each setup's first occurrence."""

    simulation_run_order = []
    seen_setups = set()

    for player_count, stack_options in batches:
        for setup in generate_setups(player_count, stack_options):
            if setup in seen_setups:
                continue

            seen_setups.add(setup)
            simulation_run_order.append(setup)

    return simulation_run_order


def main():
    simulation_run_order = generate_simulation_run_order(RUN_ORDER_BATCHES)

    SIMULATION_RUN_ORDER_FILE.parent.mkdir(parents=True, exist_ok=True)
    with SIMULATION_RUN_ORDER_FILE.open("w", encoding="utf-8") as file:
        file.writelines(
            "-".join(map(str, setup)) + "\n"
            for setup in simulation_run_order
        )

    print(
        f"Generated {len(simulation_run_order)} unique simulation "
        "run-order entries."
    )
    print(f"Saved to {SIMULATION_RUN_ORDER_FILE}")


if __name__ == "__main__":
    main()
