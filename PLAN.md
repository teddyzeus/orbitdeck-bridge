# StreamPlug — Plan d'exécution

> Référence : [SPECS.md](SPECS.md) (fonctionnel) et [SPECS-TECHNIQUES.md](SPECS-TECHNIQUES.md) §12 (étapes T0 à T7).
> Taille : **S** (< 1 séance), **M** (1 à 2 séances), **L** (plusieurs séances). Une séance = une session de travail.
> Légende : ✅ fait · ⏳ en cours · ☐ à faire · 🧪 test à faire par vous dans le jeu · 🚦 point de validation avant de continuer.

## Vue d'ensemble

```
T0 ─► T1 ─► T2 ─► T3 ─► T4 ──────┐
                 │               ├─► T6 ─► T7
                 └──► T5 ────────┘
```

- T5 (plugin, lecture) peut démarrer dès que T3 (serveur + protocole) fonctionne, en parallèle de T4 (commandes). Le **faux serveur** de l'étape T3 permet de développer le plugin sans lancer le jeu.
- Chaque étape se termine par un 🚦 : je ne passe à la suivante qu'après votre validation.
- Une branche Git par étape (`feat/tN-...`), fusionnée dans `main` à la validation.

## Étape T0 — Dépôt et specs ✅

| # | Tâche | État |
|---|-------|------|
| T0.1 | Specs fonctionnelles et techniques rédigées | ✅ |
| T0.2 | Arborescence, `.gitignore`, `README` | ✅ |
| T0.3 | Premier commit (`feat/t0-structure-depot`) | ✅ |
| T0.4 | Fusion dans `main` et push vers GitHub | ☐ à votre demande |

## Étape T1 — Mod « hello world » (taille M) ✅

> Terminée : le mod est chargé par Redux (`[StreamPlug] Mod chargé.` dans `Ksp2.log`) et logue l'altitude chaque seconde en vol. La publicisation des types n'a pas été nécessaire (membres publics). Le logger du jeu est `ReduxLib.Logging.ILogger`.

**Objectif** : prouver que notre DLL est chargée par Redux et lit l'altitude.

