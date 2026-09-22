namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>How a Dramatis Persona is paid for - unlike a generic HiredSword (always a flat HireCost +
/// Upkeep in gold), the named special characters use genuinely different economics per the rulebook
/// text: a normal gold hire+upkeep (Johann, Veskit, Marianna), a free but narratively-conditional
/// recruitment (Bertha's audience roll - see Description), payment in wyrdstone shards instead of gold
/// (Nicodemus, who leaves permanently if unpaid), or a single shared fee that recruits two Warriors at
/// once (Ulli &amp; Marquand). HireCost/Upkeep stay null for None/Wyrdstone (the real numbers, if any,
/// live in free-text Description instead - see the type's own doc).</summary>
public enum DramatisPersonaHireFeeKind
{
    Gold,
    None,
    Wyrdstone,
    Pair
}

/// <summary>
/// A named "Dramatis Persona"/special character from the rulebook (e.g. Aenur, Bertha Bestraufrung) -
/// catalogued the same way as HiredSword (full stat line, SpecialRules, StartingEquipmentIds, optional
/// MagicSchoolId, RestrictedToWarbandArchetypeIds), PLUS a fixed Skills list (a genuine difference from
/// HiredSword.AllowedSkillCategories - a Dramatis Persona already KNOWS its listed skills, it never picks
/// from a category on Advance). Per the standing "no rules engine V1" convention and the same precedent
/// as HiredSword's Dwarf Troll Slayer ("Deathwish"/"Hard to Kill" - unique-to-one-entry rules still
/// become real SpecialRule catalog rows as long as each is a single, clean, nameable effect), only the
/// genuinely elaborate multi-branch systems (Bertha's audience table, Marianna's end-of-game ambush
/// table, Nicodemus's wyrdstone-payment consequence, Ulli &amp; Marquand's mid-battle bribe mechanic...)
/// stay free text in Description - decided case by case per character (user request 2026-08-31), not a
/// blanket "keep it all in prose" rule.
///
/// Catalog-only for now (2026-08-31): browsable/editable in the Codex like any other Library type, but
/// not yet wired into recruitment/the End of Game wizard's "Personnage spécial" search (still free-text
/// CharacterName there - see RareItemSearchEntry) - hire-fee timing/payment flow is still being worked
/// out by the user and deliberately deferred to a later pass.
/// </summary>
public class DramatisPersona
{
    public int Id { get; set; }

    /// <summary>Resolved display text in the requested language - see LibraryService's
    /// ResolveTranslationsAsync/SetTranslationAsync.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Full lore + every rule unique to this character (audience/bribe tables, cursed items,
    /// starting gear...) - see the type's own doc for why so much stays free text here rather than
    /// modeled fields.</summary>
    public string? Description { get; set; }

    /// <summary>Only meaningful for Ulli &amp; Marquand (2026-09-01) - the shared "duo" lore text (how the
    /// pair met, their joint reputation), distinct from Description above (each half's OWN personal
    /// biography). Set on Marquand only (the "primary" half - see IsHiddenFromSearchPicker), shown by
    /// DramatisPersonaPairDetailDialog rather than the plain single-profile dialog, which still shows
    /// Description (this persona's own bio) regardless of pairing. Null for every non-paired character.</summary>
    public string? PairDescription { get; set; }

    /// <summary>Translation slot backing Name/Description - persistence-only, not for display.</summary>
    public string? NameKey { get; set; }
    public string? DescriptionKey { get; set; }
    public string? PairDescriptionKey { get; set; }

    public ContentSource Source { get; set; }

    /// <summary>Empty = no art yet, tile falls back to a glyph (see LibraryItemImageView).</summary>
    public string ImagePath { get; set; } = string.Empty;

    // Profile - field names match StatRowView's weak-typing contract exactly.
    public int Movement { get; set; }
    public int WeaponSkill { get; set; }
    public int BallisticSkill { get; set; }
    public int Strength { get; set; }
    public int Toughness { get; set; }
    public int Wounds { get; set; }
    public int Initiative { get; set; }
    public int Attacks { get; set; }
    public int Leadership { get; set; }

    public DramatisPersonaHireFeeKind FeeKind { get; set; }

    /// <summary>Null when FeeKind is None/Wyrdstone (no flat gold amount) - "as a pair" (FeeKind.Pair)
    /// still uses this for the single shared fee (e.g. Ulli &amp; Marquand: 30).</summary>
    public int? HireCost { get; set; }

    /// <summary>Null = no fixed ongoing gold upkeep (also true for a character whose upkeep "varies" per
    /// the book - that nuance stays in Description rather than forcing a number here).</summary>
    public int? Upkeep { get; set; }

    /// <summary>Only meaningful when FeeKind is Gold - an EquipmentItem the warband can hand over INSTEAD
    /// of the gold HireCost (e.g. Johann the Knife: "may also be hired for one portion of Crimson Shade
    /// instead of gold, since he's addicted to it"). Null = no alternative, gold is the only option (the
    /// vast majority of Gold-fee characters). Real mechanic (2026-09-01, user request), not just
    /// descriptive text: WarbandDetailViewModel.EndOfGame.ApplyRareItemSearchAsync actually removes one
    /// unit of this item from the warband's inventory when the player chooses this option instead of
    /// deducting HireCost - see RareItemSearchEntry.IsPayingWithAlternativeItem. Whole-stack removal if
    /// the band happens to own more than one unit (WarbandService.RemoveWarbandEquipmentAsync deletes the
    /// row outright) - same simplification already used everywhere else a WarbandEquipment row is
    /// consumed (Sell/assign-to-warrior), this app has no partial-stack-quantity mechanism anywhere.</summary>
    public int? AlternativePaymentItemId { get; set; }
    public EquipmentItem? AlternativePaymentItem { get; set; }

    /// <summary>Flat Warband Rating contribution (e.g. Aenur: +100) - unlike HiredSword.BaseRating, no
    /// Dramatis Persona's Rating scales with Experience in the source text gathered so far.</summary>
    public int RatingBonus { get; set; }

    /// <summary>True for a character who never stays with the warband beyond one battle - either because
    /// the book explicitly calls them a "Wanderer" (Aenur, Ulli &amp; Marquand - can't be sought again
    /// until the warband has fought at least one battle without them), or because their own mechanic
    /// requires a fresh request every time (Bertha - "A request for Bertha to aid the warband must be
    /// made for each battle you wish her to help" - corrected 2026-08-31, user caught that this is the
    /// same one-battle-only shape even though the book never uses the word "Wanderer" for her). Enforced
    /// since 2026-09-01 (WarbandDetailViewModel.EndOfGame.ApplyWandererDeparturesAsync) - a recruited
    /// warrior with this flag is automatically removed from the roster at every End of Game.</summary>
    public bool IsWanderer { get; set; }

    /// <summary>True for Aenur and Ulli &amp; Marquand (NOT Bertha, despite her also being IsWanderer) -
    /// "can't be sought again until the warband has fought at least one battle without them". Enforced
    /// (2026-09-01) via WarbandDramatisPersonaCooldownEntity: a row exists for (WarbandId, DramatisPersonaId)
    /// from the moment this persona departs (ApplyWandererDeparturesAsync) until the NEXT End of Game
    /// finishes (that battle satisfies "one battle without them") - the "Personnage spécial" search picker
    /// excludes any persona currently on cooldown for the searching warband. Bertha has no such delay (can
    /// be re-sought the very next battle), so this stays false for her even though IsWanderer is true.</summary>
    public bool RequiresCooldownBeforeResearch { get; set; }

    /// <summary>Only meaningful for Ulli &amp; Marquand (2026-09-01) - "Ulli et Marquand ne se séparent
    /// jamais et vous devez les recruter tous les deux pour une bataille". Marquand points to Ulli (and
    /// vice versa, for symmetry) - recruiting the primary of the pair (see IsHiddenFromSearchPicker)
    /// automatically recruits this one too, for the SAME shared HireCost (never charged twice - see
    /// WarbandDetailViewModel.EndOfGame.ApplyRareItemSearchAsync). Null for every other Dramatis Persona.</summary>
    public int? PairedWithDramatisPersonaId { get; set; }
    public DramatisPersona? PairedWithDramatisPersona { get; set; }

    /// <summary>True for Ulli only (2026-09-01) - the "Personnage spécial" search picker never shows her
    /// as her own selectable option (user decision, AskUserQuestion: "un seul choix « Ulli &amp; Marquand »
    /// dans le picker") - Marquand alone represents the pair there, both are recruited together via
    /// PairedWithDramatisPersonaId. She still shows up normally in the Codex (browsing/editing), only
    /// hidden from the recruitment picker itself.</summary>
    public bool IsHiddenFromSearchPicker { get; set; }

    /// <summary>True for a character who only agrees to personally join the battle when the hiring
    /// warband is genuinely outmatched (e.g. Bertha - "will only come to the aid of a warband if their
    /// enemy has a higher warband rating") - see Core.Rules.RatingGapAidTable, extracted from her own
    /// mechanic so a future Dramatis Persona sharing this exact shape reuses the same table instead of
    /// re-describing it in prose. False for every other character.
    ///
    /// Data-only, NOT wired into the End of Game wizard's "Personnage spécial" search (RareItemSearchEntry/
    /// EndOfGameDialogViewModel.RareItems.cs) - realized 2026-08-31, in conversation with the user, that it
    /// can't be: the Rating gap needs the NEXT battle's opponent, which isn't known yet at the end of the
    /// CURRENT one. This check belongs to a future "Start of Game" flow (matching an opponent, comparing
    /// Ratings) that doesn't exist in the app yet - not a gap in the End of Game wizard to fill later, a
    /// different feature entirely. Leave this flag/RatingGapAidTable as-is (ready, unused) until that flow
    /// exists rather than forcing it into the wizard.</summary>
    public bool RequiresRatingDisadvantage { get; set; }

    /// <summary>Empty = hireable by every warband. Non-empty = these WarbandArchetype ids only (e.g.
    /// Veskit: Skaven only) - same Include/Exclude semantics/UI as Skill/Mutation/EquipmentItem/HiredSword
    /// (see WarbandRestrictionEditor).</summary>
    public List<int> RestrictedToWarbandArchetypeIds { get; set; } = new();

    /// <summary>Every clean, single-effect rule this character has - some reused from the common catalog
    /// (e.g. "Causes Fear"), some genuinely unique to this one character but still catalog rows in their
    /// own right (e.g. "Invincible Swordsman") - same find-or-create idiom as HiredSword.SpecialRules,
    /// same precedent as its Dwarf Troll Slayer. Only a rule with real branching/multi-step logic (a
    /// table, a conditional sequence) stays free text in Description instead - see the type's own doc.</summary>
    public List<SpecialRule> SpecialRules { get; set; } = new();

    /// <summary>Fixed starting gear - real EquipmentItem catalogue ids, same idiom as
    /// HiredSword.StartingEquipmentIds. A one-off magic item with its own mechanical effect (e.g. Aenur's
    /// sword Ienh-Khain) still gets a real EquipmentItem row here - its effect then lives on the item
    /// itself (SpecialRules attached to it, category-appropriate cost/rarity) rather than free text.</summary>
    public List<int> StartingEquipmentIds { get; set; } = new();

    /// <summary>Skills this character already knows (e.g. Aenur: Strike to Injure, Expert Swordsman...) -
    /// resolved catalog rows (same Skills.json pool the Codex "Compétences" tab shows), NOT a pick-from-
    /// category Advance list like HiredSword.AllowedSkillCategories/WarriorArchetype - a Dramatis Persona
    /// never advances, it already has exactly this fixed list.</summary>
    public List<Skill> Skills { get; set; } = new();

    /// <summary>Null = doesn't cast spells (most of them). Set only for a character who draws from a real
    /// MagicSchool catalog entry regardless of the hiring warband (e.g. Nicodemus/Lesser Magic, Bertha/
    /// Prayers of Sigmar - both already exist as common MagicSchools.json entries) - same idiom as
    /// HiredSword.MagicSchoolId.</summary>
    public int? MagicSchoolId { get; set; }
    public MagicSchool? MagicSchool { get; set; }
}
