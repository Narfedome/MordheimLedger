# Mordheim Ledger

*Lire en [français](README.fr.md).*

A mobile and desktop app for keeping **the ledger** of a
[Mordheim](https://en.wikipedia.org/wiki/Mordheim) campaign: the history of your warbands,
warrior rosters, treasury, injuries, advancement, and a catalogue of house rules.

A non-commercial fan tool, unaffiliated with Games Workshop (see [License](#license)).

> Product context and detailed progress log: [ROADMAP.md](ROADMAP.md) (French).
> Technical notes and code conventions: [CLAUDE.md](CLAUDE.md) (French).

## Status

Under active development — **V1** (single device, no account, fully local storage).
Working end to end: warband creation, recruitment, detailed roster, end-of-game wizard
(serious injuries, exploration, loot, XP / advancement). Sharing between players
(export/import, QR code) is planned for V2.

## Stack

- **.NET 10** / **.NET MAUI** — targets Android, iOS, Mac Catalyst and Windows
- **MVVM** via [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)
- Local **SQLite** via [`sqlite-net-pcl`](https://github.com/praeclarum/sqlite-net)
- Tests: **xUnit**
- **fr / en** localization (`Strings/AppStrings*.resx`)

## Solution layout

| Project | Role |
| --- | --- |
| `MordheimLedgerApp.Core` | MAUI-free core: `Models/` (pure models), `Data/` (SQLite entities + `AppDatabase` + mapping + `SeedData/*.json`), `Services/` (`ILibraryService` / `IWarbandService` CRUD), `Rules/` (pure, testable rule decisions) |
| `MordheimLedgerApp` | MAUI head: `Features/<Domain>/` (Page + ViewModel), `Components/`, `Services/` (localization / theme / loading), `Resources/Styles/` (design tokens) |
| `MordheimLedgerApp.Tests` | Entity ↔ model mapping, service integration tests against a throwaway SQLite database, pure-rules tests (`Core.Rules`, no database) |
| `Tools/DbSeedGenerator` | Builds the embedded seed database (`Resources/Raw/seed.db3`) |

### Two data worlds

- **Library** (`Models/Library/`): the **editable** content — warband types, warrior types,
  Trading Post items, skills, spells, mutations, injuries… Every row carries a
  `ContentSource` (`Official` / `Modified` / `Custom`). Editing an official entry flips it
  to `Modified`.
- **Campaign** (`Models/`): the **played** instances — `Campaign`, `Warband`, `Warrior`,
  `WarriorEquipment`. Recruiting a warrior copies the archetype's stats at that moment; the
  warrior then evolves independently.

### Bundled official content

15 warbands seeded from the rulebook through the `Data/SeedData/` JSON pipeline (Reikland /
Middenheim / Marienburg, Averlanders, Ostlanders, Undead, Dwarf Treasure Hunters, Carnival
of Chaos, Cult of the Possessed, Beastmen Raiders, Orc Mob, Witch Hunters, Skaven of Clan
Eshin, Sisters of Sigmar, Kislevites), plus Hired Swords and Dramatis Personae, and the
shared catalogues (special rules, equipment, skills, mutations, magic schools).

## Build & tests

On Windows (the only target that builds locally here):

```bash
dotnet build MordheimLedgerApp/MordheimLedgerApp.csproj -f net10.0-windows10.0.19041.0
```

```bash
dotnet test MordheimLedgerApp.Tests
```

## Contributing

Feature-branch workflow (`feature/<name>`), never straight onto `master`. See
[CLAUDE.md](CLAUDE.md) for the conventions (official English game terminology, new rules go
in `Core/Rules/`, short commit messages…).

## License

Proprietary — all rights reserved, © 2026 Cyril Bezard-Falgas. The repository is public for
reference only; its visibility does not grant a license to use its contents. See
[LICENSE](LICENSE).

Mordheim, Warhammer and all associated names, terms and imagery are trademarks and/or
copyrights of Games Workshop Limited. This is an unofficial, non-commercial fan project,
not affiliated with or endorsed by Games Workshop.
