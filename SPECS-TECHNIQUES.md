# OrbitDeck — Spécifications techniques

> Statut : **brouillon v0.1** — découle des spécifications fonctionnelles (dépôt privé du plugin). Les éléments marqués ✅ ont été vérifiés sur l'installation réelle (KSP 2 Redux v0.2.8.5.103184-beta) ; ceux marqués 🔶 sont des choix proposés ou des points à valider par un test.

## 1. Périmètre technique du MVP

| Fonction | Direction | Réf. fonctionnelle |
|----------|-----------|--------------------|
| Altitude | jeu → Stream Deck | F-01 |
| Vitesse (surface par défaut, bascule surface / orbitale / cible) | jeu → Stream Deck | F-02 |
| Throttle : lecture + 0 / 50 / 100 % + ±10 % | bidirectionnel | C-05 |
| SAS : état + activer / désactiver | bidirectionnel | C-01 |
| RCS : état + activer / désactiver | bidirectionnel | C-02 |
| État de connexion | interne | §4.4 |

Cible : Windows 10/11, Stream Deck 15 touches, KSP 2 Redux v0.2.8.5.103184-beta.

## 2. Architecture

```
┌─────────────────────────────┐              ┌──────────────────────────────┐
│ KSP 2 Redux (Unity / Mono)  │              │ Logiciel Stream Deck (Elgato)│
│ ┌─────────────────────────┐ │  WebSocket   │ ┌──────────────────────────┐ │
│ │ Mod OrbitDeck (DLL)    │◄├──────────────┤►│ Plugin OrbitDeck (Node) │ │
│ │ - Collecte (thread Unity)│ │ 127.0.0.1    │ │ - Client WebSocket       │ │
│ │ - Serveur WebSocket     │ │ JSON v1      │ │ - Actions / rendu touches│ │
│ │ - Exécution commandes   │ │              │ │ - Property Inspector     │ │
│ └─────────────────────────┘ │              │ └──────────────────────────┘ │
└─────────────────────────────┘              └──────────────────────────────┘
```

- Le **mod** est le serveur ; le **plugin** est le client. Aucun serveur intermédiaire.
- Le plugin se reconnecte tout seul (le jeu peut être lancé avant ou après).
- Tout reste en local, aucune donnée ne sort de la machine.

## 3. Structure du dépôt (🔶)

```
OrbitDeck/
├── SPECS.md
├── SPECS-TECHNIQUES.md
├── README.md
├── protocol/                 # définition du protocole (JSON Schema + exemples)
├── mod/                      # mod KSP 2 (C#)
│   ├── OrbitDeck.Mod.sln
│   ├── src/OrbitDeck.Mod/
│   │   ├── OrbitDeckMod.cs          # point d'entrée (GeneralMod)
│   │   ├── Telemetry/                # lecture de l'état du jeu
│   │   ├── Commands/                 # exécution des commandes
│   │   ├── Server/                   # serveur WebSocket + sessions
│   │   └── Protocol/                 # DTO et sérialisation
│   ├── tests/OrbitDeck.Mod.Tests/   # tests unitaires (sans jeu)
│   └── package/swinfo.json
└── streamdeck-plugin/        # plugin Stream Deck (TypeScript)
    ├── package.json
    ├── src/ (plugin.ts, actions/, connection/, rendering/)
    ├── ui/ (Property Inspector HTML/JS)
    ├── imgs/
    └── com.teddyzeus.orbitdeck.sdPlugin/manifest.json
```

## 4. Mod KSP 2

### 4.1 Environnement ✅
- **Chargeur** : celui de Redux (basé sur SpaceWarp 2), pas de BepInEx.
- **Dossier d'installation du mod** : `<dossier du jeu>\mods\orbitdeck\` (chemin relatif `./mods`, scanné récursivement). Exemple local : `E:\SteamLibrary\steamapps\common\Kerbal Space Program 2\mods\`.
- **Manifeste** `swinfo.json` avec `spec` ≥ 1.3, sinon le mod est ignoré. `main_assembly` désigne la DLL principale ; les DLL annexes vont dans `lib/`.
- **Classe d'entrée** : hérite de `SpaceWarp2.API.Mods.GeneralMod` (dans `SpaceWarp2.dll`). Callbacks disponibles : `OnPreInitialized`, `OnInitialized`, `OnPostInitialized`. `OnCampaignLoaded` / `OnCampaignUnloading` sont **absents** de cette version : ne pas les utiliser.
- **Runtime** : Mono (Unity), assemblies du jeu en `netstandard` / mscorlib 4.x.
- **Bibliothèques déjà fournies par le jeu** : `Newtonsoft.Json`, `0Harmony`, `System.Net.Http`. Aucune bibliothèque WebSocket n'est fournie.

