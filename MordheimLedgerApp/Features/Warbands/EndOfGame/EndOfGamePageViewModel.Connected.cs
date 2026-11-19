using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Core.Lobby;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Fin de partie connectée (v2, lot 4b - maquette « Fin de partie connectée », variante A) : vainqueur proposé à
/// tous, captifs négociés d'un appareil à l'autre (LobbySession.Captures) - côté propriétaire sous le jet 61 de l'étape
/// Blessures, côté capteur dans Prisonniers ennemis. « Décider à la table » repasse à la saisie habituelle. Hors partie
/// connectée, rien ne change.</summary>
public partial class EndOfGamePageViewModel
{
    private LobbySession _lobby = null!;

    /// <summary>Partie connectée jouée avec cette bande (fixé au chargement).</summary>
    [ObservableProperty]
    private bool isConnectedGame;

    public ObservableCollection<WinnerOptionRow> WinnerOptions { get; } = [];

    [ObservableProperty]
    private string resultChosenByText = string.Empty;

    /// <summary>Le joueur a choisi lui-même le vainqueur : un choix reçu ensuite ne l'écrase pas.</summary>
    private bool _resultTouched;

    public ObservableCollection<ConnectedCaptiveRow> ConnectedCaptives { get; } = [];

    [ObservableProperty]
    private bool hasConnectedCaptives;

