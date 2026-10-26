# OrbitDeck protocol v1

Exchanges between the **game mod** (server, KSP 1 or KSP 2) and the **Stream Deck plugin** (client).
Reference (in French): [SPECS-TECHNIQUES.md](../SPECS-TECHNIQUES.md) §6.

## Transport
- WebSocket (RFC 6455), one JSON message per **text** frame, UTF-8.
- Server: `ws://127.0.0.1:18765/` (port configurable in `orbitdeck-config.json`).
- The server only listens on `127.0.0.1`. It rejects any request carrying an `Origin` header (browsers) and any `Host` header other than `127.0.0.1:<port>` or `localhost:<port>`.
- Maximum message size: **4096 bytes** (larger messages close the connection with code 1009).
- Maximum **8 simultaneous clients**.

## General rules
- Every message is a JSON object with `v` (integer, **1**) and `type` (string).
- SI units: meters, meters per second, throttle from 0 to 1. Formatting is done on the client side.
- Unknown fields are ignored (forward compatibility).
- `seq` in `state` messages is increasing: the client ignores any `state` whose `seq` is **lower** than the last one received.
- The server sends `hello`, then a full `state`, on every new connection.
- The server resends the current `state` at most **10 times per second** when it changes, and at least **once per second** (heartbeat) even without changes.

## Server → client messages

| `type` | Content | Schema |
|--------|---------|--------|
| `hello` | `mod` (mod version), `game` (game version) | [server-message.schema.json](schema/server-message.schema.json) |
| `state` | `seq`, `status`, `gameState`, `vessel`, `altitude`, `speed`, `throttle`, `sas`, `rcs`, `lights`, `brakes`, `gear`, `stage` | same |
| `ack` | `id` of the accepted command | same |
| `error` | `id` (optional), `code`, `message` | same |
| `pong` | — | same |

`status`: `connected`, `noVessel` or `notControllable`.
`gameState`: global game state (`FlightView`, `MainMenu`, `KerbalSpaceCenter`, `Loading`…), informational.
`speed.mode`: `surface`, `orbit` or `target`; `speed.target` is `null` without a target.
`sas.mode`: current SAS mode, one of `StabilityAssist`, `Prograde`, `Retrograde`, `Normal`, `Antinormal`, `RadialIn`, `RadialOut`, `Target`, `AntiTarget`, `Maneuver` (casing may differ between games: compare case-insensitively).

`sas.available`: the SAS modes that can be selected right now, with the same names as the `setSasMode` command (`stabilityAssist`, `prograde`, …). A mode missing from the list is greyed in the game (no target selected, no maneuver node, navball not in orbit mode, SAS unusable on this vessel…) and `setSasMode` would be refused. An empty list means the SAS cannot be used at all. The field is **absent** with an older mod: a client then assumes every mode is selectable.
`lights`, `brakes`, `gear`: objects `{ "enabled": boolean }`, **absent** when the game does not provide the information (a client must treat them as optional).

`stage`: object `{ "count": integer, "exhausted": boolean }`, the number shown on the game's stage button and whether there is nothing left to fire (the button is greyed). `exhausted` is decided by the mod because the threshold depends on the game (KSP 2: one stage left; KSP 1: stage 0 reached) and may be absent: a client then treats `count <= 1` as exhausted. The whole object is **absent** when the vessel has no readable staging stack.

An unavailable SAS mode (target, maneuver node or orbit-mode navball required) is refused with `REJECTED_BY_GAME`.

Error codes: `BAD_REQUEST`, `UNKNOWN_COMMAND`, `NO_VESSEL`, `NOT_CONTROLLABLE`, `REJECTED_BY_GAME`, `INTERNAL`.

## Client → server messages

| `type` | Content | Schema |
|--------|---------|--------|
| `command` | `id` (unique string), `name`, `args` | [client-message.schema.json](schema/client-message.schema.json) |
| `ping` | — (reply: `pong`) | same |

### Commands

| `name` | `args` | Effect |
|--------|--------|--------|
| `setThrottle` | `value`: number from 0 to 1 | Sets the throttle |
| `adjustThrottle` | `delta`: number from -1 to 1 | Changes the throttle (the result stays between 0 and 1) |
| `setSas` | `enabled`: boolean | Turns the SAS on or off |
| `toggleSas` | — | Toggles the SAS |
| `setRcs` | `enabled`: boolean | Turns the RCS on or off |
| `toggleRcs` | — | Toggles the RCS |
| `setSpeedMode` | `mode`: `surface`, `orbit` or `target` | Changes the speed type shown by the game |
| `setSasMode` | `mode`: `stabilityAssist`, `prograde`, `retrograde`, `normal`, `antinormal`, `radialIn`, `radialOut`, `target`, `antiTarget` or `maneuver` | Selects the SAS mode and turns the SAS on if needed |
| `setLights` / `toggleLights` | `enabled`: boolean / — | Turns the lights on or off |
| `setBrakes` / `toggleBrakes` | `enabled`: boolean / — | Applies or releases the brakes |
| `setGear` / `toggleGear` | `enabled`: boolean / — | Deploys or retracts the landing gear |
| `activateStage` | — | Fires the next stage (space bar). **Not idempotent and irreversible**: every accepted command fires one stage; a client should ask the user for confirmation. |

Rules:
- Every command gets **one reply**: `ack` (same `id`) if the game executed it, otherwise `error` (same `id`).
- The reply arrives after execution, which happens on the next game frame. The resulting state follows in a `state` message.
- **Out-of-range values are refused** (`BAD_REQUEST`), never clamped: `setThrottle` with `5` does not set the throttle to 100%.
- `set*` is **idempotent**: requesting a state that is already active returns `ack` without changing anything. Prefer `set*` over `toggle*`.
- Execution errors: `NO_VESSEL` (menu, space center…), `NOT_CONTROLLABLE` (no pilot, insufficient control),
  `REJECTED_BY_GAME` (throttle locked during time warp or in EVA, `target` without a target, RCS unavailable…),
  `INTERNAL` (command queue full, unexpected error).
- The game's command queue is bounded (64): beyond that, `INTERNAL` is returned immediately.

## Examples
In [examples/](examples/). They are checked against the schemas by the mod's tests.
