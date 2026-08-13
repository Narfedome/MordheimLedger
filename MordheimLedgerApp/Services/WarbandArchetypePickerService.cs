using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Library.WarbandArchetypes;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Services;

public interface IWarbandArchetypePickerService
{
    Task<IReadOnlyList<WarbandArchetype>> PickWarbandArchetypesAsync();
    Task<WarbandArchetype> PickWarbandArchetypeAsync();
}

public class WarbandArchetypePickerService : IWarbandArchetypePickerService
{
    private readonly IServiceProvider _provider;

    public WarbandArchetypePickerService(IServiceProvider provider) => _provider = provider;

    public async Task<IReadOnlyList<WarbandArchetype>> PickWarbandArchetypesAsync()
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<WarbandArchetype>>();

        var navigationService = _provider.GetRequiredService<IWarbandArchetypePickerNavigationService>();
        navigationService.RegisterTaskSource(tcs);

        // Poussée nue (pas de NavigationPage) - voir PickerSelectorLayout pour le pourquoi.
        var page = _provider.GetRequiredService<WarbandArchetypeSelectorPage>();
        page.SelectionMode = SelectionMode.Multiple;

        // Filet de sécurité : si la modale est fermée sans passer par ClosePickerAsync (geste/bouton
        // retour), le TaskCompletionSource ne serait jamais résolu et l'appelant resterait bloqué.
        var window = Shell.Current.Window;
        void OnModalPopped(object? sender, ModalPoppedEventArgs e)
        {
            if (!ReferenceEquals(e.Modal, page))
                return;
            window.ModalPopped -= OnModalPopped;
            tcs.TrySetResult(Array.Empty<WarbandArchetype>());
        }
        window.ModalPopped += OnModalPopped;

        await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PushModalAsync(page), "WarbandArchetypePicker.Push");

        return await tcs.Task;
    }


    public async Task<WarbandArchetype> PickWarbandArchetypeAsync()
    {
        var tcs = new TaskCompletionSource<WarbandArchetype>();

        var navigationService = _provider.GetRequiredService<IWarbandArchetypePickerNavigationService>();
        navigationService.RegisterTaskSource(tcs);

        // Poussée nue (pas de NavigationPage) - voir PickerSelectorLayout pour le pourquoi.
        var page = _provider.GetRequiredService<WarbandArchetypeSelectorPage>();
        page.SelectionMode = SelectionMode.Single;

        // Filet de sécurité : si la modale est fermée sans passer par ClosePickerAsync (geste/bouton
        // retour), le TaskCompletionSource ne serait jamais résolu et l'appelant resterait bloqué.
        var window = Shell.Current.Window;
        void OnModalPopped(object? sender, ModalPoppedEventArgs e)
        {
            if (!ReferenceEquals(e.Modal, page))
                return;
            window.ModalPopped -= OnModalPopped;
            tcs.TrySetResult(new WarbandArchetype());
        }
        window.ModalPopped += OnModalPopped;

        await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PushModalAsync(page), "WarbandArchetypePicker.Push");

        return await tcs.Task;
    }
}
