using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Un Franc-Tireur déjà activement engagé dans la bande (Warrior.IsHiredSword), à l'étape
/// "Francs-Tireurs" du wizard Fin de Partie - voir EndOfGamePageViewModel.HiredSwordUpkeepEntries.
/// Le livre : la solde se règle après CHAQUE bataille (y compris la toute première) ou il quitte la
/// bande (perd toute son XP, même s'il est réengagé plus tard - voir Warrior.HiredSwordId). Choix
/// Payer/Renvoyer via un Picker à deux options (même idiome que CapturedEnemyEntry.FateLabels/
/// SelectedFateLabel) plutôt qu'un bool? brut, pour rester cohérent avec le reste du wizard.
/// IsPrepaidFree (posé par "Une Faveur Rendue", voir Warrior.HiredSwordUpkeepPrepaid) affiche la solde
/// déjà réglée sans proposer de choix du tout (voir HasChoice).</summary>
public partial class HiredSwordUpkeepEntry : ObservableObject
{
    public Warrior Warrior { get; }
    public HiredSword HiredSword { get; }

    public string DisplayName => Warrior.Name;
    public int UpkeepCost => HiredSword.Upkeep;
    public bool IsPrepaidFree => Warrior.HiredSwordUpkeepPrepaid;
    public bool HasChoice => !IsPrepaidFree;

    public List<string> ChoiceLabels { get; }
    private readonly string _payLabel;
    private readonly string _noneLabel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WillPay))]
    private string? selectedChoiceLabel;

    partial void OnSelectedChoiceLabelChanged(string? value)
    {
        if (value is not null) ChoiceError = null;
    }

    /// <summary>Résolu depuis SelectedChoiceLabel - null tant que "Aucun" (le choix par défaut, voir le
    /// constructeur) ou rien du tout n'est sélectionné. Consommé par EndOfGamePageViewModel.Apply.
    /// ApplyHiredSwordUpkeepAsync.</summary>
    public bool? WillPay => SelectedChoiceLabel is null || SelectedChoiceLabel == _noneLabel ? null : SelectedChoiceLabel == _payLabel;

    [ObservableProperty]
    private string? choiceError;

    public HiredSwordUpkeepEntry(Warrior warrior, HiredSword hiredSword, string noneLabel, string payLabel, string dismissLabel)
    {
        Warrior = warrior;
        HiredSword = hiredSword;
        _payLabel = payLabel;
        _noneLabel = noneLabel;
        // "Aucun" en premier, présélectionné (retour utilisateur 2026-09-22) - même raisonnement que
        // CapturedEnemyEntry.FateLabels, voir sa doc.
        ChoiceLabels = new List<string> { noneLabel, payLabel, dismissLabel };
        SelectedChoiceLabel = noneLabel;
    }
}
