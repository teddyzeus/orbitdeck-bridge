# Étude de faisabilité : mod OrbitDeck pour KSP 1

Objectif : un second mod, « OrbitDeck Bridge pour KSP 1 », qui parle le **même protocole** que le mod KSP 2, pour que le plugin Stream Deck fonctionne tel quel avec KSP 1.

Cette étude vient de la lecture (décompilation en lecture seule) de l'installation Steam réelle. Rien n'a été exécuté dans le jeu : les points marqués 🔶 sont à valider par un test.

## 1. Cible

| Élément | Valeur constatée |
|---|---|
| Version du jeu | **KSP 1.12.5** (build 03190, Steam) |
| Moteur | Unity **2019.4.18f1**, 64 bits |
| Runtime | Mono (profil .NET 4.x) : `mscorlib`, `System` 4.6.57 ; **pas de `netstandard.dll`** dans `Managed/` |
| Mods installés | aucun autre que `GameData/Squad` (pas de ModuleManager, pas de Harmony) |
| Chargement des mods | DLL placée dans `GameData/<Mod>/Plugins/`, classe `MonoBehaviour` marquée `[KSPAddon(...)]` |

Conséquence : le mod KSP 2 est compilé en `netstandard2.1`, que KSP 1 ne sait pas charger. Le mod KSP 1 sera compilé en **`net472`** contre les DLL de `KSP_x64_Data/Managed`.

## 2. Données et commandes : correspondance avec l'API de KSP 1

| Besoin | KSP 2 (actuel) | KSP 1 (trouvé dans `Assembly-CSharp`) | Risque |
|---|---|---|---|
| Véhicule actif | `ViewController.ActiveVesselVehicle` | `FlightGlobals.ActiveVessel` | faible |
| En vol ? | `GameState.IsFlightMode` | `HighLogic.LoadedSceneIsFlight` | faible |
| Altitude mer | `AltitudeFromSeaLevel` | `Vessel.altitude` (double) | faible |
| Altitude sol | `AltitudeFromSurface` | `Vessel.radarAltitude` (double) ou `Vessel.heightFromTerrain` (float) | 🔶 choisir celui qui colle à l'altimètre du jeu |
| Vitesse sol / orbite / cible | `SurfaceSpeed` / `OrbitalSpeed` / `TargetSpeed` | `Vessel.srfSpeed` / `Vessel.obt_speed` / `FlightGlobals.ship_tgtSpeed` | faible |
| Type de vitesse du navball | `SpeedDisplayMode` | `FlightGlobals.speedDisplayMode` (`Surface`/`Orbit`/`Target`) | faible en lecture |
| Changer ce type | `SetSpeedDisplayMode` | `FlightGlobals.SetSpeedMode(...)` (appelé en interne) | 🔶 vérifier la signature et la visibilité |
| Poussée (lecture) | `mainThrottle` | `FlightInputHandler.state.mainThrottle` / `Vessel.ctrlState.mainThrottle` | faible |
| Poussée (écriture) | `SetMainThrottle` | à vérifier | 🔶 **le plus délicat** : `FlightInputHandler` réécrit `state.mainThrottle` à chaque image depuis sa propre valeur. Il faudra soit écrire cette valeur, soit passer par un rappel « fly-by-wire » du véhicule (méthode courante des mods de pilotage) |
| SAS (lecture / écriture) | `AutopilotStatus`, `SetAutopilotEnableDisable` | `Vessel.Autopilot.Enabled`, `Enable()`, `Disable()`, `SetMode(...)`, `CanSetMode(...)` ; ou le groupe d'action `KSPActionGroup.SAS` | moyen : le SAS exige un pilote ou une sonde adaptée |
| RCS (lecture / écriture) | `IsRCSEnabled`, `SetState` | `Vessel.ActionGroups[KSPActionGroup.RCS]`, `SetGroup(...)`, `ToggleGroup(...)` | faible |
| Contrôlable ? | `IsControllable` | `Vessel.IsControllable` | faible |
| Cible | `TargetObject` | `Vessel.targetObject` | faible |
| Thread | thread principal Unity | idem (`MonoBehaviour.Update`) | aucun |

