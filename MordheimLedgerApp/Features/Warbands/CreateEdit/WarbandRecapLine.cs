namespace MordheimLedgerApp.Features.Warbands.CreateEdit;

/// <summary>Une ligne de l'étape Récapitulatif du wizard de création (WarbandEditDialogViewModel.RecapLines) :
/// un Héros, un groupe d'Hommes de main ou un Franc-Tireur - Title = nom + type, Details = ce qu'il porte
/// (équipement, compétences, sorts, mutations, blessures, XP), vide s'il n'y a rien à lister.</summary>
public sealed record WarbandRecapLine(string Title, string Details)
{
    public bool HasDetails => Details.Length > 0;
}
