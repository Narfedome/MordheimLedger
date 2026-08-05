namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>
/// A mutation a Mutant/Possessed-type warrior can buy at recruitment (e.g. "Bras supplémentaire") - a
/// flat global catalog, not restricted per warband, since the rulebook's list (p.76) is shared verbatim
/// across every Chaos-adjacent warband (Possédés, Hommes-Bêtes, Kermesse du Chaos...). The "1st at list
/// price, each next one double" rule and "only at recruitment for Mutants/Possessed" restriction stay
/// descriptive (Description), not enforced - same "no rules engine V1" boundary as the rest of the
/// Library. Which WarriorArchetypes may buy mutations at all is gated by WarriorArchetype.
/// CanBuyMutations, not by anything here.
/// </summary>
public class Mutation
{
    public int Id { get; set; }

    /// <summary>Resolved display text in the requested language - see LibraryService's
    /// ResolveTranslationsAsync.</summary>
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Cost { get; set; }

    /// <summary>Translation slot backing Name/Description - persistence-only, not for display.</summary>
    public string? NameKey { get; set; }
    public string? DescriptionKey { get; set; }

    public ContentSource Source { get; set; }

    /// <summary>Empty = no art yet, tile falls back to a glyph (see LibraryItemImageView).</summary>
    public string ImagePath { get; set; } = string.Empty;
}
