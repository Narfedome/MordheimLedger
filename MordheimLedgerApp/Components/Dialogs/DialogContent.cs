namespace MordheimLedgerApp.Components.Dialogs;

/// <summary>Base commune des 27 dialogs de l'appli - remplace CommunityToolkit.Maui.Views.Popup&lt;TResult&gt;
/// comme classe de base XAML. Un dialog n'est plus son propre popup natif : son contenu (le Border
/// "carte" et tout ce qu'il y a dedans) est hébergé à l'intérieur de l'unique DialogHostPopup géré par
/// DialogStack, qui réassigne Content à chaque push/pop au lieu d'empiler plusieurs Popup natifs (cause
/// du bug "impossible de revenir en arrière" avec CommunityToolkit.Maui sur WinUI - voir DialogStack).</summary>
public abstract class DialogContent<TResult> : ContentView
{
}
