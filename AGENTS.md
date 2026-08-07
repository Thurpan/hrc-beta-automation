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
- Do not add source code, dependencies, or build commands before feasibility is proven.

## HRC data safety

- Use a disposable workflow with a new filename.
- Never overwrite or delete existing HRC data.
- Do not reveal poker data that is not necessary for feasibility evidence.
- Ask Euan before using poker inputs when no clearly safe disposable workflow is available.
- Ask Euan before starting an expensive calculation.
- Verify saved output without changing existing output.

## One-workflow limit

Select only one representative workflow. Map configuration, start, running, completion or failure, save, and saved-output verification.

Do not design broader or unattended automation until the selected workflow has a supported feasibility verdict.

## Validation expectations

Map the visible label, accessible name, control type, automation ID, supported patterns, keyboard path, required action, observable outcomes, and safety for each step.

Test the full lifecycle manually. Repeat it once only when the workflow is quick and safe.

Stop and record the exact blocker when any critical step requires blind clicking. Stop when completion cannot be distinguished from failure. Stop when saved output cannot be verified.

Validate changed Markdown before each commit. Review the exact diff. Keep scaffold and observed evidence in separate commits when discovery changes the documentation.

## Source-of-truth boundaries

- Store project purpose and boundaries in `README.md`.
- Store operating instructions in `AGENTS.md`.
- Store observed discovery evidence in `docs/feasibility.md`.
- Keep licence material, HRC private configuration, and sensitive poker data outside Git.
- Keep future implementation claims out of the repository until evidence supports them.