| # | Tâche | Détail |
|---|-------|--------|
| T1.1 | Installer/vérifier le SDK .NET | Aucun `dotnet` détecté sur la machine : installer le SDK .NET (version compatible `netstandard2.1`) |
| T1.2 | Créer la solution `mod/StreamPlug.Mod.sln` | Projet `StreamPlug.Mod` (netstandard2.1, C# 9), fichier `Directory.Build.props` + `Directory.Build.props.user` (non versionné) avec `Ksp2Dir` |
| T1.3 | Références aux DLL du jeu | `Assembly-CSharp`, `SpaceWarp2`, `UnityEngine.CoreModule`, `Newtonsoft.Json` (`Private=false`) ; tester la publicisation si des types sont `internal` |
| T1.4 | Classe `StreamPlugMod : GeneralMod` | Log `[StreamPlug] chargé` dans `OnInitialized` |
| T1.5 | `swinfo.json` (spec 1.3) | `mod_id`, `name`, `version`, `main_assembly`, `ksp2_version` |
| T1.6 | Cible de build « déploiement » | Copie DLL + `swinfo.json` vers `<jeu>\mods\streamplug\` |
| T1.7 | `GameBridge` minimal | `MonoBehaviour` persistant ; toutes les secondes, log de l'altitude du véhicule actif |
| T1.8 | 🧪 Test en jeu | Lancer Redux, charger un vol, lire `Ksp2.log` |

**Critère de fin** : `Ksp2.log` affiche `[StreamPlug]` et l'altitude change pendant le vol.
**Risques** : mod non chargé (R7), types `internal` (R2), version netstandard/Mono. 🚦 **Validation** : vous confirmez la ligne de log.

## Étape T2 — Collecte complète (taille M) ✅

> Terminée et validée en jeu : poussée, SAS (bascule et mode), RCS, changement de véhicule (EVA), modes de vitesse Surface/Orbite, vitesse cible, et états hors vol (`MainMenu`, `Loading`, `KerbalSpaceCenter` → `aucun véhicule`). Le jeu conserve un véhicule figé hors vol : l'état global (`GlobalGameState.IsFlightMode`) est utilisé pour le détecter. **Non vu en jeu** (recette T6.6) : mode d'affichage `Target`, éditeur de véhicule, vue carte. Pour T3 : limiter l'envoi réseau à 10 `state` par seconde (la poussée déclenche un échantillonnage à chaque image pendant qu'on la déplace).

**Objectif** : produire un snapshot complet, thread-safe et testable.

| # | Tâche | Détail |
|---|-------|--------|
| T2.1 | Interface `IGameState` + `TelemetrySnapshot` | Altitude (mer, surface), vitesses (surface, orbitale, cible, mode), throttle, SAS, RCS, `controllable`, `status` |
| T2.2 | Implémentation `GameBridge` | Lecture via `ViewController` (§4.5), `try/catch` partout, état `noVessel` / `notControllable` |
| T2.3 | Échantillonnage et seuils de changement | Fréquence configurable (défaut 10 Hz), publication atomique du dernier snapshot |
| T2.4 | Abonnements aux messages | `ThrottleChanged`, `SASEnabled/Disabled/ModeChanged`, `RCSModeChanged` → échantillonnage immédiat ; inspecter les champs des messages (point 🔶) |
| T2.5 | Configuration `streamplug-config.json` | `port`, `sampleRateHz`, `verboseLogging` |
| T2.6 | Tests unitaires (sans le jeu) | Seuils de changement, états, bornes |
| T2.7 | 🧪 Test en jeu | Vols : décollage, orbite, retour au menu, changement de véhicule |

**Critère de fin** : le snapshot loggé reflète correctement l'état du jeu dans tous les cas du tableau §7 de SPECS-TECHNIQUES.
🚦 **Validation** : vous confirmez les valeurs (altitude, vitesses) par rapport à l'affichage du jeu.

## Étape T3 — Serveur WebSocket et protocole v1 (taille L) ✅

> Terminée et validée en jeu. **Décision R3 : serveur WebSocket maison** (RFC 6455, sans dépendance, `TcpListener` sur `127.0.0.1`) ; protocole v1 gelé (`protocol/`). 130 tests dont des tests d'intégration avec un vrai client WebSocket ; interopérabilité vérifiée avec le client Node `ws`. Outils de développement dans `tools/` (client de test, faux serveur). Les commandes sont reconnues mais refusées (`UNKNOWN_COMMAND`) jusqu'à T4.

**Objectif** : exposer le snapshot à un client externe.

| # | Tâche | Détail |
|---|-------|--------|
| T3.1 | Schémas du protocole dans `protocol/` | JSON Schema pour `hello`, `state`, `command`, `ack`, `error`, `ping/pong` + exemples |
| T3.2 | Prototype du serveur WebSocket minimal | `TcpListener`, handshake, frames texte, ping/pong, close, taille max (§4.7) — **décision R3** : maison ou bibliothèque |
| T3.3 | Intégration : diffusion des `state` | `hello` à la connexion, `state` complet, envoi sur changement + secours à 1 s, `seq` |
| T3.4 | Sécurité et robustesse | Liaison `127.0.0.1`, vérification `Origin`, port occupé géré, arrêt propre |
| T3.5 | Faux serveur de test dans `tools/` | Script (Node) qui rejoue des snapshots pour développer le plugin sans le jeu |
| T3.6 | Client de test dans `tools/` | Petit client CLI qui se connecte au mod et affiche les `state` |
| T3.7 | 🧪 Test en jeu | Client CLI connecté pendant un vol ; fermeture/relance du jeu ; coupure client |

**Critère de fin** : le client CLI reçoit des `state` en direct, sans impact visible sur les FPS, et le jeu se ferme proprement.
🚦 **Validation** : décision R3 prise, protocole v1 gelé.

## Étape T4 — Commandes (taille M)

**Objectif** : piloter throttle, SAS, RCS depuis un client.

| # | Tâche | Détail |
|---|-------|--------|
| T4.1 | `CommandQueue` + validation | Parsing, bornage, codes d'erreur (§6) |
| T4.2 | Exécution sur le thread Unity | `setThrottle`, `adjustThrottle`, `setSas`, `toggleSas`, `setRcs`, `toggleRcs`, `setSpeedMode` |
| T4.3 | `ack` / `error` | Un retour par commande avec `id` |
| T4.4 | Extension du client CLI | Envoi de commandes depuis `tools/` |
| T4.5 | Tests unitaires | Validation, bornage, refus sans véhicule |
| T4.6 | 🧪 Test en jeu | Throttle, SAS, RCS ; timewarp ; véhicule non contrôlable ; commandes au clavier en parallèle |

**Critère de fin** : toutes les commandes du MVP fonctionnent et le retour d'état est instantané.
**Risques** : R4 (timewarp, SAS sans pilote). 🚦 **Validation** : liste des comportements limites documentée.

## Étape T5 — Plugin Stream Deck : connexion et affichage (taille L)

**Objectif** : afficher altitude, vitesse et état de connexion sur le Stream Deck.

| # | Tâche | Détail |
|---|-------|--------|
| T5.1 | Initialiser le plugin | Node/`@elgato/cli`, `manifest.json` (`com.teddyzeus.streamplug`), icônes provisoires |
| T5.2 | `connection/ModClient` | Connexion, reconnexion (backoff 1→5 s), état observable |
| T5.3 | `state/Store` | Dernier snapshot, `seq` croissant, état « jeu non lancé » après 3 s |
| T5.4 | `rendering/` + `format/` | SVG de touche, unités (m/km/Mm, m/s/km/s), FR/EN |
| T5.5 | Actions `altitude`, `speed`, `throttle-display`, `status` | Bascule mer↔surface (local), cycle de vitesse (commande `setSpeedMode`) |
| T5.6 | Tests unitaires | Formatage, machine d'état, SVG |
| T5.7 | 🧪 Test sur le Stream Deck | Avec le faux serveur puis avec le jeu |

**Critère de fin** : altitude, vitesse et statut s'affichent en direct ; reconnexion automatique après redémarrage du jeu.
🚦 **Validation** : lisibilité des touches validée par vous (taille, couleurs).

## Étape T6 — Plugin : commandes et états dégradés (taille M)

| # | Tâche | Détail |
|---|-------|--------|
| T6.1 | Actions `sas`, `rcs` | État allumé/éteint + `setSas` / `setRcs` |
| T6.2 | Actions `throttle-set` et `throttle-step` | Paramètres dans le Property Inspector (0/50/100 %, ±10 %) |
| T6.3 | Property Inspector | Réglages hôte/port et options par action |
| T6.4 | États dégradés | Touches neutres hors connexion, `showAlert` sur refus |
| T6.5 | Disposition par défaut 3 × 5 | Vérifier la grille du §4.6 de SPECS.md |
| T6.6 | 🧪 Recette complète | Checklist `docs/test-checklist.md` (vol Kerbin, SAS/RCS, throttle, changement de véhicule, menu, redémarrage) |

**Critère de fin** : tous les critères d'acceptation du MVP (SPECS.md §8) sont cochés.
🚦 **Validation** : recette signée par vous.

## Étape T7 — Packaging et release (taille M)

| # | Tâche | Détail |
|---|-------|--------|
| T7.1 | Script de packaging | Zip du mod (`streamplug/`), `.streamDeckPlugin` du plugin |
| T7.2 | Documentation d'installation FR | `README` + `docs/installation.md` (captures) |
| T7.3 | CI GitHub Actions | Build/tests du plugin ; le mod est construit en local (DLL du jeu non redistribuables) |
| T7.4 | Release `v0.1.0` | Tag, notes de version, artefacts joints |
| T7.5 | 🧪 Installation à froid | Installer depuis la release sur un jeu propre |

**Critère de fin** : release GitHub publiée et installable en suivant uniquement la documentation.

## Points de décision à venir

| Décision | À prendre à | Qui |
|----------|-------------|-----|
| R3 : serveur WebSocket maison ou bibliothèque | T3.2 | Moi (proposition) + vous (validation) |
| Publicisation des assemblies du jeu | T1.3 | Moi |
| Disposition définitive des touches | T5/T6 | Vous |
| Profil Stream Deck prêt à l'emploi | Après v0.1.0 | Vous |

## Ce dont j'ai besoin de vous

- **Tests en jeu** (🧪) à la fin de chaque étape T1 à T4 : lancer Redux, charger un vol, me transmettre `Ksp2.log` (ou les lignes `[StreamPlug]`).
- **Tests sur le Stream Deck** (T5, T6).
- **Installer le SDK .NET** si vous préférez le faire vous-même (sinon je propose la commande à lancer en T1.1).
- **Validation** à chaque 🚦.

## Après le MVP (backlog, non planifié)

Données P2 (apoapsis, périapsis, carburant, ΔV), mode SAS, alertes sur seuils, pages/dossiers, profil préconfiguré, trains/lumières/parachutes (voir SPECS.md §4).
