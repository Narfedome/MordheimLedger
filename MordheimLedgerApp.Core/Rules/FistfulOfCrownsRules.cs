using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Rules;

/// <summary>Un personnage, ou un duo, qui porte « Une Poignée d'Or » : il se corrompt, se garde et se paie d'un bloc.
/// <see cref="Primary"/> est la moitié visible (celle de la recherche, qui porte la description du duo).</summary>
public sealed record FistfulOfCrownsGroup(DramatisPersona Primary, DramatisPersona? Partner)
{
    public bool Contains(int personaId) => Primary.Id == personaId || Partner?.Id == personaId;

    /// <summary>« Marquand Volker &amp; Ulli Leitpold », ou le nom seul d'un personnage sans partenaire.</summary>
    public string DisplayName => Partner is null ? Primary.Name : $"{Primary.Name} & {Partner.Name}";
}

/// <summary>Qui est concerné par « Une Poignée d'Or » (2026-11-14) : n'importe quel Dramatis Persona qui porte la règle,
/// seul ou en duo, et plusieurs à la fois - en attendant un mode multijoueur, l'appli ne sait pas qui est dans la bande
/// adverse, elle propose donc tous ceux du catalogue que la bande n'a pas.</summary>
public static class FistfulOfCrownsRules
{
    /// <summary>Un groupe par personnage ou duo portant la règle, dans l'ordre du catalogue. Un duo n'apparaît qu'une
    /// fois, quelle que soit la moitié qui porte la règle, avec sa moitié visible en premier.</summary>
    public static List<FistfulOfCrownsGroup> Groups(IEnumerable<DramatisPersona> catalog, int? ruleId)
    {
        var groups = new List<FistfulOfCrownsGroup>();
        if (ruleId is null) return groups;

        var personae = catalog.ToList();
        var byId = personae.ToDictionary(p => p.Id);
        var grouped = new HashSet<int>();
        foreach (var persona in personae.Where(p => p.SpecialRules.Any(r => r.Id == ruleId)))
        {
            if (grouped.Contains(persona.Id)) continue;

            var partner = persona.PairedWithDramatisPersonaId is { } partnerId ? byId.GetValueOrDefault(partnerId) : null;
            var primary = persona;
            if (partner is not null && persona.IsHiddenFromSearchPicker && !partner.IsHiddenFromSearchPicker)
                (primary, partner) = (partner, persona);

            grouped.Add(primary.Id);
            if (partner is not null) grouped.Add(partner.Id);
            groups.Add(new FistfulOfCrownsGroup(primary, partner));
        }
        return groups;
    }
}
