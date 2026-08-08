using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Services;
using Xunit;

namespace MordheimLedgerApp.Tests;

/// <summary>Seeds one SQLite database once and shares it across every test in a class via
/// IClassFixture - the seed (7 common catalogs + 15 warbands) dominates each test's runtime, so
/// re-seeding per-[Fact] (the old DataServiceTests pattern) made the whole suite take ~12 minutes for
/// ~30 tests. Only safe for tests that don't mutate shared catalog/warband state - see
/// WarbandMutationTests for the ones that do (each still gets its own fresh database).</summary>
public class SeededDatabaseFixture : IAsyncLifetime
{
    private readonly string _dbPath;
    public AppDatabase Db { get; }
    public ILibraryService Library { get; }
    public IWarbandService Warbands { get; }

    public SeededDatabaseFixture()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mordheimledger-tests-{Guid.NewGuid()}.db3");
        Db = new AppDatabase(_dbPath);
        Library = new LibraryService(Db);
        Warbands = new WarbandService(Db);
    }

    public Task InitializeAsync() => Db.Initialization;

    public async Task DisposeAsync()
    {
        await Db.Connection.CloseAsync();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
