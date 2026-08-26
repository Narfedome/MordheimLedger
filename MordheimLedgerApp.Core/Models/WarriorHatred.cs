namespace MordheimLedgerApp.Core.Models;

/// <summary>A "Rancune"/Bitter Enmity result (Serious Injury roll 56, see Core.Rules.
/// SeriousInjuryTable.IsBitterEnmity) - the target's scope is picked via a further 1D6 roll (see
/// Core.Rules.HatredTargetTable). Only "all warbands of that type" (roll 6) references a real catalog
/// entry (TargetWarbandArchetypeId) - the app doesn't track opposing warbands/warriors as structured
/// data, so every other scope (a specific individual/leader, rolls 1-4; a specific warband, roll 5) is
/// just a free-text name the player types in (TargetFreeText) - decision explicit with the user: "ce
/// n'est pas l'archétype qui est haï mais cette bande en particulier" for roll 5, same reasoning for
/// rolls 1-4. Exactly one of the two Target* fields is set.</summary>
public class WarriorHatred
{
    public int Id { get; set; }
    public int WarriorId { get; set; }

    public int? TargetWarbandArchetypeId { get; set; }
    public string? TargetFreeText { get; set; }

    /// <summary>Resolved display name (e.g. "Von Kessler's Regiment") - NOT prefixed with "Haine : ",
    /// same split as SpecialRuleChip (Core stays localization-free, the "Haine : {0}" formatting happens
    /// in the App layer, see WarbandDetailViewModel). Resolved by WarbandService.GetWarriorsAsync since
    /// TargetWarbandArchetypeId needs a translation lookup - there's no single Item to pass through like
    /// WarriorInjury.Name, so this is a stored value rather than a computed passthrough.</summary>
    public string Name { get; set; } = string.Empty;
}
