# Installation du mod OrbitDeck Bridge

Le mod expose en local les données du jeu (WebSocket sur `127.0.0.1`, port **18765** par défaut) et reçoit les commandes du plugin Stream Deck **OrbitDeck** (Elgato Marketplace). Le mod seul ne fait rien de visible : installez aussi le plugin.

Téléchargez l'archive de votre jeu dans la dernière [release](https://github.com/teddyzeus/orbitdeck-bridge/releases/latest) :

| Jeu | Archive | Versions testées |
|-----|---------|------------------|
| Kerbal Space Program 2 (Redux) | `OrbitDeck-Bridge-KSP2-vX.Y.Z.zip` | KSP 2 Redux v0.2.8.5.103184-beta |
| Kerbal Space Program 1 | `OrbitDeck-Bridge-KSP1-vX.Y.Z.zip` | KSP 1.12.5 |

**Prérequis** : Windows 10/11. Un seul jeu à la fois avec le mod (ils utilisent le même port).

## KSP 2 (Redux)

1. Fermez le jeu.
2. Ouvrez le dossier d'installation de KSP 2 (Steam : clic droit sur le jeu → Gérer → Parcourir les fichiers locaux).
3. Dézippez l'archive **dans le dossier `mods`** du jeu (créez-le s'il n'existe pas). Vous obtenez :

```
<dossier de KSP 2>\mods\orbitdeck\
    OrbitDeck.Mod.dll
    swinfo.json
    orbitdeck-config.json
    LICENSE.txt
```

4. Lancez le jeu. Dans son journal (`%USERPROFILE%\AppData\LocalLow\Intercept Games\Kerbal Space Program 2\Player.log`), cherchez la ligne `[OrbitDeck] Serveur WebSocket à l'écoute sur ws://127.0.0.1:18765/`.

## KSP 1

1. Fermez le jeu.
2. Ouvrez le dossier d'installation de KSP 1 (Steam : clic droit sur le jeu → Gérer → Parcourir les fichiers locaux) : celui qui contient `GameData` et `KSP_x64.exe`.
3. Dézippez l'archive **à la racine** de ce dossier : le dossier `GameData` de l'archive se fusionne avec le vôtre. Vous obtenez :

```
<dossier de KSP 1>\GameData\OrbitDeck\
    Plugins\
        OrbitDeck.Ksp1.dll
        Newtonsoft.Json.dll
        orbitdeck-config.json
    LICENSE.txt
    LICENSE-Newtonsoft.Json.txt
```

4. Lancez le jeu. Dans son journal (`KSP.log`, à la racine du dossier du jeu), cherchez la ligne `[OrbitDeck] Serveur WebSocket à l'écoute sur ws://127.0.0.1:18765/`.

`Newtonsoft.Json.dll` est livrée avec le mod : KSP 1 ne la fournit pas.

Le mod n'écoute que sur votre machine (`127.0.0.1`) : rien n'est exposé sur le réseau.

## Réglages (`orbitdeck-config.json`)

Le fichier est dans le dossier du mod (`mods\orbitdeck\` pour KSP 2, `GameData\OrbitDeck\Plugins\` pour KSP 1).

- `port` : port d'écoute (18765 par défaut). Indiquez le même port dans le plugin.
- `sampleRateHz` : fréquence d'envoi des états (10 par défaut).
- `verboseLogging` : `true` pour un journal détaillé en cas de problème (valeurs, commandes reçues et leur résultat).

Relancez le jeu après toute modification.

## Mise à jour

Fermez le jeu, remplacez les fichiers du mod (gardez votre `orbitdeck-config.json`), relancez le jeu : l'ancienne DLL reste en mémoire tant que le jeu tourne.

## Si vous aviez StreamPlug (versions 0.1.x)

Le mod s'appelait StreamPlug. Supprimez le dossier `mods\streamplug\` avant d'installer OrbitDeck Bridge, sinon les deux se disputent le port.

## Dépannage

| Symptôme | Piste |
|----------|-------|
| Le journal dit « Impossible d'ouvrir le port 18765 : … accès … interdite » | Windows a réservé ce port (Hyper-V, WSL, Docker : les plages changent après un redémarrage ou une mise à jour). Vérifiez avec `netsh interface ipv4 show excludedportrange protocol=tcp`, puis choisissez un port hors de ces plages (par exemple 24765) dans `orbitdeck-config.json` **et** dans le plugin. |
| Le journal dit que le port est déjà utilisé | Un autre programme, ou l'autre jeu avec le mod, utilise le même port. Fermez-le ou changez le port. |
| Pas de ligne `[OrbitDeck]` dans le journal | Le mod n'est pas chargé : vérifiez le dossier (`mods\orbitdeck\` pour KSP 2, `GameData\OrbitDeck\Plugins\` pour KSP 1) et son contenu. |
| KSP 1 : « Could not load file or assembly » dans le journal | Le dossier `Plugins` doit contenir `Newtonsoft.Json.dll` fournie avec le mod (version « net20 ») : ne la remplacez pas par une autre. |
| Les touches indiquent « AUCUN VÉHICULE » | Normal dans les menus, le centre spatial et l'éditeur : entrez en vol. |
| Une touche affiche une croix rouge | La commande est refusée par le jeu (en EVA, en accélération du temps sur rails, véhicule sans pilote…). Le journal détaillé (`verboseLogging`) donne la raison. |

## Désinstallation

Supprimez le dossier du mod (`mods\orbitdeck\` pour KSP 2, `GameData\OrbitDeck\` pour KSP 1).
