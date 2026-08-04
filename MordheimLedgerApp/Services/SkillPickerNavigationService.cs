using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Services;

public interface ISkillPickerNavigationService
{
    void RegisterTaskSource(TaskCompletionSource<Skill?> tcs);
    Task ClosePickerAsync(Skill? result);
}

public class SkillPickerNavigationService : ISkillPickerNavigationService
{
    private TaskCompletionSource<Skill?>? _tcs;

    public void RegisterTaskSource(TaskCompletionSource<Skill?> tcs) => _tcs = tcs;

    public async Task ClosePickerAsync(Skill? result)
    {
        _tcs?.TrySetResult(result);

        if (Shell.Current.Navigation.ModalStack.Count > 0)
            await Shell.Current.Navigation.PopModalAsync();
    }
}
