# Installation du mod OrbitDeck Bridge

**Prérequis** : Windows 10/11, KSP 2 Redux v0.2.8.5.103184-beta.

Pour afficher les données sur un Stream Deck, il faut aussi le plugin **OrbitDeck** (Elgato Marketplace). Le mod seul ne fait qu'exposer les données en local.

## Installer le mod

1. Téléchargez `OrbitDeck-Bridge-vX.Y.Z.zip` depuis la dernière [release](https://github.com/teddyzeus/orbitdeck-bridge/releases/latest).
2. Fermez le jeu.
3. Ouvrez le dossier d'installation de KSP 2 (Steam : clic droit sur le jeu → Gérer → Parcourir les fichiers locaux).
4. Dézippez l'archive **dans le dossier `mods`** du jeu (créez-le s'il n'existe pas). Vous obtenez :

```
<dossier du jeu>\mods\orbitdeck\
    OrbitDeck.Mod.dll
    swinfo.json
    orbitdeck-config.json
```

5. Lancez le jeu. Dans son journal (`%USERPROFILE%\AppData\LocalLow\Intercept Games\Kerbal Space Program 2\Player.log`), cherchez la ligne `[OrbitDeck] Serveur WebSocket à l'écoute sur ws://127.0.0.1:8765/`.

Le mod n'écoute que sur votre machine (`127.0.0.1`) : rien n'est exposé sur le réseau.

## Réglages (`orbitdeck-config.json`)

- `port` : port d'écoute (8765 par défaut). Indiquez le même port dans le plugin.
- `sampleRateHz` : fréquence d'envoi des états (10 par défaut).
- `verboseLogging` : `true` pour un journal détaillé en cas de problème.

Relancez le jeu après toute modification.

## Mise à jour

Fermez le jeu, remplacez le contenu de `mods\orbitdeck\` (gardez votre `orbitdeck-config.json`), relancez le jeu : l'ancienne DLL reste en mémoire tant que le jeu tourne.

## Si vous aviez StreamPlug (versions 0.1.x)

Le mod s'appelait StreamPlug. Supprimez le dossier `mods\streamplug\` avant d'installer OrbitDeck Bridge, sinon les deux se disputent le port 8765.

## Désinstallation

Supprimez le dossier `mods\orbitdeck\` du jeu.
