# OrbitDeck Bridge

Mod gratuit et open source (licence MIT) qui expose en local les données de **Kerbal Space Program 2 (KSP 2 Redux)** et reçoit des commandes, par WebSocket sur `127.0.0.1:8765`. Il alimente le plugin Stream Deck **OrbitDeck**, mais le protocole est documenté et ouvert : vous pouvez écrire votre propre client.

> Statut : version 0.2.0. **[Télécharger la dernière version](https://github.com/teddyzeus/orbitdeck-bridge/releases/latest)**, puis suivre [docs/installation.md](docs/installation.md).

## Ce que le mod fournit

- **Lecture** : altitude (mer et sol), vitesse (sol, orbite, cible), poussée, SAS, RCS, état du véhicule.
- **Commandes** : poussée (fixer, ajuster), SAS, RCS, type de vitesse du navball.
- 100 % local : le serveur n'écoute que sur `127.0.0.1`, vérifie `Host` et `Origin`, limite la taille des messages et le nombre de clients.

## Documentation

- [Installation](docs/installation.md)
- [Protocole](protocol/README.md) (schémas JSON et exemples)
- [Spécifications techniques](SPECS-TECHNIQUES.md)

## Compatibilité

KSP 2 Redux v0.2.8.5.103184-beta, Windows 10/11. Un mod pour KSP 1 est prévu dans ce même dépôt.

## Développement

- **Mod KSP 2** : `mod/src/OrbitDeck.Mod` ; **mod KSP 1** (en cours) : `mod/src/OrbitDeck.Ksp1` (SDK .NET 8, DLL des jeux référencées via `KSP2_DIR` / `KSP1_DIR` ou `mod/Directory.Build.props.user`), tests : `dotnet test mod/OrbitDeck.Mod.sln`.
- **Release** : `node scripts/package.mjs` produit `release/OrbitDeck-Bridge-vX.Y.Z.zip`.
- **Outils** : `tools/` (client et faux serveur en ligne de commande, pour essayer le protocole sans le jeu).

## Licence

[MIT](LICENSE). Le nom « OrbitDeck » et le plugin Stream Deck ne sont pas couverts par cette licence.
