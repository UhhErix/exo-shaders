using System.Diagnostics;
using SCSKiller.Core;
using SCSKiller.Core.App;
using SCSKiller.Core.Games;
using SCSKiller.Core.Planning;
using SCSKiller.Core.Warming;

namespace SCSKiller.Tests.Platform;

/// <summary>Exo Shaders' consent, diagnostics and check. Ways these could fail: a recorder goes in with nobody asked; turning it
/// off removes a file that isn't ours; two copies swap a recorder at every run; a launch with no #end never gets a verdict; a
/// later launch's speed (the game's own compile) reads as the warm helping; a check's measure lands on the wrong pipelines;
/// a game the warm misses is queued for a full replay again.</summary>
public partial class AppTests
{
    [Fact]
    public void Consent_grants_nothing_until_asked_and_a_damaged_file_grants_nothing()
    {
        var dir = Directory.CreateDirectory(Path.Combine(_root, "consent")).FullName;
        Assert.Equal(new ExoConsent(), ExoConsent.Load(dir));
        Assert.False(ExoConsent.Load(dir).RecorderAuto || ExoConsent.Load(dir).ShareUpload);
        new ExoConsent(RecorderAuto: true, ShareUpload: true).Save(dir);
        Assert.Equal(new ExoConsent(true, true), ExoConsent.Load(dir));
        File.WriteAllText(Path.Combine(dir, ExoConsent.File), "{ not json");
        Assert.Equal(new ExoConsent(), ExoConsent.Load(dir));
    }

    /// <summary>Off is the default: no game gets the recorder. One consent records every eligible game, never an anti-cheat one.
    /// One game can be on with it off. Off takes out what this copy placed and nothing else in the folder.</summary>
    [Fact]
    public async Task The_recorder_goes_in_only_with_consent_and_off_removes_only_its_own_files()
    {
        var plain = FakeGame("test:plain", "Plain");
        var other = FakeGame("test:other", "Other");
        var eac = FakeGame("test:eac", "Eac");
        Directory.CreateDirectory(Path.Combine(eac.InstallDir, "EasyAntiCheat"));
        File.WriteAllText(Path.Combine(plain.InstallDir, "version.dll"), "the game's own");
        var k = RecordKiller([plain, other, eac]);
        k.RecordOnlyWithConsent = true;
        k.Settings = k.Settings with { RecordAllGames = false };
        await k.ScanAsync(default);
        k.ReconcileRecorders();
        Assert.False(File.Exists(Dll(plain)) || File.Exists(Dll(other)) || File.Exists(Dll(eac)));

        k.SetRecorderOverride(other.Id, RecorderOverride.On);   // one game, with the global switch off
        Assert.True(ScsKiller.IsOurProxy(Dll(other)));
        Assert.False(File.Exists(Dll(plain)));
        k.SetRecorderOverride(other.Id, RecorderOverride.Off);
        Assert.False(File.Exists(Dll(other)));

        k.Settings = k.Settings with { RecordAllGames = true };
        k.ReconcileRecorders();
        Assert.True(ScsKiller.IsOurProxy(Dll(plain)));
        Assert.False(File.Exists(Dll(other)));   // its own off stays
        Assert.False(File.Exists(Dll(eac)));

        k.Settings = k.Settings with { RecordAllGames = false };
        k.ReconcileRecorders();
        Assert.False(File.Exists(Dll(plain)));
        Assert.False(File.Exists(Path.Combine(plain.InstallDir, "scskiller.ini")));
        Assert.Equal("the game's own", File.ReadAllText(Path.Combine(plain.InstallDir, "version.dll")));
    }

