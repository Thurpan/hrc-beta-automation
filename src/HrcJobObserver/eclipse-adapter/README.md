# Offline Eclipse Jobs adapter

## Status

This directory contains a package-private Java 17 adapter. It compiles offline
against exact public Eclipse Core Jobs, Equinox Common, and OSGi API providers
on the licensed host. It is not registered with a Job manager and has no OSGi
activator, manifest, network or file service, installer, or runtime entry
point. It has never been loaded into or run with HRC.

The adapter does not make HRC Job results durable. It does not change the
project's `TO CONFIRM` feasibility verdict.

## Callback and mailbox contract

`EclipseJobChangeListener` translates only `scheduled`, `running`, and `done`
callbacks. The inherited `aboutToRun`, `awake`, and `sleeping` callbacks remain
no-ops. `aboutToRun` is not proof that a Job ran.

The mailbox allocates a callback ticket atomically when it acquires the
callback-entry lease. It allocates the ticket before the listener reads any
callback data. The adapter then captures a process-local monotonic timestamp
and a UTC timestamp before mailbox admission. Unknown Job classes are ignored
before the adapter reads their name, Bundle, flags, or result. For a recognised
class, the adapter verifies the defining Bundle and version before it reads the
remaining fields. A source mismatch becomes timestamp-only failure evidence.

For an exact source, the adapter gets the exact `Job` reference once. That raw
reference can exist briefly in the bounded mailbox and longer in the bounded
core identity map. It never enters an emitted event, log, serialised value, or
transport. The
adapter copies only:

- Bundle symbolic name and version;
- Job class, public name, user flag, and system flag; and
- for `done` only, result severity, `isOK`, code, and plug-in identifier from
  `IJobChangeEvent.getResult()`.

The adapter does not inspect status messages, exceptions, children, stack
traces, Job state, prior Job result, delay, group result, strategies, licence
data, or other HRC state. A missing `done` result reaches the core's explicit
`MISSING_TERMINAL_STATUS` path.

`EclipseCallbackMailbox` is a fixed-capacity in-process hand-off. Each callback
reserves and completes its numbered slot without waiting for the core. The
mailbox also accepts one pending generic control action. One daemon worker
processes callbacks and control actions in the same ticket order. The worker is
the only callback-path caller of `ObserverIngress`. This mailbox is not the
local transport.

The mailbox is separate from the
[offline local transport](../local-transport/README.md); raw Job references
never cross that transport boundary. It independently latches capture,
overflow, control, and dispatch failures. After the first mailbox
infrastructure failure, it rejects new callbacks and controls. It discards work
that has not already been authorised for dispatch. A lower-ticket dispatch
already inside `ObserverIngress` may finish before a later-ticket failure is
reported.

A control result contains its positive ticket as an opaque barrier identifier.
It also contains authoritative mailbox-health snapshots taken immediately
before the action and again before the result is published. A pending failure
without a published incident is not healthy. A timed-out action that the worker
has not claimed is cancelled and cannot run later. A timeout after claim
latches an infrastructure failure.

The [offline runtime assembly](../runtime-assembly/README.md) uses this ordered
control boundary for checkpoints and two-marker arm confirmation. This is an
offline implementation result, not live Eclipse or HRC evidence. A failure in
the core's failure-reporting method cannot erase the independent latch.
Callbacks do no I/O, IPC, logging, serialisation, Job mutation, or UI work. The
hand-off does not wait for `ObserverIngress` or the worker. Real callback
latency remains unvalidated.

Public Eclipse Job names are human-readable and not unique. Correlation still
requires the arm and exact Bundle, version, class, and name profile. Eclipse
Jobs are reusable in general. The one-new-object-per-HRC-submission assumption
is a fingerprint-specific HRC finding. Object reuse fails closed in the core.

No code in this directory registers the listener. A later layer must start the
mailbox before one manager-wide registration through `Job.getJobManager()`.
It must pair `addJobChangeListener` with `removeJobChangeListener`. During
teardown it must remove the manager-wide listener first, then close and await
the mailbox. Closure must drain or fault every callback and control that has
already acquired an ordered lease. A faulted close can terminate the worker
while a callback is still unwinding. The later owner must not unload code until
the in-flight callback count is zero and every control handle is terminal. It
must not also register the same listener on individual Jobs.

