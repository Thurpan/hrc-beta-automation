# Offline simpleconfigurator planner

## Status

This directory contains an in-memory planning instrument for the exact recorded
HRC simpleconfigurator baseline. It is not an installer and has no filesystem,
process, network, OSGi, activation, or rollback API.

`SimpleConfiguratorPlanner` accepts caller-supplied `config.ini` and
`bundles.info` bytes. It validates their exact recorded SHA-256 values and the
required configuration facts. It then returns a defensive in-memory proposal
with disposition `OFFLINE_PLAN_ONLY`.

The proposed row is:

```text
net.hrcautomation.jobobserver,0.1.0,plugins/net.hrcautomation.jobobserver_0.1.0.jar,4,true
```

The planner validates canonical JAR and directory Bundle locations. It rejects
duplicates, traversal, malformed UTF-8, mixed line endings, malformed rows, an
existing observer row, and any baseline hash or fact mismatch. It preserves the
source line-ending style. It does not create a JAR or manifest and cannot apply
the proposal.

## Offline validation

Run:

```powershell
& .\src\HrcJobObserver\osgi-packaging\build.ps1
```

The build uses a unique temporary output directory. It compiles with Java 17,
`-proc:none`, `-Xlint:all`, and `-Werror`. A boundary scan rejects file,
network, process, OSGi, activation, manifest, plug-in, and JAR artefacts in this
module.

The 13 synthetic tests cover the recorded policy, LF and CRLF proposals,
determinism, defensive copies, hash and fact mismatches, an existing observer,
malformed UTF-8 and line endings, directory Bundle locations, traversal, and
header validation.

Current result: 13/13. A read-only check also confirmed that the location grammar
accepts all 191 rows in the hash-pinned active `bundles.info`, including its six
directory Bundle locations. No proposal was written to the HRC installation.

Still unvalidated: a deterministic observer JAR, manifest resolution, a guarded
filesystem transaction, backup and rollback, HRC update behaviour, and any live
installation or activation.
