using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Tests;

/// <summary>Rappel « Soutenir le projet » après une Fin de partie.</summary>
public class SupportReminderTests
{
    private static (SupportReminderState State, List<int> Prompts) Play(SupportReminderState state, int games, SupportReminderChoice answer)
    {
        var prompts = new List<int>();
        for (var i = 0; i < games; i++)
        {
            (state, var show) = SupportReminder.GameCompleted(state);
            if (!show) continue;
            prompts.Add(state.CompletedGames);
            state = SupportReminder.Answer(state, answer);
        }
        return (state, prompts);
    }

    [Fact]
    public void FirstPrompt_AfterTheThirdGame_ThenEveryTenGames_WhenAnsweredLater()
    {
        var (_, prompts) = Play(SupportReminderState.Initial, 25, SupportReminderChoice.Later);
        Assert.Equal([3, 13, 23], prompts);
    }

    [Theory]
    [InlineData(SupportReminderChoice.Support)]
    [InlineData(SupportReminderChoice.Never)]
    public void SupportOrNever_StopsTheReminderForGood(SupportReminderChoice choice)
    {
        var (state, prompts) = Play(SupportReminderState.Initial, 40, choice);
        Assert.Equal([3], prompts);
        Assert.True(state.Stopped);
        Assert.Equal(40, state.CompletedGames);
    }
}
