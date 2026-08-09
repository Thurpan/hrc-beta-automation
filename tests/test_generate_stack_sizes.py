import unittest
from contextlib import redirect_stdout
from io import StringIO
from pathlib import Path
from tempfile import TemporaryDirectory
from unittest.mock import patch

import scripts.generate_stack_sizes as generator


class GenerateStackSetupsTests(unittest.TestCase):
    def test_uses_five_players(self):
        self.assertEqual(generator.PLAYER_COUNT, 5)

    def test_writes_expected_setups_and_status(self):
        expected_output = (
            "5-5-5\n"
            "5-10-10\n"
            "10-5-10\n"
            "10-10-5\n"
            "10-10-10\n"
        )

        with TemporaryDirectory() as temporary_directory:
            output_file = Path(temporary_directory) / "setups.txt"
            stdout = StringIO()

            with (
                patch.object(generator, "STACK_OPTIONS", [5, 10]),
                patch.object(generator, "STACK_SORT_ORDER", {5: 1, 10: 2}),
                patch.object(generator, "PLAYER_COUNT", 3),
                patch.object(generator, "OUTPUT_FILE", output_file),
                redirect_stdout(stdout),
            ):
                generator.main()

            self.assertEqual(
                output_file.read_text(encoding="utf-8"),
                expected_output,
            )
            self.assertEqual(
                stdout.getvalue(),
                f"Generated 5 unique stack setups.\nSaved to {output_file}\n",
            )


class StackSortKeyTests(unittest.TestCase):
    def test_calculates_product_of_one_based_stack_ranks(self):
        self.assertEqual(generator.stack_sort_key((10, 10, 10, 10, 10))[0], 1)
        self.assertEqual(generator.stack_sort_key((15, 20, 20, 10, 10))[0], 48)

    def test_sorts_by_product_then_breaks_ties_by_stack_rank(self):
        setups = [
            (15, 20, 20, 10, 10),
            (10, 10, 12.5, 15, 10),
            (10, 10, 10, 10, 10),
            (10, 10, 10, 40, 10),
        ]

        self.assertEqual(
            sorted(setups, key=generator.stack_sort_key),
            [
                (10, 10, 10, 10, 10),
                (10, 10, 10, 40, 10),
                (10, 10, 12.5, 15, 10),
                (15, 20, 20, 10, 10),
            ],
        )

    def test_assigns_every_stack_option_a_one_based_rank(self):
        self.assertEqual(set(generator.STACK_SORT_ORDER), set(generator.STACK_OPTIONS))
        self.assertEqual(
            sorted(generator.STACK_SORT_ORDER.values()),
            list(range(1, len(generator.STACK_OPTIONS) + 1)),
        )
        self.assertEqual(
            generator.STACK_SORT_ORDER[25],
            len(generator.STACK_OPTIONS),
        )


if __name__ == "__main__":
    unittest.main()
