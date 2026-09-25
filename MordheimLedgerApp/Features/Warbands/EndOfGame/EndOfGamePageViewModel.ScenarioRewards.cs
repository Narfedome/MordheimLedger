using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Warbands.CreateEdit;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Étape "Récompenses du scénario" (StepKind.ScenarioRewards) - juste après Résultat, avant les
/// blessures (retour utilisateur 2026-09-25 : "un écran somme toute plutôt libre pour la fin de partie
/// pour gérer le cas du scénario"). Pas une étape du livre : chaque scénario décide de ses propres
/// récompenses, saisies ici librement. Toujours présente, Suivant sans rien saisir = aucune récompense.
///
/// **Pas d'XP ici** : une étape "XP du scénario" dédiée a été essayée puis retirée le même jour (doublon
/// de l'étape Expérience, retour utilisateur) - toute l'XP de la partie, scénario et victoire compris, se
/// saisit à l'étape Expérience.
///
/// - **Or** : ScenarioGold, rejoint RareItemBaselineTreasury (le solde unique du wizard).
/// - **Pierres magiques** : ScenarioWyrdstoneShards, rejoint FoundThisGameWyrdstoneShards (vendables à
///   l'étape Vente de pierre magique).
/// - **Objets** : ScenarioRewardItems, rejoignent la réserve comme une trouvaille d'Exploration (voir
///   PendingExplorationStashItems) - donc visibles au Recrutement, vendables à la Vente et
///   réallouables à un Héros à l'étape Réallouer.
///
/// Tout est persisté à Terminer par ApplyScenarioRewardsAsync.</summary>
public partial class EndOfGamePageViewModel
{
    /// <summary>Texte plutôt qu'int pour que le champ parte vide (même motif qu'ExperienceGainedText).</summary>
    [ObservableProperty]
    private string scenarioGoldText = string.Empty;

    partial void OnScenarioGoldTextChanged(string value) => NotifyTreasuryChanged();

    public int ScenarioGold => int.TryParse(ScenarioGoldText, out var gold) ? gold : 0;

    [ObservableProperty]
    private string scenarioWyrdstoneText = string.Empty;

    partial void OnScenarioWyrdstoneTextChanged(string value) => NotifyWyrdstoneFoundThisGameChanged();

    public int ScenarioWyrdstoneShards => int.TryParse(ScenarioWyrdstoneText, out var shards) ? shards : 0;

    public ObservableCollection<EquipmentPick> ScenarioRewardItems { get; } = new();

    /// <summary>Tout le catalogue (unrestricted - "objet quel qu'il soit"), sans coût puisque gagné et non
    /// acheté. Choix du matériau (Gromril/Ithilmar/Ornée) pour les armes de corps à corps via le même
    /// dialog que le wizard de bande, sans la bénédiction : la réserve ne la conserve pas (voir
    /// ReserveLine).</summary>
    [RelayCommand]
    private async Task AddScenarioItem()
    {
        var items = await _equipmentPicker.PickEquipmentAsync(_recruitableWarbandArchetype.Id, allowCreate: false, unrestricted: true);
        if (items.Count == 0) return;

        var choices = await MaterialPickerDialogViewModel.BuildChoicesAsync(_libraryService, items, alreadyHasFreeDagger: true, allowBlessing: false);
        bool? confirmed = null;
        if (choices.Count > 0)
            confirmed = await ShowDialogAsync(new MaterialPickerDialog(new MaterialPickerDialogViewModel(choices, _detailDialogs)));
        var resolved = MaterialPickerDialogViewModel.ResolveChoices(items, choices, confirmed);

        for (var i = 0; i < items.Count; i++)
            ScenarioRewardItems.Add(new EquipmentPick(items[i], resolved[i].Material));
    }

    [RelayCommand]
    private void RemoveScenarioItem(EquipmentPick pick) => ScenarioRewardItems.Remove(pick);

    [RelayCommand]
    private Task ShowScenarioItemDetail(EquipmentPick pick) => _detailDialogs.ShowEquipmentDetailDialogAsync(pick.Item, pick.MaterialRule);

    public bool HasScenarioRewardsRecap => ScenarioGold != 0 || ScenarioWyrdstoneShards > 0 || ScenarioRewardItems.Count > 0;

    public IEnumerable<string> ScenarioRewardsRecapLines
    {
        get
        {
            if (ScenarioGold != 0) yield return string.Format(Loc["EndOfGameScenarioGoldRecapFormat"], ScenarioGold);
            if (ScenarioWyrdstoneShards > 0) yield return string.Format(Loc["EndOfGameScenarioWyrdstoneRecapFormat"], ScenarioWyrdstoneShards);
            foreach (var pick in ScenarioRewardItems) yield return pick.Name;
        }
    }

    /// <summary>Doit passer AVANT ApplyWyrdstoneSaleAsync (qui retranche les éclats vendus) et
    /// ApplyEquipmentTradingAsync (qui retrouve en base les objets vendus parmi les trouvailles de cette
    /// partie, voir SellableEquipmentCandidate.IsFromExploration) - appelé en tête de Finish.</summary>
    private async Task ApplyScenarioRewardsAsync(List<string> sentences)
    {
        if (Warband is null) return;

        var changed = false;
        if (ScenarioGold != 0)
        {
            Warband.Treasury += ScenarioGold;
            sentences.Add(string.Format(Loc["HistoryScenarioGoldSentence"], ScenarioGold));
            changed = true;
        }
        if (ScenarioWyrdstoneShards > 0)
        {
            Warband.WyrdstoneShards += ScenarioWyrdstoneShards;
            sentences.Add(string.Format(Loc["HistoryScenarioWyrdstoneSentence"], ScenarioWyrdstoneShards));
            changed = true;
        }
        if (changed) await _warbandService.SaveWarbandAsync(Warband);

        foreach (var pick in ScenarioRewardItems)
        {
            await _warbandService.AddWarbandEquipmentAsync(Warband.Id, pick.Item, materialRule: pick.MaterialRule);
            sentences.Add(string.Format(Loc["HistoryScenarioItemSentence"], pick.Name));
        }
    }
}
