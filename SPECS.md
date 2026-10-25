# StreamPlug — Spécifications fonctionnelles

> Statut : **brouillon v0.1** — document de base, à compléter et valider ensemble.

## 1. Vision

StreamPlug affiche en temps réel des informations de **Kerbal Space Program 2 (KSP 2)** sur les touches (et cadrans) d'un **Elgato Stream Deck**, et permet de déclencher certaines actions du jeu depuis celui-ci.

### Objectifs
- Voir d'un coup d'œil la télémétrie de vol sans quitter la vue de jeu.
- Avoir un affichage lisible, configurable et réactif (latence faible).
- Pouvoir envoyer des commandes simples au jeu (phase 2).

### Hors périmètre (pour l'instant)
- Support d'autres jeux.
- Accès distant via Internet (usage local uniquement).
- Support mobile / Stream Deck logiciel autre que Elgato.

## 2. Architecture générale

```
┌────────────┐   mod/plugin    ┌──────────────────┐   WebSocket    ┌───────────────────┐
│   KSP 2    │ ─────────────►  │  Mod KSP 2       │ ────────────►  │ Plugin Stream Deck│
│ (Unity)    │  API du jeu     │ (collecte+serveur│  JSON local    │ (SDK Elgato)      │
└────────────┘                 │  localhost)      │ ◄────────────  └───────────────────┘
                               └──────────────────┘   commandes
```

