using System.Collections.ObjectModel;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>Section de la grille de tuiles de SellEquipmentSelectorPage (picker "classique" de Vente, page
/// poussée modalement - voir SellEquipmentPickerService), groupée par source - "Réserve" en premier (stash
/// pré-partie + trouvailles d'Exploration, toutes deux IsFromStash) puis une section par guerrier porteur,
/// dans l'ordre où WarriorRows les liste (retour utilisateur 2026-09-21 - "un affichage à la codex, avec
/// la séparation par héros, avec la réserve tout en haut... c'est le principe d'homogénéisation des écrans
/// d'une appli"). Même idiome que EquipmentItemGroup (Bibliothèque/Place du Marché, groupée par catégorie) -
/// Name est l'en-tête affiché (CodexGroupHeaderStyle).</summary>
public class SellableEquipmentGroup : ObservableCollection<SellableEquipmentCandidate>
{
    public string Name { get; }

    public SellableEquipmentGroup(string name) => Name = name;
}
