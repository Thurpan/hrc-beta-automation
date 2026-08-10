# Project instructions

## Scope and authority

Keep this repository focused on HRC Beta Automation. This repository owns implementation truth after its creation.

Treat `README.md` as the project boundary. Treat `docs/feasibility.md` as the source of truth for observed feasibility evidence.

Do not copy unverified portfolio claims into implementation decisions. Record a mismatch and preserve uncertainty when external context conflicts with local evidence.

## Licensed-machine constraint

Run HRC Beta only on the licensed host `EM-3960X`. The host reports an AMD Ryzen Threadripper 3960X processor.

Do not copy HRC Beta, its licence material, or its private configuration to another computer. Do not expose licence data in files, logs, screenshots, commits, or completion reports.

## Discovery-first rules

- Inspect accessibility before choosing an automation method.
- Prefer accessible controls, supported automation patterns, and keyboard paths.
- Do not use blind coordinate clicks.
- Stop at a critical control that cannot be identified or operated safely.
- Preserve unknown facts as `TBD` or `TO CONFIRM`.
- Record only observed evidence in `docs/feasibility.md`.
- Do not add application-automation source code, dependencies, or build
  commands before feasibility is proven.
- An explicitly requested HRC tree-building candidate can be developed offline
  under `scripts/hrc/`. Keep it labelled unvalidated until HRC verifies it on
  the licensed host.

## HRC data safety

- Use a new output filename for every simulation.
- Never overwrite or delete existing HRC data.
- Do not reveal poker data that is not necessary for feasibility evidence.
- Ask Euan before using poker inputs when no clearly safe non-overwriting
  workflow is available.
- Ask Euan before starting an expensive calculation unless the current request
  explicitly authorises the run or batch. One batch authorisation covers its
  specified simulations.
- Verify saved output without changing existing output.
- Save Viewer output as a new `.hrcv` file under
  `\\VAULT\sims\Preflop\<table-group>`. Do not guess or create a missing
  table-size folder.
- Save the strategy export as a new `.zip` file in the same table-size folder.
  Use the simulation name as the base filename.
- Verify the Viewer output and strategy archive before closing the completed
  hand tab. Do not inspect strategy contents unless the task requires it and
  Euan authorises the inspection.
- When HRC shows `Save Resource` during Viewer-only tab closure, select
  `Don't Save` only when the verified `.hrcv` and `.zip` base filenames match
  the simulation named in the prompt. Stop without discarding the hand if any
  filename, prompt, or available action differs from the observed workflow.

## One workflow pattern

Validate one representative workflow pattern first. The pattern can then run
for multiple simulations in the user-specified order.

Map tree creation, rename, both Nash configurations, queue order, running,
completion or failure, Viewer save, saved-output verification, strategy export,
strategy-archive verification, Viewer-only hand-tab closure, and transition to
the next simulation.

Do not design beyond this repeatable workflow until the representative
lifecycle has a supported feasibility verdict.

## Validation expectations

Map the visible label, accessible name, control type, automation ID, supported patterns, keyboard path, required action, observable outcomes, and safety for each step.

Test the full lifecycle manually once. Do not repeat a long calculation only
for feasibility evidence.

Stop and record the exact blocker when any critical step requires blind clicking. Stop when completion cannot be distinguished from failure. Stop when saved output cannot be verified.

Validate changed Markdown before each commit. Review the exact diff. Keep scaffold and observed evidence in separate commits when discovery changes the documentation.

## Source-of-truth boundaries

- Store project purpose and boundaries in `README.md`.
- Store operating instructions in `AGENTS.md`.
- Store observed discovery evidence in `docs/feasibility.md`.
- Store verbatim external HRC script snapshots under `reference/hrc/`.
- Do not apply project changes to an external snapshot.
- Store project-owned HRC tree-building candidates under `scripts/hrc/`.
- Keep licence material, HRC private configuration, and sensitive poker data outside Git.
- Keep future implementation claims out of the repository until evidence supports them.
