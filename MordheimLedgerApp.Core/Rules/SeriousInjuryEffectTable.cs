namespace MordheimLedgerApp.Core.Rules;

/// <summary>What kind of mechanical effect a Serious Injury result applies to the Warrior - see
/// SeriousInjuryEffectTable.TryGetOutcome. Palier 1 only (see the plan this was built from): results
/// whose effect needs a new game concept not yet modeled (a per-warrior granted SpecialRule for
/// Madness/Hardened/Horrible Scars, a "one-handed weapon only" flag for a severe Arm Wound, the
/// branching Captured/Sold To The Pits sub-flows, the recurring Old Battle Wound check with no
/// "start of battle" UI moment to hook into) are deliberately left out of this table for now - see
/// SeriousInjuryTable.TryGetTextKey, still the only thing those resolve to.</summary>
public enum SeriousInjuryEffectKind
{
    /// <summary>-1 to a specific characteristic, permanent - see SeriousInjuryOutcome.Field.</summary>
    CharacteristicPenalty,

    /// <summary>All carried equipment is lost outright (Robbed, 36) - not sold, no gold refund.</summary>
    LoseAllEquipment,

    /// <summary>+1 Experience (Survives Against The Odds, 66).</summary>
    GainExperience,

    /// <summary>Misses exactly the next game (light Arm Wound/Smashed Leg sub-roll 2-6) - one game of
    /// WarriorStatus.Sick, see Warrior.SickGamesRemaining.</summary>
    MissNextGame,

    /// <summary>Misses the next D3 games (Deep Wound, 35) - see Warrior.SickGamesRemaining.</summary>
    MissGamesRollD3,

    /// <summary>Forces the warrior into permanent retirement (WarriorStatus.Retired) - only reachable
    /// via Blinded in One Eye (31) when the warrior already carries a prior instance of that same
    /// Injury (losing the SECOND eye). See TryGetOutcome's alreadyBlindedInOneEye parameter.</summary>
    ForcedRetirement
}

/// <summary>Structured counterpart to SeriousInjuryOutcome.Kind's target field - only meaningful for
/// CharacteristicPenalty.</summary>
public sealed record SeriousInjuryOutcome
{
    public required SeriousInjuryEffectKind Kind { get; init; }
    public CharacteristicField? Field { get; init; }
}

/// <summary>Mechanized subset of the Heroes' Serious Injuries D66 chart (see SeriousInjuryTable, which
/// still owns the full reference text/IsDeath/IsMultipleInjuries/IsBitterEnmity for every result) -
/// deliberately additive, same split as AdvanceOutcome/HeroAdvanceTable.TryGetOutcome. Only the
/// results with an effect that maps cleanly onto an already-modeled Warrior stat/status are covered
/// here ("Palier 1") - everything else (Madness, Old Battle Wound, Captured, Hardened, Horrible
/// Scars, Sold To The Pits, and the severe branches of Arm Wound/Smashed Leg) stays reference text
/// only, same as before this table existed. Verified against the rulebook text provided directly by
/// the user (2026-08-25).</summary>
public static class SeriousInjuryEffectTable
{
    /// <summary>Equivalent to TryGetOutcome(roll, alreadyBlindedInOneEye: false, out outcome) - every
    /// call site that can't be blinded twice in the same breath (existing RulesTests.cs coverage, the
    /// Henchman D6 chart which never reaches 31 anyway) keeps using this simpler overload.</summary>
    public static bool TryGetOutcome(int roll, out SeriousInjuryOutcome outcome) =>
        TryGetOutcome(roll, alreadyBlindedInOneEye: false, out outcome);

    /// <param name="alreadyBlindedInOneEye">True if this warrior already carries a prior "Blinded in
    /// One Eye" Injury (roll 31, see Warrior.Injuries) - only affects roll 31: per the rulebook's own
    /// description of that result, losing the SECOND eye forces the warrior into permanent retirement
    /// instead of the usual -1 Ballistic Skill (which already happened the first time).</param>
    public static bool TryGetOutcome(int roll, bool alreadyBlindedInOneEye, out SeriousInjuryOutcome outcome)
    {
        outcome = roll switch
        {
            22 => new SeriousInjuryOutcome { Kind = SeriousInjuryEffectKind.CharacteristicPenalty, Field = CharacteristicField.Movement },
            26 => new SeriousInjuryOutcome { Kind = SeriousInjuryEffectKind.CharacteristicPenalty, Field = CharacteristicField.Toughness },
            31 when alreadyBlindedInOneEye => new SeriousInjuryOutcome { Kind = SeriousInjuryEffectKind.ForcedRetirement },
            31 => new SeriousInjuryOutcome { Kind = SeriousInjuryEffectKind.CharacteristicPenalty, Field = CharacteristicField.BallisticSkill },
            33 => new SeriousInjuryOutcome { Kind = SeriousInjuryEffectKind.CharacteristicPenalty, Field = CharacteristicField.Initiative },
            34 => new SeriousInjuryOutcome { Kind = SeriousInjuryEffectKind.CharacteristicPenalty, Field = CharacteristicField.WeaponSkill },
            35 => new SeriousInjuryOutcome { Kind = SeriousInjuryEffectKind.MissGamesRollD3 },
            36 => new SeriousInjuryOutcome { Kind = SeriousInjuryEffectKind.LoseAllEquipment },
            66 => new SeriousInjuryOutcome { Kind = SeriousInjuryEffectKind.GainExperience },
            _ => null!
        };
        return outcome is not null;
    }

    /// <summary>Arm Wound (23) and Smashed Leg (25) both resolve their real-world effect via a further
    /// 1D6 rolled on top of the main D66 - the severe branch (1) needs a game concept this table
    /// doesn't cover yet (one-handed weapon only / can't run), the light branch (2-6) is a single
    /// missed game either way. Madness (24) also branches on a further 1D6 (1-3 Stupidity, 4-6 Frenzy),
    /// but neither branch produces a SeriousInjuryOutcome here - see TryGetBranchSubRollOutcome's doc -
    /// the branch only picks which catalog Injury (and its attached SpecialRule) gets attached, no
    /// separate mechanized effect on top.</summary>
    public static bool RequiresBranchSubRoll(int roll) => roll is 23 or 25 or 24;

    /// <param name="roll">The main D66 roll - one of RequiresBranchSubRoll's 3 values, but only 23/25
    /// ever produce a SeriousInjuryOutcome here: Madness (24) always falls through to false regardless
    /// of subRoll - see SeriousInjuryTable.TryGetBranchTextKey for how its branch is still resolved (a
    /// catalog Injury attachment, not a mechanized effect).</param>
    /// <param name="subRoll">The further 1D6 roll.</param>
    public static bool TryGetBranchSubRollOutcome(int roll, int subRoll, out SeriousInjuryOutcome outcome)
    {
        if (roll is 23 or 25 && subRoll is >= 2 and <= 6)
        {
            outcome = new SeriousInjuryOutcome { Kind = SeriousInjuryEffectKind.MissNextGame };
            return true;
        }

        outcome = null!;
        return false;
    }

    /// <summary>Rolls 1D6, for the Arm Wound/Smashed Leg branch sub-roll above.</summary>
    public static int RollSubDie() => Random.Shared.Next(1, 7);

    /// <summary>Rolls a Mordheim-style D3 (1D6 halved, rounded up) - for Deep Wound's "misses the next
    /// D3 games".</summary>
    public static int RollD3() => (Random.Shared.Next(1, 7) + 1) / 2;
}
