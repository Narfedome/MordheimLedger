namespace MordheimLedgerApp.Core.Rules;

/// <summary>Comment une bande règle Ulli et Marquand quand elle ne peut pas payer en or (règle « Une Poignée d'Or » :
/// « Si c'est le cas, Ulli et Marquand se payeront en armes et équipement... Si c'est impossible, ils se vengeront sur
/// le chef de la bande »). Aucun or ne sort dans ce cas : la saisie ou le duel couvre toute la dette.</summary>
public enum PairDebtSettlement
{
    /// <summary>Rien n'est dû (ou la bande peut payer en or).</summary>
    None,

    /// <summary>La réserve de la bande couvre la dette : la paire prend du matériel.</summary>
    SeizeEquipment,

    /// <summary>La réserve ne suffit pas : duel avec le meneur, mort s'il perd.</summary>
    Duel,
}

/// <summary>Comment un paiement « Une Poignée d'Or » est réglé (voir <see cref="PairDebtRules.SettleInOrder"/>).</summary>
public enum PairDebtPayment { None, Gold, SeizeEquipment, Duel }

/// <summary>Règlement d'un paiement : la façon de payer et, en cas de saisie, les lignes de réserve perdues.</summary>
public sealed record PairDebtOutcome<T>(PairDebtPayment Payment, List<T> SeizedLines);

/// <summary>« Où est l'Argent ? » de la Fin de partie - sortie du ViewModel le 2026-11-14 (dernière règle d'écriture
/// de la Fin de partie qui n'était pas testée) : l'appli choisit la branche et les objets, le joueur ne fait que
/// constater.</summary>
public static class PairDebtRules
{
    /// <summary>Choix automatique : la valeur totale de la réserve couvre-t-elle la dette ?</summary>
    public static PairDebtSettlement Settle(int amountDue, int reserveValue) =>
        amountDue <= 0 ? PairDebtSettlement.None
        : reserveValue >= amountDue ? PairDebtSettlement.SeizeEquipment
        : PairDebtSettlement.Duel;

    /// <summary>Plusieurs paiements « Une Poignée d'Or » dans la même Fin de partie (un par personnage ou duo), réglés
    /// dans l'ordre : chacun est payé en or tant que la trésorerie suffit, sinon saisit du matériel dans ce qui reste de
    /// la réserve, sinon déclenche un duel. Un montant nul ou négatif ne demande rien.</summary>
    public static List<PairDebtOutcome<T>> SettleInOrder<T>(IReadOnlyList<int> amountsDue, int treasury, IEnumerable<T> reserve, Func<T, int> value)
    {
        var remainingReserve = reserve.ToList();
        var outcomes = new List<PairDebtOutcome<T>>();
        foreach (var amount in amountsDue)
        {
            if (amount <= 0)
            {
                outcomes.Add(new PairDebtOutcome<T>(PairDebtPayment.None, new List<T>()));
            }
            else if (amount <= treasury)
            {
                treasury -= amount;
                outcomes.Add(new PairDebtOutcome<T>(PairDebtPayment.Gold, new List<T>()));
            }
            else if (Settle(amount, remainingReserve.Sum(value)) == PairDebtSettlement.SeizeEquipment)
            {
                var seized = SelectSeizedLines(remainingReserve, value, amount);
                foreach (var line in seized) remainingReserve.Remove(line);
                outcomes.Add(new PairDebtOutcome<T>(PairDebtPayment.SeizeEquipment, seized));
            }
            else
            {
                outcomes.Add(new PairDebtOutcome<T>(PairDebtPayment.Duel, new List<T>()));
            }
        }
        return outcomes;
    }

    /// <summary>Objets saisis : les lignes de la réserve de la PLUS au MOINS chère, jusqu'à atteindre ou dépasser la
    /// dette - perdre le moins d'objets possible, quitte à dépasser un peu la somme (une Épée en Gromril à 60 CO pour
    /// une dette de 50 CO, jamais les 3 épées à 10 CO en plus). Une ligne part en entier (pas de pile partielle) ; à
    /// valeur égale, l'ordre de la réserve est conservé. Réserve trop pauvre : tout part sans atteindre la dette
    /// (ne se produit pas quand Settle a choisi SeizeEquipment).</summary>
    public static List<T> SelectSeizedLines<T>(IEnumerable<T> reserve, Func<T, int> value, int amountDue)
    {
        var seized = new List<T>();
        if (amountDue <= 0) return seized;

        var sum = 0;
        foreach (var line in reserve.OrderByDescending(value))
        {
            if (sum >= amountDue) break;
            seized.Add(line);
            sum += value(line);
        }
        return seized;
    }
}
