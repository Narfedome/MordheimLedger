using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Components;

/// <summary>Sélecteur « Race » des formulaires du Codex (type de guerrier, Franc-Tireur, Dramatis Persona) : première
/// entrée = pas de race propre (celle de la bande pour un type de guerrier, « Non précisée » ailleurs), puis le catalogue
/// des races. Écrit le choix via <c>apply</c> (null pour la première entrée). Les races se chargent en arrière-plan pour ne
/// pas toucher aux constructeurs des formulaires ; seul un choix de l'utilisateur appelle <c>apply</c>.</summary>
public partial class RaceChoice : ObservableObject
{
    private readonly Dictionary<string, int> _idByLabel = new();
    private readonly Action<int?> _apply;
    private bool _loading;

    public ObservableCollection<string> Options { get; } = new();

    [ObservableProperty]
    private string selectedLabel;

    public RaceChoice(int? currentRaceId, string noneLabel, ILibraryService libraryService, Action<int?> apply)
    {
        _apply = apply;
        Options.Add(noneLabel);
        selectedLabel = noneLabel;
        _ = LoadAsync(currentRaceId, libraryService);
    }

    private async Task LoadAsync(int? currentRaceId, ILibraryService libraryService)
    {
        var races = await libraryService.GetRacesAsync(LocalizationService.Instance.Language);
        _loading = true;
        try
        {
            foreach (var race in races.OrderBy(r => r.Name, StringComparer.CurrentCulture))
            {
                if (!_idByLabel.TryAdd(race.Name, race.Id)) continue;
                Options.Add(race.Name);
            }
            if (races.FirstOrDefault(r => r.Id == currentRaceId) is { } current) SelectedLabel = current.Name;
        }
        finally { _loading = false; }
    }

    partial void OnSelectedLabelChanged(string value)
    {
        if (_loading) return;
        _apply(_idByLabel.TryGetValue(value ?? string.Empty, out var id) ? id : null);
    }
}
