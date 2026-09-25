using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using MordheimLedgerApp.Core.Data.Entities;
using MordheimLedgerApp.Core.Data.Entities.Library;
using MordheimLedgerApp.Core.Models.Library;
using SQLite;

namespace MordheimLedgerApp.Core.Data;

public class AppDatabase
{
    private readonly SQLiteAsyncConnection _db;
    public SQLiteAsyncConnection Connection => _db;

    /// <summary>
    /// Table creation, run at construction. Data services must await this before their first query:
    /// SQLiteAsyncConnection does not guarantee ordering between operations, so a query issued before
    /// init completes could hit "no such table" on first launch.
    /// </summary>
    public Task Initialization { get; }

    public AppDatabase(string path)
    {
        _db = new SQLiteAsyncConnection(path);
        // Toute écriture ORM (Insert/Update/Delete, d'où qu'elle vienne) évince la table concernée du
        // cache - les DELETE en SQL brut ne déclenchent pas cet événement, voir InvalidateCachedTable.
        _db.GetConnection().TableChanged += (_, e) => InvalidateCachedTable(e.Table.MappedType);
        Initialization = InitializeAsync();
    }

    // --- Cache de lecture (2026-09-24) -------------------------------------------------------------
    // Le catalogue (Library) est relu en entier à chaque Get*Async - et chaque Get*Async en rappelle
    // d'autres (GetEquipmentItemsAsync -> GetSpecialRulesAsync, GetDramatisPersonaeAsync -> équipement +
    // compétences + règles...), si bien que l'ouverture du wizard Fin de Partie relisait la même table
    // d'équipement ~5 fois et les règles spéciales ~10 fois. On ne cache que les LIGNES BRUTES (entités)
    // et les traductions, jamais les modèles : chaque appel reconstruit des modèles neufs, donc aucun
    // écran ne partage d'instance mutable avec un autre. Les listes renvoyées ne doivent pas être
    // modifiées (IReadOnlyList), ni les entités qu'elles contiennent (les Save* relisent via FindAsync).
    private readonly ConcurrentDictionary<Type, Task<object>> _tableCache = new();

    /// <summary>Contenu complet de la table T, lu une fois puis servi depuis la mémoire jusqu'à la
    /// prochaine écriture sur T. Réservé aux tables du catalogue et aux traductions (petites, lues bien
    /// plus souvent qu'écrites).</summary>
    internal async Task<IReadOnlyList<T>> CachedTableAsync<T>() where T : new()
    {
        var task = _tableCache.GetOrAdd(typeof(T), async _ => (object)await _db.Table<T>().ToListAsync());
        try
        {
            return (IReadOnlyList<T>)await task;
        }
        catch
        {
            // Ne pas garder une lecture en échec en cache pour toujours.
            _tableCache.TryRemove(new KeyValuePair<Type, Task<object>>(typeof(T), task));
            throw;
        }
    }

    /// <summary>À appeler après tout DELETE/UPDATE en SQL brut (ExecuteAsync) sur une table cachée -
    /// seules les écritures ORM passent par TableChanged.</summary>
    internal void InvalidateCachedTable(Type entityType) => _tableCache.TryRemove(entityType, out _);

    internal void InvalidateCachedTable<T>() => InvalidateCachedTable(typeof(T));

    private void InvalidateAllCachedTables() => _tableCache.Clear();

    private async Task InitializeAsync()
    {
        await CreateAllTablesAsync();

        // Une seule transaction pour tout le seed/backfill/resync : sans elle, chaque Insert/Update/
        // Delete de ce pipeline est sa propre transaction implicite, donc un flush disque par ligne -
        // mesuré le 2026-09-24 : ~45 s au premier lancement et ~4,5 s à CHAQUE lancement (quasi tout
        // dans ResyncExplorationResultsAsync, ~200 lignes supprimées/réinsérées une à une), que tout
        // service attend via Initialization avant sa première requête.
        await RunInTransactionAsync(SeedAndBackfillAsync);
    }

    /// <summary>BEGIN/COMMIT explicites plutôt que SQLiteAsyncConnection.RunInTransactionAsync, qui
    /// n'accepte qu'un callback synchrone sur SQLiteConnection - tout le pipeline de seed est écrit en
    /// async contre _db. Sûr ici : SQLiteAsyncConnection sérialise tout sur une seule connexion par
    /// chemin, et aucun appelant n'écrit en parallèle (tous les services attendent Initialization,
    /// ResetAsync aussi). Ne pas appeler d'InsertAllAsync/RunInTransactionAsync à l'intérieur : sqlite-
    /// net ouvrirait son propre BEGIN, refusé dans une transaction déjà ouverte.</summary>
    private async Task RunInTransactionAsync(Func<Task> work)
    {
        await _db.ExecuteAsync("BEGIN TRANSACTION");
        try
        {
            await work();
            await _db.ExecuteAsync("COMMIT");
        }
        catch
        {
            await _db.ExecuteAsync("ROLLBACK");
            throw;
        }
        finally
        {
            // Seed/backfill/reset écrivent aussi en SQL brut (DROP, DELETE) - invisible pour
            // TableChanged, et un ROLLBACK annulerait des lignes éventuellement déjà cachées.
            InvalidateAllCachedTables();
        }
    }

    private async Task SeedAndBackfillAsync()
    {
        // First-launch only: if the archetype catalog is empty, nothing has been seeded yet (and
        // nothing the player made is at risk of being duplicated).
        if (await _db.Table<WarbandArchetypeEntity>().CountAsync() == 0)
            await SeedOfficialContentAsync();

        // Bug trouvé le 2026-09-01 en ajoutant BackfillDramatisPersonaSpecialRulesAsync : plusieurs
        // Backfill* méthodes ci-dessous appellent FindOrCreateSpecialRuleAsync (BackfillNewEquipmentItemsAsync,
        // BackfillInjurySpecialRulesAsync), qui ne consulte QUE le cache _specialRuleIdsByEnglishName - or ce
        // cache n'est peuplé que pendant un SeedOfficialContentAsync frais (voir sa doc), donc reste VIDE à
        // chaque lancement ordinaire (catalogue déjà seedé, le bloc juste au-dessus est sauté). Résultat :
        // un Backfill* qui a besoin d'une règle DÉJÀ existante en base (ex. "Parry (Sword)", partagée par
        // Ienh-Khain/Dague du Corsaire/Dague de Johann) ne la retrouve jamais via le cache et en recrée un
        // doublon à chaque lancement où ce Backfill* a quelque chose à faire - resté silencieux jusqu'ici
        // (personne ne construisait de dictionnaire strict par nom sur TOUTES les SpecialRuleEntity), mais
        // a fait planter BackfillDramatisPersonaSpecialRulesAsync (ToDictionary refuse une clé en double)
        // dès qu'un test recréait "Dagger (Johann)" via BackfillNewEquipmentItemsAsync sur une base ayant
        // déjà "Parry (Sword)". Pré-chauffe le cache depuis la base AVANT tout Backfill* - no-op sur une
        // base fraîche (catalogue vide, rien à charger).
        await WarmSpecialRuleCacheAsync();

        // Runs on every launch, not just first: fixes existing data rather than seeding new data (see
        // the method's own doc comment).
        await BackfillNeverGainsExperienceAsync();
        await BackfillWarbandArchetypeRaceAsync();
        await BackfillWarriorArchetypeRacialProfileAsync();
        // Après : versions user_version croissantes (1 puis 2), voir MustStartWithMutationBackfillDataVersion.
        await BackfillMustStartWithMutationAsync();
        await BackfillWarriorRacialMaxesAsync();
        await BackfillBranchedInjuriesAsync();
        await BackfillInjurySpecialRulesAsync();
        await BackfillSpecialRuleDescriptionsAsync();
        await BackfillWarriorStartingStatsAsync();
        // Doit rester AVANT BackfillDramatisPersonaStartingEquipmentAsync : cette dernière résout les
        // noms d'objets d'un Dramatis Persona (ex. Johann/"Dagger (Johann)") contre le catalogue déjà en
        // base - un objet Equipment.json tout neuf doit donc déjà exister avant que cette résolution ne
        // tourne, sinon elle échoue silencieusement (fail-soft, voir sa doc).
        await BackfillNewEquipmentItemsAsync();
        await BackfillDramatisPersonaStartingEquipmentAsync();
        await BackfillDramatisPersonaSpecialRulesAsync();
        await BackfillDramatisPersonaDescriptionsAsync();

        // Contrairement au reste de cette méthode : inconditionnel, pas gardé derrière le check
        // "catalogue vide" (voir la doc de ResyncExplorationResultsAsync).
        await ResyncExplorationResultsAsync();
    }

    /// <summary>One-time-per-row data fix for campaigns that started before WarriorArchetype/
    /// Warrior.GainsExperience existed (2026-08-17): the new column's SQLite-added default is `true`
    /// even for archetypes (Zombie, etc.) that already carry the "Never Gains Experience"/"Ne gagne
    /// jamais d'Expérience" special rule - so the flag would silently disagree with the rule already
    /// shown on the warrior's sheet until something corrects it. Unlike the "editing an archetype
    /// doesn't retroactively change already-recruited warriors" rule elsewhere in this app (a
    /// deliberate design choice about future edits), this is a missing-initial-value bug, not an edit -
    /// so both the archetype template AND any already-recruited Warrior snapshot get corrected here,
    /// once. Runs unconditionally (not gated by the "catalog empty" seed check, which only fires on a
    /// brand new install) so it fixes any existing local database on next launch - cheap no-op every
    /// run after the first since the WHERE-equivalent filters (GainsExperience still true) then match
    /// nothing. A fresh install never hits this: Equipment/SpecialRules.json-derived seed data already
    /// sets GainsExperience: false directly (see WarbandSeedData.WarriorSeedData), so nothing here is
    /// ever stale for it.</summary>
    private async Task BackfillNeverGainsExperienceAsync()
    {
        var ruleKeys = (await _db.Table<TranslationEntity>().ToListAsync())
            .Where(t => t.Value is "Never Gains Experience" or "Ne gagne jamais d'Expérience")
            .Select(t => t.Key)
            .ToHashSet();
        if (ruleKeys.Count == 0) return;

        var ruleIds = (await _db.Table<SpecialRuleEntity>().ToListAsync())
            .Where(r => ruleKeys.Contains(r.NameKey))
            .Select(r => r.Id)
            .ToHashSet();
        if (ruleIds.Count == 0) return;

        var archetypeIds = (await _db.Table<WarriorArchetypeSpecialRuleEntity>().ToListAsync())
            .Where(j => ruleIds.Contains(j.SpecialRuleId))
            .Select(j => j.WarriorArchetypeId)
            .ToHashSet();
        if (archetypeIds.Count == 0) return;

        var staleArchetypes = (await _db.Table<WarriorArchetypeEntity>().ToListAsync())
            .Where(a => archetypeIds.Contains(a.Id) && a.GainsExperience)
            .ToList();
        foreach (var archetype in staleArchetypes)
        {
            archetype.GainsExperience = false;
            await _db.UpdateAsync(archetype);
        }

        var staleWarriors = (await _db.Table<WarriorEntity>().ToListAsync())
            .Where(w => w.WarriorArchetypeId is { } waId && archetypeIds.Contains(waId) && w.GainsExperience)
            .ToList();
        foreach (var warrior in staleWarriors)
        {
            warrior.GainsExperience = false;
            await _db.UpdateAsync(warrior);
        }
    }

    /// <summary>English WarbandArchetype.Name -> English Race.Name, for the fixed 15 bands seeded
    /// before WarbandArchetype.RaceId existed (2026-08-20) - hardcoded here rather than re-reading each
    /// band's own JSON file's new "race" field, simpler for a one-time fix that only ever targets these
    /// 15 already-known bands (a future 16th band goes through SeedWarbandFromJsonAsync normally, which
    /// already resolves RaceId from its own JSON at insert time).</summary>
    private static readonly Dictionary<string, string> _raceNameByWarbandEnglishName = new()
    {
        ["Averlander Mercenaries"] = "Human",
        ["Beastmen Raiders"] = "Beastman",
        ["Carnival of Chaos"] = "Marauder of Chaos",
        ["Cult of the Possessed"] = "Marauder of Chaos",
        ["Dwarf Treasure Hunters"] = "Dwarf",
        ["Kislevites"] = "Human",
        ["Marienburg Mercenaries"] = "Human",
        ["Middenheim Mercenaries"] = "Human",
        ["Orc Mob"] = "Orc",
        ["Ostlander Mercenaries"] = "Human",
        ["Reiklander Mercenaries"] = "Human",
        ["The Sisters of Sigmar"] = "Human",
        ["Skaven of Clan Eshin"] = "Skaven",
        ["Undead"] = "Undead",
        ["Witch Hunters"] = "Human"
    };

    /// <summary>One-time-per-row data fix for warbands seeded before WarbandArchetype.RaceId existed
    /// (2026-08-20) - same idiom as BackfillNeverGainsExperienceAsync: runs unconditionally on every
    /// launch (not gated by the "catalog empty" check, which only fires on a brand new install), cheap
    /// no-op once every row has a real RaceId. A fresh install never hits this: SeedWarbandFromJsonAsync
    /// already sets RaceId directly from each band's own JSON "race" field. Ensures Races.json is seeded
    /// first (FindOrCreateRaceAsync is DB-aware, safe to call even though SeedOfficialContentAsync -
    /// and therefore _raceIdsByEnglishName - never ran this launch), then maps each stale
    /// WarbandArchetype to its race by English Name (_raceNameByWarbandEnglishName).</summary>
    private async Task BackfillWarbandArchetypeRaceAsync()
    {
        var staleArchetypes = (await _db.Table<WarbandArchetypeEntity>().ToListAsync())
            .Where(a => a.RaceId == 0)
            .ToList();
        if (staleArchetypes.Count == 0) return;

        foreach (var seed in await LoadSeedArrayAsync<RaceSeedData>("Races.json"))
            await FindOrCreateRaceAsync(seed);

        var englishNamesByKey = (await _db.Table<TranslationEntity>().Where(t => t.LanguageCode == "en").ToListAsync())
            .ToDictionary(t => t.Key, t => t.Value);

        foreach (var archetype in staleArchetypes)
        {
            if (!englishNamesByKey.TryGetValue(archetype.NameKey, out var englishName)) continue;
            if (!_raceNameByWarbandEnglishName.TryGetValue(englishName, out var raceName)) continue;
            if (!_raceIdsByEnglishName.TryGetValue(raceName, out var raceId)) continue;

            archetype.RaceId = raceId;
            await _db.UpdateAsync(archetype);
        }
    }

