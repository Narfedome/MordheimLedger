using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands;

/// <summary>Résout un <see cref="Warrior"/> persisté en <see cref="WarriorRow"/> (RoleName + puces Règles
/// spéciales/Haine résolues) - extrait de <c>WarbandDetailViewModel.ToRow</c> (2026-09-22, passage de Fin
/// de Partie en page Shell) pour être partagé avec <c>EndOfGame.EndOfGamePageViewModel</c> sans dupliquer
/// cette logique : sans cette extraction, la résolution du roster (RoleName/Règles spéciales/Haine)
/// pourrait diverger silencieusement entre la fiche de bande et le wizard. Catalogues capturés au
/// constructeur, résolus une fois par l'appelant (voir <c>WarbandDetailViewModel.LoadAsync</c>/
/// <c>EndOfGamePageViewModel.InitializeAsync</c>) - jamais rechargés en interne.</summary>
public sealed class WarriorRosterBuilder
{
    private readonly List<WarriorArchetype> _recruitableArchetypes;
    private readonly List<HiredSword> _recruitableHiredSwords;
    private readonly List<DramatisPersona> _recruitableDramatisPersonae;
    private readonly List<SpecialRule> _bandWideSpecialRules;
    private readonly List<MagicSchool> _bandMagicSchools;

    /// <summary>All WarbandArchetype names (not just this warband's own), needed to resolve a Hatred
    /// rule's target - which can point at any band type, not just the current one. See
    /// BuildSpecialRuleChips.</summary>
    private readonly Dictionary<int, string> _warbandArchetypeNames;

    private static LocalizationService Loc => LocalizationService.Instance;

    public WarriorRosterBuilder(List<WarriorArchetype> recruitableArchetypes, List<HiredSword> recruitableHiredSwords,
        List<DramatisPersona> recruitableDramatisPersonae, List<SpecialRule> bandWideSpecialRules, List<MagicSchool> bandMagicSchools,
        Dictionary<int, string> warbandArchetypeNames)
    {
        _recruitableArchetypes = recruitableArchetypes;
        _recruitableHiredSwords = recruitableHiredSwords;
        _recruitableDramatisPersonae = recruitableDramatisPersonae;
        _bandWideSpecialRules = bandWideSpecialRules;
        _bandMagicSchools = bandMagicSchools;
        _warbandArchetypeNames = warbandArchetypeNames;
    }

    public List<WarriorRow> BuildRows(IEnumerable<Warrior> warriors) => warriors.Select(ToRow).ToList();

