using SCSKiller.Core;
using SCSKiller.Core.App;

namespace SCSKiller.Tests.Platform;

// A driver update between a compile and its fold: what goes into the game's D3DSCache folder, what goes, what waits.
// Ways it could fail: the earlier driver's pipelines written into the game's cache for nothing (Beast of Reincarnation's
// 9.6 GB after 621.01 -> 621.12); this driver's compile deleted because the game's own db is still the earlier driver's;
// the game's own db touched; a fold that waited stuck because this driver's compiler isn't known before a warm on it.
public partial class AppTests
{
    static readonly string[] K = ["01", "02", "03"];

    /// <summary>A fold that waited across a driver update (the engine wasn't running, or the game was), the game not run since, so
    /// its own db is the earlier driver's too (Beast of Reincarnation's): the staged db, which no driver reads, is deleted, never
    /// written into the game's cache, and nothing waits. Before any warm on this driver, its compiler is known from what programs
    /// wrote since its install.</summary>
    [Fact]
    public async Task After_a_driver_update_a_waiting_compile_of_the_earlier_driver_is_deleted_not_merged()
    {
        var installed = DateTime.UtcNow.AddHours(-1);
        var (k, game, staged) = await WaitingFold(installed, GameCachesTests.Old, GameCachesTests.Old);
        FakeD3DSCache.Adapter(Path.Combine(_root, "D3DSCache", "0123abcd"), @"C:\Program Files\Other\other.exe", GameCachesTests.New, "0A");   // a program since

        await Fold(k);

        Assert.False(Directory.Exists(Path.GetDirectoryName(staged)));
        Assert.False(Directory.Exists(Path.Combine(k.Store.GameDir(_game.Id), "d3dscache")));   // nothing waits
        Assert.Equal([K[2]], FakeD3DSCache.Keys(game));   // the earlier driver's pipelines didn't go in
    }

    /// <summary>A waiting fold of this driver's compile still goes into the game's cache, as before.</summary>
    [Fact]
    public async Task A_waiting_compile_of_this_driver_still_goes_into_the_games_cache()
    {
        var (k, game, staged) = await WaitingFold(DateTime.UtcNow.AddHours(-1), GameCachesTests.New, GameCachesTests.New);

        await Fold(k);

        Assert.False(Directory.Exists(Path.GetDirectoryName(staged)));
        Assert.Equal([K[1], K[2]], FakeD3DSCache.Keys(game));
    }

    /// <summary>The game's own adapter db (<see cref="K"/>[2]) and one staged db (<see cref="K"/>[1], <see cref="K"/>[0] when it is
    /// <see cref="GameCachesTests.Old"/>, then written before <paramref name="installed"/>), waiting for their fold.</summary>
    async Task<(ScsKiller K, string Game, string Staged)> WaitingFold(DateTime installed, string gameCompiler, string stagedCompiler)
    {
        var d3ds = Path.Combine(_root, "D3DSCache");   // Killer: LocalAppData = _root
        var k = Killer(new FakeReader(Unreal), vendor: new FakeVendor(Gpu with { Vendor = GpuVendor.Nvidia }));
        k.DriverInstalledAt = () => installed;
        await k.ScanAsync(default);
        var earlier = stagedCompiler == GameCachesTests.Old;
        var exe = Path.Combine(k.Store.GameDir(_game.Id), "work", "stage-1-1", "Fake-Win64-Shipping.exe");
        var staged = FakeD3DSCache.Adapter(Path.Combine(d3ds, D3DSCache.FolderName(exe)), exe, stagedCompiler, earlier ? K[0] : K[1]);
        var game = FakeD3DSCache.Adapter(Path.Combine(d3ds, D3DSCache.FolderName(_game.ExePath)), _game.ExePath, gameCompiler, K[2]);
        foreach (var db in new[] { staged, game }.Where(_ => earlier)) File.SetLastWriteTimeUtc(db, installed.AddDays(-4));
        var rec = k.Store.LoadGame(_game.Id);
        rec.D3DSCacheSince = DateTime.UtcNow.AddMinutes(-10);
        k.Store.SaveGame(_game.Id, rec);
        return (k, game, staged);
    }

    /// <summary>The first poll folds what waits: done once the record no longer has it pending.</summary>
    static async Task Fold(ScsKiller k)
    {
        k.PollGames();
        var id = k.Games.Single().Game.Id;
        for (var t = System.Diagnostics.Stopwatch.StartNew(); k.Store.LoadGame(id).D3DSCacheSince != null && t.Elapsed < HangGuard;) await Task.Delay(50);
        Assert.Null(k.Store.LoadGame(id).D3DSCacheSince);
    }

    /// <summary>A compile on this driver while the game's own db is still the earlier driver's (it hasn't run since the update):
    /// the compile's db can't go in yet and waits for the game's next run, never deleted; the game's db isn't touched.</summary>
    [Fact]
    public async Task A_compile_on_this_driver_waits_while_the_games_db_is_the_earlier_drivers_and_is_never_deleted()
    {
        var d3ds = Path.Combine(_root, "D3DSCache");
        var work = "";
        var k = Killer(new FakeReader(Unreal), vendor: new FakeVendor(Gpu with { Vendor = GpuVendor.Nvidia }), warmer: new FakeWarmer(() =>
        {
            var exe = Path.Combine(work, "stage-10-1", "Fake-Win64-Shipping.exe");
            FakeD3DSCache.Adapter(Path.Combine(d3ds, D3DSCache.FolderName(exe)), exe, GameCachesTests.New, K[1]);
            return null;
        }));
        k.DriverInstalledAt = () => DateTime.UtcNow.AddHours(-1);
        work = Path.Combine(k.Store.GameDir(_game.Id), "work");
        var game = FakeD3DSCache.Adapter(Path.Combine(d3ds, D3DSCache.FolderName(_game.ExePath)), _game.ExePath, GameCachesTests.Old, K[0]);
        File.SetLastWriteTimeUtc(game, DateTime.UtcNow.AddDays(-4));
        await k.ScanAsync(default);
        k.Enqueue(_game.Id);
        k.StartQueue();
        await k.WhenQueueIdle().WaitAsync(HangGuard);

        var waiting = Directory.GetFiles(Path.Combine(k.Store.GameDir(_game.Id), "d3dscache"), FakeD3DSCache.AdapterName, SearchOption.AllDirectories);
        Assert.Equal([K[1]], FakeD3DSCache.Keys(Assert.Single(waiting)));
        Assert.Equal([K[0]], FakeD3DSCache.Keys(game));
        Assert.Equal(ScsKiller.D3DSCachePendingReason, k.Games.Single().StatusReason);
    }
}
