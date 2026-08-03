using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
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

    private static Page CurrentPage => Application.Current!.Windows[0].Page!;

    protected Task<bool> ConfirmDeleteAsync(string itemName) =>
        ConfirmAsync(Loc["DialogDelete"], string.Format(Loc["DialogDeleteConfirm"], itemName));

    // IMPORTANT: reading result.Result after a popup was dismissed by tapping outside THROWS an
    // InvalidOperationException for value types (bool, int...) — the toolkit can't represent "null"
    // there. Always check WasDismissedByTappingOutsideOfPopup first.
    protected async Task<bool> ConfirmAsync(string title, string message)
    {
        var popup = new ConfirmDialog(new ConfirmDialogViewModel(title, message, Loc["DialogYes"], Loc["DialogNo"]));
        var result = await CurrentPage.ShowPopupAsync<bool>(popup, new PopupOptions { CanBeDismissedByTappingOutsideOfPopup = true }, CancellationToken.None);
        return !result.WasDismissedByTappingOutsideOfPopup && result.Result is true;
    }

    protected Task ShowErrorAsync(Exception ex)
    {
        var popup = new ConfirmDialog(new ConfirmDialogViewModel(Loc["ErrorTitle"], ex.Message, Loc["DialogOk"], null));
        return CurrentPage.ShowPopupAsync<bool>(popup, new PopupOptions { CanBeDismissedByTappingOutsideOfPopup = true }, CancellationToken.None);
    }

    protected Task ShowInfoAsync(string title, string message)
    {
        var popup = new ConfirmDialog(new ConfirmDialogViewModel(title, message, Loc["DialogOk"], null));
        return CurrentPage.ShowPopupAsync<bool>(popup, new PopupOptions { CanBeDismissedByTappingOutsideOfPopup = true }, CancellationToken.None);
    }

    protected async Task<string?> ShowPromptAsync(string title, string message, string placeholder = "", string initialValue = "")
    {
        var popup = new PromptDialog(new PromptDialogViewModel(title, message, placeholder, initialValue, Loc["DialogYes"], Loc["DialogNo"]));
        var result = await CurrentPage.ShowPopupAsync<string?>(popup, new PopupOptions { CanBeDismissedByTappingOutsideOfPopup = true }, CancellationToken.None);
        return result.WasDismissedByTappingOutsideOfPopup ? null : result.Result;
    }

    protected async Task<TResult?> ShowDialogAsync<TResult>(Popup<TResult> popup)
    {
        var result = await CurrentPage.ShowPopupAsync<TResult>(popup, new PopupOptions { CanBeDismissedByTappingOutsideOfPopup = true }, CancellationToken.None);
        return result.WasDismissedByTappingOutsideOfPopup ? default : result.Result;
    }

    protected async Task<string?> ShowActionSheetAsync(string title, params string[] options)
    {
        var index = await ShowActionSheetIndexAsync(title, options);
        return index >= 0 && index < options.Length ? options[index] : null;
    }

    protected async Task<int> ShowActionSheetIndexAsync(string title, params string[] options)
    {
        var popup = new ActionSheetDialog(new ActionSheetDialogViewModel(title, options, Loc["BtnCancel"]));
        var result = await CurrentPage.ShowPopupAsync<int>(popup, new PopupOptions { CanBeDismissedByTappingOutsideOfPopup = true }, CancellationToken.None);
        return result.WasDismissedByTappingOutsideOfPopup ? -1 : result.Result is int index ? index : -1;
    }
}
