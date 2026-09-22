# Mordheim Ledger

*Read this in [English](README.md).*

Application mobile et bureau pour tenir **le grand livre** d'une campagne
[Mordheim](https://en.wikipedia.org/wiki/Mordheim) : historique de tes bandes, roster des
guerriers, trésorerie, blessures, avancement, et catalogue de règles maison.

Outil de fan, non commercial, sans lien avec Games Workshop (voir [Licence](#licence)).

> Contexte produit et suivi d'avancement détaillé : [ROADMAP.md](ROADMAP.md).
> Notes techniques et conventions de code : [CLAUDE.md](CLAUDE.md).

## Statut

En développement actif — **V1** (mono-appareil, sans compte, stockage 100 % local).
Fonctionnel de bout en bout : création de bande, recrutement, roster détaillé, assistant
de fin de partie (blessures graves, exploration, butin, XP/avancement). Le partage entre
joueurs (export/import, QR code) est prévu pour la V2.

## Stack

- **.NET 10** / **.NET MAUI** — cibles Android, iOS, Mac Catalyst et Windows
- **MVVM** via [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)
- **SQLite** local via [`sqlite-net-pcl`](https://github.com/praeclarum/sqlite-net)
- Tests : **xUnit**
- Localisation **fr / en** (`Strings/AppStrings*.resx`)

## Structure de la solution

| Projet | Rôle |
| --- | --- |
| `MordheimLedgerApp.Core` | Cœur sans dépendance MAUI : `Models/` (modèles purs), `Data/` (entités SQLite + `AppDatabase` + mapping + `SeedData/*.json`), `Services/` (CRUD `ILibraryService` / `IWarbandService`), `Rules/` (décisions de règles pures et testables) |
| `MordheimLedgerApp` | Tête MAUI : `Features/<Domaine>/` (Page + ViewModel), `Components/`, `Services/` (localisation / thème / chargement), `Resources/Styles/` (design tokens) |
| `MordheimLedgerApp.Tests` | Mapping entité ↔ modèle, tests d'intégration des services sur une base SQLite temporaire, tests de règles pures (`Core.Rules`, sans base) |
| `Tools/DbSeedGenerator` | Génération de la base de seed embarquée (`Resources/Raw/seed.db3`) |

### Deux mondes de données

- **Library** (`Models/Library/`) : le contenu **éditable** — types de bandes, types de
  guerriers, objets de la Place du Marché, compétences, sorts, mutations, blessures…
  Chaque ligne porte un `ContentSource` (`Official` / `Modified` / `Custom`). Éditer une
  entrée officielle la bascule en `Modified`.
- **Campagne** (`Models/`) : les **instances jouées** — `Campaign`, `Warband`, `Warrior`,
  `WarriorEquipment`. Recruter un guerrier copie les stats de l'archétype à cet instant ;
  le guerrier évolue ensuite indépendamment.

### Contenu officiel embarqué

15 bandes seedées depuis le livre de règles via le pipeline JSON `Data/SeedData/`
(Reiklander / Middenheim / Marienburg, Averlanders, Ostlanders, Morts-Vivants, Chasseurs
de Trésors Nains, Kermesse du Chaos, Culte des Possédés, Pillards Hommes-Bêtes, Horde
Orque, Répurgateurs, Skavens du Clan Eshin, Sœurs de Sigmar, Kislévites), plus les
Épées à Louer et les Personnages Dramatis, et les catalogues communs (règles spéciales,
équipement, compétences, mutations, écoles de magie).

## Build & tests

Sur Windows (seule cible testable en local) :

```bash
dotnet build MordheimLedgerApp/MordheimLedgerApp.csproj -f net10.0-windows10.0.19041.0
```

```bash
dotnet test MordheimLedgerApp.Tests
```

## Contribution

Développement par branche de fonctionnalité (`feature/<nom>`), pas directement sur
`master`. Voir [CLAUDE.md](CLAUDE.md) pour les conventions (terminologie anglaise
officielle du jeu, règles nouvelles à placer dans `Core/Rules/`, messages de commit
courts…).

## Licence

Propriétaire — tous droits réservés, © 2026 Cyril Bezard-Falgas. Le dépôt est public
pour référence uniquement ; sa visibilité ne vaut pas licence d'utilisation. Voir
[LICENSE](LICENSE).

Mordheim, Warhammer et les noms, termes et images associés sont des marques et/ou
œuvres protégées de Games Workshop Limited. Projet de fan non officiel, non commercial,
sans affiliation ni approbation de Games Workshop.
