# Outils de développement

Nécessitent Node.js ≥ 20 : `cd tools && npm install`.

| Outil | Rôle |
|-------|------|
| `client.mjs` | Se connecte au mod (ou au faux serveur), affiche l'état en direct, envoie des commandes |
| `fake-server.mjs` | Imite le mod (protocole v1) pour développer le plugin Stream Deck sans lancer le jeu |

```bash
# Faux serveur : vol simulé (ou --scenario menu | cycle)
npm run fake-server

# Client : n'affiche un état que lorsqu'un élément « discret » change
# (poussée, SAS, RCS, mode de vitesse, statut, véhicule) ; les réponses ACK / ERREUR s'affichent toujours
npm run client

# Tous les états (10 par seconde), JSON brut, un seul état
npm run client -- --all
npm run client -- --raw --once
```

En mode interactif, le client accepte : `ping`, `throttle 0.5`, `throttle +0.1`, `sas on|off|toggle`,
`rcs on|off|toggle`, `speed surface|orbit|target`, ou un objet JSON.

Le faux serveur applique les commandes à son état simulé, avec la même validation que le vrai mod.
