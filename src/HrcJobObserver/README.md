# HRC Job Observer Core

## Status

This directory contains a package-private, pure Java 17 feasibility core. It is
not the standalone runner and it is not an installable HRC plug-in. It has no
OSGi manifest, activator, Eclipse listener, network service, file writer, or
HRC installation path.

The core has never been installed, loaded, attached to, or run with HRC. Its
offline tests add no HRC observation and do not change the `TO CONFIRM`
feasibility verdict. The dirty HRC tabs `*Hand 7` and `*From Hand 7` remain
protected; do not restart HRC or consume the authorised smoke for this core.

## Implemented scope

`ObserverCoordinator` provides three in-memory operations:

- `arm` accepts one unmatched operation intent with a bounded, process-local
  monotonic timeout;
- `accept` correlates callback-captured `SCHEDULED`, `RUNNING`, and `DONE`
  inputs; and
- `expire` faults a pending arm after its deadline.

`ReplayBuffer` owns positive per-session sequence numbers and returns immutable,
ordered replay windows through `replayAfter`. A replay window reports `OK`,
`GAP`, or `CURSOR_AHEAD`; eviction can never be mistaken for a complete replay.

## Correlation and failure invariants

- At most one arm is unmatched. A second request receives `BUSY` without
  changing observer state.
- Repeating the same request ID and intent is idempotent and never extends its
  original deadline. Reusing the ID for a different intent faults the session.
- A `SCHEDULED` input must match the injected operation profile's exact bundle,
  version, class, and public Job name. Profiles are injected only; the core has
  no API that bypasses the repository's installed-component identity gate.
- Nash names must satisfy the repository's canonical simulation-name policy.
  Viewer and Export names use a separately bounded, Windows-safe staging-leaf
  rule; the later runner remains responsible for proving the private staging
  path, lease, uniqueness, and exact destination.
- Correlated Jobs are tracked by Java reference identity in an `IdentityHashMap`.
  The raw object never enters an emitted event or generated equality, hashing,
  or string representation.
- Multiple already-correlated Jobs may remain queued or running, including two
  Nash Jobs with the same public name. Each receives a distinct positive ID in
  the observer session.
- Only a known `OK`, `CANCEL`, or `ERROR` result with a usable plug-in identity
  can produce a trusted terminal event. An unknown status, invalid event order,
  omitted plug-in identity, or terminal event after an existing observer fault
  is emitted as an explicitly rejected terminal projection and cannot be used
  to advance a workflow.
- Any ambiguity latches the first observer fault and rejects new arms. Exact
  later lifecycle inputs for already tracked Jobs are still recorded as
  rejected evidence rather than silently discarded.
- The core bounds remembered requests, Jobs, and replay events. This first
  feasibility implementation deliberately retains a small number of Job
  identities strongly; production lifetime and retention require a later
  design after feasibility is positive.

## Data boundary

Events contain only session/sequence/time metadata, request and operation
identity, a per-session numeric Job ID, the bounded Job bundle/version/class and
public name, user/system flags, terminal severity/OK/code/plug-in primitives,
and enumerated fault reasons.

Status messages, exception objects, exception text, stack traces, strategies,
licence material, and unrelated HRC memory are excluded. Necessary public Job
names can contain a hand or staging-output name and must remain local and be
treated as sensitive by the later transport and runner.

The HRC bundle/version/class/name recognisers used by the tests come from the
version-specific static findings in [`../../docs/feasibility.md`](../../docs/feasibility.md).
They are not a public or vendor-supported API. Runtime use remains conditional
on the exact eight-component fingerprint and the active-process path check.

## Offline validation

Run on the licensed development host:

```powershell
& .\src\HrcJobObserver\build.ps1
```

The script uses the existing Android Studio JBR compiler and runtime, compiles
with `javac --release 17 -Xlint:all -Werror`, and runs the dependency-free test
harness with assertions enabled. It does not use or copy HRC's Java runtime or
components.

The current 25-test harness covers input invariants, name/profile filtering, arm
idempotency/busy/expiry, callback-time and wrap-safe deadlines, reference
identity, all three operation profiles, two same-name Nash Jobs, normal and
rejected terminal paths, callback-time ordering, post-fault evidence, status
minimisation, replay ordering/gaps/cursor bounds/transactionality/immutability,
and synchronized reader/writer access.

The following remain unvalidated: OSGi resolution, bundle activation, Eclipse
callback capture and latency, callback queueing, serialization, IPC,
authentication, replay across a client connection, packaging, startup,
installation, rollback, HRC runtime correlation, and every standalone-runner
operation.
