using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Features.Library.Spells;

namespace MordheimLedgerApp.Services;

public interface ISpellPickerService
{
    /// <summary>allowedMagicSchoolIds: only spells from these schools are selectable - the caller's
    /// WarbandArchetype.MagicSchools (see WarriorEditDialogViewModel.AddSpell), so a caster can only
    /// learn from the school(s) its band actually grants.</summary>
    Task<IReadOnlyList<Spell>> PickSpellsAsync(IReadOnlyList<int> allowedMagicSchoolIds);
}

public class SpellPickerService : ISpellPickerService
{
    private readonly IServiceProvider _provider;

    public SpellPickerService(IServiceProvider provider) => _provider = provider;

    public async Task<IReadOnlyList<Spell>> PickSpellsAsync(IReadOnlyList<int> allowedMagicSchoolIds)
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<Spell>>();

        var navigationService = _provider.GetRequiredService<ISpellPickerNavigationService>();
        navigationService.RegisterTaskSource(tcs);

        // Résolu manuellement (pas GetRequiredService<SpellSelectorPage>()) pour pouvoir poser le
        // filtre AllowedMagicSchoolIds sur le ViewModel avant que la page ne charge ses données.
        var viewModel = _provider.GetRequiredService<SpellViewModel>();
        viewModel.AllowedMagicSchoolIds = allowedMagicSchoolIds.ToList();
        var page = new SpellSelectorPage(viewModel);
        var modal = new NavigationPage(page);

        // Filet de sécurité : si la modale est fermée sans passer par ClosePickerAsync (geste/bouton
        // retour), le TaskCompletionSource ne serait jamais résolu et l'appelant resterait bloqué.
        var window = Shell.Current.Window;
        void OnModalPopped(object? sender, ModalPoppedEventArgs e)
        {
            if (!ReferenceEquals(e.Modal, modal))
                return;
            window.ModalPopped -= OnModalPopped;
            tcs.TrySetResult(Array.Empty<Spell>());
        }
        window.ModalPopped += OnModalPopped;

        await Shell.Current.Navigation.PushModalAsync(modal);

        return await tcs.Task;
    }
}
