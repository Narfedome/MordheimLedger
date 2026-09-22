using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.DramatisPersonae.CreateEdit;

/// <summary>Read-only recap shown instead of DramatisPersonaDetailDialog whenever the tapped Dramatis
/// Persona is one half of a pair (Item.PairedWithDramatisPersonaId - Ulli &amp; Marquand today, see the
/// model's own doc) - 2026-09-01, user request: a single combined tile represents the pair everywhere it
/// matters (the "Personnage spécial" search picker hides Ulli entirely, see IsHiddenFromSearchPicker), so
/// its info button showing only ONE half's own stat line/equipment (whichever one happened to be tapped)
/// was misleading - the reader has no way to see the other half's profile at all. This dialog shows what
/// IS genuinely shared (Description, SpecialRules - both halves carry identical copies by convention, see
/// DramatisPersonae.json) plus two tappable "profile" chips (one per half, in Item-then-partner order) -
/// tapping either opens the OTHER dialog (DramatisPersonaDetailDialog, full stat line/equipment/skills)
/// for that specific persona. Deliberately does NOT show Item's own stat line/equipment/skills directly -
/// those belong to ONE half, showing them here would silently favor whichever half happened to be tapped.
///
/// Chip taps call DetailDialogService.ShowDramatisPersonaDetailDialogAsync, which is now ALWAYS the plain
/// profile (2026-09-01, user request - see its own doc) - safe to call directly on either half here.</summary>
public partial class DramatisPersonaPairDetailDialogViewModel : ReadOnlyDialogViewModel
{
    public DramatisPersona Item { get; }

    /// <summary>Item then its partner, in that order - both DramatisPersona objects already fully
    /// resolved (PairedWithDramatisPersona is populated by LibraryService.GetDramatisPersonaeAsync before
    /// this dialog is ever reachable). ChipView binds directly to DramatisPersona.Name, no wrapper type
    /// needed.</summary>
    public List<DramatisPersona> Profiles { get; }

    private readonly IDetailDialogService _detailDialogs;

    public DramatisPersonaPairDetailDialogViewModel(DramatisPersona item, IDetailDialogService detailDialogs)
    {
        Item = item;
        _detailDialogs = detailDialogs;
        Profiles = item.PairedWithDramatisPersona is { } partner
            ? new List<DramatisPersona> { item, partner }
            : new List<DramatisPersona> { item };
        Title = Profiles.Count == 2 ? $"{Profiles[0].Name} & {Profiles[1].Name}" : item.Name;
    }

    [RelayCommand]
    private Task ShowProfileDetail(DramatisPersona persona) => _detailDialogs.ShowDramatisPersonaDetailDialogAsync(persona);

    [RelayCommand]
    private Task ShowSpecialRuleDetail(SpecialRule rule) => ShowChipDetailAsync(rule.Name, rule.Description);
}
