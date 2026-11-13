using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Tests;

public class WhatsNewTests
{
    private const string Changelog = """
        <main class="legal">
          <h1>Changelog</h1>
          <div class="changelog-entry">
            <h2>1.5</h2>
            <span class="date">Novembre 2026</span>
            <ul>
              <li>Formulaires en pages
                  plein écran.</li>
              <li>Site : <a href="https://mordheimledger.com/">mordheimledger.com</a> &amp; Codex.</li>
              <li>   </li>
            </ul>
          </div>
          <div class="changelog-entry">
            <h2>1.4</h2>
            <ul><li>Navigation plus fluide.</li></ul>
          </div>
        </main>
        """;

    [Theory]
    [InlineData("1.5.392", "1.5")]
    [InlineData("1.5.392.0", "1.5")]
    [InlineData("10.12", "10.12")]
    [InlineData("1", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void MajorMinor_KeepsTheFirstTwoNumbers(string? version, string? expected) =>
        Assert.Equal(expected, WhatsNew.MajorMinor(version));

    [Fact]
    public void ExtractItems_ReturnsThePlainTextBulletsOfThatVersionOnly()
    {
        var items = WhatsNew.ExtractItems(Changelog, "1.5");

        Assert.Equal(["Formulaires en pages plein écran.", "Site : mordheimledger.com & Codex."], items);
    }

    [Fact]
    public void ExtractItems_StopsAtTheEndOfTheDocumentForTheLastSection() =>
        Assert.Equal(["Navigation plus fluide."], WhatsNew.ExtractItems(Changelog, "1.4"));

    [Theory]
    [InlineData("1.6")]
    [InlineData("1")]
    public void ExtractItems_UnknownVersion_IsEmpty(string version) =>
        Assert.Empty(WhatsNew.ExtractItems(Changelog, version));

    [Fact]
    public void ExtractItems_NoChangelog_IsEmpty() => Assert.Empty(WhatsNew.ExtractItems(null, "1.5"));

    [Theory]
    [InlineData(null, "1.5", 3, true)]   // installée avant ce dialog : une mise à jour
    [InlineData("1.4", "1.5", 3, true)]
    [InlineData("1.5", "1.5", 3, false)] // déjà vue
    [InlineData("1.4", "1.5", 0, false)] // rien dans le changelog pour cette version
    [InlineData("1.4", null, 3, false)]
    public void ShouldShow_OnlyForANewVersionWithSomethingToSay(string? lastSeen, string? current, int count, bool expected) =>
        Assert.Equal(expected, WhatsNew.ShouldShow(lastSeen, current, count));
}
