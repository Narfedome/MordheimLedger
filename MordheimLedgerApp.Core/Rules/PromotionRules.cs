namespace MordheimLedgerApp.Core.Rules;

/// <summary>Pure rules for the Henchman-to-Hero promotion (HenchmanAdvanceTable.IsPromotion, rolls
/// 10-12).</summary>
public static class PromotionRules
{
    /// <summary>Fixed rulebook cap on Heroes per warband - not derived from any per-archetype MaxCount
    /// (confirmed with the user: this is the game's own flat limit, unrelated to Library data).</summary>
    public const int MaxHeroes = 6;

    public static bool CanPromoteToHero(int currentHeroCount) => currentHeroCount < MaxHeroes;
}
