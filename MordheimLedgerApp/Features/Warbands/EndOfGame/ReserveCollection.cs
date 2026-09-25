using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Réserve d'équipement de la bande à un instant donné du wizard Fin de Partie. Jamais gardée
/// en champ ni mutée par une étape : EndOfGamePageViewModel.BuildReserve en construit une neuve à chaque
/// lecture, à partir de l'instantané d'ouverture et de TOUTES les décisions du wizard (2026-09-25,
/// retour utilisateur - "qu'on ait qu'une seule source de vérité", fin de l'unification commencée le
/// 2026-09-23). Voir EndOfGamePageViewModel.Reserve.cs.
///
/// **Granularité ligne = ligne DB** : une PreExisting (SourceId réel) ne se fusionne JAMAIS avec une
/// autre, même même Item+Matériau - Terminer doit savoir exactement quelle ligne réelle réduire/supprimer.
/// Une ligne SANS SourceId (née pendant la Fin de Partie) se fusionne avec une autre ligne sans SourceId
/// de même (Item, Matériau, FoundValueOverride, Origin).</summary>
public sealed class ReserveCollection
{
    private readonly List<ReserveLine> _lines = new();

    public IReadOnlyList<ReserveLine> Lines => _lines;

    /// <summary>sourceId renseigné UNIQUEMENT pour l'instantané d'ouverture (une ligne PreExisting par
    /// ligne WarbandEquipment réelle).</summary>
    public void Add(EquipmentItem item, SpecialRule? materialRule, int quantity, ReserveLineOrigin origin, int? sourceId = null, int? foundValueOverride = null)
    {
        if (quantity <= 0) return;

        if (sourceId is null)
        {
            var existing = _lines.FirstOrDefault(l => l.SourceId is null && l.Origin == origin
                && l.Item.Id == item.Id && l.MaterialRule?.Id == materialRule?.Id && l.FoundValueOverride == foundValueOverride);
            if (existing is not null)
            {
                existing.Quantity += quantity;
                return;
            }
        }

        _lines.Add(new ReserveLine(item, materialRule, quantity, origin, sourceId, foundValueOverride));
    }

    public void Add(ReserveInflow inflow, ReserveLineOrigin origin) =>
        Add(inflow.Item, inflow.MaterialRule, inflow.Quantity, origin, foundValueOverride: inflow.FoundValueOverride);

    /// <summary>Combien d'exemplaires de cet Item+Matériau, toutes lignes/origines confondues.</summary>
    public int TotalQuantity(int itemId, int? materialRuleId) =>
        _lines.Where(l => l.Item.Id == itemId && l.MaterialRule?.Id == materialRuleId).Sum(l => l.Quantity);

    /// <summary>Quantités par (Item, Matériau) - la vue qu'utilisent le Recrutement et le sélecteur
    /// d'équipement (qui ne distinguent ni l'origine ni la valeur trouvée).</summary>
    public Dictionary<(int ItemId, int? MaterialRuleId), int> ToPool()
    {
        var pool = new Dictionary<(int ItemId, int? MaterialRuleId), int>();
        foreach (var line in _lines)
        {
            var key = (line.Item.Id, line.MaterialRule?.Id);
            pool[key] = pool.GetValueOrDefault(key) + line.Quantity;
        }
        return pool;
    }

    /// <summary>Retire jusqu'à quantity exemplaires de cet Item+Matériau, lignes dans leur ordre
    /// d'apparition (réserve d'avant la partie d'abord) - renvoie combien ont réellement été retirés.</summary>
    public int ConsumeUpTo(int itemId, int? materialRuleId, int quantity)
    {
        var remaining = quantity;
        foreach (var line in _lines.Where(l => l.Item.Id == itemId && l.MaterialRule?.Id == materialRuleId).ToList())
        {
            if (remaining <= 0) break;
            remaining -= Take(line, remaining);
        }
        return quantity - remaining;
    }

    /// <summary>Retire jusqu'à quantity exemplaires de LA ligne désignée par key (vente, déplacement vers
    /// un Héros) - sans effet si cette ligne n'existe plus (décision en amont modifiée depuis).</summary>
    public void RemoveByKey(ReserveLineKey key, int quantity)
    {
        var line = _lines.FirstOrDefault(l => l.Key == key);
        if (line is not null) Take(line, quantity);
    }

    /// <summary>Retire ENTIÈREMENT la ligne réelle sourceId (une pile entière - saisie de matériel,
    /// paiement en objet).</summary>
    public void RemoveSource(int sourceId) => _lines.RemoveAll(l => l.SourceId == sourceId);

    private int Take(ReserveLine line, int quantity)
    {
        var taken = Math.Min(line.Quantity, quantity);
        line.Quantity -= taken;
        if (line.Quantity <= 0) _lines.Remove(line);
        return taken;
    }
}
