# StreamPlug

Affichez et pilotez **Kerbal Space Program 2 (KSP 2 Redux)** depuis un **Stream Deck 15 touches**.

> Statut : en conception (étape T0). Rien n'est encore utilisable.

## Fonctionnalités prévues (MVP)

- Lecture : altitude (mer ↔ surface), vitesse (surface / orbitale / cible).
- Commandes : throttle (0 / 50 / 100 %, ±10 %), SAS, RCS.

## Architecture

Deux composants, 100 % locaux :

1. **Mod KSP 2** (`mod/`, C#) : lit l'état du jeu et héberge un serveur WebSocket sur `127.0.0.1:8765`.
2. **Plugin Stream Deck** (`streamdeck-plugin/`, TypeScript) : se connecte au mod, affiche les valeurs et envoie les commandes.

Le protocole d'échange est décrit dans `protocol/`.

## Documentation

- [Spécifications fonctionnelles](SPECS.md)
- [Spécifications techniques](SPECS-TECHNIQUES.md)

## Compatibilité

KSP 2 Redux v0.2.8.5.103184-beta, Windows 10/11.

## Distribution

GitHub Releases uniquement.
