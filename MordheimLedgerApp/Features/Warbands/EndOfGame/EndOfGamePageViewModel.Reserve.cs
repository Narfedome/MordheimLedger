using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Features.Warbands.CreateEdit;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Réserve d'équipement de la bande : UNE seule source de vérité pour tout le wizard (2026-09-25,
/// retour utilisateur - "le tout c'est qu'on ait qu'une seule source de vérité", fin de l'unification
/// commencée le 2026-09-23 avec ReserveCollection).
///
/// **Principe** : la réserve n'est jamais stockée ni mutée par une étape. BuildReserve la RECALCULE à
/// chaque lecture à partir de l'instantané d'ouverture (_originalReserveSnapshot) et de toutes les
/// décisions du wizard, dans l'ordre du livre :
/// 1. sorties d'avant-Exploration sur la réserve d'origine - "Céder du matériel" (Où est l'Argent ?) et
///    paiement d'un Dramatis Persona en objet (Johann) ;
/// 2. apports - Exploration, Récompenses du scénario, objets rares achetés (ReserveGains), équipement
///    rendu par les renvois, achats de l'étape Achat/Vente ;
/// 3. ventes (PendingSales) ;
/// 4. recrutement - réserve consommée en priorité par les vétérans (PlanTopUps) puis par les picks
///    FromReserve des nouvelles recrues ;
/// 5. déplacements de Réallouer (_reallocationReserveMoves).
/// Chaque étape lit la réserve à SON stade (ReserveStage) : la Vente voit l'état avant ventes, le
/// Recrutement l'état après ventes, Réallouer l'état final. Plus aucune copie propre à une étape, plus
/// aucun instantané qui ignore une décision prise ailleurs.
///
/// **Persistance** : une seule écriture à Terminer, ApplyReserveAsync, qui compare la réserve finale à
/// l'instantané d'ouverture (ligne réelle réduite/supprimée, ligne nouvelle créée). Les autres Apply*Async
/// ne touchent plus du tout WarbandEquipment - seulement l'équipement porté par les guerriers, la
/// trésorerie et l'historique.
///
/// **Retour en arrière** : une décision qui vise une ligne précise (vente, déplacement de Réallouer)
/// retient sa clé (ReserveLineKey). Pour qu'un changement fait plus haut ne la laisse jamais pointer vers
/// un objet disparu, quitter Achat/Vente ou Réallouer par Précédent annule ces décisions (voir Back()) ;
/// ce qu'une recrue prend dans la réserve est recalculé en direct (ConsumeRecruitment/RecruitPickCost),
/// jamais figé. RemoveByKey reste tolérant par sécurité.</summary>
public partial class EndOfGamePageViewModel
{
    public enum ReserveStage { BeforeSales, AfterSales, AfterRecruitment, Final }

    /// <summary>Déplacements de/vers la Réserve à l'étape Réallouer, dans l'ordre chronologique (un objet
    /// peut y entrer puis en ressortir) : Out = clé de la ligne quittée, In = ce qui arrive.</summary>
    private readonly List<ReserveMove> _reallocationReserveMoves = new();

    private sealed record ReserveMove(ReserveLineKey? Out, int Quantity, ReserveInflow? In);

    private ReserveCollection BuildReserve(ReserveStage stage)
    {
        var reserve = new ReserveCollection();
        foreach (var line in _originalReserveSnapshot)
            reserve.Add(line.Item, line.MaterialRule, line.Quantity, ReserveLineOrigin.PreExisting, line.SourceId, line.FoundValueOverride);

        foreach (var sourceId in SeizedReserveSourceIds())
            reserve.RemoveSource(sourceId);
        foreach (var paymentLine in AlternativePaymentLines().Values)
            reserve.RemoveSource(paymentLine.SourceId!.Value);

        foreach (var (inflow, origin) in ReserveGains())
            reserve.Add(inflow, origin);
        foreach (var inflow in PendingDismissedEquipment())
            reserve.Add(inflow, ReserveLineOrigin.Dismissal);
        foreach (var pick in PurchasedReserveItems)
            reserve.Add(pick.Item, pick.MaterialRule, 1, ReserveLineOrigin.Purchase);
        if (stage == ReserveStage.BeforeSales) return reserve;

        foreach (var sale in PendingSales.Where(s => s.ReserveKey is not null))
            reserve.RemoveByKey(sale.ReserveKey!, sale.SelectedQuantity);
        if (stage == ReserveStage.AfterSales) return reserve;

        ConsumeRecruitment(reserve);
        if (stage == ReserveStage.AfterRecruitment) return reserve;

        foreach (var move in _reallocationReserveMoves)
        {
            if (move.Out is { } key) reserve.RemoveByKey(key, move.Quantity);
            if (move.In is { } inflow) reserve.Add(inflow, ReserveLineOrigin.Reallocation);
        }
        return reserve;
    }

