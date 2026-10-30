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
| `state` | `seq`, `status`, `gameState`, `vessel`, `altitude`, `speed`, `throttle`, `sas`, `rcs`, `lights`, `brakes`, `gear`, `stage`, `view`, `time`, `maneuver` | same |
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

`view`: `ship` (flight camera on the vessel) or `map` (orbit map). **Absent** with an older mod or when the game does not provide it (a client then treats it as unknown). The map is only reported while a vessel is active; in the menus and at the space centre the field is absent like the rest of the vessel data.

`time`: the game's time, **absent** with an older mod, when the game does not provide it, and without a vessel.
- `ut`: game time in seconds (it advances all the time and does **not** trigger a new `state`; a client estimates it between two states with `rate`, and stops when `paused`).
- `rate`: the time factor applied now (1 = normal); during a ramp-up it differs from `rates[index]`, the step the game asked for.
- `rates`, `index`: the steps of the **current mode**, from the slowest to the fastest, and the position of the requested step.
- `mode`: `rails` (high warp, the vessel is frozen on its orbit) or `physics` (low warp, full simulation).
- `paused`: the game is paused. `pausable`: the mod can pause it (absent = false).
- `maxIndex`: the highest step allowed now; absent = unknown. `limit`: why the next step is refused (`atmosphere`, `altitude`, `thrust`, `ground`, `proximity`, `other`); absent when nothing refuses it or the game does not say.
- `auto`: an automatic warp (to a given instant) is running. `day`: length of a game day in seconds; absent = unknown.

`maneuver`: the maneuver plan of the active vessel, **absent** with an older mod, when the game does not provide it, and without a vessel. `count: 0` (with `nodes: []`) means the game provides the plan and there is no node, which is different from "unknown".
- `nodes`: the planned nodes, nearest first, **eight at most** (`count` is the real number). A node is identified by its `ut` (game time of the node); there is no other identifier. `dv`: required delta-V (m/s). `burn`: estimated burn duration (s). `ignition`: game time at which the game recommends starting the burn; the mod decides the convention (half the burn before the node). Both are absent when unknown. `after`: orbit after the node.
- Orbits (`before`, `after`): `ap`, `pe` are altitudes above the body (m); `ap` is absent for an open orbit, with `escape: true`. `atmosphere`: height of the atmosphere of that body (absent = none or unknown), to tell a periapsis in the air.
- `dvAvailable`: the vessel's delta-V (m/s). `burning`: the first node's burn is under way (`remaining` m/s, `progress` 0 to 1); absent otherwise, and always absent when the game gives neither.

- `warp`: the "warp to the maneuver" operation, **absent** at rest. `state`: `running` (`stop` = the instant the game is asked to stop at), `arrived` (stopped at 1× on `stop`), `late` (the request was refused, the stop was already past), `interrupted` (`reason`: `manual` the player took over, `nodeGone` the node was removed or moved, `vessel` another vessel became active, `control` the vessel is no longer controllable). A final state stays published for 5 seconds.

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
| `setView` / `toggleView` | `view`: `ship` or `map` / — | Shows the vessel view or the map (the game's map key). Idempotent for `setView`. |
| `increaseWarp` / `decreaseWarp` | — | Moves to the next / previous time step of the current mode. From the pause, `increaseWarp` resumes at 1× then goes up one step; `decreaseWarp` does nothing at 1× or paused. **Not idempotent.** |
| `stopWarp` | — | Back to 1× at once: cancels an automatic warp and leaves the pause. Idempotent; the emergency stop. |
| `setWarpRate` | `rate`: number > 0 (the factor, for example `100`) | Selects the step whose factor is `rate` in the current mode; `REJECTED_BY_GAME` if there is none or the game refuses it. Leaves the pause. |
| `setPause` / `togglePause` | `paused`: boolean / — | Pauses or resumes the game. |
| `warpToManeuver` | `ut`: number (the node's instant), `margin`: number 0 to 3600 (seconds before the ignition), `maxRate`: number > 0, optional | Starts the game's automatic warp to `ignition − margin` of the node at `ut` (±1 s); without a known burn duration, to `ut − margin − 60`. The game slows down by itself on approach; the mod only watches it. Resumes from the pause. `REJECTED_BY_GAME` if the node does not exist, the stop instant has already passed (`maneuver.warp.state` becomes `late`) or the game refuses; `NOT_CONTROLLABLE` on an uncontrollable vessel. The game picks the step from the trip length (the warp lasts about ten real seconds); `maxRate` caps that step, which lengthens the trip. `stopWarp` cancels it. |
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
