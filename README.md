# StreamPlug

Affichez et pilotez **Kerbal Space Program 2 (KSP 2 Redux)** depuis un **Stream Deck 15 touches**.

> Statut : version 0.1.0 (MVP). **[Télécharger la dernière version](https://github.com/teddyzeus/StreamPlug/releases/latest)**, puis suivre [docs/installation.md](docs/installation.md).

## Fonctionnalités (MVP)

- **Lecture** : altitude (sol par défaut, appui = mer ↔ sol), vitesse (type du navball : sol, orbite, cible ; appui = type suivant), poussée, état de la connexion.
- **Commandes** : SAS et RCS (appui = activer/couper), poussée (fixer à 0 / 25 / 50 / 75 / 100 %, ±5 / 10 / 25 %).
- Reconnexion automatique, touches grisées quand le jeu n'est pas lancé, croix rouge si une commande est refusée.
- Interface en français ou en anglais selon le logiciel Stream Deck.

## Architecture

Deux composants, 100 % locaux :

1. **Mod KSP 2** (`mod/`, C#) : lit l'état du jeu et héberge un serveur WebSocket sur `127.0.0.1:8765`.
2. **Plugin Stream Deck** (`streamdeck-plugin/`, TypeScript) : se connecte au mod, affiche les valeurs et envoie les commandes.

Le protocole d'échange est décrit dans `protocol/`.

## Documentation

- [Installation](docs/installation.md)
- [Recette de test](docs/test-checklist.md)
- [Protocole](protocol/README.md)
- [Spécifications fonctionnelles](SPECS.md)
- [Spécifications techniques](SPECS-TECHNIQUES.md)

## Compatibilité

KSP 2 Redux v0.2.8.5.103184-beta, Windows 10/11.

## Distribution

GitHub Releases uniquement : le mod (`.zip`) et le plugin (`.streamDeckPlugin`) sont joints à chaque version.

## Développement

- **Mod** : `mod/` (SDK .NET 8, DLL du jeu référencées via `KSP2_DIR` ou `mod/Directory.Build.props.user`), tests : `dotnet test mod/StreamPlug.Mod.sln`.
- **Plugin** : `streamdeck-plugin/` (Node 24) : `npm ci`, `npm test`, `npm run build`, `npm run gallery` pour prévisualiser les touches.
- **Release** : `node scripts/package.mjs` produit `release/StreamPlug-Mod-vX.Y.Z.zip` et le `.streamDeckPlugin`. Les versions de `mod/package/swinfo.json` et du manifeste du plugin doivent concorder.
- **Outils** : `tools/` (client et faux serveur en ligne de commande).
