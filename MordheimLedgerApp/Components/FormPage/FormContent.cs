namespace MordheimLedgerApp.Components.FormPage;

/// <summary>Base XAML des formulaires plein écran (création/modification), pendant de DialogContent pour les
/// dialogs : le contenu (en général un FormPageLayout) est posé dans une FormPage poussée sur la pile de
/// navigation Shell par FormNavigator. Son BindingContext reste un DialogViewModel&lt;TResult&gt; : même
/// Close/Cancel/détection des modifications non enregistrées que du temps où ces formulaires étaient des
/// dialogs, rien à réécrire côté ViewModel.</summary>
public abstract class FormContent<TResult> : ContentView
{
}
