# Draft HRC automation permission request

## Status

This is an unsent draft for Euan to review and send. It contains no licence key,
account identifier, private HRC configuration, or strategy data.

## Recipient

`support@holdemresources.net`

## Subject

Permission and supported interface for local single-user HRC automation

## Message

Hello HoldemResources support,

I am a licensed HRC Pro/Beta user running HRC locally on one licensed Windows
computer. I would like to automate a repetitive preflop workflow for my own
calculations. The proposed tool would remain local to my licensed interactive
user session. It would not expose HRC to other people, provide a web service,
share a licence, or inspect exported strategy contents. Calculation results
would be produced only through HRC's built-in Viewer Save and Export Strategies
functions.

For safety, the tool needs to distinguish each sampling, Viewer Save, and
strategy-export job's identity and terminal success, cancellation, or error.
The visible Progress view does not appear to retain a durable distinction
between every successful and cancelled job.

Could you please confirm:

1. Does HRC provide a supported API, CLI, callback, plug-in extension, or
   structured log for submitting this workflow and reading exact per-job
   terminal status?
2. If no such interface exists, may I use a project-owned local accessibility
   runner that programmatically reads visible UI labels, values, selections,
   prompts, and status states and invokes visible controls through accessibility,
   keyboard, or native control APIs, without screen-coordinate clicks?
3. If you do not provide a supported status interface, may a project-owned
   companion Eclipse/OSGi bundle be loaded when HRC starts, register the public
   Eclipse `IJobChangeListener`, and write only each matching job's identity,
   lifecycle, and terminal `IStatus` to a local single-user channel? It would
   not read calculation strategies, licence data, or other HRC memory. If this
   exact mechanism is not permitted, please identify an approved alternative.
4. If either design is permitted, what integration method and restrictions do
   you require, and does permission cover HRC Beta as well as released HRC 4.x?

I will not proceed with automated UI readback or control, the companion
observer, or any other integration mechanism unless you confirm that exact
approach in writing. Approval of one mechanism will not be treated as approval
of another.

Thank you.

## Required response record

Before resuming automation work, preserve the vendor's written response outside
Git if it contains account, licence, or other private information. Record only
the allowed technical scope, restrictions, date, and a non-sensitive reference
in this repository. If the response is ambiguous, ask a narrower follow-up and
keep the project blocked.