## Offline validation

Run:

```powershell
& .\src\HrcJobObserver\eclipse-adapter\build.ps1
```

The build first runs 30 dependency-free core tests. It then compiles adapter
main and test outputs separately with `javac --release 17 -proc:none
-Xlint:all -Werror`. A targeted source/output boundary scan rejects internal
Eclipse imports, selected I/O/network packages and listener-registration or
activator symbols, plus named packaging artefacts in compiled output. The core
and adapter scripts share a named local build lock because both use fixed
ignored output trees. It runs 34 deterministic adapter tests. They cover all
three operation profiles, data minimisation, status projection, strict source
filtering, failure latches, non-waiting callback hand-off, overflow, ordered
callback and control dispatch, immediate control resubmission, one-control
ownership, cancellation before claim, timeout after claim, late completion
after a timed wait, clean closure,
fatal capture, callback-entry leases, pre-arm timestamps, atomic
close-versus-lease linearisation, pre-admission close races, counter balance,
wake-up coalescing, lower-ticket dispatch during a later failure, and exact
core integration.

The current adapter result is 34/34. This is offline validation only.

The build resolves and hashes these files from the configured HRC
installation `plugins` directory. It does not copy them into the repository:

| Offline compile provider | Exact filename | SHA-256 |
| --- | --- | --- |
| Eclipse Core Jobs | `org.eclipse.core.jobs_3.15.500.v20250204-0817.jar` | `189199CD46A284220B7B97FD59218B533FE9FD8E0AD22258F674A3F2DF4DE7C9` |
| Equinox Common | `org.eclipse.equinox.common_3.20.0.v20250129-1348.jar` | `617C5D7E759276B7E9ED363C56A6714B7F21D4A812D533FCB90E48723CC4C001` |
| Eclipse OSGi | `org.eclipse.osgi_3.23.0.v20250228-0640.jar` | `1AC113541A19F0C72C0421FB24058DEFCA7E3C6F282E5EE73F14D2768A9AE653` |

This is an offline compile gate, not the active-process runtime gate. Core Jobs
is in the existing eight-component runtime fingerprint. Equinox Common and
Eclipse OSGi are not. Before live use, extend the source-of-truth gate for those
providers and resolve them from the active HRC process. Do not treat this build
as runtime identity proof.

Still unvalidated: OSGi resolution, listener registration and removal, real
callback delivery and latency, concrete HRC Bundle provenance, activator and
startup, live transport integration, packaging, installation, rollback, and
every HRC runtime result.

## Public API references

- Eclipse [`IJobChangeListener`](https://help.eclipse.org/latest/topic/org.eclipse.platform.doc.isv/reference/api/org/eclipse/core/runtime/jobs/IJobChangeListener.html)
- Eclipse [`IJobChangeEvent`](https://help.eclipse.org/latest/topic/org.eclipse.platform.doc.isv/reference/api/org/eclipse/core/runtime/jobs/IJobChangeEvent.html)
- Eclipse [`Job`](https://help.eclipse.org/latest/topic/org.eclipse.platform.doc.isv/reference/api/org/eclipse/core/runtime/jobs/Job.html)
- Eclipse [`IJobManager`](https://help.eclipse.org/latest/topic/org.eclipse.platform.doc.isv/reference/api/org/eclipse/core/runtime/jobs/IJobManager.html)
- Eclipse [`IStatus`](https://help.eclipse.org/latest/topic/org.eclipse.platform.doc.isv/reference/api/org/eclipse/core/runtime/IStatus.html)
- OSGi [`FrameworkUtil`](https://docs.osgi.org/javadoc/osgi.core/8.0.0/org/osgi/framework/FrameworkUtil.html)

These are public Eclipse and OSGi contracts. They are not a vendor-supported
HRC integration API. Current web documentation does not prove the installed
runtime or HRC-specific behaviour.
