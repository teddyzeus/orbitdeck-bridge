# OrbitDeck Bridge

Mod gratuit et open source (licence MIT) pour **Kerbal Space Program 1** et **Kerbal Space Program 2 (Redux)** : il expose en local les données du jeu et reçoit des commandes, par WebSocket sur `127.0.0.1:18765`. Il alimente le plugin Stream Deck **OrbitDeck**, mais le protocole est documenté et ouvert : vous pouvez écrire votre propre client.

> Statut : version 0.4.0. **[Télécharger la dernière version](https://github.com/teddyzeus/orbitdeck-bridge/releases/latest)**, puis suivre [docs/installation.md](docs/installation.md).

## Ce que le mod fournit

- **Lecture** : altitude (mer et sol), vitesse (sol, orbite, cible), poussée, SAS et son mode, RCS, phares, freins, train d'atterrissage, état du véhicule.
- **Commandes** : poussée (fixer, ajuster), SAS et ses modes (stabilité, prograde, rétrograde, normal, anti-normal, radial, anti-radial, cible, anti-cible, manœuvre), RCS, phares, freins, train d'atterrissage, type de vitesse du navball, déclenchement de l'étage suivant.
- 100 % local : le serveur n'écoute que sur `127.0.0.1`, vérifie `Host` et `Origin`, limite la taille des messages et le nombre de clients.

## Documentation

- [Installation](docs/installation.md) (KSP 1 et KSP 2)
- [Protocole](protocol/README.md) (schémas JSON et exemples)
- [Recette de test](docs/test-checklist.md)
- [Spécifications techniques](SPECS-TECHNIQUES.md) (KSP 2) et [étude de faisabilité KSP 1](docs/ksp1-faisabilite.md)

## Compatibilité

| Jeu | Version testée | Dossier du mod |
|-----|----------------|----------------|
| KSP 2 (Redux) | v0.2.8.5.103184-beta | `mods\orbitdeck\` |
| KSP 1 | 1.12.5 | `GameData\OrbitDeck\` |

Windows 10/11.

## Développement

- **Mod KSP 2** : `mod/src/OrbitDeck.Mod` ; **mod KSP 1** : `mod/src/OrbitDeck.Ksp1`. SDK .NET 8, DLL des jeux référencées via `KSP2_DIR` / `KSP1_DIR` ou `mod/Directory.Build.props.user`. Le serveur, le protocole et les commandes sont communs : le projet KSP 1 compile les mêmes fichiers (dossiers `Server`, `Protocol`, `Commands`, `Config`, `Telemetry` de `OrbitDeck.Mod`).
- **Tests** : `dotnet test mod/OrbitDeck.Mod.sln`.
- **Release** : `node scripts/package.mjs` produit `release/OrbitDeck-Bridge-KSP2-vX.Y.Z.zip` et `release/OrbitDeck-Bridge-KSP1-vX.Y.Z.zip`.
- **Outils** : `tools/` (client et faux serveur en ligne de commande, pour essayer le protocole sans le jeu).

## Licence

[MIT](LICENSE). Le nom « OrbitDeck » et le plugin Stream Deck ne sont pas couverts par cette licence. Le mod KSP 1 embarque Newtonsoft.Json (licence MIT, voir `LICENSE-Newtonsoft.Json.txt` dans l'archive).
