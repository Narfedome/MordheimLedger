using MordheimLedgerApp.Components.FormPage;

namespace MordheimLedgerApp.Features.Lobby;

/// <summary>Salle d'attente d'une partie connectée (voir LobbyViewModel).</summary>
public partial class LobbyForm : FormContent<bool>
{
    public LobbyForm(LobbyViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
