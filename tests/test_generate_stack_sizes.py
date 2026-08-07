import unittest

from scripts.generate_stack_sizes import (
    STACK_OPTIONS,
    STACK_SORT_ORDER,
    stack_sort_key,
)


class StackSortKeyTests(unittest.TestCase):
    def test_calculates_product_of_one_based_stack_ranks(self):
        self.assertEqual(stack_sort_key((10, 10, 10, 10))[0], 1)
        self.assertEqual(stack_sort_key((15, 20, 20, 10))[0], 48)

    def test_sorts_by_product_then_breaks_ties_by_stack_rank(self):
        setups = [
            (15, 20, 20, 10),
            (10, 10, 12.5, 15),
            (10, 10, 10, 10),
            (10, 10, 10, 40),
        ]

        self.assertEqual(
            sorted(setups, key=stack_sort_key),
            [
                (10, 10, 10, 10),
                (10, 10, 10, 40),
                (10, 10, 12.5, 15),
                (15, 20, 20, 10),
            ],
        )

    def test_assigns_every_stack_option_a_one_based_rank(self):
        self.assertEqual(set(STACK_SORT_ORDER), set(STACK_OPTIONS))
        self.assertEqual(
            sorted(STACK_SORT_ORDER.values()),
            list(range(1, len(STACK_OPTIONS) + 1)),
        )
        self.assertEqual(STACK_SORT_ORDER[25], len(STACK_OPTIONS))


if __name__ == "__main__":
    unittest.main()
