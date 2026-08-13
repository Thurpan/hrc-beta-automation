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
| HRC Beta availability | HRC Beta was installed and running when inspected on 10 August 2026. Its main window title was `HRC Pro [Beta]`. | Inspected the running `hrc.exe` process and its executable path and window title. | CONFIRMED for that inspection. Current process state is TO CONFIRM. |
| HRC Beta version | The executable does not expose a file version or product version. The version in the HRC interface has not been inspected. | Inspected the `hrc.exe` version metadata. | TO CONFIRM |
| Accessibility Insights availability | No executable was present in the two standard `Program Files` locations checked. Other installation methods were not checked. | Checked the standard 64-bit and 32-bit installation paths. | TO CONFIRM |
| Microsoft Inspect availability | The x64 `inspect.exe` is available in Windows Kits `10.0.26100.0`. Its file version is `7.2.0.0`. | Inspected the installed Windows Kits executable and its version metadata. | CONFIRMED |
| Read-only HRC window capture | Codex can capture the current HRC window directly by using its live window handle. Euan does not need to send each screenshot manually. | A direct capture showed the open CI `10.0` Nash Calculation dialog and the HRC Progress pane. | CONFIRMED for discovery only; this does not identify or operate controls. |
| Codex activation side effect | A Codex discovery activation call restored HRC from its maximised state and changed its window bounds. A later native focus-only call preserved the current bounds. | Compared the HRC window rectangle before and after each focus method during the `HU-2` run. | CONFIRMED for the discovery tool only; do not make that activation call part of automation. |
| HRC window state on 11 August | HRC was restored and sized close to the full work area. It was not maximised. No resize, maximise, or activation action was issued during the control-map run. | Euan supplied a full-screen screenshot that showed the restored title-bar state. The control log contained no window-state action. | CONFIRMED for that run. Do not infer current or maximised state from capture dimensions. |
| Coordinate-target failure | An unverified coordinate intended for the hand-tab close control selected the tree root row instead. No setting or output changed. The later close used a point that first showed the exact `Close` tooltip. | Compared the expected target with the captured cursor position and the resulting HRC state during the `HU-2` run. | CONFIRMED for discovery; raw coordinates are not a safe automation path. |

## Owner authorisation and vendor-documentation boundary

On 12 August 2026, Euan attested in the project conversation that HRC Beta's
owner personally authorised him to do anything with HRC for this project if the
use is not commercial. Euan confirmed that the project is personal and wholly
non-commercial. This is user-attested oral permission, not a written vendor
response or evidence of a supported public interface.

The project treats the attested owner permission as covering the local
accessibility runner, read-only component identity checks, the project-owned
startup status observer, and the HRC validation needed for this personal
workflow. Commercial use, licence sharing, redistribution of HRC components,
and unnecessary strategy-data access remain outside scope. All technical,
protected-resource, no-overwrite, and data-safety gates remain in force.

The following is current official vendor-documentation evidence, not legal
advice.