### 4.2 Projet et build (🔶)
- Bibliothèque de classes ciblant **`netstandard2.1`** (ou `net48` si une incompatibilité apparaît au chargement) ; langage C# 9 maximum pour rester compatible Unity/Mono.
- Références **directes aux DLL du jeu** (`Assembly-CSharp.dll`, `SpaceWarp2.dll`, `UnityEngine.CoreModule.dll`, `Newtonsoft.Json.dll`), jamais copiées dans l'artefact (`Private=false`).
- Chemin du jeu fourni par une propriété MSBuild `Ksp2Dir` (fichier `Directory.Build.props.user`, non versionné) ; valeur par défaut : variable d'environnement `KSP2_DIR`.
- Cible de build post-compilation : copie de la DLL et de `swinfo.json` dans `$(Ksp2Dir)\mods\orbitdeck\`.
- Si les types du jeu sont `internal`, utiliser le référencement des assemblies « publicisées » (outil de type BepInEx.AssemblyPublicizer) ; à confirmer au premier build.

### 4.3 Composants

| Composant | Responsabilité |
|-----------|----------------|
| `OrbitDeckMod` | Point d'entrée ; crée un `GameObject` persistant portant le `MonoBehaviour` de pompe ; démarre / arrête le serveur ; lit la configuration |
| `GameBridge` (`MonoBehaviour`) | Seul code qui touche l'API du jeu. `Update()` : échantillonne la télémétrie à la fréquence configurée ; vide la file de commandes |
| `TelemetrySnapshot` | Structure immuable : altitude, vitesse (3 types), throttle, SAS, RCS, état du véhicule |
| `CommandQueue` | File thread-safe (`ConcurrentQueue`) entre le thread réseau et le thread Unity |
| `WsServer` | Serveur WebSocket local, gère plusieurs clients, diffuse les snapshots |
| `ProtocolCodec` | (Dé)sérialisation JSON (Newtonsoft) et validation des messages entrants |
| `OrbitDeckConfig` | Port, fréquence, activation des logs verbeux (fichier `orbitdeck-config.json`) |

### 4.4 Règles de threading (critique)
1. **Tout accès à l'API du jeu se fait sur le thread principal Unity** (dans `GameBridge.Update`).
2. Le thread réseau ne lit jamais le jeu : il ne manipule que le dernier `TelemetrySnapshot` publié (échange atomique) et la `CommandQueue`.
3. Les commandes reçues sont validées sur le thread réseau, **exécutées** sur le thread Unity.
4. Aucune exception ne doit sortir de `Update()` : chaque lecture et chaque commande est protégée par un `try/catch` qui journalise (`SWLogger`) sans planter le jeu.

### 4.5 Collecte de données (✅ API vérifiée)

Accès au véhicule actif : `GameManager.Instance.Game.ViewController`, puis `ActiveVesselVehicle` (champ) ou `TryGetActiveSimVessel(out VesselComponent, requireValidInSim: true)`.

| Donnée | Source | Notes |
|--------|--------|-------|
| `altitude.seaLevel` | `VesselVehicle.AltitudeFromSeaLevel` | mètres |
| `altitude.surface` | `VesselVehicle.AltitudeFromSurface` | mètres, valeur affichée par défaut ; un appui sur la touche bascule vers `seaLevel` (côté plugin) |
| `speed.surface` | `VesselVehicle.SurfaceSpeed` | m/s |
| `speed.orbital` | `VesselVehicle.OrbitalSpeed` | m/s |
| `speed.target` | `VesselVehicle.TargetSpeed` | m/s ; valeur utile seulement si une cible existe |
| `speed.mode` | `VesselVehicle.SpeedDisplayMode` | `Orbit` / `Surface` / `Target` |
| `throttle` | `VesselVehicle.mainThrottle` | 0.0 à 1.0 |
| `sas.enabled`, `sas.mode` | `VesselComponent.Autopilot.Enabled`, `.AutopilotMode` | `AutopilotMode` : StabilityAssist, Prograde, Retrograde, … |
| `rcs.enabled` | `VesselComponent.IsRCSEnabled` | |
| `controllable` | `VesselComponent.IsControllable` | |

Mise à jour :
- **Échantillonnage périodique** à la fréquence configurée (défaut 10 Hz côté mod ; le plugin n'affiche qu'à 5 Hz au plus).
- **Envoi sur changement** : le snapshot n'est diffusé que s'il diffère du précédent (seuil par champ pour éviter le bruit : altitude ±0,5 m, vitesse ±0,05 m/s, throttle ±0,005), plus un envoi de secours toutes les secondes.
- **Abonnement aux messages** (`ThrottleChangedMessage`, `SASEnabledMessage`, `SASDisabledMessage`, `SASModeChangedMessage`, `RCSModeChangedMessage`) via `Game.Messages.Subscribe` : déclenche un échantillonnage immédiat pour un retour d'état instantané. 🔶 Les champs exacts de ces messages n'ont pas été inspectés ; le snapshot reste la source de vérité.

États du véhicule dans le snapshot : `connected`, `noVessel` (menu, centre spatial, véhicule nul), `notControllable`.

### 4.6 Commandes (✅ API vérifiée)

| Commande | Appel | Garde-fous |
|----------|-------|-----------|
| `setThrottle {value}` | `VesselVehicle.SetMainThrottle(float)` | valeur bornée [0, 1] ; refus si véhicule absent ou non contrôlable |
| `adjustThrottle {delta}` | lecture de `mainThrottle` + delta, puis `SetMainThrottle` | résultat borné [0, 1] |
| `setSas {enabled}` | `VesselComponent.SetAutopilotEnableDisable(bool)` | refus si non contrôlable ; retour booléen du jeu renvoyé au client |
| `toggleSas` | `SetAutopilotEnableDisable(!Autopilot.Enabled)` | idem |
| `setRcs {enabled}` | `VesselComponent.IsRCSEnabled = bool` | refus si non contrôlable |
| `toggleRcs` | `IsRCSEnabled = !IsRCSEnabled` | idem |
| `setSpeedMode {mode}` | `VesselVehicle.SetSpeedDisplayMode(mode, callback)` | `surface` / `orbit` / `target` ; `target` refusé sans cible (🔶 à tester) |

Chaque commande reçoit une réponse `ack` ou `error` (voir §6). Les commandes sont idempotentes quand c'est possible (`set*` plutôt que `toggle*`) ; le plugin utilise les `set*` à partir de l'état connu pour éviter les doubles bascules.

🔶 Points à tester en jeu : le throttle est-il bien appliqué quand le joueur ne touche pas aux commandes ? Le verrouillage pendant le timewarp (`VesselThrottleLockedDueToTimewarpingMessage`) bloque-t-il `SetMainThrottle` ? `SetAutopilotEnableDisable(true)` active-t-il le mode précédent ?

### 4.7 Serveur WebSocket (🔶)
- **Liaison** : `127.0.0.1` uniquement, port par défaut **`18765`**, configurable.
- **Implémentation proposée** : serveur minimal maison (RFC 6455, frames texte uniquement, ping/pong, close) sur `System.Net.Sockets.TcpListener`, ~300 lignes, **sans dépendance** à embarquer. Justification : aucune bibliothèque WebSocket n'est fournie par le jeu, et le support serveur WebSocket de `HttpListener` sous Mono est incertain. Alternative : embarquer une bibliothèque légère dans `lib/` (à évaluer : compatibilité netstandard et licence).
- **Sécurité** : en plus de la liaison locale, vérifier l'en-tête `Origin` à l'ouverture (refuser les origines de navigateur ; le plugin Node n'en envoie pas) et limiter la taille des messages (ex. 4 Ko). Pas d'authentification pour le MVP (risque accepté : usage local).
- **Clients multiples** : autorisés (ex. le plugin + un outil de debug) ; chaque client reçoit l'état courant à la connexion.
- **Arrêt propre** : le serveur est arrêté à la fermeture du jeu / au déchargement du mod ; aucun thread de premier plan ne doit empêcher la fermeture du processus.

### 4.8 Journalisation et configuration
- Logs via `SWLogger` (visibles dans `Ksp2.log`), préfixe `[OrbitDeck]`. Niveau verbeux activable par configuration.
- Fichier de configuration `orbitdeck-config.json` dans le dossier du mod : `port`, `sampleRateHz`, `verboseLogging`.

## 5. Plugin Stream Deck

### 5.1 Environnement (🔶, à confirmer à l'initialisation du projet)
- SDK Elgato officiel (`@elgato/streamdeck`), **TypeScript**, Node.js fourni par le logiciel Stream Deck.
- Outillage : CLI Elgato (`@elgato/cli`) pour créer, lier (`streamdeck link`) et empaqueter (`streamdeck pack`) le plugin en `.streamDeckPlugin`.
- Client WebSocket vers le mod : bibliothèque `ws` (ou le `WebSocket` natif de Node si disponible dans la version fournie).
- Tests : `vitest` (ou équivalent) pour la logique pure.
- Identifiant de plugin : `com.teddyzeus.orbitdeck` (🔶 à valider).

### 5.2 Modules

| Module | Responsabilité |
|--------|----------------|
| `connection/ModClient` | Connexion WebSocket, reconnexion automatique (backoff 1 s → 5 s), parsing des messages, état de connexion observable |
| `state/Store` | Dernier snapshot reçu + état de connexion ; notifie les actions abonnées |
| `actions/*` | Une classe par action Stream Deck (voir §5.3) |
| `rendering/` | Génération des images de touche (SVG → data URI) : valeur, libellé, unité, couleurs |
| `format/` | Mise à l'échelle des unités (m, km, Mm ; m/s, km/s), arrondis, formats FR/EN |
| `ui/` | Property Inspector (HTML/JS) : hôte/port, options par action |

### 5.3 Actions du MVP

| Action (UUID) | Touche | Comportement |
|---------------|--------|--------------|
| `...altitude` | Altitude | Affiche l'altitude formatée avec son libellé (`MER` / `SOL`) ; **appui = bascule niveau de la mer ↔ surface**. Le choix est local au plugin (le snapshot contient déjà les deux valeurs), aucune commande n'est envoyée au jeu ; il est conservé dans les réglages de l'action. Défaut : surface |
| `...speed` | Vitesse | Affiche la vitesse du type courant (surface par défaut) avec son libellé ; **appui = cycle surface → orbitale → cible**, envoie `setSpeedMode` |
| `...sas` | SAS | Allumée / éteinte selon `sas.enabled` ; appui = `setSas` inverse de l'état affiché |
| `...rcs` | RCS | Idem avec `rcs.enabled` / `setRcs` |
| `...throttle-display` | Valeur throttle | Affiche le pourcentage ; pas de commande |
| `...throttle-set` | 0 % / 50 % / 100 % | Paramètre `value` dans le Property Inspector ; appui = `setThrottle` |
| `...throttle-step` | −10 % / +10 % | Paramètre `delta` ; appui = `adjustThrottle` |
| `...status` | Connexion | Affiche « Jeu non lancé » / « Aucun véhicule » / « Connecté » / « Non contrôlable » |

Principes :
- **Une seule action `throttle-set` et une seule `throttle-step`**, paramétrées, plutôt que 5 actions distinctes.
- **Le plugin est la vue, le mod est la vérité** : aucun état de jeu n'est supposé côté plugin. Après une commande, l'affichage change quand le mod envoie le nouveau snapshot.
- **États dégradés** : si le jeu n'est pas connecté, toutes les touches affichent un état neutre (grisé, « — ») et les appuis ne font rien (retour visuel d'alerte `showAlert`).
- **Disposition par défaut** : le §4.6 de [SPECS.md](SPECS.md) (grille 3 × 5). Un profil `.streamDeckProfile` préconfiguré est livré en 🔶 phase ultérieure.

### 5.4 Rendu
- Touches 72 × 72 px (affichage à vérifier selon le modèle) : image générée en SVG, envoyée via `setImage`.
- Valeur en gros, libellé en petit en haut, unité en petit en bas. Couleurs : fond sombre, valeur claire ; état actif (SAS / RCS) en couleur d'accent.
- Limitation du taux de mise à jour des images (≤ 5 Hz par touche) et **redessin uniquement si la valeur affichée change**.

## 6. Protocole (v1)

- **Transport** : WebSocket, un message JSON par frame texte, UTF-8.
- **Versionnage** : champ `v` (entier) dans chaque message ; le serveur répond `hello` avec sa version ; si incompatible, le client affiche « Mise à jour requise ».
- **Unités** : SI, valeurs brutes (mètres, mètres par seconde, fraction 0–1). Le formatage est fait côté plugin.

### 6.1 Messages serveur → client

```jsonc
// À l'ouverture de la connexion
{ "v": 1, "type": "hello", "mod": "0.1.0", "game": "0.2.8.5.103184" }

// Snapshot d'état (à la connexion, sur changement, et toutes les secondes)
{
  "v": 1, "type": "state", "seq": 1042,
  "status": "connected",            // connected | noVessel | notControllable
  "vessel": { "name": "Kerbal X" }, // absent si noVessel
  "altitude": { "seaLevel": 12400.5, "surface": 12380.1 },
  "speed": { "mode": "surface", "surface": 245.3, "orbital": 2300.1, "target": null },
  "throttle": 0.5,
  "sas": { "enabled": true, "mode": "StabilityAssist" },
  "rcs": { "enabled": false }
}

// Réponse à une commande
{ "v": 1, "type": "ack", "id": "c-17" }
{ "v": 1, "type": "error", "id": "c-17", "code": "NO_VESSEL", "message": "Aucun véhicule actif" }
```

Codes d'erreur : `BAD_REQUEST`, `UNKNOWN_COMMAND`, `NO_VESSEL`, `NOT_CONTROLLABLE`, `REJECTED_BY_GAME`, `INTERNAL`.

### 6.2 Messages client → serveur

```jsonc
{ "v": 1, "type": "command", "id": "c-17", "name": "setThrottle",    "args": { "value": 0.5 } }
{ "v": 1, "type": "command", "id": "c-18", "name": "adjustThrottle", "args": { "delta": 0.1 } }
{ "v": 1, "type": "command", "id": "c-19", "name": "setSas",         "args": { "enabled": true } }
{ "v": 1, "type": "command", "id": "c-20", "name": "setRcs",         "args": { "enabled": false } }
{ "v": 1, "type": "command", "id": "c-21", "name": "setSpeedMode",   "args": { "mode": "orbit" } }
{ "v": 1, "type": "ping" }   // réponse : { "v": 1, "type": "pong" }
```

### 6.3 Règles
- `seq` croissant : le client ignore tout `state` dont le `seq` est inférieur au dernier reçu.
- Un champ inconnu est ignoré (compatibilité ascendante) ; un `type` inconnu est ignoré côté client et répond `UNKNOWN_COMMAND` côté serveur pour les commandes.
- Les schémas JSON sont maintenus dans `protocol/` et servent aux tests des deux côtés.

## 7. Gestion des erreurs et cas limites

| Cas | Comportement |
|-----|-------------|
| Jeu non lancé | Le plugin retente la connexion ; touches en état « Jeu non lancé » |
| Jeu lancé, aucun véhicule (menu, centre spatial) | `status: noVessel` ; touches neutres |
| Véhicule sans contrôle (pas de pilote, EVA) | `status: notControllable` ; commandes refusées (`NOT_CONTROLLABLE`) |
| Changement de véhicule | Nouveau snapshot immédiat ; pas de commande en cours conservée |
| Perte de connexion en vol | Reconnexion automatique ; état « Jeu non lancé » après 3 s sans message |
| Port occupé | Le mod journalise l'erreur et ne démarre pas le serveur ; il ne plante pas le jeu |
| Message invalide | Réponse `error BAD_REQUEST` ; connexion conservée |
| Timewarp | Throttle verrouillé par le jeu : commande renvoyée en `REJECTED_BY_GAME` si l'appel échoue |

## 8. Performance et robustesse
- Surcoût visé dans le jeu : < 0,2 ms par frame en moyenne ; pas d'allocation dans `Update()` hors snapshot publié (réutilisation de buffers et de la sérialisation).
- Fréquence max d'envoi par client : 10 messages `state` par seconde.
- Aucun appel bloquant sur le thread Unity.
- Le mod doit pouvoir être retiré (supprimer son dossier) sans laisser d'effet sur les sauvegardes : **aucune donnée n'est écrite dans les sauvegardes**.

## 9. Tests

| Niveau | Contenu | Où |
|--------|---------|----|
| Unitaires mod | Codec JSON, validation des commandes, seuils de changement, bornage du throttle | `mod/tests` (sans le jeu, via interfaces d'accès à l'état) |
| Unitaires plugin | Formatage des unités, machine d'état de la connexion, génération des SVG | `streamdeck-plugin` |
| Contrat | Mêmes exemples JSON validés contre les schémas de `protocol/` des deux côtés | `protocol/` + tests |
| Intégration locale | Faux serveur WebSocket (script) qui rejoue des snapshots → vérifier l'affichage sans lancer le jeu | `tools/` |
| Manuel en jeu | Checklist : vol sur Kerbin, lecture altitude/vitesse, bascule SAS/RCS, throttle, changement de véhicule, retour au menu, redémarrage du jeu | `docs/test-checklist.md` |

Pour rendre le mod testable : l'accès à l'API du jeu est caché derrière une interface (`IGameState`) ; seule son implémentation `GameBridge` référence les types du jeu.

## 10. Build, versionnement et release
- **Version** : SemVer pour le mod et le plugin, protocole versionné séparément (`v`).
- **Artefacts de release GitHub** :
  - `OrbitDeck-Mod-<version>.zip` : dossier `orbitdeck/` (DLL, `swinfo.json`, `lib/` éventuel) à extraire dans `<jeu>\mods\`.
  - `com.teddyzeus.orbitdeck-<version>.streamDeckPlugin` : plugin Stream Deck.
- **CI (🔶)** : GitHub Actions — build du plugin, tests des deux côtés. Le build du mod en CI nécessite les DLL du jeu (non redistribuables) : prévoir un stub de références ou un build local uniquement pour le mod.
- **`swinfo.json`** : champ `ksp2_version` renseigné avec la version de Redux testée ; `source` pointe vers le dépôt GitHub.

## 11. Risques et points ouverts

| # | Risque / question | Mitigation |
|---|-------------------|-----------|
| R1 | L'API du jeu change avec les mises à jour de Redux (branche beta) | Isoler dans `GameBridge`, tests manuels à chaque version, `ksp2_version` dans `swinfo.json` |
| R2 | Types `internal` / accès aux membres du jeu | Publicisation des assemblies au build (§4.2) |
| R3 | Serveur WebSocket : implémentation maison vs bibliothèque | Prototype du serveur minimal, décision après test (§4.7) |
| R4 | Comportement de `SetMainThrottle` / SAS sans entrée joueur ou en timewarp | Tests en jeu (§4.6), codes d'erreur dédiés |
| R5 | Thread principal : appels depuis un autre thread | Règles §4.4, revue systématique |
| R6 | Dépendance à la version de Node du logiciel Stream Deck | Utiliser le SDK officiel et la CLI qui fixent la version |
| R7 | Dossier `mods` « introuvable » signalé dans un ancien log | Vérifier au premier démarrage que le mod est bien chargé (log `[OrbitDeck]`) |
| Q1 | ~~Altitude par défaut~~ | **Tranché** : bascule par appui entre surface et niveau de la mer ; surface par défaut |
| Q2 | ~~Port par défaut 18765~~ | **Tranché** : validé |
| Q3 | ~~Identifiant du plugin `com.teddyzeus.orbitdeck`~~ | **Tranché** : validé |

## 12. Plan de réalisation (technique)

| Étape | Livrable | Critère de fin |
|-------|----------|----------------|
| T0 | Dépôt structuré, `.gitignore`, `README` | Arborescence §3 en place |
| T1 | Mod « hello world » | Le mod est chargé par Redux et logue l'altitude dans `Ksp2.log` |
| T2 | Collecte complète + `IGameState` | Snapshot complet loggé, thread-safe |
| T3 | Serveur WebSocket + protocole v1 | Un client de test reçoit les `state` en direct |
| T4 | Commandes | Throttle / SAS / RCS pilotés depuis un client de test |
| T5 | Plugin Stream Deck : connexion + affichage | Altitude et vitesse affichées en direct |
| T6 | Plugin : actions de commande + états dégradés | Critères d'acceptation du MVP (SPECS.md §8) |
| T7 | Packaging, documentation d'installation FR, release GitHub | Release v0.1.0 publiée |
