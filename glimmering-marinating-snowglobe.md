# Pilot import: Morts-vivants + Chasseurs de Trésors Nains

## Context

Following the bilingual translation system (FR/EN, already built and committed), the next step is
importing the full official Mordheim catalogue. The user provided 13 warbands as raw FR text extracts
(fan translations, reproduced close to verbatim per explicit instruction — fan license, no copyright
concern) and proposed mordheimer.net (confirmed reachable via the in-app browser, a well-organized
Docusaurus site: Rules/Warbands/Armoury/Trading Post, tiered core/grade-1a/1b/1c) as the EN structural
reference, matching the existing bilingual design (EN authoritative structure, FR paired translation).
Goal throughout: **stay as close as possible to the established rules so players aren't lost.**

Given the scale (13 warbands × warriors/equipment/skills/spells/special rules), we're piloting with 2
structurally distinct warbands before replicating 13×:
- **Morts-vivants** (Undead): self-contained, complete Nécromancie spell table (6 entries) — validates
  the new Spell type end-to-end. Rich Hero/Henchman special-rules variety (Peur, Immunité Psychologie/
  Poison, no-XP flags on Zombies/Loups Funestes).
- **Chasseurs de Trésors Nains**: no magic at all (isolates the Spell variable), has one cleanly
  single-warband-restricted item (Hache Naine, "Nains seulement") — validates the new equipment-
  restriction concept independently.

Two new modeling gaps surfaced during scoping (both confirmed with the user):
1. Prières/Rituels/Magie aren't modeled at all today (free text only) → **new 6th Library type: Spell**.
2. Some equipment/skills are canon-restricted to specific warband types → **add a restriction concept
   to EquipmentItem and Skill**, extending to the model.

Confirmed via Explore agent: the 5 existing Library types share **no abstraction** — every layer (Get/
Save/Delete, picker services, ViewModels, Selector pages, DI registrations, LibraryPage tabs) is hand-
duplicated per type. A 6th type is mechanical but touches ~10-12 files. `WarriorArchetype` has **zero**
existing spellcaster signal (grepped exhaustively — purely free text like "sort aléatoire si Sorcier").
The many-to-many join pattern already exists (`WarriorEquipmentEntity`/`WarriorSkillEntity`/
`WarriorInjuryEntity` — manual `FindAsync` loops, no real SQL joins, sqlite-net-pcl limitation) and is
the template to replicate for the new restriction tables.

## Architecture

### 1. New Spell Library type
- `MordheimLedgerApp.Core/Models/Library/Spell.cs`: `Id, Name (resolved), Description (resolved),
  NameKey, DescriptionKey, RollValue (int - the D6/2D6 result, e.g. 1-6), Difficulty (int? - target
  number, nullable since not all tables use one), SpellListName (string - e.g. "Nécromancie", groups
  entries into their table), Source (ContentSource), ImagePath`.
  - `SpellListName` stays a plain string (not a separate normalized entity) for pilot simplicity — a
    spell list is just a shared grouping label. Revisit if unwieldy once scaled to 13 warbands.
- `Data/Entities/Library/SpellEntity.cs`: mirrors, `NameKey`/`DescriptionKey` instead of `Name`/
  `Description` (same pattern as the other 5 types).
- `EntityMapping.cs`: add `ToModel(translations)`/`ToEntity` pair for Spell.
- `ILibraryService`/`LibraryService`: add `GetSpellsAsync(languageCode)`/`SaveSpellAsync(spell,
  languageCode)`/`DeleteSpellAsync(id)` — mechanical, mirrors the other 5 exactly (including the
  `ApplyTranslationsAsync(Spell, languageCode)` overload).
- `AppDatabase.cs`: register `CreateTableAsync<SpellEntity>()`.
- New UI under `Features/Library/Spells/` (SpellViewModel/SpellRow/SpellView.xaml/SpellEditDialog),
  mirroring Injury's shape (closest existing analog — flat list, no category enum, but Spells filter by
  `SpellListName` the way Equipment/Skill filter by category). Added as a 5th toggle section in
  `LibraryPage.xaml`/`LibraryViewModel` (`SelectedTab` 0-4, `IsSpellsTab`) — same pattern already
  established for the other 4.

### 2. WarriorArchetype gains a spellcasting link
- Add `SpellListName` (`string?`, nullable) to `WarriorArchetype`/`WarriorArchetypeEntity` — null = not
  a spellcaster; non-null = "this archetype rolls on Spell entries whose `SpellListName` matches" (e.g.
  Nécromancien → `"Nécromancie"`). A simple string match avoids a many-to-many join for what's really a
  1:1 relationship (one archetype → one list).
- No runtime UI change for rolling/casting this pilot (matches "no rules engine V1") — the data just
  needs to exist and be browsable via the new Spells tab for reference. No change to `Warrior` itself.

