using System.Globalization;

namespace MordheimLedgerApp.Core.Rules;

/// <summary>Lecture des valeurs d'effet (SkillEffect.Value) et petits calculs qui en découlent.</summary>
public static class EffectValues
{
    /// <summary>"+2" / "-1" / "2" -> entier signé ; 0 si vide ou illisible.</summary>
    public static int ParseAmount(string? value) =>
        int.TryParse(value?.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var amount) ? amount : 0;

    /// <summary>Comme ParseAmount, mais 1 par défaut (Reroll/AdjustDie/ExtraDie sans valeur = un dé).</summary>
    public static int ParseCount(string? value) => string.IsNullOrWhiteSpace(value) ? 1 : ParseAmount(value);

    public static string FormatModifier(int amount) => amount >= 0 ? $"+{amount}" : amount.ToString(CultureInfo.InvariantCulture);

    /// <summary>Prix après réduction, jamais sous le plancher (Marchandage : 1 CO) - ni au-dessus du prix
    /// d'origine si celui-ci était déjà sous le plancher.</summary>
    public static int ApplyDiscount(int price, int discount, int? minimumPrice)
    {
        var floor = minimumPrice ?? 0;
        if (price <= floor) return price;
        return Math.Max(floor, price - Math.Max(0, discount));
    }
}
