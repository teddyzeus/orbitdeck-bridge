# Recette StreamPlug (MVP)

À dérouler avec le jeu (KSP 2 Redux 0.2.8.5) et le Stream Deck 15 touches. Cochez chaque ligne ; notez ce qui diffère.

## Disposition (SPECS.md §4.6)

| | Col. 1 | Col. 2 | Col. 3 | Col. 4 | Col. 5 |
|---|---|---|---|---|---|
| **Ligne 1** | Altitude | Vitesse | État de la connexion | | |
| **Ligne 2** | SAS | RCS | Poussée (affichage) | | |
| **Ligne 3** | Poussée : fixer (0 %) | Poussée : fixer (50 %) | Poussée : fixer (100 %) | Poussée : diminuer | Poussée : augmenter |

Les actions sont dans la catégorie **StreamPlug**. Pour les touches « fixer », choisir 0, 50 et 100 % dans l'interface de la touche.

## 1. Démarrage
- [ ] Stream Deck lancé, jeu fermé : toutes les touches sont grises avec « — », l'état affiche « JEU NON LANCÉ ».
- [ ] Lancer le jeu : l'état passe à « AUCUN VÉHICULE » dans les menus, sans redémarrer le plugin.

## 2. Vol sur Kerbin
- [ ] Charger une fusée sur le pas de tir : l'état affiche « EN LIGNE » et le nom du véhicule.
- [ ] Altitude (sol) et vitesse (sol) correspondent aux valeurs du jeu.
- [ ] Appui sur Altitude : bascule sol ↔ mer ; la valeur correspond au jeu.
- [ ] Appui sur Vitesse : surface → orbite ; le navball du jeu suit. Avec une cible : orbite → cible → surface.

## 3. Commandes
- [ ] SAS : appui = le SAS du jeu s'allume/s'éteint ; la touche passe au vert/OFF ; fonctionne aussi si on le change au clavier.
- [ ] RCS : idem.
- [ ] Poussée 100 % / 50 % / 0 % : la poussée du jeu suit ; la touche de la valeur atteinte passe en bleu.
- [ ] Poussée +10 % / −10 % : paliers de 10 %, bornés entre 0 et 100 % (appuis répétés à 100 % sans erreur).
- [ ] Changer le pas à 25 % dans l'interface : le texte de la touche et l'effet changent.

## 4. Cas refusés (la touche affiche une croix rouge brève)
- [ ] Menu principal / centre spatial : appui sur SAS ou une poussée → croix rouge, aucun effet.
- [ ] Accélération du temps physique : poussée refusée (croix rouge).
- [ ] Vaisseau sans pilote/contrôle : valeurs en jaune, « PAS DE CONTRÔLE ».
- [ ] EVA d'un kerbal : poussée refusée.

## 5. Changements de contexte
- [ ] Changer de véhicule en vol (touche `]`) : nom, valeurs, SAS/RCS suivent.
- [ ] Vue carte : les valeurs continuent de se mettre à jour.
- [ ] Éditeur de véhicule : « AUCUN VÉHICULE », commandes en croix rouge.
- [ ] Retour au menu puis rechargement d'une sauvegarde : les touches se rétablissent seules.

## 6. Connexion
- [ ] Fermer le jeu en vol : les touches passent en « JEU NON LANCÉ » en moins de 5 s.
- [ ] Relancer le jeu : les touches reviennent seules, sans toucher au plugin. Un appui sur « État » force une reconnexion immédiate.
- [ ] Changer le port dans l'interface d'une touche (mauvais port) : hors ligne ; remettre 8765 : reconnecté.

## 7. Confort
- [ ] Lisibilité à bout de bras : libellés et valeurs (km, Mm) lisibles.
- [ ] Pas de lag perceptible entre la poussée du jeu et l'affichage ; l'écran du Stream Deck ne scintille pas.