    /// <summary>Stade 4 de BuildReserve : les vétérans (PlanTopUps) puis les picks marqués FromReserve des
    /// nouvelles recrues - 1 exemplaire pour un Héros, group.Count pour un nouveau groupe d'Hommes de main
    /// (tout-ou-rien, voir AddRecruitEquipment's own doc). Renvoie les picks RÉELLEMENT servis par la
    /// réserve : FromReserve n'est qu'une intention posée au moment du pick ; si la réserve ne le couvre
    /// plus (retour en arrière vers la Vente, par exemple), le pick n'y prend rien et redevient payant
    /// (RecruitPickCost) - jamais un objet gratuit sorti de nulle part.</summary>
    private HashSet<EquipmentPick> ConsumeRecruitment(ReserveCollection reserve)
    {
        foreach (var plan in PlanTopUps().Values.SelectMany(p => p))
            reserve.ConsumeUpTo(plan.Equipment.Item.Id, plan.Equipment.MaterialRule?.Id, plan.FromStash);

        var served = new HashSet<EquipmentPick>();
        void Serve(EquipmentPick pick, int needed)
        {
            if (reserve.TotalQuantity(pick.Item.Id, pick.MaterialRule?.Id) < needed) return;
            reserve.ConsumeUpTo(pick.Item.Id, pick.MaterialRule?.Id, needed);
            served.Add(pick);
        }

        foreach (var pick in HeroRecruitRows.SelectMany(r => r.NameSlots).SelectMany(s => s.Equipment).Where(p => p.FromReserve))
            Serve(pick, 1);
        foreach (var group in HenchmanRecruitRows.SelectMany(r => r.HenchmanGroupDrafts))
            foreach (var pick in group.Equipment.Where(p => p.FromReserve))
                Serve(pick, group.Count);
        return served;
    }

    /// <summary>Picks de recrutement réellement servis par la réserve en ce moment (voir
    /// ConsumeRecruitment).</summary>
    private HashSet<EquipmentPick> ServedReservePicks() => ConsumeRecruitment(BuildReserve(ReserveStage.AfterSales));

    /// <summary>Coût unitaire réel d'un pick de recrutement : son Cost habituel, sauf un pick FromReserve
    /// que la réserve ne couvre plus, alors acheté au plein tarif (matériau compris). Seule formule de coût
    /// d'un pick pour l'aperçu de trésorerie ET pour Terminer.</summary>
    private static int RecruitPickCost(EquipmentPick pick, HashSet<EquipmentPick> served) =>
        pick.FromReserve && !served.Contains(pick)
            ? EquipmentPricing.CalculateCost(pick.Item.Cost, pick.MaterialRule?.CostMultiplier, pick.IsFree || pick.IsReallocated)
            : pick.Cost;

    private bool _resolvingSeizure;

    /// <summary>"Céder du matériel" (Où est l'Argent ?) - lignes réelles perdues en entier, seulement si
    /// ce choix s'applique (même condition qu'ApplyPairEquipmentSeizureIfNeededAsync).
    ///
    /// **Garde de ré-entrée** : ce choix dépend du solde prévisionnel (DramatisPersonaeBaselineTreasury ->
    /// EndOfGameTreasuryRemaining), qui dépend du coût des vétérans (PlanTopUps), qui relit la réserve -
    /// donc ce choix-même. Pendant son propre calcul, la réserve relue le considère comme non appliqué :
    /// le coût des vétérans qui sert à décider de la saisie est celui d'une réserve encore intacte.</summary>
    private List<int> SeizedReserveSourceIds()
    {
        if (_resolvingSeizure) return new List<int>();
        _resolvingSeizure = true;
        try
        {
            return (IsPairCorruptionUnaffordable || IsPairRetentionUnaffordable) && WantsEquipmentSeizure
                ? SeizedEquipmentItems.Select(l => l.SourceId!.Value).ToList()
                : new List<int>();
        }
        finally
        {
            _resolvingSeizure = false;
        }
    }

    /// <summary>Ligne réelle cédée par chaque Dramatis Persona recruté contre un objet plutôt que de l'or
    /// (Johann) - une pile entière, première ligne de l'objet pas déjà cédée à un autre personnage. Même
    /// sélection pour la réserve (BuildReserve) et pour l'historique (ApplyRareItemSearchAsync).</summary>
    private Dictionary<RareItemSearchEntry, ReserveLine> AlternativePaymentLines()
    {
        var result = new Dictionary<RareItemSearchEntry, ReserveLine>();
        foreach (var entry in RareItemSearchEntries.Where(e => e.IsRecruited && !e.HasWyrdstoneCost && e.IsPayingWithAlternativeItem))
        {
            var line = _originalReserveSnapshot.FirstOrDefault(w => w.Item.Id == entry.SelectedCharacter!.AlternativePaymentItemId
                && !result.Values.Contains(w));
            if (line is not null) result[entry] = line;
        }
        return result;
    }

