# Recette des mods OrbitDeck Bridge

À dérouler pour chaque jeu (KSP 2 Redux, KSP 1) avec `"verboseLogging": true`. Deux façons d'observer : le plugin OrbitDeck sur le Stream Deck, ou le client en ligne de commande (`node tools/client.mjs`, voir [tools/README.md](../tools/README.md)) et le journal du jeu.

## 1. Démarrage
- [ ] Le journal affiche `[OrbitDeck] Mod chargé (…, port 18765, …)` puis `Serveur WebSocket à l'écoute sur ws://127.0.0.1:18765/`, sans avertissement ni erreur.
- [ ] Dans les menus (principal, centre spatial, éditeur) : `AUCUN VÉHICULE`.
- [ ] Un client qui se connecte reçoit `hello` (version du mod et du jeu) puis des états.

## 2. Lecture en vol
- [ ] Le nom du véhicule est lisible (traduit, pas `#autoLOC_…`).
- [ ] L'état passe à `Connected` quand un pilote ou une sonde est aux commandes, `NotControllable` sinon.
- [ ] Altitude mer et altitude sol suivent l'altimètre du jeu à l'ascension, y compris au-dessus de l'eau.
- [ ] Vitesse surface, orbite et cible correspondent au navball ; avec une cible sélectionnée, la vitesse cible varie.
- [ ] Le type de vitesse suit le navball quand on le change à la souris.
- [ ] Poussée, SAS (et son mode : stabilité, prograde, rétrograde…) et RCS suivent les touches du jeu.
- [ ] Changement de véhicule (touche `]`), vue carte, retour au centre spatial : les valeurs suivent sans erreur.

## 3. Commandes
- [ ] SAS : activer et couper depuis le Stream Deck ; l'état du jeu suit (voyant SAS du navball).
- [ ] Modes du SAS : prograde, rétrograde, normal, anti-normal, radial, anti-radial (navball en mode orbite pour les quatre derniers), cible et anti-cible (avec une cible), manœuvre (avec un nœud), stabilité. Le mode actif est lu en vert ; un mode indisponible est refusé avec la raison.
- [ ] Phares, freins, train d'atterrissage : l'état du jeu suit dans les deux sens (aussi quand on change au clavier). Sans pièce correspondante, la touche affiche « — ».
- [ ] Étage : une commande déclenche un étage (pré-lancement compris sur KSP 2) ; refusée en EVA, sur rails, ou si les étages sont verrouillés (Maj + L sur KSP 1).
- [ ] RCS : idem.
- [ ] Poussée : fixer à 0, 50 et 100 % ; la jauge du jeu suit et le véhicule accélère en conséquence.
- [ ] Poussée : +10 % et −10 % en rafale, bornée entre 0 et 100 %.
- [ ] Type de vitesse : surface, orbite, cible ; le navball suit ; « cible » sans cible est refusé.
- [ ] Une valeur hors bornes (par exemple une poussée de 5) est refusée (`BAD_REQUEST`), jamais corrigée en silence.

## 4. Cas refusés (le Stream Deck affiche une croix rouge, le jeu ne bronche pas)
- [ ] Au centre spatial ou dans un menu : `NO_VESSEL`.
- [ ] Kerbal en EVA : poussée, SAS et RCS refusés.
- [ ] Accélération du temps sur rails : poussée refusée.
- [ ] Véhicule sans pilote ni sonde : `NOT_CONTROLLABLE`.
- [ ] SAS sur un véhicule sans module adapté : refusé avec un message.

## 5. Robustesse
- [ ] Deux clients en même temps : tous deux reçoivent les états.
- [ ] Fermer et relancer le client, le plugin ou le jeu : la connexion se rétablit seule.
- [ ] Port réservé par Windows : le journal explique la cause et la marche à suivre.
- [ ] Aucune exception du mod dans le journal pendant toute la session.
