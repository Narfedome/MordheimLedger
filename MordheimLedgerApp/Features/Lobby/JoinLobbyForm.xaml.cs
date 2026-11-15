using MordheimLedgerApp.Components.FormPage;

namespace MordheimLedgerApp.Features.Lobby;

/// <summary>Rejoindre une partie connectée (voir JoinLobbyViewModel).</summary>
public partial class JoinLobbyForm : FormContent<bool>
{
    public JoinLobbyForm(JoinLobbyViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
