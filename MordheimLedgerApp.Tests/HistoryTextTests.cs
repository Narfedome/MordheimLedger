using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Tests;

public class HistoryTextTests
{
    [Fact]
    public void Join_AddsAPeriodOnlyWhereASentenceHasNone()
    {
        var text = HistoryText.Join(["Résultat : Victoire.", "Hans a progressé : CT +1", "Otto promu Héros !"]);

        Assert.Equal("Résultat : Victoire. Hans a progressé : CT +1. Otto promu Héros !", text);
    }

    [Theory]
    [InlineData("Note (voir livre)")]
    [InlineData("« Ce gars est doué »")]
    [InlineData("Vraiment ?")]
    [InlineData("Et puis…")]
    public void Join_KeepsAnExistingFinalPunctuation(string sentence) =>
        Assert.Equal(sentence, HistoryText.Join([sentence]));

    [Fact]
    public void Join_SkipsBlankSentencesAndTrims() =>
        Assert.Equal("Un. Deux.", HistoryText.Join(["  Un ", "", "   ", "Deux."]));
}
