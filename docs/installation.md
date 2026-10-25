# Installation de StreamPlug

StreamPlug se compose de deux parties à installer : un **mod pour KSP 2 Redux** et un **plugin Stream Deck**.

**Prérequis** : Windows 10/11, KSP 2 Redux v0.2.8.5.103184-beta, logiciel Stream Deck 7.1 ou plus récent, Stream Deck à 15 touches (3 × 5).

Téléchargez les deux fichiers de la dernière [release](https://github.com/teddyzeus/StreamPlug/releases) :
- `StreamPlug-Mod-vX.Y.Z.zip` : le mod ;
- `com.teddyzeus.streamplug.streamDeckPlugin` : le plugin.

## 1. Installer le mod

1. Fermez le jeu.
2. Ouvrez le dossier d'installation de KSP 2 (Steam : clic droit sur le jeu → Gérer → Parcourir les fichiers locaux).
3. Dézippez `StreamPlug-Mod-vX.Y.Z.zip` **dans le dossier `mods`** du jeu (créez-le s'il n'existe pas). Vous obtenez :

```
<dossier du jeu>\mods\streamplug\
    StreamPlug.Mod.dll
    swinfo.json
    streamplug-config.json
```

4. Lancez le jeu. Dans son journal (`%USERPROFILE%\AppData\LocalLow\Intercept Games\Kerbal Space Program 2\Player.log`), cherchez la ligne `[StreamPlug] Serveur WebSocket à l'écoute sur ws://127.0.0.1:8765/`.

Le mod n'écoute que sur votre machine (`127.0.0.1`) : rien n'est exposé sur le réseau.

## 2. Installer le plugin Stream Deck

1. Double-cliquez sur `com.teddyzeus.streamplug.streamDeckPlugin`, puis confirmez l'installation dans le logiciel Stream Deck.
2. Dans la liste des actions (à droite), ouvrez la catégorie **StreamPlug**.

## 3. Placer les touches

Disposition recommandée pour un Stream Deck à 15 touches :

| | Col. 1 | Col. 2 | Col. 3 | Col. 4 | Col. 5 |
|---|---|---|---|---|---|
| **Ligne 1** | Altitude | Vitesse | État de la connexion | | |
| **Ligne 2** | SAS | RCS | Poussée | | |
| **Ligne 3** | Poussée : fixer (0 %) | Poussée : fixer (50 %) | Poussée : fixer (100 %) | Poussée : diminuer | Poussée : augmenter |

Pour les touches « Poussée : fixer », choisissez la valeur (0, 25, 50, 75 ou 100 %) dans l'interface de la touche. Pour « augmenter » et « diminuer », choisissez le pas (5, 10 ou 25 %).

## Utilisation

| Touche | Affichage | Appui |
|--------|-----------|-------|
| Altitude | Altitude au-dessus du sol (défaut) | Bascule sol ↔ niveau de la mer |
| Vitesse | Vitesse du type choisi dans le jeu (sol par défaut) | Type suivant : sol → orbite → cible (cible sautée sans cible) |
| État de la connexion | « EN LIGNE » + nom du véhicule, ou la raison de l'indisponibilité | Force une reconnexion |
| SAS / RCS | ON (vert) ou OFF | Active ou coupe |
| Poussée | Pourcentage et jauge | — |
| Poussée : fixer / augmenter / diminuer | Valeur de l'action | Applique la poussée |

- Les touches sont grisées avec « — » quand le jeu n'est pas lancé ou qu'aucun véhicule n'est piloté.
- Une **croix rouge** brève sur une touche signale une commande refusée (menu, accélération du temps, kerbal en EVA, jeu non lancé…).
- Le plugin se reconnecte tout seul quand le jeu démarre ou redémarre.

## Réglages

- **Port** : si le port 8765 est pris, modifiez `port` dans `<jeu>\mods\streamplug\streamplug-config.json`, puis indiquez le même port dans le champ « Port du mod » de n'importe quelle touche StreamPlug (réglage commun à toutes). Relancez le jeu.
- **Journal détaillé** : `"verboseLogging": true` dans le même fichier, pour diagnostiquer un problème.

## Dépannage

| Symptôme | Piste |
|----------|-------|
| « JEU NON LANCÉ » alors que le jeu tourne | Le mod n'est pas chargé : vérifiez le dossier `mods\streamplug\` et le journal du jeu. Vérifiez le port. Pare-feu : la connexion reste locale, aucune règle n'est nécessaire. |
| « AUCUN VÉHICULE » | Normal dans les menus, le centre spatial et l'éditeur : entrez en vol. |
| « PAS DE CONTRÔLE » | Le véhicule n'est pas pilotable (pas de pilote, contrôle insuffisant) : valeurs en jaune, commandes refusées. |
| Croix rouge à l'appui | Commande refusée par le jeu, voir le journal du plugin (logiciel Stream Deck → Plugins). |
| Après mise à jour du mod | Fermez et relancez le jeu : l'ancienne DLL reste en mémoire sinon. |

## Désinstallation

Supprimez le dossier `mods\streamplug\` du jeu et désinstallez le plugin StreamPlug dans le logiciel Stream Deck (Plugins → clic droit → Désinstaller).
