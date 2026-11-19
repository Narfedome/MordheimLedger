using MordheimLedgerApp.Core.Lobby;

namespace MordheimLedgerApp.Tests;

/// <summary>Captifs de la Fin de partie connectée (v2, lot 4b) : registre de chaque appareil, messages passés à la main
/// de l'un à l'autre (le réseau est couvert par LobbyTests).</summary>
public class CaptureLedgerTests
{
    private readonly CaptureLedger _cyril = new("cyril");
    private readonly CaptureLedger _marc = new("marc");

    /// <summary>Remet à l'autre appareil les messages qu'un appareil vient de produire.</summary>
    private void Deliver(CaptureLedger from, IEnumerable<LobbyOutgoing> outgoing)
    {
        foreach (var message in outgoing)
        {
            var to = message.To == _cyril.Me ? _cyril : _marc;
            Assert.True(to.Apply(new LobbyEvent(from.Me, message.To, message.Kind, message.Payload)));
        }
    }

    [Fact]
    public void Ransom_Offered_ThenPaid_IsSeenTheSameWayOnBothDevices()
    {
        Deliver(_cyril, _cyril.Declare(42, "marc", "Ulrike Brandt", "Champion", "Les Roses Sanglantes"));
        var held = Assert.Single(_marc.Held);
        Assert.Equal((CaptureStatus.AwaitingOffer, "Ulrike Brandt", "cyril", (int?)null), (held.Status, held.WarriorName, held.OwnerPlayerId, held.OwnerWarriorId));

        Deliver(_marc, _marc.Offer(held.Id, 40));
        var mine = _cyril.FindByWarrior(42)!;
        Assert.Equal((CaptureStatus.Offered, (int?)40), (mine.Status, mine.Amount));

        Deliver(_cyril, _cyril.Answer(mine.Id, accepted: true));
        Assert.Equal(CaptureStatus.Accepted, _marc.Find(held.Id)!.Status);
        Assert.Equal(CaptureStatus.Accepted, _cyril.Find(held.Id)!.Status);
        Assert.Empty(_cyril.Held);
        Assert.Empty(_marc.Mine);
    }

    [Fact]
    public void Refusal_TableDecision_CancelAndChangedCaptor_AreSynchronised()
    {
        Deliver(_cyril, _cyril.Declare(42, "marc", "Ulrike Brandt", "Champion", "Les Roses Sanglantes"));
        var id = _cyril.FindByWarrior(42)!.Id;
        Deliver(_marc, _marc.Offer(id, 25));
        Deliver(_cyril, _cyril.Answer(id, accepted: false));
        Assert.Equal(CaptureStatus.Refused, _marc.Find(id)!.Status);
        Assert.Empty(_cyril.Answer(id, accepted: true)); // déjà répondu

        Deliver(_cyril, _cyril.Declare(7, "marc", "Klaus", "Jeune Loup", "Les Roses Sanglantes"));
        var klaus = _cyril.FindByWarrior(7)!.Id;
        Deliver(_marc, _marc.GoManual(klaus));
        Assert.Equal(CaptureStatus.Manual, _cyril.Find(klaus)!.Status);
        Assert.Empty(_marc.Offer(klaus, 10)); // plus d'échange une fois à la table

        // Jet changé : la déclaration disparaît chez le capteur.
        Deliver(_cyril, _cyril.Declare(9, "marc", "Hans", "Guerrier", "Les Roses Sanglantes"));
        Deliver(_cyril, _cyril.Cancel(9));
        Assert.Equal(2, _marc.Held.Count);

        // Autre capteur choisi : retirée chez l'ancien, déclarée chez le nouveau.
        var julie = new CaptureLedger("julie");
        var outgoing = _cyril.Declare(42, "julie", "Ulrike Brandt", "Champion", "Les Roses Sanglantes");
        Assert.Equal([LobbyEventKinds.CaptureCancel, LobbyEventKinds.Capture], outgoing.Select(o => o.Kind));
        Assert.True(julie.Apply(new LobbyEvent("cyril", "julie", outgoing[1].Kind, outgoing[1].Payload)));
        Assert.Single(julie.Held);
    }

    [Fact]
    public void PlayerLeaving_SendsTheirPendingExchangesToTheTable_AndForeignMessagesAreIgnored()
    {
        Deliver(_cyril, _cyril.Declare(42, "marc", "Ulrike Brandt", "Champion", "Les Roses Sanglantes"));
        var id = _cyril.FindByWarrior(42)!.Id;

        Assert.False(_cyril.Apply(new LobbyEvent("julie", "cyril", LobbyEventKinds.RansomOffer,
            LobbyEventKinds.Serialize(new RansomOfferPayload(id, 999))))); // pas le capteur
        Assert.False(_cyril.Apply(new LobbyEvent("marc", "cyril", "inconnu", "{}")));
        Assert.Equal(CaptureStatus.AwaitingOffer, _cyril.Find(id)!.Status);

        Assert.True(_cyril.PlayerLeft("marc"));
        Assert.Equal(CaptureStatus.Manual, _cyril.Find(id)!.Status);
    }
}