    /// <summary>One-time-per-row data fix for WarriorArchetypes seeded before RacialProfileId existed -
    /// same idiom as BackfillWarbandArchetypeRaceAsync just above, except gated by PRAGMA user_version
    /// (see RacialProfileBackfillDataVersion) rather than by its own row filter. Ensures RacialProfiles.json is seeded
    /// first (FindOrCreateRacialProfileAsync is DB-aware, safe even on a launch where
    /// SeedOfficialContentAsync never ran), then re-reads all 15 warband JSON files (LoadWarbandSeedDataAsync)
    /// to resolve each stale WarriorArchetype's profile by English Name against WarriorSeedData.
    /// RacialProfileName - the per-band JSON field is the single source of truth (see its own doc), not
    /// a separate hardcoded table, so a fix to one band's file is picked up here without touching this
    /// method. Must run before BackfillWarriorRacialMaxesAsync, which depends on every WarriorArchetype
    /// already having a real RacialProfileId (0 = genuinely none, see RacialProfileId's own doc).</summary>
    /// <summary>The 15 warband seed file names - same list as SeedOfficialContentAsync's explicit
    /// SeedWarbandFromJsonAsync calls, duplicated here (rather than having that method iterate this
    /// array) so the ordered/commented call list there stays easy to scan on its own. Consumed by
    /// BackfillWarriorArchetypeRacialProfileAsync, which needs to revisit every band file regardless of
    /// seeding order.</summary>
    private static readonly string[] _warbandFileNames =
    [
        "Undead.json", "DwarfTreasureHunters.json", "Averlanders.json", "Ostlanders.json",
        "Reiklanders.json", "Middenheimers.json", "Marienburgers.json", "CarnivalOfChaos.json",
        "CultOfThePossessed.json", "OrcMob.json", "BeastmenRaiders.json", "WitchHunters.json",
        "SkavenOfClanEshin.json", "SistersOfSigmar.json", "Kislevites.json"
    ];

    /// <summary>Valeur de PRAGMA user_version une fois BackfillWarriorArchetypeRacialProfileAsync passé -
    /// son filtre "RacialProfileId == 0" ne suffit pas à le rendre no-op, 0 étant aussi la valeur
    /// légitime d'un archétype sans profil (quelques archétypes officiels, tout archétype Custom) : il
    /// relisait donc RacialProfiles.json + les 15 fichiers de bande + toutes les traductions anglaises à
    /// CHAQUE lancement (~140 ms sur PC sur ~220 ms d'init, mesuré le 2026-09-24 - bien plus sur
    /// téléphone), bloquant la liste des bandes derrière Initialization.</summary>
    private const int RacialProfileBackfillDataVersion = 1;

    private async Task BackfillWarriorArchetypeRacialProfileAsync()
    {
        if (await _db.ExecuteScalarAsync<int>("PRAGMA user_version") >= RacialProfileBackfillDataVersion) return;
        await BackfillWarriorArchetypeRacialProfileOnceAsync();
        await _db.ExecuteAsync($"PRAGMA user_version = {RacialProfileBackfillDataVersion}");
    }

    /// <summary>user_version une fois BackfillMustStartWithMutationAsync passé - voir
    /// RacialProfileBackfillDataVersion, même mécanisme (versions croissantes, chaque backfill ne
    /// s'exécute qu'en dessous de la sienne).</summary>
    private const int MustStartWithMutationBackfillDataVersion = 2;

    /// <summary>WarriorArchetype.MustStartWithMutation (2026-09-24) : la colonne arrive à false sur une base
    /// déjà seedée - relit une fois les 15 fichiers de bande pour poser le drapeau sur les archétypes
    /// concernés (Mutant du Culte des Possédés), par nom anglais, même principe que
    /// BackfillWarriorArchetypeRacialProfileOnceAsync.</summary>
    private async Task BackfillMustStartWithMutationAsync()
    {
        if (await _db.ExecuteScalarAsync<int>("PRAGMA user_version") >= MustStartWithMutationBackfillDataVersion) return;

        var mandatoryEnglishNames = new HashSet<string>();
        foreach (var fileName in _warbandFileNames)
        {
            var data = await LoadWarbandSeedDataAsync(fileName);
            foreach (var w in data.Warriors.Where(w => w.MustStartWithMutation))
                mandatoryEnglishNames.Add(w.Name.En);
        }

        if (mandatoryEnglishNames.Count > 0)
        {
            var englishNamesByKey = (await _db.Table<TranslationEntity>().Where(t => t.LanguageCode == "en").ToListAsync())
                .ToDictionary(t => t.Key, t => t.Value);
            foreach (var archetype in await _db.Table<WarriorArchetypeEntity>().ToListAsync())
            {
                if (archetype.MustStartWithMutation || archetype.NameKey is null) continue;
                if (!englishNamesByKey.TryGetValue(archetype.NameKey, out var englishName) || !mandatoryEnglishNames.Contains(englishName)) continue;
                archetype.MustStartWithMutation = true;
                await _db.UpdateAsync(archetype);
            }
        }

        await _db.ExecuteAsync($"PRAGMA user_version = {MustStartWithMutationBackfillDataVersion}");
    }

    private async Task BackfillWarriorArchetypeRacialProfileOnceAsync()
    {
        var staleArchetypes = (await _db.Table<WarriorArchetypeEntity>().ToListAsync())
            .Where(a => a.RacialProfileId == 0)
            .ToList();
        if (staleArchetypes.Count == 0) return;

        foreach (var seed in await LoadSeedArrayAsync<RacialProfileSeedData>("RacialProfiles.json"))
            await FindOrCreateRacialProfileAsync(seed);

        var racialProfileNameByArchetypeEnglishName = new Dictionary<string, string>();
        foreach (var fileName in _warbandFileNames)
        {
            var data = await LoadWarbandSeedDataAsync(fileName);
            foreach (var w in data.Warriors)
                if (w.RacialProfileName is { } profileName) racialProfileNameByArchetypeEnglishName[w.Name.En] = profileName;
        }

        var englishNamesByKey = (await _db.Table<TranslationEntity>().Where(t => t.LanguageCode == "en").ToListAsync())
            .ToDictionary(t => t.Key, t => t.Value);

        foreach (var archetype in staleArchetypes)
        {
            if (!englishNamesByKey.TryGetValue(archetype.NameKey, out var englishName)) continue;
            if (!racialProfileNameByArchetypeEnglishName.TryGetValue(englishName, out var profileName)) continue;
            if (!_racialProfileIdsByEnglishName.TryGetValue(profileName, out var profileId)) continue;

            archetype.RacialProfileId = profileId;
            await _db.UpdateAsync(archetype);
        }
    }

    /// <summary>One-time-per-row data fix for Warriors recruited before the racial-maximum snapshot
    /// fields (MaxWeaponSkill etc.) existed - unlike the RaceId/RacialProfileId backfills above, this
    /// doesn't need seed data or English-name lookups: every WarriorEntity already carries a real
    /// WarriorArchetypeId - if that archetype's own RacialProfileId resolves to a real profile (0 =
    /// genuinely none, see SeedWarbandFromJsonAsync/RacialProfiles.json's own doc - stays null forever,
    /// nothing to backfill), copy its 9 maximums across. Filters on MaxWeaponSkill == null rather than
    /// all 9 fields at once, same cheap-no-op-after-first-run idiom as the other backfills - re-scans
    /// (harmlessly, a no-op via the profilesById lookup below) every launch for warriors whose archetype
    /// has no profile, since null is otherwise indistinguishable from "not yet backfilled".</summary>
    private async Task BackfillWarriorRacialMaxesAsync()
    {
        var staleWarriors = (await _db.Table<WarriorEntity>().ToListAsync())
            .Where(w => w.MaxWeaponSkill == null)
            .ToList();
        if (staleWarriors.Count == 0) return;

        var archetypesById = (await _db.Table<WarriorArchetypeEntity>().ToListAsync()).ToDictionary(a => a.Id);
        var profilesById = (await _db.Table<RacialProfileEntity>().ToListAsync()).ToDictionary(p => p.Id);

        foreach (var warrior in staleWarriors)
        {
            if (warrior.WarriorArchetypeId is not { } warriorArchetypeId) continue;
            if (!archetypesById.TryGetValue(warriorArchetypeId, out var archetype)) continue;
            if (!profilesById.TryGetValue(archetype.RacialProfileId, out var profile)) continue;

            warrior.MaxMovement = profile.MovementOverride is null ? profile.Movement : null;
            warrior.MaxWeaponSkill = profile.WeaponSkill;
            warrior.MaxBallisticSkill = profile.BallisticSkill;
            warrior.MaxStrength = profile.Strength;
            warrior.MaxToughness = profile.Toughness;
            warrior.MaxWounds = profile.Wounds;
            warrior.MaxInitiative = profile.Initiative;
            warrior.MaxAttacks = profile.Attacks;
            warrior.MaxLeadership = profile.Leadership;
            await _db.UpdateAsync(warrior);
        }
    }

    /// <summary>One-time-per-row data fix for warriors recruited before Warrior.StartingMovement/etc
    /// existed (2026-08-25) - the new columns default to 0 for pre-existing rows, which would make the
    /// stat-changed color code (StatRowView's DataTriggers) show every single one of a warrior's
    /// current stats as "increased" the moment this ships. All 9 fields being exactly 0 is used as the
    /// "never set" marker (safe in practice: no real profile has Wounds/Attacks/Leadership all at 0 -
    /// that would mean an unfieldable model) - baseline resets to whatever each warrior's CURRENT stats
    /// happen to be right now (their true recruitment-time values aren't recoverable retroactively), so
    /// no false delta shows up today and tracking is accurate from this point forward.</summary>
    private async Task BackfillWarriorStartingStatsAsync()
    {
        var staleWarriors = (await _db.Table<WarriorEntity>().ToListAsync())
            .Where(w => w.StartingMovement == 0 && w.StartingWeaponSkill == 0 && w.StartingBallisticSkill == 0 &&
                        w.StartingStrength == 0 && w.StartingToughness == 0 && w.StartingWounds == 0 &&
                        w.StartingInitiative == 0 && w.StartingAttacks == 0 && w.StartingLeadership == 0)
            .ToList();

        foreach (var warrior in staleWarriors)
        {
            warrior.StartingMovement = warrior.Movement;
            warrior.StartingWeaponSkill = warrior.WeaponSkill;
            warrior.StartingBallisticSkill = warrior.BallisticSkill;
            warrior.StartingStrength = warrior.Strength;
            warrior.StartingToughness = warrior.Toughness;
            warrior.StartingWounds = warrior.Wounds;
            warrior.StartingInitiative = warrior.Initiative;
            warrior.StartingAttacks = warrior.Attacks;
            warrior.StartingLeadership = warrior.Leadership;
            await _db.UpdateAsync(warrior);
        }
    }

    /// <summary>Equipment.json has no dedup-at-runtime mechanism (see the file's own note in CLAUDE.md) -
    /// fine for the normal case (SeedEquipmentAsync only ever runs once, on a genuinely empty catalog),
    /// but any edit to the file made after a machine already seeded once would otherwise silently never
    /// reach that machine - two independent cases per entry, matched by English name against what's
    /// already in TranslationEntity: (1) a brand-new entry (2026-09-01: "Dagger (Johann)", a new unique
    /// artefact for Johann's "counts as a Sword for Parry" mechanic) gets INSERTED (mirrors
    /// SeedEquipmentAsync's own per-item logic exactly); (2) an ALREADY-existing entry whose specialRules
    /// changed (2026-09-01: "Wizard's Staff (Nicodemus)" gained "Concussion"/"Parry (Buckler)" alongside
    /// its own "Two-Handed Grip") gets its EquipmentItemSpecialRuleEntity rows re-synced by COUNT mismatch
    /// (delete + reinsert, same idiom as BackfillDramatisPersonaStartingEquipmentAsync's equipment-count
    /// check) - every other field on an existing row is left untouched, so nothing a player edited into
    /// Modified/Custom is at risk either way. Known limitation: unlike SeedOfficialContentAsync,
    /// RestrictedToWarbandNames isn't resolvable here (no deferred-resolution queue on this path) - not
    /// needed by any entry added so far, would need extending if a future backfilled item requires it.</summary>
    private async Task BackfillNewEquipmentItemsAsync()
    {
        var englishTranslations = (await _db.Table<TranslationEntity>().ToListAsync())
            .Where(t => t.LanguageCode == "en")
            .ToDictionary(t => t.Key, t => t.Value);
        var existingEquipmentIdByName = (await _db.Table<EquipmentItemEntity>().ToListAsync())
            .Where(e => englishTranslations.ContainsKey(e.NameKey))
            .ToDictionary(e => englishTranslations[e.NameKey], e => e.Id);

        foreach (var eq in await LoadSeedArrayAsync<EquipmentSeedData>("Equipment.json"))
        {
            if (existingEquipmentIdByName.TryGetValue(eq.Name.En, out var existingId))
            {
                // L'objet existe déjà - même limite/logique que BackfillDramatisPersonaStartingEquipmentAsync
                // pour StartingEquipmentIds : un item déjà seedé dont les specialRules ont changé depuis
                // (2026-09-01 : "Wizard's Staff (Nicodemus)" a gagné Concussion/Parry (Buckler) en plus de
                // sa propre "Two-Handed Grip") ne les récupère jamais tout seul - comparé par COMPTE, pas
                // par contenu (assez pour ce cas, comme pour l'équipement de départ d'un Dramatis Persona).
                var existingRuleCount = await _db.Table<EquipmentItemSpecialRuleEntity>().Where(r => r.EquipmentItemId == existingId).CountAsync();
                if (existingRuleCount != eq.SpecialRules.Count)
                {
                    var staleRuleRows = await _db.Table<EquipmentItemSpecialRuleEntity>().Where(r => r.EquipmentItemId == existingId).ToListAsync();
                    foreach (var row in staleRuleRows)
                        await _db.DeleteAsync(row);
                    foreach (var sr in eq.SpecialRules)
                    {
                        var ruleId = await FindOrCreateSpecialRuleAsync(sr);
                        await _db.InsertAsync(new EquipmentItemSpecialRuleEntity { EquipmentItemId = existingId, SpecialRuleId = ruleId });
                    }
                }
                continue;
            }

            var item = new EquipmentItem
            {
                Category = Enum.Parse<EquipmentCategory>(eq.Category),
                Cost = eq.Cost,
                Rarity = eq.Rarity,
                CostRandomMax = eq.CostRandomMax,
                Source = ContentSource.Official,
                IsFreeDagger = eq.IsFreeDagger,
                Movement = eq.Movement,
                WeaponSkill = eq.WeaponSkill,
                BallisticSkill = eq.BallisticSkill,
                Strength = eq.Strength,
                Toughness = eq.Toughness,
                Wounds = eq.Wounds,
                Initiative = eq.Initiative,
                Attacks = eq.Attacks,
                Leadership = eq.Leadership,
                GrantsSkillCategory = eq.GrantsSkillCategory is { } grantsSkillCategory ? Enum.Parse<SkillCategory>(grantsSkillCategory) : null,
                GrantsSpecificSkillName = eq.GrantsSpecificSkillName,
                GrantsRareItemSearchBonus = eq.GrantsRareItemSearchBonus,
                IsSellable = eq.IsSellable,
                GrantsBonusExplorationDice = eq.GrantsBonusExplorationDice,
                IsUniqueArtefact = eq.IsUniqueArtefact,
                IsExplorationOnly = eq.IsExplorationOnly
            };
            item.NameKey = await SeedTranslationAsync(eq.Name.En, eq.Name.Fr);
            item.DescriptionKey = eq.Description is null ? null : await SeedTranslationAsync(eq.Description.En, eq.Description.Fr);
            var itemEntity = item.ToEntity();
            await _db.InsertAsync(itemEntity);
            _equipmentIdsByEnglishName[eq.Name.En] = itemEntity.Id;

            foreach (var sr in eq.SpecialRules)
            {
                var ruleId = await FindOrCreateSpecialRuleAsync(sr);
                await _db.InsertAsync(new EquipmentItemSpecialRuleEntity { EquipmentItemId = itemEntity.Id, SpecialRuleId = ruleId });
            }
        }
    }

