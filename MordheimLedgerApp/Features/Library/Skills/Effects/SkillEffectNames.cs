using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Features.Library.Skills.Effects;

/// <summary>Noms affichables des cibles d'un effet (SkillEffect.Targets / ConditionTargets), qui sont des
/// OfficialId ou des noms d'enum - jamais des Id locaux. Chargé une fois à l'ouverture d'un dialog du Codex,
/// passé tel quel au présentateur et au popup d'édition. Les listes OfficialId -> nom servent aussi de choix
/// proposés par le popup (objets portés, écoles de magie, bandes).</summary>
public sealed class SkillEffectNames
{
    public static readonly SkillEffectNames Empty = new([], [], [], []);

    private readonly Dictionary<string, string> _byOfficialId;

    /// <summary>Objets proposés pour « Porte un objet » : équipement divers et artefacts magiques seulement
    /// (grimoire, porte-bonheur...), triés par nom - les armes, armures, montures et consommables ne
    /// servent pas de condition et noyaient la liste (plus de 100 entrées). Une cible déjà posée hors de
    /// ces catégories reste affichée et modifiable, elle n'est juste plus proposée.</summary>
    public IReadOnlyList<(string OfficialId, string Name)> CarriableItems { get; }

    /// <summary>Écoles de magie (choix de « Ouvre » pour un Choix de sort).</summary>
    public IReadOnlyList<(string OfficialId, string Name)> MagicSchools { get; }

    /// <summary>Bandes (choix de « Hors de certaines bandes »).</summary>
    public IReadOnlyList<(string OfficialId, string Name)> WarbandArchetypes { get; }

    private SkillEffectNames(IReadOnlyList<(string, string)> equipment, IReadOnlyList<(string, string)> carriable,
        IReadOnlyList<(string, string)> schools, IReadOnlyList<(string, string)> warbands, IEnumerable<(string, string)>? others = null)
    {
        CarriableItems = carriable;
        MagicSchools = schools;
        WarbandArchetypes = warbands;
        _byOfficialId = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (id, name) in equipment.Concat(schools).Concat(warbands).Concat(others ?? []))
            _byOfficialId.TryAdd(id, name);
    }

    public static async Task<SkillEffectNames> LoadAsync(ILibraryService library, string language)
    {
        var equipment = await library.GetEquipmentItemsAsync(language);
        var schools = await library.GetMagicSchoolsAsync(language);
        var warbands = await library.GetWarbandArchetypesAsync(language);
        var warriors = await library.GetWarriorArchetypesAsync(warbands.Select(w => w.Id), language);
        var skills = await library.GetSkillsAsync(language);

        static List<(string, string)> Pairs<T>(IEnumerable<T> items, Func<T, string?> id, Func<T, string> name) =>
            items.Where(i => !string.IsNullOrEmpty(id(i)))
                 .Select(i => (id(i)!, name(i)))
                 .OrderBy(p => p.Item2, StringComparer.CurrentCultureIgnoreCase)
                 .ToList();

        return new SkillEffectNames(
            Pairs(equipment, e => e.OfficialId, e => e.Name),
            Pairs(equipment.Where(e => e.Category is EquipmentCategory.MiscellaneousEquipment or EquipmentCategory.MagicalArtefact),
                e => e.OfficialId, e => e.Name),
            Pairs(schools, s => s.OfficialId, s => s.Name),
            Pairs(warbands, w => w.OfficialId, w => w.Name),
            Pairs(warriors, w => w.OfficialId, w => w.Name).Concat(Pairs(skills, s => s.OfficialId, s => s.Name)));
    }

    /// <summary>Nom d'une cible : caractéristique (seulement pour une Augmentation de caractéristique -
    /// « Strength » y est la Force du profil, pas la catégorie de compétence), catégorie d'équipement ou de
    /// compétence (nom d'enum), OfficialId connu, sinon l'identifiant tel quel (une cible qui pointe vers un
    /// contenu absent de cette base reste lisible).</summary>
    public string NameOf(string target, EffectMoment? moment = null)
    {
        if (moment == EffectMoment.CharacteristicIncrease && Enum.TryParse<Core.Rules.CharacteristicField>(target, out var field))
            return Services.LocalizationService.Instance[$"Stat{field}Abbr"];
        if (Enum.TryParse<EquipmentCategory>(target, out var equipmentCategory))
            return Services.LocalizationService.Instance[$"EquipmentCategory{equipmentCategory}"];
        if (Enum.TryParse<SkillCategory>(target, out var skillCategory))
            return Services.LocalizationService.Instance[$"SkillCategory{skillCategory}"];
        return _byOfficialId.TryGetValue(target, out var name) ? name : target;
    }
}
