# HRC Beta feasibility evidence

## Evidence rules

Record only direct observations from the licensed host. Use `TBD` for missing
information and `TO CONFIRM` for an observation that requires confirmation.
Use `CONFIRMED` for a direct observation that does not require further
confirmation.

Do not record licence data, unnecessary poker data, or assumptions as facts.

## Environment findings

| Item | Observation | Evidence method | Status |
| --- | --- | --- | --- |
| Licensed host | `EM-3960X` | Read the `COMPUTERNAME` environment value on 10 August 2026. | CONFIRMED |
| Processor | AMD Ryzen Threadripper 3960X 24-Core Processor | Queried `Win32_Processor` on the licensed host. | CONFIRMED |
| Windows version | Microsoft Windows 11 Pro for Workstations, version `10.0.26200`, build `26200` | Queried `Win32_OperatingSystem` on the licensed host. | CONFIRMED |
| HRC Beta availability | HRC Beta is installed and running. Its main window title is `HRC Pro [Beta]`. | Inspected the running `hrc.exe` process and its executable path and window title. | CONFIRMED |
| HRC Beta version | The executable does not expose a file version or product version. The version in the HRC interface has not been inspected. | Inspected the `hrc.exe` version metadata. | TO CONFIRM |
| Accessibility Insights availability | No executable was present in the two standard `Program Files` locations checked. Other installation methods were not checked. | Checked the standard 64-bit and 32-bit installation paths. | TO CONFIRM |
| Microsoft Inspect availability | The x64 `inspect.exe` is available in Windows Kits `10.0.26100.0`. Its file version is `7.2.0.0`. | Inspected the installed Windows Kits executable and its version metadata. | CONFIRMED |

## Corrected context

| Previous statement | Observed evidence | Resolution |
| --- | --- | --- |
| The licensed host used an AMD Ryzen 9 5950X processor. | `EM-3960X` reports an AMD Ryzen Threadripper 3960X 24-Core Processor. | Use the observed host and processor. The earlier 5950X reference is incorrect for this host. |

## Selected workflow

- Workflow: TBD
- Selection reason: TBD
- Disposable inputs: TBD
- New output filename: TBD
- Expected cost and duration: TBD

## Control map

| Lifecycle step | Visible label | Accessible name | Control type | Automation ID | Supported patterns | Keyboard path | Required action | Observable success | Observable failure | Safe to automate |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Configure | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TO CONFIRM |
| Start | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TO CONFIRM |
| Detect running | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TO CONFIRM |
| Detect completion or failure | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TO CONFIRM |
| Save | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TO CONFIRM |
| Verify saved output | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TO CONFIRM |

## Observable states

| State | Visible evidence | Accessible evidence | Distinguishable | Notes |
| --- | --- | --- | --- | --- |
| Configured | TBD | TBD | TO CONFIRM | TBD |
| Running | TBD | TBD | TO CONFIRM | TBD |
| Completed | TBD | TBD | TO CONFIRM | TBD |
| Failed | TBD | TBD | TO CONFIRM | TBD |
| Saved | TBD | TBD | TO CONFIRM | TBD |
| Saved output verified | TBD | TBD | TO CONFIRM | TBD |

## Test runs

| Run | Date and time | Disposable filename | Result | Observed duration | Evidence | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | TBD | TBD | NOT RUN | TBD | TBD | TBD |
| 2 | TBD | TBD | NOT RUN | TBD | TBD | Run only when quick and safe. |

## Blockers

- No HRC control blocker has been observed. The HRC interface and Microsoft
  Inspect output have not been inspected.

## Verdict

- Feasibility: TO CONFIRM
- Confidence: TO CONFIRM
- Basis: The licensed environment and Microsoft Inspect are available. No
  workflow is selected, no lifecycle control map is complete, and no workflow
  test has been recorded.

## Next action

Open the x64 Microsoft Inspect executable and inspect the existing HRC main
window manually. Record the accessible properties and keyboard paths needed to
select one small disposable workflow. Confirm the workflow inputs and expected
calculation cost with Euan before entering poker inputs or starting a
calculation.
