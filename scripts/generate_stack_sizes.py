from itertools import product
from math import prod
from pathlib import Path

STACK_OPTIONS = [10, 12.5, 15, 20, 30, 40, 100, 50, 7, 5, 17.5, 22.5, 35, 45, 60, 70, 80, 25]
PLAYER_COUNT = 4
OUTPUT_FILE = Path("stack_size_options.txt")


def build_stack_sort_order():
    """Map each stack size to its one-based position in STACK_OPTIONS."""

    if len(STACK_OPTIONS) != len(set(STACK_OPTIONS)):
        raise ValueError("STACK_OPTIONS contains duplicate stack sizes")

    return {stack: rank for rank, stack in enumerate(STACK_OPTIONS, start=1)}


STACK_SORT_ORDER = build_stack_sort_order()


def canonicalise_stacks(stacks):
    """
    Reduce equivalent stack setups.

    If exactly one player has the biggest stack, their extra chips do not matter.
    They are only effective against the next deepest stack.

    Example:
    5-10-5-15-20-10 becomes 5-10-5-15-15-10
    """

    stacks = list(stacks)
    biggest_stack = max(stacks)

    if stacks.count(biggest_stack) == 1:
        next_biggest_stack = max(stack for stack in stacks if stack < biggest_stack)
        biggest_stack_index = stacks.index(biggest_stack)
        stacks[biggest_stack_index] = next_biggest_stack

    return tuple(stacks)


def format_stacks(stacks):
    return "-".join(str(stack) for stack in stacks)


def stack_priority_index(stacks):
    """Calculate a setup's index from its one-based stack priority ranks."""

    return prod(STACK_SORT_ORDER[stack] for stack in stacks)


def stack_sort_key(stacks):
    """Sort by setup index, then by stack ranks to break equal-index ties."""

    stack_ranks = tuple(STACK_SORT_ORDER[stack] for stack in stacks)
    return stack_priority_index(stacks), stack_ranks


def main():
    unique_setups = set()

    for setup in product(STACK_OPTIONS, repeat=PLAYER_COUNT):
        canonical_setup = canonicalise_stacks(setup)
        unique_setups.add(canonical_setup)

    sorted_setups = sorted(unique_setups, key=stack_sort_key)

    with OUTPUT_FILE.open("w", encoding="utf-8") as file:
        for setup in sorted_setups:
            file.write(format_stacks(setup) + "\n")

    print(f"Generated {len(sorted_setups)} unique stack setups.")
    print(f"Saved to {OUTPUT_FILE}")


if __name__ == "__main__":
    main()