    /// <summary>The official app, in a portable copy, takes a game's recorder (it does when its Record all games is on). Exo's
    /// engine with consent leaves it: no marker, proxy or stamp changes, turning it off here removes nothing of that copy's,
    /// and only the user's own request takes it back, once: that copy taking it again doesn't start a swap at each run.</summary>
    [Fact]
    public async Task Another_copys_recorder_is_left_alone_and_only_a_request_takes_it_once()
    {
        var exo = Copy(null, [_game]);
        exo.RecordOnlyWithConsent = true;
        await exo.ScanAsync(default);
        exo.ReconcileRecorders(_game.Id);
        Assert.True(ScsKiller.IsOurProxy(Path.Combine(_exeDir, ScsKiller.Dll12)));

        var portable = Copy("portable", [_game]);
        await portable.ScanAsync(default);
        portable.ReconcileRecorders(_game.Id);
        var stamp = Path.Combine(_exeDir, ScsKiller.LedgerStamp);
        Assert.Equal(Path.GetFullPath(CopyLedger("portable")), File.ReadAllText(stamp));
        var theirs = Files(_game.InstallDir);

        ScsKiller.LedgerDir = CopyLedger(null);
        exo.RefreshGame(_game.Id);
        exo.ReconcileRecorders(_game.Id);
        Assert.Equal(theirs, Files(_game.InstallDir));
        Assert.True(exo.Games.Single().RecorderForeign);
        exo.SetRecorderOverride(_game.Id, RecorderOverride.Off);
        Assert.Equal(theirs, Files(_game.InstallDir));

        exo.TakeOverRecorder(_game.Id);
        Assert.False(File.Exists(stamp));   // the installed copy's recorders carry no marker
        Assert.False(exo.Games.Single().RecorderForeign);

        ScsKiller.LedgerDir = CopyLedger("portable");
        portable.RefreshGame(_game.Id);
        portable.ReconcileRecorders(_game.Id);
        Assert.Equal(Path.GetFullPath(CopyLedger("portable")), File.ReadAllText(stamp));
        var again = Files(_game.InstallDir);
        ScsKiller.LedgerDir = CopyLedger(null);
        exo.RefreshGame(_game.Id);
        exo.ReconcileRecorders(_game.Id);   // still "on" here: no second takeover
        Assert.Equal(again, Files(_game.InstallDir));
    }

    string Csv(params string[] lines)
    {
        var path = Path.Combine(_root, $"creates-{Guid.NewGuid():N}.csv");
        File.WriteAllLines(path, lines);
        return path;
    }

    /// <summary>Unreal terminates its own process, so a launch has no #end: it counts as ended once no process of its exe runs,
    /// not while one does. Before this the verdict waited for the next launch.</summary>
    [Fact]
    public void A_launch_with_no_end_marker_ends_once_its_exe_has_gone()
    {
        var at = DateTimeOffset.FromUnixTimeMilliseconds(1_000);
        var csv = Csv("#session,2000,PBZ.exe", "1.0,G,1,1,1.000,a,0,1,0", "2.0,G,1,1,90.000,b,0,1,0");
        LaunchCheck? First(Func<string, bool>? gone) => SessionLog.Read(csv, out _, out _, "PBZ.exe", at, 1, lastEnded: gone == null ? null : () => gone("PBZ.exe")).First;
        Assert.Null(First(null));
        Assert.Null(First(_ => false));
        var first = First(exe => exe == "PBZ.exe");
        Assert.Equal((1L, 1L), (first!.Hits, first.Compiles));
    }

    /// <summary>The numbers are the last launch's; what judges the warm is the first ended launch after it, without what an
    /// earlier launch created. PBZ's pattern: the first launch after the warm compiles the warmed pipelines, the second is fast.</summary>
    [Fact]
    public void Diagnostics_read_the_last_launch_and_judge_by_the_first_after_the_warm()
    {
        var lines = new List<string> { "#session,500,G.exe", "1.0,C,0,0,50.000,early,0,1,0", "#end,600,2.0" };
        lines.Add("#session,2000,G.exe");
        lines.Add("1.0,C,1,1,70.000,early,0,1,0");   // created before the warm too: not its measure
        for (var i = 0; i < 120; i++) lines.Add($"{i + 2}.0,C,1,1,140.000,k{i},0,1,0");
        lines.Add("#session,3000,G.exe");
        for (var i = 0; i < 120; i++) lines.Add($"{i}.0,C,1,1,8.000,k{i},0,1,0");
        lines.Add("200.0,C,0,1,5.000,tuple,0,1,0");
        lines.Add("201.0,S,0,0,60.000,cold,0,1,0");
        lines.Add("202.0,c,0,0,1.000,fromlib,0,1,0");
        lines.Add("203.0,R,1,1,30.000,so1,0,1,0");
        lines.Add("204.0,A,0,0,40.000,so2,0,1,0");
        var (last, median, known, coverage, over20, played) = GameDiagnostics.Read(Csv([.. lines]), "G.exe", DateTimeOffset.FromUnixTimeMilliseconds(1_000), _ => true);
        Assert.Equal((124L, 121L, 122L, 2L, 2L, 1L), (last!.Creates, last.Known, last.TupleKnown, last.Unknown, last.StateObjects, last.StateObjectsWarmed));
        Assert.Equal((8.0, 60.0), (last.KnownMedianMs, last.UnknownMedianMs));
        Assert.Equal(Math.Round(3.0 / 124, 3), last.Over20Share);
        Assert.False(last.Running);
        Assert.Equal((140.0, 120L), (median, known));
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(3000).AddMilliseconds(204), played);
        Assert.Equal(WarmVerdict.Missed, GameDiagnostics.Verdict(true, false, null, median, known, coverage, over20));