    public WarriorRow ToRow(Warrior warrior)
    {
        // Un guerrier recruté d'un Franc-Tireur (voir Warrior.IsHiredSword) n'a pas de WarriorArchetype
        // à résoudre - RoleName vient du catalogue HiredSword à la place, ses propres SpecialRules
        // jouant le rôle que jouerait archetypeRules ci-dessous. École de magie : PROPRE au Franc-Tireur
        // (HiredSword.MagicSchoolId, ex. le Sorcier/Magie Mineure) plutôt que celle de la bande qui
        // l'engage - contrairement à un WarriorArchetype normal (IsSpellcaster + écoles DE LA BANDE).
        if (warrior.HiredSwordId is { } hiredSwordId)
        {
            var hiredSword = _recruitableHiredSwords.FirstOrDefault(h => h.Id == hiredSwordId);
            var hiredSwordMergedRules = _bandWideSpecialRules.Concat(hiredSword?.SpecialRules ?? new List<SpecialRule>()).DistinctBy(r => r.Id);
            var hiredSwordHatredChips = warrior.Hatreds.Select(h => new WarriorHatredChip { Item = h, Name = string.Format(Loc["WarriorsHatredChipFormat"], h.Name) })
                .Concat(BuildRuleHatredChips(HatredSourceRules(warrior, hiredSwordMergedRules)))
                .Concat(BuildSkillHatredChips(warrior.Skills.Select(s => s.Item)));
            var hiredSwordMagicSchools = hiredSword?.MagicSchool is { } school ? new List<MagicSchool> { school } : null;
            return new WarriorRow(warrior, hiredSword?.Name ?? "?", BuildSpecialRuleChips(hiredSwordMergedRules), hiredSwordMagicSchools, hiredSwordHatredChips);
        }

        // Même raisonnement pour un guerrier recruté depuis le catalogue Dramatis Personae (voir
        // Warrior.IsDramatisPersona) - ni WarriorArchetypeId ni HiredSwordId ne sont renseignés pour lui,
        // il tomberait sinon dans la branche générique ci-dessous avec archetype=null (aucune SpecialRule/
        // MagicSchool/Hatred résolue - bug trouvé le 2026-09-01 en câblant son propre bloc de roster).
        // RoleName vide (contrairement au Franc-Tireur ci-dessus) : Warrior.Name EST déjà le nom du
        // personnage (voir WarbandService.RecruitDramatisPersonaAsync, persona.Name recopié tel quel à
        // l'engagement) - afficher le nom du catalogue en RoleName aurait juste répété le titre de la
        // carte (retour utilisateur 2026-09-01). Vide plutôt que "?" : même convention que
        // WarriorRow.HeadCountDisplay, une chaîne vide se rend invisible dans la Grid de la carte.
        if (warrior.DramatisPersonaId is { } dramatisPersonaId)
        {
            var persona = _recruitableDramatisPersonae.FirstOrDefault(p => p.Id == dramatisPersonaId);
            var personaMergedRules = _bandWideSpecialRules.Concat(persona?.SpecialRules ?? new List<SpecialRule>()).DistinctBy(r => r.Id);
            var personaHatredChips = warrior.Hatreds.Select(h => new WarriorHatredChip { Item = h, Name = string.Format(Loc["WarriorsHatredChipFormat"], h.Name) })
                .Concat(BuildRuleHatredChips(HatredSourceRules(warrior, personaMergedRules)))
                .Concat(BuildSkillHatredChips(warrior.Skills.Select(s => s.Item)));
            var personaMagicSchools = persona?.MagicSchool is { } personaSchool ? new List<MagicSchool> { personaSchool } : null;
            return new WarriorRow(warrior, string.Empty, BuildSpecialRuleChips(personaMergedRules), personaMagicSchools, personaHatredChips, persona);
        }

        var archetype = _recruitableArchetypes.FirstOrDefault(a => a.Id == warrior.WarriorArchetypeId);
        var archetypeRules = archetype?.SpecialRules ?? new List<SpecialRule>();
        // Une règle accordée par un objet équipé (ex. Marteau des Sorcières) ou par une Blessure Grave
        // permanente (Folie 24 -> Stupidité/Frénésie, voir Injury.SpecialRules) n'est PAS fusionnée ici -
        // chacune reste visible uniquement derrière sa propre puce (celle de l'objet/EquipmentDetailDialog,
        // celle de la Blessure/InjuryDetailDialog qui affiche la SpecialRule en puce imbriquée) plutôt que
        // remontée aussi dans Règles spéciales. Revenu sur la fusion équipement (2026-09-01, retour
        // utilisateur - initialement un correctif volontaire, jugé après coup produire le même risque de
        // puce en double déjà écarté pour les Blessures le 2026-08-26 : "une fois via ... dans Blessures,
        // une fois via ... dans Règles spéciales").
        var mergedRules = _bandWideSpecialRules.Concat(archetypeRules).DistinctBy(r => r.Id);
        // Un lanceur de sorts pioche dans les écoles de SA bande (pas d'affiliation propre au guerrier) -
        // voir WarriorRow.MagicSchools. Vide pour tout autre guerrier.
        var magicSchools = archetype?.IsSpellcaster == true ? _bandMagicSchools : null;
        var hatredChips = warrior.Hatreds.Select(h => new WarriorHatredChip { Item = h, Name = string.Format(Loc["WarriorsHatredChipFormat"], h.Name) })
            .Concat(BuildRuleHatredChips(HatredSourceRules(warrior, mergedRules)))
            .Concat(BuildSkillHatredChips(warrior.Skills.Select(s => s.Item)));
        return new WarriorRow(warrior, archetype?.Name ?? "?", BuildSpecialRuleChips(mergedRules), magicSchools, hatredChips);
    }