    /// <summary>Une ligne d'équipement d'un groupe existant renforcé : combien il en faut pour les
    /// recrues ajoutées, combien viennent de la réserve (gratuits) et le coût de ce qui reste à acheter.</summary>
    private sealed record TopUpEquipmentPlan(WarriorEquipment Equipment, int NeededQty, int FromStash, int ToBuy, int Cost);

    /// <summary>Consommation de la réserve par les groupes existants renforcés (vétérans), dans l'ordre
    /// d'ExistingHenchmanTopUps, depuis la réserve après ventes - un même exemplaire n'est jamais compté
    /// deux fois. Seule définition de ce calcul : lue par GetTopUpBreakdown (aperçu), BuildReserve
    /// (réserve après recrutement) et ApplyHenchmanRecruitmentAsync (Terminer).
    ///
    /// WarriorEquipment.Quantity d'un groupe d'Hommes de main est une quantité PAR MODÈLE (confirmé par
    /// l'utilisateur 2026-09-21) : chaque recrue ajoutée a besoin d'autant d'exemplaires que Quantity.</summary>
    private Dictionary<ExistingHenchmanTopUp, List<TopUpEquipmentPlan>> PlanTopUps()
    {
        var pool = BuildReserve(ReserveStage.AfterSales).ToPool();
        var plans = new Dictionary<ExistingHenchmanTopUp, List<TopUpEquipmentPlan>>();
        foreach (var topUp in ExistingHenchmanTopUps.Where(t => t.AddCount > 0))
        {
            var lines = new List<TopUpEquipmentPlan>();
            foreach (var equipment in topUp.CurrentEquipment)
            {
                var neededQty = topUp.AddCount * equipment.Quantity;
                var key = (equipment.Item.Id, equipment.MaterialRule?.Id);
                var fromStash = Math.Min(pool.GetValueOrDefault(key), neededQty);
                if (fromStash > 0) pool[key] -= fromStash;
                var toBuy = neededQty - fromStash;
                // Dague gratuite (livre des règles - "in addition to his free dagger") : chaque recrue
                // ajoutée est un modèle NEUF qui n'en porte encore aucune - jamais gratuite avec un
                // matériau (voir EquipmentPricing.IsFreeDaggerEligible).
                var isFreeDagger = equipment.Item.IsFreeDagger && equipment.MaterialRule is null;
                var cost = toBuy * EquipmentPricing.CalculateCost(equipment.Item.Cost, equipment.MaterialRule?.CostMultiplier, isFree: isFreeDagger);
                lines.Add(new TopUpEquipmentPlan(equipment, neededQty, fromStash, toBuy, cost));
            }
            plans[topUp] = lines;
        }
        return plans;
    }

    /// <summary>Seule écriture de la réserve en base de toute la Fin de Partie - appelée en fin de Finish
    /// avec la réserve finale calculée AVANT le pipeline (les Apply*Async mutent des guerriers dont
    /// dépendent certains apports, ex. l'équipement des renvoyés). Ligne d'origine : inchangée = rien,
    /// réduite = supprimée puis recréée au reliquat (pas d'update de quantité côté service), disparue =
    /// supprimée. Ligne nouvelle : créée.</summary>
    private async Task ApplyReserveAsync(ReserveCollection finalReserve)
    {
        if (Warband is null) return;

        foreach (var original in _originalReserveSnapshot)
        {
            var current = finalReserve.Lines.FirstOrDefault(l => l.SourceId == original.SourceId);
            var finalQuantity = current?.Quantity ?? 0;
            if (finalQuantity == original.Quantity) continue;

            await _warbandService.RemoveWarbandEquipmentAsync(original.SourceId!.Value);
            if (finalQuantity > 0)
                await _warbandService.AddWarbandEquipmentAsync(Warband.Id, original.Item, finalQuantity, original.MaterialRule, original.FoundValueOverride);
        }

        // Fusionnées par (Item, Matériau, valeur trouvée) quelle que soit l'origine - l'origine ne sert
        // que pendant le wizard, une ligne WarbandEquipment ne la retient pas.
        foreach (var group in finalReserve.Lines.Where(l => l.SourceId is null)
                     .GroupBy(l => (l.Item.Id, MaterialRuleId: l.MaterialRule?.Id, l.FoundValueOverride)))
        {
            var first = group.First();
            await _warbandService.AddWarbandEquipmentAsync(Warband.Id, first.Item, group.Sum(l => l.Quantity), first.MaterialRule, first.FoundValueOverride);
        }
    }
}