    /// <summary>One-time-per-row data fix for an already-seeded database whose DramatisPersonae.json was
    /// edited AFTER SeedDramatisPersonaeAsync already ran once (2026-09-01: Bertha's startingEquipmentNames
    /// gained a second "Sigmarite Warhammer" entry to represent her carrying two - the empty-catalog seed
    /// gate only fires on a brand new install, so a machine that had already seeded her once never picked
    /// this up, same root cause as ResyncExplorationResultsAsync's problem (2) but for a catalog that -
    /// unlike Exploration - DOES have a real Library editor (Official -> Modified). A full unconditional
    /// wipe-and-reseed like Exploration's would risk clobbering a player's own edit, so this only touches
    /// ContentSource.Official personas, matched to their JSON entry by English name (same idiom as
    /// BackfillNeverGainsExperienceAsync's rule-text match). Several independent checks per persona, all
    /// re-run every launch (cheap no-op once in sync): (1) equipment ROW COUNT mismatch re-syncs
    /// DramatisPersonaEquipmentEntity rows (delete + reinsert) to the JSON's startingEquipmentNames list,
    /// duplicates included; (2) AlternativePaymentItemId (2026-09-01, Johann/Ombre Cramoisie),
    /// (3) RequiresCooldownBeforeResearch, (4) PairedWithDramatisPersonaId and (5) IsHiddenFromSearchPicker
    /// (all 2026-09-01, Ulli &amp; Marquand) just overwrite their column directly - simpler than (1) since
    /// they're plain columns, not a join table. All no-op once in sync, and for anyone who has since
    /// edited a persona into Modified/Custom.</summary>
    private async Task BackfillDramatisPersonaStartingEquipmentAsync()
    {
        var personae = (await _db.Table<DramatisPersonaEntity>().ToListAsync())
            .Where(p => p.Source == ContentSource.Official)
            .ToList();
        if (personae.Count == 0) return;

        var englishTranslations = (await _db.Table<TranslationEntity>().ToListAsync())
            .Where(t => t.LanguageCode == "en")
            .ToDictionary(t => t.Key, t => t.Value);

        var equipmentIdByEnglishName = (await _db.Table<EquipmentItemEntity>().ToListAsync())
            .Where(e => englishTranslations.ContainsKey(e.NameKey))
            .ToDictionary(e => englishTranslations[e.NameKey], e => e.Id);

        var jsonByEnglishName = (await LoadSeedArrayAsync<DramatisPersonaSeedData>("DramatisPersonae.json"))
            .ToDictionary(dp => dp.Name.En);

        // Pour PairedWithPersonaName (Ulli/Marquand, 2026-09-01) - toutes les personas existent déjà ici
        // (contrairement à SeedDramatisPersonaeAsync's résolution différée, nécessaire seulement pendant
        // l'insertion initiale), donc une simple résolution directe suffit.
        var personaIdByEnglishName = personae
            .Where(p => englishTranslations.ContainsKey(p.NameKey))
            .ToDictionary(p => englishTranslations[p.NameKey], p => p.Id);

        foreach (var entity in personae)
        {
            if (!englishTranslations.TryGetValue(entity.NameKey, out var englishName)) continue;
            if (!jsonByEnglishName.TryGetValue(englishName, out var dp)) continue;

            var existingRows = await _db.Table<DramatisPersonaEquipmentEntity>().Where(r => r.DramatisPersonaId == entity.Id).ToListAsync();
            if (existingRows.Count != dp.StartingEquipmentNames.Count)
            {
                foreach (var row in existingRows)
                    await _db.DeleteAsync(row);
                foreach (var itemName in dp.StartingEquipmentNames)
                {
                    // Fail-soft (unlike the first-launch seed path, which throws on a typo): a backfill
                    // running on every subsequent launch shouldn't be able to block startup over bad data.
                    if (equipmentIdByEnglishName.TryGetValue(itemName, out var itemId))
                        await _db.InsertAsync(new DramatisPersonaEquipmentEntity { DramatisPersonaId = entity.Id, EquipmentItemId = itemId });
                }
            }

            // Même logique pour AlternativePaymentItemId (2026-09-01, Johann/Ombre Cramoisie) - un champ
            // simple (FK direct, pas une table de jointure), donc comparé et réécrit directement plutôt que
            // delete+reinsert. Indépendant du bloc équipement ci-dessus (pas de "continue" partagé) : les
            // deux backfills doivent chacun s'exécuter même si l'autre est déjà à jour.
            var wantedAlternativePaymentItemId = dp.AlternativePaymentItemName is { } altName && equipmentIdByEnglishName.TryGetValue(altName, out var altId)
                ? altId : (int?)null;
            // Même logique pour RequiresCooldownBeforeResearch/PairedWithDramatisPersonaId/
            // IsHiddenFromSearchPicker (2026-09-01, Aenur/Ulli & Marquand) - de simples colonnes,
            // comparées et réécrites directement comme AlternativePaymentItemId ci-dessus.
            var wantedPairedWithId = dp.PairedWithPersonaName is { } pairedName && personaIdByEnglishName.TryGetValue(pairedName, out var pairedId)
                ? pairedId : (int?)null;
            var needsUpdate = entity.AlternativePaymentItemId != wantedAlternativePaymentItemId
                || entity.RequiresCooldownBeforeResearch != dp.RequiresCooldownBeforeResearch
                || entity.PairedWithDramatisPersonaId != wantedPairedWithId
                || entity.IsHiddenFromSearchPicker != dp.HiddenFromSearchPicker;
            if (needsUpdate)
            {
                entity.AlternativePaymentItemId = wantedAlternativePaymentItemId;
                entity.RequiresCooldownBeforeResearch = dp.RequiresCooldownBeforeResearch;
                entity.PairedWithDramatisPersonaId = wantedPairedWithId;
                entity.IsHiddenFromSearchPicker = dp.HiddenFromSearchPicker;
                await _db.UpdateAsync(entity);
            }
        }
    }

    /// <summary>Runs on every launch - keeps an Official persona's Description/PairDescription text
    /// (translations) in sync with DramatisPersonae.json's current wording, same "Official content always
    /// mirrors the JSON, only a player edit (ContentSource.Modified) permanently diverges" precedent as
    /// every other Backfill* here. Added 2026-09-01 (user-supplied source text): Marquand/Ulli's own
    /// Description was a short trimmed bio + a mechanical hire-fee note; replaced with each persona's real
    /// individual biography, and the mechanical note moved into a NEW PairDescription (Marquand only - the
    /// shared "duo" lore, e.g. "Never in the history of the Empire...", distinct from either half's own
    /// personal bio) shown by DramatisPersonaPairDetailDialog instead of the plain profile dialog. Compares
    /// by current English text (cheap, no version/hash column) - a no-op once in sync, creates
    /// PairDescriptionKey on first run (null beforehand, only Marquand's JSON entry has PairDescription).</summary>
    private async Task BackfillDramatisPersonaDescriptionsAsync()
    {
        var personae = (await _db.Table<DramatisPersonaEntity>().ToListAsync())
            .Where(p => p.Source == ContentSource.Official)
            .ToList();
        if (personae.Count == 0) return;

        var englishTranslations = (await _db.Table<TranslationEntity>().ToListAsync())
            .Where(t => t.LanguageCode == "en")
            .ToDictionary(t => t.Key, t => t.Value);

        var jsonByEnglishName = (await LoadSeedArrayAsync<DramatisPersonaSeedData>("DramatisPersonae.json"))
            .ToDictionary(dp => dp.Name.En);

        foreach (var entity in personae)
        {
            if (!englishTranslations.TryGetValue(entity.NameKey, out var englishName)) continue;
            if (!jsonByEnglishName.TryGetValue(englishName, out var dp)) continue;

            if (dp.Description is { } desc && englishTranslations.GetValueOrDefault(entity.DescriptionKey ?? string.Empty) != desc.En)
            {
                entity.DescriptionKey = await TranslationResolver.SetAsync(this, entity.DescriptionKey, "en", desc.En);
                if (!string.IsNullOrEmpty(desc.Fr))
                    await TranslationResolver.SetAsync(this, entity.DescriptionKey, "fr", desc.Fr);
                await _db.UpdateAsync(entity);
            }

            if (dp.PairDescription is { } pairDesc && englishTranslations.GetValueOrDefault(entity.PairDescriptionKey ?? string.Empty) != pairDesc.En)
            {
                entity.PairDescriptionKey = await TranslationResolver.SetAsync(this, entity.PairDescriptionKey, "en", pairDesc.En);
                if (!string.IsNullOrEmpty(pairDesc.Fr))
                    await TranslationResolver.SetAsync(this, entity.PairDescriptionKey, "fr", pairDesc.Fr);
                await _db.UpdateAsync(entity);
            }
        }
    }

    /// <summary>Runs on every launch, after BackfillDramatisPersonaStartingEquipmentAsync - adds any
    /// SpecialRule a persona's JSON entry now lists but the DB link doesn't have yet (e.g. "A Fistful of
    /// Crowns"/"Une Poignée d'Or", split out of Marquand/Ulli's free-text Description into a real
    /// SpecialRule on 2026-09-01 so it shows as its own tappable chip on the roster card - see
    /// WarbandDetailViewModel.ToRow - instead of being buried in a wall of biography prose). Additive
    /// only, matched by (DramatisPersonaId, SpecialRuleId) pair - never touches a rule the persona already
    /// has (e.g. "Inseparable"), so a player who hand-edited a persona's rules in the Codex isn't silently
    /// overwritten. Deliberately does NOT reuse FindOrCreateSpecialRuleAsync's transient
    /// _specialRuleIdsByEnglishName cache directly - that cache is only populated during a fresh
    /// SeedOfficialContentAsync pass and stays empty on every ordinary launch, so blindly calling it here
    /// would insert a duplicate SpecialRuleEntity for any rule name that happens to already exist in the
    /// DB. Instead resolves an existing row by English name straight against the DB first (same shape as
    /// the equipment-name lookup already built in BackfillDramatisPersonaStartingEquipmentAsync), only
    /// inserting - and caching locally for the rest of this one pass - when genuinely no row exists yet.</summary>
    private async Task BackfillDramatisPersonaSpecialRulesAsync()
    {
        var personae = (await _db.Table<DramatisPersonaEntity>().ToListAsync())
            .Where(p => p.Source == ContentSource.Official)
            .ToList();
        if (personae.Count == 0) return;

        var englishTranslations = (await _db.Table<TranslationEntity>().ToListAsync())
            .Where(t => t.LanguageCode == "en")
            .ToDictionary(t => t.Key, t => t.Value);

        var jsonByEnglishName = (await LoadSeedArrayAsync<DramatisPersonaSeedData>("DramatisPersonae.json"))
            .ToDictionary(dp => dp.Name.En);

        // Boucle plutôt qu'un ToDictionary strict (2026-09-01, bug trouvé) : une base déjà installée
        // avant le correctif de WarmSpecialRuleCacheAsync peut porter un VRAI doublon de nom (ex. "Parry
        // (Sword)" créé deux fois par un ancien passage de BackfillNewEquipmentItemsAsync) - premier
        // rencontré gagne plutôt que planter au chargement.
        var specialRuleIdByEnglishName = new Dictionary<string, int>();
        foreach (var rule in await _db.Table<SpecialRuleEntity>().ToListAsync())
        {
            if (englishTranslations.TryGetValue(rule.NameKey, out var ruleName) && !specialRuleIdByEnglishName.ContainsKey(ruleName))
                specialRuleIdByEnglishName[ruleName] = rule.Id;
        }

        var existingLinks = (await _db.Table<DramatisPersonaSpecialRuleEntity>().ToListAsync())
            .Select(l => (l.DramatisPersonaId, l.SpecialRuleId)).ToHashSet();

        foreach (var entity in personae)
        {
            if (!englishTranslations.TryGetValue(entity.NameKey, out var englishName)) continue;
            if (!jsonByEnglishName.TryGetValue(englishName, out var dp)) continue;

            foreach (var sr in dp.SpecialRules)
            {
                if (!specialRuleIdByEnglishName.TryGetValue(sr.Name.En, out var ruleId))
                {
                    var rule = new SpecialRule
                    {
                        Source = ContentSource.Official,
                        CostMultiplier = sr.CostMultiplier,
                        Abbreviation = sr.Abbreviation,
                        Rarity = sr.Rarity,
                        IsResaleUpgrade = sr.IsResaleUpgrade,
                        HatredTargetsSpellcasters = sr.HatredTargetsSpellcasters
                    };
                    rule.NameKey = await SeedTranslationAsync(sr.Name.En, sr.Name.Fr);
                    rule.DescriptionKey = sr.Description is null ? null : await SeedTranslationAsync(sr.Description.En, sr.Description.Fr);
                    var newRuleEntity = rule.ToEntity();
                    await _db.InsertAsync(newRuleEntity);
                    ruleId = newRuleEntity.Id;
                    specialRuleIdByEnglishName[sr.Name.En] = ruleId;
                }

                if (existingLinks.Add((entity.Id, ruleId)))
                    await _db.InsertAsync(new DramatisPersonaSpecialRuleEntity { DramatisPersonaId = entity.Id, SpecialRuleId = ruleId });
            }
        }
    }

