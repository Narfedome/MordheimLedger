using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Exchange;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Settings.ContentConflicts;

namespace MordheimLedgerApp.Features.Exchange;

/// <summary>Une bande reçue. Déjà sur cet appareil (même identifiant d'échange) : remplacer sa version ou ajouter une copie.</summary>
public sealed partial class ImportWarbandRow : ObservableObject
{
    public ImportWarbandRow(WarbandImportItem item, int index)
    {
        Item = item;
        GroupName = $"ImportWarband{index}";
    }

    public WarbandImportItem Item { get; }
    public string GroupName { get; }
    public string Name => Item.Name;
    public string Detail => string.Format(LocalizationServiceText["ImportWarbandDetail"], Item.ArchetypeName, Item.WarriorCount);
    public bool IsExisting => Item.ExistingWarbandId is not null;
    public string ExistingNote => string.Format(LocalizationServiceText["ImportWarbandExisting"], Item.ExistingName);

    /// <summary>Remplacer est pré-coché (maquette retenue, variante A) : c'est le cas courant, la bande d'un ami renvoyée
    /// après une partie.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AddCopy))]
    private bool replace = true;

    public bool AddCopy
    {
        get => !Replace;
        set => Replace = !value;
    }

    private static Services.LocalizationService LocalizationServiceText => Services.LocalizationService.Instance;
}

/// <summary>Une entrée du Codex déjà présente mais différente : garder la sienne (par défaut) ou prendre la reçue.</summary>
public sealed partial class ImportConflictRow : ObservableObject
{
    public ImportConflictRow(CatalogImportItem item, int index)
    {
        Item = item;
        GroupName = $"ImportConflict{index}";
        Differences = item.Differences
            .Select(d => new ContentDifferenceRow(ContentFieldText.FieldLabel(d.Field),
                ContentFieldText.Blank(ContentFieldText.ValueText(d.Field, d.Official)),
                ContentFieldText.Blank(ContentFieldText.ValueText(d.Field, d.Mine))))
            .ToList();
    }

    public CatalogImportItem Item { get; }
    public string GroupName { get; }
    public string Name => Item.Name;
    public string TypeLabel => ContentFieldText.TypeLabel(Item.Table);

    /// <summary>Official = reçue, Mine = la sienne (même ligne que « Versions à départager »).</summary>
    public IReadOnlyList<ContentDifferenceRow> Differences { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeepMine))]
    private bool takeIncoming;

    public bool KeepMine
    {
        get => !TakeIncoming;
        set => TakeIncoming = !value;
    }
}

/// <summary>Une entrée du Codex qui sera ajoutée.</summary>
public sealed record ImportNewRow(string Name, string Detail);

/// <summary>Écran d'import d'un fichier .mordheim (v2, lot 2 - maquette « Importer un fichier reçu », variante A) : une
/// carte par bande reçue, une pour les nouveautés du Codex, une par entrée en conflit. Résultat : les choix du joueur,
/// ou null s'il abandonne.</summary>
public partial class ImportPreviewViewModel : DialogViewModel<ImportDecisions?>
{
    public ImportPreviewViewModel(ImportPreview preview, string fileName)
    {
        FileName = fileName;
        Warbands = preview.Warbands.Select((w, i) => new ImportWarbandRow(w, i)).ToList();
        NewEntries = preview.Catalog
            .Where(c => c.Status == CatalogImportStatus.New)
            .Select(c => new ImportNewRow(c.Name, c.Source == ContentSource.Official
                ? string.Format(Loc["ImportNewOfficial"], ContentFieldText.TypeLabel(c.Table))
                : ContentFieldText.TypeLabel(c.Table)))
            .OrderBy(r => r.Detail, StringComparer.CurrentCulture).ThenBy(r => r.Name, StringComparer.CurrentCulture)
            .ToList();
        Conflicts = preview.Catalog.Where(c => c.Status == CatalogImportStatus.Conflict)
            .Select((c, i) => new ImportConflictRow(c, i)).ToList();
        var unchanged = preview.Catalog.Count(c => c.Status is CatalogImportStatus.Existing or CatalogImportStatus.Identical);
        UnchangedNote = unchanged > 0 ? string.Format(Loc["ImportUnchanged"], unchanged) : string.Empty;
    }

    protected override ImportDecisions? CancelResult => null;

    public string Title => Loc["ImportTitle"];
    public string FileName { get; }
    public IReadOnlyList<ImportWarbandRow> Warbands { get; }
    public IReadOnlyList<ImportNewRow> NewEntries { get; }
    public IReadOnlyList<ImportConflictRow> Conflicts { get; }
    public bool HasWarbands => Warbands.Count > 0;
    public bool HasNewEntries => NewEntries.Count > 0;
    public string UnchangedNote { get; }
    public bool HasUnchangedNote => UnchangedNote.Length > 0;

    [RelayCommand]
    private void SetReplace(ImportWarbandRow row) => row.Replace = true;

    [RelayCommand]
    private void SetAddCopy(ImportWarbandRow row) => row.Replace = false;

    [RelayCommand]
    private void SetKeepMine(ImportConflictRow row) => row.TakeIncoming = false;

    [RelayCommand]
    private void SetTakeIncoming(ImportConflictRow row) => row.TakeIncoming = true;

    [RelayCommand]
    private void Import()
    {
        var decisions = new ImportDecisions();
        foreach (var conflict in Conflicts.Where(c => c.TakeIncoming))
            decisions.TakeIncoming.Add(conflict.Item.EntryId);
        foreach (var warband in Warbands.Where(w => w.IsExisting && w.Item.ShareId is not null))
            decisions.Warbands[warband.Item.ShareId!] = warband.Replace ? WarbandImportMode.Replace : WarbandImportMode.NewCopy;
        Close(decisions);
    }
}