### 3. Warband-restriction on EquipmentItem and Skill
- New join entities, exact shape of `WarriorEquipmentEntity`: `WarbandArchetypeEquipmentEntity (Id,
  WarbandArchetypeId [Indexed], EquipmentItemId)` and `WarbandArchetypeSkillEntity (Id,
  WarbandArchetypeId [Indexed], SkillId)`.
- `EquipmentItem`/`Skill` models gain `RestrictedToWarbandArchetypeIds` (`List<int>`, empty = common to
  all — matches current behavior for every already-seeded item unchanged).
- `LibraryService.GetEquipmentItemsAsync`/`GetSkillsAsync`: bulk-load the whole restriction join table
  once (same "load whole table, filter in-memory" idiom as `TranslationResolver.ResolveAsync` — avoids
  N+1 `FindAsync` calls per item) and attach matching ids before returning.
- `SaveEquipmentItemAsync`/`SaveSkillAsync`: replace-all the item's restriction rows on save (delete
  existing for that item, insert current list) — no diffing needed at this scale.
- **Explicitly out of scope this pilot**: enforcing the restriction in the Equipment/Skill picker
  (Trading Post / skill picker filtered to the warband being edited). Confirmed via Explore agent this
  needs a new parameter threaded through 4 layers × 2 pickers (`I*PickerService` → impl → new settable
  property on the shared `*ViewModel`, mirroring `IsSelectorMode` → `WarriorEditDialogViewModel`'s
  `AddEquipment`/`AddSkill` call sites) — real work, deliberately deferred. This pass: restriction is
  **data-only**, shown as a badge/note on the Library tile (e.g. "Nains uniquement"), not enforced.

### 4. Seeding: move to embedded JSON
`OfficialContentSeed.cs`'s hand-written C# shape (~90 lines for 1 warband) doesn't scale to 13. Move to
embedded JSON resources: `MordheimLedgerApp.Core/Data/SeedData/*.json` (one file per warband, marked
`EmbeddedResource` in the `.csproj`), parsed by `AppDatabase.SeedOfficialContentAsync()` at first launch
(replacing the hardcoded warband/warrior/equipment loops, reusing the existing `SeedTranslationAsync`
helper unchanged for bilingual key allocation). Reiklander Mercenaries (`OfficialContentSeed.cs`) is
left as-is for this pilot — not migrated to JSON yet, just proves the two schemes coexist.

Each warband JSON carries: warband archetype (name/description EN+FR, starting treasury, max warriors),
warrior archetypes (name EN+FR, full stat line, cost, maxCount, startingExperience, description EN+FR,
`spellListName` if applicable), equipment additions (name EN+FR, category, cost, rarity, description
EN+FR, `restrictedToThisWarband` flag), spells where applicable (list name, roll value, difficulty,
name/description EN+FR).

**Sourcing**: EN names/wording from mordheimer.net's per-warband pages (fetched during implementation,
browser-based since WebFetch gets 403'd — `preview_start`/`get_page_text` as used during this session's
research); FR text from the provided extracts, reproduced close to verbatim.

## Files (representative — full pattern repeats per the 6th-type shape already established for the 5
existing types)

- New: `Models/Library/Spell.cs`, `Data/Entities/Library/SpellEntity.cs`,
  `Data/Entities/WarbandArchetypeEquipmentEntity.cs`, `Data/Entities/WarbandArchetypeSkillEntity.cs`,
  `Data/SeedData/MortsVivants.json`, `Data/SeedData/ChasseursDeTresorsNains.json`,
  `Features/Library/Spells/*` (ViewModel/Row/View/EditDialog, mirroring `Features/Library/Injuries/*`)
- Modified: `EntityMapping.cs`, `ILibraryService.cs`/`LibraryService.cs`, `AppDatabase.cs`,
  `Models/Library/WarriorArchetype.cs` + `WarriorArchetypeEntity.cs` (`SpellListName`),
  `Models/Library/EquipmentItem.cs`/`Skill.cs` + entities (`RestrictedToWarbandArchetypeIds`),
  `LibraryPage.xaml`/`LibraryViewModel.cs` (5th tab), `MauiProgram.cs` (DI registrations),
  `MordheimLedgerApp.Core.csproj` (embed `SeedData/*.json`)

## Verification

1. `dotnet build` + `dotnet test` as usual.
2. Delete the local `.db3`, launch, confirm Morts-vivants and Chasseurs de Trésors Nains both appear in
   Codex > Bandes with correct warriors/stats in French (default) and English (after switching in
   Réglages).
3. Confirm the 6 Nécromancie entries appear under the new Spells tab, and that the Nécromancien
   `WarriorArchetype` carries `SpellListName = "Nécromancie"`.
4. Confirm Hache Naine shows a "Nains uniquement" restriction badge, and confirm it is **not** hidden
   from other warbands' equipment picker (restriction is data-only this pass — that's expected, not a
   bug).
5. Confirm Reiklander Mercenaries (existing hardcoded seed) still works unaffected — proves the two
   seeding mechanisms (hardcoded C# + JSON) coexist cleanly.
