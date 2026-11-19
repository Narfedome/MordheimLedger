using System.Text.Json;

namespace MordheimLedgerApp.Core.Lobby;

/// <summary>Sortes de messages entre joueurs de la Fin de partie connectée (v2, lot 4b).</summary>
public static class LobbyEventKinds
{
    /// <summary>Vainqueur choisi (GameResultPayload), à tous.</summary>
    public const string Result = "result";

    /// <summary>Le propriétaire signale son guerrier capturé (CapturePayload), au capteur.</summary>
    public const string Capture = "capture";

    /// <summary>Le propriétaire retire sa déclaration (jet changé), au capteur.</summary>
    public const string CaptureCancel = "capture-cancel";

    /// <summary>Le capteur propose une rançon (RansomOfferPayload), au propriétaire.</summary>
    public const string RansomOffer = "ransom-offer";

    /// <summary>Le propriétaire paie ou refuse (RansomAnswerPayload), au capteur.</summary>
    public const string RansomAnswer = "ransom-answer";

    /// <summary>L'un des deux repasse ce captif en saisie à la table (CaptureIdPayload).</summary>
    public const string CaptureManual = "capture-manual";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T payload) => JsonSerializer.Serialize(payload, Options);

    public static T? Deserialize<T>(string payload) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(payload, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Vainqueur de la partie, null pour un match nul. Celui qui l'a choisi : LobbyEvent.From (« Choisi par Marc »).</summary>
public sealed record GameResultPayload(string? WinnerPlayerId);

public sealed record CapturePayload(string CaptureId, string WarriorName, string RoleName, string WarbandName);

public sealed record RansomOfferPayload(string CaptureId, int Amount);

public sealed record RansomAnswerPayload(string CaptureId, bool Accepted);

public sealed record CaptureIdPayload(string CaptureId);

/// <summary>Message à envoyer après un changement d'état local.</summary>
public sealed record LobbyOutgoing(string To, string Kind, string Payload);

public enum CaptureStatus
{
    /// <summary>Déclaré par le propriétaire, le capteur n'a pas encore proposé de rançon.</summary>
    AwaitingOffer,

    /// <summary>Rançon proposée, le propriétaire n'a pas encore répondu.</summary>
    Offered,

    /// <summary>Rançon payée : le guerrier revient chez son propriétaire, l'or chez le capteur.</summary>
    Accepted,

    /// <summary>Rançon refusée : le capteur décide du sort (vente, Zombie, sacrifice) chez lui ; le guerrier est perdu.</summary>
    Refused,

    /// <summary>Échange abandonné (« Décider à la table », joueur parti) : chacun saisit comme avant la partie connectée.</summary>
    Manual,
}

/// <summary>Un guerrier capturé, vu de l'un des deux appareils. OwnerWarriorId n'est connu que chez le propriétaire.</summary>
public sealed record Capture(string Id, string OwnerPlayerId, string CaptorPlayerId, string WarriorName, string RoleName,
    string WarbandName, CaptureStatus Status, int? Amount, int? OwnerWarriorId);

/// <summary>Captifs de la Fin de partie connectée (v2, lot 4b - maquette « Fin de partie connectée », variante A) : le
/// propriétaire déclare son guerrier tombé sur 61 « Capturé » et désigne le capteur, le capteur propose une rançon, le
/// propriétaire paie ou refuse (sur un refus, le capteur décide du sort chez lui, comme aujourd'hui). Chacun des deux peut
/// à tout moment repasser le captif en saisie à la table ; un joueur qui quitte la partie y fait passer tous ses échanges.
/// Une instance par appareil, état pur : chaque action locale renvoie le message à envoyer, chaque message reçu met l'état
/// à jour (Apply). Le réseau et l'interface restent dans l'appli (LobbySession).</summary>
public sealed class CaptureLedger(string me)
{
    private readonly Dictionary<string, Capture> _captures = new(StringComparer.Ordinal);

    public string Me { get; } = me;

    /// <summary>Mes guerriers capturés (je suis le propriétaire).</summary>
    public IReadOnlyList<Capture> Mine => _captures.Values.Where(c => c.OwnerPlayerId == Me).ToList();

    /// <summary>Mes prisonniers (je suis le capteur).</summary>
    public IReadOnlyList<Capture> Held => _captures.Values.Where(c => c.CaptorPlayerId == Me).ToList();

    public Capture? Find(string captureId) => _captures.GetValueOrDefault(captureId);

    public Capture? FindByWarrior(int warriorId) => Mine.FirstOrDefault(c => c.OwnerWarriorId == warriorId);

    /// <summary>Propriétaire : guerrier tombé sur 61, capturé par captorPlayerId. Remplace une déclaration précédente du
    /// même guerrier (autre capteur choisi) en la retirant chez l'ancien capteur.</summary>
    public IReadOnlyList<LobbyOutgoing> Declare(int warriorId, string captorPlayerId, string warriorName, string roleName, string warbandName)
    {
        var outgoing = new List<LobbyOutgoing>();
        if (FindByWarrior(warriorId) is { } previous)
        {
            if (previous.CaptorPlayerId == captorPlayerId && previous.Status != CaptureStatus.Manual) return outgoing;
            outgoing.AddRange(Cancel(warriorId));
        }
        var capture = new Capture(Guid.NewGuid().ToString("N"), Me, captorPlayerId, warriorName, roleName, warbandName,
            CaptureStatus.AwaitingOffer, null, warriorId);
        _captures[capture.Id] = capture;
        outgoing.Add(new LobbyOutgoing(captorPlayerId, LobbyEventKinds.Capture,
            LobbyEventKinds.Serialize(new CapturePayload(capture.Id, warriorName, roleName, warbandName))));
        return outgoing;
    }

    /// <summary>Propriétaire : le guerrier n'est plus capturé (jet changé) - la déclaration disparaît des deux côtés.</summary>
    public IReadOnlyList<LobbyOutgoing> Cancel(int warriorId)
    {
        if (FindByWarrior(warriorId) is not { } capture) return [];
        _captures.Remove(capture.Id);
        return capture.Status == CaptureStatus.Manual
            ? []
            : [new LobbyOutgoing(capture.CaptorPlayerId, LobbyEventKinds.CaptureCancel, LobbyEventKinds.Serialize(new CaptureIdPayload(capture.Id)))];
    }

    /// <summary>Capteur : rançon proposée (ou nouvelle proposition tant que le propriétaire n'a pas répondu).</summary>
    public IReadOnlyList<LobbyOutgoing> Offer(string captureId, int amount)
    {
        if (Find(captureId) is not { } capture || capture.CaptorPlayerId != Me
            || capture.Status is not (CaptureStatus.AwaitingOffer or CaptureStatus.Offered) || amount < 0)
            return [];
        _captures[captureId] = capture with { Status = CaptureStatus.Offered, Amount = amount };
        return [new LobbyOutgoing(capture.OwnerPlayerId, LobbyEventKinds.RansomOffer, LobbyEventKinds.Serialize(new RansomOfferPayload(captureId, amount)))];
    }

    /// <summary>Propriétaire : rançon payée ou refusée.</summary>
    public IReadOnlyList<LobbyOutgoing> Answer(string captureId, bool accepted)
    {
        if (Find(captureId) is not { Status: CaptureStatus.Offered } capture || capture.OwnerPlayerId != Me) return [];
        _captures[captureId] = capture with { Status = accepted ? CaptureStatus.Accepted : CaptureStatus.Refused };
        return [new LobbyOutgoing(capture.CaptorPlayerId, LobbyEventKinds.RansomAnswer, LobbyEventKinds.Serialize(new RansomAnswerPayload(captureId, accepted)))];
    }

    /// <summary>L'un ou l'autre : « Décider à la table ».</summary>
    public IReadOnlyList<LobbyOutgoing> GoManual(string captureId)
    {
        if (Find(captureId) is not { } capture || capture.Status == CaptureStatus.Manual) return [];
        _captures[captureId] = capture with { Status = CaptureStatus.Manual };
        var other = capture.OwnerPlayerId == Me ? capture.CaptorPlayerId : capture.OwnerPlayerId;
        return [new LobbyOutgoing(other, LobbyEventKinds.CaptureManual, LobbyEventKinds.Serialize(new CaptureIdPayload(captureId)))];
    }

    /// <summary>Message reçu d'un autre joueur. Vrai si l'état des captifs a changé.</summary>
    public bool Apply(LobbyEvent message)
    {
        switch (message.Kind)
        {
            case LobbyEventKinds.Capture when LobbyEventKinds.Deserialize<CapturePayload>(message.Payload) is { } p:
                _captures[p.CaptureId] = new Capture(p.CaptureId, message.From, Me, p.WarriorName, p.RoleName, p.WarbandName,
                    CaptureStatus.AwaitingOffer, null, null);
                return true;
            case LobbyEventKinds.CaptureCancel when LobbyEventKinds.Deserialize<CaptureIdPayload>(message.Payload) is { } p:
                return _captures.TryGetValue(p.CaptureId, out var cancelled) && cancelled.OwnerPlayerId == message.From
                    && _captures.Remove(p.CaptureId);
            case LobbyEventKinds.RansomOffer when LobbyEventKinds.Deserialize<RansomOfferPayload>(message.Payload) is { } p:
                return Update(p.CaptureId, c => c.CaptorPlayerId == message.From && c.Status is CaptureStatus.AwaitingOffer or CaptureStatus.Offered,
                    c => c with { Status = CaptureStatus.Offered, Amount = p.Amount });
            case LobbyEventKinds.RansomAnswer when LobbyEventKinds.Deserialize<RansomAnswerPayload>(message.Payload) is { } p:
                return Update(p.CaptureId, c => c.OwnerPlayerId == message.From && c.Status == CaptureStatus.Offered,
                    c => c with { Status = p.Accepted ? CaptureStatus.Accepted : CaptureStatus.Refused });
            case LobbyEventKinds.CaptureManual when LobbyEventKinds.Deserialize<CaptureIdPayload>(message.Payload) is { } p:
                return Update(p.CaptureId, c => c.OwnerPlayerId == message.From || c.CaptorPlayerId == message.From,
                    c => c with { Status = CaptureStatus.Manual });
            default:
                return false;
        }
    }

    /// <summary>Un joueur a quitté la partie : ses échanges en cours passent à la table.</summary>
    public bool PlayerLeft(string playerId)
    {
        var changed = false;
        foreach (var capture in _captures.Values.ToList())
            if ((capture.OwnerPlayerId == playerId || capture.CaptorPlayerId == playerId)
                && capture.Status is CaptureStatus.AwaitingOffer or CaptureStatus.Offered)
            {
                _captures[capture.Id] = capture with { Status = CaptureStatus.Manual };
                changed = true;
            }
        return changed;
    }

    private bool Update(string captureId, Func<Capture, bool> allowed, Func<Capture, Capture> change)
    {
        if (!_captures.TryGetValue(captureId, out var capture) || !allowed(capture)) return false;
        _captures[captureId] = change(capture);
        return true;
    }
}
