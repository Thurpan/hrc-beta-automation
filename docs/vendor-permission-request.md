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

The repeated workflow is: create a hand and tree from user-authorised table and
stack inputs; rename the hand; submit a CI `10` Nash calculation followed by a
CI `1` calculation with Reset Strategies; create a Viewer Save; export complete
strategies; verify the two new outputs; close the completed hand without a
Complete Save; and repeat for the next authorised simulation.

For safety, the tool needs to distinguish each sampling, Viewer Save, and
strategy-export operation's identity, acceptance or rejection, queued and
running states, and terminal success, cancellation, or error. The visible
Progress view does not appear to retain a durable distinction between every
successful and cancelled job.

Could you please confirm:

1. Does HRC provide a supported API, CLI, or other interface for submitting
   each operation in this workflow? Separately, does HRC provide a supported
   callback, plug-in extension, structured log, or other interface for reading
   the unique identity, acceptance or rejection, queued and running states, and
   exact terminal success, cancellation, or error of each sampling, Viewer
   Save, and Export Strategies operation?
2. If no supported interface covers every required operation and lifecycle
   state, may I use a project-owned local accessibility runner that
   programmatically reads visible UI labels, values, selections, prompts, and
   status states and invokes visible controls through accessibility, keyboard,
   or native control APIs, without screen-coordinate clicks?
3. If no supported status interface covers every required operation and state,
   may a project-owned companion Eclipse/OSGi bundle be loaded when HRC starts,
   register the public Eclipse `IJobChangeListener`, and write only each
   matching job's identity, lifecycle, and terminal `IStatus` to a local
   single-user channel? It would not read calculation strategies, licence data,
   or other HRC memory. If this exact mechanism is permitted, please specify the
   approved installation or loading method, permitted identity/correlation and
   status fields, and allowed local communication channel. If it is not
   permitted, please identify an approved alternative.
4. If the accessibility runner or companion observer is permitted, may it
   resolve the active HRC installation, read installed component filenames and
   product or component versions, and read the component files only to
   calculate SHA-256 hashes as a compatibility check? It would not decompile,
   disassemble, parse, or otherwise interpret those files.
   Alternatively, can HRC supply a supported build identifier or compatibility
   handshake that makes this file-identity check unnecessary?
5. If any interface or proposed mechanism is permitted, what integration method
   and restrictions do you require, and does permission cover HRC Beta as well
   as released HRC 4.x? Does it remain valid across ordinary product updates,
   and is it subject to revocation or other continuing conditions? Please also
   confirm whether the answer is HoldemResources GmbH's written consent for the
   named mechanism under the current HRC v4+ EULA, rather than general technical
   guidance.

I will not proceed with automated UI readback or control, the companion
observer, or any other integration mechanism unless you confirm that exact
approach in writing. Approval of one mechanism will not be treated as approval
of another.

Thank you.

## Required response record

Before resuming automation work, require a dated written response through an
authenticated official vendor channel and request a ticket or correspondence
reference where available. Keep the full correspondence outside Git. Record
only the allowed technical scope, restrictions, date, and a non-sensitive
reference in this repository. Community opinion is not vendor permission. If
the response is ambiguous, ask a narrower follow-up and keep the project
blocked.

## Response decision matrix

Do not interpret a general approval as permission for a mechanism that the
vendor did not explicitly address. Apply the narrowest matching row below.

| Vendor response | Project status | Next allowed action |
| --- | --- | --- |
| Supported submission and lifecycle-status interfaces are supplied and their permitted use covers the required local workflow | Paused until the exact interfaces, product versions, restrictions, operation-correlation method, and accepted/rejected, queued, running, and terminal-state contracts are recorded | Design a feasibility probe using only those supported interfaces. Do not infer permission for accessibility control or an in-process observer. |
| A supported interface covers only submission, only terminal status, or another subset of the required lifecycle | Blocked for the uncovered lifecycle states or operations | Ask how the remaining operations and states are supported or authorised. Do not infer that a status interface authorises UI submission, or that a submission interface supplies exact outcomes. |
| Accessibility readback and control are explicitly permitted, and a supported interface also supplies every required identity-matched lifecycle state | Eligible to resume scoped feasibility after the written scope is recorded | Revalidate the representative lifecycle within the stated limits before implementing a standalone runner. |
| Accessibility readback and control are permitted, but no exact complete lifecycle-status interface or observer is permitted | Blocked | Do not submit the reserved Nash smoke. Ask whether the vendor can supply an approved lifecycle oracle; visible idle or disappearance is insufficient. |
| The exact startup-loaded Eclipse/OSGi `IJobChangeListener` companion and accessibility runner are both explicitly permitted | Paused until the protected dirty tabs have a user-approved safe disposition and the approved installation method is known | On a clean HRC start, build and validate the minimal read-only status observer first. Do not use dynamic attachment or expand the observer beyond the approved fields. |
| The observer is permitted but accessibility readback or control is not | Blocked | Ask for an approved workflow-control interface. Observer approval alone does not authorise UI automation. |
| A different vendor-supported approach is offered | Paused for technical review | Check it against the full lifecycle, exact terminal-state, no-overwrite, local-session, and data-safety requirements. Use it only if the written scope covers the required actions. |
| A proposed interface or mechanism is denied | Blocked for that named design | Do not implement or test the denied design. Record the non-sensitive decision and assess only vendor-approved alternatives. |
| The answer is conditional, partial, or ambiguous | Blocked pending clarification | Reply with a narrow question naming the unresolved mechanism, HRC version, and required status fields. |
| No answer is received | Paused | Send at most a polite follow-up through an official support channel when Euan chooses; do not treat silence as consent. |

## Resume gate

Before any HRC automation work resumes, the written response must establish all
of the following that apply to the selected design:

- permission for the exact workflow-control mechanism;
- approved identity-matched lifecycle evidence that distinguishes acceptance or
  rejection, queued and running states, and terminal success, cancellation, and
  error for both Nash jobs, Viewer Save, and export;
- the covered HRC editions and versions, including whether HRC Beta is covered;
- the approved installation or activation method for any companion component;
- the permitted data fields, storage, retention, and local communication path;
- either permission for the narrow installed-component identity check or a
  vendor-supported build identity that replaces it, when the selected design
  depends on version-specific findings;
- any support, security, distribution, or licence restrictions; and
- whether a clean HRC restart is required.

Vendor approval removes only the authorisation gate. It does not establish
technical feasibility or waive any no-overwrite, protected-tab, output,
logging, or data-safety requirement. Hash matching and technical feasibility do
not substitute for permission. If a restart or runtime-composition change is
authorised, Euan must first decide how to preserve or discard the protected
dirty tabs `*Hand 7` and `*From Hand 7`; the project must not close or restart
them automatically.

The repository record should contain the response date, covered product/version,
permitted mechanisms, restrictions, and a non-sensitive correspondence
reference. Keep the full message and any account-specific details outside Git.
