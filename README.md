# OrbitDeck Bridge

Free, open-source mod (MIT license) for **Kerbal Space Program 1** and **Kerbal Space Program 2 (Redux)**. It exposes game data locally and receives commands over WebSocket on `127.0.0.1:18765`. It powers the **OrbitDeck** Stream Deck plugin, but the protocol is documented and open: you can write your own client.

> Status: version 0.5.0. **[Download the latest release](https://github.com/teddyzeus/orbitdeck-bridge/releases/latest)**, then follow [docs/installation.md](docs/installation.md).

## What the mod provides

- **Reading**: altitude (sea level and ground), speed (surface, orbit, target), throttle, SAS and its mode, RCS, lights, brakes, landing gear, vessel status.
- **Commands**: throttle (set, adjust), SAS and its modes (stability, prograde, retrograde, normal, anti-normal, radial, anti-radial, target, anti-target, maneuver), RCS, lights, brakes, landing gear, navball speed mode, next-stage activation.
- 100% local: the server only listens on `127.0.0.1`, checks `Host` and `Origin`, and limits message size and the number of clients.

## Documentation

- [Installation](docs/installation.md) (KSP 1 and KSP 2)
- [Protocol](protocol/README.md) (JSON schemas and examples)
- [Test checklist](docs/test-checklist.md)
- [Technical specifications](SPECS-TECHNIQUES.md) (KSP 2) and [KSP 1 feasibility study](docs/ksp1-faisabilite.md) (in French)

## Compatibility

| Game | Tested version | Mod folder |
|------|----------------|------------|
| KSP 2 (Redux) | v0.2.8.5.103184-beta | `mods\orbitdeck\` |
| KSP 1 | 1.12.5 | `GameData\OrbitDeck\` |

Windows 10/11.

## Development

- **KSP 2 mod**: `mod/src/OrbitDeck.Mod`; **KSP 1 mod**: `mod/src/OrbitDeck.Ksp1`. .NET 8 SDK, game DLLs referenced through `KSP2_DIR` / `KSP1_DIR` or `mod/Directory.Build.props.user`. The server, protocol and commands are shared: the KSP 1 project compiles the same files (the `Server`, `Protocol`, `Commands`, `Config` and `Telemetry` folders of `OrbitDeck.Mod`).
- **Tests**: `dotnet test mod/OrbitDeck.Mod.sln`.
- **Release**: `node scripts/package.mjs` produces `release/OrbitDeck-Bridge-KSP2-vX.Y.Z.zip` and `release/OrbitDeck-Bridge-KSP1-vX.Y.Z.zip`.
- **Tools**: `tools/` (command-line client and fake server, to try the protocol without the game).

## License

[MIT](LICENSE). The "OrbitDeck" name and the Stream Deck plugin are not covered by this license. The KSP 1 mod bundles Newtonsoft.Json (MIT license, see `LICENSE-Newtonsoft.Json.txt` in the archive).
