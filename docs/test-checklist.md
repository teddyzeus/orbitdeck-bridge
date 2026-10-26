# OrbitDeck Bridge mod test checklist

Run it for each game (KSP 2 Redux, KSP 1) with `"verboseLogging": true`. Two ways to observe: the OrbitDeck plugin on the Stream Deck, or the command-line client (`node tools/client.mjs`, see [tools/README.md](../tools/README.md)) and the game log. The mod's log messages are in French.

## 1. Startup
- [ ] The log shows `[OrbitDeck] Mod chargé (…, port 18765, …)` then `Serveur WebSocket à l'écoute sur ws://127.0.0.1:18765/`, with no warning or error.
- [ ] In menus (main menu, space center, editor): `noVessel` (the plugin shows "NO VEHICLE" in its own language).
- [ ] A connecting client receives `hello` (mod and game versions) then states.

## 2. Reading in flight
- [ ] The vessel name is readable (translated, not `#autoLOC_…`).
- [ ] The status is `connected` when a pilot or probe is in control, `notControllable` otherwise.
- [ ] Sea altitude and ground altitude follow the game's altimeter while ascending, including over water.
- [ ] Surface, orbit and target speeds match the navball; with a target selected, the target speed changes.
- [ ] The speed mode follows the navball when it is changed with the mouse.
- [ ] Throttle, SAS (and its mode: stability, prograde, retrograde…) and RCS follow the game's keys.
- [ ] Vessel switching (`]` key), map view, return to the space center: values follow without errors.

## 3. Commands
- [ ] SAS: turn on and off from the Stream Deck; the game state follows (navball SAS light).
- [ ] SAS modes: prograde, retrograde, normal, anti-normal, radial, anti-radial (navball in orbit mode for the last four), target and anti-target (with a target), maneuver (with a node), stability. The active mode is shown in green; an unavailable mode is refused with the reason.
- [ ] Lights, brakes, landing gear: the game state follows in both directions (also when changed from the keyboard). Without a matching part, the key shows "—".
- [ ] Stage: one command fires one stage (including pre-launch on KSP 2); refused in EVA, on rails, or when stages are locked (Shift + L on KSP 1).
- [ ] RCS: same.
- [ ] Throttle: set to 0, 50 and 100%; the game's gauge follows and the vessel accelerates accordingly.
- [ ] Throttle: +10% and −10% in bursts, bounded between 0 and 100%.
- [ ] Speed mode: surface, orbit, target; the navball follows; "target" without a target is refused.
- [ ] An out-of-range value (for example a throttle of 5) is refused (`BAD_REQUEST`), never silently clamped.

## 4. Refused cases (the Stream Deck shows a red cross, the game is unaffected)
- [ ] At the space center or in a menu: `NO_VESSEL`.
- [ ] Kerbal in EVA: throttle, SAS and RCS refused.
- [ ] Time warp on rails: throttle refused.
- [ ] Vessel with no pilot or probe core: `NOT_CONTROLLABLE`.
- [ ] SAS on a vessel without a suitable module: refused with a message.

## 5. Robustness
- [ ] Two clients at the same time: both receive the states.
- [ ] Closing and restarting the client, the plugin or the game: the connection recovers by itself.
- [ ] Port reserved by Windows: the log explains the cause and what to do.
- [ ] No mod exception in the log during the whole session.