    private void InitializeConnectedGame()
    {
        IsConnectedGame = Warband is { } warband && _lobby.IsPlayingWith(warband.Id);
        if (!IsConnectedGame || _lobby.Connection is not { } connection) return;

        foreach (var player in _lobby.State.Players.Where(p => p.Warband is not null))
            WinnerOptions.Add(new WinnerOptionRow(player.Id, player.Id == connection.PlayerId
                ? string.Format(Loc["ConnectedWinnerYou"], player.Warband!.Name)
                : string.Format(Loc["ConnectedWinnerPlayer"], player.Name, player.Warband!.Name)));
        WinnerOptions.Add(new WinnerOptionRow(null, Loc["EndOfGameResultDraw"]));

        var captors = _lobby.Opponents.Select(p => new CaptorOption(p.Id, p.Name)).ToList();
        foreach (var row in WarriorRows.Where(r => r.Warrior.IsHero))
        {
            row.ConnectedCapture = new ConnectedCaptureState(row, captors);
            row.ConnectedCapture.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ConnectedCaptureState.SelectedCaptor)) _ = SyncCaptureAsync(row);
            };
            row.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WarriorOutcomeRow.ShowCapturedChoice)) _ = SyncCaptureAsync(row);
            };
        }

        WeakReferenceMessenger.Default.Register<LobbyChangedMessage>(this, (r, m) => ((EndOfGamePageViewModel)r).RefreshConnectedGame());
        RefreshConnectedGame();
    }

    private void RefreshConnectedGame()
    {
        if (!IsConnectedGame) return;
        ApplyReceivedResult();

        // Propriétaire : chaque guerrier capturé suit son échange ; payé = racheté, refusé = perdu (saisie habituelle).
        if (_lobby.Captures is { } captures)
        {
            foreach (var row in WarriorRows.Where(r => r.ConnectedCapture is not null))
            {
                var capture = captures.FindByWarrior(row.Warrior.Id);
                row.ConnectedCapture!.Update(capture, Warband?.Treasury ?? 0);
                if (capture?.Status == CaptureStatus.Accepted)
                {
                    row.IsRansomed = true;
                    row.RansomAmount = capture.Amount?.ToString() ?? "0";
                }
                else if (capture?.Status == CaptureStatus.Refused)
                {
                    row.IsRansomed = false;
                }
            }

            // Capteur : un captif par guerrier adverse déclaré, en gardant les saisies en cours.
            var held = captures.Held;
            foreach (var gone in ConnectedCaptives.Where(c => held.All(h => h.Id != c.Id)).ToList())
                ConnectedCaptives.Remove(gone);
            foreach (var capture in held)
            {
                if (ConnectedCaptives.FirstOrDefault(c => c.Id == capture.Id) is { } existing) existing.Update(capture);
                else ConnectedCaptives.Add(new ConnectedCaptiveRow(capture, _lobby.PlayerNameOf(capture.OwnerPlayerId), IsUndeadWarband, IsPossessedWarband));
            }
        }
        HasConnectedCaptives = ConnectedCaptives.Count > 0;
    }

    // --- Résultat ---------------------------------------------------------------------------------------------

    private WinnerOptionRow? _selectedWinner;

    /// <summary>Vainqueur coché (RadioButtonGroup.SelectedValue, TwoWay) : un choix du joueur est proposé aux autres ;
    /// le null transitoire d'une vue réattachée est ignoré (même parade que SelectedResult).</summary>
    public WinnerOptionRow? SelectedWinner
    {
        get => _selectedWinner;
        set
        {
            if (value is null || value == _selectedWinner) return;
            SelectWinner(value);
        }
    }

    [RelayCommand]
    private void SelectWinner(WinnerOptionRow row)
    {
        if (row == _selectedWinner && _resultTouched) return;
        _resultTouched = true;
        SelectWinnerRow(row);
        ResultChosenByText = string.Empty;
        _ = _lobby.ProposeResultAsync(row.PlayerId);
    }

    private void ApplyReceivedResult()
    {
        if (_resultTouched || _lobby.GameResult is not { } result || _lobby.Connection is not { } connection) return;
        if (WinnerOptions.FirstOrDefault(o => o.PlayerId == result.WinnerPlayerId) is not { } row) return;
        SelectWinnerRow(row);
        ResultChosenByText = result.ChosenBy == connection.PlayerId
            ? string.Empty
            : string.Format(Loc["ConnectedResultChosenBy"], _lobby.PlayerNameOf(result.ChosenBy), SelectedResult);
    }

    /// <summary>Vainqueur coché et résultat de cette bande déduit (victoire, défaite, nul) - le choix habituel reste
    /// modifiable en dessous.</summary>
    private void SelectWinnerRow(WinnerOptionRow row)
    {
        foreach (var option in WinnerOptions) option.IsSelected = option == row;
        _selectedWinner = row;
        OnPropertyChanged(nameof(SelectedWinner));
        var index = row.PlayerId is null ? 2 : row.PlayerId == _lobby.Connection?.PlayerId ? 0 : 1;
        SelectedResult = ResultOptions[index];
    }

    // --- Captif, côté propriétaire -----------------------------------------------------------------------------

    /// <summary>Déclare le guerrier capturé à son capteur (61 confirmé et capteur choisi), ou retire la déclaration si le
    /// jet a changé.</summary>
    private async Task SyncCaptureAsync(WarriorOutcomeRow row)
    {
        if (row.ConnectedCapture is not { } state || _lobby.Captures is not { } captures) return;
        var existing = captures.FindByWarrior(row.Warrior.Id);
        if (!row.ShowCapturedChoice)
        {
            if (existing is not null) await _lobby.CancelCaptureAsync(row.Warrior.Id);
            return;
        }
        if (state.SelectedCaptor is not { } captor || existing?.Status == CaptureStatus.Manual) return;
        await _lobby.DeclareCaptureAsync(row.Warrior.Id, captor.PlayerId, row.Warrior.Name, row.ArchetypeName, Warband?.Name ?? string.Empty);
    }

    [RelayCommand]
    private Task PayRansom(WarriorOutcomeRow row) => AnswerRansomAsync(row, accepted: true);

    [RelayCommand]
    private Task RefuseRansom(WarriorOutcomeRow row) => AnswerRansomAsync(row, accepted: false);

    private async Task AnswerRansomAsync(WarriorOutcomeRow row, bool accepted)
    {
        if (_lobby.Captures?.FindByWarrior(row.Warrior.Id) is not { Status: CaptureStatus.Offered } capture) return;
        await _lobby.AnswerRansomAsync(capture.Id, accepted);
        row.CapturedChoiceError = null;
    }

    [RelayCommand]
    private async Task CaptureToTable(WarriorOutcomeRow row)
    {
        row.ConnectedCapture?.GoManualLocally();
        if (_lobby.Captures?.FindByWarrior(row.Warrior.Id) is { } capture) await _lobby.CaptureToTableAsync(capture.Id);
        row.CapturedChoiceError = null;
    }

    /// <summary>Étape Blessures : un captif connecté doit avoir son capteur, et la rançon doit être tranchée (ou passée à
    /// la table) avant d'aller plus loin.</summary>
    private bool ValidateConnectedCapture(WarriorOutcomeRow row)
    {
        if (!row.ShowConnectedCapture || row.ConnectedCapture is not { } state) return true;
        if (state.SelectedCaptor is null)
        {
            row.CapturedChoiceError = Loc["ConnectedCaptureChooseCaptor"];
            return false;
        }
        if (!state.IsPending) return true;
        row.CapturedChoiceError = Loc["ConnectedCapturePendingError"];
        return false;
    }

    // --- Captif, côté capteur ---------------------------------------------------------------------------------

    [RelayCommand]
    private async Task OfferRansom(ConnectedCaptiveRow row)
    {
        if (!int.TryParse(row.RansomInput, out var amount) || amount < 0)
        {
            row.Error = Loc["EndOfGameRollRequired"];
            return;
        }
        row.Error = null;
        await _lobby.OfferRansomAsync(row.Id, amount);
    }

    [RelayCommand]
    private Task CaptiveToTable(ConnectedCaptiveRow row) => _lobby.CaptureToTableAsync(row.Id);

    [RelayCommand]
    private void AutoRollConnectedSoldToSlavers(ConnectedCaptiveRow row) => AutoRollSoldToSlavers(row.RefusedFate);

    private bool ValidateConnectedCaptives()
    {
        var valid = true;
        foreach (var row in ConnectedCaptives)
        {
            if (row.CanOffer || row.IsOffered)
            {
                row.Error = Loc[row.CanOffer ? "ConnectedCaptiveOfferRequired" : "ConnectedCaptiveAwaitingAnswer"];
                valid = false;
            }
            else if (row.IsRefused)
            {
                valid &= CheckRoll(row.RefusedFate.SelectedFate is null, () => row.RefusedFate.FateError = Loc["EndOfGameChoiceRequired"]);
                if (row.RefusedFate.ShowGoldAmount)
                    valid &= CheckRoll(!row.RefusedFate.HasValidGoldAmount, () => row.RefusedFate.FateError = Loc["EndOfGameRollRequired"]);
            }
        }
        return valid;
    }

    /// <summary>Issues des captifs connectés pour l'enregistrement : rançon payée = or reçu, refusée = sort choisi ; à la
    /// table = saisi à la main dans la liste habituelle.</summary>
    private IEnumerable<(CaptiveOutcome Outcome, string? Sentence)> ConnectedCaptiveOutcomes()
    {
        foreach (var row in ConnectedCaptives)
        {
            if (row.IsAccepted && row.Amount is { } paid)
                yield return (new CaptiveOutcome(CaptiveFate.Ransomed, paid), string.Format(Loc["HistoryConnectedCaptiveRansomedSentence"], row.Title, row.OwnerName, paid));
            else if (row.IsRefused && row.RefusedFate.SelectedFate is { } fate)
            {
                var hasGold = int.TryParse(row.RefusedFate.GoldAmount, out var gold);
                switch (fate)
                {
                    case CapturedEnemyFate.SoldToSlavers when hasGold:
                        yield return (new CaptiveOutcome(CaptiveFate.SoldToSlavers, gold), string.Format(Loc["HistoryCapturedEnemySoldSentence"], gold));
                        break;
                    case CapturedEnemyFate.KilledForZombie:
                        yield return (new CaptiveOutcome(CaptiveFate.KilledForZombie), Loc["HistoryCapturedEnemyKilledSentence"]);
                        break;
                    case CapturedEnemyFate.SacrificedForXp:
                        yield return (new CaptiveOutcome(CaptiveFate.SacrificedForXp), null);
                        break;
                }
            }
        }
    }
}
