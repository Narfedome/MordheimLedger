using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Étape « Grimoire » (1.6, livre + FAQ) : une carte par Grimoire acquis pendant cette Fin de partie (objet rare,
/// Exploration, scénario). Un lanceur de sorts gagne un sort de plus, tiré dans sa liste ou en Magie Mineure ; sans
/// lanceur, un Héros avec Savoir Arcanique apprend la Magie Mineure et tire son premier sort ; sinon l'effet est perdu.
/// Un seul guerrier en profite, une seule fois. Règles : Core.Rules.TomeOfMagicRules/SpellRules ; écritures :
/// Core.Services.SpellLearningService.</summary>
public partial class EndOfGamePageViewModel
{
    public ObservableCollection<TomeUseEntry> TomeEntries { get; } = new();

    private IReadOnlyList<Spell>? _spellCatalog;
    private IReadOnlyList<MagicSchool>? _magicSchoolCatalog;

    /// <summary>Grimoires acquis pendant cette Fin de partie - l'étape n'existe que s'il y en a.</summary>
    private int TomeAcquiredCount =>
        TomeOfMagicRules.AcquiredCount(ReserveGains().Select(g => (g.Inflow.Item.OfficialId, g.Inflow.Quantity)));

    private async Task BuildTomeEntriesAsync()
    {
        var language = LocalizationService.Instance.Language;
        _spellCatalog ??= await _libraryService.GetSpellsAsync(language);
        _magicSchoolCatalog ??= await _libraryService.GetMagicSchoolsAsync(language);
        var lesser = _magicSchoolCatalog.FirstOrDefault(s => s.OfficialId == OfficialIds.LesserMagicSchool);

        var count = TomeAcquiredCount;
        // Le joueur revient sur l'étape sans rien changer : ses choix sont gardés.
        if (TomeEntries.Count == count && count > 0) return;

        var living = _allActiveWarriorRows.Where(r => r.Warrior.Status != WarriorStatus.Dead && !IsDeadThisBattle(r.Warrior)).ToList();
        var candidates = living.Select(r => new TomeCandidate(r.Warrior, r.HasMagicSchools,
            r.Warrior.Skills.Any(s => s.Item.OfficialId == OfficialIds.ArcaneLoreSkill))).ToList();
        var (kind, users) = TomeOfMagicRules.Users(candidates);
        var rowsByWarrior = living.ToDictionary(r => r.Warrior);

        TomeEntries.Clear();
        for (var i = 0; i < count; i++)
        {
            var options = users.Select(w => new TomeUserOption(w, rowsByWarrior[w].RoleName,
                kind == TomeUseKind.ExtraSpell ? rowsByWarrior[w].MagicSchools.ToList() : new List<MagicSchool>())).ToList();
            TomeEntries.Add(new TomeUseEntry(i + 1, kind, options, lesser, _spellCatalog));
        }
    }

    private TomeUseEntry? TomeEntryOf(TomeUserOption option) => TomeEntries.FirstOrDefault(e => e.Users.Contains(option));

    [RelayCommand]
    private void SelectTomeUser(TomeUserOption option) => TomeEntryOf(option)?.SelectUser(option);

    [RelayCommand]
    private void SelectTomeList(TomeListOption option) =>
        TomeEntries.FirstOrDefault(e => e.Lists.Contains(option))?.SelectList(option);

    [RelayCommand]
    private void AutoRollTome(TomeUseEntry entry) => entry.Roll = SpellRules.RollDice().ToString();

    /// <summary>À Terminer : chaque Grimoire tiré apprend son sort (ou réduit sa difficulté) au guerrier choisi.</summary>
    private async Task ApplyTomeOfMagicAsync(List<string> sentences)
    {
        if (TomeEntries.Count == 0) return;
        var learning = new SpellLearningService(_warbandService);
        var lesser = _magicSchoolCatalog?.FirstOrDefault(s => s.OfficialId == OfficialIds.LesserMagicSchool);

        foreach (var entry in TomeEntries)
        {
            if (entry.IsNobody)
            {
                sentences.Add(Loc["HistoryTomeUnusedSentence"]);
                continue;
            }
            if (entry.SelectedUser is not { } user || entry.ResolvedSpell is not { } spell) continue;

            var outcome = entry.IsArcaneLore && lesser is not null
                ? await learning.LearnSchoolAsync(user.Warrior, lesser, spell)
                : await learning.LearnAsync(user.Warrior, spell);
            sentences.Add(outcome == SpellLearningOutcome.DifficultyReduced
                ? string.Format(Loc["HistoryTomeDuplicateSentence"], user.Name, spell.Name)
                : entry.IsArcaneLore
                    ? string.Format(Loc["HistoryTomeLesserMagicSentence"], user.Name, spell.Name)
                    : string.Format(Loc["HistoryTomeSpellSentence"], user.Name, spell.Name));
        }
    }
}
