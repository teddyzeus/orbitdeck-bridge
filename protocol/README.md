# Protocole OrbitDeck v1

Échanges entre le **mod KSP 2** (serveur) et le **plugin Stream Deck** (client).
Référence : [SPECS-TECHNIQUES.md](../SPECS-TECHNIQUES.md) §6.

## Transport
- WebSocket (RFC 6455), un message JSON par frame **texte**, UTF-8.
- Serveur : `ws://127.0.0.1:18765/` (port configurable dans `orbitdeck-config.json`).
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
| `state` | `seq`, `status`, `gameState`, `vessel`, `altitude`, `speed`, `throttle`, `sas`, `rcs`, `lights`, `brakes`, `gear` | idem |
| `ack` | `id` de la commande acceptée | idem |
| `error` | `id` (optionnel), `code`, `message` | idem |
| `pong` | — | idem |

`status` : `connected`, `noVessel` ou `notControllable`.
`gameState` : état global du jeu (`FlightView`, `MainMenu`, `KerbalSpaceCenter`, `Loading`…), informatif.
`speed.mode` : `surface`, `orbit` ou `target` ; `speed.target` vaut `null` sans cible.
`sas.mode` : mode courant du SAS, parmi `StabilityAssist`, `Prograde`, `Retrograde`, `Normal`, `Antinormal`, `RadialIn`, `RadialOut`, `Target`, `AntiTarget`, `Maneuver` (la casse peut varier d'un jeu à l'autre : comparez sans tenir compte de la casse).
`lights`, `brakes`, `gear` : objets `{ "enabled": booléen }`, **absents** quand le jeu ne fournit pas l'information (un client doit les traiter comme facultatifs).

Un mode du SAS indisponible (cible, nœud de manœuvre ou navball en mode orbite requis) est refusé avec `REJECTED_BY_GAME`.

Codes d'erreur : `BAD_REQUEST`, `UNKNOWN_COMMAND`, `NO_VESSEL`, `NOT_CONTROLLABLE`, `REJECTED_BY_GAME`, `INTERNAL`.

## Messages client → serveur

| `type` | Contenu | Schéma |
|--------|---------|--------|
| `command` | `id` (chaîne unique), `name`, `args` | [client-message.schema.json](schema/client-message.schema.json) |
| `ping` | — (réponse : `pong`) | idem |

### Commandes

| `name` | `args` | Effet |
|--------|--------|-------|
| `setThrottle` | `value` : nombre de 0 à 1 | Règle la poussée |
| `adjustThrottle` | `delta` : nombre de -1 à 1 | Modifie la poussée (le résultat reste entre 0 et 1) |
| `setSas` | `enabled` : booléen | Active ou coupe le SAS |
| `toggleSas` | — | Inverse le SAS |
| `setRcs` | `enabled` : booléen | Active ou coupe le RCS |
| `toggleRcs` | — | Inverse le RCS |
| `setSpeedMode` | `mode` : `surface`, `orbit` ou `target` | Change le type de vitesse affiché par le jeu |
| `setSasMode` | `mode` : `stabilityAssist`, `prograde`, `retrograde`, `normal`, `antinormal`, `radialIn`, `radialOut`, `target`, `antiTarget` ou `maneuver` | Choisit le mode du SAS et l'allume au besoin |
| `setLights` / `toggleLights` | `enabled` : booléen / — | Allume ou éteint les phares |
| `setBrakes` / `toggleBrakes` | `enabled` : booléen / — | Actionne ou relâche les freins |
| `setGear` / `toggleGear` | `enabled` : booléen / — | Sort ou rentre le train d'atterrissage |

Règles :
- Chaque commande reçoit **une réponse** : `ack` (même `id`) si le jeu l'a exécutée, sinon `error` (même `id`).
- La réponse arrive après l'exécution, qui a lieu sur la prochaine image du jeu. L'état résultant suit dans un message `state`.
- Les valeurs **hors bornes sont refusées** (`BAD_REQUEST`), jamais corrigées : `setThrottle` avec `5` ne met pas la poussée à 100 %.
- `set*` est **idempotent** : demander un état déjà actif renvoie `ack` sans rien changer. Préférez `set*` à `toggle*`.
- Erreurs d'exécution : `NO_VESSEL` (menu, centre spatial…), `NOT_CONTROLLABLE` (pas de pilote, contrôle insuffisant),
  `REJECTED_BY_GAME` (poussée verrouillée pendant l'accélération du temps ou en EVA, `target` sans cible, RCS indisponible…),
  `INTERNAL` (file de commandes pleine, erreur inattendue).
- La file de commandes du jeu est bornée (64) : au-delà, `INTERNAL` est renvoyé immédiatement.

## Exemples
Dans [examples/](examples/). Ils sont vérifiés par les tests du mod contre les schémas.