        var (running, _, _, _, _, _) = GameDiagnostics.Read(Csv("#session,3000,G.exe", "1.0,C,1,1,8.000,a,0,1,0"), "G.exe", null, _ => false);
        Assert.True(running!.Running);
        Assert.Equal((null, 0L), GameDiagnostics.Read(Csv("#session,3000,Other.exe", "1.0,C,1,1,8.000,a,0,1,0"), "G.exe", null) is var o ? (o.Last, 0L) : default);
    }

    [Theory]
    [InlineData(false, false, null, 8.0, 500, 0.95, 0.01, WarmVerdict.NoData)]    // no warm
    [InlineData(true, false, null, null, 0, null, null, WarmVerdict.NoData)]      // no launch judged
    [InlineData(true, false, null, 8.0, 50, 0.95, 0.01, WarmVerdict.NoData)]      // too few warmed pipelines to say
    [InlineData(true, false, null, 8.0, 500, 0.95, 0.01, WarmVerdict.Helped)]
    [InlineData(true, false, null, 8.0, 500, 0.76, 0.01, WarmVerdict.Partly)]     // a quarter of what it asked wasn't compiled
    [InlineData(true, false, null, 8.0, 500, 0.95, 0.30, WarmVerdict.Partly)]     // much still took long
    [InlineData(true, false, null, 146.0, 1808, 0.76, 0.60, WarmVerdict.Missed)]  // PBZ's first launch
    [InlineData(true, true, null, null, 0, null, null, WarmVerdict.Missed)]       // the engine's own verdict
    [InlineData(true, false, false, 8.0, 500, 0.95, 0.01, WarmVerdict.Missed)]    // the check's
    [InlineData(true, true, true, 146.0, 500, 0.95, 0.30, WarmVerdict.Partly)]    // the first launch missed, the check says it reaches now
    public void The_verdict_is_plain(bool warmed, bool unreached, bool? reaches, double? median, long known, double? coverage, double? over20, WarmVerdict want) =>
        Assert.Equal(want, GameDiagnostics.Verdict(warmed, unreached, reaches, median, known, coverage, over20));

    [Theory]
    [InlineData(0.95, 0.95, VerifyCause.None)]
    [InlineData(0.95, 0.05, VerifyCause.Retention)]
    [InlineData(0.05, 0.95, VerifyCause.Lookup)]
    [InlineData(0.05, 0.05, VerifyCause.CacheLost)]
    [InlineData(0.5, 0.95, null)]
    [InlineData(null, 0.95, VerifyCause.Unplayed)]
    [InlineData(null, 0.05, VerifyCause.CacheLost)]
    public void The_check_tells_retention_from_lookup(double? played, double? unplayed, string? want) =>
        Assert.Equal(want, GameDiagnostics.Cause(played, unplayed));

    static List<PsoDb.Rec> Pipelines(int n) => [.. Enumerable.Range(0, n).Select(i => new PsoDb.Rec('C', PsoDb.Compute(PsoDb.Zero, $"{i + 1:x40}")))];

    static byte[] DbOf(IEnumerable<PsoDb.Rec> records)
    {
        using var m = new MemoryStream();
        foreach (var r in records) PsoDb.Write(m, r.Tag, r.Payload);
        return m.ToArray();
    }

    [Fact]
    public void A_sample_is_bounded_spread_and_half_played()
    {
        var all = Pipelines(1000);
        var played = all.Take(100).SelectMany(r => PsoDb.Parse(r).Stages.Values.Select(s => s + "|" + PsoDb.Parse(r).Rs)).ToHashSet();
        var sample = ScsKiller.SampleOf(all, played, 100);
        Assert.Equal(100, sample.Count);
        Assert.Equal(50, sample.Count(r => ScsKiller.Played(r, played)));
        Assert.Contains(sample, r => all.IndexOf(r) > 900);   // not just the plan's start
        Assert.Equal(1000, ScsKiller.SampleOf(all, played, 5000).Count);
    }

    /// <summary>The whole check, the warm's process faked: what the game played takes a compile's time and what only the warm
    /// compiled is served, after a first launch that compiled what the warm did: keys differ. The result is kept, the game asks
    /// for its first launch instead of a compile, and nothing of the compile's own record moves.</summary>
    [Fact]
    public async Task The_check_records_that_a_compile_misses_and_a_full_replay_is_not_queued_again()
    {
        var plan = Pipelines(100);
        var warmer = new FakeWarmer();
        var k = Killer(new FakeReader(Unreal), new FakePlanner(genDb: DbOf(plan), records: plan), warmer, game: _game);
        k.ProcessNames = () => new HashSet<string>();
        await k.ScanAsync(default);
        await WarmOnce(k, _game.Id);
        k.Remove(_game.Id);
        var warmed = k.Store.LoadGame(_game.Id).WarmedAt;
        Assert.Null(k.VerifyRefusal(_game.Id));
        File.WriteAllBytes(Path.Combine(k.Store.GameDir(_game.Id), "recording.db"), DbOf(plan.Take(50)));
        var after = warmed!.Value.AddMinutes(1).ToUnixTimeMilliseconds();
        File.WriteAllLines(Path.Combine(_exeDir, "scskiller_creates.csv"), [$"#session,{after},{Path.GetFileName(_game.ExePath)}", .. Enumerable.Range(0, 150).Select(i => $"{i}.0,C,1,1,140.000,k{i},0,1,0")]);
        warmer.OnWork = work =>
        {
            Directory.CreateDirectory(Path.Combine(work, "stage"));
            File.WriteAllLines(Path.Combine(work, "stage", "scskiller_warm_times.csv"), Enumerable.Range(0, 100).Select(i => $"{i},{(i < 50 ? 140.0 : 2.0):0.0},1"));
        };
        var found = await k.VerifyAsync(_game.Id);
        Assert.Equal((100L, 0.5, 0.0, 1.0, false, VerifyCause.Lookup), (found!.Sampled, found.HitRate, found.PlayedHitRate!.Value, found.UnplayedHitRate!.Value, found.Reaches!.Value, found.Cause));
        var rec = k.Store.LoadGame(_game.Id);
        Assert.Equal(found, rec.Verify);
        Assert.Equal(warmed, rec.WarmedAt);
        Assert.False(k.Verifying);
        Assert.False(Directory.Exists(Path.Combine(k.Store.GameDir(_game.Id), "verify-work")));
        Assert.True(k.Games.Single().NeedsFirstLaunch);
        var d = k.Diagnose(_game.Id);
        Assert.Equal((WarmVerdict.Missed, true, 140.0, 150L), (d.Verdict, d.NeedsFirstLaunch, d.Last!.KnownMedianMs!.Value, d.Last.Creates));
        var started = warmer.Started.Count;
        k.Enqueue(_game.Id);
        k.Compile(_game.Id);
        Assert.Empty(k.Queue);
        Assert.Equal(started, warmer.Started.Count);
    }

    [Fact]
    public async Task The_check_stops_for_a_running_game_and_keeps_no_result_when_it_says_nothing()
    {
        var plan = Pipelines(10);
        var warmer = new FakeWarmer();
        var running = new HashSet<string>();
        var k = Killer(new FakeReader(Unreal), new FakePlanner(genDb: DbOf(plan), records: plan), warmer, game: _game);
        k.ProcessNames = () => running;
        await k.ScanAsync(default);
        Assert.NotNull(k.VerifyRefusal(_game.Id));   // nothing compiled yet
        await WarmOnce(k, _game.Id);
        k.Remove(_game.Id);
        running.Add(Path.GetFileNameWithoutExtension(_game.ExePath));
        Assert.Equal("a game is running", k.VerifyRefusal(_game.Id));
        running.Clear();
        Assert.Null(await k.VerifyAsync(_game.Id));   // the fake warm timed nothing
        Assert.Null(k.Store.LoadGame(_game.Id).Verify);
        Assert.False(k.Verifying);
        Assert.Throws<ArgumentException>(() => k.VerifyRefusal("no such game"));
    }

    /// <summary>Phantom Blade Zero as it measured: a warm served 99.8% of the sample at about a millisecond, played or not, while
    /// its first launch's warmed pipelines took a median 126 ms. A warm reads what a warm wrote, so the sample alone can't see
    /// the game look them up under other keys, the first launch does: no reach, cause lookup, and no recompile.</summary>
    [Fact]
    public async Task A_warm_served_while_the_first_launch_compiled_is_a_lookup_and_is_not_compiled_again()
    {
        var (k, warmer, plan) = await Compiled(1200);
        File.WriteAllBytes(Path.Combine(k.Store.GameDir(_game.Id), "recording.db"), DbOf(plan.Take(600)));
        FirstLaunch(k, 126);
        warmer.OnWork = work =>
        {
            Directory.CreateDirectory(Path.Combine(work, "stage"));
            File.WriteAllLines(Path.Combine(work, "stage", "scskiller_warm_times.csv"), Enumerable.Range(0, 1200).Select(i => $"{i},{(i >= 1198 ? 37.6 : 1.05):0.00},1"));
        };
        var found = await k.VerifyAsync(_game.Id);
        Assert.Equal((1200L, 0.998, 1.0, 0.997, false, VerifyCause.Lookup), (found!.Sampled, found.HitRate, found.PlayedHitRate!.Value, found.UnplayedHitRate!.Value, found.Reaches!.Value, found.Cause));
        Assert.True(k.Games.Single().NeedsFirstLaunch);
        var d = k.Diagnose(_game.Id);
        Assert.Equal((WarmVerdict.Missed, true), (d.Verdict, d.NeedsFirstLaunch));
        var started = warmer.Started.Count;
        k.Enqueue(_game.Id);
        k.Compile(_game.Id);
        Assert.Empty(k.Queue);
        Assert.Equal(started, warmer.Started.Count);
    }

    /// <summary>A verdict stored when a served sample meant the compile reaches the game is judged again from its rates on the next
    /// read, and kept: Phantom Blade Zero's stored reaches true, cause none. A launch since judges one that had none to go by, and a
    /// first launch that used the compile keeps its yes.</summary>
    [Theory]
    [InlineData(126.0, true, false, VerifyCause.Lookup)]
    [InlineData(126.0, null, false, VerifyCause.Lookup)]
    [InlineData(5.0, null, true, VerifyCause.None)]
    [InlineData(5.0, true, true, VerifyCause.None)]
    public async Task A_stored_verdict_is_judged_again_on_the_next_read(double firstLaunchMs, bool? stored, bool want, string wantCause)
    {
        var (k, warmer, plan) = await Compiled(100);
        File.WriteAllBytes(Path.Combine(k.Store.GameDir(_game.Id), "recording.db"), DbOf(plan.Take(50)));
        warmer.OnWork = work =>
        {
            Directory.CreateDirectory(Path.Combine(work, "stage"));
            File.WriteAllLines(Path.Combine(work, "stage", "scskiller_warm_times.csv"), Enumerable.Range(0, 100).Select(i => $"{i},1.05,1"));
        };
        var measured = await k.VerifyAsync(_game.Id);   // no launch yet: served, not judged, with this driver and build
        Assert.Equal((null, VerifyCause.None), (measured!.Reaches, measured.Cause));
        var rec = k.Store.LoadGame(_game.Id);
        rec.Verify = measured with { Reaches = stored };
        k.Store.SaveGame(_game.Id, rec);
        FirstLaunch(k, firstLaunchMs);
        k.RefreshGame(_game.Id);
        var v = k.Store.LoadGame(_game.Id).Verify!;
        Assert.Equal((want, wantCause), (v.Reaches!.Value, v.Cause));
        Assert.Equal((measured.Sampled, measured.HitRate, measured.At), (v.Sampled, v.HitRate, v.At));   // what was measured stays
        Assert.Equal(!want, k.Games.Single().NeedsFirstLaunch);
    }

    [Theory]
    [InlineData(1.0, 0.997, 126.0, 1800, VerifyCause.Lookup, false)]   // Phantom Blade Zero
    [InlineData(1.0, 0.997, 5.0, 1800, VerifyCause.None, true)]
    [InlineData(1.0, 0.997, null, 0, VerifyCause.None, null)]          // no launch judged it yet
    [InlineData(1.0, 0.997, 126.0, 50, VerifyCause.None, null)]        // too few to judge
    [InlineData(null, 0.95, 126.0, 1800, VerifyCause.Lookup, false)]   // served, none of it played
    [InlineData(null, 0.95, null, 0, VerifyCause.Unplayed, null)]
    [InlineData(0.95, 0.05, 126.0, 1800, VerifyCause.Retention, false)]
    [InlineData(0.05, 0.95, null, 0, VerifyCause.Lookup, false)]
    [InlineData(0.05, 0.05, null, 0, VerifyCause.CacheLost, false)]
    [InlineData(0.5, 0.95, 126.0, 1800, null, null)]                   // the sample says nothing
    public void The_first_launch_has_the_last_word_on_a_served_sample(double? played, double? unplayed, double? median, long known, string? cause, bool? reaches) =>
        Assert.Equal((cause, reaches), GameDiagnostics.Judge(played, unplayed, median, known));

    async Task<(ScsKiller K, FakeWarmer Warmer, List<PsoDb.Rec> Plan)> Compiled(int pipelines)
    {
        var plan = Pipelines(pipelines);
        var warmer = new FakeWarmer();
        var k = Killer(new FakeReader(Unreal), new FakePlanner(genDb: DbOf(plan), records: plan), warmer, game: _game);
        k.ProcessNames = () => new HashSet<string>();
        await k.ScanAsync(default);
        await WarmOnce(k, _game.Id);
        k.Remove(_game.Id);
        return (k, warmer, plan);
    }

    /// <summary>The first launch after the warm: 150 warmed pipelines at <paramref name="ms"/>, its exe gone.</summary>
    void FirstLaunch(ScsKiller k, double ms)
    {
        var after = k.Store.LoadGame(_game.Id).WarmedAt!.Value.AddMinutes(1).ToUnixTimeMilliseconds();
        File.WriteAllLines(Path.Combine(_exeDir, "scskiller_creates.csv"), [$"#session,{after},{Path.GetFileName(_game.ExePath)}",
            .. Enumerable.Range(0, 150).Select(i => FormattableString.Invariant($"{i}.0,C,1,1,{ms:0.000},k{i},0,1,0"))]);
    }

    /// <summary>The check's warm runs under the game's exe name from its stage folder, which on Phantom Blade Zero stopped the
    /// check before its first create (0 timed): the watcher took it for the game playing, and GameRunning(g), a guard of the game's
    /// folder, counts a staged warm as the game. The real watcher and process checks run here, on a faked process list: the warm
    /// goes on and nothing plays, until the real game starts from its folder.</summary>
    [Fact]
    public async Task The_check_does_not_take_its_own_warm_for_the_game_but_stops_for_the_game()
    {
        var plan = Pipelines(10);
        var warmer = new ControlledWarmer();
        var k = Killer(new FakeReader(Unreal), new FakePlanner(genDb: DbOf(plan), records: plan), warmer, game: _game);
        var exe = Path.GetFileName(_game.ExePath);
        await k.ScanAsync(default);
        k.Enqueue(_game.Id);
        k.StartQueue();
        await warmer.WhenStarted();
        warmer.Run!.Finish();
        await k.WhenQueueIdle().WaitAsync(TimeSpan.FromSeconds(10));
        k.Remove(_game.Id);
        var staged = Path.Combine(k.Store.GameDir(_game.Id), ScsKiller.VerifyWork, "stage-4343-1", "Fake", "Binaries", "Win64", exe);
        List<(int Pid, int Parent, string Exe)> now = [];
        k.Processes = _ => now;
        k.ProcessNames = () => now.Select(p => Path.GetFileNameWithoutExtension(p.Exe)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        k.ProcessPath = pid => pid == 4343 ? staged : pid == 4242 ? _game.ExePath : null;
        k.WatchInterval = TimeSpan.FromMilliseconds(20);
        using var stop = new CancellationTokenSource();
        var watching = k.WatchGames(stop.Token);
        var check = k.VerifyAsync(_game.Id);
        await warmer.WhenStarted(2);
        now = [(4343, 1, exe)];
        await Task.Delay(TimeSpan.FromSeconds(2.5));   // past two turns of the check's loop and many of the watcher's
        Assert.False(warmer.Run!.Stopped);
        Assert.False(k.Games.Single().Playing);
        now = [(4343, 1, exe), (4242, 1, exe)];
        await Until(() => warmer.Run!.Stopped);
        Assert.Null(await check);
        stop.Cancel();
        await watching;
    }

    /// <summary>The whole check with the real warm on WARP, the real process list and the real watcher, as a game's check runs:
    /// created pipelines are timed, what was played and what wasn't both counted. Needs this checkout's proxy built.</summary>
    [Fact]
    public async Task The_check_times_a_real_warm_on_WARP_to_its_end()
    {
        if (OwnWarmExe() is not { } warmExe) return;
        var luid = Process.Start(new ProcessStartInfo(Path.Combine(Path.GetDirectoryName(warmExe)!, "selftest.exe"), "warpluid") { RedirectStandardOutput = true, Environment = { ["SCSKILLER_SELFTEST_UNARMED"] = "1" } })!
            .StandardOutput.ReadToEnd().Trim();
        var warp = new FakeVendor(Gpu with { AdapterLuid = Convert.ToInt64(luid, 16) });
        var rsBlob = Carved.Hlsl.RootSignature("DescriptorTable(UAV(u0))");
        var rs = PsoDb.Hex(System.Security.Cryptography.SHA1.HashData(rsBlob));
        var plan = new List<PsoDb.Rec>();
        using var db = new MemoryStream();
        PsoDb.WriteBlob(db, rs, rsBlob);
        foreach (var seed in Enumerable.Range(1, 300))
        {
            var cs = Carved.Hlsl.Cs(seed);
            var hash = PsoDb.Hex(System.Security.Cryptography.SHA1.HashData(cs));
            PsoDb.WriteBlob(db, hash, cs);
            plan.Add(new('C', PsoDb.Compute(rs, hash)));
        }
        foreach (var r in plan) PsoDb.Write(db, r.Tag, r.Payload);
        var k = Killer(new FakeReader(Unreal), new FakePlanner(genDb: db.ToArray(), records: plan), new Warmer(warp, warmExe), game: _game, vendor: warp);
        k.WatchInterval = TimeSpan.FromMilliseconds(50);
        await k.ScanAsync(default);
        k.Enqueue(_game.Id);
        k.StartQueue();
        await k.WhenQueueIdle().WaitAsync(TimeSpan.FromMinutes(2));
        Assert.Equal(QueueStage.Done, k.Queue.Single().Stage);
        k.Remove(_game.Id);
        File.WriteAllBytes(Path.Combine(k.Store.GameDir(_game.Id), "recording.db"), [.. db.ToArray()]);   // the game played them all
        using var stop = new CancellationTokenSource();
        var watching = k.WatchGames(stop.Token);
        var played = false;
        var check = k.VerifyAsync(_game.Id);
        while (!check.IsCompleted)
        {
            played |= k.Games.Single().Playing;
            await Task.WhenAny(check, Task.Delay(10));
        }
        var found = await check;
        stop.Cancel();
        await watching;
        Assert.False(played);
        Assert.NotNull(found);
        Assert.Equal(300L, found.Sampled);
        Assert.NotNull(found.PlayedHitRate);
        Assert.Equal(found, k.Store.LoadGame(_game.Id).Verify);
        Assert.False(Directory.Exists(Path.Combine(k.Store.GameDir(_game.Id), ScsKiller.VerifyWork)));
    }

    /// <summary>A retry's process times in a stage folder of its own: the items of every process count, a later line over an earlier.</summary>
    [Fact]
    public void The_check_counts_the_times_of_every_process_of_its_warm()
    {
        var (first, second) = (Path.Combine(_root, "first.csv"), Path.Combine(_root, "second.csv"));
        File.WriteAllLines(first, ["0,2.0,1", "1,140.0,1", "2,140.0,-1"]);
        File.WriteAllLines(second, ["2,3.0,1", "3,140.0,1"]);
        Assert.Equal((2L, 4L, 0L, 0L), ScsKiller.ReadTimes([first, second], Pipelines(4), new HashSet<string>()));
    }
}
