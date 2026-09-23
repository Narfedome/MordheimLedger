using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Réserve d'équipement de la bande - UNE seule source de vérité pour tout le wizard Fin de
/// Partie (2026-09-23, retour utilisateur - "pour l'inventaire on va unifié le tout") : Achat/Vente/
/// Renvoyer mutent directement CETTE collection au moment où le joueur confirme sa décision, au lieu de
/// (comme avant) reconstruire indépendamment 3 vues différentes de la même réserve à chaque lecture
/// (BuildStashPool pour le Recrutement, BuildSellableCandidates pour la Vente, ReallocationReserve pour
/// Réallouer - chacune avec sa propre convention d'id synthétique). Voir EndOfGamePageViewModel.cs's
/// champ _reserve.
///
/// **Granularité ligne = ligne DB** : une PreExisting (déjà en base à l'ouverture du wizard, un
/// SourceId réel par ligne) ne se fusionne JAMAIS avec une autre, même même Item+Matériau - chaque ligne
/// réelle reste distincte pour que Terminer sache exactement laquelle réduire/supprimer. Une ligne SANS
/// SourceId (apparue pendant cette même Fin de Partie - Renvoyer/Achat, pas encore en base) se fusionne
/// par contre avec une autre ligne SANS SourceId de même (Item, Matériau, FoundValueOverride, Origin) -
/// ce sont de simples compteurs de session, rien à distinguer avant que Terminer ne les crée réellement.
///
/// **Lecture vs écriture** : TotalQuantity est un simple calcul, jamais mutant - tous les aperçus
/// d'affordabilité en direct (BuildAvailableReservePool, GetTopUpBreakdown...) doivent s'appuyer
/// dessus, jamais sur Add/Remove/TryConsume, réservés aux actions utilisateur explicitement confirmées.
///
/// **Exploration reste hors de cette collection** (voir EndOfGamePageViewModel.Exploration.cs's
/// PendingExplorationStashItems, laissé tel quel) : ses champs résolus (montant/objet trouvé) sont
/// saisis en direct par le joueur (Entry, pas un bouton "Confirmer" ponctuel) et peuvent changer de
/// valeur à tout moment tant que l'étape reste ouverte, y compris après un retour en arrière - il n'y a
/// pas de moment stable où "pousser" une décision définitive dans cette collection sans devoir suivre et
/// annuler la précédente valeur poussée à chaque frappe. Les lecteurs de cette collection qui doivent
/// voir les trouvailles d'Exploration (BuildAvailableReservePool, BuildSellableCandidates, la carte
/// Réserve de Réallouer) continuent de fusionner PendingExplorationStashItems() en plus de Lines, au
/// moment de la lecture.</summary>
public sealed class ReserveCollection
{
    private readonly List<ReserveLine> _lines = new();

    public IReadOnlyList<ReserveLine> Lines => _lines;

    /// <summary>Ajoute une quantité - fusionne dans une ligne existante SANS SourceId de même clé si une
    /// telle ligne existe déjà (voir la doc de classe), sinon crée une nouvelle ligne. sourceId renseigné
    /// UNIQUEMENT pour le seed initial depuis la DB (une ligne PreExisting par ligne WarbandEquipment
    /// réelle) - jamais pour une décision prise pendant le wizard.</summary>
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

    /// <summary>Combien d'exemplaires de cet Item+Matériau, toutes lignes/origines confondues - lecture
    /// pure, jamais mutante. Ignore FoundValueOverride (même principe que l'ancien BuildStashPool - une
    /// question de DISPONIBILITÉ, pas de quelle ligne précise sera consommée).</summary>
    public int TotalQuantity(int itemId, int? materialRuleId) =>
        _lines.Where(l => l.Item.Id == itemId && l.MaterialRule?.Id == materialRuleId).Sum(l => l.Quantity);

    /// <summary>Retire quantity exemplaires si le stock est suffisant (toutes lignes confondues, dans
    /// l'ordre où elles existent - jamais un ordre garanti entre PreExisting/Dismissal/Purchase), sinon
    /// ne mute rien et renvoie false.</summary>
    public bool TryConsume(int itemId, int? materialRuleId, int quantity)
    {
        if (TotalQuantity(itemId, materialRuleId) < quantity) return false;
        Consume(itemId, materialRuleId, quantity);
        return true;
    }

    /// <summary>Retire quantity exemplaires SANS vérifier le stock au préalable (l'appelant sait déjà que
    /// cette ligne précise en contient assez, ex. annuler un Achat/une vente déjà confirmée) - ne
    /// descend jamais sous 0 par ligne, la ligne disparaît une fois vidée.</summary>
    public void Remove(int itemId, int? materialRuleId, int quantity) => Consume(itemId, materialRuleId, quantity);

    private void Consume(int itemId, int? materialRuleId, int quantity)
    {
        var remaining = quantity;
        foreach (var line in _lines.Where(l => l.Item.Id == itemId && l.MaterialRule?.Id == materialRuleId).ToList())
        {
            if (remaining <= 0) break;
            var taken = Math.Min(line.Quantity, remaining);
            line.Quantity -= taken;
            remaining -= taken;
            if (line.Quantity <= 0) _lines.Remove(line);
        }
    }

    /// <summary>Retire directement CETTE ligne précise (identité de référence, pas une clé) - pour un
    /// appelant qui a déjà résolu la ligne exacte à consommer (ex. un candidat de Vente construit
    /// directement depuis une ReserveLine) plutôt que de rejouer une recherche par clé.</summary>
    public void RemoveLine(ReserveLine line, int quantity)
    {
        line.Quantity -= quantity;
        if (line.Quantity <= 0) _lines.Remove(line);
    }

    /// <summary>Contrepartie de RemoveLine - restaure quantity sur CETTE MÊME instance de ligne (jamais
    /// une nouvelle ligne, pour ne pas perdre son identité - ex. SourceId d'une ligne PreExisting) : pour
    /// annuler une vente déjà confirmée avant Terminer (voir EndOfGamePageViewModel.EquipmentTrading.
    /// cs's RemoveSaleEquipment). Réinsère la ligne si RemoveLine l'avait entièrement retirée (vente qui
    /// vidait tout le stock).</summary>
    public void RestoreLine(ReserveLine line, int quantity)
    {
        line.Quantity += quantity;
        if (!_lines.Contains(line)) _lines.Add(line);
    }
}
