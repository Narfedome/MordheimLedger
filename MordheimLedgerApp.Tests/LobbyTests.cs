using MordheimLedgerApp.Core.Lobby;

namespace MordheimLedgerApp.Tests;

/// <summary>Partie connectée (v2, lot 4a) : un hôte et ses invités sur cette machine (127.0.0.1), sans annonce UDP.</summary>
public class LobbyTests : IAsyncLifetime
{
    private LobbyHost _host = null!;
    private readonly List<LobbyClient> _clients = [];

    public Task InitializeAsync()
    {
        _host = LobbyHost.Start("Cyril", announce: false);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        foreach (var client in _clients) await client.DisposeAsync();
        await _host.DisposeAsync();
    }

    private LobbyAddress Address(string? code = null) => new("127.0.0.1", _host.Port, code ?? _host.Code);

    private async Task<LobbyClient> JoinAsync(string name)
    {
        var client = await LobbyClient.JoinAsync(Address(), name);
        _clients.Add(client);
        return client;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++) await Task.Delay(50);
        Assert.True(condition(), "état attendu non atteint");
    }

    private static LobbyWarbandSummary Summary(string name) => new(name, "Skavens", 9, 112);

    [Fact]
    public async Task Guests_Join_ShareTheirWarbands_AndLeaving_UpdatesEveryone()
    {
        var marc = await JoinAsync("Marc");
        var julie = await JoinAsync("Julie");
        await _host.SetWarbandAsync("{\"host\":1}", Summary("Les Roses Sanglantes"));
        await marc.SetWarbandAsync("{\"marc\":1}", Summary("Les Rats du Puits"));

        await WaitUntilAsync(() => julie.State.Packages.Count == 2 && julie.State.Players.Count == 3);
        Assert.Equal(["Cyril", "Marc", "Julie"], julie.State.Players.Select(p => p.Name));
        Assert.True(julie.State.Players[0].IsHost);
        Assert.Equal("Les Rats du Puits", julie.State.Players.Single(p => p.Id == marc.PlayerId).Warband!.Name);
        Assert.Equal("{\"marc\":1}", julie.State.Packages[marc.PlayerId]);
        Assert.Null(julie.State.Players.Single(p => p.Id == julie.PlayerId).Warband);

        await marc.LeaveAsync();
        await WaitUntilAsync(() => julie.State.Players.Count == 2 && _host.State.Players.Count == 2);
        Assert.False(julie.State.Packages.ContainsKey(marc.PlayerId));
    }

    [Fact]
    public async Task WrongCode_FullGame_AndStartedGame_AreRefused()
    {
        var wrong = await Assert.ThrowsAsync<LobbyJoinException>(() => LobbyClient.JoinAsync(Address("ABCDEF"), "Intrus"));
        Assert.Equal(LobbyCloseReason.WrongCode, wrong.Reason);

        await JoinAsync("Marc");
        await JoinAsync("Julie");
        await JoinAsync("Paul");
        var full = await Assert.ThrowsAsync<LobbyJoinException>(() => LobbyClient.JoinAsync(Address(), "Cinquième"));
        Assert.Equal(LobbyCloseReason.Full, full.Reason);

        await _clients[^1].LeaveAsync();
        await WaitUntilAsync(() => _host.State.Players.Count == 3);
        await _host.StartGameAsync();
        await WaitUntilAsync(() => _clients[0].State.Started);
        var late = await Assert.ThrowsAsync<LobbyJoinException>(() => LobbyClient.JoinAsync(Address(), "Retardataire"));
        Assert.Equal(LobbyCloseReason.AlreadyStarted, late.Reason);
    }

    [Fact]
    public async Task HostLeaving_ClosesTheGameForGuests()
    {
        var marc = await JoinAsync("Marc");
        var closed = new TaskCompletionSource<LobbyCloseReason>();
        marc.Closed += reason => closed.TrySetResult(reason);

        await _host.LeaveAsync();

        Assert.Equal(LobbyCloseReason.HostClosed, await closed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Address_QrText_AndTypedCode_AreRead()
    {
        var address = new LobbyAddress("192.168.1.23", 50123, "K7Q482");
        Assert.True(LobbyAddress.TryParseQr(address.ToQrText(), out var parsed));
        Assert.Equal(address, parsed);
        Assert.False(LobbyAddress.TryParseQr("MLX1:abc", out _));
        Assert.False(LobbyAddress.TryParseQr("MLL1:pas-une-ip:1:K7Q482", out _));

        Assert.Equal("K7Q482", LobbyAddress.NormalizeCode(" k7q-482 "));
        Assert.Null(LobbyAddress.NormalizeCode("K7Q48"));
        Assert.Null(LobbyAddress.NormalizeCode("K7Q480")); // 0 exclu (confusion avec O)
        Assert.Equal("K7Q-482", LobbyAddress.FormatCode("K7Q482"));
        Assert.Matches("^[A-Z2-9]{6}$", LobbyAddress.NewCode());

        Assert.True(LobbyAddress.IsPrivate(System.Net.IPAddress.Parse("192.168.43.1")));
        Assert.True(LobbyAddress.IsPrivate(System.Net.IPAddress.Parse("172.20.10.2")));
        Assert.False(LobbyAddress.IsPrivate(System.Net.IPAddress.Parse("100.64.3.7"))); // 4G (CGNAT)
    }
}
