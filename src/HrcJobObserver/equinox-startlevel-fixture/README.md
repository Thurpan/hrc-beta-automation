# Offline Equinox start-level fixture

## Status

This directory contains an isolated Java 17 feasibility fixture. It starts a
clean Equinox framework in a separate JVM. It does not start, stop, inspect, or
modify HRC.

The fixture reads exact installed Eclipse provider JARs. The build verifies
their recorded SHA-256 values before use. It does not copy these JARs into the
repository. The recorded-row scenario includes Eclipse OSGi, Equinox Common,
Core Jobs, Core Runtime, and Core Runtime's direct required Bundles.

The build generates two test-only Bundles under a unique temporary directory:

- A level-4 observer Bundle registers a real manager-wide Eclipse Jobs
  listener before it publishes synthetic controller admission.
- A level-5 producer Bundle attempts controller admission and creates an
  immediately completing Eclipse Job only when publication is active.

The repository contains no Bundle manifest, generated JAR, `plugin.xml`, or
framework storage for this fixture. The build deletes its validated temporary
directory after each run.

The prerequisite scenario uses this synthetic start configuration:

- Equinox Common is persistently started at level 2.
- Eclipse Core Jobs is persistently started at level 3.
- The observer is persistently started at level 4.
- The producer is persistently started at level 5.

The Core Jobs configuration is an intentional fixture prerequisite. It makes
the real Job manager available before the observer starts. This isolates the
observer-to-producer ordering claim from provider-start uncertainty.

The recorded HRC rows differ. Equinox Common is `2,true`, Eclipse Core Jobs is
`4,false`, and the calculator is `5,false`. This fixture does not prove how HRC
starts the non-autostart Core Jobs or calculator Bundles.

The recorded-row scenario matches the relevant installed rows:

- Equinox Common is level 2 with autostart enabled.
- Eclipse Core Jobs is level 4 with autostart disabled.
- Eclipse Core Runtime is level 4 with autostart enabled.
- The observer is level 4 with autostart enabled.
- The synthetic producer is level 5 with autostart enabled.

The scenario also installs Core Runtime's exact direct requirements at their
recorded level 4. It leaves their recorded autostart settings disabled. These
requirements are Core Content Type, Equinox App, Equinox Preferences, Equinox
Registry, and OSGi Preferences Service.

At observer activation, Core Jobs remains resolved, level 4, and not
persistently started. Core Runtime is active, level 4, and persistently started.
The observer registers through `Job.getJobManager()` and then publishes. The
level-5 producer subsequently emits the complete immediate Job lifecycle.

## Verified scenarios

The build launches one new JVM for each scenario.

The success scenario verifies these conditions:

1. The framework activates the observer at level 4.
1. Listener registration and publication finish before the level-5 producer
   starts.
1. The producer receives controller admission only after publication.
1. The real Eclipse Jobs manager delivers `scheduled`, `running`, and `done`
   for an immediately completing Job.
1. Framework shutdown closes callback admission before listener removal.
1. A synthetic stale provider callback is rejected after admission closes.
1. Publication revocation leaves the policy terminal.
1. The policy refuses republish, restart, update, uninstall, and refresh
   requests while active and after termination.

The recorded-row scenario repeats those checks. It also verifies the exact
same-level Core Jobs and Core Runtime states described above.

The failure scenario makes the observer activator fail at level 4. It verifies
these conditions:

1. Equinox reports a `FrameworkEvent.ERROR` with a `BundleException`.
1. The framework still advances to level 5 and activates the producer.
1. The observer does not register a listener or publish.
1. The producer controller attempt is refused.
1. The producer does not schedule a Job.

## Run the fixture

Run this command from the repository root:

```powershell
& .\src\HrcJobObserver\equinox-startlevel-fixture\build.ps1
```

The build uses the shared observer build mutex. It compiles every Java source
with Java 17, `-proc:none`, `-Xlint:all`, and `-Werror`. It uses bounded event
waits and does not use timed sleeps.

Current result: 12/12 prerequisite-scenario tests, 18/18 recorded-row-scenario
tests, and 9/9 failure-scenario tests.

## Evidence boundary

This fixture proves public Equinox start-level behaviour with synthetic
observer and producer Bundles. It proves that the observer can register and
publish in the isolated recorded-provider arrangement. It does not prove that
every exact HRC Job producer starts above level 4. It does not prove HRC
lazy-activation behaviour or exclude other HRC Job producers.

Static evidence records normal HRC application startup only after
`EclipseStarter` reaches level 6. It records the calculator at level 5 with
autostart disabled and lazy activation. It also records that the calculator is
the only Bundle that defines or refers to the exact relevant Job classes. These
facts support the normal startup route. They do not prevent arbitrary
`Bundle.loadClass` use or another early activation mechanism. Keep that
remaining condition in the pre-live static identity gate.

The clean synthetic JVM conditionally addresses the delayed-`done` startup
gap. No synthetic producer can exist before the level-4 listener is published.
This does not close the public-API gap in HRC unless static evidence proves that
exact producer identity and normal clean-launch sequencing prevent every
relevant Job from being instantiated or scheduled before level-4 observer
publication.

The no-runtime-unload result is a policy model. The observer remains loaded
until final framework shutdown. The fixture does not prove provider-level
listener drainage for dynamic Bundle stop, update, uninstall, or refresh.

This fixture does not provide endpoint transport, token transfer, a controller,
an installer, a deployable observer Bundle, or HRC runtime validation. Do not
install its generated Bundles in HRC.
