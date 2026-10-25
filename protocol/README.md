# Protocole StreamPlug v1

Échanges entre le **mod KSP 2** (serveur) et le **plugin Stream Deck** (client).
Référence : [SPECS-TECHNIQUES.md](../SPECS-TECHNIQUES.md) §6.

## Transport
- WebSocket (RFC 6455), un message JSON par frame **texte**, UTF-8.
- Serveur : `ws://127.0.0.1:8765/` (port configurable dans `streamplug-config.json`).
- Le serveur n'écoute que sur `127.0.0.1`. Il refuse toute requête portant un en-tête `Origin` (navigateurs) et tout en-tête `Host` autre que `127.0.0.1:<port>` ou `localhost:<port>`.
- Taille maximale d'un message : **4096 octets** (au-delà, fermeture avec le code 1009).
- Maximum **8 clients** simultanés.

## Règles générales
- Chaque message est un objet JSON avec `v` (entier, **1**) et `type` (chaîne).
- Unités SI : mètres, mètres par seconde, poussée de 0 à 1. Le formatage est fait côté client.
- Un champ inconnu est ignoré (compatibilité ascendante).
- `seq` des messages `state` est croissant : le client ignore tout `state` dont le `seq` est **inférieur** au dernier reçu.
- Le serveur envoie `hello` puis un `state` complet à chaque nouvelle connexion.
- Le serveur renvoie le `state` courant au plus **10 fois par seconde** quand il change, et au moins **une fois par seconde** (battement de cœur) même sans changement.

## Messages serveur → client

| `type` | Contenu | Schéma |
|--------|---------|--------|
| `hello` | `mod` (version du mod), `game` (version du jeu) | [server-message.schema.json](schema/server-message.schema.json) |
| `state` | `seq`, `status`, `gameState`, `vessel`, `altitude`, `speed`, `throttle`, `sas`, `rcs` | idem |
| `ack` | `id` de la commande acceptée | idem |
| `error` | `id` (optionnel), `code`, `message` | idem |
| `pong` | — | idem |

`status` : `connected`, `noVessel` ou `notControllable`.
`gameState` : état global du jeu (`FlightView`, `MainMenu`, `KerbalSpaceCenter`, `Loading`…), informatif.
`speed.mode` : `surface`, `orbit` ou `target` ; `speed.target` vaut `null` sans cible.

Codes d'erreur : `BAD_REQUEST`, `UNKNOWN_COMMAND`, `NO_VESSEL`, `NOT_CONTROLLABLE`, `REJECTED_BY_GAME`, `INTERNAL`.

## Messages client → serveur

| `type` | Contenu | Schéma |
|--------|---------|--------|
| `command` | `id` (chaîne unique), `name`, `args` | [client-message.schema.json](schema/client-message.schema.json) |
| `ping` | — (réponse : `pong`) | idem |

Commandes (étape T4) : `setThrottle {value}`, `adjustThrottle {delta}`, `setSas {enabled}`, `toggleSas`, `setRcs {enabled}`, `toggleRcs`, `setSpeedMode {mode}`.
Tant que T4 n'est pas livrée, toute commande reçoit une erreur `UNKNOWN_COMMAND`.

## Exemples
Dans [examples/](examples/). Ils sont vérifiés par les tests du mod contre les schémas.
