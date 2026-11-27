using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Un guerrier qui peut utiliser le Grimoire (étape Grimoire, 1.6).</summary>
public partial class TomeUserOption : ObservableObject
{
    public TomeUserOption(Warrior warrior, string roleName, IReadOnlyList<MagicSchool> ownSchools)
    {
        Warrior = warrior;
        RoleName = roleName;
        OwnSchools = ownSchools;
    }

    public Warrior Warrior { get; }
    public string Name => Warrior.Name;
    public string RoleName { get; }
    public IReadOnlyList<MagicSchool> OwnSchools { get; }

    [ObservableProperty]
    private bool isSelected;
}

/// <summary>Une liste où tirer le sort (sa propre école ou la Magie Mineure).</summary>
public partial class TomeListOption : ObservableObject
{
    public TomeListOption(MagicSchool school, bool isOwn)
    {
        School = school;
        Label = isOwn ? string.Format(LocalizationService.Instance["TomeOwnListFormat"], school.Name) : school.Name;
    }

    public MagicSchool School { get; }
    public string Label { get; }

    [ObservableProperty]
    private bool isSelected;
}

/// <summary>Un Grimoire acquis pendant cette Fin de partie (Core.Rules.TomeOfMagicRules) : qui l'utilise, dans quelle
/// liste il tire, et le sort obtenu - doublon accepté (difficulté -1). Saisie libre et informative : sans jet, aucun
/// sort n'est appris, rien n'est bloqué.</summary>
public partial class TomeUseEntry : ObservableObject
{
    private readonly IReadOnlyList<Spell> _spells;
    private readonly MagicSchool? _lesserMagic;

    public TomeUseEntry(int index, TomeUseKind kind, IReadOnlyList<TomeUserOption> users, MagicSchool? lesserMagic, IReadOnlyList<Spell> spells)
    {
        Index = index;
        Kind = kind;
        Users = users;
        _lesserMagic = lesserMagic;
        _spells = spells;
        if (users.Count > 0) SelectUser(users[0]);
    }

    public int Index { get; }
    public TomeUseKind Kind { get; }
    public IReadOnlyList<TomeUserOption> Users { get; }
    public ObservableCollection<TomeListOption> Lists { get; } = new();

    public bool HasUsers => Users.Count > 0;
    public bool IsNobody => Kind == TomeUseKind.Nobody;
    public bool IsArcaneLore => Kind == TomeUseKind.LearnLesserMagic;
    public bool ShowLists => Kind == TomeUseKind.ExtraSpell && Lists.Count > 1;

    public string UsersLabel => LocalizationService.Instance[IsArcaneLore ? "TomeUsersArcaneLore" : "TomeUsersWizard"];

    public TomeUserOption? SelectedUser => Users.FirstOrDefault(u => u.IsSelected);
    public TomeListOption? SelectedList => Lists.FirstOrDefault(l => l.IsSelected);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResolvedSpell), nameof(HasResolvedSpell), nameof(ResultText), nameof(DuplicateText), nameof(IsDuplicate))]
    private string roll = string.Empty;

    public Spell? ResolvedSpell => SelectedList is { } list && int.TryParse(Roll, out var value)
        ? _spells.FirstOrDefault(s => s.MagicSchoolId == list.School.Id && s.RollValue == value)
        : null;

    public bool HasResolvedSpell => ResolvedSpell is not null;

    public string ResultText => ResolvedSpell is { } spell ? spell.RollDisplay : string.Empty;

    public bool IsDuplicate => ResolvedSpell is { } spell && SelectedUser is { } user
        && SpellRules.Learn(user.Warrior.Spells.Select(s => s.Item.Id), spell.Id) == SpellLearningOutcome.DifficultyReduced;

    public string DuplicateText => IsDuplicate && ResolvedSpell is { } spell
        ? spell.Difficulty is { } difficulty
            ? string.Format(LocalizationService.Instance["SpellRollDuplicateFormat"], spell.Name, difficulty, SpellRules.EffectiveDifficulty(difficulty, 1))
            : string.Format(LocalizationService.Instance["SpellRollDuplicateNoDifficultyFormat"], spell.Name)
        : string.Empty;

    public string ListName => SelectedList?.School.Name ?? string.Empty;

    public void SelectUser(TomeUserOption user)
    {
        foreach (var u in Users) u.IsSelected = ReferenceEquals(u, user);
        Lists.Clear();
        foreach (var school in user.OwnSchools)
            Lists.Add(new TomeListOption(school, isOwn: true));
        if (_lesserMagic is not null && Lists.All(l => l.School.Id != _lesserMagic.Id))
            Lists.Add(new TomeListOption(_lesserMagic, isOwn: false));
        if (Lists.Count > 0) Lists[0].IsSelected = true;
        NotifyChoiceChanged();
    }

    public void SelectList(TomeListOption list)
    {
        foreach (var l in Lists) l.IsSelected = ReferenceEquals(l, list);
        NotifyChoiceChanged();
    }

    private void NotifyChoiceChanged()
    {
        OnPropertyChanged(nameof(SelectedUser));
        OnPropertyChanged(nameof(SelectedList));
        OnPropertyChanged(nameof(ShowLists));
        OnPropertyChanged(nameof(ListName));
        OnPropertyChanged(nameof(ResolvedSpell));
        OnPropertyChanged(nameof(HasResolvedSpell));
        OnPropertyChanged(nameof(ResultText));
        OnPropertyChanged(nameof(IsDuplicate));
        OnPropertyChanged(nameof(DuplicateText));
    }
}
