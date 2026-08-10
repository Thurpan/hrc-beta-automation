import unittest
from contextlib import redirect_stdout
from io import StringIO
from pathlib import Path
from tempfile import TemporaryDirectory
from unittest.mock import patch

import scripts.generate_simulation_run_order as generator


class RunOrderConfigurationTests(unittest.TestCase):
    def test_uses_the_requested_batch_order(self):
        self.assertEqual(
            generator.RUN_ORDER_BATCHES,
            (
                (2, generator.HEADS_UP_STACK_OPTIONS),
                (3, (10, 20, 15, 30)),
                (5, (10, 20, 15)),
                (3, generator.CORE_STACK_OPTIONS),
                (5, generator.CORE_STACK_OPTIONS),
                (3, generator.FULL_STACK_OPTIONS),
                (5, generator.FULL_STACK_OPTIONS),
            ),
        )

    def test_uses_the_requested_heads_up_stack_options(self):
        options = generator.HEADS_UP_STACK_OPTIONS

        self.assertEqual(len(options), 68)
        self.assertEqual(len(options), len(set(options)))
        self.assertEqual(
            options[:49],
            tuple(value / 2 for value in range(2, 51)),
        )
        self.assertEqual(
            options[49:],
            (
                26, 27, 28, 29, 30,
                32.5, 35, 37.5, 40, 42.5, 45, 47.5, 50,
                55, 60, 65, 70, 75, 80,
            ),
        )

    def test_uses_the_requested_multiway_stack_options(self):
        self.assertEqual(
            generator.CORE_STACK_OPTIONS,
            (10, 12.5, 15, 20, 30, 40, 100),
        )
        self.assertEqual(
            generator.FULL_STACK_OPTIONS,
            (
                10, 12.5, 15, 20, 30, 40, 100, 50, 7.5,
                5, 17.5, 22.5, 35, 45, 60, 25, 70, 80,
            ),
        )

    def test_each_batch_contains_distinct_stack_options(self):
        for _, stack_options in generator.RUN_ORDER_BATCHES:
            with self.subTest(stack_options=stack_options):
                self.assertEqual(len(stack_options), len(set(stack_options)))

    def test_writes_to_the_simulation_run_order_file(self):
        repository_root = Path(__file__).resolve().parents[1]
        self.assertEqual(
            generator.SIMULATION_RUN_ORDER_FILE,
            repository_root / "data" / "stack-sizes" / "simulation_run_order.txt",
        )


class GenerateStackSetupsTests(unittest.TestCase):
    def test_heads_up_contains_one_equal_stack_setup_per_size(self):
        self.assertEqual(
            generator.generate_setups(2, (1, 1.5, 2)),
            [(1, 1), (1.5, 1.5), (2, 2)],
        )

    def test_omits_a_setup_with_only_one_largest_stack(self):
        setups = generator.generate_setups(3, (5, 10))

        self.assertEqual(
            setups,
            [
                (5, 5, 5),
                (5, 10, 10),
                (10, 5, 10),
                (10, 10, 5),
                (10, 10, 10),
            ],
        )

    def test_uses_each_batch_option_order_as_its_priority(self):
        setups = generator.generate_setups(3, (10, 20, 15))

        self.assertLess(
            setups.index((10, 20, 20)),
            setups.index((10, 15, 15)),
        )

    def test_rejects_duplicate_stack_options(self):
        with self.assertRaisesRegex(ValueError, "duplicate stack options"):
            generator.generate_setups(3, (5, 10, 10))


class GenerateSimulationRunOrderTests(unittest.TestCase):
    def test_appends_batches_and_omits_previous_setups(self):
        run_order = generator.generate_simulation_run_order(
            (
                (3, (5, 10)),
                (3, (5, 10, 15)),
            )
        )

        self.assertEqual(
            run_order,
            [
                (5, 5, 5),
                (5, 10, 10),
                (10, 5, 10),
                (10, 10, 5),
                (10, 10, 10),
                (5, 15, 15),
                (15, 5, 15),
                (15, 15, 5),
                (10, 15, 15),
                (15, 10, 15),
                (15, 15, 10),
                (15, 15, 15),
            ],
        )
        self.assertEqual(len(run_order), len(set(run_order)))

    def test_writes_expected_run_order_and_status(self):
        expected_output = (
            "1-1\n"
            "2-2\n"
            "5-5-5\n"
            "5-10-10\n"
            "10-5-10\n"
            "10-10-5\n"
            "10-10-10\n"
        )

        with TemporaryDirectory() as temporary_directory:
            output_file = Path(temporary_directory) / "simulation_run_order.txt"
            stdout = StringIO()

            with (
                patch.object(
                    generator,
                    "RUN_ORDER_BATCHES",
                    ((2, (1, 2)), (3, (5, 10))),
                ),
                patch.object(generator, "SIMULATION_RUN_ORDER_FILE", output_file),
                redirect_stdout(stdout),
            ):
                generator.main()

            self.assertEqual(
                output_file.read_text(encoding="utf-8"),
                expected_output,
            )
            self.assertEqual(
                stdout.getvalue(),
                "Generated 7 unique simulation run-order entries.\n"
                f"Saved to {output_file}\n",
            )


class StackSortKeyTests(unittest.TestCase):
    def setUp(self):
        self.stack_sort_order = {
            stack: rank
            for rank, stack in enumerate(
                generator.CORE_STACK_OPTIONS,
                start=1,
            )
        }

    def test_calculates_product_of_one_based_stack_ranks(self):
        self.assertEqual(
            generator.stack_sort_key(
                (10, 10, 10, 10, 10),
                self.stack_sort_order,
            )[0],
            1,
        )
        self.assertEqual(
            generator.stack_sort_key(
                (15, 20, 20, 10, 10),
                self.stack_sort_order,
            )[0],
            48,
        )

    def test_sorts_by_product_then_breaks_ties_by_stack_rank(self):
        setups = [
            (15, 20, 20, 10, 10),
            (10, 10, 12.5, 15, 10),
            (10, 10, 10, 10, 10),
            (10, 10, 10, 40, 10),
        ]

        self.assertEqual(
            sorted(
                setups,
                key=lambda setup: generator.stack_sort_key(
                    setup,
                    self.stack_sort_order,
                ),
            ),
            [
                (10, 10, 10, 10, 10),
                (10, 10, 10, 40, 10),
                (10, 10, 12.5, 15, 10),
                (15, 20, 20, 10, 10),
            ],
        )


if __name__ == "__main__":
    unittest.main()
