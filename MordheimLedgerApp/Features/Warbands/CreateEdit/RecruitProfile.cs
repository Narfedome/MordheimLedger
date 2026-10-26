using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Models;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit;

/// <summary>Profil saisi à la main pour une recrue en mode « Bande existante » (onglet Profil de
/// RecruitSlotTabsView, 2026-10-26) : on recopie la fiche papier d'une bande déjà jouée, profil déjà
/// progressé compris. Part du profil de référence (archétype pour une recrue neuve, guerrier en base sinon)
/// et n'est écrit sur le Warrior que s'il en diffère (IsModified) - dans ce cas les malus des blessures
/// saisies ne sont PAS réappliqués par-dessus (décision utilisateur : le profil recopié les contient déjà).
/// Mêmes noms de propriété que Warrior pour que StatRowView (IsEditable) s'y lie tel quel.</summary>
public partial class RecruitProfile : ObservableObject
{
    private readonly string _baseMovement;
    private readonly int[] _base;

    public RecruitProfile(string movement, int weaponSkill, int ballisticSkill, int strength, int toughness,
        int wounds, int initiative, int attacks, int leadership)
    {
        _baseMovement = movement;
        _base = new[] { weaponSkill, ballisticSkill, strength, toughness, wounds, initiative, attacks, leadership };
        ResetToBase();
    }

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsModified))] private string movementInput = string.Empty;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsModified))] private int weaponSkill;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsModified))] private int ballisticSkill;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsModified))] private int strength;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsModified))] private int toughness;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsModified))] private int wounds;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsModified))] private int initiative;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsModified))] private int attacks;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsModified))] private int leadership;

    private int[] Current => new[] { WeaponSkill, BallisticSkill, Strength, Toughness, Wounds, Initiative, Attacks, Leadership };

    public bool IsModified => MovementInput.Trim() != _baseMovement || !Current.SequenceEqual(_base);

    /// <summary>Rappel du profil de référence, affiché sous la grille une fois modifiée (StatRowView en
    /// édition n'a pas de surlignage par case).</summary>
    public string BaseDisplay => string.Join("  ", new[] { _baseMovement }.Concat(_base.Select(v => v.ToString())));

    /// <summary>Profil saisi, même format que BaseDisplay - pour le récapitulatif de l'assistant.</summary>
    public string CurrentDisplay => string.Join(" ", new[] { MovementInput.Trim() }.Concat(Current.Select(v => v.ToString())));

    [RelayCommand]
    private void ResetToBase()
    {
        MovementInput = _baseMovement;
        (WeaponSkill, BallisticSkill, Strength, Toughness) = (_base[0], _base[1], _base[2], _base[3]);
        (Wounds, Initiative, Attacks, Leadership) = (_base[4], _base[5], _base[6], _base[7]);
    }

    /// <summary>Écrit le profil saisi sur le guerrier - un Mouvement non numérique (ex. « 2D6 » des Squigs)
    /// passe par MovementOverride, comme dans WarriorEditDialogViewModel.</summary>
    public void ApplyTo(Warrior warrior)
    {
        var movement = MovementInput.Trim();
        if (int.TryParse(movement, out var value)) { warrior.Movement = value; warrior.MovementOverride = null; }
        else if (movement.Length > 0) warrior.MovementOverride = movement;
        warrior.WeaponSkill = WeaponSkill;
        warrior.BallisticSkill = BallisticSkill;
        warrior.Strength = Strength;
        warrior.Toughness = Toughness;
        warrior.Wounds = Wounds;
        warrior.Initiative = Initiative;
        warrior.Attacks = Attacks;
        warrior.Leadership = Leadership;
    }
}