- The [HRC v4+ EULA](https://www.holdemresources.net/legal/eula/hrc_v4)
  licenses the Product in unmodified binary form, limits usage to the provided
  launcher, and says the product and its components may be used only through
  the user interface accessible via that launcher.
- It expressly prohibits automated scraping of information from the product's
  user interface or memory. It separately prohibits decompilation, reverse
  engineering, or modification without the licensor's consent, subject to
  applicable law.
- The published [HRC scripting documentation](https://www.holdemresources.net/docs/scripting/)
  describes JavaScript callbacks for tree-building decisions. Its
  [complete public API](https://www.holdemresources.net/s/updatesites/hrc/latest/scripting/javadoc/allclasses-index.html)
  contains only the
  tree-related `IDecisionContext`, `IPlayerAction`, `IPlayerAction.ActionType`,
  `IPotState`, and `ITreeBuildingScript` types. It exposes no Nash, sampling,
  queue, save, export, Job, callback, or terminal-status type.
- A 12 August 2026 public-interface review covered the official documentation
  index and all ten articles it listed, the full News/changelog page, FAQ,
  download and pricing pages, relevant HRC release posts, the complete public
  scripting Javadoc package/class indices, and exact site searches for API,
  CLI, SDK, plug-in, callback, headless, status, unattended, scheduler, and
  remote-operation terms. It found no advertised lifecycle API, CLI/headless
  solver, per-Job status log, unattended scheduler, or third-party plug-in SDK.
  This is absence from the reviewed public corpus, not proof that no private or
  partner interface exists.
- The official
  [Monte Carlo documentation](https://www.holdemresources.net/docs/monte-carlo-sampling/)
  describes an interactive flow in which the user creates a hand, invokes Run
  Nash Calculation, accepts the dialog, and watches progress and ETA. Its
  cancellation/resume guidance does not expose a programmatic terminal-status
  callback. The only documented log found in the
  [troubleshooting documentation](https://www.holdemresources.net/docs/troubleshooting/)
  is the JVM crash-only `hs_err_pid*.log`, not a calculation result log.
- The official [contact page](https://www.holdemresources.net/contact) lists
  `support@holdemresources.net` and the HRC Discord community.

The earlier written-permission pause is superseded by Euan's attestation. The
unsent mechanism-specific request remains at
[`vendor-permission-request.md`](vendor-permission-request.md) as a scope record
and fallback. Permission removes the authorisation blocker only. It does not
prove feasibility or permit the protected dirty tabs to be discarded.

## Installed component identity gate

The HRC application version remains TO CONFIRM. Version-specific static findings
in this document apply only to this exact installed component set:

| Component | Installed filename | SHA-256 |
| --- | --- | --- |
| Calculator | `net.holdemresources.calculator_4.1.1.202607211244.jar` | `F9FA329B80C265356E6D29C1E98DAF1EFFB06D1536C92BDACEFD0C1EEE90562A` |
| Commons Compress | `org.apache.commons.commons-compress_1.27.1.jar` | `293D80F54B536B74095DCD7EA3CF0A29BBFC3402519281332495F4420D370D16` |
| JFace | `org.eclipse.jface_3.36.0.v20250129-1243.jar` | `E0E28699ECD8783597B3E481D18CFA5A1889FFB9F3007260BD2DD78311779BC4` |
| NatTable core | `org.eclipse.nebula.widgets.nattable.core_2.5.0.202411280718.jar` | `992684F329AECC71E320D17D4D7B968372C3BFB09EF4A50C35DF560B1392D0C7` |
| SWT Win32 | `org.eclipse.swt.win32.win32.x86_64_3.129.0.v20250221-1734.jar` | `5315B5A2E1260CEB125B421CBCD467935ED9666374E57C38AECBAA04E01DFC85` |
| Eclipse Core Jobs | `org.eclipse.core.jobs_3.15.500.v20250204-0817.jar` | `189199CD46A284220B7B97FD59218B533FE9FD8E0AD22258F674A3F2DF4DE7C9` |
| Eclipse UI | `org.eclipse.ui_3.207.100.v20250103-1151.jar` | `550A0E03B8C1939D297BF0ABEDC241507D81AEAC8F1A2A07FC9A96A4264B6101` |
| Eclipse UI Workbench | `org.eclipse.ui.workbench_3.135.0.v20250204-1142.jar` | `04432CFCE181780475CC061F5813F90D3AF3567AD8916C74B7303DDD80850900` |

On 12 August 2026, the one active `hrc.exe` process resolved to
`C:\Users\euanh\AppData\Local\Programs\HoldemResources\HRC Beta\hrc.exe`.
All eight files were rehashed read-only from that installation's `plugins`
directory and matched this table.

### Offline adapter compile-provider evidence

On 12 August 2026, the offline adapter build resolved and rehashed these
additional public API providers from the configured HRC installation path:

| Public API provider | Installed filename | SHA-256 |
| --- | --- | --- |
| Equinox Common | `org.eclipse.equinox.common_3.20.0.v20250129-1348.jar` | `617C5D7E759276B7E9ED363C56A6714B7F21D4A812D533FCB90E48723CC4C001` |
| Eclipse OSGi | `org.eclipse.osgi_3.23.0.v20250228-0640.jar` | `1AC113541A19F0C72C0421FB24058DEFCA7E3C6F282E5EE73F14D2768A9AE653` |

This is offline compile-provider evidence. The build used the configured
installation path. It did not resolve these files from the active `hrc.exe`
process. These providers are not members of the eight-component active-process
runtime gate above. Before live observer use, extend that gate deliberately and
verify both files from the active process installation. This evidence adds no
HRC runtime observation.

### Read-only OSGi configuration evidence

On 12 August 2026, a read-only check of the active HRC installation recorded
the following startup facts:

- `configuration/config.ini` had SHA-256
  `7FB69262C0FCB2C96A605A95B1834C4FC3756724634378E1973C49ACAC0A3C72`;
- `configuration/org.eclipse.equinox.simpleconfigurator/bundles.info` had
  SHA-256
  `A3B776136BAF2323357731CECEEF004C95B2553DB09900979FB277F1FFB2ED41`;
- `org.eclipse.equinox.simpleconfigurator.configUrl` selected that
  `bundles.info` file;
- the default Bundle start level was `4`; and
- `bundles.info` used UTF-8 format version `1` and contained 191 Bundle rows.

The configured simpleconfigurator JAR was
`org.eclipse.equinox.simpleconfigurator_1.5.400.v20250129-0942.jar`, with
SHA-256
`2970D2C5C4253E543431FADBAAE93F5DDC42923DA23348CEC7EF7CD824D7F424`.
`hrc.ini` selected the bundled JustJ Java 21 runtime. Its `java.exe` had SHA-256
`B495803CD2D3315A530EAB39780CF59A43FDC71EE1F3AC6C5969DEDC9387B1AA`
and reported Temurin `21.0.11+10`.

The source/test-only
[simpleconfigurator planner](../src/HrcJobObserver/osgi-packaging/README.md)
pins those two hashes and the required recorded rows. It accepts all 191
recorded locations and produces only an in-memory `OFFLINE_PLAN_ONLY` proposal.
It did not write to HRC. The simpleconfigurator and Java runtime identities are
not enforced inputs to that planner and remain future artefact-build gates.

### Read-only Job-producer and clean-launch evidence

On 12 August 2026, a read-only filesystem audit used the exact configuration
and installed component hashes in this section. It did not interact with the
running HRC process, its UI, or its Eclipse callbacks. The protected dirty tabs
remained untouched.

The audit recorded `hrc.ini` with SHA-256
`DB8461D2FE88A37E238EB76481E4A0BC35DB98CD441947FEE4CF42A6640B1D73`.
It found no framework start-level override there. Static inspection of the
hash-pinned Eclipse OSGi launcher records a normal `EclipseStarter` target-level
fallback of 6. The normal application launch follows completion of that level
advance.

These are the exact relevant `bundles.info` rows from the hash-pinned 191-row
configuration:

```text
net.holdemresources.calculator,4.1.1.202607211244,plugins/net.holdemresources.calculator_4.1.1.202607211244.jar,5,false
org.eclipse.core.jobs,3.15.500.v20250204-0817,plugins/org.eclipse.core.jobs_3.15.500.v20250204-0817.jar,4,false
org.eclipse.core.runtime,3.33.0.v20250206-0919,plugins/org.eclipse.core.runtime_3.33.0.v20250206-0919.jar,4,true
org.eclipse.core.contenttype,3.9.600.v20241001-1711,plugins/org.eclipse.core.contenttype_3.9.600.v20241001-1711.jar,4,false
org.eclipse.equinox.app,1.7.300.v20250130-0528,plugins/org.eclipse.equinox.app_1.7.300.v20250130-0528.jar,4,false
org.eclipse.equinox.preferences,3.11.300.v20250130-0533,plugins/org.eclipse.equinox.preferences_3.11.300.v20250130-0533.jar,4,false
org.eclipse.equinox.registry,3.12.300.v20250129-1129,plugins/org.eclipse.equinox.registry_3.12.300.v20250129-1129.jar,4,false
org.osgi.service.prefs,1.1.2.202109301733,plugins/org.osgi.service.prefs_1.1.2.202109301733.jar,4,false
org.eclipse.equinox.common,3.20.0.v20250129-1348,plugins/org.eclipse.equinox.common_3.20.0.v20250129-1348.jar,2,true
org.eclipse.equinox.simpleconfigurator,1.5.400.v20250129-0942,plugins/org.eclipse.equinox.simpleconfigurator_1.5.400.v20250129-0942.jar,1,true
org.eclipse.osgi,3.23.0.v20250228-0640,plugins/org.eclipse.osgi_3.23.0.v20250228-0640.jar,-1,true
```

In addition to Eclipse OSGi, Equinox Common, and Core Jobs, Core Runtime's
remaining direct requirements are Core Content Type, Equinox App, Equinox
Preferences, and Equinox Registry. OSGi Preferences Service is a mandatory
requirement of Equinox Preferences, not a direct Core Runtime requirement.

The audit scanned every configured artefact and each embedded JAR. Only the
calculator archive defines or literally refers to the exact Job classes
`net.holdemresources.internal.bQ`, `net.holdemresources.internal.bT`, and
`net.holdemresources.internal.af`. No Bundle at level 4 or lower defines or
literally refers to those classes. The exact class hashes and observed roles
are:

| Class | SHA-256 | Static provenance |
| --- | --- | --- |
| `net.holdemresources.internal.bQ` | `869DA5F1E4AE61E5745A05221547004D77AD0F1A23CCCB3B604E3B2DA57710A4` | Nash calculation Job; constructed and scheduled by `bO`. |
| `net.holdemresources.internal.bT` | `3841A1FC150B213D98361C62AF070211ACC8F34E3BD2F30676FF48DCE443A250` | Viewer Save Job; constructed and scheduled by `bO`. |
| `net.holdemresources.internal.af` | `5988ECB412ED4C5E9C5CAEFC2BFC09944F3259E59697738A1A318723B45FA80D` | Strategy Export Job; constructed and scheduled by `ac`. |
| `net.holdemresources.internal.bO` | `00B78046919B00BD2E3903F65B2919D21DEE83E6131DBED953C649AD94A24AED` | Observed caller for `bQ` and `bT`. |
| `net.holdemresources.internal.ac` | `3A01AD29808CCAC6690692341B3C0B474CBD01989CDD02509E762325F971A6B7` | Observed caller for `af`. |

The three Job types are package-private and `final`. The calculator manifest
exports no package and declares no dynamic import. The installed scan found no
fragment, Declarative Services metadata, or startup extension that supplies an
alternate route to these exact Jobs. The exact Job types are not executable
extension declarations. The calculator uses lazy activation and is recorded at
level `5,false`.

Together, these facts support the normal clean-launch sequence: a persistently
started level-4 observer can register and publish before the level-5 calculator
application can schedule an exact relevant Job. Start levels are ordering, not
an access-control boundary. They do not prevent arbitrary `Bundle.loadClass`,
reflection, or another unobserved early activation mechanism. Treat any
configuration, provider, class, or startup-route mismatch as a stop condition.

### Offline Equinox start-level fixture evidence

The source/test-only
[Equinox start-level fixture](../src/HrcJobObserver/equinox-startlevel-fixture/README.md)
starts each scenario in a fresh JVM with unique temporary framework storage. It
hash-checks these exact installed providers before use:

| Provider | Installed filename | SHA-256 |
| --- | --- | --- |
| Eclipse Core Jobs | `org.eclipse.core.jobs_3.15.500.v20250204-0817.jar` | `189199CD46A284220B7B97FD59218B533FE9FD8E0AD22258F674A3F2DF4DE7C9` |
| Equinox Common | `org.eclipse.equinox.common_3.20.0.v20250129-1348.jar` | `617C5D7E759276B7E9ED363C56A6714B7F21D4A812D533FCB90E48723CC4C001` |
| Eclipse OSGi | `org.eclipse.osgi_3.23.0.v20250228-0640.jar` | `1AC113541A19F0C72C0421FB24058DEFCA7E3C6F282E5EE73F14D2768A9AE653` |
| Eclipse Core Runtime | `org.eclipse.core.runtime_3.33.0.v20250206-0919.jar` | `FF59EFB6FB7D610D819D44777BD306860EC7926CD31AC95419E729EDFB38CC02` |
| Eclipse Core Content Type | `org.eclipse.core.contenttype_3.9.600.v20241001-1711.jar` | `D8A2974F5EC3D7CFB8E3E177AA7303BABED0A1565DBE5416084A751044255002` |
| Equinox App | `org.eclipse.equinox.app_1.7.300.v20250130-0528.jar` | `CA5D75F9228510F19250EF947E340A7A2CDEBD1A888EFDF13A3F3A4B114D4D2E` |
| Equinox Preferences | `org.eclipse.equinox.preferences_3.11.300.v20250130-0533.jar` | `7F8B452EE5F9D836DB8534C6BD1A29A2662352D868FF94856B6B54BC8032A999` |
| Equinox Registry | `org.eclipse.equinox.registry_3.12.300.v20250129-1129.jar` | `E2145418FF639B44FF50E83B66848F40AE38C869DB6B8F95044BBB5D0D652722` |
| OSGi Preferences Service | `org.osgi.service.prefs_1.1.2.202109301733.jar` | `43C7C870710E363405D422DA653CCE0D798A4537F76E4930F79BCEADD3A55345` |

The fixture generates only temporary test Bundles. It leaves no manifest, JAR,
class file, `plugin.xml`, or framework storage in the repository. It does not
install the calculator. A synthetic level-5 producer represents only the
ordering boundary.

The prerequisite scenario passed 12/12 tests with Common at `2,true`, Core Jobs
at the intentional fixture prerequisite `3,true`, the observer at `4,true`, and
the producer at `5,true`. It proves registration, publication, admission, and a
real immediate Eclipse Job lifecycle when Core Jobs is available first.

The recorded-row scenario passed 18/18 tests. At observer activation it
asserted every installed provider's actual state, Bundle start level, and
persistent-start flag. Common was active at level 2 and persistent. Core Jobs
was resolved at level 4 and non-persistent. Core Runtime was active at level 4
and persistent. Core Content Type, Equinox App, Equinox Preferences, Equinox
Registry, and OSGi Preferences Service were resolved at level 4 and
non-persistent. The level-4 observer registered through the real Jobs manager
and published before the synthetic level-5 producer emitted `scheduled`,
`running`, and `done`.

The observer-failure scenario passed 9/9 tests. Equinox emitted
`FrameworkEvent.ERROR` with a `BundleException`, still advanced to level 5, and
activated the producer. No listener or publication existed. The synthetic
controller was refused and no Job was scheduled. A production controller must
therefore refuse all HRC input when publication is absent or invalid; framework
advancement is not an observer-success oracle.

The fixture also validates a no-runtime-unload policy model. Publication is a
prerequisite for admission. The ordered stop-and-revoke sequence is terminal.
Restart, republish, update, uninstall, and refresh are refused, and a stale
callback is rejected. The observer remains loaded until final framework
shutdown. This policy result does not prove dynamic provider-level listener
drainage or safe live HRC unload.

This is isolated public-Equinox evidence, not HRC runtime evidence. It does not
prove an installer, a deployable observer Bundle, actual HRC listener delivery,
arbitrary early class-loading absence, or the protected tabs' safe disposition.
`Feasibility` remains `TO CONFIRM`.

### Offline Windows bootstrap implementation evidence

On 12 August 2026, the source/test-only .NET 8 Windows bootstrap harness at
checkpoint `2ea4d0e` passed 40/40 tests on the licensed host. Checkpoint
`6283fe8` expanded the same harness and passed 55/55 tests on the licensed host.
Checkpoint `be83a90` added guarded file publication and passed 66/66 tests.
Checkpoint `efc399a` adds retained artefact identity and passes 71/71 tests: 20
primitive tests, 8 descriptor and protocol tests, 27 broker and in-memory-store
tests, 11 filesystem tests, and 5 artefact-identity tests.
Checkpoint `c38bf29` adds the protected app-local artefact-set primitive and
passes 77/77 tests. The current total is 20 primitive tests, 8 descriptor and
protocol tests, 27 broker and in-memory-store tests, 11 filesystem tests, 5
single-file artefact-identity tests, and 6 protected app-local artefact-set
tests.
None of these builds installed, loaded, attached to, or interacted with HRC.

The implementation records one process ID, creation `FILETIME`, full image
path, user SID, logon SID, token session ID, and process session ID. It retains
the process handle and checks liveness and creation identity before matching.
The pipe server retrieves and matches the client identity. The pipe client
retrieves and matches the server identity. Separate tests reject every field
mismatch and exercise each endpoint's rejection path.

Pipe creation requests a protected DACL with exactly two full-access entries:
`SYSTEM` and the bound user. It reads back the applied DACL and fails unless its
canonical form equals the requested DACL. The harness directly confirmed the
protected flag, both trustees, full-access rights, and exactly two entries.

The candidate pipe accepts frames from 1 through 8,192 bytes. Server acceptance,
client connection, and each endpoint's one send and one receive require a
positive timeout of at most 30 seconds. The connection deadlines cover PID
lookup, process capture, exact peer authentication, and authenticated-peer
publication. Connection operations also accept caller cancellation. An
admitted cancellation, I/O failure, malformed received frame, or timeout
disposes the channel. Tests cover delayed synchronous peer authentication,
connection timeout and cancellation, pending-operation disposal, and exact
pipe-name release. Native peer-PID and DACL calls retain the safe pipe handle
until each call completes.

Most primitive tests run both pipe endpoints as tasks inside one process. Two
primitive tests launch the harness as synthetic child peers. The first
requires distinct parent and child PIDs, nonzero process-creation identities,
two-sided full process-binding checks, fixed public-frame exchange, silent child
output, and a clean child exit. The second retains separate parent, expected-
child, and wrong-child identity leases while proving that the server rejects the
wrong live child and the untouched expected child exits cleanly. The child gets
a fixed public mode argument. When launched through `dotnet.exe`, the absolute
harness assembly path is also a public host argument. Redirected stdin carries
the public parent PID and pipe name. No bearer token, endpoint descriptor, HRC
path, licence data, or poker data crosses this test channel.

Eight additional in-memory tests cover the new descriptor and protocol models.
The descriptor has one canonical bounded encoding. It binds the loopback
endpoint, publication identity, claim-pipe name, and exact observer and broker
process identities with a domain-separated HMAC-SHA256 tag. It requires
distinct observer and broker processes in the same user, logon, token session,
and process session. Creation and verification enforce an explicit caller-
supplied maximum lifetime. Parsing proves structure only. Authentication
requires a later secure token claim, HMAC check, exact binding check, and
freshness check.

The protocol defines eight type- and role-bound messages across four one-shot
exchanges. They model publication and acknowledgement, claim and grant, a
separate possession receipt and final acknowledgement, and revocation and
acknowledgement. The possession receipt uses a separate HMAC domain and binds
the publication, descriptor digest, controller nonce, and receipt nonce. A
phase-bound decoder wipes its complete owned input frame on success or failure.
Secret-bearing messages and encoded frames own and wipe their mutable buffers.

Twenty-seven broker and store tests include a capacity-one asynchronous
in-memory reference publisher. The store validates and clones one canonical
descriptor. Each read returns an independent wipeable snapshot. Successful
publication returns an opaque store-affine lease. The lease coalesces exact
removal and caches its terminal result. Exact entry identity and cross-store
checks prevent an old owner from removing a later equal publication after an
ABA sequence. Removal wipes the owned descriptor buffer. Store disposal
rejects an active publication instead of claiming cleanup.

The working snapshot adds an internal `FileBootstrapPublicationStore` and an
independent `FileBootstrapPublicationReader`. Each accepts only a
caller-supplied, already-existing protected local NTFS directory. The expected
owner must equal the current process account SID. The directory and files require an
exact protected DACL for that account and `SYSTEM`. This is not logon-SID
isolation; another process in a different logon session for the same account is
inside the filesystem DACL boundary. A retained directory handle rejects
reparse points and deliberately denies delete sharing. This pins the validated
namespace for the store or reader lifetime.

The file store reserves the fixed public name `endpoint-v1.bin`. It accepts one
canonical descriptor and does not publish the bearer token. It creates a random
temporary file with `CREATE_NEW`, writes and flushes the canonical bytes, and
validates exact read-back, DACL, path, volume, and file identity. It uses native
`NtSetInformationFile` with `FileRenameInformation` class 10, the retained
directory as `RootDirectory`, and replacement disabled. It reopens the final
name and requires the same file identity. The retained publication handle
denies new write and delete access. The store checks the fixed name-to-file
identity again immediately before it returns the lease.

The store-affine lease uses POSIX handle deletion and bounded enumeration
through the retained directory to prove the fixed name absent. An indeterminate
terminal removal cannot claim absence and forbids store reuse. An ABA
replacement is preserved and rejected, while retained operating-system handles
can still be released. The independent reader repeats the guarded directory and
final-file checks. It returns a separate wipeable snapshot. The snapshot proves
canonical structure only; authentication still requires the securely claimed
token and exact HMAC and binding checks.

The 11 filesystem cases cover exact public-byte publication and removal,
capacity and collision paths, independent reader snapshots, malformed and
wrongly secured state, ABA and file-identity replacement, cancellation and
deadline cleanup, late verified removal, namespace pinning, bounded multi-page
enumeration, real fixed-leaf and root junction rejection, and cross-directory
retained-root rename without replacement. The deadline and cancellation checks
are cooperative. They do not hard-preempt a blocking synchronous native call.

Five artefact-identity cases exercise `TrustedArtifactIdentity` against one
caller-supplied canonical DOS path. The path must name a file on a fixed local
drive and Mount Manager volume. The primitive opens the default data stream
with a retained read handle that denies new data-write and delete access. This
sharing guarantee does not deny attribute or extended-attribute access.

The primitive verifies the expected default-stream length and SHA-256, a single
link, no reparse ancestor or leaf, the final handle path, volume serial number,
and 128-bit `FILE_ID`. Revalidation reopens the path and detects path, identity,
length, or digest drift. It is detection-only and does not make a later
path-based process launch atomic.

One verified file does not bind mutable siblings. The tests specifically show
that a sibling DLL and `.runtimeconfig.json` can change while the leased file
continues to revalidate. The lease therefore does not bind a framework-
dependent apphost's DLL, `.deps.json`, `.runtimeconfig.json`, or selected .NET
runtime.

Six protected app-local artefact-set cases require one caller-supplied
canonical DOS directory on local NTFS. The root must have an exact protected
DACL for the current process account and `SYSTEM`. The caller supplies 1
through 128 expected files. Each entry uses one exact-case printable ASCII
Windows filename with an expected default-stream length and SHA-256. Every
directory entry must be expected. An extra PDB, `.runtimeconfig.dev.json`, or
subdirectory fails the scan.

The primitive retains every expected default stream through the single-file
lease. Each member is pinned by its expected length, SHA-256, volume serial
number, and 128-bit `FILE_ID`. One caller-supplied absolute deadline covers the
complete open operation. A domain-separated canonical digest binds the
designated executable and the ordinally sorted exact names, lengths, and
SHA-256 values. Revalidation scans the exact entry set before and after it
revalidates every retained member.

The retained protected root permits new child creation. The set is a snapshot
and detection control only. A race remains between the last revalidation and a
later path-based loader action. This proof does not authenticate release
provenance or bind the selected shared .NET runtime. It has no independently
trusted release manifest that authenticates the complete production artefact
set and its canonical digest. It also proves no member file ACL, signature,
launch atomicity, launched-process identity, production role executables,
containment, private handoff, role-bound `READY`, Java integration, or HRC
runtime behaviour.

The one-shot broker runs in the main harness process. Long-lived synthetic
observer and controller child modes run in two child processes. All three roles
must be distinct and must match one user, logon, token session, and process
session. The broker requires its own exact current-process binding. The
synthetic roles execute all four protected-pipe exchanges: publish, claim and
grant, separate receipt and final acknowledgement, and revoke.

The publish acknowledgement is sent only after the descriptor is visible
through the injected publisher. Claim and revoke workers validate the exact
publication and descriptor digest before they can complete. Arbitration
selects one valid winner and removes the exact publication lease before any
claim grant or revocation acknowledgement. A faulted or unknown removal cannot
claim publication absence. Removal verified only after its deadline still
fails the session before terminal acknowledgement.
The tests cover a valid claim win, a valid revoke win, simultaneous release,
and both directions where a valid winner is selected before an already-
completed malformed loser is inspected. The malformed loser makes the whole
session terminal before any winner acknowledgement.

After a valid winner, the broker cancels the other worker, drains it within the
remaining bound, and closes its one-shot pipe. Only winner-induced cancellation
is ignored. An independent loser failure remains terminal. The first timeout,
cancellation, transcript mismatch, proof mismatch, I/O uncertainty, or store-
ownership failure ends the session. The broker does not retry or republish.

An injected `TimeProvider` supplies fixed absolute monotonic publication and
session deadlines. The publication budget is capped by the remaining session
budget when publication starts. Later phases receive only the remaining
duration; no phase resets either deadline. These are cooperative budget checks.
They do not hard-preempt an arbitrary blocking native call. The claim path
disposes the broker-owned grant token and encoded grant before receipt
processing. It disposes the accepted receipt proof and wipes the retained
broker token before the final acknowledgement. The revocation path wipes the
retained token before its acknowledgement.

The broker implements `IAsyncDisposable`. Coalesced disposal cancels and awaits
a running session. `RunAsync` remains the authoritative protocol-failure
channel. `DisposeAsync` separately reports cancellation-request and cleanup
failures. Cleanup starts one non-abandonable exact removal, wipes the retained
token, and attempts every pipe close before it awaits the removal result.
Primary protocol failure and cleanup failure remain independently observable.
Adversarial tests cover cancellation before publication commit,
disposal before and during publication, commit returned after disposal,
synchronous disposal re-entry from the publisher, a throwing cancellation
callback, blocked and faulting removal, unknown removal status, combined
protocol and removal failures, and exact pipe-name release. These tests do not
prove process-crash cleanup.

The broker children receive only fixed public role arguments. Their cleared
environment contains no secret. Public role commands use redirected standard
input, but the bearer token travels only on authenticated protected protocol
pipes. Each long-lived synthetic child mode has an explicit successful exit
status and must write no standard output or standard error.

This is offline existing-directory publication, protected app-local artefact-
set, asynchronous in-memory publisher, and synthetic three-process broker
evidence only. The module has no Windows known-folder resolution, protected
LocalAppData hierarchy provisioning or provenance, stale or crash recovery,
secure initial pipe-name handoff, independently trusted release manifest,
dedicated production role executables, atomic kill-on-close containment,
role-bound `READY`, Java bridge, controller integration, or HRC entry point. The
harness's kill-and-bounded-wait failure cleanup is not crash containment. The
module adds no HRC runtime observation. `Feasibility` remains `TO CONFIRM`.

The runner must first identify the one active HRC process and resolve the
`plugins` directory from that process's own `hrc.exe` installation. It must
rehash these exact files there before relying on the NatTable, Rename, Nash Job,
Viewer Save, Export, Finish, or hand-tab-close findings. A match is necessary
but does not prove live focus, retained preferences, or runtime state. Any
process, installation path, filename, or hash mismatch stops the run and reopens
feasibility validation.

## Corrected context

| Previous statement | Observed evidence | Resolution |
| --- | --- | --- |
| The licensed host used an AMD Ryzen 9 5950X processor. | `EM-3960X` reports an AMD Ryzen Threadripper 3960X 24-Core Processor. | Use the observed host and processor. The earlier 5950X reference is incorrect for this host. |
| Post-Finish Hand Settings contained `Tree Statistics and Abstractions`. | Hand Settings showed `Hand Data`, `Equity Model`, `Treeconfig`, and `Engine`. No `Tree Statistics and Abstractions` page was visible. | Do not require tree statistics for this workflow. |

`Tree Statistics and Abstractions` is visible inside the pre-Finish Betting
Setup page. This does not contradict the post-Finish Hand Settings finding.

## Installed NatTable inspection on 11 August 2026

This is static evidence from the installed HRC components. It is separate from
the live UI evidence below and does not by itself prove that HRC receives a key
sequence or changes the intended cell.

- The installed calculator component
  `net.holdemresources.calculator_4.1.1.202607211244.jar` builds the Stacks and
  Blinds player grid with Eclipse Nebula NatTable `2.5.0` from installed
  dependency `org.eclipse.nebula.widgets.nattable.core_2.5.0.202411280718.jar`
  on an SWT Canvas.
- Its top-left cell is a combo with `Auto`, `HU`, and `3-max` through `10-max`.
  The NatTable has a SelectionLayer plus the default selection and edit
  bindings.
- The installed SelectionLayer starts without a selected cell. The HRC focus
  listener changes the cell painter but does not initialise a selection. This
  is consistent with the earlier observation that Home, arrows, and F2 had no
  visible effect immediately after the grid first received focus.
- The installed bindings map `Ctrl+A` to Select All, `Ctrl+Home` to movement to
  the origin, Space and F2 to cell editing, arrows and Tab to cell movement,
  Enter to vertical movement, and `Ctrl+C` to raw selected-cell copy.
- The static structure supports a focus, `Ctrl+A`, `Ctrl+Home`, Space or F2
  bootstrap hypothesis. A live check was still required because the installed
  bytecode cannot establish the dialog's Tab route, current focus, displayed
  values, or HRC's resulting state.

### Installed Nash and export dialog inspection

These findings originated as static evidence from calculator plug-in `4.1.1`.
Run 16 later live-confirmed the supervised Nash grid bootstrap, editing, a
visible Reset Strategies checkmark, Cancel-button invocation, and `Alt+F4`
closure without submission. Run 17 then confirmed exact supervised per-cell raw
read-back and the required reset pair `false,true`, followed by restoration to
`false,false` before closing.
Standalone foreground, native-focus, and delivery assertions remain TO CONFIRM.

- Nash Calculation uses the exact native shell title `Nash Calculation` and
  explicitly gives `OK` initial focus. Enter immediately after opening the
  dialog is therefore unsafe because it can submit a calculation. Run 16 used
  Enter only after the CI cell editor was visibly active.
- Its two-column NatTable model has seven rows: `CFR Algorithm`, `Scope`,
  `Run Sampling`, `Samples (mio.)`, `CI Target`, `Reset Regret`, and
  `Reset Strategies`. Six value rows are visible at once because Samples and CI
  Target are conditional on the sampling mode. The configured values use combo,
  integer, double, and checkbox editors. Static model inspection encodes one
  logical reset mode, but the raw Boolean presentation is asymmetric:
  `false,false` means no reset; `false,true` means Reset Strategies;
  `true,true` means Reset Regret; and `true,false` is unreachable in the
  inspected model. The handler edits a cloned configuration. After OK, it
  derives the reset action and immediately stores a retained clone with reset
  mode cleared before it queues sampling. A reset passed to that calculation is
  therefore one-shot. Run 16 produced ambiguous
  selection styling across both reset cells; it did not establish either raw
  value. Automation must read back the exact pair and must not infer it from cell
  highlighting.
- Nash retains most accepted settings across openings. A runner must read and
  explicitly verify every required value rather than trust initial defaults or
  values left by the previous calculation.
- Every accepted Nash submission constructs and schedules a new user-visible
  Eclipse Job with public name `<hand-name>: Monte Carlo Sampling`. The CI `10`
  and CI `1` Jobs are distinct objects, do not replace or cancel one another,
  and share a conflicting per-hand scheduling rule, so they serialise. This is
  queue serialisation, not dependency success: CI `1` becomes eligible after CI
  `10` ends even when the first Job is cancelled or fails.
- The exact static lifecycle is `scheduled`/`WAITING`, `aboutToRun` or
  `running`/`RUNNING`, then `done` with result severity `OK`, `CANCEL`, or
  `ERROR`. The running target text is `MC-CFR [Target CI < %.2f]`, initially
  followed by `Sampling...`; an error uses exact message
  `An error ocurred during sampling.` Each result must be checked independently.
- Both Nash Jobs have the same public name and no public UUID or CI field. CI is
  exposed only in the task text after that Job starts. Exact association requires
  retaining the Java Job object received by the scheduled event and matching
  later events by object identity and submission order.
- HRC registers the legacy Eclipse UI Workbench Progress view. That view removes
  a Job from its active model when the Job's `done` event arrives. Its
  finished-Job model retains an individual Job only when that exact object has
  Eclipse's `KEEP_PROPERTY` or `KEEPONE_PROPERTY` set to true, or when its
  result severity is `ERROR`. HRC sets neither keep property on its Nash Jobs.
  The installed Progress view menu exposes only Remove All Finished Operations
  and Preferences, and its preferences expose only Show sleeping and system
  operations. There is no global retain-completed-jobs or history setting.
  Successful and cancelled Nash Jobs can therefore both disappear and leave
  exact idle text
  `No operations to display at this time.` A UI-only observer cannot
  distinguish success from cancellation. The only exact contract found is the
  in-process Eclipse Jobs API and its `IJobChangeListener` events; no supported
  external event, IPC, structured Nash log, or Progress-history hook was found.
  An in-process bridge is a separate architecture and implementation decision.
  The recorded owner authorisation covers the minimal exact-status observer
  design, but it does not prove that design works.
- Achieved CI remains job-local and is never stored on the hand. Success,
  cancellation, and error all preserve accumulated strategy/sample state, store
  the same root-derived data, update the timestamp and dirty state, and invoke
  the same editor refresh. Root sample count can therefore be partial and is not
  a success oracle.
- Viewer Save serialises the current model regardless of why the preceding
  per-hand rule was released; it does not inspect a predecessor Job or result.
  A successful Viewer Job and its file metadata prove ordering and save success
  only, not either Nash result. No status/history, outcome-specific command
  state, or retained finished-Job preference closes the gap externally. Eclipse
  can retain an individual `OK`/`CANCEL` Job only through a property on that
  exact in-process object; HRC does not set it. Severity `ERROR` remains the one
  externally promising retained terminal state.
- The Nash NatTable has the same default selection, edit, and raw-copy
  bindings. Run 16 live-confirmed supervised grid entry, `Ctrl+A`, mandatory
  `Ctrl+Home`, row movement, and editing. Run 17 used `Ctrl+C` to read each value
  cell exactly. One whole-grid `Ctrl+A`, `Ctrl+C` attempt copied only the origin
  label, so it is not a supported atomic snapshot route. Escape can cancel an
  active cell editor, but it does not dismiss Nash Calculation from the grid. From
  initial OK focus, Tab visibly focused Cancel and Space invoked it. `Alt+F4`
  closed the dialog from the grid without submission. Never press Enter while OK
  or the default button owns focus. Exact native focus remains TO CONFIRM.
- Export Strategies has an exact in-dialog title and instruction, but static
  inspection did not establish a matching native shell caption. It must be
  identified by ownership plus the exact descendant title, message, scope
  choices, and controls.
- Its scope choices are `Manual Selection`, `Complete Export`, `All Strategies,
  Limited depth`, and `Selected Spot, Limited Depth`. Scope, Depth,
  PrettyPrint JSON, and Node Filter Threshold are retained settings and may
  change even when the dialog is cancelled. Every value must be read and
  verified on every export.
- In the inspected calculator plug-in `4.1.1` bytecode, `Complete Export`
  selects unlimited depth.
  The visible Depth spinner is only semantically applied to the two Limited
  Depth modes. The demonstrated value `16` can still be read and preserved, but
  it does not limit a Complete Export in this version.
- The authorised workflow must explicitly select and read back
  `*.zip Archived Json`; Complete Export does not invariantly select it. If the
  target exists, HRC can show `Confirm save as` and ask whether to replace it.
  This is only a pre-write check. The installed writer later opens the final
  target with create-and-truncate semantics rather than atomic create-new, so a
  file created in the remaining race can be silently overwritten.
- Complete Export's exact Job name is
  `Exporting ranges to <target-filename>`, including the extension. An
  execution failure can delete the target, while cancellation or other errors
  can leave a partial target. The accessible live terminal presentation remains
  TO CONFIRM. File presence, disappearance, or idle is not a success oracle.
- The installed calculator plug-in `4.1.1` contains two export-file helpers.
  Helper A exposes
  `*.zip Archived Json` at index `0` and `*.txt Plain Text` at index `1`; helper
  B exposes only ZIP. The writer always consumes helper A's retained index even
  when helper B supplied the path. A prior accepted text export through A can
  therefore make a later ZIP-only dialog write plain text to the selected
  `.zip` path while the Export Job still returns `OK`.
- Manual Selection always uses helper B. A non-Manual scope uses B only for a
  `bM` hand whose selector field is non-null; otherwise it uses A. Each helper's
  retained index initialises to ZIP (`0`) when Export Strategies is initialised
  in a fresh JVM/classloader. A genuine restart resets A, but static evidence
  does not prove that current dirty tabs can be restored safely, so restart is
  not an allowed remedy for this session.
- Cancel, a directory result, and overwrite-prompt rejection do not update a
  helper's retained index. Only accepting a normalised, non-directory path
  stores the invoked dialog's selected index. Accepting through B updates B only
  and cannot repair A.
- Export Strategies closes after its OK path invokes Save As even when that file
  dialog returns no path. Export-dialog disappearance therefore proves neither
  submission nor success; no Job is scheduled for a null path. Only the exact
  scheduled `Exporting ranges to <staging-filename>` identity, including the
  extension, proves submission.
- A newly constructed Hand Setup hand initialises the state that chooses helper
  A to null. Exhaustive field-reference tracing found that Rename, Nash, and
  Viewer Save do not change it, so the intended full fresh-hand sequence reaches
  Complete Export through A and must expose the exact two-filter list. Selecting
  and reading back ZIP in the actual accepted staging export sets A to `0`
  before the Job is submitted. A ZIP-only dialog is a stop condition.
- Under the authorised no-ZIP-inspection boundary, successful Job identity plus
  new/non-empty/stable metadata cannot independently distinguish archive from
  plain text. The two-filter list and exact selected ZIP value are therefore
  mandatory preconditions and remain TO CONFIRM LIVE through the standalone
  design. The reserved smoke must not be consumed first.
- The runner must acquire an exclusive system-wide HRC-control lease and a validated,
  exclusively owned staging namespace. An atomically reserved private
  high-entropy directory is a candidate, not proof of ownership by itself. Hold
  the lease from before the first automated HRC input through both canonical
  promotions, target-tab closure, and final state verification. Prohibit another
  runner, all manual HRC interaction, a second HRC process, and other automation
  in scope. A random filename in the shared folder is
  insufficient. It must cancel and stop on any overwrite prompt or unexpected
  Job, dialog, file, or input transition, require an identity-matched successful
  terminal Job plus new/non-empty/stable staging-file metadata, and promote to
  the simulation filename with fail-if-exists semantics. The exact namespace
  and lease mechanism remain TO CONFIRM. The authorised smoke does not permit
  inspecting ZIP contents, and partial output must not be deleted automatically.

### Installed Rename and Viewer Save inspection

These are static findings from calculator plug-in `4.1.1`. Run 19 supplied the
separate live evidence for dialog entry, visible values, failed provider actions,
and cancellation without output.

- Rename Hand is a JFace `InputDialog` with exact title `Rename Hand`, message
  `Rename to:`, an initial name selected in its single-line edit, and OK as the
  default button. The initial value removes every unanchored lowercase `.hrcz`
  or `.hrcv` occurrence from the current editor name. Enter is therefore unsafe
  until exact edit focus and value are proved; a direct exact-button action is
  preferable. Static JFace calls `Text.setFocus()` and `selectAll()`, but that is
  not live native-focus evidence; the provider contradicted the visible state.
- The handler silently opens no dialog when the active input is not a hand. Each
  valid invocation constructs a fresh `InputDialog`, and only OK calls the
  rename setter.
- Native enumeration must distinguish the writable name edit from JFace's
  hidden/read-only error edit by label relationship, writability, enabled state,
  and style rather than class alone. Require exactly one visible HRC-owned
  `Rename Hand` `#32770`, owner equal to the asserted main HRC window, and exact
  prompt/control roles. Freshly re-enumerate children immediately before guarded
  `WM_SETTEXT` and again before the one-shot `BM_CLICK`; never reuse cached HWNDs
  or retry an unknown outcome.
- Rename length validation permits `1` through `100` Java UTF-16 code units and returns
  `Name is too long.` above `100`. Null or empty input produces a hidden blank
  validation result that disables OK, so button enabled state is mandatory.
  Separate character validation returns `Name contains invalid characters.` for
  backslash, slash, apostrophe, double
  quote, `<`, or `>`. It does not trim, check uniqueness, or reject every
  Windows-filename metacharacter. Same-name and duplicate-name values are also
  accepted. The runner must apply its own stricter, unique simulation-name
  policy. Compare bases after separating the independent leading dirty `*`:
  require active base != requested base and requested base absent from all open
  hand-tab bases before opening the dialog.
- Project normalisation defines a hand-tab base by stripping at most one leading
  dirty `*`, then one terminal `.hrcv` or `.hrcz` suffix case-insensitively. It
  does not strip embedded suffix text. Compare normalised bases with ordinal,
  case-insensitive semantics. A requested simulation name must have no HRC
  suffix, match `^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$`, not end in `.`, and not be a
  Windows reserved device base such as `CON`, `PRN`, `AUX`, `NUL`, `COM1`–`COM9`,
  or `LPT1`–`LPT9`, case-insensitively and including before any dot. This ASCII
  allow-list also satisfies the
  installed `100` UTF-16-unit limit and rejects trailing spaces and every Windows
  filename metacharacter.
- Accepted Rename stores the exact string synchronously and notifies the Hand
  Editor, which updates that editor's part name. It does not save, queue a job,
  or write a file. A reusable success oracle must snapshot editor identity,
  selection, count, full title set, and dirty state; build the expected full
  title by preserving the active tab's leading dirty decoration; require exact
  replacement of old full title by expected full title; require the same editor
  to remain selected; and reject every other tab change. Rename does not alter
  the dirty state. Non-OK closure calls no setter and retains no typed text.
- Viewer Save As offers filters in exact index order: `*.hrcz Complete Save`,
  `*.hrcv Viewer Save`, and `*.json Hand Config`. The process starts at Complete
  Save, then retains the last accepted Save As filter index. Cancel does not
  update the index. The shared last-folder holder also changes after an accepted
  Save As selection and can be changed by normal Save. These values can persist
  even if the later Viewer Job fails or is cancelled; neither path nor type may
  be trusted.
- HRC enforces the selected extension case-sensitively after acceptance. It can
  strip a recognised lowercase HRC suffix before appending the chosen suffix;
  uppercase variants can become doubled. The exact lowercase filename,
  extension, type, and destination must be read back before Save.
- An existing normalised target produces exact prompt `Confirm save as` with
  message `<target-filename> exists, do you wish to replace it?`, including the
  extension. The workflow must
  choose Cancel and stop. Enter is dangerous.
- Viewer Save schedules a user Job named
  `Saving hand to: <target-filename>`, including the extension, on the
  same per-hand scheduling rule as Nash. Its subtasks are `Writing tree
  structure.`, `Writing node data.`, and `Writing index.` It does not rename the
  hand, set the primary save path, or clear the dirty tab, so the later
  `Save Resource` prompt is expected. The shared rule serialises it behind the
  Nash Jobs but does not make it conditional on their success; a successful
  Viewer Job cannot validate either calculation.
- The Viewer Job writes `<target-filename>.<random>.tmp` in the target folder and
  then moves it to the target with replace-existing semantics. Handled I/O and
  execution failures collapse to the same standard cancellation status as user
  cancellation, with no custom error status; all can leave the temporary file.
  Cancellation observed after the final move can also coexist with a created
  target. File presence alone is therefore not a success oracle: the runner
  needs identity-matched terminal semantics plus a new, non-empty, stable target.
  Consequently, even two exact absence checks cannot eliminate a race in which
  another writer creates the final target before HRC's move. The implementation
  gate needs a validated exclusively owned staging namespace plus fail-if-exists
  promotion to the canonical name. A high-entropy filename alone is
  insufficient. That namespace, its exclusive HRC-control lease, and externally
  readable terminal states remain TO CONFIRM, and temporary files must not be
  deleted automatically.

### Installed Finish inspection

These findings originated in static inspection. Run 18 later confirmed the
guarded native `BM_CLICK` path and successful new-tab post-state live.
Post-Finish error and cancellation paths are statically identified but have not
been live-confirmed.

- Hand Setup is a JFace WizardDialog hosted in an owned native `#32770` window.
  Its standard button has raw native caption `&Finish`. Page transitions do not
  explicitly assign focus, and a focused push button can become SWT's current
  default. Generic Enter is therefore not a durable Finish route.
- The button's raw native caption is `&Finish`; its normalised visible caption is
  `Finish`. Run 18's live provider exposed the accessible name as `Finish`, not
  `&Finish`; the target runner must require that exact name independently of the
  raw native caption.
- The discovery provider could expose the button as `Finish` but could not act
  on its indexed element. Fresh observation proved that HRC remained unchanged.
  Run 18 therefore chose the native route before actuation. One guarded
  `SendMessageTimeout` `BM_CLICK` against the uniquely enumerated button returned
  successfully and created one new hand tab. A direct unique-control action does
  not require native keyboard focus. Foreground and focus assertions are
  required only if a keyboard fallback is attempted.
- Finish closes the wizard before the background Hand Setup Job reaches a
  terminal result. Wizard closure, or even a Job OK result on its own, is not
  sufficient success. Positive success requires exactly one new hand-editor tab.
  An explicit tree-creation failure or cancellation is a terminal non-success.
  If none of those states appears before timeout, stop without retrying.
- The inspected error text for one tree-creation failure path is
  `An error occured while creating the game tree. This is typically caused by
  all players having forced actions, please check the hand setup.` Its live
  presentation remains TO CONFIRM.
- Static inspection used calculator plug-in SHA-256
  `F9FA329B80C265356E6D29C1E98DAF1EFFB06D1536C92BDACEFD0C1EEE90562A`,
  JFace SHA-256
  `E0E28699ECD8783597B3E481D18CFA5A1889FFB9F3007260BD2DD78311779BC4`,
  and SWT SHA-256
  `5315B5A2E1260CEB125B421CBCD467935ED9666374E57C38AECBAA04E01DFC85`.

### Installed hand-tab closure inspection

These are static findings and a guarded live-probe design. Run 19 did not send
the close command because the required native focus and active-editor identity
were not established.

- SWT exposes the hand-tab strip as an MSAA tab folder with tab-item children.
  The close glyph is not a separate accessible child, and a tab item's default
  action is `Switch`, not Close. UIA or MSAA invocation of the item must not be
  treated as a close operation.
- `Ctrl+W` is an HRC command prefix, including the `Ctrl+W`, then `H` route that
  opened retained-state Hand Setup in run 18. It is not a tab-close shortcut.
- Installed Eclipse bindings map `Ctrl+F4` to `org.eclipse.ui.file.close`, which
  closes the active editor with save handling. A future Cancel-only probe must
  first focus the exact tab-folder HWND natively, verify that `*From Hand 7` is
  the single selected tab, and use a Left/Right selection round-trip through
  `*Hand 7` to prove Eclipse activates the intended editor. Any focus,
  selection, order, or identity mismatch must stop before `Ctrl+F4`.
- Static JFace inspection expects owned prompt title `Save Resource`, message
  `Save 'From Hand 7'?`, raw native buttons `&Save`, `Do&n't Save`, and
  `Cancel`, and UIA names `Save`, `Don't Save`, and `Cancel`. Save is the default
  button, so Enter is dangerous.
- For that live probe only, the safe non-destructive outcome is one freshly
  guarded native `BM_CLICK` on the unique visible enabled `Cancel` button.
  Escape, Enter, `Alt+F4`, and `Don't Save` must not be used. Success requires
  the prompt to disappear while both dirty tabs, the selected active tab,
  Progress state, HRC bounds, and files remain unchanged. An unknown outcome
  stops without retry.

## Data-preserving NatTable bootstrap on 11 August 2026

This live check did not select a table size, change a row or stack, advance the
wizard, finish a tree, submit a calculation, or write a file.

- HRC began on `Home`. The named `New: Monte Carlo Hand` link opened Basic Hand
  Data. The setup displayed the previously used five-player rows and the same
  `10/20/30/40/50 bb` values, showing that opening a new setup can retain prior
  inputs.
- Starting from the newly opened page with Next shown as the default button,
  successive Tab presses visibly reached Cancel, the information text,
  clipboard, eraser, yellow right arrow, and yellow left arrow. The seventh
  press had no visible outline.
- At that seventh stop, `Ctrl+A` placed the visible black selection border on
  the cell displaying `Auto`. `Ctrl+Home` left that cell selected. This directly
  established the non-coordinate Tab and selection bootstrap for this setup.
- Space opened the player-count list. The accessibility tree exposed list ID
  `11606852` and named selectable items `Auto`, `HU`, and `3-max` through
  `10-max`. The provider still incorrectly reported background Range edit
  `69008` as focused.
- Escape closed the list. All five rows, five chip values, and five BB values
  remained unchanged. No item was activated in this run, so table-size
  selection effects were still TO CONFIRM at the end of run 12. Run 13 later
  confirmed the HU row-removal and retained-stack effect described below.
- Two semantic attempts to activate the named Cancel button did not reach the
  cached target. `Alt+C` and Escape did not dismiss Basic Hand Data. `Alt+F4`
  closed the unsaved Hand Setup and returned to `Home` without a prompt.

At the end of run 12, this confirmed one live, non-coordinate route to focus
the NatTable and open the player-count list. It had not yet confirmed the route
through a standalone runner, selection of a different table size, row creation
or removal, retained-value handling after a selection, or an end-to-end route
from this bootstrap to stack entry and read-back. Run 13 later confirmed the HU
selection and its immediate row-removal and retained-stack effects.

## HU table-size selection effect on 11 August 2026

This follow-up changed only the disposable, unsaved Hand Setup. It did not
advance the wizard, finish a tree, submit a calculation, or write a file.

- HRC again opened Basic Hand Data with `Auto` and the retained five-player
  rows: `HJ 1000 / 10.0 bb`, `CO 2000 / 20.0 bb`, `BU 3000 / 30.0 bb`,
  `SB 4000 / 40.0 bb`, and `BB 5000 / 50.0 bb`.
- From that newly opened page, seven Tab presses followed by `Ctrl+A`,
  `Ctrl+Home`, and Space again selected the player-count cell and opened the
  list without a pointer. The list ID was `6359478`, different from the prior
  observed IDs.
- With `Auto` current, one Down press visibly selected `HU`. The editor showed
  `HU` while the list remained open, but all five rows were still present.
- Enter committed the selection and closed the list. HRC removed `HJ`, `CO`,
  and `BU`; the remaining rows were exactly `SB 4000 / 40.0 bb` and
  `BB 5000 / 50.0 bb`.
- `Alt+F4` closed the unsaved Hand Setup and returned to `Home` without a
  prompt.

This confirms one supervised, non-coordinate table-size selection and its
immediate row-removal effect. In this transition HRC retained the prior blind
stacks rather than resetting them. Automation must therefore overwrite and
read back every active seat after selecting a table size. At the end of this
run, multiway row creation, different-valid-value entry, cell read-back,
rejected-input handling, and delivery through a standalone runner remained
TO CONFIRM. Run 14 later confirmed the supervised HU edit and visual-validation
behaviour described below.

## HU stack entry and rejected-input handling on 11 August 2026

This follow-up changed only a disposable, unsaved Hand Setup. It did not
advance the wizard, finish a tree, submit a calculation, or write a file.

- Opening another new setup showed `Auto`, but HRC retained only the two rows
  left by the earlier HU selection: `SB 4000 / 40.0 bb` and
  `BB 5000 / 50.0 bb`. The selector label and retained row state therefore
  cannot be assumed to describe the same reset state.
- Seven Tab presses, `Ctrl+A`, `Ctrl+Home`, Space, one Down press, and Enter
  again selected and committed `HU` without a pointer.
- From the selected HU cell, Down selected the SB row label and Right selected
  SB Chips. `F2` opened an unnamed transient editor with `4000` selected.
- Typing the fabricated test value `4100` and pressing Enter committed it,
  visibly recalculated the row as `41.0 bb`, and opened BB Chips with `5000`
  selected. Typing `5100` and pressing Enter committed `51.0 bb` and opened
  the blank next-row Chips editor.
- Escape cancelled the blank editor. No third row was added; the visible rows
  remained exactly `SB 4100 / 41.0 bb` and `BB 5100 / 51.0 bb`.
- Returning to SB Chips and entering the deliberately invalid text `abc`
  displayed it in red. Enter did not commit or advance, no modal appeared, and
  the derived BB value stayed `41.0`. Escape cancelled the editor and restored
  the visible `4100 / 41.0 bb` value.
- Transient edit IDs changed during the sequence, including `1185980`,
  `1251516`, and `1382588`. The provider continued to report background Range
  edit `69008` as focused even while the stack editor was visibly active.
- `Alt+F4` closed Hand Setup and returned to `Home` without a prompt.

This confirms the combined supervised, non-coordinate route from a newly
opened page through HU selection and two different-valid-value stack commits.
It also confirms visual derived-value read-back, the final-row advance into a
blank editor, safe cancellation without adding a row, and one non-numeric
rejection-and-recovery path. It does not provide machine-readable stack-cell
read-back or prove the foreground and focus assertions needed by a standalone
runner. Multiway choice effects and standalone delivery remain TO CONFIRM.

## Keyboard script-picker route on 11 August 2026

This disposable follow-up did not select or load a script, finish a tree,
submit a calculation, or write a file.

- The named Home link opened Basic Hand Data with `Auto` and the retained
  `SB 4100 / 41.0 bb` and `BB 5100 / 51.0 bb` rows from the previous disposable
  check. `Alt+N` advanced to Betting Setup.
- `Ctrl+PageDown` did not change the selected `Preflop` page.
- With the visible focus rectangle on `Back`, four Tab presses moved through
  `Finish`, `Cancel`, the information text, and then the `Preflop` tab. Two
  Right presses selected `Postflop` and then `Scripting`.
- With the visible focus rectangle on `Scripting`, one Tab press reached the
  `Script:` edit and the next reached the first, unnamed folder button. Space
  opened the standard Windows `Open` dialog without a pointer.
- The dialog opened at
  `C:\Users\euanh\.codex\worktrees\35c1\hrc-beta-automation\scripts\hrc` and
  displayed both candidate filenames. It exposed named `File name`, `Open`, and
  `Cancel` controls. The visible insertion caret was in `File name`, while the
  provider reported the `Search hrc` edit as focused.
- Escape cancelled the dialog and returned to Scripting with the script field
  still empty and the visible focus rectangle on the folder button. Space
  immediately reopened the same folder. Escape cancelled it again. No candidate
  was selected.
- `Alt+F4` then closed Hand Setup and returned to `Home` without a prompt.

This confirms one supervised, non-coordinate route to the script picker and one
same-setup repeat after cancellation from the observed visible focus rectangle
on `Back`. It does not establish native foreground or focus, cross-setup or
cross-session durability, semantic identity for the unnamed folder button, the
active Open-dialog field, or safe selection and validation of the intended
script through a standalone runner.

## Current HU candidate and Nash-grid probe on 11 August 2026

This disposable follow-up loaded and previewed the current worktree HU
candidate, finished a two-node hand for discovery, and configured both required
Nash states without submitting either calculation. It did not rename or save
the hand, export strategies, create an output file, or close the hand tab.

- A new Basic Hand Data setup used the confirmed keyboard route to select `HU`
  and commit equal `200`-chip, `2.0 bb` stacks for SB and BB. Escape cancelled
  the blank third-row editor. `Alt+N` opened Betting Setup.
- From the visible focus rectangle on `Back`, the confirmed Tab and Right route
  selected Scripting. Space opened the standard `Open` dialog at the exact
  worktree `scripts/hrc` folder. The provider again reported the Search edit
  while the visible insertion caret was in `File name`.
- Before entry, the exact worktree file
  `tree-building-hu-candidate.js` had SHA-256
  `e127ed9285d4f77253ad3c9ad3ac45afdb105f7d930ed3c45208d604fce845ec`.
  Typing that exact filename and pressing Enter loaded it. HRC displayed the
  expected basename without `[Errors]`, reported Total Nodes `2`, and enabled
  Finish.
- Expanded Preview showed `R 2.00 SB PRE` with exactly one child,
  `C 1.00 BB PRE`. No SB completion branch was present. This directly confirms
  the current candidate's below-cutoff behaviour for equal `2 bb` stacks. It
  does not validate the inclusive `5 bb` boundary, the first supported stack
  above it, other stack combinations, or later streets.
- Valid Tab and Space input did not activate Finish and appeared to reach the
  background window. A one-use current-frame screenshot-located Finish click
  closed Hand Setup and created unsaved `*Hand 7`. This was discovery only and
  is not a safe standalone operation.
- `Alt+R` opened native `Nash Calculation` with OK visibly focused. Tab moved
  the visible focus rectangle to Cancel; Space closed the dialog without a
  submission.
- After reopening, `Shift+Tab` from OK reached the NatTable. `Ctrl+A` selected
  the grid, `Ctrl+Home` collapsed selection to its origin, and Right selected
  the value column. Down navigated the rows. `F2` exposed the exact choices for
  CFR Algorithm, Scope, and Run Sampling; Escape closed each editor unchanged.
- On CI Target, `F2`, `Ctrl+A`, typing `10.0`, and Enter committed the edit and
  moved to Reset Regret. `Alt+F4` closed the dialog without submission.
  Reopening showed CI `1.0` and both reset boxes visually clear, so the observed
  CI `10.0` edit was not retained.
- A separate correctly bootstrapped route navigated to Reset Strategies. Space
  produced the required visible second-run state: CI `1.0`, no Reset Regret
  checkmark, and a Reset Strategies checkmark. `Alt+F4` closed the dialog without
  submission. Run 16 did not press OK, so it did not live-test post-OK retained
  reset clearing.
- In an earlier attempt, omitting `Ctrl+Home` after `Ctrl+A` left both reset
  cells under ambiguous selection styling. This did not establish either
  checkbox value and did not show both reset modes active. Navigation back
  across reset rows also produced ambiguous cell highlighting. The route must
  collapse selection at origin and read back every setting before any future OK
  action.
- Escape did not close Nash from the grid. `Alt+F4` did. No Nash job appeared;
  Progress remained `No operations to display at this time.`

The supervised route now proves candidate load and post-load state, current
equal-`2 bb` Preview, observed Nash grid entry, CI editing, a Reset Strategies
checkmark, and non-submitting close routes. Run 17 later added machine-readable
per-cell read-back and exact supervised reset verification. Standalone
feasibility still requires verified native foreground and focus, a durable
Finish operation, guarded OK submission, and observable acceptance, rejection,
queueing, running, cancellation, completion, and failure states.

## Machine-readable Nash read-back on 12 August 2026

This follow-up used the existing unsaved `*Hand 7`. It did not submit a Nash
calculation, rename or save the hand, export strategies, create an output file,
or close the hand tab.

- `Alt+R` opened Nash Calculation. `Shift+Tab`, `Ctrl+A`, mandatory
  `Ctrl+Home`, and Right entered the value column from initial OK focus.
- Row movement and `Ctrl+C` returned these exact raw values in order:
  `HRC 4.0 (Default)`, `Full Tree`, `Until CI value is reached`, `1.0`,
  `false`, and `false`.
- On Reset Strategies, Space displayed a checkmark and `Ctrl+C` returned
  `true`. While that checkmark remained visible, Up and `Ctrl+C` returned
  Reset Regret `false`; Down and `Ctrl+C` returned Reset Strategies `true`.
  This directly verified the required reset pair `false,true`. Space then
  cleared Reset Strategies, and `Ctrl+C` returned `false`.
- `Alt+F4` closed the dialog without submission after the reset pair had been
  restored to `false,false`. An earlier close-and-reopen also showed CI `1.0`
  and both reset boxes clear, but the reset change had been restored before
  closing; this does not test cancellation of a changed reset value.
- One whole-grid `Ctrl+A`, `Ctrl+C` attempt copied only `CFR Algorithm`. The
  supervised runner path must navigate to each value cell and validate the
  exact raw value. The currently supported route must not rely on whole-grid
  copy. Run 16 edited CI to `10.0` separately but did not copy it after editing.
- Progress stayed `No operations to display at this time.` The `*Hand 7` tab
  remained open. HRC kept its existing window bounds.

This run confirms machine-readable per-cell Nash read-back and exact supervised
reset-pair verification. It does not confirm native foreground or focus,
standalone delivery, OK submission, post-OK retained reset clearing,
or accepted, rejected, queued, running, cancelled, completed, and failed
post-states.

## Native Finish action on 12 August 2026

This disposable probe created one additional unsaved, in-memory two-node HU
hand. It did not rename a hand, submit Nash, save or export a file, or close a
hand tab.

- From active `*Hand 7`, `Ctrl+W` followed by `H` opened Hand Setup. Basic Hand
  Data retained `Auto`, the prior SB and BB rows at `200` chips / `2.0 bb`, and
  the prior HU candidate. The confirmed NatTable route explicitly selected HU;
  `Alt+N` reached Betting Setup.
- Scripting displayed `tree-building-hu-candidate.js` without `[Errors]`. Tree
  Statistics and Abstractions reported Total Nodes `2`, Total Tree Size
  `0.00GB`, and an enabled Finish button. The exact worktree file and hash were
  not reselected in this probe; the retained basename and estimate are observed
  state, not independent provenance proof.
- Read-only native enumeration found exactly one visible HRC-owned `#32770`
  titled `Hand Setup`, owned by the main HRC window, and exactly one visible
  enabled child of class `Button` with raw caption `&Finish`. The live provider
  exposed its accessible name as `Finish`.
- The discovery provider rejected the current indexed Finish element as
  unavailable. Fresh observation showed Hand Setup unchanged, the existing
  `*Hand 7` tab only, no error, and idle Progress. That provider route was not
  used again.
- One guarded `SendMessageTimeout` `BM_CLICK` was sent to the freshly
  re-enumerated unique native button. Dispatch returned successfully. No second
  actuation or retry was attempted.
- Hand Setup closed and, relative to the pre-action hand-editor tab set, the
  accessible tab-item set gained exactly one expected hand-editor entry,
  `*From Hand 7`, alongside `*Hand 7`. No tree-creation error appeared and
  Progress remained `No operations to display at this time.` This is the required
  positive Finish post-state; wizard closure alone was not used as success.
- The retained setup state and `*From Hand 7` result show that `Ctrl+W`, then
  `H` from an active hand can inherit state. In run 18 it retained the rows and
  script. It is not a validated clean next-simulation transition. The Home route
  or explicit full reset still requires validation after the completed hand
  closes.

This run confirms the guarded native Finish action and successful accessible
new-tab detection without coordinates or keyboard focus. The explicit
tree-creation error, cancellation, and unknown-timeout paths remain TO CONFIRM
live. The current HRC state is active unsaved `*From Hand 7` with unsaved
`*Hand 7` still open; neither has matching outputs and neither may be discarded
through the Viewer-only close workflow.

## Non-writing lifecycle mapping on 12 August 2026

This follow-up used active unsaved `*From Hand 7`. It did not rename the hand,
submit Nash, save or export a file, open a close prompt, or close either dirty
tab.

- `Ctrl+H` followed by `R` opened Rename Hand. The dialog exposed exact title
  `Rename Hand`, label and edit name `Rename to:`, and named `OK` and `Cancel`
  buttons. `From Hand 7` was visibly selected in the edit.
- A provider `set_value` action against that edit returned an unknown outcome.
  Fresh observation proved that the displayed name remained `From Hand 7`; the
  action was not retried. The provider reported a background edit as focused
  despite the visible selection. Escape cancelled, leaving both tab names
  unchanged.
- `Ctrl+Alt+S` opened the standard Save As dialog at exact address
  `\\VAULT\sims\Preflop\HU`. It proposed `From Hand 7.hrcv` and visibly
  selected `*.hrcv Viewer Save`. The standard filename edit, type combo, Save,
  Cancel, address, and existing Viewer-file items were accessible.
- The provider reported `Search HU` as focused instead of the visibly selected
  File name field, and the accessibility tree did not expose the selected type
  text. Escape cancelled without writing. Read-only metadata checks afterward
  confirmed that exact `From Hand 7.hrcv` and `From Hand 7.zip` targets were
  absent.
- `Ctrl+H` followed by `E` opened Export Strategies. It visibly retained
  Complete Export, Depth `2`, PrettyPrint JSON clear, threshold `0.1`, and the
  two-node `R` / `C` range tree. The provider exposed the scope combo, spinner,
  tree, OK, and Cancel but again reported a background edit as focused.
- A provider semantic action against the scope combo returned an unknown
  outcome. Fresh observation proved that the displayed scope and every visible
  value were unchanged; the action was not retried. Escape cancelled without
  opening an archive dialog or writing a file.
- `Alt+F` exposed File menu items New Calculation, Load Hand, View Hand, Import,
  Save, Save As, and Exit. It contained no hand-close command. A semantic click
  on the already-active `*From Hand 7` tab succeeded and moved the pointer away
  from an incidental range tooltip, but the provider still reported the
  background Strategy Table edit as focused. No close command was sent.

This run confirms non-coordinate entry and non-writing cancellation for the
three later dialogs, plus the exact current Viewer destination/type/name and
Export visible state. It does not establish machine-readable Rename text input,
Viewer type read-back, Export control, native focus, tab closure, or standalone
delivery. Both dirty tabs remain open and Progress remains idle.

## Idle control-map discovery on 11 August 2026

This discovery created one disposable, unsaved two-node hand. It did not submit
a Nash calculation or write an output file. The resulting `*Hand 6` tab was
still open at the end of that run because its required Viewer and strategy
outputs did not exist. Its later disposition is TO CONFIRM.

- The named `New: Monte Carlo Hand` Home link exposed ID `3342566`. The first
  semantic click returned an unknown outcome. A refreshed retry opened Hand
  Setup.
- Hand Setup exposed `Next` as a named button with ID `268476`. Semantic click,
  Tab, and Enter did not reach the owned dialog. Euan clicked `Next` manually.
- Betting Setup exposed a `Scripting` tab and a `Script:` edit with ID `334118`.
  The tab items shared one repeated element index in the discovery provider.
- The script-picker folder button had no accessible name. Its ID was `334110`,
  which differs from the earlier session value `1903002`. Semantic invocation
  failed. A one-use screenshot-located click opened the standard `Open` dialog.
- The `Open` dialog exposed both candidate filenames, `File name`, `Open`, and
  `Cancel`. `Alt+N` visibly focused `File name`. The reported focused element
  incorrectly remained the background search box. Typing the exact HU filename
  and pressing Enter loaded the candidate.
- HRC loaded the file from `C:\Projects\hrc-beta-automation\scripts\hrc`.
  Its SHA-256 hash matched the candidate in this worktree.
- The loaded candidate changed Total Nodes from `16` to `2`. Expanded Preview
  showed `R 2.00 SB PRE` with one child, `C 1.00 BB PRE`. No SB completion
  branch was present.
- Enter invoked the default `Finish` button and created unsaved `*Hand 6`.
  `Rename Hand` exposed a labelled edit and named buttons. Escape cancelled it.
- `Alt+R` opened Nash Calculation. Only `OK` and `Cancel` were exposed. The
  algorithm, scope, sampling, CI, and reset controls remained absent from the
  accessibility tree. The current CI value was `1.0`. Escape did not close the
  dialog. A screenshot-located `Cancel` click closed it without submission.
- `Ctrl+Alt+S` opened the standard `Save As` dialog at the HU folder. This run
  retained `*.hrcv Viewer Save`; the earlier run defaulted to Complete Save.
  The file type must therefore be verified on every save. Escape cancelled.
- The chord `Ctrl+H`, then `E` opened Export Strategies. The scope combo, Depth
  spinner, range tree, and buttons were exposed. `PrettyPrint JSON` and
  `Node Filter Threshold %` were not exposed as reliably named controls.
  Escape cancelled without creating an archive.
- `Ctrl+F4` did not close the hand tab or produce `Save Resource`. A durable
  hand-tab close target remains unproven.

In a same-day follow-up, Euan pressed `Alt+N` while Basic Hand Data was open.
Hand Setup advanced to Betting Setup. A read-only capture immediately afterward
confirmed Betting Setup, with `Back` enabled, `Next` disabled, and `Finish`
enabled. Codex issued no input during this confirmation. This establishes the
keyboard route and supersedes the earlier missing-route blocker; the earlier
run still required Euan's manual pointer click because `Alt+N` had not yet been
tested. Delivery and post-state detection through the standalone runner remain
TO CONFIRM.

## Stack-grid keyboard discovery on 11 August 2026

This discovery reused the existing unsaved five-player setup. It did not add or
remove a player row, change a stack, finish a tree, submit a calculation, or
write a file. HRC remained in its restored, non-maximised window. The discovery
ended on Basic Hand Data with the same five visible stack values.

- `Alt+B` returned from Betting Setup to Basic Hand Data. `Alt+N` advanced back
  to Betting Setup. The provider continued to report background Range edit
  `69008` as focused instead of a Hand Setup control.
- A semantic click on the exposed `Back` button did not change the page. A
  secondary accessibility action also failed. These failures reinforce that
  the owned Hand Setup dialog cannot yet be driven reliably through the current
  semantic target.
- On Basic Hand Data, the accessibility tree exposed unnamed panes and toolbar
  buttons but no stack-grid cells. After returning from Betting Setup, the
  visible Tab order began with Next, Cancel, the information text, then the four
  unnamed toolbar buttons in icon order: clipboard, eraser, yellow right arrow,
  and yellow left arrow. Later Tab stops, reverse-Tab, `F6`, `Ctrl+Home`, and
  `F2` did not establish a visible, repeatable route into the grid.
- For discovery only, a screenshot-located click opened the `Auto` player-count
  selector. While open, the accessibility tree exposed a list with session-only
  ID `95687566` and named selectable items `Auto`, `HU`, `3-max`, `4-max`,
  `5-max`, `6-max`, `7-max`, `8-max`, `9-max`, and `10-max`. No choice was
  activated. Escape closed the list. The post-close display remained `Auto`,
  and the existing five rows and all five stack values were visibly preserved.
  At the end of that earlier run, a durable non-coordinate route to focus the
  selector remained TO CONFIRM, as did the effect of selecting a table size.
  The later data-preserving NatTable run established one focus-and-open route
  but did not activate a choice.
- After Escape closed that list, Space reopened it without a pointer action.
  `Alt+Down` and `F4` did not reopen it. The reopened list had ID `18224586`,
  confirming that the numeric list ID changed within the same session. Escape
  closed it again without activating a choice.
- `Alt+N` advanced to Betting Setup and `Alt+B` returned to Basic Hand Data,
  preserving the rows and stacks. Space then did not open the selector. This
  page cycle therefore did not establish a repeatable selector-focus route.
- For discovery only, a screenshot-located click focused the existing HJ Chips
  cell. Once a grid cell had focus, Up and Down moved between player rows, and
  Left and Right moved between columns.
- `Ctrl+Home` selected the cell displaying `Auto`. Down selected the first player row,
  and Right selected its first Chips cell. `Ctrl+End` selected the bottom-right
  grid cell.
- `F2` entered edit mode and selected the current value. A single pointer click
  on a populated value did the same. Escape cancelled editing without changing
  the value.
- The no-change test typed `1000` into the already-`1000` HJ Chips cell and
  pressed Enter. HRC accepted the same value, moved to the CO Chips cell, and
  selected its visible `2000` value for editing. Escape left CO unchanged. The
  visible values remained HJ `1000`, CO `2000`, BU `3000`, SB `4000`, and BB
  `5000` chips.

At the end of this earlier check, the evidence confirmed supervised keyboard
movement, edit mode, same-value commit, advance, and cell-editor cancellation
only after an existing grid cell had focus. It did not then prove a durable
non-coordinate entry target, blank-row handling, different-valid-value entry,
value read-back, rejected-input validation, or safe operation through a
standalone runner. Run 14 later confirmed the combined supervised HU path,
visual read-back, blank-row cancellation, and one invalid-input recovery.

## Five-player setup discovery on 11 August 2026

This discovery stopped at a Script Error. It did not finish a tree, submit a
Nash calculation, or write an output file.

- Basic Hand Data showed `HJ 10.0 bb / 1000 chips`, `CO 20.0 bb / 2000
  chips`, `BU 30.0 bb / 3000 chips`, `SB 40.0 bb / 4000 chips`, and `BB 50.0
  bb / 5000 chips` in that order.
- The small blind was `50`, the big blind was `100`, and Antes was `0`.
  Straddle was `Off`, SkipSB was clear, and Moving BU was selected.
- Euan added each extra player by selecting an empty cell in the BB column.
  HRC populated the player row and position. The yellow arrow buttons were not
  used for this operation.
- Euan edited each BB cell separately. After manual cell activation, Tab moved
  one cell right and Enter moved one row down.
- Selecting the HJ `10.0` cell exposed a transient unnamed edit with session ID
  `6690946`. The discovery provider incorrectly reported background edit
  `69008` as focused.
- `Alt+N` advanced the five-player setup to Betting Setup.
- Hand Mode displayed `Monte Carlo [Advanced, max. 4 players]`. Euan explained
  that this limit concerns some postflop calculations. The direct observation
  proves only that HRC accepted five preflop rows and advanced.
- Before the project script loaded, Betting Setup showed Total Nodes `448527`,
  Total Tree Size `3.1GB`, and HRC available `165.8GB / 166.3GB`. These values
  belong to the default setup, not the project candidate.
- Scripting exposed the `Script:` edit with session ID `858296`. The unnamed
  picker used session ID `464974`, a third observed value for that control.
- The standard `Open` dialog opened at
  `C:\Projects\hrc-beta-automation\scripts\hrc`. It exposed
  `tree-building-3m-6m-candidate.js` as item ID `0`, `File name:` as `1148`,
  and `Open` as `1`. Euan used `Alt+N`, entered the exact filename, and pressed
  Enter.
- The loaded file and the then-current pre-correction worktree candidate had
  the same SHA-256 hash,
  `128110cc73abd5bfd45167d426935e8d43923ae8648deffbc0251f4d03178782`.
  HRC showed `Error: Effective stack does not match a configured workbook
  column: 100000`. The Script Error OK button had session ID `859030`.
- After the error was dismissed, Scripting showed `[Errors]`, Total Nodes `0`,
  Total Tree Size `0.00GB`, and disabled Finish.

Offline analysis found that `100000` equals the supported `10 bb` stack for
this `50/100` setup in HRC amount units. The candidates used
`sizingBigBlinds()` as a raw unit conversion even though the API defines it as
a decision-point action-sizing helper. The project candidates now use the
nominal big blind for state, history, and threshold comparisons. Regression
tests cover the observed five-player stack vector and a deliberately divergent
action-sizing helper.
At the time of the failing run, the candidate under
`C:\Projects\hrc-beta-automation` had SHA-256
`128110cc73abd5bfd45167d426935e8d43923ae8648deffbc0251f4d03178782`.
The corrected worktree candidate has SHA-256
`fa2612bd1d3b01a8aa6419fc3697450cf708adff73fc6d085e2223ff605d7c63`.
Euan reported loading the corrected worktree candidate for the retest. A
contemporaneous read-only capture showed `tree-building-3m-6m-candidate.js`
without `[Errors]`, Total Nodes `1815589`, Total Tree Size `12.3GB`, and enabled
Finish. HRC available was `165.7GB / 166.3GB`. For the reported corrected load,
this confirms that the candidate passed script evaluation and tree estimation
for the observed setup.
Finish was not selected, and no calculation or file write occurred.

The following path-scoped Preview evidence was then observed:

- The root showed `HJ R 2.00/R 10.0`, `CO R 2.00/R 20.0`,
  `BU R 2.10/R 30.0`, and `SB C 0.50/R 3.00/R 40.0`. Every row was `PRE`.
- After `HJ R 2.00`, CO showed `C 2.00/3B 5.00/3B 20.0`; BU showed
  `C 2.00/3B 5.50/3B 30.0`; SB showed `C 1.50/3B 7.50/3B 40.0`; and BB showed
  `C 1.00/3B 10.0` after the intervening folds.
- After `HJ R 2.00, CO C 2.00`, the displayed one-caller squeeze sizes were
  `BU 3B 6.00`, `SB 3B 8.00`, and `BB 3B 6.00`. After BU also called, SB showed
  `3B 8.50/3B 40.0`, BB showed `3B 8.00/3B 30.0`, and neither blind showed a
  third call.
- The other ordinary roots showed the expected calls and re-raises:
  `CO R 2.00` produced BU `3B 5.50`, SB `3B 7.50`, and BB `3B 5.50`;
  `BU R 2.10` produced SB `3B 7.50` and BB `3B 7.00`; and `SB R 3.00`
  produced BB `C 2.00/3B 8.00/3B 40.0`. Their displayed all-in alternatives
  matched the current effective stacks.
- After `SB C 0.50`, BB showed `X 0.00/R 3.00/R 40.0`. This is an
  above-cutoff completion example at the configured `40 bb` SB stack.
- After the non-ordinary `HJ R 10.0`, later seats showed calls and only their
  legal all-in re-raise. BB showed only `C 9.00` when the all-in opener was the
  sole remaining opponent.
- The ordinary path `HJ R 2.00, CO 3B 5.00` showed SB
  `4B 11.3/4B 40.0`. After SB `4B 11.3`, BB showed only `5B 40.0`, HJ could
  call `8.00`, and CO could call `6.25` or `5B 20.0`. After HJ called, CO's
  call option disappeared and only `5B 20.0` remained.
- The observed low-SPR flop rows were `X 0.00/B 1.00/B 1.38/B 2.20/B 3.69/
  B 5.50/B 8.00` heads-up and `X 0.00/B 1.00/B 1.88/B 3.00/B 5.03/B 8.00`
  three-way.

A read-only comparison found that every listed path matched the current
candidate's workbook-derived manifest and legal-normalisation rules. This is
not exhaustive validation of the `1815589`-node tree. It does not validate
unexpanded branches, the `5 bb` completion boundary, the `>=40 bb` squeeze
boundary, other stacks or table sizes, later streets, Finish, Nash, or output.
The full loaded path and hash remained unexposed; provenance is Euan-reported.
Where HRC suppressed an illegal sizing, Preview confirms only the visible legal
tree, not the candidate's raw callback return.

The current accessibility capture exposed Preview tab ID `661798`, tree ID
`989272`, named column headers, and selectable action-only tree items. It did
not expose each item's amount, player, or street in the item name. The provider
again reported background edit `69008` as focused. Screenshot-located expansion
was sufficient for supervised discovery but is not a durable automation path.
The HRC-tested pre-conversion HU candidate had SHA-256
`8fc4d2d79aefee249db4ea3cbecb2516f19b7a2bfbfcf85f3f12a6e23e54db6a`.
The current HU candidate has SHA-256
`e127ed9285d4f77253ad3c9ad3ac45afdb105f7d930ed3c45208d604fce845ec`.
The exact worktree file and hash were verified before a supervised load. HRC
showed the expected basename without `[Errors]`, reported two nodes, and
expanded Preview showed `R 2.00 SB PRE` with only `C 1.00 BB PRE`. This directly
revalidates the current candidate at equal `2 bb`.

## Historical workflows and reserved runner smoke

- Historical selected workflow definition: Create and rename one true heads-up Monte
  Carlo tree, queue two
  Nash calculations, queue a Viewer save, export the strategies, close the
  completed tab, and continue to the next simulation.
- Historical selection reason: This was the smallest equal-stack setup in the generated
  simulation run order.
- Historical selected inputs: Two players with `1 bb` starting stacks from the generated
  simulation run order.
- Historical Viewer output filename: `HU-1.hrcv`. The demonstration also created an
  unintended `HU-1.hrcz` Complete Save. It was present and unmodified when
  last checked; its later state is TO CONFIRM.
- Strategy export filename: `HU-1.zip`. The demonstration created this file in
  the same HU folder as the Viewer output. The saved `HU-1.hrcz` tab was then
  closed.
- Viewer-only close filenames: `HU-1.5.hrcv` and `HU-1.5.zip`. Both files were
  present and non-empty after `Don't Save` closed the unsaved `*HU-1.5` tab.
  No matching `.hrcz` file was present in the HU folder.
- Shallow-tree follow-up: `HU-2` used the pre-conversion HU candidate from
  source commit `9b24166`. Hand Setup reported two nodes at equal `2 bb`
  stacks. A later preview of the same revision showed an SB raise to `2.00 BB`
  with only a BB call of `1.00 BB`. No SB completion branch was present. This
  confirms shallow-completion suppression for that revision at equal `2 bb`.
  A later disposable run loaded and previewed the exact current worktree HU
  candidate and showed the same two-node path. Its inclusive `5 bb` boundary
  and the first supported stack above it remain TO CONFIRM. Multiway evidence
  is limited to the representative five-player paths recorded above; other
  stacks, table sizes, boundaries, dynamic post-fold cases, later streets, and
  unexpanded branches remain TO CONFIRM.
- `HU-2` outputs: `HU-2.hrcv` was `9,015` bytes and `HU-2.zip` was `3,301`
  bytes. Both persisted after Viewer-only closure. No `HU-2.hrcz` file was
  present.
- Reserved runner smoke: equal `2 bb` HU with base name
  `AUTO-HU-2-20260812-01`. Its only permitted output directory is
  `\\VAULT\sims\Preflop\HU`, producing new
  `AUTO-HU-2-20260812-01.hrcv` and `AUTO-HU-2-20260812-01.zip` files. A
  read-only preflight on 12 August 2026 found both targets and the matching
  `.hrcz` absent. The runner must repeat the exact absence preflight immediately
  before any write and stop on any existing target or overwrite prompt. The
  smoke is reserved for post-gate project-owned runner validation; it must not be
  consumed during feasibility discovery. Owner authorisation covers the
  required live validation, but no further Nash submission may occur before the
  exact-status observer and all pre-submit safety gates are ready.
- Expected cost and duration: The small demonstration calculations transitioned
  quickly. Euan reports that production calculations can take a long time.
  Exact production durations remain TO CONFIRM.

## Required workflow sequence

After tree creation, submit steps 2 through 5 without waiting for the previous
operation to finish:

1. Create the tree for the next setup in the simulation run order. Select the
   required table size, overwrite every active seat's stack, and read back the
   exact position order and values. Do not rely on prior inputs being reset.
2. Rename the tree to `HU-1` for this setup. Use the equivalent ordered stack
   name for other table sizes, such as `5m-10-30-30-20-12.5`.
3. Open Nash Calculation with `Alt+R`. Use `HRC 4.0 (Default)`, Full Tree,
   Until CI value is reached, and CI Target `10.0`. Keep Reset Regret and Reset
   Strategies clear. Queue the operation with OK.
4. Open Nash Calculation again. Keep the same algorithm, scope, and sampling
   mode. Set CI Target to `1.0`. Select Reset Strategies and keep Reset Regret
   clear. Queue the operation with OK.
5. Queue a Viewer save under `\\VAULT\sims\Preflop\HU` using a new high-entropy
   filename inside a validated exclusively owned staging namespace. Use the
   corresponding table-size folder for other workflows, such as `5m`. Save As
   can retain its previous type or
   default to `*.hrcz Complete Save`. Select `*.hrcv Viewer Save`; read back the
   exact lowercase staging filename and `.hrcv` extension. After the exact Job
   succeeds and staging metadata is new/non-empty/stable, promote it with
   fail-if-exists semantics to `HU-1.hrcv`.
6. After the queued operations finish successfully, open `Hand` →
   `Export Strategies`. Use `Complete Export`, Depth `16`, clear
   `PrettyPrint JSON`, and set `Node Filter Threshold %` to `0.1`. In Save As,
   require both exact filters, select/read `*.zip Archived Json`, and save to a
   new high-entropy `.zip` filename in that exclusive namespace. After the
   exact Job succeeds and staging metadata is new/non-empty/stable,
   promote it with fail-if-exists semantics to `HU-1.zip`. In the inspected calculator plug-in
   `4.1.1`, Complete
   Export is unlimited-depth and does not consume the visible
   Depth setting; set and read back `16` to preserve the required workflow. The
   visible ZIP type is not an archive-format oracle because of the hidden
   retained-index defect documented above.
7. Start the next simulation.

Before step 1, reserve and verify that neither canonical target `HU-1.hrcv` nor
`HU-1.zip` exists. Use the corresponding unique base name for each later
simulation. Stop and choose a new unique name if either exists. Acquire the
exclusive HRC-control lease, atomically reserve and verify an exclusively owned
staging namespace, then generate absent high-entropy targets inside it. Recheck
each staging target immediately before HRC Save and each canonical target before
fail-if-exists promotion. If HRC shows an overwrite prompt, select Cancel and
stop; never replace an existing output.

Before the workflow, treat every pre-existing hand-editor tab as protected and
snapshot its stable identity, title, and dirty state. After step 6, verify the
canonical Viewer file and `.zip`-named strategy output. Both files must exist and
must not be empty; separately require a proved ZIP format state without
inspecting strategy contents. Immediately before closure, require the exact
hand-editor set to equal the protected identities plus exactly one expected
completed simulation. Close only that completed tab before step 7. HRC shows
`Save Resource` with `Save '<simulation-name>'?`. Confirm that both output base
filenames and the prompt name match the completed simulation. Only then select
`Don't Save`; require the post-close set to equal exactly the unchanged
protected identities, with no addition or replacement. Explicitly activate
`Home`, verify its page, and stop on any mismatch.

The required sequence is Euan's workflow definition. The second Nash dialog
opened while CI 10 Progress was visible. After submission, CI 1 Progress
appeared. The demonstration was too fast to establish queue order, calculation
completion, or failure states.

## Control map

| Lifecycle step | Visible label | Accessible name | Control type | Automation ID | Supported patterns | Keyboard path | Required action | Observable success | Observable failure | Safe to automate |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Start tree setup | `New: Monte Carlo Hand`; `Start New Calculation` | `New: Monte Carlo Hand`; shortcut command label observed in the menu | Link; command | Home link `3342566` in one session; stability TO CONFIRM | TO CONFIRM | From an active hand, `Ctrl+W`, then `H` opened Hand Setup in run 18 | Open a new Monte Carlo hand from `Home` and require a known clean or fully overwritten setup. Do not use the active-hand shortcut as a reset. | The Home link opened Basic Hand Data in isolated discovery. The shortcut also opened Hand Setup. | Run 18's shortcut path retained the active hand's two rows and script and ultimately produced `*From Hand 7`. Any inherited, unexpected, or unverifiable state must stop. | TO CONFIRM: the shortcut dispatch is live-confirmed but is not a clean next-simulation route; the Home post-close transition and machine-readable reset verification remain unproven. |
| Select table size or add multiway player rows | `Auto`; `HU`; `3-max` through `10-max`; empty BB-column cells | The open selector exposed every named table-size item; empty cells remained unnamed | Selector list and list items while open; table cell and transient edit for the earlier manual method | Open-list IDs `95687566`, `18224586`, `11606852`, and `6359478`; closed selector and empty-cell IDs TBD | The open items were selectable; Space opened the list after the non-coordinate NatTable bootstrap | From the newly opened page: press `Tab` seven times, `Ctrl+A`, `Ctrl+Home`, Space. For the observed HU case only, press `Down` once from `Auto`, then `Enter`; other choices and arrow counts remain TO CONFIRM. Escape cancels without selection. | Select the required table size. Then overwrite every active seat and verify the exact row count, position order, and values before advancing. | In two follow-ups, one `Down` press selected `HU` and Enter committed it. The first reduced five retained rows to SB and BB; the next setup reopened as `Auto` while retaining those two rows. Earlier manual empty-cell selection populated `HJ`, `CO`, `BU`, `SB`, `BB`. | A missing, extra, retained, reset, or misordered row or value must stop the workflow. | NO: HU selection and its row-removal effect are observed, but multiway choice effects, machine-readable row/value verification, and standalone delivery remain untested. Numeric list IDs changed. |
| Configure active stacks | `BB`; `Chips`; `Auto`; `HU` | Active edits were unnamed | Table cell; transient edit | `6690946` in the earlier run; `1185980`, `1251516`, and `1382588` during the HU run; stability disproven | TO CONFIRM | After the HU commit, Down selected SB, Right selected Chips, and `F2` opened the editor. Enter committed and moved down. After the last active row, Enter opened the blank next-row editor; Escape cancelled it. | Overwrite each active stack. After the last commit, cancel the blank-row editor. Verify the exact row count, positions, chip values, and derived BB values before advancing. | `4100` and `5100` committed as `41.0 bb` and `51.0 bb`. Enter advanced through both rows; Escape cancelled the blank third row without adding it. | Invalid `abc` stayed red; Enter did not commit or advance and the derived value stayed `41.0`. Escape restored `4100`. Any other mismatch must stop. The provider still reported background Range edit `69008` as focused. | NO: the combined supervised HU keyboard path, visual read-back, and one invalid-input recovery are observed, but machine-readable cell verification, multiway operation, foreground/focus assertions, and standalone delivery remain unproven. |
| Advance Hand Setup | `Next` | `&Next` | Button | `268476` in the earlier session; stability TO CONFIRM | TO CONFIRM | `Alt+N` | After validating all inputs and confirming Basic Hand Data is open, press `Alt+N`. | Euan confirmed that `Alt+N` advanced Hand Setup to Betting Setup. A read-only capture confirmed the resulting page. | Earlier semantic clicks, Tab, and Enter did not change the page. Any unchanged or unexpected page must stop the workflow. | TO CONFIRM through the target runner: the supervised keyboard route works, but reliable dialog focus, key delivery, and post-state detection are unproven. |
| Cancel Hand Setup | `Cancel` | `Cancel` | Button | `727278` in the NatTable run; stability TO CONFIRM | TO CONFIRM | `Alt+F4` while the owned Hand Setup is active | Abort a disposable or invalid setup without creating a hand. | `Alt+F4` closed the unsaved setup and returned to `Home` without a prompt. | Two cached named-target attempts could not be activated; `Alt+C` and Escape did not dismiss the dialog. Any unexpected prompt or window must stop. | TO CONFIRM through the target runner: one keyboard close worked, but exact owned-dialog and foreground assertions are required. |
| Select scripting | `Preflop`; `Postflop`; `Scripting` | Same as visible labels | Tab items | Current parent tab ID `2231886`; earlier parent `334064`; item IDs empty | SelectionItem and LegacyIAccessible in the earlier inspection | From the visible focus rectangle on `Back`: Tab four times to `Preflop`, then Right twice to `Scripting` | Select Scripting only after asserting the owned Betting Setup dialog and native focus. | `Scripting` became selected and exposed the `Script:` field and script controls. | `Ctrl+PageDown` did nothing. Any unexpected focus target or page must stop the workflow. | TO CONFIRM through the target runner: the supervised keyboard path worked once, but native starting-focus, transition, and post-state assertions remain unproven. |
| Open script picker | First folder icon beside `Script:` | Empty | Button | Current button `989356`; earlier values `1903002`, `334110`, and `464974`; current `Script:` edit `2037824`; earlier edits `334118` and `858296` | Invoke and LegacyIAccessible in the earlier inspection | From the visible focus rectangle on `Scripting`: Tab to `Script:`, Tab to the first folder button, Space | Open the script file picker only after verifying each transition. | Space opened the standard `Open` dialog. Escape restored the visible focus rectangle to the folder button; Space reopened the same folder once in the same setup. | The button remains unnamed, numeric IDs changed between sessions, and earlier semantic invocation failed. Any unexpected dialog or folder must stop the workflow. | TO CONFIRM through the target runner: the picker reopened once after cancellation in the same setup, but semantic identity, native foreground/focus, and cross-session durability remain unproven. |
| Select script file | Both candidate filenames; `File name:`; `Open` | Same as visible labels | List item; edit; button | Multiway item `0`; HU item `1`; filename edit `1148`; Open `1`; Cancel `2` in the observed dialogs | SelectionItem and Value for standard dialog controls; exact set TO CONFIRM | From the observed File name caret, type the exact filename and press Enter | Select the applicable candidate only after verifying the exact folder, filename, and expected candidate hash. | The exact current worktree HU candidate and hash were verified before entry. Opening it produced the expected basename, no `[Errors]`, Total Nodes `2`, and enabled Finish. The pre-conversion HU file and reported corrected multiway file also produced their separately recorded post-load states. | A wrong path, missing file, hash mismatch, Script Error, or unchanged estimate must stop the workflow. The provider reported Search while the visible caret was in File name. | TO CONFIRM through the target runner: the supervised end-to-end load worked, but reliable native focus, active-field and filename read-back, Open activation, and post-load detection remain unresolved. |
| Detect tree-script error | `Script Error`; error text; `OK`; `[Errors]` | The exact error and OK were exposed | Dialog; text; button | Error text `924558`; OK `859030` in this session | TO CONFIRM | TO CONFIRM | Record the exact error and stop before Finish. | Not applicable | The five-player candidate reported `Error: Effective stack does not match a configured workbook column: 100000`; Finish was disabled and Total Nodes was `0`. | TO CONFIRM: the visible failure is distinguishable, but durable automated detection is unproven. |
| Verify tree preview | `Preview`; `Action`; `Amt [BB]`; `Player`; `Street` | The current tree exposed selectable action-only items. Amount, player, and street were visible but absent from item names. | Tab; tree; tree items | HU tree `923428` earlier; current Preview tab `661798`; current tree `989272`; stability TO CONFIRM | Tree items were selectable; a durable expand operation remains TO CONFIRM. | Shift+Tab reached Preview from Scripting; Right expanded the selected HU root in the latest supervised run | Expand and inspect the documented candidate paths before Finish. | Both the pre-conversion and exact current HU candidates showed `R 2.00 SB PRE` with only `C 1.00 BB PRE` at equal `2 bb`. The listed five-player multiway paths matched the current candidate manifest for that setup. | Any unexpected branch, amount, player, or street must stop the workflow. | NO for automation: supervised keyboard and screenshot inspection worked, but provider focus was wrong and the accessible item names omitted three required columns. Evidence remains path-scoped. |
| Finish tree setup | `Finish`; raw native caption `&Finish` | `Finish` in the live provider | Button | Provider ID `3673952` in run 18; native HWND is session-only | Guarded native `SendMessageTimeout(BM_CLICK)` confirmed live; provider indexed action unavailable | Enter worked in run 6 but is not durable; valid Tab and Space did not activate Finish in run 16 | Snapshot the hand-editor tab set. Require exactly one visible HRC-owned `Hand Setup` `#32770`, its ownership by the main HRC window, and exactly one visible enabled child `Button` with raw caption `&Finish`. Re-enumerate immediately, send one guarded `BM_CLICK`, and never retry an unknown outcome. | Relative to the pre-action set, the one native action closed Hand Setup and added exactly one expected accessible hand-editor tab, `*From Hand 7`, alongside `*Hand 7`; no error appeared. | Wizard closure or idle alone is not success. Zero, multiple, wrong-type, replaced, renamed-only, or unknown tab deltas must stop without retry. An explicit tree-creation error or cancellation is terminal non-success. | TO CONFIRM overall: the guarded positive action and exact-one-hand-editor-tab detector are confirmed, but explicit error, cancellation, unknown-timeout handling, and target-runner reproduction remain unvalidated. No keyboard focus is required for the confirmed action. |
| Rename | `Hand`; `Rename Hand`; `Rename to:`; `OK`; `Cancel` | Same as visible labels | JFace input dialog; writable name edit plus hidden/read-only error edit; buttons | Run 19 name edit `2300212`, OK `3808978`, Cancel `6425084`; earlier IDs differed | Provider value action returned unknown; guarded `WM_SETTEXT`, read-back, and exact OK `BM_CLICK` are static candidates | `Ctrl+H`, then `R`; Escape cancelled | Separate dirty decoration from tab bases; require active base != requested and requested absent across bases. Assert unique owned dialog/roles; freshly enumerate before set/read and again before enabled-OK click; require same editor/selection/count/dirty state and exact old-full-title to expected-full-title replacement. | The production demonstration changed `*Hand 2` to `*HU-1`. Static inspection says accepted Rename updates the active editor synchronously without a job, file write, or dirty-state change. Run 19 proved exact dialog entry and Escape cancellation. | Empty input hides the blank validation result but disables OK. Too-long and listed invalid characters have exact messages. Same/duplicate names are accepted. Any owner/control/validation/read-back/enabled-state/title/selection/count/dirty-state mismatch or unknown outcome must stop without retry. | NO through the current provider. The native route and exact post-state are statically defined, but live enumeration, set/read-back, guarded OK, validation, and unknown-outcome handling remain TO CONFIRM through the target runner. |
| Submit CI 10 | `Run Nash Calculation`; `Nash Calculation`; `OK`; exact setting labels | Base accessibility exposed OK and Cancel; F2 exposed exact combo choices and a transient CI editor | Dialog; buttons; NatTable; combo list; edit | Earlier OK `662418` and Cancel `859034`; later OK/Cancel IDs changed; CI editor `3087194`; stability disproven | Default NatTable selection, edit, and raw-copy bindings worked live. `Ctrl+C` returned the exact selected-cell value. | `Alt+R`; from initially focused OK use `Shift+Tab`, `Ctrl+A`, mandatory `Ctrl+Home`, Right; Down by row; use `Ctrl+C` for each value; F2 edits; `Alt+F4` closes without submission | Explicitly read and set `HRC 4.0 (Default)`, Full Tree, Until CI value is reached, CI Target `10.0`, Reset Regret clear, and Reset Strategies clear. Read back each value before selecting OK. | Per-cell raw copy returned the exact retained values, including CI `1.0` and reset pair `false,false`. The earlier probe separately committed CI `10.0`, but did not copy it after editing and closed without submission. The earlier demonstration showed Progress with `MC-CFR [Target CI < 10.00]`. | Whole-grid `Ctrl+A`, `Ctrl+C` copied only `CFR Algorithm`. Omitting `Ctrl+Home` caused ambiguous highlighting. A malformed value, rejected edit, failed submission, or unrecognised post-state must stop. | NO for submission: exact supervised per-cell read-back is confirmed, but native focus, CI `10.0` post-edit copy, OK submission, and accepted, rejected, queued, running, cancelled, completed, or failed detection remain unproven through the target runner. |
| Submit CI 1 | `Run Nash Calculation`; `Reset Strategies`; `OK`; exact setting labels | Base accessibility exposed OK and Cancel; F2 exposed exact combo choices | Dialog; buttons; NatTable; combo list; edit | Earlier OK `662418` and Cancel `859034`; later IDs changed; stability disproven | Default NatTable selection, edit, checkbox, and raw-copy bindings worked live. | Use the same mandatory bootstrap; navigate from origin to both reset rows; Space toggles the selected reset cell; `Ctrl+C` reads each raw Boolean; `Alt+F4` closes without submission | Explicitly read and keep the same algorithm, scope, and sampling mode. Set CI Target to `1.0`, select Reset Strategies, keep Reset Regret clear, read back every value, and only then select OK. | With Reset Strategies visibly checked, per-cell copies returned the exact required pair `Reset Regret = false`, `Reset Strategies = true`. Strategies was restored to `false` before `Alt+F4`. The earlier demonstration showed Progress with `MC-CFR [Target CI < 1.00]`. | Any reset pair other than required `false,true`, any malformed read-back, failed submission, or unrecognised post-state must stop. Cell highlighting is not a value oracle. | NO for submission: exact supervised reset-pair verification is confirmed, but native focus, OK submission, post-OK one-shot clearing, and accepted, rejected, queued, running, cancelled, completed, or failed detection remain unproven through the target runner. |
| Detect accepted, rejected, queued, running, cancelled, completed, or failed Nash state | `Progress`; `<hand-name>: Monte Carlo Sampling`; `MC-CFR [Target CI < 10.00]`; `MC-CFR [Target CI < 1.00]`; `No operations to display at this time.` | Visible running labels were demonstrated; exact terminal result is not externally exposed | Eclipse Job and Progress view | No public UUID; both submissions share one public name | In-process `IJobChangeListener` supplies exact object identity/state/result statically; no supported external hook was found | UI-only route is insufficient | Associate each scheduled Job object with submission order; require `WAITING`, `RUNNING`, and its own terminal `IStatus`. A later Job must not validate an earlier one. | Static inspection proves two distinct serialised Jobs and exact `OK` success. Earlier UI showed each running target. Severity `ERROR` is retained externally. | `CANCEL` and `ERROR` are explicit in-process, but successful and cancelled Jobs can both disappear into the same idle text. Achieved CI, samples, dirty/model state, editor refresh, Viewer success, and Viewer metadata are not outcome-specific. Missing/mismatched events and timeout are unknown. | NO for the current external UI-only architecture. Exact terminal detection requires an authorised in-process event bridge or another independently proven durable postcondition. |
| Viewer save | `File`; `Save As`; `File name:`; `Save as type:`; `*.hrcv Viewer Save`; `Save` | Standard dialog labels, exact address, and existing file names were exposed | Standard Save As dialog; edits; combo box; buttons | Filename `1001`; type host `FileTypeControlHost`; Save `1`; Cancel `2` | Standard patterns; exact filename/type read-back and setting remain TO CONFIRM LIVE | `Ctrl+Alt+S` opens Save As; Escape cancelled | Acquire the exclusive HRC-control lease and atomically reserved private staging namespace; preflight canonical and staging targets; set/read destination, lowercase `.hrcv` staging filename, and Viewer type; submit once; identity-match `Saving hand to: <staging-filename>`; require success and stable non-empty staging metadata; publish canonical output with fail-if-exists semantics. | `HU-1.5.hrcv` was submitted earlier. Run 19 opened exact HU destination, proposed `From Hand 7.hrcv`, visibly selected Viewer Save, and cancelled. Static inspection proves accepted Viewer Save queues on the same per-hand rule and leaves the editor dirty. | Type and folder are retained mutable state. Run 19's provider reported Search and omitted selected type text. HRC can replace a race-created staging target, so high entropy alone is insufficient. Any guard, namespace, path, field, type, name, prompt, Job, result, or output mismatch must stop. | NO for a write: native control, identity-matched result, exclusive HRC-control lease/namespace, and fail-if-exists publication remain unvalidated through the target runner. |
| Verify Viewer output | `<simulation-name>.hrcv` | Not applicable | File | Not applicable | Not applicable | Not applicable | Verify the new file without opening or modifying it. | `HU-1.hrcv`, `HU-1.5.hrcv`, and `HU-2.hrcv` existed at the required HU path with non-zero sizes. | The file is absent, empty, or saved elsewhere. | YES for read-only metadata verification of these exact new files. |
| Submit strategy export | `Hand`; `Export Strategies`; `Complete Export`; `Depth:`; `PrettyPrint JSON`; `Node Filter Threshold %`; `OK` | Title and instruction were exposed; scope and settings values were not named machine-readably | Combo box; spinner; edit; tree; buttons; settings NatTable and two Save As helpers known statically | Run 19 scope `2103484`, spinner `5969360`, edit `1579322`, OK `407704098`, Cancel `1186062`; earlier IDs differed | Provider scope action returned unknown; static native-control, NatTable, and standard FileDialog routes remain TO CONFIRM LIVE | `Ctrl+H`, then `E` opened the dialog; Escape cancelled | Under the same exclusive session/private namespace, set/read every Export value; require exactly ZIP and Plain Text; select/read ZIP; preflight staging; accept once; identity-match `Exporting ranges to <staging-filename>` including `.zip`; require Job success and stable non-empty metadata; publish canonical output with fail-if-exists semantics. | Run 19 visibly retained Complete Export, Depth `2`, PrettyPrint clear, threshold `0.1`, and the two-node range tree; Escape cancelled before Save As. Earlier runs created non-empty `.zip`-named files. Static inspection proves the fresh-hand two-filter guard and that accepted ZIP sets the writer index before submission. | Export closes even when Save As returns no path; disappearance is not submission. A ZIP-only dialog is unverifiable. The writer can truncate a race-created target, and error/cancellation can delete or leave partial output. Never inspect ZIP contents or delete a partial file automatically. | NO until every action/read-back, two-filter guard, identity-matched result, exclusive session/namespace, and fail-if-exists publication have a live durable path. |
| Verify strategy output metadata | `<simulation-name>.zip` | Not applicable | File | Not applicable | Not applicable | Not applicable | Verify the new `.zip`-named file without opening or modifying it. | `HU-1.zip`, `HU-1.5.zip`, and `HU-2.zip` existed at the required HU path with non-zero sizes. | The file is absent, empty, or saved elsewhere. Hidden format state cannot be resolved from metadata. | YES for read-only metadata verification of these exact files; NO as proof of archive format. |
| Close completed hand tab | `Close`; `Save Resource`; `Save '<simulation-name>'?`; `Don't Save`; `Home` | Prior prompt exposed its title, message, and three named buttons; the tab close glyph is not an accessible child | MSAA tab folder/items; owned JFace prompt; native buttons | Numeric session values only; stability TO CONFIRM | Tab-item default action is only `Switch`; guarded `Ctrl+F4` plus prompt-button native action is a static candidate | Previous `Ctrl+F4` had no effect while active-editor focus was not proved | Treat every pre-existing hand-editor tab as protected. Immediately before close require the exact set to be those stable identities plus exactly one expected completed target. Prove exact target editor/focus, send `Ctrl+F4` once, require the exact prompt, then select `Don't Save`. Require the post-close set to equal exactly the unchanged protected identities, with no additions or replacements; explicitly activate Home. A pre-production probe uses exact Cancel instead. | Earlier clean-session demonstrations closed `*HU-1.5` and `*HU-2` and left only Home. Static inspection supplies an exact Cancel-only probe contract, but it was not executed in run 19. | Any focus, selection, prompt, filename, tab-set, target-removal, timeout, or Home-state mismatch must stop without retry; Enter is dangerous because Save is default. | NO until native target proof, one `Ctrl+F4` prompt, guarded Cancel/unchanged post-state, guarded `Don't Save`/exact-set removal, protected-tab preservation, and Home activation are live-confirmed through the runner. |
| Start next simulation | `New: Monte Carlo Hand`; `Hand`; `Start New Calculation` | Home link named; command semantics TO CONFIRM | Link; command | TO CONFIRM | TO CONFIRM | `Ctrl+W`, then `H` works from an active hand but inherits its state | Explicitly activate Home after target-only closure, preserve protected tabs, then open a new Monte Carlo Hand from Home. Require a known clean setup or overwrite/read every retained field. | The Home link opened Hand Setup during isolated discovery. | Run 18 proved that the active-hand shortcut retains rows and script and creates a `From Hand` resource. It must not be treated as a clean transition. | TO CONFIRM: the target-only close, Home activation with protected tabs still open, and next clean setup have not been demonstrated. |

## Observable states

| State | Visible evidence | Accessible evidence | Distinguishable | Notes |
| --- | --- | --- | --- | --- |
| Configured | Hand Setup closed. An unsaved `*Hand 1` tab opened with strategy, range, and HU table views. Progress showed no active operation. | The tree exposed `*Hand 1`, `Strategy Table`, `Hand Settings`, and `Run Nash Calculation (Alt+R)`. | CONFIRMED | Tree creation completed. The calculation was not started. |
| Native Finish succeeded | One guarded native `BM_CLICK` closed Hand Setup and opened `*From Hand 7` alongside `*Hand 7`; no error appeared and Progress was idle. | Before action, native enumeration found one owned `#32770` and one visible enabled `Button` captioned `&Finish`. Relative to the pre-action hand-editor set, the accessible set gained exactly one expected editor tab. | CONFIRMED for the positive Finish path | The provider's indexed action was unavailable and was not used. Wizard closure or idle alone is not success. Zero, multiple, wrong-type, replaced, renamed-only, or unknown tab deltas must stop. Explicit failure, cancellation, and unknown-timeout paths remain TO CONFIRM live. |
| Later lifecycle dialogs cancelled without output | Rename Hand, Save As, and Export Strategies each opened from active `*From Hand 7` and returned unchanged after Escape. | Named dialog controls were exposed, but provider focus pointed to background or wrong fields; Rename and Export semantic actions returned unknown and fresh observation showed no change. | CONFIRMED for entry, visual state, cancellation, and no output only | Exact Rename input, Viewer filename/type read-back, Export setting control, and native focus remain TO CONFIRM. Both corresponding exact output targets remained absent. |
| Five-player inputs accepted | Basic Hand Data showed `HJ`, `CO`, `BU`, `SB`, and `BB` with `10`, `20`, `30`, `40`, and `50` bb. `Alt+N` opened Betting Setup. | The transient stack editor was unnamed and provider focus data was wrong. | CONFIRMED visually | This confirms the manual setup only. It does not confirm safe stack automation or a multiway tree. |
| HU 2bb shallow preview verified | Expanded Preview showed `R 2.00 SB PRE` with exactly one child, `C 1.00 BB PRE`. | The preview tree exposed root `R` and child `C`. | CONFIRMED for both the pre-conversion revision and exact current worktree candidate at equal `2 bb` | No SB completion branch was present. This does not validate the `5 bb` boundary, other HU stacks, or multiway behaviour. |
| Nash configurations inspected without submission | The grid exposed the exact algorithm, scope, sampling, CI, and reset values. Per-cell raw copy returned the retained values. With Reset Strategies checked, the required pair read `false,true`; Strategies was then restored to `false`. | Base accessibility exposed the buttons; F2 exposed combo items and a transient CI editor. `Ctrl+C` returned raw selected-cell values. | CONFIRMED for supervised configuration, per-cell read-back, reset-pair verification, and non-submitting close routes | One whole-grid copy returned only the origin label. From initial OK focus, Tab visibly focused Cancel and Space invoked it. `Alt+F4` closed from the grid without submission. CI `10.0` was not copied after its separate edit. No OK submission occurred and Progress stayed idle. |
| Multiway retest estimate | Scripting showed `tree-building-3m-6m-candidate.js` without `[Errors]`, Total Nodes `1815589`, Total Tree Size `12.3GB`, and enabled Finish. | No accessibility tree was available in the contemporaneous capture. | CONFIRMED visually for the observed five-player setup | Euan reported loading the corrected worktree candidate. At the time of this estimate capture, Preview had not yet been inspected and Finish was not selected. The following row records the later Preview inspection. |
| Multiway representative Preview | The root and selected opening, squeeze, 3-bet, 4-bet, 5-bet, call-cap, SB-completion, and low-SPR flop paths were expanded. | Preview tab `661798`; tree `989272`; named headers; selectable action-only items. | CONFIRMED visually for the listed paths | Every displayed value matched the current candidate manifest for the reported corrected load. This is not exhaustive tree validation, and the accessible item names omitted amount, player, and street. |
| Player-count choices exposed | Pressing `Tab` seven times from the newly opened Basic Hand Data page, then `Ctrl+A` and `Ctrl+Home`, selected the cell displaying `Auto`; Space opened `Auto`, `HU`, and table sizes `3-max` through `10-max`. Escape closed the list without changing the setup. | Every item had a distinct accessible name and was selectable. The latest list ID in that run was `11606852`; earlier openings used `95687566` and `18224586`. Provider focus still pointed to the background Range edit. | CONFIRMED for one non-coordinate focus-and-open route | Run 12 did not activate a choice. Run 13 later confirmed HU row removal and retained blind stacks; other choice effects and standalone delivery remain TO CONFIRM. |
| HU table-size selection committed | From the retained five-player setup, the keyboard route selected `HU`. Before Enter, the list remained open and five rows remained; after Enter, only `SB 4000 / 40.0 bb` and `BB 5000 / 50.0 bb` remained. | The open list exposed `HU` as a named selectable item. Its latest list ID was `6359478`; provider focus still pointed to the background Range edit. | CONFIRMED for one supervised non-coordinate selection | At the end of run 13, multiway selection effects, active-seat overwrite/read-back, and standalone delivery remained TO CONFIRM. Run 14 later confirmed supervised HU overwrite and visual read-back; machine-readable verification remains TO CONFIRM. |
| Multiway stack keyboard edit | With HJ Chips focused, `F2` exposed `1000`; typing the same value and pressing Enter moved to CO Chips with `2000` selected. Escape preserved CO. | Stack cells were absent from the accessibility tree, and the provider continued to report background edit `69008` as focused. | CONFIRMED after an existing cell was focused | This earlier observation alone confirmed post-focus movement, edit mode, same-value commit, advance, and cancel without a net value change. Run 14 later confirmed the supervised HU route from initial grid focus through different-valid-value entry. Standalone foreground/focus assertions and machine-readable read-back remain TO CONFIRM. |
| HU stack values committed and rejected input recovered | The combined keyboard route committed `SB 4100 / 41.0 bb` and `BB 5100 / 51.0 bb`; Escape cancelled the blank third-row editor. Invalid `abc` stayed red and did not commit on Enter; Escape restored `4100 / 41.0 bb`. | Transient editors were unnamed and changed IDs. The provider incorrectly reported background Range edit `69008` as focused. | CONFIRMED visually for this supervised HU path | This proves different-valid-value entry, derived visual read-back, blank-row cancellation, and one non-numeric recovery. Machine-readable cell verification, multiway operation, and standalone delivery remain TO CONFIRM. |
| Script picker opened and cancelled | From the visible focus rectangle on `Back`, the keyboard route selected Scripting and opened the standard Open dialog at the worktree candidate folder. Escape restored the visible focus rectangle to the folder button; Space reopened the same folder and Escape cancelled again. | The Open dialog exposed both filenames and named standard controls, but provider focus disagreed with the visible File name caret. | CONFIRMED for one supervised non-coordinate route and one same-setup reopen | No script was selected. Native foreground/focus, active-field, exact-candidate, post-load, and standalone assertions remain TO CONFIRM. |
| Renamed | The tab changed from `*Hand 2` to `*HU-1`. | TO CONFIRM | CONFIRMED visually | Progress later used the `HU-1` name. |
| Queued | Installed inspection shows every OK schedules a distinct Job and the shared per-hand rule serialises them; no persistent queue list was visible in the captured states. | Exact identity exists only as the in-process Java Job object. Both use the same public name. | CONFIRMED statically for distinct Job creation and serialisation; TO CONFIRM externally | The CI 1 dialog opened while CI 10 was visible, but the small operation transitioned quickly. Serialisation does not require CI 10 to succeed before CI 1 runs. |
| CI 10 running | Progress showed `HU-1: Monte Carlo Sampling` and target CI `< 10.00`. | TBD | CONFIRMED visually | A red stop button and activity bar were visible. |
| CI 1 running | Progress showed `HU-1: Monte Carlo Sampling` and target CI `< 1.00`. | TBD | CONFIRMED visually | Reset Strategies displayed a checkmark in the submitted dialog. |
| CI 10 no longer displayed | The CI 10 line was replaced by the CI 1 line. | TBD | CONFIRMED visually | The reason for the transition is TO CONFIRM. No explicit successful-completion marker was captured. |
| No operation displayed | Progress later showed `No operations to display at this time.` | TBD | CONFIRMED visually | This text alone does not distinguish success from failure. |
| Viewer saved | The Save As dialog accepted `HU-1.5.hrcv` with `*.hrcv Viewer Save`. | File existence was verified separately. | CONFIRMED | Viewer Save returned to the still-unsaved `*HU-1.5` tab. |
| Tree-script failure | Script Error showed `Error: Effective stack does not match a configured workbook column: 100000`. After dismissal, Scripting showed `[Errors]`, zero nodes, and disabled Finish. | The exact error text and OK button were exposed in the inspected tree. | CONFIRMED for this failure | No tree, calculation, or output followed. Generic failure detection remains TO CONFIRM. |
| Calculation or output failed | TBD | TBD | TO CONFIRM | No Nash, Viewer Save, or export failure has been observed. |
| Complete Save | The first Save As used the default `*.hrcz Complete Save` in error. The tab changed to `HU-1.hrcz`. | TBD | CONFIRMED visually | The unintended file was present and unmodified when last checked; later state is TO CONFIRM. |
| Viewer output verified | The new `.hrcv` file exists at the required HU path and has non-zero size. | Read-only file metadata returned the expected path, size, and timestamp. | CONFIRMED | File contents were not opened or modified. |
| Strategy export file created | The export dialog accepted the required settings and `HU-1.zip` path. HRC returned to the source tab. | File existence was verified separately. | CONFIRMED for non-zero `.zip`-named file creation only | No explicit export-success message was visible, and hidden format state was not observed. |
| Strategy output metadata verified | The new `.zip`-named file exists at the required HU path and has non-zero size. | Read-only file metadata returned the expected path, size, and timestamp. | CONFIRMED for metadata only | The file was not opened. Its actual format, structure, JSON content, and strategy completeness remain unverified. |
| Viewer-only close prompt | `Save Resource` asked `Save 'HU-1.5'?` and later `Save 'HU-2'?`. Both prompts showed `Save`, `Don't Save`, and `Cancel`. | The `HU-2` prompt and button names were readable through UI Automation. `Don't Save` did not expose InvokePattern. | CONFIRMED visually; accessible operation TO CONFIRM | Viewer Save and strategy export did not clear the leading asterisk on either unsaved tab. |
| Completed tab closed | `Don't Save` was selected. The source tab disappeared and only `Home` remained. | File metadata was verified after the close. | CONFIRMED | The `.hrcv` and `.zip` files persisted. No matching `.hrcz` file was present. |
| Next simulation started | TBD | TBD | TO CONFIRM | The transition to the next simulation was not demonstrated. |

## Test runs

| Run | Date and time | Planned output | Result | Observed duration | Evidence | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 10 August 2026, 22:40 BST | None | TREE CREATED | TBD | HU `1 bb` tree creation succeeded and opened `*Hand 1`. | No calculation or save was performed in this observation. |
| 2 | 10 August 2026, 23:08–23:10 BST | `HU-1.hrcv`; unintended `HU-1.hrcz` | PARTIAL DEMONSTRATION | Progress changed to no operation displayed within the observation period. Explicit completion and production duration remain TO CONFIRM. | The demonstration renamed `*Hand 2` to `*HU-1`, submitted both Nash configurations, showed both running targets, made an accidental Complete Save, corrected it with Viewer Save, and verified the Viewer file. | This observation began with `*Hand 2` and is separate from run 1. Long-run queue order and explicit calculation success or failure remain unconfirmed. The unintended Complete Save was present and unmodified when last checked; later state is TO CONFIRM. |
| 3 | 10 August 2026, 23:18 BST | `HU-1.zip` | PARTIAL DEMONSTRATION | The export and close transition completed within the observation period. | The demonstration kept Complete Export, changed Depth from `2` to `16`, kept PrettyPrint JSON clear, and kept the threshold at `0.1`. It saved a non-empty `.zip`-named file and then closed the source tab. | The source tab was `HU-1.hrcz` from run 2. The actual output format, contents, and close behaviour after a Viewer-only save remain unverified. |
| 4 | 10 August 2026, 23:35–23:36 BST | `HU-1.5.hrcv`; `HU-1.5.zip` | PARTIAL DEMONSTRATION | Viewer Save submission, non-empty output creation, and Viewer-only tab closure were observed. | The demonstration began on `*HU-1.5`, submitted Viewer Save and strategy export, selected `Don't Save` in the close prompt, and returned to `Home`. Both files were non-empty after close, and no matching `.hrcz` file was present. | Euan reported that rename and both Nash runs were already complete before observation. Their completion was not independently observed. File contents were not opened. |
| 5 | 10–11 August 2026, ending 00:13 BST | `HU-2.hrcv`; `HU-2.zip` | PARTIAL DEMONSTRATION | The two-node calculations returned to idle during the supervised observation. No explicit calculation-success marker appeared. | The pre-conversion HU candidate from `9b24166` created an equal-stack `2 bb` tree. Hand Setup reported two nodes. The run renamed the hand, submitted CI `10.0`, submitted CI `1.0` with Reset Strategies, created both non-empty outputs, verified no matching `.hrcz`, selected `Don't Save` on the exact `HU-2` prompt, and returned to `Home`. | Run 5 did not inspect Preview or confirm the cutoff; run 6 later confirmed the equal-`2 bb` case for that revision. Euan assisted with strategy export. The `.zip`-named file was not opened; actual format and contents remain unverified. Codex-specific window activation changed the HRC bounds and was discontinued. An unverified coordinate selected the root row instead of the tab close control; the later close point was verified by its exact tooltip before use. |
| 6 | 11 August 2026 | None | ACCESSIBILITY DISCOVERY | No Nash operation or file write occurred. | HRC remained in a restored, near-full-size window. The same pre-conversion HU candidate loaded through the standard Open dialog. Hand Setup reported two nodes. Expanded Preview showed `R 2.00 SB PRE` with only `C 1.00 BB PRE`. Enter finished to `*Hand 6`. Rename, Nash, Save As, and Export Strategies were opened for inspection and cancelled. | Euan manually selected Hand Setup Next after programmatic input failed. A later same-day follow-up confirmed `Alt+N` as the keyboard route. Nash settings remained inaccessible in run 6. `Ctrl+F4` did not close the hand. The unsaved `*Hand 6` was still open at the end of run 6; its later disposition is TO CONFIRM. Run 16 later revalidated the current HU candidate and established a supervised Nash-grid route. |
| 7 | 11 August 2026 | None | SCRIPT ERROR | No tree was finished and no Nash operation or file write occurred. | Euan configured five rows as `HJ 10`, `CO 20`, `BU 30`, `SB 40`, and `BB 50` bb. `Alt+N` advanced to Betting Setup. Loading the then-byte-identical pre-correction multiway candidate produced `Error: Effective stack does not match a configured workbook column: 100000`. | The pre-script default estimate was `448527` nodes and `3.1GB`; it was not a candidate result. After the error, Total Nodes was `0` and Finish was disabled. Offline regression coverage was added, but the corrected candidate was still HRC-unverified at the end of run 7. |
| 8 | 11 August 2026 | None | TREE ESTIMATE AND PARTIAL PREVIEW | No tree was finished and no Nash operation or file write occurred. | Euan reported loading the corrected worktree candidate. A contemporaneous capture showed its basename without `[Errors]`, Total Nodes `1815589`, Total Tree Size `12.3GB`, and enabled Finish. Root and representative deeper Preview paths matched the current candidate manifest. | The prior `100000` error did not recur. Preview evidence is path-scoped, Finish was not selected, and the visible basename did not expose the full loaded path. Other stacks, table sizes, boundaries, later streets, and unexpanded paths remain TO CONFIRM. |
| 9 | 11 August 2026 | None | ACCESSIBILITY DISCOVERY | No row, stack, calculation, tree, or file was changed. | `Alt+B` and `Alt+N` moved between the two setup pages. After a one-use screenshot-located focus, arrows, `Ctrl+Home`, `F2`, Enter, and Escape supported an observed grid-edit sequence. A same-value HJ Chips commit advanced to CO Chips; all five values remained unchanged. | No durable non-coordinate entry into the grid was found in run 9. Stack cells were absent from the accessibility tree, Tab routes failed, provider focus remained wrong, and blank-row creation and different-valid-value entry were not tested. The setup was open on Basic Hand Data at the end of the run. |
| 10 | 11 August 2026 | None | ACCESSIBILITY DISCOVERY | No selector choice, row, stack, calculation, tree, or file was changed. | Tab visibly reached Next, Cancel, information text, and four unnamed toolbar buttons, but traversal did not establish a visible, repeatable route to the selector or stack grid. A one-use screenshot-located click opened `Auto`; the accessibility tree exposed named selectable choices from `HU` through `10-max`. Escape closed the list. | Run 10 found no durable semantic or keyboard route to open the selector, and no choice was activated. The existing five rows and `10/20/30/40/50 bb` values remained unchanged. Hand Setup was open on Basic Hand Data at the end of the run. |
| 11 | 11 August 2026 | None | ACCESSIBILITY DISCOVERY | No selector choice, row, stack, finished tree, calculation, or file was changed. | After the screenshot-located open and Escape from run 10, Space reopened the player-count list while the cell displaying `Auto` remained current; `Alt+Down` and `F4` did not. Its list ID changed. Escape closed it. `Alt+N` and `Alt+B` cycled the setup pages and preserved the five inputs, but Space then did not reopen the list. | Space was confirmed as a post-focus list-opening action in run 11 only. The page cycle did not provide repeatable selector focus, no choice was activated, and selection effects remained untested. Hand Setup was open on Basic Hand Data at the end of the run. |
| 12 | 11 August 2026 | None | ACCESSIBILITY DISCOVERY | No selector choice, row, stack, finished tree, calculation, or file was changed. | Static installed-component inspection identified the NatTable selection bootstrap. In a separate live check, the named Home link opened Basic Hand Data with the previous five inputs retained. Pressing `Tab` seven times, then `Ctrl+A`, `Ctrl+Home`, and Space selected the cell displaying `Auto` and opened every named player-count choice without a pointer. Escape closed the list and preserved all inputs. | This superseded the missing initial selector-focus route from runs 10 and 11 for one supervised setup. No choice was activated. Two cached named-target attempts, `Alt+C`, and Escape did not dismiss Hand Setup; `Alt+F4` returned safely to `Home`. Selection effects and retained-value handling were unproven at the end of run 12; runs 13–14 later confirmed the supervised HU effects. Standalone delivery remains unproven. |
| 13 | 11 August 2026 | None | ACCESSIBILITY DISCOVERY | The disposable in-memory row set changed; no stack was edited, and no tree, calculation, or file was created. | The confirmed keyboard bootstrap opened the selector. Down visibly selected `HU`; Enter committed it and reduced the retained five-player setup to `SB 4000 / 40.0 bb` and `BB 5000 / 50.0 bb`. `Alt+F4` returned to `Home` without advancing or prompting. | This proves one supervised non-coordinate table-size selection and shows that the change retained prior blind stacks. At the end of run 13, active-seat overwrite/read-back and rejected-input handling remained unproven; run 14 later confirmed supervised visual HU handling. Multiway choice effects, machine-readable verification, and standalone operation remain TO CONFIRM. |
| 14 | 11 August 2026 | None | ACCESSIBILITY DISCOVERY | The disposable in-memory stack values changed; no wizard advance, tree, calculation, or file was created. | A new setup showed `Auto` with the retained SB/BB row set. The confirmed keyboard route selected HU, entered `4100` and `5100`, visibly read back `41.0 bb` and `51.0 bb`, and cancelled the blank next-row editor. Invalid `abc` stayed red and uncommitted; Escape restored `4100`. `Alt+F4` returned to `Home`. | This proves the combined supervised HU keyboard path, two different-valid-value commits, visual derived-value checks, blank-row cancellation, and one rejected-input recovery. It does not prove machine-readable cell verification, multiway choice/edit behaviour, reliable focus metadata, or standalone operation. |
| 15 | 11 August 2026 | None | ACCESSIBILITY DISCOVERY | No script was selected or loaded; no tree was finished, calculation submitted, or file written. | From the visible focus rectangle on `Back`, four Tabs reached `Preflop`, two Rights selected `Scripting`, and two Tabs plus Space opened the standard Open dialog at the exact worktree candidate folder. Escape restored the visible focus rectangle to the unnamed folder button; Space reopened the same folder and Escape cancelled again. `Alt+F4` returned to `Home`. | This proves one supervised non-coordinate route and one same-setup reopen after cancellation. `Ctrl+PageDown` did nothing. Provider focus disagreed with the visible File name caret, so native foreground/focus, active-field, exact-candidate, post-load, and standalone assertions remain TO CONFIRM. |
| 16 | 11 August 2026 | None | CURRENT HU PREVIEW AND NASH-GRID DISCOVERY | No Nash operation or file write occurred. | The full supervised keyboard route selected HU, entered equal `2 bb` stacks, opened the exact worktree candidate after hash verification, showed no `[Errors]`, reported two nodes, and expanded the exact two-node Preview. A discovery-only current-frame Finish click created `*Hand 7`. Nash probes configured CI `10.0` and the required CI `1.0` plus Reset Strategies checkmark, then closed without submission. | Valid keyboard input did not activate Finish and appeared to reach the background. Nash required `Ctrl+A` followed by `Ctrl+Home`; omitting the second key caused ambiguous multi-cell/reset highlighting. From initial OK focus, Tab visibly focused Cancel and Space invoked it. `Alt+F4` closed from the grid without submission. Progress stayed idle and `*Hand 7` remained open. |
| 17 | 12 August 2026 | None | NASH RAW READ-BACK DISCOVERY | No Nash operation or file write occurred. | On existing `*Hand 7`, the supervised grid route returned the six exact retained values through per-cell `Ctrl+C`. With Reset Strategies visibly checked, copies returned `Reset Regret = false` and `Reset Strategies = true`. Strategies was restored to `false` and copied again before closing without submission. | One whole-grid `Ctrl+A`, `Ctrl+C` attempt copied only `CFR Algorithm`; it is not the supported snapshot route. CI `10.0` was edited separately in run 16 but not copied after editing. Native foreground and focus were not independently asserted. Progress stayed idle, HRC kept its bounds, and `*Hand 7` remained open. |
| 18 | 12 August 2026 | None | NATIVE FINISH DISCOVERY | No Nash operation or file write occurred. | From active `*Hand 7`, `Ctrl+W`, then `H` opened a retained-state setup. The supervised route selected HU and reached the retained two-node candidate estimate. Exact native enumeration found one owned Hand Setup dialog and one enabled `&Finish` button. One guarded `SendMessageTimeout(BM_CLICK)` added exactly one expected hand-editor tab relative to the pre-action set, `*From Hand 7`, with no error. | The provider could expose but not target the indexed Finish element; fresh observation proved no action before the native route. The active-hand shortcut retained the rows and script in this run and is not a clean next-simulation transition. Both unsaved tabs remain open; neither has matching outputs and neither may be discarded through Viewer-only closure. |
| 19 | 12 August 2026 | None | NON-WRITING LIFECYCLE DISCOVERY | No rename, Nash submission, output, prompt, or tab close occurred. | On active `*From Hand 7`, the exact shortcuts opened Rename Hand, Save As, and Export Strategies. Rename and Export semantic actions returned unknown, then fresh observations proved no visible change. Save As showed exact HU destination, proposed Viewer filename, and Viewer type. Escape cancelled every dialog. | Provider focus disagreed with each visible field. The File menu had no hand-close command and the run exposed no named close target, so no close command was sent. Read-only checks confirmed both corresponding exact output targets remained absent; both dirty tabs and idle Progress were preserved. |

## Blockers

- `Alt+N` is a confirmed supervised path for Hand Setup Next. The target runner
  still must focus the owned dialog, deliver the shortcut, and detect Betting
  Setup reliably; earlier automated keyboard input reached the background
  window.
- The non-coordinate NatTable bootstrap now covers HU selection and both active
  stack edits end to end in a supervised run. Two different values committed,
  their derived BB values were checked visually, the blank next-row editor was
  cancelled without adding a row, and one non-numeric value was rejected and
  safely cancelled. New setups still retain prior row/value state independently
  of the `Auto` selector label. The provider reported the wrong background
  focus, transient editor IDs changed, and no machine-readable stack-cell
  verification was established. Multiway choice/edit effects, foreground and
  focus assertions, and standalone delivery are not safe for automation.
- One supervised keyboard sequence reached the unnamed script-picker folder
  button from the visible focus rectangle on `Back`, and Space reopened the
  picker once after cancellation in the same setup. The button still has no
  accessible name or access key, its numeric ID changed between sessions, and
  semantic invocation failed. A later supervised run verified the exact
  worktree candidate and hash, typed the filename, opened it, and observed the
  expected post-load basename, no `[Errors]`, two-node estimate, and enabled
  Finish. Before this path is safe, the target runner must reproduce the exact
  owned foreground dialog, native starting focus, every Tab and Right
  transition, exact folder, active `File name` field, exact filename read-back,
  preflight hash, Open activation, and post-load or Script Error detection.
- Finish is no longer a coordinate blocker for its positive path. Run 18
  confirmed exact owned-dialog and unique-button enumeration, one guarded native
  `BM_CLICK`, and a pre/post hand-editor set delta of exactly one expected tab as
  success. Zero, multiple, wrong-type, replaced, renamed-only, or unknown deltas
  must stop without retry. The runner still needs the same guards and no-retry
  timeout policy. Explicit tree-creation error and cancellation presentation
  remain TO CONFIRM live.
  Native foreground and focus are required only for a keyboard fallback, which
  is unnecessary for the confirmed native action.
- After Euan reported loading the corrected 3–6-max candidate, HRC passed
  runtime evaluation and produced a non-zero tree estimate in the observed
  five-player setup. The capture exposed only the basename. The inspected
  Preview paths matched the current candidate manifest, but unexpanded paths,
  other stacks and table sizes, boundary cases, and later streets remain
  unverified. Preview item names omitted amount, player, and street, and provider
  focus data was wrong. Durable automated validation remains blocked.
- The live Nash probe confirmed initial OK focus, Tab focus on Cancel followed
  by Space invocation, `Shift+Tab` entry to the NatTable, the mandatory
  `Ctrl+A`, `Ctrl+Home`, Right bootstrap, exact combo choices, CI editing, a
  Reset Strategies checkmark, and `Alt+F4` closure without submission. Run 17
  added exact per-cell raw-copy values. With Reset Strategies checked, the
  required reset pair copied as `false,true`; Strategies was restored to
  `false` before closing. One whole-grid copy returned only the origin label.
  Omitting `Ctrl+Home` produced ambiguous highlighting. Base accessibility still
  exposed only OK and Cancel. The target runner needs native-focus assertions,
  per-cell parser and transition checks, CI `10.0` post-edit read-back, safe OK
  submission, and machine-readable accepted, rejected, queued, running,
  cancelled, completed, and failed post-states.
- Run 19 confirmed exact Rename dialog entry and cancellation, but provider
  `set_value` returned unknown and fresh observation showed no name change.
  Reported focus pointed to a background edit. Static inspection supplies exact
  validation messages and a synchronous same-editor title post-state, but exact
  input, read-back, guarded OK, and live rejected-value handling remain unsafe.
- Save As opened at the exact HU destination with the expected proposed `.hrcv`
  filename and Viewer type. The provider reported Search instead of File name
  and did not expose the selected type text. Exact machine-readable destination,
  filename, type, extension, write-job identity, and terminal-state handling
  remain unsafe. Static inspection proves that Viewer Save uses the same
  per-hand scheduling rule as Nash, but HRC's final replace-existing move leaves
  a no-overwrite race after preflight. An exclusive HRC-control lease,
  validated exclusively owned staging namespace, and fail-if-exists final
  publication remain implementation-gate blockers.
- Export Strategies exposed its native scope, spinner, tree, and buttons in run
  19 but did not name the settings values machine-readably. Provider focus was
  wrong; a scope action returned unknown and fresh observation showed no change.
  Settings can persist after Cancel. Static inspection shows Complete Export is
  unlimited-depth even though Depth remains visible. Its final writer uses
  create-and-truncate rather than create-new, so preflight cannot prevent a
  race-created target from being overwritten. Safe targeting, read-back,
  identity-matched terminal detection, exclusive staging/HRC-control guards, and
  fail-if-exists publication are unproven. A separate hidden retained-index
  defect can write plain text to
  a ZIP-only target and still return `OK`. Static inspection supplies a safe
  fresh-hand guard—the actual Save As must expose both ZIP and Plain Text, and
  accepted ZIP updates the consumed index—but its live durable route remains
  unproved.
- The tab close glyph is not an accessible child and a tab item's default action
  only switches tabs. Static inspection supplies a guarded native-focus,
  selection-round-trip, one-shot `Ctrl+F4`, exact-prompt, and Cancel-only probe.
  Run 19 did not execute it because native focus and active-editor identity were
  not proved. A durable live tab-close and `Don't Save` operation remain
  unproven. The smoke must also protect every pre-existing hand-editor tab,
  prove exact pre-close set equality with one added completed target, prove exact
  post-close equality with the unchanged protected set, and explicitly activate
  Home.
- Installed inspection proves separate same-hand Nash Jobs serialise in
  submission order, but visible names do not distinguish them and serialisation
  is not dependency success. A UI-only observer cannot distinguish successful
  completion from cancellation after either item disappears. No supported
  external exact-result hook was found.
- The read-only producer audit and isolated Equinox fixture support observer-
  before-producer ordering on the exact normal clean-launch route. They do not
  prove live HRC observer activation or prevent arbitrary `Bundle.loadClass`,
  reflection, or a different early activation route. The no-runtime-unload
  result is a policy model; it does not prove dynamic provider-level callback
  drainage. These conditions remain pre-live gates.
- Version-specific paths are conditional on the exact eight-component fingerprint
  above. The runner's filename/hash identity gate is not implemented or tested.
- The transition to the next simulation has not been demonstrated.

## Verdict

- Feasibility: TO CONFIRM
- Execution status: ACTIVE WITH OWNER AUTHORISATION; EXACT-STATUS OBSERVER PENDING
- Confidence: TO CONFIRM
- Basis: The selected HU tree was configured and created without a visible
  error. Rename, both Nash submissions, running targets, Viewer Save, and
  read-only output verification were observed. Strategy-export submission,
  non-zero `.zip`-named output creation, read-only metadata verification, and
  source-tab closure were also observed. Actual archive format is unverified.
  The Viewer-only close prompt, `Don't Save`
  result, and persistence of both output files were observed. A separate
  `2 bb` Preview of the pre-conversion HU revision directly showed the SB raise
  to `2.00 BB` with only the BB call. No SB completion branch was present. This
  confirms that revision's HU rule at `2 bb`. Run 16 loaded the exact current
  worktree candidate after hash verification and directly showed the same
  two-node Preview, revalidating its equal-`2 bb` behaviour.
  The five-player row order, manual stack entry, and `Alt+N` transition were
  observed. The HRC-tested pre-correction multiway candidate stopped with the
  exact `100000` Script Error. After Euan reported loading the corrected
  worktree candidate, HRC displayed its basename without `[Errors]`, produced a
  `1815589`-node estimate, and enabled Finish. The capture did not expose the
  full loaded path. Root and representative deeper Preview paths matched the
  current candidate manifest for this five-player setup. This is path-scoped
  evidence, not exhaustive validation of the tree.
  A separate supervised HU discovery joined the non-coordinate table-size
  bootstrap to two different-valid-value stack commits, visual derived-value
  checks, blank-row cancellation, and one rejected-input recovery. It did not
  establish machine-readable cell verification or standalone focus safety. A
  later disposable check reached Scripting and opened the unnamed script picker
  twice by keyboard from an observed visible focus rectangle. Run 16 then loaded
  the exact current HU candidate and observed its expected post-load state, but
  native focus, cross-session durability, and target-runner validation remain
  unproven. The same run visibly configured both required Nash states without
  submitting either. It invoked Cancel once from initial OK focus and used
  `Alt+F4` to close the grid probes without submission. Reset-cell highlighting
  became ambiguous when the mandatory origin-collapse step was omitted. Run 17
  then copied each retained Nash value exactly. With Reset Strategies checked,
  it verified the required raw pair `false,true`, then restored Strategies to
  `false`. One whole-grid copy did not produce the settings matrix, so future
  automation must validate each cell. The separate CI `10.0` edit was not copied
  after editing.
  Run 18 then confirmed a guarded native Finish action and exact successful
  hand-editor-tab set-delta detection without coordinates. It also proved that
  `Ctrl+W`, then `H` from an active hand can inherit state: this run retained its
  rows and script and produced a `From Hand` resource. The shortcut therefore
  cannot serve as the clean next-simulation route.
  Run 19 then reached Rename Hand, Viewer Save As, and Export Strategies without
  coordinates and cancelled all three without output. It confirmed their exact
  visible current states but also reproduced the provider's wrong focus and
  failed semantic actions. Static inspection identified a coordinate-free tab-
  close hypothesis, but its native-focus and Cancel-only live proof remain
  outstanding.
  Static Nash inspection proves that the CI Jobs are distinct and serialised,
  but also proves that successful and cancelled Jobs can both disappear into
  the same idle Progress state. Exact success is available only from in-process
  Eclipse Job events; no supported external hook was found.
  The `5 bb` boundary, the first supported stack above it, and dynamic post-fold
  behaviour remain unconfirmed. Same-hand queue serialisation is statically
  confirmed, but externally observable terminal success and several critical
  accessible targets remain unconfirmed.

## Next action

The repository now contains an offline-tested Java correlation core, Eclipse
Jobs adapter, bearer-token loopback transport, ordered runtime assembly,
disabled OSGi lifecycle owner, in-memory simpleconfigurator planner, isolated
Equinox start-level fixture, and source/test-only Windows bootstrap module with
an asynchronous in-memory publisher, a guarded existing-directory file seam,
an independent file reader, a protected app-local artefact-set primitive, and a
synthetic broker under `src/HrcJobObserver/`.
The current suites pass 30 core tests, 34 adapter tests, 25 transport tests,
10 joined-assembly tests, 14 lifecycle tests, 13 packaging tests, and 77
Windows bootstrap tests. The Windows total is 20 primitive tests, 8 descriptor
and protocol tests, 27 broker and in-memory-store tests, 11 filesystem tests,
5 single-file artefact-identity tests, and 6 protected app-local artefact-set
tests. The start-level fixture passes 12/12 prerequisite, 18/18 recorded-row,
and 9/9 observer-failure tests. The assembly
orders callbacks, checkpoints, and arms through the same mailbox worker and
uses a second post-arm marker to verify request ownership and start a fresh
observer-local lease. Every successfully confirmed exact idempotent retry
renews that lease. It emits `ARM_CONFIRMED` for each confirmed lease. The joined
harness exercises the real control through an actual loopback socket in one
JVM. The future controller must still enforce a local round-trip and pre-input
margin inside that lease before it can use an ARM response for HRC input.

The lifecycle tests synthetic manager registration, two bounded baseline scans,
startup callback admission, ordered health checks, rollback, and shutdown. Its
public no-argument activator remains disabled. The planner validates only
caller-supplied bytes and cannot install its proposal.

This is offline implementation evidence plus the read-only configuration and
producer-provenance facts above. The fixture supplies isolated public-Equinox
resolution, activation, listener, and Job delivery evidence with synthetic
Bundles. It adds no observation of HRC OSGi resolution, live HRC listener
registration, real HRC callback delivery, token publication, production
controller/observer IPC, or runtime terminal capture. The ordered barrier and
actionable-checkpoint contract are offline-tested but not HRC-runtime validated.
`Feasibility` remains `TO CONFIRM`.

The Windows module proves exact applied DACL read-back, both endpoint-side
process identity checks, bounded one-shot frame operations, synthetic distinct-
process fixed-frame exchange, and rejection of a wrong live child. It also
proves a canonical HMAC-bound descriptor, a capacity-one ABA-safe in-memory
publisher with store-affine coalesced removal, and a synthetic three-process
broker. The broker executes all four exchanges and caps publication by the
remaining absolute session deadline. It removes the exact publication before a
grant or revocation acknowledgement, rejects a completed malformed loser, and
wipes its retained token before the final or revocation acknowledgement.
Coalesced asynchronous disposal waits for non-abandonable cleanup. Faulted or
unknown removal cannot claim absence. Late verified removal and combined
protocol/cleanup failures remain terminal and observable.

The existing-directory file seam binds its expected owner to the current
process account SID and requires an exact current-account-plus-`SYSTEM` DACL.
It does not provide logon-SID isolation. Its retained handle rejects reparse
points, proves a local NTFS volume, and pins the namespace. The capacity-one
publisher reserves `endpoint-v1.bin` without replacement and never writes the
bearer token. It validates the canonical bytes, security, path, volume, and file
identity before and after retained-root native rename. Exact removal uses POSIX
handle deletion and bounded directory enumeration. The reader returns an
independent wipeable structural snapshot. Terminal removal uncertainty cannot
claim absence or permit store reuse. Cooperative checks do not hard-preempt
synchronous native calls.

The one-file artefact lease verifies a caller-supplied canonical DOS path, fixed
local drive and Mount Manager volume, retained read sharing, default-stream
length and SHA-256, single-link state, reparse ancestors and leaf, final path,
volume serial number, and 128-bit `FILE_ID`. Its revalidation is detection-only.
It does not bind mutable siblings.

The protected app-local set requires an exact current-account-plus-`SYSTEM`
DACL on one caller-supplied canonical DOS directory on local NTFS. It accepts 1
through 128 one-level exact-case printable ASCII names. Every entry must match
an expected default stream, including all intended PDB and
`.runtimeconfig.dev.json` files; any undeclared file or subdirectory fails. It
retains each member with its length, SHA-256, volume serial number, and 128-bit
`FILE_ID` under one absolute deadline. Its canonical digest binds the
designated executable and the ordinally sorted names, lengths, and SHA-256
values. Revalidation performs exact scans around all member revalidations.

The root still permits new child creation. This is a snapshot and detection
control only. A race remains between the last revalidation and a later loader
action. The primitive has no independently trusted release manifest that
authenticates the complete production artefact set and its canonical digest. It
does not bind shared .NET runtime selection or prove member file ACLs,
signatures, atomic launch, launched-process identity, production roles,
containment, private handoff, role-bound `READY`, Java integration, or HRC
runtime use.

Next, prove that dedicated roles enter kill-on-close Job Object containment
atomically at process creation. Keep this as a separate proof. The protected
application namespace and shared-runtime trust remain unresolved. Require an
independently trusted release manifest to authenticate each complete production
artefact set and its canonical digest. Complete those boundaries before private
initial name handoff and role-bound `READY`. Then add guarded Windows known-
folder resolution, protected LocalAppData hierarchy provisioning and provenance,
and stale or crash recovery around the existing-directory seam. Do not integrate
this seam with Java or open the standalone-runner gate until those boundaries
pass.

Before creating an installable Bundle, enforce the exact clean-launch
configuration, provider rows, provider hashes, Job-class hashes, and normal
start-level route recorded above. The static audit and isolated fixture support
listener publication before exact Job production on that route. They do not
prevent arbitrary `Bundle.loadClass`, reflection, or another activation route.
Do not use runtime stop, restart, republish, update, uninstall, or refresh as a
cleanup design. Keep the observer loaded until final framework shutdown, then
require ordered admission closure, listener removal, mailbox drainage,
transport shutdown, and control-call completion. Integrate same-user token and
endpoint publication with Java only after the production Windows prerequisites
above.
Then add a deterministic JAR, manifest, guarded install, and rollback design.
Before live use, extend the active-process runtime identity gate for every
added provider. The runtime observer must subscribe only to the Eclipse Jobs
lifecycle and must not read strategy or licence data.

Preserve both current unsaved tabs. Do not install the observer, restart HRC, or
consume the reserved smoke until Euan explicitly resolves the protected tabs.

The authorised design relies on the version-specific findings in this
document. Resolve the active HRC installation and rehash the exact
eight-component identity set above from its `plugins` directory before using
them. Before an install, verify the recorded baseline configuration hashes,
Bundle rows, provider JARs, and Job-class hashes. After a guarded install,
verify separately the deterministic target configuration hashes, exact inserted
observer row, every preserved baseline row, provider JARs, and Job-class hashes.
Stop on any process, path, filename, hash, row, target-level, or provenance
mismatch.

Begin the remaining non-writing probes with the statically defined exact-tab-
focus, selection-round-trip, one-shot `Ctrl+F4`, and `Save Resource` Cancel path
on active `*From Hand 7` only after native focus and active-editor identity can
be proved. Require both dirty tabs, selected tab, Progress, HRC bounds, and
files to remain unchanged. Stop without retry on any mismatch or unknown
outcome.

Integrate the confirmed per-cell Nash route with exact native-focus,
clipboard-transition, and CI `10.0` post-edit read-back assertions. Before any
further pre-runner OK action, use static, non-submitting, and prior demonstration
evidence to define machine-readable candidate detectors and stop rules for
accepted, rejected, queued, running, cancelled, completed, and failed states.
Treat any focus, transition, value, hash, reset-pair, or post-state mismatch as a
stop. Owner authorisation covers the required live submission, but the observer
and all pre-submit safety gates must be ready first.

Do not spend the authorised Nash submission merely watching Progress; that UI
cannot make terminal success durable. Resolve the technical architecture
through the owner-authorised in-process event observer. No read-only external
postcondition found so far distinguishes `OK` from `CANCEL` and `ERROR` for each
exact submission.

Do not submit the authorised strategy export until the standalone design can
read the exact two-filter list and selected ZIP value in the actual Save As.
Cancel does not reset the hidden state; the accepted staging export must supply
the proof and update. Stop on a ZIP-only dialog. Extension, metadata, and Job
success remain insufficient on their own.

Resolve every other known critical control before releasing the implementation
gate: machine-readable Rename value and post-state; Viewer Save destination,
  type, filename, extension, submission, and job result; every Export setting and
  read-back; identity-matched Viewer and Export terminals; an exclusive
  HRC-control lease; a validated exclusively owned staging namespace; and
fail-if-exists publication for both outputs; guarded `Don't Save`; active Home;
and the next-simulation transition. Use
non-writing, Cancel-only, prior demonstration, and static evidence wherever
possible. Do not risk the reserved smoke against a known blocker.

The current project goal authorises exactly one new-output, equal-`2 bb` HU
end-to-end smoke. Keep it reserved until those critical mappings are complete;
do not consume it during feasibility discovery. Only after the evidence supports
a positive feasibility verdict should the project-owned runner be implemented.
Use that runner for the single reserved smoke
`AUTO-HU-2-20260812-01`, after repeating the exact no-overwrite preflight under
`\\VAULT\sims\Preflop\HU`. Require a job-identity-matched accepted, queued,
running, or explicit successful-terminal state after each Nash submission while
preserving CI `10.0` before CI `1.0`. Queue Viewer Save immediately without
waiting for Nash completion. Wait for both Nash jobs and Viewer Save to finish
successfully before strategy export. Disappearance or idle alone is never
success. Verify the new non-empty `.hrcv` and `.zip`, then validate the exact
matching `Save Resource` prompt and `Don't Save`. Treat every pre-smoke
hand-editor tab as protected; immediately before close require exactly that set
plus the completed simulation, and afterwards require exactly the unchanged
protected set with no additions or replacements. Explicitly activate Home and
validate the next-simulation transition. Negative
states not encountered during the smoke remain
`TO CONFIRM`; any unrecognised state stops it. Verify the Save As destination,
Viewer type, filename, and extension on every save.

Complete the representative HU lifecycle before any further multiway discovery.
Do not add standalone runner source, application dependencies, or build
commands until feasibility has a supported verdict. The minimal, uninstalled
exact-status observer described above is the sole offline feasibility-instrument
exception.