    /// <summary>Wipes and re-seeds the Exploration chart from Data/SeedData/ExplorationResults.json on
    /// EVERY launch, unconditionally - not gated behind InitializeAsync's "catalog empty" check like the
    /// rest of SeedOfficialContentAsync. Two problems this solves at once:
    /// (1) Found 2026-08-17: SeedExplorationResultsAsync got invoked a second time against a live dev
    ///     database outside the normal single-pass seed, silently doubling every row (no dedup guard of
    ///     its own, same "plain insert" precedent as Injury/EquipmentItem).
    /// (2) Found 2026-08-18, the more fundamental issue: an ALREADY-SEEDED database never re-seeds
    ///     anything (the empty-catalog gate only fires once, ever), so an edit to ExplorationResults.json
    ///     - like adding Puits/StatTestField - silently never reached a machine that had already run the
    ///     seed once before that edit existed. Every other Library catalog has a real CRUD editor and a
    ///     "no rules engine" boundary that makes this a non-issue; Exploration is pure reference content
    ///     with no editor and, critically, no other table holding a foreign key into
    ///     ExplorationResultEntity/ExplorationOutcomeEntity (History entries are plain strings, not
    ///     references) - nothing is lost by deleting and recreating it wholesale every launch. Cheap
    ///     (30 rows) compared to re-running the ~22-pass full catalog+warband seed this replaces having
    ///     to trigger manually.</summary>
    private async Task ResyncExplorationResultsAsync()
    {
        var staleResults = await _db.Table<ExplorationResultEntity>().ToListAsync();
        if (staleResults.Count > 0)
        {
            var staleOutcomes = await _db.Table<ExplorationOutcomeEntity>().ToListAsync();
            foreach (var outcome in staleOutcomes)
                await _db.DeleteAsync<ExplorationOutcomeEntity>(outcome.Id);

            var staleKeys = staleResults.SelectMany(r => new[] { r.NameKey, r.DescriptionKey }).ToHashSet();
            var staleTranslations = (await _db.Table<TranslationEntity>().ToListAsync())
                .Where(t => staleKeys.Contains(t.Key));
            foreach (var translation in staleTranslations)
                await _db.DeleteAsync<TranslationEntity>(translation.Id);

            foreach (var result in staleResults)
                await _db.DeleteAsync<ExplorationResultEntity>(result.Id);
        }

        await SeedExplorationResultsAsync();
    }

    private async Task CreateAllTablesAsync()
    {
        await _db.CreateTableAsync<CampaignEntity>();
        await _db.CreateTableAsync<WarbandArchetypeEntity>();
        await _db.CreateTableAsync<WarbandEntity>();
        await _db.CreateTableAsync<WarriorArchetypeEntity>();
        await _db.CreateTableAsync<WarriorEntity>();
        await _db.CreateTableAsync<EquipmentItemEntity>();
        await _db.CreateTableAsync<SkillEntity>();
        await _db.CreateTableAsync<InjuryEntity>();
        await _db.CreateTableAsync<InjurySpecialRuleEntity>();
        await _db.CreateTableAsync<WarriorEquipmentEntity>();
        await _db.CreateTableAsync<WarbandEquipmentEntity>();
        await _db.CreateTableAsync<WarriorSkillEntity>();
        await _db.CreateTableAsync<WarriorInjuryEntity>();
        await _db.CreateTableAsync<WarriorHatredEntity>();
        await _db.CreateTableAsync<WarriorSpellEntity>();
        await _db.CreateTableAsync<HistoryEntryEntity>();
        await _db.CreateTableAsync<TranslationEntity>();
        await _db.CreateTableAsync<SpellEntity>();
        await _db.CreateTableAsync<WarbandArchetypeEquipmentEntity>();
        await _db.CreateTableAsync<WarbandArchetypeSkillEntity>();
        await _db.CreateTableAsync<WarriorArchetypeSkillEntity>();
        await _db.CreateTableAsync<SpecialRuleEntity>();
        await _db.CreateTableAsync<WarbandArchetypeSpecialRuleEntity>();
        await _db.CreateTableAsync<WarriorArchetypeSpecialRuleEntity>();
        await _db.CreateTableAsync<MutationEntity>();
        await _db.CreateTableAsync<WarriorMutationEntity>();
        await _db.CreateTableAsync<MagicSchoolEntity>();
        await _db.CreateTableAsync<WarbandArchetypeMagicSchoolEntity>();
        await _db.CreateTableAsync<RaceEntity>();
        await _db.CreateTableAsync<RacialProfileEntity>();
        await _db.CreateTableAsync<WarbandArchetypeMutationEntity>();
        await _db.CreateTableAsync<EquipmentListEntity>();
        await _db.CreateTableAsync<EquipmentListItemEntity>();
        await _db.CreateTableAsync<WarriorArchetypeEquipmentEntity>();
        await _db.CreateTableAsync<EquipmentItemSpecialRuleEntity>();
        await _db.CreateTableAsync<ExplorationResultEntity>();
        await _db.CreateTableAsync<ExplorationOutcomeEntity>();
        await _db.CreateTableAsync<HiredSwordEntity>();
        await _db.CreateTableAsync<HiredSwordEquipmentEntity>();
        await _db.CreateTableAsync<WarbandArchetypeHiredSwordEntity>();
        await _db.CreateTableAsync<HiredSwordSpecialRuleEntity>();
        await _db.CreateTableAsync<DramatisPersonaEntity>();
        await _db.CreateTableAsync<DramatisPersonaSpecialRuleEntity>();
        await _db.CreateTableAsync<WarbandArchetypeDramatisPersonaEntity>();
        await _db.CreateTableAsync<DramatisPersonaEquipmentEntity>();
        await _db.CreateTableAsync<DramatisPersonaSkillEntity>();
        await _db.CreateTableAsync<WarbandDramatisPersonaCooldownEntity>();
    }

    private async Task DropAllTablesAsync()
    {
        await _db.DropTableAsync<CampaignEntity>();
        await _db.DropTableAsync<WarbandArchetypeEntity>();
        await _db.DropTableAsync<WarbandEntity>();
        await _db.DropTableAsync<WarriorArchetypeEntity>();
        await _db.DropTableAsync<WarriorEntity>();
        await _db.DropTableAsync<EquipmentItemEntity>();
        await _db.DropTableAsync<SkillEntity>();
        await _db.DropTableAsync<InjuryEntity>();
        await _db.DropTableAsync<InjurySpecialRuleEntity>();
        await _db.DropTableAsync<WarriorEquipmentEntity>();
        await _db.DropTableAsync<WarriorSkillEntity>();
        await _db.DropTableAsync<WarriorInjuryEntity>();
        await _db.DropTableAsync<WarriorHatredEntity>();
        await _db.DropTableAsync<WarriorSpellEntity>();
        await _db.DropTableAsync<HistoryEntryEntity>();
        await _db.DropTableAsync<TranslationEntity>();
        await _db.DropTableAsync<SpellEntity>();
        await _db.DropTableAsync<WarbandArchetypeEquipmentEntity>();
        await _db.DropTableAsync<WarbandArchetypeSkillEntity>();
        await _db.DropTableAsync<WarriorArchetypeSkillEntity>();
        await _db.DropTableAsync<SpecialRuleEntity>();
        await _db.DropTableAsync<WarbandArchetypeSpecialRuleEntity>();
        await _db.DropTableAsync<WarriorArchetypeSpecialRuleEntity>();
        await _db.DropTableAsync<MutationEntity>();
        await _db.DropTableAsync<WarriorMutationEntity>();
        await _db.DropTableAsync<MagicSchoolEntity>();
        await _db.DropTableAsync<WarbandArchetypeMagicSchoolEntity>();
        await _db.DropTableAsync<WarbandArchetypeMutationEntity>();
        await _db.DropTableAsync<RacialProfileEntity>();
        await _db.DropTableAsync<EquipmentListEntity>();
        await _db.DropTableAsync<EquipmentListItemEntity>();
        await _db.DropTableAsync<WarriorArchetypeEquipmentEntity>();
        await _db.DropTableAsync<EquipmentItemSpecialRuleEntity>();
        await _db.DropTableAsync<ExplorationResultEntity>();
        await _db.DropTableAsync<ExplorationOutcomeEntity>();
        await _db.DropTableAsync<WarbandEquipmentEntity>();
        await _db.DropTableAsync<HiredSwordEntity>();
        await _db.DropTableAsync<HiredSwordEquipmentEntity>();
        await _db.DropTableAsync<WarbandArchetypeHiredSwordEntity>();
        await _db.DropTableAsync<HiredSwordSpecialRuleEntity>();
        await _db.DropTableAsync<DramatisPersonaEntity>();
        await _db.DropTableAsync<DramatisPersonaSpecialRuleEntity>();
        await _db.DropTableAsync<WarbandArchetypeDramatisPersonaEntity>();
        await _db.DropTableAsync<DramatisPersonaEquipmentEntity>();
        await _db.DropTableAsync<DramatisPersonaSkillEntity>();
        await _db.DropTableAsync<WarbandDramatisPersonaCooldownEntity>();
    }

    /// <summary>Wipes every table (all campaign data AND Library edits/custom content) and recreates +
    /// reseeds from the bundled JSON - lets the Settings "Réinitialiser" button re-run the seed after a
    /// Core schema/data change without the user manually deleting the db file. Also clears the
    /// find-or-create caches (SpecialRule/Mutation/MagicSchool) since an id resolved during a previous
    /// seeding pass is meaningless against the fresh tables.</summary>
    public async Task ResetAsync()
    {
        await Initialization;
        await DropAllTablesAsync();
        InvalidateAllCachedTables();
        _specialRuleIdsByEnglishName.Clear();
        _mutationIdsByEnglishName.Clear();
        _magicSchoolIdsByEnglishName.Clear();
        _equipmentIdsByEnglishName.Clear();
        _skillIdsByEnglishName.Clear();
        _racialProfileIdsByEnglishName.Clear();
        _warbandArchetypeIdsByFileStem.Clear();
        _pendingSharedRestrictions.Clear();
        await CreateAllTablesAsync();
        await RunInTransactionAsync(async () =>
        {
            await SeedOfficialContentAsync();

            // Pas dans SeedOfficialContentAsync (voir ResyncExplorationResultsAsync, appelée à chaque
            // lancement plutôt que gardée derrière son garde-fou "catalogue vide") - après un DropTableAsync
            // complet ci-dessus, la table est garantie vide, un seed direct suffit ici (pas besoin du
            // nettoyage préalable que fait ResyncExplorationResultsAsync sur une base déjà peuplée).
            await SeedExplorationResultsAsync();
        });
    }

    private async Task SeedOfficialContentAsync()
    {
        // The 7 common catalogs (Data/SeedData/SpecialRules.json, Equipment.json, Mutations.json,
        // Skills.json, Injuries.json, MagicSchools.json, ExplorationResults.json) must seed before any
        // warband file below - warband JSON files only declare rules/equipment/mutations/schools that
        // are genuinely THEIRS, and find-or-create-by-English-Name (SpecialRule/Mutation/MagicSchool) or
        // a plain unrestricted insert (Equipment/Skill/Injury) relies on the canonical row already
        // existing by the time a warband references it. Injuries.json/ExplorationResults.json aren't
        // referenced by any warband file at all (no per-band injury/exploration tables in the rulebook),
        // they just need to seed once. Equipment.json's core rulebook mounts (Cheval/Destrier/Chien de
        // guerre, EquipmentCategory.Animal) carry RestrictedToWarbandNames instead of a single-band flag
        // - band-only mounts (e.g. Orc Mob's Sanglier de guerre) stay declared directly in their own
        // warband file, same split as any other band-declared equipment.
        await SeedSpecialRulesAsync();
        await SeedEquipmentAsync();
        await SeedMutationsAsync();
        await SeedSkillsAsync();
        await SeedInjuriesAsync();
        // Avant SeedHiredSwordsAsync : le Sorcier ("Warlock") référence "Lesser Magic" par un stub
        // name-only (HiredSwordSeedData.MagicSchoolName, même idiome que les stubs SpecialRules/Mutations
        // d'un fichier de bande) résolu via le même cache find-or-create que celui-ci alimente - le
        // find-or-create renvoie l'id existant SANS jamais mettre à jour la Description si le nom a déjà
        // été créé par un stub, donc l'ordre importe ici comme pour les 15 fichiers de bande (voir la
        // note juste au-dessus) : school MagicSchools.json doit être LE créateur (avec sa vraie
        // Description), pas le stub. Bug réel trouvé le 2026-08-27 (DataServiceTests.
        // MagicSchools_AllHaveBilingualDescriptions) : Lesser Magic seedait sans description tant que
        // HiredSwords seedait en premier.
        await SeedMagicSchoolsAsync();
        await SeedHiredSwordsAsync();
        await SeedRacesAsync();
        await SeedRacialProfilesAsync();
        // Pas ici : voir ResyncExplorationResultsAsync, appelée inconditionnellement depuis
        // InitializeAsync plutôt que gardée derrière le garde-fou "catalogue vide" de cette méthode.

        await SeedWarbandFromJsonAsync("Undead.json");
        await SeedWarbandFromJsonAsync("DwarfTreasureHunters.json");
        await SeedWarbandFromJsonAsync("Averlanders.json");
        await SeedWarbandFromJsonAsync("Ostlanders.json");
        await SeedWarbandFromJsonAsync("Reiklanders.json");
        await SeedWarbandFromJsonAsync("Middenheimers.json");
        await SeedWarbandFromJsonAsync("Marienburgers.json");
        await SeedWarbandFromJsonAsync("CarnivalOfChaos.json");
        await SeedWarbandFromJsonAsync("CultOfThePossessed.json");
        await SeedWarbandFromJsonAsync("OrcMob.json");
        await SeedWarbandFromJsonAsync("BeastmenRaiders.json");
        await SeedWarbandFromJsonAsync("WitchHunters.json");
        await SeedWarbandFromJsonAsync("SkavenOfClanEshin.json");
        await SeedWarbandFromJsonAsync("SistersOfSigmar.json");
        await SeedWarbandFromJsonAsync("Kislevites.json");

        // Après les 15 bandes (pas avant, contrairement à HiredSwords) : certains personnages référencent
        // par nom une Compétence propre à une bande (ex. Bertha/"Righteous Fury", propre aux Sœurs de
        // Sigmar - RestrictedToThisWarband dans SistersOfSigmar.json) via _skillIdsByEnglishName, qui
        // n'existe donc qu'une fois cette bande seedée. Conséquence positive : plus besoin de la passe de
        // résolution différée pour RestrictedToWarbandNames (voir SeedDramatisPersonaeAsync) - chaque
        // WarbandArchetypeId existe déjà, résolu directement via _warbandArchetypeIdsByFileStem.
        await SeedDramatisPersonaeAsync();

        // Deferred resolution: common-catalog entries (Equipment/Skill/Mutation) that named several
        // bands via RestrictedToWarbandNames couldn't resolve a WarbandArchetypeId at seed time, since
        // none of the 15 warband files above had been seeded yet. Every band now exists, so resolve each
        // file-stem name against _warbandArchetypeIdsByFileStem (throws on an unknown stem - same
        // fail-fast precedent as the other XxxIdsByEnglishName dictionaries, surfaces a JSON typo at
        // first launch) and insert the matching join row.
        foreach (var pending in _pendingSharedRestrictions)
        {
            // SpecialRule's Hatred target isn't a join table (see SpecialRuleEntity.
            // HatredTargetWarbandArchetypeIds) - resolve every stem for this rule first, then write the
            // whole CSV list back in one update, rather than one join row per stem like the other 3 kinds.
            if (pending.Kind == SharedRestrictionKind.SpecialRule)
            {
                var targetIds = pending.WarbandFileStems.Select(stem => _warbandArchetypeIdsByFileStem[stem]).ToList();
                var ruleEntity = await _db.Table<SpecialRuleEntity>().Where(r => r.Id == pending.ItemId).FirstAsync();
                ruleEntity.HatredTargetWarbandArchetypeIds = string.Join(',', targetIds);
                await _db.UpdateAsync(ruleEntity);
                continue;
            }

            // Skill's Hatred target mirrors SpecialRule's above - same CSV-write shape, same reason
            // (not a join table, see SkillEntity.HatredTargetWarbandArchetypeIds).
            if (pending.Kind == SharedRestrictionKind.SkillHatredTarget)
            {
                var targetIds = pending.WarbandFileStems.Select(stem => _warbandArchetypeIdsByFileStem[stem]).ToList();
                var skillEntityForHatred = await _db.Table<SkillEntity>().Where(s => s.Id == pending.ItemId).FirstAsync();
                skillEntityForHatred.HatredTargetWarbandArchetypeIds = string.Join(',', targetIds);
                await _db.UpdateAsync(skillEntityForHatred);
                continue;
            }

            foreach (var stem in pending.WarbandFileStems)
            {
                var warbandArchetypeId = _warbandArchetypeIdsByFileStem[stem];
                switch (pending.Kind)
                {
                    case SharedRestrictionKind.Equipment:
                        await _db.InsertAsync(new WarbandArchetypeEquipmentEntity { WarbandArchetypeId = warbandArchetypeId, EquipmentItemId = pending.ItemId });
                        break;
                    case SharedRestrictionKind.Skill:
                        await _db.InsertAsync(new WarbandArchetypeSkillEntity { WarbandArchetypeId = warbandArchetypeId, SkillId = pending.ItemId });
                        break;
                    case SharedRestrictionKind.Mutation:
                        await _db.InsertAsync(new WarbandArchetypeMutationEntity { WarbandArchetypeId = warbandArchetypeId, MutationId = pending.ItemId });
                        break;
                    case SharedRestrictionKind.HiredSword:
                        await _db.InsertAsync(new WarbandArchetypeHiredSwordEntity { WarbandArchetypeId = warbandArchetypeId, HiredSwordId = pending.ItemId });
                        break;
                }
            }
        }
    }

