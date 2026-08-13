using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp;

public abstract partial class BaseViewModel : ObservableObject
{
    public LocalizationService Loc => LocalizationService.Instance;
    public ThemeService Theme => ThemeService.Instance;

    // Cached per ViewModel instance (LoadingService is registered Transient): each page gets its own
    // loading indicator, so a long load on one tab doesn't show a spinner on another tab visited
    // meanwhile.
    private LoadingService? _loading;
    public LoadingService Loading =>
        _loading ??= IPlatformApplication.Current!.Services.GetRequiredService<LoadingService>();

    // Shell.Current d'abord - c'est l'ancre stable que les ~10 XxxPickerService utilisent déjà pour
    // leurs propres Push/PopModalAsync (voir DialogNavigationGate). Windows[0].Page n'est PAS un
    // équivalent fiable une fois le Shell actif : OnboardingViewModel le réassigne lui-même
    // (Application.Current.Windows[0].Page = _shell) au lancement, et rien ne garantit qu'il continue à
    // pointer vers le Shell une fois des pages modales empilées par-dessus - ancrer DialogStack dessus
    // pouvait faire pousser une dialog sur un contexte de navigation différent de celui utilisé par le
    // sélecteur ouvrant, causant exactement le symptôme observé (la dialog imbriquée s'ouvre comme une
    // page Shell à part - flèche retour sur Android - au lieu d'une carte superposée). Repli sur
    // Windows[0].Page uniquement pendant la fenêtre d'onboarding, avant que le Shell ne soit la page
    // active (OnboardingViewModel appelle déjà ShowActionSheetAsync à ce moment-là).
    private static Page CurrentPage => (Page?)Shell.Current ?? Application.Current!.Windows[0].Page!;

    protected Task<bool> ConfirmDeleteAsync(string itemName) =>
        ConfirmAsync(Loc["DialogDelete"], string.Format(Loc["DialogDeleteConfirm"], itemName));

    protected async Task<bool> ConfirmAsync(string title, string message)
    {
        var popup = new ConfirmDialog(new ConfirmDialogViewModel(title, message, Loc["DialogYes"], Loc["DialogNo"]));
        return await ShowDialogAsync(popup) is true;
    }

    protected Task ShowErrorAsync(Exception ex)
    {
        var popup = new ConfirmDialog(new ConfirmDialogViewModel(Loc["ErrorTitle"], ex.Message, Loc["DialogOk"], null));
        return ShowDialogAsync(popup);
    }

    protected Task ShowInfoAsync(string title, string message)
    {
        var popup = new ConfirmDialog(new ConfirmDialogViewModel(title, message, Loc["DialogOk"], null));
        return ShowDialogAsync(popup);
    }

    protected async Task<string?> ShowPromptAsync(string title, string message, string placeholder = "", string initialValue = "")
    {
        var popup = new PromptDialog(new PromptDialogViewModel(title, message, placeholder, initialValue, Loc["DialogYes"], Loc["DialogNo"]));
        return await ShowDialogAsync(popup);
    }

    /// <summary>Ouvre (ou pousse dans la pile existante, voir DialogStack) le dialog donné - un seul
    /// Popup natif CommunityToolkit.Maui reste jamais ouvert à la fois, quelle que soit la profondeur
    /// d'imbrication (dialog qui en ouvre un autre).</summary>
    protected Task<TResult?> ShowDialogAsync<TResult>(DialogContent<TResult> content) =>
        DialogStack.Instance.PushAsync(content, CurrentPage);

    protected async Task<string?> ShowActionSheetAsync(string title, params string[] options)
    {
        var index = await ShowActionSheetIndexAsync(title, options);
        return index >= 0 && index < options.Length ? options[index] : null;
    }

    protected Task<int> ShowActionSheetIndexAsync(string title, params string[] options)
    {
        var popup = new ActionSheetDialog(new ActionSheetDialogViewModel(title, options, Loc["BtnCancel"]));
        return ShowDialogAsync(popup);
    }

    /// <summary>Pre-built option list variant - lets a caller mix in non-selectable header rows.</summary>
    protected Task<int> ShowActionSheetIndexAsync(string title, IEnumerable<ActionSheetOption> options)
    {
        var popup = new ActionSheetDialog(new ActionSheetDialogViewModel(title, options, Loc["BtnCancel"]));
        return ShowDialogAsync(popup);
    }
}
