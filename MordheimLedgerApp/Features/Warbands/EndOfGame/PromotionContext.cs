using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Ce que toutes les promotions d'une même Fin de partie partagent (« The lad's got talent ») :
/// - les listes de compétences proposées au promu = celles des Héros de la bande, liste spéciale comprise
///   (FAQ : elle compte alors pour l'une des deux) - plus les 6 listes quelle que soit la bande ;
/// - le plafond de Héros, qui tient compte des promotions déjà obtenues plus tôt dans la même Fin de partie
///   (« if you already have the maximum number of Heroes, roll again ») ;
/// - la liste des jets de Promotion acceptés, pour compter le promu à l'Exploration et aux objets rares
///   (il est Héros dès l'étape Expérience, qui précède les revenus).
/// Décisions de l'utilisateur du 2026-11-11.</summary>
public sealed class PromotionContext
{
    private readonly List<AdvanceRollEntry> _promotions = new();

    public PromotionContext(int startingHeroCount, IEnumerable<SkillCategory> heroSkillCategories)
    {
        StartingHeroCount = startingHeroCount;
        var categories = heroSkillCategories.Distinct().OrderBy(c => c).ToList();
        HeroSkillCategories = categories.Count > 0 ? categories : Enum.GetValues<SkillCategory>().ToList();
    }

    public int StartingHeroCount { get; }

    public IReadOnlyList<SkillCategory> HeroSkillCategories { get; }

    /// <summary>Jets de Promotion acceptés, dans l'ordre où ils ont été obtenus.</summary>
    public IReadOnlyList<AdvanceRollEntry> Promotions => _promotions;

    /// <summary>Raised whenever a Promotion is accepted or withdrawn.</summary>
    public event Action? Changed;

    /// <summary>Une promotion déjà acceptée le reste ; une nouvelle n'est possible que s'il reste une place
    /// en comptant les promotions déjà obtenues dans cette Fin de partie.</summary>
    public bool CanPromote(AdvanceRollEntry entry) =>
        _promotions.Contains(entry) || StartingHeroCount + _promotions.Count < PromotionRules.MaxHeroes;

    internal void SetPromotion(AdvanceRollEntry entry, bool accepted)
    {
        var changed = accepted ? !_promotions.Contains(entry) : _promotions.Contains(entry);
        if (!changed) return;
        if (accepted) _promotions.Add(entry);
        else _promotions.Remove(entry);
        Changed?.Invoke();
    }
}