    /// <summary>A rule with a mechanized Hatred target explodes into one chip per target ("Haine :
    /// Skavens") instead of a single generic "Haine" chip - either a WarbandArchetype target
    /// (SpecialRule.HatredTargetWarbandArchetypeIds, one chip per band) or the spellcaster-trait target
    /// (SpecialRule.HatredTargetsSpellcasters, e.g. "Au Bûcher !" - one chip, not band-scoped). A rule
    /// with neither maps 1:1 to its own catalog Name, unchanged.</summary>
    private List<SpecialRuleChip> BuildSpecialRuleChips(IEnumerable<SpecialRule> rules)
    {
        var chips = new List<SpecialRuleChip>();
        foreach (var rule in rules)
        {
            if (rule.HatredTargetWarbandArchetypeIds.Count == 0 && !rule.HatredTargetsSpellcasters)
            {
                chips.Add(new SpecialRuleChip { Item = rule, Name = rule.Name });
                continue;
            }

            foreach (var targetId in rule.HatredTargetWarbandArchetypeIds)
            {
                var targetName = _warbandArchetypeNames.GetValueOrDefault(targetId, "?");
                chips.Add(new SpecialRuleChip { Item = rule, Name = string.Format(Loc["WarriorsHatredChipFormat"], targetName) });
            }
            if (rule.HatredTargetsSpellcasters)
                chips.Add(new SpecialRuleChip { Item = rule, Name = string.Format(Loc["WarriorsHatredChipFormat"], Loc["HatredTargetSpellcasters"]) });
        }
        return chips;
    }

    /// <summary>Every SpecialRule that could plausibly grant Hatred for this warrior, gathered
    /// independently of whatever BuildSpecialRuleChips shows in "Règles spéciales" - the two sections
    /// deliberately no longer share one list (2026-09-01, user request/regression report): "Règles
    /// spéciales" dropped equipment-granted rules to avoid duplicate chips (see the "change partout"
    /// note in ToRow), but "Haine" must still see them - a Hatred-granting weapon (or anything else) has
    /// to show up there regardless of what the generic rules list decides to display. baseRules is
    /// whatever this branch already resolved (band-wide + archetype/HiredSword/DramatisPersona's own
    /// SpecialRules); equipment is added back in on top, specifically for this computation. Skills are
    /// a separate source (see BuildSkillHatredChips below, e.g. Bertha's "Righteous Fury") since Skill
    /// carries its own HatredTargetWarbandArchetypeIds rather than reusing SpecialRule's.</summary>
    private static IEnumerable<SpecialRule> HatredSourceRules(Warrior warrior, IEnumerable<SpecialRule> baseRules) =>
        baseRules.Concat(warrior.Equipment.SelectMany(e => e.Item.SpecialRules)).DistinctBy(r => r.Id);

    /// <summary>Same explosion as BuildRuleHatredChips, reading Skill.HatredTargetWarbandArchetypeIds
    /// instead of SpecialRule's - e.g. Bertha's "Righteous Fury" (Skill, Sœurs de Sigmar) grants Hatred
    /// against Skaven/Undead/Possédés/Hommes-Bêtes. Skill has no spellcaster-trait target equivalent to
    /// SpecialRule.HatredTargetsSpellcasters, so this only explodes the band-target list. Added
    /// 2026-09-01 per user request ("des skill" - the one Hatred source not yet covered by
    /// HatredSourceRules).</summary>
    private List<WarriorHatredChip> BuildSkillHatredChips(IEnumerable<Skill> skills)
    {
        var chips = new List<WarriorHatredChip>();
        foreach (var skill in skills)
        {
            foreach (var targetId in skill.HatredTargetWarbandArchetypeIds)
                chips.Add(new WarriorHatredChip { Name = string.Format(Loc["WarriorsHatredChipFormat"], _warbandArchetypeNames.GetValueOrDefault(targetId, "?")) });
        }
        return chips;
    }

    /// <summary>Same explosion as BuildSpecialRuleChips (one chip per mechanized Hatred target, band or
    /// spellcaster-trait), mirrored into the roster card's "Haine" section - user request 2026-08-28:
    /// hatred granted by a special rule (e.g. "Haine des Orques et des Gobelins", "Au Bûcher !") should
    /// show up alongside Rancune-granted hatred, not just in Règles spéciales. Item stays null - see
    /// WarriorHatredChip.</summary>
    private List<WarriorHatredChip> BuildRuleHatredChips(IEnumerable<SpecialRule> rules)
    {
        var chips = new List<WarriorHatredChip>();
        foreach (var rule in rules)
        {
            foreach (var targetId in rule.HatredTargetWarbandArchetypeIds)
                chips.Add(new WarriorHatredChip { Name = string.Format(Loc["WarriorsHatredChipFormat"], _warbandArchetypeNames.GetValueOrDefault(targetId, "?")) });
            if (rule.HatredTargetsSpellcasters)
                chips.Add(new WarriorHatredChip { Name = string.Format(Loc["WarriorsHatredChipFormat"], Loc["HatredTargetSpellcasters"]) });
        }
        return chips;
    }
}
