# Offline observer runtime assembly

## Status

This package-private Java 17 layer joins the observer core, Eclipse callback
mailbox, and local transport contract in one offline classpath.
`OrderedObserverTransportControl` is the concrete package-private
`ObserverTransportControl` implementation. All four layers must later share one
OSGi bundle and classloader because their Java boundary is deliberately
package-private.

This layer has no OSGi manifest, server startup entry point, secure endpoint or
token provisioning, installer, rollback path, or HRC entry point. The
[offline lifecycle owner](../osgi-lifecycle/README.md) now composes these layers
behind a deliberately disabled activator for synthetic validation. It has never
received a real Eclipse callback. It has never been loaded into or run with
HRC. Its loopback integration test keeps the server and client in one Java
virtual machine (JVM), so it is not cross-process evidence. It does not change
the project's `TO CONFIRM` feasibility verdict.

## Ordered checkpoint and arm control

`OrderedObserverTransportControl` submits both checkpoint and arm operations to
the mailbox's ordered worker stream. The mailbox allocates a callback ticket
when that callback begins, before it reads callback data. A control action
therefore cannot overtake an earlier callback that is still capturing its
bounded payload.

A checkpoint control action follows every lower-ticket callback. The action
atomically expires the pending arm when required and captures the core replay
and fault state. Its positive control ticket becomes the opaque `barrierId`.
The completed checkpoint combines that core snapshot with authoritative
mailbox health captured after the action and before result publication.

An arm uses two ordered control markers:

1. The first marker requires healthy mailbox state and calls the core arm
   operation. Its post-action mailbox state must also be healthy.
1. For an accepted or idempotent arm, the second marker drains every earlier
   callback ticket, including callbacks admitted before the first marker
   completed.
1. The second marker atomically verifies that the same pending arm still exists
   and has not expired. It starts a fresh observer-local lease and emits
   `ARM_CONFIRMED`. Its pre-action and post-action mailbox states must remain
   healthy.

The control returns `ACCEPTED` or `IDEMPOTENT` only when both markers satisfy
those conditions. That response is not yet authority for HRC input. A future
controller must enforce a local round-trip and pre-input margin inside the
confirmed lease. Each successful exact idempotent retry records and starts a
fresh lease. This assembly performs no user-interface action.

## Control bounds and failures

Control waits are bounded. A timed-out unclaimed action is cancelled and cannot
execute later. A timeout after the worker has claimed an action latches a
mailbox infrastructure failure and cannot be treated as a known result.

The mailbox permits one pending control action. It processes callbacks and
that control through one worker. A failure-pending health snapshot is never
healthy, even before the independent first-failure incident is published.
Stopping, callback failure, action failure, or lost control ownership already
visible at the post-action snapshot prevents an actionable result. A live
owner must also fence teardown, and the controller must apply the response and
confirmed-lease budget, because a higher-ticket failure can begin afterwards.

## Offline validation

Run:

```powershell
& .\src\HrcJobObserver\runtime-assembly\build.ps1
```

The build runs the 30-test core harness. It resolves and hashes the exact
offline providers, recompiles the adapter against them, and recompiles the
transport against the core. It does not run the 34-test adapter harness or the
25-test transport harness. Run their own build scripts when the complete
offline validation matrix is required.

The runtime build compiles its main and test sources with Java 17,
`-proc:none`, `-Xlint:all`, and `-Werror`. It then runs ten deterministic
runtime tests. They cover invalid construction, a checkpoint behind a blocked
callback, all three ordered arm operations, expiry before second-marker
confirmation, arm consumption before confirmation, lease start at confirmation,
lease renewal for an exact idempotent retry, same-JVM loopback integration,
non-actionable observer faults, and
invalid-cursor rejection without core mutation.

The current runtime assembly result is 10/10. This is offline validation only.

The build resolves and hashes the exact Eclipse Core Jobs, Equinox Common, and
Eclipse OSGi compile providers from the configured HRC installation. It does
not copy them or start HRC. This is offline compile evidence, not active-process
identity or runtime evidence.

Still unvalidated: real Eclipse callback delivery, OSGi packaging and
activation, listener registration and removal, secure token and endpoint
provisioning, cross-process control, active-process identity checks, startup,
rollback, safe unload, HRC runtime correlation, and standalone-runner use.