    /// <summary>Deserializes an embedded Data/SeedData/*.json file and inserts its warband, warrior
    /// archetypes, band-specific equipment (with restriction rows where flagged) and spells - each
    /// translatable field gets a fresh key via SeedTranslationAsync, same as the Reiklander seed above.</summary>
    /// <summary>Deserializes one warband seed file into its full WarbandSeedData - shared by
    /// SeedWarbandFromJsonAsync (first-launch seeding) and BackfillWarriorArchetypeRacialProfileAsync
    /// (which re-reads all 15 files to resolve WarriorSeedData.RacialProfileName by English archetype
    /// name, since that's per-band JSON data rather than a shared lookup table).</summary>
    private static async Task<WarbandSeedData> LoadWarbandSeedDataAsync(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames().Single(n => n.EndsWith(fileName, StringComparison.Ordinal));
        await using var stream = assembly.GetManifestResourceStream(resourceName)!;
        return await JsonSerializer.DeserializeAsync<WarbandSeedData>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException($"Empty or invalid seed file: {fileName}");
    }

    private async Task SeedWarbandFromJsonAsync(string fileName)
    {
        var data = await LoadWarbandSeedDataAsync(fileName);

        var warband = new WarbandArchetype
        {
            Source = ContentSource.Official,
            Grade = Enum.Parse<WarbandGrade>(data.Grade),
            StartingTreasury = data.StartingTreasury,
            MaxWarriors = data.MaxWarriors,
            MinWarriors = data.MinWarriors,
            ImagePath = data.ImagePath ?? string.Empty,
            // Indexeur direct (pas GetValueOrDefault) : une bande sans "race" reconnue dans Races.json
            // (typo, ou Races.json pas encore seedé avant celle-ci) doit planter au premier lancement
            // plutôt que silencieusement RaceId=0 - même précédent fail-fast que
            // _warbandArchetypeIdsByFileStem plus bas.
            RaceId = _raceIdsByEnglishName[data.Race]
        };
        warband.NameKey = await SeedTranslationAsync(data.Name.En, data.Name.Fr);
        warband.DescriptionKey = data.Description is null ? null : await SeedTranslationAsync(data.Description.En, data.Description.Fr);
        var warbandEntity = warband.ToEntity();
        await _db.InsertAsync(warbandEntity);
        _warbandArchetypeIdsByFileStem[Path.GetFileNameWithoutExtension(fileName)] = warbandEntity.Id;

        foreach (var sr in data.SpecialRules)
        {
            var ruleId = await FindOrCreateSpecialRuleAsync(sr);
            await _db.InsertAsync(new WarbandArchetypeSpecialRuleEntity { WarbandArchetypeId = warbandEntity.Id, SpecialRuleId = ruleId });
        }

        // Doit précéder le traitement de data.Spells plus bas : chaque Spell référence son école par
        // nom anglais (SpellSeedData.MagicSchoolName), résolu contre ce cache.
        foreach (var ms in data.MagicSchools)
        {
            var schoolId = await FindOrCreateMagicSchoolAsync(ms);
            await _db.InsertAsync(new WarbandArchetypeMagicSchoolEntity { WarbandArchetypeId = warbandEntity.Id, MagicSchoolId = schoolId });
        }

        // Doit précéder EquipmentLists (qui référence ces items par nom) et Warriors (dont
        // EquipmentListId dépend des listes ci-dessous) - donc seedé avant les guerriers cette fois,
        // contrairement à SpecialRules/Skills qui restent après. RestrictedToWarriorNames ne peut pas
        // encore être résolu ici (les ids de guerrier n'existent pas), voir pendingEquipmentWarriorRestrictions.
        var bandEquipmentIdsByEnglishName = new Dictionary<string, int>();
        var pendingEquipmentWarriorRestrictions = new List<(int ItemId, List<string> Names)>();

        foreach (var eq in data.Equipment)
        {
            // Find-or-create par nom anglais (comme SpecialRule/Mutation/MagicSchool) - un objet Rare
            // partagé par plusieurs bandes avec des restrictions différentes (ex. Holy Tome : Warrior-
            // Priest chez les Répurgateurs, Héroïnes chez les Sœurs de Sigmar) doit rester une seule
            // ligne de catalogue, chaque bande n'ajoutant que SES propres lignes de restriction.
            // _equipmentIdsByEnglishName est alimenté aussi bien par le pool commun (SeedEquipmentAsync)
            // que par ces déclarations propres aux bandes, contrairement à Equipment.json lui-même qui
            // reste un simple insert sans dédup interne (fichier écrit à la main, garanti sans doublon).
            int itemId;
            if (_equipmentIdsByEnglishName.TryGetValue(eq.Name.En, out var existingItemId))
            {
                itemId = existingItemId;
            }
            else
            {
                var item = new EquipmentItem
                {
                    Category = Enum.Parse<EquipmentCategory>(eq.Category),
                    Cost = eq.Cost,
                    Rarity = eq.Rarity,
                    CostRandomMax = eq.CostRandomMax,
                    Source = ContentSource.Official
                };
                item.NameKey = await SeedTranslationAsync(eq.Name.En, eq.Name.Fr);
                item.DescriptionKey = eq.Description is null ? null : await SeedTranslationAsync(eq.Description.En, eq.Description.Fr);
                var itemEntity = item.ToEntity();
                await _db.InsertAsync(itemEntity);
                itemId = itemEntity.Id;
                _equipmentIdsByEnglishName[eq.Name.En] = itemId;

                foreach (var sr in eq.SpecialRules)
                {
                    var ruleId = await FindOrCreateSpecialRuleAsync(sr);
                    await _db.InsertAsync(new EquipmentItemSpecialRuleEntity { EquipmentItemId = itemId, SpecialRuleId = ruleId });
                }
            }
            bandEquipmentIdsByEnglishName[eq.Name.En] = itemId;

            if (eq.RestrictedToThisWarband)
                await _db.InsertAsync(new WarbandArchetypeEquipmentEntity { WarbandArchetypeId = warbandEntity.Id, EquipmentItemId = itemId });

            if (eq.RestrictedToWarriorNames is { Count: > 0 } eqNames)
                pendingEquipmentWarriorRestrictions.Add((itemId, eqNames));
        }

        // Résout EquipmentListSeedData.ItemNames contre le pool commun (Equipment.json, seedé avant
        // tout fichier de bande) puis les items propres à cette bande ci-dessus - construit
        // equipmentListIdsByName, consommé juste en dessous par WarriorSeedData.EquipmentListName.
        var equipmentListIdsByName = new Dictionary<string, int>();
        foreach (var el in data.EquipmentLists)
        {
            var list = new EquipmentList { WarbandArchetypeId = warbandEntity.Id, Source = ContentSource.Official };
            list.NameKey = await SeedTranslationAsync(el.Name.En, el.Name.Fr);
            var listEntity = list.ToEntity();
            await _db.InsertAsync(listEntity);
            equipmentListIdsByName[el.Name.En] = listEntity.Id;

            foreach (var itemName in el.ItemNames)
            {
                var itemId = bandEquipmentIdsByEnglishName.TryGetValue(itemName, out var bandItemId)
                    ? bandItemId
                    : _equipmentIdsByEnglishName[itemName];
                await _db.InsertAsync(new EquipmentListItemEntity { EquipmentListId = listEntity.Id, EquipmentItemId = itemId });
            }
        }

        // Nom anglais -> id, alimenté ci-dessous pour résoudre SkillSeedData.RestrictedToWarriorNames
        // plus bas dans cette même passe (les noms de guerrier ne sont pas uniques globalement, donc pas
        // de cache au niveau classe comme pour SpecialRule/Mutation/MagicSchool).
        var warriorIdsByEnglishName = new Dictionary<string, int>();

        foreach (var w in data.Warriors)
        {
            var warrior = new WarriorArchetype
            {
                WarbandArchetypeId = warbandEntity.Id,
                IsHero = w.IsHero,
                Cost = w.Cost,
                MaxCount = w.MaxCount,
                MinCount = w.MinCount,
                StartingExperience = w.StartingExperience,
                Movement = w.Movement,
                MovementOverride = w.MovementOverride,
                WeaponSkill = w.WeaponSkill,
                BallisticSkill = w.BallisticSkill,
                Strength = w.Strength,
                Toughness = w.Toughness,
                Wounds = w.Wounds,
                Initiative = w.Initiative,
                Attacks = w.Attacks,
                Leadership = w.Leadership,
                Source = ContentSource.Official,
                IsSpellcaster = w.IsSpellcaster,
                CanBuyMutations = w.CanBuyMutations,
                MustStartWithMutation = w.MustStartWithMutation,
                EquipmentListId = w.EquipmentListName is null ? null : equipmentListIdsByName[w.EquipmentListName],
                CanUseEquipment = w.CanUseEquipment,
                AllowedSkillCategories = w.SkillCategories.Select(Enum.Parse<SkillCategory>).ToList(),
                IsLargeCreature = w.IsLargeCreature,
                GainsExperience = w.GainsExperience,
                IsLeader = w.IsLeader,
                // 0 (jamais fail-fast, contrairement à WarbandArchetype.RaceId ci-dessus) si : (a)
                // w.RacialProfileName est null (archétype qui ne gagne jamais d'Expérience - Zombie/
                // Loup Funeste/Chien de guerre/Squig des Cavernes/Troll/Rats géants - l'étape
                // Progression ne se déclenche jamais pour lui, voir WarriorOutcomeRow.
                // ShowsInExperienceStep, donc ses maximums raciaux ne sont jamais consultés) ; ou (b) le
                // nom référencé n'existe pas (encore) dans RacialProfiles.json. 0 se comporte comme
                // "aucun maximum connu, ne bloque jamais" (voir Warrior.MaxWeaponSkill etc., nullable)
                // plutôt que "plafonné à 0" - ajouter le profil manquant plus tard (Bibliothèque >
                // Profils raciaux) suffit à l'activer, aucun changement de code requis.
                RacialProfileId = w.RacialProfileName is { } racialProfileName
                    && _racialProfileIdsByEnglishName.TryGetValue(racialProfileName, out var racialProfileId)
                        ? racialProfileId
                        : 0
            };
            warrior.NameKey = await SeedTranslationAsync(w.Name.En, w.Name.Fr);
            warrior.DescriptionKey = w.Description is null ? null : await SeedTranslationAsync(w.Description.En, w.Description.Fr);
            var warriorEntity = warrior.ToEntity();
            await _db.InsertAsync(warriorEntity);
            warriorIdsByEnglishName[w.Name.En] = warriorEntity.Id;

            foreach (var sr in w.SpecialRules)
            {
                var ruleId = await FindOrCreateSpecialRuleAsync(sr);
                await _db.InsertAsync(new WarriorArchetypeSpecialRuleEntity { WarriorArchetypeId = warriorEntity.Id, SpecialRuleId = ruleId });
            }
        }

        // Différé depuis la boucle Equipment ci-dessus - les ids de guerrier n'existaient pas encore.
        foreach (var (itemId, names) in pendingEquipmentWarriorRestrictions)
        {
            foreach (var name in names)
                await _db.InsertAsync(new WarriorArchetypeEquipmentEntity { EquipmentItemId = itemId, WarriorArchetypeId = warriorIdsByEnglishName[name] });
        }

        foreach (var sk in data.Skills)
        {
            var skill = new Skill
            {
                Category = Enum.Parse<SkillCategory>(sk.Category),
                Source = ContentSource.Official
            };
            skill.NameKey = await SeedTranslationAsync(sk.Name.En, sk.Name.Fr);
            skill.DescriptionKey = sk.Description is null ? null : await SeedTranslationAsync(sk.Description.En, sk.Description.Fr);
            var skillEntity = skill.ToEntity();
            await _db.InsertAsync(skillEntity);
            _skillIdsByEnglishName[sk.Name.En] = skillEntity.Id;

            if (sk.RestrictedToThisWarband)
                await _db.InsertAsync(new WarbandArchetypeSkillEntity { WarbandArchetypeId = warbandEntity.Id, SkillId = skillEntity.Id });

            if (sk.RestrictedToWarriorNames is { Count: > 0 } names)
            {
                foreach (var name in names)
                    await _db.InsertAsync(new WarriorArchetypeSkillEntity { WarriorArchetypeId = warriorIdsByEnglishName[name], SkillId = skillEntity.Id });
            }

            if (sk.HatredTargetWarbandNames is { Count: > 0 } hatredTargetNames)
                _pendingSharedRestrictions.Add(new PendingSharedRestriction(SharedRestrictionKind.SkillHatredTarget, skillEntity.Id, hatredTargetNames));
        }

        foreach (var sp in data.Spells)
        {
            var spell = new Spell
            {
                MagicSchoolId = _magicSchoolIdsByEnglishName[sp.MagicSchoolName],
                RollValue = sp.RollValue,
                Difficulty = sp.Difficulty,
                Source = ContentSource.Official
            };
            spell.NameKey = await SeedTranslationAsync(sp.Name.En, sp.Name.Fr);
            spell.DescriptionKey = sp.Description is null ? null : await SeedTranslationAsync(sp.Description.En, sp.Description.Fr);
            await _db.InsertAsync(spell.ToEntity());
        }

        foreach (var mu in data.Mutations)
            await FindOrCreateMutationAsync(mu, warbandEntity.Id);
    }

