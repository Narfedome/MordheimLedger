using System.Collections.ObjectModel;
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
            c.OfficialId, c.Name, ContentFieldText.TypeLabel(c.TableName),
            c.Differences.Select(d => new ContentDifferenceRow(ContentFieldText.FieldLabel(d.Field), ContentFieldText.Blank(ContentFieldText.ValueText(d.Field, d.Official)),
                ContentFieldText.Blank(ContentFieldText.ValueText(d.Field, d.Mine)))).ToList())));
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
}
