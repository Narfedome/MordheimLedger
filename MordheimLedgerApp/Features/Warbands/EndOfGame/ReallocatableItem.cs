using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Warbands.CreateEdit;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Un objet réallocable - wrapper unifié (même idiome que SellableEquipmentCandidate) sur
/// EXACTEMENT une des trois sources possibles : un WarriorEquipment porté par un Héros déjà actif, une
/// ReserveLine de la réserve finale (BuildReserve, stade Final - la ligne n'est jamais modifiée,
/// seule sa clé sert à enregistrer un déplacement), ou un EquipmentPick brouillon porté par une nouvelle recrue
/// (WarriorNameSlot.Equipment). Carrier porte la référence stable (WarriorOutcomeRow/WarriorNameSlot,
/// jamais recréés pendant le wizard) permettant de retrouver la VRAIE collection à modifier au moment du
/// déplacement, même si ReallocationCarrier lui-même est recréé à chaque accès à
/// ReallocationCarriers - voir EndOfGamePageViewModel.Reallocation.cs's MoveReallocationItem.</summary>
public sealed class ReallocatableItem
{
    public WarriorEquipment? WarriorEquipmentSource { get; }
    public ReserveLine? ReserveLineSource { get; }
    public EquipmentPick? DraftPickSource { get; }

    public EquipmentItem Item { get; }
    public SpecialRule? MaterialRule { get; }
    public SpecialRule? BlessingRule { get; }
    public string Name { get; }
    public ReallocationCarrier Carrier { get; }

    public ReallocatableItem(WarriorEquipment source, ReallocationCarrier carrier)
    {
        WarriorEquipmentSource = source;
        Carrier = carrier;
        Item = source.Item;
        MaterialRule = source.MaterialRule;
        BlessingRule = source.BlessingRule;
        Name = source.NameDisplay;
    }

    public ReallocatableItem(ReserveLine source, ReallocationCarrier carrier)
    {
        ReserveLineSource = source;
        Carrier = carrier;
        Item = source.Item;
        MaterialRule = source.MaterialRule;
        Name = source.NameDisplay;
    }

    public ReallocatableItem(EquipmentPick source, ReallocationCarrier carrier)
    {
        DraftPickSource = source;
        Carrier = carrier;
        Item = source.Item;
        MaterialRule = source.MaterialRule;
        Name = source.Name;
    }

    /// <summary>Quantité réelle du lot déplacé - 1 pour un brouillon de recrue (EquipmentPick n'a pas de
    /// notion de quantité empilée), sinon la Quantity réelle de la ligne.</summary>
    public int Quantity => WarriorEquipmentSource?.Quantity ?? ReserveLineSource?.Quantity ?? 1;

    public int? FoundValueOverride => WarriorEquipmentSource?.FoundValueOverride ?? ReserveLineSource?.FoundValueOverride;
}

public enum ReallocationCarrierKind { ExistingHero, NewRecruit, Reserve }

/// <summary>Qui peut actuellement porter/recevoir un ReallocatableItem - recréé à chaque accès à
/// EndOfGamePageViewModel.ReallocationCarriers (jamais mis en cache, même principe que
/// DismissibleWarriorRows) pour refléter en direct les décisions des étapes précédentes (Renvoyer,
/// Recrutement) : ExistingHeroRow/NewRecruitSlot restent néanmoins des références STABLES tout au long
/// du wizard, donc valables même une fois ce wrapper "périmé".</summary>
public sealed class ReallocationCarrier
{
    public string Name { get; }
    public ReallocationCarrierKind Kind { get; }
    public WarriorOutcomeRow? ExistingHeroRow { get; }
    public WarriorNameSlot? NewRecruitSlot { get; }
    private readonly List<ReserveLine>? _reserve;

    public IEnumerable<ReallocatableItem> Items => Kind switch
    {
        ReallocationCarrierKind.ExistingHero => ExistingHeroRow!.Warrior.Equipment.Select(we => new ReallocatableItem(we, this)),
        ReallocationCarrierKind.NewRecruit => NewRecruitSlot!.Equipment.Select(p => new ReallocatableItem(p, this)),
        ReallocationCarrierKind.Reserve => _reserve!.Select(rl => new ReallocatableItem(rl, this)),
        _ => Enumerable.Empty<ReallocatableItem>()
    };

    private ReallocationCarrier(string name, ReallocationCarrierKind kind, WarriorOutcomeRow? hero, WarriorNameSlot? recruit, List<ReserveLine>? reserve)
    {
        Name = name;
        Kind = kind;
        ExistingHeroRow = hero;
        NewRecruitSlot = recruit;
        _reserve = reserve;
    }

    public static ReallocationCarrier ForHero(WarriorOutcomeRow row) => new(row.Name, ReallocationCarrierKind.ExistingHero, row, null, null);
    // slot.Name (pas DisplayLabel, qui retombe sur ArchetypeLabel générique pour une recrue neuve -
    // conçu pour l'étape Équipement AVANT que le nom ne soit saisi) : à l'étape Réallouer, plus tardive
    // que RecruitHeroDetail (déjà validée, obligatoirement remplie), le nom réel est toujours disponible.
    public static ReallocationCarrier ForRecruit(WarriorNameSlot slot) =>
        new(string.IsNullOrWhiteSpace(slot.Name) ? slot.ArchetypeLabel : slot.Name, ReallocationCarrierKind.NewRecruit, null, slot, null);
    public static ReallocationCarrier ForReserve(List<ReserveLine> reserve, string label) => new(label, ReallocationCarrierKind.Reserve, null, null, reserve);

    /// <summary>Deux instances désignent le même porteur RÉEL si elles pointent la même référence
    /// stable (ExistingHeroRow/NewRecruitSlot) ou sont toutes deux la Réserve - jamais une égalité
    /// d'instance sur ReallocationCarrier lui-même, recréé à chaque accès.</summary>
    public bool IsSameCarrierAs(ReallocationCarrier other) => Kind == other.Kind
        && ReferenceEquals(ExistingHeroRow, other.ExistingHeroRow)
        && ReferenceEquals(NewRecruitSlot, other.NewRecruitSlot);
}
