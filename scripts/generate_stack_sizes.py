from itertools import product
from math import prod
from pathlib import Path

STACK_OPTIONS = [
    10, 12.5, 15, 20, 30, 40, 100, 50, 7,
    5, 17.5, 22.5, 35, 45, 60, 70, 80, 25,
]
PLAYER_COUNT = 5
OUTPUT_FILE = Path("stack_size_options.txt")

STACK_SORT_ORDER = {stack: rank for rank, stack in enumerate(STACK_OPTIONS, start=1)}


def stack_sort_key(stacks):
    """Sort by setup index, then by stack ranks to break equal-index ties."""

    stack_ranks = tuple(STACK_SORT_ORDER[stack] for stack in stacks)
    return prod(stack_ranks), stack_ranks


def main():
    # A sole largest stack is equivalent to one capped at the next-largest stack.
    setups = (
        setup
        for setup in product(STACK_OPTIONS, repeat=PLAYER_COUNT)
        if setup.count(max(setup)) > 1
    )

    sorted_setups = sorted(setups, key=stack_sort_key)

    with OUTPUT_FILE.open("w", encoding="utf-8") as file:
        file.writelines(
            "-".join(map(str, setup)) + "\n" for setup in sorted_setups
        )

    print(f"Generated {len(sorted_setups)} unique stack setups.")
    print(f"Saved to {OUTPUT_FILE}")


if __name__ == "__main__":
    main()