    /// <summary>Deserializes an embedded Data/SeedData/*.json file that is a bare top-level array (the 5
    /// common catalogs, as opposed to the warband files' single top-level object) - same embedded-
    /// resource lookup as SeedWarbandFromJsonAsync.</summary>
    private static async Task<List<T>> LoadSeedArrayAsync<T>(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames().Single(n => n.EndsWith(fileName, StringComparison.Ordinal));
        await using var stream = assembly.GetManifestResourceStream(resourceName)!;
        return await JsonSerializer.DeserializeAsync<List<T>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException($"Empty or invalid seed file: {fileName}");
    }

    private async Task SeedSpecialRulesAsync()
    {
        foreach (var sr in await LoadSeedArrayAsync<SpecialRuleSeedData>("SpecialRules.json"))
            await FindOrCreateSpecialRuleAsync(sr);
    }

    /// <summary>Plain insert, no dedup lookup needed - Equipment.json is hand-authored to be duplicate-
    /// free internally, and after this runs no warband file declares any of these names anymore.</summary>
    private async Task SeedEquipmentAsync()
    {
        foreach (var eq in await LoadSeedArrayAsync<EquipmentSeedData>("Equipment.json"))
        {
            var item = new EquipmentItem
            {
                Category = Enum.Parse<EquipmentCategory>(eq.Category),
                Cost = eq.Cost,
                Rarity = eq.Rarity,
                CostRandomMax = eq.CostRandomMax,
                Source = ContentSource.Official,
                IsFreeDagger = eq.IsFreeDagger,
                Movement = eq.Movement,
                WeaponSkill = eq.WeaponSkill,
                BallisticSkill = eq.BallisticSkill,
                Strength = eq.Strength,
                Toughness = eq.Toughness,
                Wounds = eq.Wounds,
                Initiative = eq.Initiative,
                Attacks = eq.Attacks,
                Leadership = eq.Leadership,
                GrantsSkillCategory = eq.GrantsSkillCategory is { } grantsSkillCategory ? Enum.Parse<SkillCategory>(grantsSkillCategory) : null,
                GrantsSpecificSkillName = eq.GrantsSpecificSkillName,
                GrantsRareItemSearchBonus = eq.GrantsRareItemSearchBonus,
                IsSellable = eq.IsSellable,
                GrantsBonusExplorationDice = eq.GrantsBonusExplorationDice,
                IsUniqueArtefact = eq.IsUniqueArtefact,
                IsExplorationOnly = eq.IsExplorationOnly
            };
            item.NameKey = await SeedTranslationAsync(eq.Name.En, eq.Name.Fr);
            item.DescriptionKey = eq.Description is null ? null : await SeedTranslationAsync(eq.Description.En, eq.Description.Fr);
            var itemEntity = item.ToEntity();
            await _db.InsertAsync(itemEntity);
            _equipmentIdsByEnglishName[eq.Name.En] = itemEntity.Id;

            foreach (var sr in eq.SpecialRules)
            {
                var ruleId = await FindOrCreateSpecialRuleAsync(sr);
                await _db.InsertAsync(new EquipmentItemSpecialRuleEntity { EquipmentItemId = itemEntity.Id, SpecialRuleId = ruleId });
            }

            if (eq.RestrictedToWarbandNames is { Count: > 0 } eqWarbandNames)
                _pendingSharedRestrictions.Add(new PendingSharedRestriction(SharedRestrictionKind.Equipment, itemEntity.Id, eqWarbandNames));
        }
    }

    private async Task SeedMutationsAsync()
    {
        foreach (var mu in await LoadSeedArrayAsync<MutationSeedData>("Mutations.json"))
            await FindOrCreateMutationAsync(mu, warbandArchetypeId: null);
    }

    /// <summary>Plain insert, no dedup - first (and, for the standard 5 skill lists, only) source of
    /// Skill data in the whole seed pipeline.</summary>
    private async Task SeedSkillsAsync()
    {
        foreach (var sk in await LoadSeedArrayAsync<SkillSeedData>("Skills.json"))
        {
            var skill = new Skill
            {
                Category = Enum.Parse<SkillCategory>(sk.Category),
                Source = ContentSource.Official
            };
            skill.NameKey = await SeedTranslationAsync(sk.Name.En, sk.Name.Fr);
            skill.DescriptionKey = sk.Description is null ? null : await SeedTranslationAsync(sk.Description.En, sk.Description.Fr);
            var skillEntity = skill.ToEntity();
            await _db.InsertAsync(skillEntity);
            _skillIdsByEnglishName[sk.Name.En] = skillEntity.Id;

            if (sk.RestrictedToWarbandNames is { Count: > 0 } skWarbandNames)
                _pendingSharedRestrictions.Add(new PendingSharedRestriction(SharedRestrictionKind.Skill, skillEntity.Id, skWarbandNames));

            if (sk.HatredTargetWarbandNames is { Count: > 0 } skHatredTargetNames)
                _pendingSharedRestrictions.Add(new PendingSharedRestriction(SharedRestrictionKind.SkillHatredTarget, skillEntity.Id, skHatredTargetNames));
        }
    }

    /// <summary>Plain insert, no dedup - the only source of HiredSword data in the seed pipeline. Runs
    /// after SeedEquipmentAsync (needs _equipmentIdsByEnglishName populated to resolve
    /// StartingEquipmentNames) and before any SeedWarbandFromJsonAsync call (its RestrictedToWarbandNames
    /// needs the same deferred-resolution pass as Equipment/Skill/Mutation, see SeedOfficialContentAsync).</summary>
    private async Task SeedHiredSwordsAsync()
    {
        foreach (var hs in await LoadSeedArrayAsync<HiredSwordSeedData>("HiredSwords.json"))
        {
            var hiredSword = new HiredSword
            {
                HireCost = hs.HireCost,
                Upkeep = hs.Upkeep,
                BaseRating = hs.BaseRating,
                Movement = hs.Movement,
                WeaponSkill = hs.WeaponSkill,
                BallisticSkill = hs.BallisticSkill,
                Strength = hs.Strength,
                Toughness = hs.Toughness,
                Wounds = hs.Wounds,
                Initiative = hs.Initiative,
                Attacks = hs.Attacks,
                Leadership = hs.Leadership,
                AllowedSkillCategories = hs.AllowedSkillCategories.Select(Enum.Parse<SkillCategory>).ToList(),
                Source = ContentSource.Official
            };
            if (hs.MagicSchoolName is { } magicSchoolName)
                hiredSword.MagicSchoolId = await FindOrCreateMagicSchoolAsync(new MagicSchoolSeedData { Name = magicSchoolName });
            hiredSword.NameKey = await SeedTranslationAsync(hs.Name.En, hs.Name.Fr);
            hiredSword.DescriptionKey = hs.Description is null ? null : await SeedTranslationAsync(hs.Description.En, hs.Description.Fr);
            var entity = hiredSword.ToEntity();
            await _db.InsertAsync(entity);

            foreach (var itemName in hs.StartingEquipmentNames)
            {
                if (!_equipmentIdsByEnglishName.TryGetValue(itemName, out var itemId))
                    throw new InvalidOperationException($"HiredSwords.json references unknown equipment '{itemName}'");
                await _db.InsertAsync(new HiredSwordEquipmentEntity { HiredSwordId = entity.Id, EquipmentItemId = itemId });
            }

            foreach (var sr in hs.SpecialRules)
            {
                var ruleId = await FindOrCreateSpecialRuleAsync(sr);
                await _db.InsertAsync(new HiredSwordSpecialRuleEntity { HiredSwordId = entity.Id, SpecialRuleId = ruleId });
            }

            if (hs.RestrictedToWarbandNames is { Count: > 0 } hsWarbandNames)
                _pendingSharedRestrictions.Add(new PendingSharedRestriction(SharedRestrictionKind.HiredSword, entity.Id, hsWarbandNames));
        }
    }

    /// <summary>Plain insert, no dedup - the only source of DramatisPersona data in the seed pipeline.
    /// Runs AFTER all 15 SeedWarbandFromJsonAsync calls (unlike SeedHiredSwordsAsync, which runs before
    /// them) - some characters reference a band-exclusive Skill by name (e.g. Bertha/"Righteous Fury",
    /// RestrictedToThisWarband in SistersOfSigmar.json), which only exists in _skillIdsByEnglishName once
    /// that band has seeded. Side benefit: every WarbandArchetypeId already exists by this point, so
    /// RestrictedToWarbandNames resolves directly via _warbandArchetypeIdsByFileStem - no deferred-
    /// resolution pass needed here, unlike Equipment/Skill/Mutation/HiredSword (see
    /// SeedOfficialContentAsync).</summary>
    private async Task SeedDramatisPersonaeAsync()
    {
        // Résolution différée du pairage Ulli/Marquand (2026-09-01, "vous devez les recruter tous les
        // deux") - Marquand référence Ulli par nom AVANT qu'elle n'existe (elle vient après lui dans le
        // fichier), même problème que RestrictedToWarbandNames ailleurs dans cette classe. Peuplé pendant
        // la boucle, résolu juste après (les deux entrées existent alors forcément, aucun besoin de la
        // file d'attente _pendingSharedRestrictions partagée avec les catalogues communs - celle-ci ne
        // sert qu'à Equipment/Skill/Mutation, résolue APRÈS les 15 bandes, bien plus tard que ce point).
        var personaIdsByEnglishName = new Dictionary<string, int>();
        var pendingPairings = new List<(int EntityId, string PairedWithName)>();

        foreach (var dp in await LoadSeedArrayAsync<DramatisPersonaSeedData>("DramatisPersonae.json"))
        {
            var persona = new DramatisPersona
            {
                Movement = dp.Movement,
                WeaponSkill = dp.WeaponSkill,
                BallisticSkill = dp.BallisticSkill,
                Strength = dp.Strength,
                Toughness = dp.Toughness,
                Wounds = dp.Wounds,
                Initiative = dp.Initiative,
                Attacks = dp.Attacks,
                Leadership = dp.Leadership,
                FeeKind = Enum.Parse<DramatisPersonaHireFeeKind>(dp.FeeKind),
                HireCost = dp.HireCost,
                Upkeep = dp.Upkeep,
                RatingBonus = dp.RatingBonus,
                IsWanderer = dp.IsWanderer,
                RequiresRatingDisadvantage = dp.RequiresRatingDisadvantage,
                RequiresCooldownBeforeResearch = dp.RequiresCooldownBeforeResearch,
                IsHiddenFromSearchPicker = dp.HiddenFromSearchPicker,
                Source = ContentSource.Official
            };
            if (dp.MagicSchoolName is { } magicSchoolName)
                persona.MagicSchoolId = await FindOrCreateMagicSchoolAsync(new MagicSchoolSeedData { Name = magicSchoolName });
            if (dp.AlternativePaymentItemName is { } alternativePaymentItemName)
            {
                if (!_equipmentIdsByEnglishName.TryGetValue(alternativePaymentItemName, out var alternativePaymentItemId))
                    throw new InvalidOperationException($"DramatisPersonae.json references unknown equipment '{alternativePaymentItemName}'");
                persona.AlternativePaymentItemId = alternativePaymentItemId;
            }
            persona.NameKey = await SeedTranslationAsync(dp.Name.En, dp.Name.Fr);
            persona.DescriptionKey = dp.Description is null ? null : await SeedTranslationAsync(dp.Description.En, dp.Description.Fr);
            persona.PairDescriptionKey = dp.PairDescription is null ? null : await SeedTranslationAsync(dp.PairDescription.En, dp.PairDescription.Fr);
            var entity = persona.ToEntity();
            await _db.InsertAsync(entity);
            personaIdsByEnglishName[dp.Name.En] = entity.Id;
            if (dp.PairedWithPersonaName is { } pairedWithName)
                pendingPairings.Add((entity.Id, pairedWithName));

            foreach (var sr in dp.SpecialRules)
            {
                var ruleId = await FindOrCreateSpecialRuleAsync(sr);
                await _db.InsertAsync(new DramatisPersonaSpecialRuleEntity { DramatisPersonaId = entity.Id, SpecialRuleId = ruleId });
            }

            foreach (var itemName in dp.StartingEquipmentNames)
            {
                if (!_equipmentIdsByEnglishName.TryGetValue(itemName, out var itemId))
                    throw new InvalidOperationException($"DramatisPersonae.json references unknown equipment '{itemName}'");
                await _db.InsertAsync(new DramatisPersonaEquipmentEntity { DramatisPersonaId = entity.Id, EquipmentItemId = itemId });
            }

            foreach (var skillName in dp.SkillNames)
            {
                if (!_skillIdsByEnglishName.TryGetValue(skillName, out var skillId))
                    throw new InvalidOperationException($"DramatisPersonae.json references unknown skill '{skillName}'");
                await _db.InsertAsync(new DramatisPersonaSkillEntity { DramatisPersonaId = entity.Id, SkillId = skillId });
            }

            if (dp.RestrictedToWarbandNames is { Count: > 0 } dpWarbandNames)
            {
                foreach (var stem in dpWarbandNames)
                    await _db.InsertAsync(new WarbandArchetypeDramatisPersonaEntity { DramatisPersonaId = entity.Id, WarbandArchetypeId = _warbandArchetypeIdsByFileStem[stem] });
            }
        }

        foreach (var (entityId, pairedWithName) in pendingPairings)
        {
            if (!personaIdsByEnglishName.TryGetValue(pairedWithName, out var pairedWithId))
                throw new InvalidOperationException($"DramatisPersonae.json references unknown persona '{pairedWithName}' for pairedWithPersonaName");
            var entity = await _db.Table<DramatisPersonaEntity>().Where(e => e.Id == entityId).FirstAsync();
            entity.PairedWithDramatisPersonaId = pairedWithId;
            await _db.UpdateAsync(entity);
        }
    }

