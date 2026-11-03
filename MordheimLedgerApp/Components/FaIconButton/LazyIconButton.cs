using System.Windows.Input;

namespace MordheimLedgerApp.Components;

/// <summary>Petit bouton icône fantôme (GhostIconButtonStyle) qui n'existe que si sa commande est fournie -
/// pour les boutons optionnels des puces (⇄/✕ de ChipView, "+" de ChipListView/ChipItemView). Déclarés en
/// XAML avec IsVisible="{Binding XxxCommand, Converter=IsNotNull}", ils étaient construits (Grid + Border +
/// Image + Button, ~19 ms pièce sur Android) même quand la commande restait nulle, c'est-à-dire dans tous
/// les usages en lecture seule : une carte guerrier en créait plusieurs dizaines pour rien (2026-11-03).</summary>
internal static class LazyIconButton
{
    /// <summary>Crée le bouton à la première commande non nulle (via <paramref name="create"/>, qui le place
    /// dans son parent), puis le masque/réaffiche selon que la commande est nulle ou non.</summary>
    public static void Sync(ref FaIconButtonView? button, ICommand? command, Func<FaIconButtonView> create)
    {
        if (button is null)
        {
            if (command is null) return;
            button = create();
        }
        button.IsVisible = command is not null;
    }

    public static FaIconButtonView CreateGhost(string icon, double iconSize)
    {
        var button = new FaIconButtonView { Icon = icon, IconSize = iconSize };
        if (Application.Current?.Resources.TryGetValue("GhostIconButtonStyle", out var style) == true)
            button.Style = (Style)style;
        return button;
    }
}
