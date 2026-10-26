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

5. Lancez le jeu. Dans son journal (`%USERPROFILE%\AppData\LocalLow\Intercept Games\Kerbal Space Program 2\Player.log`), cherchez la ligne `[OrbitDeck] Serveur WebSocket à l'écoute sur ws://127.0.0.1:18765/`.

Le mod n'écoute que sur votre machine (`127.0.0.1`) : rien n'est exposé sur le réseau.

## Réglages (`orbitdeck-config.json`)

- `port` : port d'écoute (18765 par défaut). Indiquez le même port dans le plugin.
- `sampleRateHz` : fréquence d'envoi des états (10 par défaut).
- `verboseLogging` : `true` pour un journal détaillé en cas de problème.

Relancez le jeu après toute modification.

## Mise à jour

Fermez le jeu, remplacez le contenu de `mods\orbitdeck\` (gardez votre `orbitdeck-config.json`), relancez le jeu : l'ancienne DLL reste en mémoire tant que le jeu tourne.

## Si vous aviez StreamPlug (versions 0.1.x)

Le mod s'appelait StreamPlug. Supprimez le dossier `mods\streamplug\` avant d'installer OrbitDeck Bridge, sinon les deux se disputent le port 18765.

## Dépannage

| Symptôme | Piste |
|----------|-------|
| Le journal dit « Impossible d'ouvrir le port 18765 : … accès … interdite » | Windows a réservé ce port (Hyper-V, WSL, Docker : les plages changent après un redémarrage ou une mise à jour). Vérifiez avec `netsh interface ipv4 show excludedportrange protocol=tcp`, puis choisissez un port hors de ces plages (par exemple 24765) dans `orbitdeck-config.json` **et** dans le plugin. |
| Le journal dit que le port est déjà utilisé | Un autre programme, ou un autre jeu avec le mod, utilise le même port. Fermez-le ou changez le port. |
| Pas de ligne `[OrbitDeck]` dans le journal | Le mod n'est pas chargé : vérifiez que le dossier est bien `mods\orbitdeck\` (KSP 2) et qu'il contient les trois fichiers. |

## Désinstallation

Supprimez le dossier `mods\orbitdeck\` du jeu.