    /// <summary>Runs on every launch, not just first (same idiom as BackfillWarbandArchetypeRaceAsync) -
    /// SeedInjuriesAsync only runs on a genuinely empty database (see InitializeAsync), so an existing
    /// player database never picks up rows added to Injuries.json after their first launch. Added
    /// 2026-08-25 when Arm Wound (23)/Smashed Leg (25) each split from one merged catalog entry into two
    /// branch-specific rows (light "2-6"/severe "1" - see Injury.BranchRange), and extended the same day
    /// for Madness (24)'s Stupidity "1-3"/Frenzy "4-6" split : inserts whichever of those new rows are
    /// still missing (SpecialRules included, see Injury.SpecialRules), identified by (Category, RollRange,
    /// BranchRange) rather than Name/translation text - Injury has no player-facing editor that could
    /// rename that triple, unlike Name which is just display text.</summary>
    private async Task BackfillBranchedInjuriesAsync()
    {
        var existing = await _db.Table<InjuryEntity>().ToListAsync();
        foreach (var inj in await LoadSeedArrayAsync<InjurySeedData>("Injuries.json"))
        {
            if (string.IsNullOrEmpty(inj.BranchRange)) continue;

            var category = Enum.Parse<InjuryCategory>(inj.Category);
            if (existing.Any(e => e.Category == category && e.RollRange == inj.RollRange && e.BranchRange == inj.BranchRange))
                continue;

            var injury = new Injury { Category = category, RollRange = inj.RollRange, BranchRange = inj.BranchRange, Source = ContentSource.Official };
            injury.NameKey = await SeedTranslationAsync(inj.Name.En, inj.Name.Fr);
            injury.DescriptionKey = inj.Description is null ? null : await SeedTranslationAsync(inj.Description.En, inj.Description.Fr);
            var entity = injury.ToEntity();
            await _db.InsertAsync(entity);
            existing.Add(entity);

            foreach (var sr in inj.SpecialRules)
            {
                var ruleId = await FindOrCreateSpecialRuleAsync(sr);
                await _db.InsertAsync(new InjurySpecialRuleEntity { InjuryId = entity.Id, SpecialRuleId = ruleId });
            }
        }
    }

    /// <summary>Runs on every launch, after BackfillBranchedInjuriesAsync - covers a DIFFERENT gap: an
    /// Injury row that already existed (seeded or backfilled by an earlier launch) before Injuries.json
    /// gave it any SpecialRules (e.g. "Blessure au bras : amputé" existed from the Arm Wound/Smashed Leg
    /// split, 2026-08-25, but only gained its "One-Handed Weapons Only" rule in a later pass the same
    /// day). BackfillBranchedInjuriesAsync only inserts brand-new rows (and attaches their rules at
    /// insert time) - it never revisits a row that already exists, so this one specifically matches
    /// existing rows by (Category, RollRange, BranchRange) and attaches whichever SpecialRules the seed
    /// now lists, but ONLY if that row currently has zero attached (never re-adds/duplicates for a row
    /// that already has some, even if the seed's own rule list changes again later - same
    /// don't-retroactively-touch-an-already-resolved-row precedent as the rest of this file).</summary>
    private async Task BackfillInjurySpecialRulesAsync()
    {
        var existingInjuries = await _db.Table<InjuryEntity>().ToListAsync();
        var injuryIdsWithRules = (await _db.Table<InjurySpecialRuleEntity>().ToListAsync())
            .Select(l => l.InjuryId).ToHashSet();

        foreach (var inj in await LoadSeedArrayAsync<InjurySeedData>("Injuries.json"))
        {
            if (inj.SpecialRules.Count == 0) continue;

            var category = Enum.Parse<InjuryCategory>(inj.Category);
            var match = existingInjuries.FirstOrDefault(e => e.Category == category && e.RollRange == inj.RollRange && e.BranchRange == inj.BranchRange);
            if (match is null || injuryIdsWithRules.Contains(match.Id)) continue;

            foreach (var sr in inj.SpecialRules)
            {
                var ruleId = await FindOrCreateSpecialRuleAsync(sr);
                await _db.InsertAsync(new InjurySpecialRuleEntity { InjuryId = match.Id, SpecialRuleId = ruleId });
            }
        }
    }

    /// <summary>One-time text correction, runs on every launch - 3 SpecialRule catalog entries (Causes
    /// Fear, Frenzy, Stupidity) were seeded with a thin stub/summary description rather than the full
    /// official Psychology rule text ("pour ces règles là je peux t'envoyer les textes" - full text
    /// supplied by the user 2026-08-25, purely textual/reference content, no new mechanic). Neither
    /// SeedTranslationAsync nor FindOrCreateSpecialRuleAsync ever revisit an existing row (same gap as
    /// the various other Backfill* methods for other catalogs) - editing SpecialRules.json/Injuries.json
    /// alone never reaches an already-seeded database. Updates the translation VALUE in place (same
    /// Key, not a new one) so every other consumer of that key stays correctly pointed at it, and only
    /// for a row still at ContentSource.Official - an Official->Modified flip means the player edited
    /// it, never silently overwritten (same rule as everywhere else in the Library). Cheap no-op every
    /// run after the first, once the stored text already matches.</summary>
    private async Task BackfillSpecialRuleDescriptionsAsync()
    {
        var corrections = new (string EnglishName, string En, string Fr)[]
        {
            ("Causes Fear",
                "This model causes Fear. Enemies charged by it, or wishing to charge it, must first pass a Fear test (a Leadership test). Failing it when charged: the model must roll 6s to score hits that round. Failing it when trying to charge: the charge fails and the model stays stationary that turn. A model that itself causes Fear ignores these tests.",
                "Cette figurine provoque la Peur. Les ennemis qu'elle charge, ou qui souhaitent la charger, doivent d'abord réussir un test de Peur (un test de Commandement). En cas d'échec en étant chargé : seuls les 6 touchent lors de ce round. En cas d'échec en voulant charger : la charge échoue et la figurine reste immobile ce tour. Une figurine qui provoque elle-même la Peur ignore ces tests."),
            ("Frenzy",
                "A frenzied warrior must always charge any enemy within charge range - the player has no choice. He fights with double Attacks in hand-to-hand combat (an extra Attack for a second weapon isn't doubled), and is immune to all other psychology while he remains within charge range. Being knocked down or stunned ends his frenzy for the rest of the battle, after which he fights normally.",
                "Un guerrier frénétique doit toujours charger tout ennemi à portée de charge - le joueur n'a pas le choix. Il combat avec le double de ses Attaques au corps à corps (l'Attaque supplémentaire pour une arme dans chaque main n'est pas doublée), et est immunisé à toute autre règle de psychologie tant qu'il reste à portée de charge. Être mis à terre ou étourdi met fin à sa frénésie pour le reste de la bataille, après quoi il combat normalement."),
            ("Stupidity",
                "At the start of its turn, a Stupid model must pass a Leadership test or remain Stupid until its next turn: it cannot cast spells or fight in hand-to-hand combat (though enemies can still hit it normally). If not in hand-to-hand combat, roll a D6: on 1-3 it shambles forward at half speed (won't charge, stops at an obstacle or a drop, won't shoot); on 4-6 it stands inactive and does nothing.",
                "Au début de son tour, une figurine Stupide doit réussir un test de Commandement, sinon elle reste Stupide jusqu'à son prochain tour : elle ne peut ni lancer de sorts ni combattre au corps à corps (mais les ennemis peuvent toujours la toucher normalement). Si elle n'est pas au corps à corps, lancez 1D6 : sur 1-3 elle avance en traînant les pieds à vitesse réduite (ne charge pas, s'arrête devant un obstacle ou une chute, ne tire pas) ; sur 4-6 elle reste immobile et ne fait rien.")
        };

        var englishByKey = (await _db.Table<TranslationEntity>().ToListAsync())
            .Where(t => t.LanguageCode == "en")
            .ToDictionary(t => t.Key, t => t.Value);
        var rules = await _db.Table<SpecialRuleEntity>().ToListAsync();

        foreach (var (englishName, en, fr) in corrections)
        {
            var rule = rules.FirstOrDefault(r => r.Source == ContentSource.Official
                && r.NameKey is { } nk && englishByKey.TryGetValue(nk, out var name) && name == englishName);
            if (rule?.DescriptionKey is not { } descKey) continue;

            await TranslationResolver.SetAsync(this, descKey, "en", en);
            await TranslationResolver.SetAsync(this, descKey, "fr", fr);
        }
    }

    /// <summary>Plain insert, no dedup - the rulebook's Serious Injuries charts (Heroes' D66 + Henchmen's
    /// D6), common to every warband. Purely a browsable/editable reference catalog - see Injury's doc
    /// comment for why this is deliberately not wired into SeriousInjuryTable/HenchmanInjuryTable.</summary>
    private async Task SeedInjuriesAsync()
    {
        foreach (var inj in await LoadSeedArrayAsync<InjurySeedData>("Injuries.json"))
        {
            var injury = new Injury
            {
                Category = Enum.Parse<InjuryCategory>(inj.Category),
                RollRange = inj.RollRange,
                BranchRange = inj.BranchRange,
                Source = ContentSource.Official
            };
            injury.NameKey = await SeedTranslationAsync(inj.Name.En, inj.Name.Fr);
            injury.DescriptionKey = inj.Description is null ? null : await SeedTranslationAsync(inj.Description.En, inj.Description.Fr);
            var entity = injury.ToEntity();
            await _db.InsertAsync(entity);

            foreach (var sr in inj.SpecialRules)
            {
                var ruleId = await FindOrCreateSpecialRuleAsync(sr);
                await _db.InsertAsync(new InjurySpecialRuleEntity { InjuryId = entity.Id, SpecialRuleId = ruleId });
            }
        }
    }

    /// <summary>Plain insert, no dedup - the rulebook's Exploration chart (doubles through
    /// six-of-a-kind), common to every warband. EquipmentOutcome.EquipmentItemName is stored as-is (a
    /// plain name, not an id): it's resolved by lookup against the Trading Post catalog by the End of
    /// Game wizard at roll time, not at seed time - see Models.Library.ExplorationOutcome.</summary>
    private async Task SeedExplorationResultsAsync()
    {
        foreach (var res in await LoadSeedArrayAsync<ExplorationResultSeedData>("ExplorationResults.json"))
        {
            var result = new ExplorationResult
            {
                DiceCount = res.DiceCount,
                Value = res.Value,
                RollsIndependently = res.RollsIndependently,
                StatTestField = res.StatTestField is { } field ? Enum.Parse<ExplorationStatField>(field) : null,
                StatTestTargetsLeader = res.StatTestTargetsLeader,
                AutoPassStatTestWarbandArchetypeNames = res.AutoPassStatTestWarbandArchetypeNames ?? new(),
                RequiresDoubleRoll = res.RequiresDoubleRoll,
                BonusStatTestField = res.BonusStatTestField is { } bonusField ? Enum.Parse<ExplorationStatField>(bonusField) : null,
                RequiresSentHero = res.RequiresSentHero,
                Source = ContentSource.Official
            };
            result.NameKey = await SeedTranslationAsync(res.Name.En, res.Name.Fr);
            result.DescriptionKey = await SeedTranslationAsync(res.Description.En, res.Description.Fr);
            if (res.ShortDescription is { } shortDescription)
                result.ShortDescriptionKey = await SeedTranslationAsync(shortDescription.En, shortDescription.Fr);
            var resultEntity = result.ToEntity();
            await _db.InsertAsync(resultEntity);

            foreach (var outcome in res.Outcomes)
            {
                var branchTextKey = outcome.BranchText is { } branchText
                    ? await SeedTranslationAsync(branchText.En, branchText.Fr) : null;
                var nextGameNoteTextKey = outcome.NextGameNoteText is { } nextGameNoteText
                    ? await SeedTranslationAsync(nextGameNoteText.En, nextGameNoteText.Fr) : null;
                await _db.InsertAsync(new ExplorationOutcomeEntity
                {
                    ExplorationResultId = resultEntity.Id,
                    SubRollMin = outcome.SubRollMin,
                    SubRollMax = outcome.SubRollMax,
                    Kind = Enum.Parse<ExplorationOutcomeKind>(outcome.Kind),
                    GoldFormula = outcome.GoldFormula,
                    EquipmentItemName = outcome.EquipmentItemName,
                    ItemQuantityFormula = outcome.ItemQuantityFormula,
                    FoundValueFormula = outcome.FoundValueFormula,
                    MaterialRuleName = outcome.MaterialRuleName,
                    SecondaryEquipmentItemName = outcome.SecondaryEquipmentItemName,
                    AlternativeEquipmentItemName = outcome.AlternativeEquipmentItemName,
                    Note = outcome.Note,
                    BranchTextKey = branchTextKey,
                    StatTestPass = outcome.StatTestPass,
                    CausesSickness = outcome.CausesSickness,
                    RequiresDoubleRoll = outcome.RequiresDoubleRoll,
                    CausesDeath = outcome.CausesDeath,
                    TriggersArtefactRoll = outcome.TriggersArtefactRoll,
                    RestrictedToWarbandArchetypeNamesCsv = outcome.RestrictedToWarbandArchetypeNames is { Count: > 0 } names
                        ? string.Join(",", names) : null,
                    GrantsNextExplorationBonusDie = outcome.GrantsNextExplorationBonusDie,
                    GrantsLeaderExperience = outcome.GrantsLeaderExperience,
                    GrantsDistributedHeroExperienceFormula = outcome.GrantsDistributedHeroExperienceFormula,
                    GrantsFreeHenchmanArchetypeName = outcome.GrantsFreeHenchmanArchetypeName,
                    GrantsOptionalEquippedHenchman = outcome.GrantsOptionalEquippedHenchman,
                    NextGameNoteTextKey = nextGameNoteTextKey,
                    GrantsWeaponBlessing = outcome.GrantsWeaponBlessing,
                    GrantsCatacombReroll = outcome.GrantsCatacombReroll,
                    GrantsFreeHiredSword = outcome.GrantsFreeHiredSword
                });
            }
        }
    }