Le protocole n'a **aucun terme propre à KSP 2** hormis des valeurs informatives (`gameState`) : il n'a pas besoin de changer pour la v1. Le message `hello` indique déjà le jeu ; il faudra y mettre la version de KSP 1.

## 3. Ce qui se réutilise du mod KSP 2

| Réutilisable tel quel (même code source) | À réécrire pour KSP 1 |
|---|---|
| Serveur WebSocket (`Server/`) | Point d'entrée (`[KSPAddon]` à la place de la classe du chargeur Redux) |
| Protocole et validation (`Protocol/`, `CommandValidator`) | `GameAccess`, `GameState`, `GameEventWatcher` (lecture du jeu) |
| File de commandes (`CommandQueue`) | `GameControls` (exécution des commandes) |
| `SnapshotComparer`, `TelemetrySnapshot`, interfaces | Fichier projet (cible `net472`, références vers `KSP_x64_Data/Managed`) |
| Configuration (`Config/`) | Métadonnées de mod (`swinfo.json` n'existe pas : un fichier `.version`/CKAN à la place), emplacement du fichier de configuration |
| Tests unitaires (`net8.0`) | Tests d'intégration en jeu (à faire par vous) |

Le code est déjà découpé par interfaces (`ITelemetrySource`, `IGameControls`, `IGameState`) : le travail est surtout une **nouvelle implémentation de ces interfaces**.

Le plugin Stream Deck ne change pas, sauf le libellé « KSP » déjà neutre.

## 4. Organisation du dépôt proposée

```
mod/
  shared/        code commun (serveur, protocole, file de commandes) — compilé pour netstandard2.1 et net472
  ksp2/          mod KSP 2 Redux (existant, renommé)
  ksp1/          mod KSP 1
protocol/        inchangé
```

Le plus simple : le projet KSP 1 inclut les fichiers communs par lien (`<Compile Include=... Link=...>`), sans refondre le projet KSP 2. À trancher au début de l'étape K1.

## 5. Risques

1. **Écriture de la poussée** (voir §2) : seul point où l'approche n'est pas évidente. À prototyper en premier.
2. **Compatibilité du code commun avec Mono 4 / C# du compilateur** : le code utilise C# 9 et des API de `netstandard2.1` (par exemple `Span`, certaines méthodes de `Stream`). 🔶 Il faudra les remplacer ou les isoler pour `net472`.
3. **Écosystème KSP 1** : d'autres mods de télémétrie existent déjà (par exemple Telemachus Reborn, kRPC). Je ne les ai pas comparés ; il faut vérifier si un affichage Stream Deck y est déjà proposé et s'en différencier.
4. **Distribution** : les mods KSP 1 passent habituellement par CKAN et SpaceDock. Une release GitHub suffit pour démarrer ; CKAN demande un fichier de métadonnées.
5. **Marques** : « Kerbal » et « KSP » appartiennent à Take-Two. Ne pas les mettre dans le nom du produit.

## 6. Plan proposé

| Étape | Contenu | Validation |
|---|---|---|
| K0 | Cet état des lieux ✅ ; choix du découpage (§4) | vous |
| K1 | Projet `ksp1` compilable (`net472`), mod « hello » chargé par KSP 1, journal | 🧪 vous (journal) |
| K2 | Lecture : altitude, vitesse, type de vitesse, poussée, SAS, RCS, contrôlable | 🧪 vous (client en ligne de commande) |
| K3 | Serveur et protocole branchés (code commun) | 🧪 vous |
| K4 | Commandes : SAS, RCS, type de vitesse, puis poussée (point délicat) | 🧪 vous |
| K5 | Plugin Stream Deck avec KSP 1, recette complète, packaging, release | 🧪 vous |

Même mode de travail que pour KSP 2 : vous testez dans le jeu après chaque étape et vous m'envoyez les journaux.

## 7. Conclusion

**Faisable, effort moyen** : la partie la plus lourde (serveur, protocole, tests) existe déjà. Les inconnues sont l'écriture de la poussée et la compatibilité du code commun avec le runtime Mono de KSP 1, deux points qu'on lève dès K1/K4.
