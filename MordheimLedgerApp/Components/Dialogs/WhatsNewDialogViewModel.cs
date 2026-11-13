using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Components.Dialogs;

/// <summary>Nouveautés d'une version (puces du changelog du site, voir <see cref="WhatsNewService"/>), avec un lien
/// vers toutes les versions. Rien à confirmer : OK ferme, comme le ✕ des autres récaps en lecture seule.</summary>
public partial class WhatsNewDialogViewModel : ReadOnlyDialogViewModel
{
    public string VersionText { get; }

    public IReadOnlyList<string> Items { get; }

    public WhatsNewDialogViewModel(string version, IReadOnlyList<string> items)
    {
        Title = Loc["WhatsNewTitle"];
        VersionText = string.Format(Loc["WhatsNewVersion"], version);
        Items = items;
    }

    [RelayCommand]
    private Task OpenAllVersions() => Launcher.OpenAsync(WhatsNewService.AllVersionsUri);
}
