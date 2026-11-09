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

    /// <summary>Un dé ajusté de delta (Chercheur de Ressources : ±1), toujours entre 1 et 6.</summary>
    public static int AdjustDie(int die, int delta) => Math.Clamp(die + delta, 1, 6);

    /// <summary>Plage d'un jet de dés « nDm » (Marchandage : "2D6" -> 2..12) pour valider un résultat saisi à la
    /// main ; null si la valeur n'est pas une formule de dés simple.</summary>
    public static (int Min, int Max)? DiceRange(string? formula)
    {
        var text = formula?.Trim().ToUpperInvariant();
        var d = text?.IndexOf('D') ?? -1;
        if (text is null || d < 0) return null;
        var count = d == 0 ? 1 : int.TryParse(text[..d], out var c) ? c : 0;
        if (count <= 0 || !int.TryParse(text[(d + 1)..], out var sides) || sides <= 0) return null;
        return (count, count * sides);
    }

    /// <summary>Prix après réduction, jamais sous le plancher (Marchandage : 1 CO) - ni au-dessus du prix
    /// d'origine si celui-ci était déjà sous le plancher.</summary>
    public static int ApplyDiscount(int price, int discount, int? minimumPrice)
    {
        var floor = minimumPrice ?? 0;
        if (price <= floor) return price;
        return Math.Max(floor, price - Math.Max(0, discount));
    }
}
