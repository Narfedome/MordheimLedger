using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Settings.ContentConflicts;

/// <summary>Un champ qui diffère, côte à côte.</summary>
public sealed record ContentDifferenceRow(string Label, string Official, string Mine);

/// <summary>Une entrée Modifiée en attente de choix.</summary>
public sealed record ContentConflictRow(string OfficialId, string Name, string TypeLabel, IReadOnlyList<ContentDifferenceRow> Differences);

/// <summary>Entrées que le joueur avait modifiées et dont la version officielle a changé à une synchro :
/// pour chacune, les champs qui diffèrent côte à côte et le choix garder la sienne / prendre l'officielle
/// (AppDatabase.ResolveContentConflictAsync). Résultat du dialog : true si au moins une entrée a été
/// remplacée par l'officielle (le catalogue a changé).</summary>
public partial class ContentConflictsDialogViewModel : DialogViewModel<bool>
{
    private readonly AppDatabase _db;
    private bool _catalogChanged;

    protected override bool CancelResult => _catalogChanged;

    public ObservableCollection<ContentConflictRow> Conflicts { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(KeepMineCommand), nameof(TakeOfficialCommand))]
    private bool isBusy;

    public ContentConflictsDialogViewModel(AppDatabase db, IEnumerable<ContentConflict> conflicts)
    {
        _db = db;
        Conflicts = new ObservableCollection<ContentConflictRow>(conflicts.Select(c => new ContentConflictRow(
            c.OfficialId, c.Name, TypeLabel(c.TableName),
            c.Differences.Select(d => new ContentDifferenceRow(FieldLabel(d.Field), Blank(d.Official), Blank(d.Mine))).ToList())));
    }

    private bool CanResolve => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanResolve))]
    private Task KeepMine(ContentConflictRow row) => ResolveAsync(row, takeOfficial: false);

    [RelayCommand(CanExecute = nameof(CanResolve))]
    private Task TakeOfficial(ContentConflictRow row) => ResolveAsync(row, takeOfficial: true);

    private async Task ResolveAsync(ContentConflictRow row, bool takeOfficial)
    {
        IsBusy = true;
        try
        {
            await _db.ResolveContentConflictAsync(row.OfficialId, takeOfficial, OfficialContentService.OpenEmbeddedSeedAsync);
            _catalogChanged |= takeOfficial;
            Conflicts.Remove(row);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex);
        }
        finally
        {
            IsBusy = false;
        }
        if (Conflicts.Count == 0) Close(_catalogChanged);
    }

    private string Blank(string value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

    private string TypeLabel(string tableName) => LocOrNull("ContentType" + tableName) ?? Humanize(tableName.Replace("Entity", ""));

    /// <summary>Libellé lisible d'un champ (colonne ou table de jointure, voir ContentFieldDifference) - réutilise
    /// les libellés du Codex quand ils existent, sinon le nom de colonne découpé.</summary>
    private string FieldLabel(string field)
    {
        if (field.EndsWith("SpecialRuleEntity")) return Loc["LibSpecialRulesPh"];
        if (field == "WarbandArchetypeMagicSchoolEntity") return Loc["LibMagicSchoolsPh"];
        if (field.StartsWith("WarbandArchetype") && field.EndsWith("Entity")) return Loc["LibRestrictedToWarbandsPh"];
        if (field.StartsWith("WarriorArchetype") && field.EndsWith("Entity")) return Loc["LibRestrictedToWarriorsPh"];
        if (field.EndsWith("EquipmentEntity") || field == "EquipmentListItemEntity") return Loc["WarriorsEquipmentHeading"];
        if (field.EndsWith("SkillEntity")) return Loc["WarriorsSkillsHeading"];
        return field switch
        {
            "NameKey" => Loc["ContentFieldName"],
            "DescriptionKey" => Loc["LibDescriptionPh"],
            "ImagePath" => Loc["ContentFieldImage"],
            "Cost" => Loc["EquipmentItemCostPh"],
            "Rarity" => Loc["EquipmentItemRarityPh"],
            "Category" => Loc["EquipmentItemCategoryPh"],
            "HireCost" => Loc["HiredSwordHireCostPh"],
            "RollValue" => Loc["LibRollAbbr"],
            "Difficulty" => Loc["LibDifficultyAbbr"],
            "Movement" or "WeaponSkill" or "BallisticSkill" or "Strength" or "Toughness" or "Wounds" or "Initiative" or "Attacks" or "Leadership"
                => Loc[$"Stat{field}Abbr"],
            "RaceId" or "RacialProfileId" or "WarbandArchetypeId" or "EquipmentListId" or "MagicSchoolId"
                => Loc[$"ContentType{field[..^2]}Entity"],
            _ => Humanize(field.EndsWith("Key") ? field[..^3] : field.EndsWith("Id") ? field[..^2] : field),
        };
    }

    private string? LocOrNull(string key) => Loc[key] is var value && value != key ? value : null;

    /// <summary>"MaxWarriors" -> "Max warriors".</summary>
    private static string Humanize(string pascalCase)
    {
        var words = Regex.Replace(pascalCase, "(?<=[a-z])(?=[A-Z])", " ").ToLowerInvariant();
        return words.Length == 0 ? words : char.ToUpperInvariant(words[0]) + words[1..];
    }
}
