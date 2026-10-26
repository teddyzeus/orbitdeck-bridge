# Installing the OrbitDeck Bridge mod

The mod exposes the game's data locally (WebSocket on `127.0.0.1`, default port **18765**) and receives commands from the **OrbitDeck** Stream Deck plugin (Elgato Marketplace). The mod alone does nothing visible: install the plugin too.

Download the archive for your game from the latest [release](https://github.com/teddyzeus/orbitdeck-bridge/releases/latest):

| Game | Archive | Tested versions |
|------|---------|-----------------|
| Kerbal Space Program 2 (Redux) | `OrbitDeck-Bridge-KSP2-vX.Y.Z.zip` | KSP 2 Redux v0.2.8.5.103184-beta |
| Kerbal Space Program 1 | `OrbitDeck-Bridge-KSP1-vX.Y.Z.zip` | KSP 1.12.5 |

**Requirements:** Windows 10/11. Only one game at a time can run the mod (they use the same port).

## KSP 2 (Redux)

1. Close the game.
2. Open the KSP 2 installation folder (Steam: right-click the game → Manage → Browse local files).
3. Unzip the archive **into the game's `mods` folder** (create it if it does not exist). You get:

```
<KSP 2 folder>\mods\orbitdeck\
    OrbitDeck.Mod.dll
    swinfo.json
    orbitdeck-config.json
    LICENSE.txt
```

4. Start the game. In its log (`%USERPROFILE%\AppData\LocalLow\Intercept Games\Kerbal Space Program 2\Player.log`), look for the line `[OrbitDeck] Serveur WebSocket à l'écoute sur ws://127.0.0.1:18765/` (the mod's log messages are in French).

## KSP 1

1. Close the game.
2. Open the KSP 1 installation folder (Steam: right-click the game → Manage → Browse local files): the one that contains `GameData` and `KSP_x64.exe`.
3. Unzip the archive **into the root** of that folder: the archive's `GameData` folder merges with yours. You get:

```
<KSP 1 folder>\GameData\OrbitDeck\
    Plugins\
        OrbitDeck.Ksp1.dll
        Newtonsoft.Json.dll
        orbitdeck-config.json
    LICENSE.txt
    LICENSE-Newtonsoft.Json.txt
```

4. Start the game. In its log (`KSP.log`, at the root of the game folder), look for the line `[OrbitDeck] Serveur WebSocket à l'écoute sur ws://127.0.0.1:18765/`.

`Newtonsoft.Json.dll` ships with the mod: KSP 1 does not provide it.

The mod only listens on your machine (`127.0.0.1`): nothing is exposed on the network.

## Settings (`orbitdeck-config.json`)

The file is in the mod folder (`mods\orbitdeck\` for KSP 2, `GameData\OrbitDeck\Plugins\` for KSP 1).

- `port`: listening port (default 18765). Enter the same port in the plugin.
- `sampleRateHz`: how often the state is sent (default 10).
- `verboseLogging`: `true` for a detailed log when troubleshooting (values, received commands and their result).

Restart the game after any change.

## Updating

Close the game, replace the mod files (keep your `orbitdeck-config.json`), then start the game again: the old DLL stays in memory as long as the game is running.

## If you had StreamPlug (0.1.x versions)

The mod used to be called StreamPlug. Delete the `mods\streamplug\` folder before installing OrbitDeck Bridge, otherwise the two compete for the port.

## Troubleshooting

The mod's log messages are in French; the quoted messages below are what you will see in the log.

| Symptom | What to do |
|---------|------------|
| The log says "Impossible d'ouvrir le port 18765 : … accès … interdite" (cannot open the port, access forbidden) | Windows has reserved this port (Hyper-V, WSL, Docker: the ranges change after a restart or an update). Check with `netsh interface ipv4 show excludedportrange protocol=tcp`, then choose a port outside those ranges (for example 24765) in `orbitdeck-config.json` **and** in the plugin. |
| The log says the port is already in use | Another program, or the other game with the mod, is using the same port. Close it or change the port. |
| No `[OrbitDeck]` line in the log | The mod is not loaded: check the folder (`mods\orbitdeck\` for KSP 2, `GameData\OrbitDeck\Plugins\` for KSP 1) and its contents. |
| KSP 1: "Could not load file or assembly" in the log | The `Plugins` folder must contain the `Newtonsoft.Json.dll` shipped with the mod (the "net20" build): do not replace it with another one. |
| Keys show "NO VEHICLE" | Normal in menus, the space center and the editor: go into flight. |
| A key shows a red cross | The command was refused by the game (in EVA, during time warp on rails, vessel with no pilot or probe core…). The detailed log (`verboseLogging`) gives the reason. |

## Uninstalling

Delete the mod folder (`mods\orbitdeck\` for KSP 2, `GameData\OrbitDeck\` for KSP 1).
