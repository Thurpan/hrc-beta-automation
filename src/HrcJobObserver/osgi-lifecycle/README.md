# Offline OSGi lifecycle owner

## Status

This directory contains a Java 17 lifecycle owner for the offline observer
layers. It compiles against the exact public Eclipse Jobs and OSGi API providers
recorded by this repository. It has not been installed, resolved, started, or
stopped by HRC.

`HrcJobObserverActivator` is structurally valid for OSGi. Its public no-argument
constructor is deliberately disabled and fails with `BOOTSTRAP_DISABLED` before
it creates a worker, opens a socket, or registers a listener. Offline tests use a
package-private factory. This directory contains no manifest, `plugin.xml`, JAR,
installer, concrete secure endpoint-publication implementation, or live
bootstrap configuration.

## Implemented lifecycle

The offline lifecycle implements this startup order:

1. Start the ordered callback mailbox.
1. Register one manager-wide Eclipse Jobs listener.
1. Run a bounded scan of waiting, executing, and sleeping Jobs.
1. Seal startup callback admission.
1. Run a second bounded Job scan.
1. Drain callbacks admitted during startup and activate callback dispatch.
1. Take an ordered health checkpoint.
1. Start the local transport.
1. Recheck ordered health and transport health.
1. Publish the endpoint through an injected transactional interface.

Startup fails closed on a recognised HRC Job, a recognised Job class from the
wrong Bundle, an oversized baseline, an admitted callback failure, or an
unhealthy ordered checkpoint. A startup failure runs the same cleanup sequence
as normal shutdown.

Shutdown revokes the endpoint publication first. It then closes the transport,
closes listener admissions, removes the listener, drains admitted callbacks,
and closes the mailbox. It verifies that no callback or mailbox ownership count
remains. Cleanup continues after an individual failure and reports an unsafe
result when any stage is unclean.

The lifecycle creates no token or endpoint storage. It accepts these functions
through package-private interfaces. It clones and wipes the token arrays that it
owns. Secure endpoint publication remains unimplemented.

## Deliberate live blockers

The public Eclipse APIs do not make listener registration plus `find(null)` one
atomic operation. A Job can leave the manager before its queued `done` callback
reaches a newly registered listener. The two scans and startup callback gate
reduce observable races, but they do not prove that no earlier relevant Job
existed. The offline harness records this unresolved case and confirms that a
late unarmed `done` callback faults the observer.

`removeJobChangeListener` also does not prove that the provider has finished
with every listener-array snapshot. The local admission gate prevents a stale
callback from reaching the mailbox after closure, but public APIs do not prove
that the Bundle can unload immediately. OSGi can stop a Bundle even when its
`stop` method reports a failure.

These gaps block live activation and safe unload. Do not add an activatable
manifest, install this code, or enable the public constructor until a reviewed
startup and unload proof closes both gaps.

## Offline validation

Run:

```powershell
& .\src\HrcJobObserver\osgi-lifecycle\build.ps1
```

The build runs the 30 core tests and ten runtime-assembly tests. It then compiles
the lifecycle main and test outputs with Java 17, `-proc:none`, `-Xlint:all`, and
`-Werror`. It resolves and hashes the exact Core Jobs, Equinox Common, and
Eclipse OSGi providers from the configured HRC installation. It does not copy
them or start HRC.

The 14 lifecycle tests cover the disabled public activator, exact startup and
shutdown order, relevant and wrong-source baselines, unknown-Job data
minimisation, callbacks during both startup phases, the delayed-`done` gap,
transport and publication rollback, cleanup after transport failure, stale
callbacks after listener removal, and one-shot activator state.

Current result: 30/30 core tests, 10/10 runtime-assembly tests, and 14/14
lifecycle tests. This is offline compile and synthetic-callback evidence only.

Still unvalidated: OSGi resolution, real manager registration or removal,
provider-level callback drainage, real Eclipse callback delivery, secure token
and endpoint publication, manifest and JAR packaging, installation, rollback,
safe unload, HRC startup, and every HRC runtime result.