Deux composants :
1. **Mod KSP 2** (C#, chargeur de mods de Redux basé sur SpaceWarp — à confirmer, voir §9) : lit l'état du jeu, l'expose via un serveur local.
2. **Plugin Stream Deck** (SDK Elgato, Node.js/TypeScript) : se connecte au mod, affiche les données, gère la configuration des touches.

> **Architecture validée** : exactement ces deux composants, sans serveur intermédiaire. Le mod héberge le serveur WebSocket local, le plugin Stream Deck est le client. Structure de dépôt prévue : `mod/` et `streamdeck-plugin/`, publiés ensemble dans chaque release GitHub.
>
> Reste à valider : choix techniques précis (bibliothèque WebSocket côté mod, voir §9 et §11.3).

## 3. Acteurs et utilisateurs
- **Joueur / streamer** : installe le mod et le plugin, configure ses touches.
- **Spectateurs** : voient les infos via le stream (aucune interaction directe).

## 4. Fonctionnalités

Priorités : **P1** = MVP, **P2** = souhaité, **P3** = idée.

### 4.1 Télémétrie de vol (lecture)

| ID | Donnée | Priorité |
|----|--------|----------|
| F-01 | Altitude (surface par défaut ; bascule niveau de la mer ↔ surface par appui) | P1 |
| F-02 | Vitesse (surface par défaut ; bascule orbitale / cible par appui) | P1 |
| F-03 | Apoapsis et périapsis (altitude + temps restant) | P2 |
| F-04 | Carburant restant (par étage et total) et ΔV | P2 |
| F-05 | Corps céleste actuel (Kerbin, Mun, …) et situation (sol, vol, orbite, sub-orbital) | P2 |
| F-06 | Niveau de poussée / commande des gaz | P2 |
| F-07 | Électricité (batterie, production) | P2 |
| F-08 | Inclinaison, excentricité, période orbitale | P2 |
| F-09 | Température / contraintes thermiques | P3 |
| F-10 | Temps de mission (MET) | P2 |
| F-11 | Cible sélectionnée : distance, vitesse relative | P3 |
| F-12 | Communication / liaison (si implémentée dans le jeu) | P3 |

### 4.2 Commandes (écriture)

| ID | Action | Priorité |
|----|--------|----------|
| C-01 | Activer / désactiver le SAS (le choix du mode SAS est en P2) | P1 |
| C-02 | Activer / désactiver le RCS | P1 |
| C-05 | Gestion de la poussée (throttle) : voir détail ci-dessous | P1 |
| C-03 | Déclencher l'étage suivant (staging) | P2 |
| C-04 | Trains d'atterrissage / lumières / freins | P2 |
| C-06 | Panneaux solaires, antennes, parachutes | P3 |
| C-07 | Quick save / quick load | P3 |

**Détail C-05 — throttle (validé)** : touches de réglage rapide (0 %, 50 %, 100 %) et touches +10 % / −10 % ; la valeur courante de la poussée est affichée sur une touche dédiée.

**Retour d'état (C-01, C-02)** : les touches SAS et RCS reflètent l'état réel dans le jeu (allumé / éteint), même si le joueur les change au clavier.

### 4.3 Affichage sur le Stream Deck

- **Touche d'information** : une valeur + libellé + unité (ex. `ALT 12,4 km`).
- **Jauge** : barre ou cercle de progression (carburant, électricité).
- **Indicateur d'état** : couleur selon seuils (vert / orange / rouge).
- ~~Cadran (Stream Deck +)~~ : hors périmètre, l'appareil cible n'en a pas.
- **Unités** : métrique avec mise à l'échelle automatique (m, km, Mm ; m/s, km/s).
- **Personnalisation** par touche : donnée choisie, couleur, seuils d'alerte, icône, format.
- **Alertes** : clignotement ou changement de couleur (carburant bas, périapsis sous l'atmosphère, etc.).

### 4.4 Connexion et état
- Détection automatique du jeu (reconnexion transparente).
- Affichage d'un état clair sur les touches : « Jeu non lancé », « Aucun véhicule », « Connecté ».
- Fréquence de rafraîchissement configurable (par défaut 5 Hz).

### 4.5 Configuration
- Réglages globaux : port du serveur, fréquence, unités, thème.
- Réglages par touche via le Property Inspector du Stream Deck.
- Profils Stream Deck préconfigurés livrés (vol, orbite, atterrissage).

### 4.6 Contrainte matérielle : Stream Deck 15 touches
- Appareil cible : **Stream Deck 15 touches** (3 lignes × 5 colonnes), sans cadrans.
- Mise en page MVP proposée (à valider) :

| | Col. 1 | Col. 2 | Col. 3 | Col. 4 | Col. 5 |
|---|---|---|---|---|---|
| **Ligne 1** | Altitude (appui = mer ↔ surface) | Vitesse (appui = changer de type) | État de connexion | — | — |
| **Ligne 2** | SAS | RCS | Throttle (valeur) | — | — |
| **Ligne 3** | Throttle 0 % | Throttle 50 % | Throttle 100 % | Throttle −10 % | Throttle +10 % |

- Les touches libres sont réservées aux données P2 (apoapsis, périapsis, carburant…).
- Au-delà de 15 touches : organisation en **pages / dossiers** (Vol, Orbite, Atterrissage).
- **Vitesse (F-02)** : type affiché par défaut = **surface** ; un appui sur la touche alterne entre surface, orbitale et cible (le type courant est indiqué sur la touche).
- Une touche peut alterner automatiquement entre 2 valeurs (ex. apoapsis / périapsis) pour économiser de la place.

## 5. Parcours utilisateur types

1. **Installation** : installer le mod dans KSP 2 → installer le plugin `.streamDeckPlugin` → lancer le jeu.
2. **Configuration** : glisser une action StreamPlug sur une touche → choisir la donnée → ajuster seuils/couleurs.
3. **Vol** : le joueur lance une fusée, les touches se mettent à jour en direct ; une alerte s'affiche si le carburant est bas.
4. **Fin de session** : fermeture du jeu → les touches repassent à l'état « Jeu non lancé ».

## 6. Exigences non fonctionnelles

| Domaine | Exigence |
|---------|----------|
| Performance | Impact sur les FPS du jeu négligeable (< 1 %) ; latence de bout en bout < 200 ms |
| Fiabilité | Aucun crash du jeu si le plugin Stream Deck est absent ; reconnexion automatique |
| Sécurité | Serveur lié à `127.0.0.1` uniquement ; aucune donnée envoyée sur Internet |
| Compatibilité | Windows 10/11 ; Stream Deck logiciel récent ; KSP 2 Redux v0.2.8.5.103184 |
| Maintenabilité | Protocole versionné ; tests unitaires sur le formatage et le protocole |
| Localisation | Interface en français et en anglais |

## 7. Protocole mod ↔ plugin (ébauche)

Transport : WebSocket local, messages JSON.

```jsonc
// mod → plugin (télémétrie, à fréquence régulière)
{
  "type": "telemetry",
  "version": 1,
  "vessel": { "name": "Kerbal X", "situation": "orbiting", "body": "Kerbin" },
  "data": { "altitude": 85000.0, "speedOrbital": 2250.4, "apoapsis": 86000.0, "periapsis": 84000.0, "fuelPercent": 62.5 }
}

// plugin → mod (commande)
{ "type": "command", "action": "toggleSAS" }
```

## 8. Critères d'acceptation du MVP

- [ ] Le mod se charge dans KSP 2 sans erreur et expose l'altitude (F-01) et la vitesse (F-02).
- [ ] Le plugin affiche l'altitude et la vitesse en direct sur deux touches.
- [ ] Les touches SAS et RCS basculent l'état dans le jeu et reflètent son état réel.
- [ ] Le throttle est réglable depuis le Stream Deck (0 / 50 / 100 %, ±10 %) et sa valeur est affichée.
- [ ] Reconnexion automatique après redémarrage du jeu ou du Stream Deck.
- [ ] Documentation d'installation en français.

## 9. Questions ouvertes

1. **Modding KSP 2** : version cible = **KSP 2 Redux v0.2.8.5.103184** (version développée par la communauté). Constats sur le GitHub [KSP2Redux](https://github.com/KSP2Redux) : Redux **remplace BepInEx par un chargeur de mods intégré** (les mods BepInEx existants ne fonctionnent pas forcément) ; ce chargeur s'appuie sur **SpaceWarp** (dépôt `Redux.ModApi`, avec UitkForKsp2, PatchManager, V-SwiFT, DebugTools) ; le SDK de mods est un package **Unity** (dépôt `SDK`, outils `Redux-SDK-Manager` et `Redux.Template`) ; la documentation modding est annoncée « en cours » et une doc d'API autogénérée existe (`API-Documentation`, assemblies `Assembly-CSharp`, `ReduxLib`, `UitkForKsp2`). **Recherche faite** : structure d'un mod, chargement et API de télémétrie / commandes sont détaillés en **§11**. **À vérifier par un test réel** : que ces classes existent telles quelles dans la v0.2.8.5.103184 (la doc consultée est celle de la branche beta) et que le SDK Unity est compatible avec cette version. Le README du dépôt `Redux` mentionne aussi KSP 2 v0.2.2.0 comme base, à confirmer.
2. ~~Modèle Stream Deck~~ : **tranché** — 15 touches, pas de cadrans (§4.6).
3. ~~Périmètre phase 1~~ : **tranché** — MVP = altitude + vitesse (lecture) et throttle + SAS + RCS (commandes).
4. **Langage du plugin** : TypeScript (SDK Elgato officiel) — OK ?
5. ~~Distribution~~ : **tranché** — GitHub Releases uniquement (mod + `.streamDeckPlugin` publiés en pièces jointes de chaque release).
6. **Multi-véhicules** : que faire quand le joueur change de véhicule ou se trouve dans le centre spatial ?
7. ~~Données prioritaires~~ : **tranché** — altitude et vitesse pour le MVP.
8. ~~Vitesse~~ : **tranché** — surface par défaut, bascule par appui sur la touche.

## 10. Feuille de route proposée

| Phase | Contenu |
|-------|---------|
| 0 | Specs validées, prototype « hello world » du mod (lire l'altitude) |
| 1 | MVP : altitude, vitesse, throttle, SAS, RCS sur Stream Deck |
| 2 | Données P2 (apoapsis, périapsis, carburant…), alertes, mode SAS |
| 3 | Cadrans, profils prêts à l'emploi, packaging et distribution |

## 11. Annexe — Recherche technique sur KSP 2 Redux

Sources : dépôts GitHub [KSP2Redux](https://github.com/KSP2Redux) (`Redux.ModApi`, `API-Documentation`, `Redux.Templates`, `SDK`) et doc d'API en ligne `api.ksp2redux.org` (branches `beta` et `develop`). Les noms ci-dessous viennent de la doc d'API Redux (beta) ; ils restent à valider par un test réel dans le jeu.

### 11.1 Structure et chargement d'un mod
- **Dossier des mods** : `./mods/` à côté de l'exécutable du jeu (scanné récursivement).
- **Manifeste** : un fichier `swinfo.json` par mod, **spec ≥ 1.3** (sinon le mod est ignoré). Champs utiles : `spec`, `mod_id`, `name`, `author`, `description`, `source`, `version`, `dependencies`, `ksp2_version` (min/max), `main_assembly`, `version_check`.
- **Code** : DLL principale désignée par `main_assembly` ; DLL annexes dans un sous-dossier `lib/` (chargées automatiquement). La classe du mod hérite de `SpaceWarp2.API.Mods.GeneralMod` et reçoit `OnPreInitialized`, `OnInitialized`, `OnPostInitialized`. (`OnCampaignLoaded` / `OnCampaignUnloading` existent dans le code source de la branche dev mais **sont absents de la v0.2.8.5.103184 installée** : ne pas les utiliser.)
- **Outils** : Harmony est disponible (`CreateHarmonyAndPatchAll`). Configuration par mod via `SWConfiguration` (fichier `<mod_id>-config.json`) et logs via `SWLogger`.
- **Pas de BepInEx** : le chargeur est intégré à Redux. Les mods Lua (`*.lua`) existent aussi mais ne conviennent pas à notre besoin (serveur réseau).
- **Création du projet** : projet Unity depuis `Redux.Templates` via le *Redux SDK Manager* (SDK `ksp2redux.sdk`, registre UPM `https://upm.ksp2redux.org`). Versions des templates calquées sur celles du jeu (ex. `0.2.9-26w43b`), donc **vérifier la compatibilité avec v0.2.8.5.103184**.

### 11.2 Accès aux données et commandes (API du jeu)
Accès au véhicule actif : `GameManager.Instance.Game.ViewController` → champ `ActiveVesselVehicle` (`VesselVehicle`) ou `TryGetActiveSimVessel(out VesselComponent, requireValidInSim)` / `GetActiveSimVessel(bool requireValidInSim)`. Messages du jeu : `Game.Messages.Subscribe<TMessage>(...)` (`MessageCenter`).

| Besoin | Lecture | Écriture |
|--------|---------|----------|
| Altitude (F-01) | `VesselVehicle.AltitudeFromSeaLevel`, `AltitudeFromSurface`, `AltitudeFromTerrain`, `AltitudeFromScenery` (m) | — |
| Vitesse (F-02) | `SurfaceSpeed`, `OrbitalSpeed`, `TargetSpeed` ; aussi `VerticalSpeed`, `HorizontalSpeed` ; mode du jeu : `SpeedDisplayMode` | `SetSpeedDisplayMode(mode)` (optionnel, pour suivre le mode du jeu) |
| Throttle (C-05) | `VesselVehicle.mainThrottle` (0–1) ; événement `ThrottleChangedMessage` | `VesselVehicle.SetMainThrottle(float)` |
| SAS (C-01) | `VesselComponent.AutopilotStatus` / `Autopilot.Enabled` ; événements `SASEnabledMessage`, `SASDisabledMessage`, `SASModeChangedMessage` | `VesselComponent.SetAutopilotEnableDisable(bool)` ; mode : `SetAutopilotMode(AutopilotMode)` |
| RCS (C-02) | `VesselComponent.IsRCSEnabled` ; événement `RCSModeChangedMessage` | `VesselComponent.IsRCSEnabled = bool` |
| P2 : apoapsis / périapsis | classes `VehicleApoapsisAltitudePropertyWatcher`, `VehiclePeriapsisAltitudePropertyWatcher` ; `VesselVehicle` (orbite) | — |
| P2 : ΔV / étages | `StagingDataProvider` | — |

Modes SAS (`AutopilotMode`) : StabilityAssist, Prograde, Retrograde, Normal, Antinormal, RadialIn, RadialOut, Target, AntiTarget, Maneuver, Navigation, Autopilot.

### 11.3 Points d'attention techniques
1. **Thread principal** : lire et écrire l'état du jeu uniquement depuis le thread Unity. Le serveur réseau tourne sur son propre thread ; les commandes entrantes sont mises en file puis exécutées dans la boucle de mise à jour (ex. un `MonoBehaviour.Update`).
2. **Pas de véhicule actif** (menu, centre spatial, EVA) : `ActiveVesselVehicle` peut être nul ; le mod envoie alors un état « aucun véhicule » (cf. §4.4).
3. **Contrôle perdu** : `VesselComponent.IsControllable` ; le SAS peut être suspendu (`ModeAwaitingControl`). Les commandes doivent être ignorées ou signalées dans ce cas.
4. **Timewarp** : le throttle peut être verrouillé (`VesselThrottleLockedDueToTimewarpingMessage`) ; l'état envoyé au Stream Deck doit en tenir compte.
5. **Retour d'état** : préférer l'abonnement aux messages (`ThrottleChangedMessage`, `SASEnabledMessage`, `RCSModeChangedMessage`…) à un sondage, avec un envoi périodique de secours (§4.4).
6. **Versions** : les chemins d'API ci-dessus sont ceux de la doc Redux beta ; ils peuvent différer de la v0.2.8.5.103184.
7. **Réseau** : serveur WebSocket / TCP local sous Unity (Mono) — bibliothèque à choisir (ex. implémentation WebSocket légère embarquée dans `lib/`), liée à `127.0.0.1` uniquement.

### 11.4 Vérification sur l'installation réelle (v0.2.8.5.103184-beta)
Méthode : inspection par réflexion (sans exécution) de `Assembly-CSharp.dll` du jeu installé dans `E:\SteamLibrary\steamapps\common\Kerbal Space Program 2\KSP2_x64_Data\Managed` (version confirmée par `Ksp2.log` : `0.2.8.5.103184-beta`, commit `ffc94930`).

**Confirmé présent** dans cette version :

| Élément | Résultat |
|---------|----------|
| `GameManager.Instance` / `.Game` / `GameInstance.ViewController` / `.Messages` | ✅ |
| `ViewController.ActiveVesselVehicle` (champ), `TryGetActiveSimVessel`, `GetActiveSimVessel(bool)` | ✅ |
| `VesselVehicle.AltitudeFromSeaLevel`, `AltitudeFromSurface` (double) | ✅ |
| `VesselVehicle.SurfaceSpeed`, `OrbitalSpeed`, `TargetSpeed` (double) | ✅ |
| `VesselVehicle.SpeedDisplayMode` + `SetSpeedDisplayMode` ; enum `Orbit` / `Surface` / `Target` | ✅ |
| `VesselVehicle.mainThrottle` (float, lecture) et `SetMainThrottle(float)` | ✅ |
| `VesselComponent.IsRCSEnabled` (get/set), `IsControllable` | ✅ |
| `VesselComponent.SetAutopilotEnableDisable(bool)`, `SetAutopilotMode(AutopilotMode)`, `AutopilotStatus`, `Autopilot` | ✅ |
| `VesselAutopilot.Enabled` (get/set), `AutopilotMode`, `GetStatus()` | ✅ |
| Messages `ThrottleChangedMessage`, `SASEnabledMessage`, `SASDisabledMessage`, `SASModeChangedMessage`, `RCSModeChangedMessage` | ✅ |
| `MessageCenter.Subscribe`, `PersistentSubscribe`, `Unsubscribe` | ✅ |
| `SpaceWarp2.dll` : `GeneralMod`, `ISpaceWarpMod`, clé `main_assembly` | ✅ |

**Différences avec la doc / les sources de la branche dev** :
- `OnCampaignLoaded` / `OnCampaignUnloading` : absents de la version installée.
- Le dossier `mods` existe dans le dossier du jeu (vide aujourd'hui) ; `Ksp2.log` contient une ancienne ligne « Unable to locate the mods directory! » (antérieure à sa création, à surveiller).

**Reste à valider par un test dans le jeu** : le comportement à l'exécution (valeurs retournées, thread, abonnement aux messages) ; la compatibilité du SDK Unity avec cette version (non nécessaire si nous compilons une DLL .NET en référençant directement les DLL du jeu).
