using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Lobby;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Lobby;

/// <summary>Un guerrier adverse, en texte : la fiche adverse est une consultation, pas une feuille à éditer.</summary>
public sealed record OpponentWarriorRow(Warrior Warrior, string Title, string RoleName, string Equipment, string Skills, string Injuries)
{
    public bool HasEquipment => Equipment.Length > 0;
    public bool HasSkills => Skills.Length > 0;
    public bool HasInjuries => Injuries.Length > 0;
}

/// <summary>Fiche d'une bande adverse en lecture seule (v2, lot 4a - maquette « Partie connectée », fiche adverse) : ni
/// crayon, ni lancement de partie ; effacée à la fin de la partie (LobbySession).</summary>
[QueryProperty(nameof(PlayerId), "playerId")]
public partial class OpponentWarbandViewModel : BaseViewModel
{
    private readonly LobbySession _session;

    public OpponentWarbandViewModel(LobbySession session) => _session = session;

    [ObservableProperty]
    private string playerId = string.Empty;

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string subtitle = string.Empty;

    [ObservableProperty]
    private string summary = string.Empty;

    public ObservableCollection<OpponentWarriorRow> Warriors { get; } = [];

    partial void OnPlayerIdChanged(string value) => _ = UiErrors.RunAsync(LoadAsync, nameof(OpponentWarbandViewModel));

    private async Task LoadAsync()
    {
        var player = _session.State.Players.FirstOrDefault(p => p.Id == PlayerId);
        Title = player?.Warband?.Name ?? string.Empty;
        Subtitle = string.Format(Loc["OpponentSubtitle"], player?.Name);
        OpponentRoster? roster = null;
        await Loading.RunAsync(async () => roster = await _session.LoadOpponentAsync(PlayerId));
        if (roster is null) return;
        Summary = string.Format(Loc["OpponentSummary"], roster.ArchetypeName, roster.Warband.Treasury, roster.Warband.WyrdstoneShards, roster.Rating);
        Warriors.Clear();
        foreach (var (warrior, role) in roster.Warriors)
            Warriors.Add(new OpponentWarriorRow(warrior,
                warrior.HeadCount > 1 ? $"{warrior.Name} (×{warrior.HeadCount})" : warrior.Name,
                role,
                string.Join(", ", warrior.Equipment.Select(e => e.Quantity > 1 ? $"{e.NameDisplay} ×{e.Quantity}" : e.NameDisplay)),
                string.Join(", ", warrior.Skills.Select(s => s.Item.Name)),
                string.Join(", ", warrior.Injuries.Select(i => i.Item.Name))));
    }

    [RelayCommand]
    private Task Back() => Shell.Current.GoToAsync("..");
}
