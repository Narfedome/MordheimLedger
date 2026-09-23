using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Récap en lecture seule d'un guerrier DÉJÀ ACTIF (jamais un WarriorArchetype candidat au
/// recrutement, qui reste couvert par ShowWarriorArchetypeDetailDialogAsync/
/// EndOfGamePageViewModel.Recruitment.cs's ShowWarriorDetail - voir la distinction dans le plan) -
/// ouvert en tapant la chip nom d'un guerrier sur plusieurs étapes du wizard Fin de Partie (Renvoyer,
/// Vétérans, Hors de combat, Blessure, Progression, Expérience - EndOfGamePageViewModel.
/// ShowWarriorRecapCommand). 2026-09-23, retour utilisateur - "plutôt que d'avoir un label ou une chip
/// non interagissable, ça serait top de pouvoir cliquer sur une chip qui nous révèle le détail du
/// personnage". Structure calquée sur HiredSwordDetailDialogViewModel (le récap "profil de combat" le
/// plus proche déjà existant) - Warrior porte déjà Equipment/Skills/Spells/Mutations, seul SpecialRules
/// doit être fourni par l'appelant (jamais résolu par Warrior lui-même, voir sa propre doc). Volontairement
/// plus court que la fiche roster complète (WarriorCardTemplate) - pas de Blessures/Haine/case Malade/
/// bouton Édition, hors de propos pour un simple "qui est ce guerrier" depuis le wizard.</summary>
public partial class WarriorRecapDialogViewModel : ReadOnlyDialogViewModel
{
    public Warrior Warrior { get; }
    public string ArchetypeName { get; }
    public IReadOnlyList<SpecialRuleChip> SpecialRules { get; }

    /// <summary>Le bloc Équipement/Compétences est un FlexLayout manuel (icône par catégorie, voir
    /// WarriorRecapDialog.xaml), pas un ChipListView (qui s'auto-masque déjà si vide) - a donc besoin de
    /// son propre IsVisible bool, même idiome que WarriorRow.HasEquipment/HasSkills (binder directement
    /// sur Warrior.Equipment.Count ne fonctionnerait pas, MAUI ne convertit pas int en bool).</summary>
    public bool HasEquipment => Warrior.Equipment.Count > 0;
    public bool HasSkills => Warrior.Skills.Count > 0;

    private readonly IDetailDialogService _detailDialogs;

    public WarriorRecapDialogViewModel(Warrior warrior, string archetypeName, IReadOnlyList<SpecialRuleChip> specialRules, IDetailDialogService detailDialogs)
    {
        Warrior = warrior;
        Title = warrior.Name;
        ArchetypeName = archetypeName;
        SpecialRules = specialRules;
        _detailDialogs = detailDialogs;
    }

    [RelayCommand]
    private Task ShowEquipmentDetail(WarriorEquipment equipment) =>
        _detailDialogs.ShowEquipmentDetailDialogAsync(equipment.Item, equipment.MaterialRule, equipment.FoundValueOverride, equipment.BlessingRule);

    [RelayCommand]
    private Task ShowSkillDetail(WarriorSkill skill) => _detailDialogs.ShowSkillDetailDialogAsync(skill.Item);

    [RelayCommand]
    private Task ShowSpecialRuleDetail(SpecialRuleChip chip) => _detailDialogs.ShowSpecialRuleDetailDialogAsync(chip.Item);

    [RelayCommand]
    private Task ShowSpellDetail(WarriorSpell spell) => _detailDialogs.ShowSpellDetailDialogAsync(spell.Item);

    [RelayCommand]
    private Task ShowMutationDetail(WarriorMutation mutation) => _detailDialogs.ShowMutationDetailDialogAsync(mutation.Item);
}
