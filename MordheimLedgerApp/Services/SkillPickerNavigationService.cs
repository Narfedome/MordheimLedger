using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Components.Dialogs;

namespace MordheimLedgerApp.Services;

public interface ISkillPickerNavigationService
{
    void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<Skill>> tcs);
    Task ClosePickerAsync(IReadOnlyList<Skill> result);
}

public class SkillPickerNavigationService : ISkillPickerNavigationService
{
    private TaskCompletionSource<IReadOnlyList<Skill>>? _tcs;

    public void RegisterTaskSource(TaskCompletionSource<IReadOnlyList<Skill>> tcs) => _tcs = tcs;

    public async Task ClosePickerAsync(IReadOnlyList<Skill> result)
    {
        _tcs?.TrySetResult(result);

        if (Shell.Current.Navigation.ModalStack.Count > 0)
            await DialogNavigationGate.RunAsync(() => Shell.Current.Navigation.PopModalAsync(), "SkillPicker.Pop");
    }
}