    private async Task SeedMagicSchoolsAsync()
    {
        foreach (var school in await LoadSeedArrayAsync<MagicSchoolWithSpellsSeedData>("MagicSchools.json"))
        {
            var schoolId = await FindOrCreateMagicSchoolAsync(new MagicSchoolSeedData { Name = school.Name, Description = school.Description });

            foreach (var sp in school.Spells)
            {
                var spell = new Spell
                {
                    MagicSchoolId = schoolId,
                    RollValue = sp.RollValue,
                    Difficulty = sp.Difficulty,
                    Source = ContentSource.Official
                };
                spell.NameKey = await SeedTranslationAsync(sp.Name.En, sp.Name.Fr);
                spell.DescriptionKey = sp.Description is null ? null : await SeedTranslationAsync(sp.Description.En, sp.Description.Fr);
                await _db.InsertAsync(spell.ToEntity());
            }
        }
    }

    /// <summary>Allocates a fresh translation key and writes the English (seed data's authoring
    /// language) and, when supplied, French values for it directly - bypasses LibraryService (which
    /// also does the Official-&gt;Modified flip check, irrelevant for a brand new insert).</summary>
    private async Task<string> SeedTranslationAsync(string en, string? fr)
    {
        var key = Guid.NewGuid().ToString("N");
        await _db.InsertAsync(new TranslationEntity { Key = key, LanguageCode = "en", Value = en });
        if (!string.IsNullOrEmpty(fr))
            await _db.InsertAsync(new TranslationEntity { Key = key, LanguageCode = "fr", Value = fr });
        return key;
    }

    /// <summary>English Name -> already-created SpecialRuleEntity id, for this seeding pass only (the
    /// whole SeedOfficialContentAsync run happens once, gated by "catalog empty" - no need to also check
    /// the DB for pre-existing rows). Lets e.g. "Leader" attached from 4 different warbands' JSON files
    /// resolve to the SAME catalog row instead of 4 duplicates - keep the English Name verbatim-identical
    /// across files for a rule meant to be shared.</summary>
    private readonly Dictionary<string, int> _specialRuleIdsByEnglishName = new();

    /// <summary>Called once at the top of InitializeAsync (2026-09-01, bug fix) - populates
    /// _specialRuleIdsByEnglishName from whatever SpecialRuleEntity rows already exist in the DB, so any
    /// Backfill* method calling FindOrCreateSpecialRuleAsync later in the same launch reuses an
    /// already-seeded rule instead of blindly inserting a duplicate (the cache is otherwise only ever
    /// populated live during a fresh SeedOfficialContentAsync pass - see the class doc). No-op on a fresh
    /// empty database (nothing to load yet, SeedOfficialContentAsync will populate the cache itself as it
    /// goes).</summary>
    private async Task WarmSpecialRuleCacheAsync()
    {
        var englishTranslations = (await _db.Table<TranslationEntity>().ToListAsync())
            .Where(t => t.LanguageCode == "en")
            .ToDictionary(t => t.Key, t => t.Value);
        foreach (var rule in await _db.Table<SpecialRuleEntity>().ToListAsync())
        {
            if (englishTranslations.TryGetValue(rule.NameKey, out var name) && !_specialRuleIdsByEnglishName.ContainsKey(name))
                _specialRuleIdsByEnglishName[name] = rule.Id;
        }
    }

    private async Task<int> FindOrCreateSpecialRuleAsync(SpecialRuleSeedData seed)
    {
        if (_specialRuleIdsByEnglishName.TryGetValue(seed.Name.En, out var existingId))
            return existingId;

        var rule = new SpecialRule { Source = ContentSource.Official, CostMultiplier = seed.CostMultiplier, Abbreviation = seed.Abbreviation, Rarity = seed.Rarity, IsResaleUpgrade = seed.IsResaleUpgrade, HatredTargetsSpellcasters = seed.HatredTargetsSpellcasters };
        rule.NameKey = await SeedTranslationAsync(seed.Name.En, seed.Name.Fr);
        rule.DescriptionKey = seed.Description is null ? null : await SeedTranslationAsync(seed.Description.En, seed.Description.Fr);
        var entity = rule.ToEntity();
        await _db.InsertAsync(entity);

        // Target WarbandArchetypes may not be seeded yet (this rule can attach from a band-level array
        // that seeds before the target band's own file) - resolved in the same deferred pass as
        // Equipment/Skill/Mutation's RestrictedToWarbandNames, see SeedOfficialContentAsync.
        if (seed.HatredTargetWarbandNames is { Count: > 0 } hatredTargets)
            _pendingSharedRestrictions.Add(new PendingSharedRestriction(SharedRestrictionKind.SpecialRule, entity.Id, hatredTargets));

        _specialRuleIdsByEnglishName[seed.Name.En] = entity.Id;
        return entity.Id;
    }

    /// <summary>English Name -> already-created MutationEntity id, same rationale/scope as
    /// _specialRuleIdsByEnglishName - lets the identical rulebook Mutations list (p.76), reused verbatim
    /// across every Chaos-adjacent warband's JSON, resolve to one shared catalog row.</summary>
    private readonly Dictionary<string, int> _mutationIdsByEnglishName = new();

    private async Task<int> FindOrCreateMutationAsync(MutationSeedData seed, int? warbandArchetypeId)
    {
        if (_mutationIdsByEnglishName.TryGetValue(seed.Name.En, out var existingId))
            return existingId;

        var mutation = new Mutation { Source = ContentSource.Official, Cost = seed.Cost };
        mutation.NameKey = await SeedTranslationAsync(seed.Name.En, seed.Name.Fr);
        mutation.DescriptionKey = seed.Description is null ? null : await SeedTranslationAsync(seed.Description.En, seed.Description.Fr);
        var entity = mutation.ToEntity();
        await _db.InsertAsync(entity);

        if (seed.RestrictedToThisWarband && warbandArchetypeId is not null)
            await _db.InsertAsync(new WarbandArchetypeMutationEntity { WarbandArchetypeId = warbandArchetypeId.Value, MutationId = entity.Id });

        if (seed.RestrictedToWarbandNames is { Count: > 0 } muWarbandNames)
            _pendingSharedRestrictions.Add(new PendingSharedRestriction(SharedRestrictionKind.Mutation, entity.Id, muWarbandNames));

        _mutationIdsByEnglishName[seed.Name.En] = entity.Id;
        return entity.Id;
    }

    /// <summary>English Name -> already-created MagicSchoolEntity id, same rationale/scope as
    /// _specialRuleIdsByEnglishName - a school like "Necromancy" is declared once per warband's
    /// WarbandSeedData.MagicSchools and then referenced by its Spell entries via
    /// SpellSeedData.MagicSchoolName.</summary>
    private readonly Dictionary<string, int> _magicSchoolIdsByEnglishName = new();

    private async Task<int> FindOrCreateMagicSchoolAsync(MagicSchoolSeedData seed)
    {
        if (_magicSchoolIdsByEnglishName.TryGetValue(seed.Name.En, out var existingId))
            return existingId;

        var school = new MagicSchool { Source = ContentSource.Official };
        school.NameKey = await SeedTranslationAsync(seed.Name.En, seed.Name.Fr);
        school.DescriptionKey = seed.Description is null ? null : await SeedTranslationAsync(seed.Description.En, seed.Description.Fr);
        var entity = school.ToEntity();
        await _db.InsertAsync(entity);

        _magicSchoolIdsByEnglishName[seed.Name.En] = entity.Id;
        return entity.Id;
    }

    private async Task SeedRacesAsync()
    {
        foreach (var seed in await LoadSeedArrayAsync<RaceSeedData>("Races.json"))
            await FindOrCreateRaceAsync(seed);
    }

    /// <summary>English Name -> already-created RaceEntity id, same rationale as
    /// _magicSchoolIdsByEnglishName - a race like "Human" is shared across most of the 15 warband files.
    /// Unlike the other FindOrCreateXAsync helpers, also checks the DATABASE (not just this in-memory
    /// dict) before creating: BackfillWarbandArchetypeRaceAsync calls this too, on a launch where
    /// SeedOfficialContentAsync (and therefore this dict) never ran because the catalog wasn't empty -
    /// without the DB check, an already-seeded machine would get a duplicate Race row every launch.</summary>
    private readonly Dictionary<string, int> _raceIdsByEnglishName = new();

    private async Task<int> FindOrCreateRaceAsync(RaceSeedData seed)
    {
        if (_raceIdsByEnglishName.TryGetValue(seed.Name.En, out var existingId))
            return existingId;

        var existingKeys = (await _db.Table<TranslationEntity>().ToListAsync())
            .Where(t => t.LanguageCode == "en" && t.Value == seed.Name.En)
            .Select(t => t.Key).ToHashSet();
        if (existingKeys.Count > 0)
        {
            var existingRace = (await _db.Table<RaceEntity>().ToListAsync()).FirstOrDefault(r => existingKeys.Contains(r.NameKey));
            if (existingRace is not null)
            {
                _raceIdsByEnglishName[seed.Name.En] = existingRace.Id;
                return existingRace.Id;
            }
        }

        var race = new Race { Source = ContentSource.Official };
        race.NameKey = await SeedTranslationAsync(seed.Name.En, seed.Name.Fr);
        race.DescriptionKey = seed.Description is null ? null : await SeedTranslationAsync(seed.Description.En, seed.Description.Fr);
        var entity = race.ToEntity();
        await _db.InsertAsync(entity);

        _raceIdsByEnglishName[seed.Name.En] = entity.Id;
        return entity.Id;
    }

    private async Task SeedRacialProfilesAsync()
    {
        foreach (var seed in await LoadSeedArrayAsync<RacialProfileSeedData>("RacialProfiles.json"))
            await FindOrCreateRacialProfileAsync(seed);
    }

    /// <summary>English Name -> already-created RacialProfileEntity id, same rationale/DB-aware
    /// find-or-create as _raceIdsByEnglishName above (a creature type like "Human" or "Skaven" is
    /// shared by dozens of WarriorArchetypes across the 15 warband files).</summary>
    private readonly Dictionary<string, int> _racialProfileIdsByEnglishName = new();

    private async Task<int> FindOrCreateRacialProfileAsync(RacialProfileSeedData seed)
    {
        if (_racialProfileIdsByEnglishName.TryGetValue(seed.Name.En, out var existingId))
            return existingId;

        var existingKeys = (await _db.Table<TranslationEntity>().ToListAsync())
            .Where(t => t.LanguageCode == "en" && t.Value == seed.Name.En)
            .Select(t => t.Key).ToHashSet();
        if (existingKeys.Count > 0)
        {
            var existingProfile = (await _db.Table<RacialProfileEntity>().ToListAsync()).FirstOrDefault(r => existingKeys.Contains(r.NameKey));
            if (existingProfile is not null)
            {
                _racialProfileIdsByEnglishName[seed.Name.En] = existingProfile.Id;
                return existingProfile.Id;
            }
        }

        var profile = new RacialProfile
        {
            Source = ContentSource.Official,
            Movement = seed.Movement,
            MovementOverride = seed.MovementOverride,
            WeaponSkill = seed.WeaponSkill,
            BallisticSkill = seed.BallisticSkill,
            Strength = seed.Strength,
            Toughness = seed.Toughness,
            Wounds = seed.Wounds,
            Initiative = seed.Initiative,
            Attacks = seed.Attacks,
            Leadership = seed.Leadership
        };
        profile.NameKey = await SeedTranslationAsync(seed.Name.En, seed.Name.Fr);
        profile.DescriptionKey = seed.Description is null ? null : await SeedTranslationAsync(seed.Description.En, seed.Description.Fr);
        var entity = profile.ToEntity();
        await _db.InsertAsync(entity);

        _racialProfileIdsByEnglishName[seed.Name.En] = entity.Id;
        return entity.Id;
    }

    /// <summary>English Name -> already-created EquipmentItemEntity id - populated by both the common
    /// pool (SeedEquipmentAsync, plain insert, no dedup needed since Equipment.json is hand-authored
    /// duplicate-free) and by SeedWarbandFromJsonAsync's own find-or-create Equipment loop (needed for
    /// Rare items shared by exactly a couple of bands with different restrictions, e.g. Holy Tome -
    /// Warrior-Priest for Witch Hunters, Heroines for Sisters of Sigmar - one catalog row, two sets of
    /// restriction rows). Also consumed when resolving EquipmentListSeedData.ItemNames.</summary>
    private readonly Dictionary<string, int> _equipmentIdsByEnglishName = new();

    /// <summary>English Name -> SkillEntity id, populated by SeedSkillsAsync - same purpose as
    /// _equipmentIdsByEnglishName, needed to resolve DramatisPersonaSeedData.SkillNames (a Dramatis
    /// Persona's fixed known-skills list, not a WarriorArchetype pick-from-category Advance table).</summary>
    private readonly Dictionary<string, int> _skillIdsByEnglishName = new();

    /// <summary>Warband JSON file stem (e.g. "Reiklanders", from SeedWarbandFromJsonAsync's fileName
    /// without extension) -> WarbandArchetypeEntity id, populated as each of the 15 warband files seeds.
    /// Lets a common-catalog entry (Equipment/Skill/Mutation) declared BEFORE any warband exists still
    /// name several bands via RestrictedToWarbandNames - see _pendingSharedRestrictions.</summary>
    private readonly Dictionary<string, int> _warbandArchetypeIdsByFileStem = new();

    private enum SharedRestrictionKind { Equipment, Skill, Mutation, SpecialRule, HiredSword, SkillHatredTarget }

    private record struct PendingSharedRestriction(SharedRestrictionKind Kind, int ItemId, List<string> WarbandFileStems);

    /// <summary>Common-catalog restrictions naming several bands (RestrictedToWarbandNames) can't resolve
    /// a WarbandArchetypeId at the point they're seeded (SeedEquipmentAsync/SeedSkillsAsync/
    /// FindOrCreateMutationAsync all run before any warband file) - collected here and resolved in one
    /// pass at the end of SeedOfficialContentAsync, once every band exists.</summary>
    private readonly List<PendingSharedRestriction> _pendingSharedRestrictions = new();
}
