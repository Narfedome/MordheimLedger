using System.Windows.Input;

namespace MordheimLedgerApp.Components;

/// <summary>Sous-onglets Équipement/Compétences/XP partagés par WarriorNameSlot et HenchmanGroupDraft
/// (Features.Warbands.CreateEdit, tous deux dérivés de RecruitSlot) en mode Bande existante - factorisé
/// ici pour ne pas dupliquer le même bloc XAML dans les deux DataTemplate de WarbandEditDialog.xaml.
/// Item est volontairement typé object (pas RecruitSlot) pour ne pas faire dépendre ce composant
/// générique d'un type propre à une seule feature - les bindings internes ({Binding Item.
/// IsEquipmentSection} etc.) résolvent par réflexion comme WarriorRecruitListView/TabToggleButton.
/// AddEquipmentCommand etc. remontent jusqu'au WarbandEditDialogViewModel de la page hôte, qui seul les
/// porte réellement.</summary>
public partial class RecruitSlotTabsView : ContentView
{
    public static readonly BindableProperty ItemProperty =
        BindableProperty.Create(nameof(Item), typeof(object), typeof(RecruitSlotTabsView));

    public object? Item
    {
        get => GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public static readonly BindableProperty AddEquipmentCommandProperty =
        BindableProperty.Create(nameof(AddEquipmentCommand), typeof(ICommand), typeof(RecruitSlotTabsView));

    public ICommand? AddEquipmentCommand
    {
        get => (ICommand?)GetValue(AddEquipmentCommandProperty);
        set => SetValue(AddEquipmentCommandProperty, value);
    }

    public static readonly BindableProperty RemoveEquipmentCommandProperty =
        BindableProperty.Create(nameof(RemoveEquipmentCommand), typeof(ICommand), typeof(RecruitSlotTabsView));

    public ICommand? RemoveEquipmentCommand
    {
        get => (ICommand?)GetValue(RemoveEquipmentCommandProperty);
        set => SetValue(RemoveEquipmentCommandProperty, value);
    }

    public static readonly BindableProperty ShowEquipmentDetailCommandProperty =
        BindableProperty.Create(nameof(ShowEquipmentDetailCommand), typeof(ICommand), typeof(RecruitSlotTabsView));

    public ICommand? ShowEquipmentDetailCommand
    {
        get => (ICommand?)GetValue(ShowEquipmentDetailCommandProperty);
        set => SetValue(ShowEquipmentDetailCommandProperty, value);
    }

    public static readonly BindableProperty AddSkillCommandProperty =
        BindableProperty.Create(nameof(AddSkillCommand), typeof(ICommand), typeof(RecruitSlotTabsView));

    public ICommand? AddSkillCommand
    {
        get => (ICommand?)GetValue(AddSkillCommandProperty);
        set => SetValue(AddSkillCommandProperty, value);
    }

    public static readonly BindableProperty RemoveSkillCommandProperty =
        BindableProperty.Create(nameof(RemoveSkillCommand), typeof(ICommand), typeof(RecruitSlotTabsView));

    public ICommand? RemoveSkillCommand
    {
        get => (ICommand?)GetValue(RemoveSkillCommandProperty);
        set => SetValue(RemoveSkillCommandProperty, value);
    }

    public static readonly BindableProperty ShowSkillDetailCommandProperty =
        BindableProperty.Create(nameof(ShowSkillDetailCommand), typeof(ICommand), typeof(RecruitSlotTabsView));

    public ICommand? ShowSkillDetailCommand
    {
        get => (ICommand?)GetValue(ShowSkillDetailCommandProperty);
        set => SetValue(ShowSkillDetailCommandProperty, value);
    }

    public static readonly BindableProperty AddSpellCommandProperty =
        BindableProperty.Create(nameof(AddSpellCommand), typeof(ICommand), typeof(RecruitSlotTabsView));

    public ICommand? AddSpellCommand
    {
        get => (ICommand?)GetValue(AddSpellCommandProperty);
        set => SetValue(AddSpellCommandProperty, value);
    }

    public static readonly BindableProperty RemoveSpellCommandProperty =
        BindableProperty.Create(nameof(RemoveSpellCommand), typeof(ICommand), typeof(RecruitSlotTabsView));

    public ICommand? RemoveSpellCommand
    {
        get => (ICommand?)GetValue(RemoveSpellCommandProperty);
        set => SetValue(RemoveSpellCommandProperty, value);
    }

    public static readonly BindableProperty ShowSpellDetailCommandProperty =
        BindableProperty.Create(nameof(ShowSpellDetailCommand), typeof(ICommand), typeof(RecruitSlotTabsView));

    public ICommand? ShowSpellDetailCommand
    {
        get => (ICommand?)GetValue(ShowSpellDetailCommandProperty);
        set => SetValue(ShowSpellDetailCommandProperty, value);
    }

    /// <summary>Hors mode Bande existante (Item.IsExistingWarband) : ouvre SpellRollDialog (contexte
    /// école de magie + saisie du jet 1D6, voir WarbandEditDialogViewModel.ShowSpellRollDialog) -
    /// remplace AddSpellCommand, où le sort de départ est tiré au hasard plutôt que choisi librement.</summary>
    public static readonly BindableProperty ShowSpellRollDialogCommandProperty =
        BindableProperty.Create(nameof(ShowSpellRollDialogCommand), typeof(ICommand), typeof(RecruitSlotTabsView));

    public ICommand? ShowSpellRollDialogCommand
    {
        get => (ICommand?)GetValue(ShowSpellRollDialogCommandProperty);
        set => SetValue(ShowSpellRollDialogCommandProperty, value);
    }

    /// <summary>Onglet Mutations (Item.CanBuyMutations, voir RecruitSlot.Mutations) - pas câblé par tous
    /// les appelants : sans AddMutationCommand (wizard Fin de Partie, pas encore géré), l'onglet reste
    /// masqué plutôt que d'afficher un "+" inopérant (voir HasMutationCommands).</summary>
    public static readonly BindableProperty AddMutationCommandProperty =
        BindableProperty.Create(nameof(AddMutationCommand), typeof(ICommand), typeof(RecruitSlotTabsView),
            propertyChanged: (b, _, _) => ((RecruitSlotTabsView)b).OnPropertyChanged(nameof(HasMutationCommands)));

    public ICommand? AddMutationCommand
    {
        get => (ICommand?)GetValue(AddMutationCommandProperty);
        set => SetValue(AddMutationCommandProperty, value);
    }

    public bool HasMutationCommands => AddMutationCommand is not null;

    public static readonly BindableProperty RemoveMutationCommandProperty =
        BindableProperty.Create(nameof(RemoveMutationCommand), typeof(ICommand), typeof(RecruitSlotTabsView));

    public ICommand? RemoveMutationCommand
    {
        get => (ICommand?)GetValue(RemoveMutationCommandProperty);
        set => SetValue(RemoveMutationCommandProperty, value);
    }

    public static readonly BindableProperty ShowMutationDetailCommandProperty =
        BindableProperty.Create(nameof(ShowMutationDetailCommand), typeof(ICommand), typeof(RecruitSlotTabsView));

    public ICommand? ShowMutationDetailCommand
    {
        get => (ICommand?)GetValue(ShowMutationDetailCommandProperty);
        set => SetValue(ShowMutationDetailCommandProperty, value);
    }

    public RecruitSlotTabsView()
    {
        InitializeComponent();
    }
}
