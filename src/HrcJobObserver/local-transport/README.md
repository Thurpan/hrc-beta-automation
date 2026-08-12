# Offline local observer transport

## Status

This directory contains a package-private Java 17 feasibility transport.
`LocalObserverServer` implements protocol version `1` over IPv4 loopback
`127.0.0.1` and an operating-system-assigned port. It is not an OSGi bundle,
installer, runtime entry point, standalone runner, or HRC integration.

The transport has never been loaded into or run with HRC. Its tests use a
synthetic `ObserverTransportControl` and local sockets in one JVM. They add no
running-HRC, Eclipse-callback, user-interface, active-process, or cross-process
evidence. Feasibility remains `TO CONFIRM`.

## Authentication and reachability

The caller supplies exactly 32 token bytes; the server rejects the all-zero
value. The server defensively copies the token, compares a Base64URL-decoded
`HELLO` value with `MessageDigest.isEqual`, and wipes its private copy during
terminal shutdown.
It does not generate the token, provision it to a controller, protect endpoint
metadata, or wipe the caller-owned copy.

The token is a bearer credential. It and all protocol data travel as
unencrypted plaintext over IPv4 loopback. Base64URL is an encoding, not
encryption. Loopback restricts network reachability but does not establish
process identity, same-user access, client ownership, confidentiality, or the
required system-wide HRC-control lease.

## Framing and commands

The server services one active client sequentially. Request frames contain
printable ASCII plus tab, use LF or CRLF, and are limited to 8 KiB. Responses
are limited to 256 KiB. The caller supplies a socket timeout from 1 through
60,000 milliseconds; an arm timeout is from 1 through 300,000 milliseconds.

Protocol operations are:

- `HELLO` authenticates protocol version `1`;
- `PING` checks the observer session;
- `CHECKPOINT` requests replay after a non-negative sequence cursor;
- `ARM` carries a request UUID, `NASH`, `VIEWER_SAVE`, or `EXPORT`, a
  Base64URL-encoded expected Job name, and timeout milliseconds; and
- `BYE` closes the current client conversation.

A routine client disconnect or socket timeout can permit a later connection
to the same running server. Authentication, protocol, session, checkpoint,
control, serialisation, internal, and shutdown failures latch the first
`TransportFailure`. A protocol failure terminates that server instance. The
wire protocol does not return the latched failure reason to the remote client.

## Checkpoints and controller fence

`ObserverCheckpoint` binds the requested `afterSequence` cursor to one session,
a positive opaque `barrierId`, replay disposition and bounds, observer fault
state, callback health and failure, and at most 256 events. Construction rejects
incoherent replay bounds, non-contiguous `OK` replay, events from another
session, non-empty failed replay, rejected events without a faulted observer,
and inconsistent fault events.

Its `actionable` field is true only for `OK` replay with no observer fault,
`HEALTHY` callback state, and no callback failure. This predicate is not yet a
live consumer fence. `ObserverTransportControl` has no runtime implementation,
and the adapter has no non-destructive mailbox barrier that can produce the
required atomic checkpoint.

A future controller must stop on `GAP`, `CURSOR_AHEAD`, session change,
non-actionable checkpoint, rejected event, observer fault, callback failure,
transport failure, or lost continuity. It must never reset a cursor or adopt a
new session automatically.

## ARM and response loss

The transport validates the expected Job-name policy before calling
`armIfHealthy`. The offline fake-control harness returns `ACCEPTED` for a new
request and `IDEMPOTENT` for an exact repeat. The core separately implements
request-ID idempotency.

Loss of an `ARM` response does not prove that the arm failed. Reconciliation
must reconnect to the same session and repeat the same request UUID and
identical intent. A new request UUID could create another intent; reuse with a
different intent faults the core.

## Time, durability, and data

`eventUtc` is diagnostic wall-clock data and can jump. `monotonicNanos` and
`deadlineNanos` are opaque values from the observer JVM. A controller must not
compare them with its own clock.

Session UUIDs, sequences, replay events, request state, and token state are in
memory only. Reconnect replay applies only to the same running observer
session. Observer or controller restart provides no durability or takeover
guarantee.

The allow-list serialises protocol/session metadata, replay bounds and health,
request and operation identity, numeric Job IDs, bounded public Job
descriptors, user/system flags, terminal status primitives, and enumerated
faults. It never serialises raw Eclipse `Job` objects, status messages,
exception objects or text, stack traces, strategies, licence material, or
unrelated HRC state. Public Job names can contain simulation or staging
filenames; they remain sensitive local plaintext even after Base64URL encoding.

## Offline validation

Run:

```powershell
& .\src\HrcJobObserver\local-transport\build.ps1
```

The build first runs the 27 core tests. It compiles transport main and test
outputs separately with Java 17, `-proc:none`, `-Xlint:all`, and `-Werror`, then
runs 23 transport tests. A targeted source/output boundary scan rejects Eclipse
imports, listener-registration and activator symbols, selected file-I/O APIs,
and named packaging artefacts.

The tests cover endpoint and checkpoint invariants; bearer authentication;
LF/CRLF and malformed framing; input and output bounds; cursor-contiguous replay
and selected non-actionable states; control/session/checkpoint failures; all
three operation types and repeated request identity; reconnect and cursor
forwarding; all seven event projections and JSON escaping; client sequencing;
admitted-arm shutdown ordering; and defensive token copying.

Still unvalidated: a real `ObserverTransportControl`, an atomic mailbox/core
checkpoint barrier, bounded runtime control calls, secure token generation,
same-user token and endpoint provisioning, controller ownership and takeover,
cross-process IPC, OSGi assembly, listener registration, active-process identity
checks, packaging, startup, rollback, safe unload, real HRC callbacks and
runtime results, and standalone-runner integration.
