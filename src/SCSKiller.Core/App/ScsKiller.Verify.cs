// Exo Shaders - unofficial fork of SCSKiller, based on SCSKiller (https://github.com/BlueHeisenberg/SCSKiller).
// Copyright (C) 2026 Exo Shaders contributors. GPL-3.0-or-later (see LICENSE, LICENSE-EXCEPTION.txt).
//
// The check that replaces a 5-minute test by hand: is a game's compile in the cache, and does it reach the game? It compiles a
// sample of the plan again and times each create. A create the cache serves costs under 20 ms, one it doesn't costs a compile.
// What it tells comes from comparing pipelines the game itself compiled in play with ones only a warm did: see VerifyCause.

using System.Globalization;
using SCSKiller.Core.Games;
using SCSKiller.Core.Planning;
using SCSKiller.Core.Warming;

namespace SCSKiller.Core.App;

public sealed partial class ScsKiller
{
    /// <summary>How many pipelines the check creates at most. At a miss's 150 ms of thread time, about a minute on its threads.</summary>
    public const int VerifySample = 1200;

    public const int VerifyThreads = 4;

    /// <summary>The whole check's limit, reading the sample's shaders included.</summary>
    public static readonly TimeSpan VerifyLimit = TimeSpan.FromMinutes(5);

    /// <summary>Fewer created than this says nothing: stopped, no result is kept.</summary>
    public const int VerifyMinimum = 40;

    int _verifying;

    /// <summary>Exo Shaders: a check runs (<see cref="VerifyAsync"/>).</summary>
    public bool Verifying => Volatile.Read(ref _verifying) != 0;

    string? VerifyBuild(Game g) => g.Version ?? ExeStamp(g);

    /// <summary>The last check says a compile doesn't reach this game, on this driver and build: compiling it again would
    /// replay everything for nothing. The game's own first launch fills what it reads. A new driver or build ends this.</summary>
    bool NeedsFirstLaunchRun(Game g, GameRecord r) =>
        r.Verify is { Reaches: false } v && v.DriverId == Snapshot().Id && v.Build == VerifyBuild(g);

    public const string NeedsFirstLaunchReason = "a compile doesn't reach this game on this driver: compiling again wouldn't help. Its first launch builds what it needs";

    /// <summary>Why the check can't run now; null when it can. On a driver that keeps compiled pipelines in D3DSCache the warm
    /// writes its own staged folder (the fold), so a sample would read as all misses.</summary>
    public string? VerifyRefusal(string gameId)
    {
        var s = Find(gameId);
        if (GameFiles.IsRiot(s.Game)) return RiotReason;
        var rec = Store.LoadGame(gameId);
        if (rec.WarmedAt == null || rec.Plan is not { } plan) return "no compile to check yet";
        if (plan.Platform == Vulkan.VulkanPlan.Platform || s.Engine == null) return "the check reads DirectX 12 compiles only";
        if (KeepsD3DSCache()) return "this driver keeps compiled shaders in D3DSCache, where a sample can't be read back";
        if (rec.WarmedDriverId != Snapshot().Id) return "the compile was for another driver";
        if (BlockingMod(s.Game) != null) return "a mod changes this game's pipelines";
        if (Games.Any(x => x.Playing) || GameRunning(s.Game)) return "a game is running";
        lock (_lock) if (_current != null || _running) return "a compile is running";
        return Verifying ? "a check is running" : (_warmer as Warmer)?.Refusal(s.Game);
    }

    /// <summary>Compiles at most <see cref="VerifySample"/> of the plan's pipelines at idle priority, half of them ones the game
    /// compiled in play, within <see cref="VerifyLimit"/>, and keeps what it found on the game's record. Nothing else of the
    /// record changes: this is no compile. Stops gracefully on <paramref name="ct"/>, a game starting or a compile starting.
    /// Null when it stopped too early to say. Throws when <see cref="VerifyRefusal"/> does.</summary>
    public async Task<VerifyRecord?> VerifyAsync(string gameId, CancellationToken ct = default)
    {
        if (VerifyRefusal(gameId) is { } why) throw new InvalidOperationException(why);
        if (Interlocked.Exchange(ref _verifying, 1) != 0) throw new InvalidOperationException("a check is running");
        var s = Find(gameId);
        var (g, rec) = (s.Game, Store.LoadGame(gameId));
        var work = Path.Combine(Store.GameDir(gameId), "verify-work");
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(VerifyLimit);
        try
        {
            Directory.CreateDirectory(work);
            var snap = Snapshot();
            var exeDir = Path.GetDirectoryName(g.ExePath)!;
            await Lowered(VerifyThreads, true, () =>
            {
                var played = PlayedPairs([RecordingPath(gameId), .. Foreign(exeDir) ? Array.Empty<string>() : [Path.Combine(exeDir, "scskiller.db")]]);
                var small = rec.Plan! with { FilePath = Path.Combine(work, "sample.bin") };
                PlanFile.Write(small, SampleOf(PlanFile.Read(rec.Plan!.FilePath).Records, played, VerifySample));
                _planner.Materialize(small, g, s.Engine!, _reader, null, Path.Combine(work, "w"), limit.Token);
                return 0;
            });
            var items = CompiledItems.Numbered([Path.Combine(work, "w", "scskiller.db"), Path.Combine(work, "w", "scskiller_gen.db")]);
            if (items.Count == 0) throw new InvalidOperationException("the plan has no pipelines to sample");
            var played2 = PlayedPairs([RecordingPath(gameId), .. Foreign(exeDir) ? Array.Empty<string>() : [Path.Combine(exeDir, "scskiller.db")]]);
            var options = new WarmOptions(Math.Min(VerifyThreads, Math.Max(1, Settings.BackgroundThreads)), WarmPriority.Idle, 0, CompileMemoryGB(Settings) * 1024,
                snap.Id != null && rec.CrashKeys.Count > 0 && CrashKeysCurrent(rec, snap) ? [.. rec.CrashKeys] : null, 0, false, null, CompileCap(gameId), false,
                OperatingSystem.IsWindows() ? D3DSCache.Delivered(g.ExePath)?.Stamp : null, KnownKeysFor(rec, snap.Id, false, g.Name));
            var staged = g with { ExePath = Path.Combine(exeDir, WarmExeName(g, rec)) };
            IWarmRun run;
            if (_warmer is Warmer real)
            {
                var timed = real.Timed();
                (timed.Log, timed.Ags, timed.Aftermath) = (Log, x => AgsFor(x, rec, recover: true), x => AftermathOf(x, rec, s.Engine).Init);
                run = timed.Start(staged, Path.Combine(work, "w"), options, null, snap.Gpu);
            }
            else run = _warmer.Start(staged, Path.Combine(work, "w"), options, null);
            Log?.Report($"{g.Name}: checking the compile with a sample of {items.Count:N0} pipelines");
            while (!run.Completion.IsCompleted)
            {
                bool compiling;
                lock (_lock) compiling = _current != null;
                if (limit.IsCancellationRequested || compiling || Games.Any(x => x.Playing) || GameRunning(g)) run.Stop();
                await Task.WhenAny(run.Completion, Task.Delay(1000, CancellationToken.None));
            }
            var result = await run.Completion;
            var times = Path.Combine(Path.GetDirectoryName(result.LogPath) is { Length: > 0 } stage ? stage : Path.Combine(work, "w", "stage"), "scskiller_warm_times.csv");
            var (hits, created, playedHits, playedCreated) = ReadTimes(times, items, played2);
            if (created < VerifyMinimum)
            {
                Log?.Report($"{g.Name}: the check stopped too early to say ({created:N0} pipelines timed)");
                return null;
            }
            var rate = (double)hits / created;
            double? playedRate = playedCreated > 0 ? (double)playedHits / playedCreated : null;
            double? unplayedRate = created > playedCreated ? (double)(hits - playedHits) / (created - playedCreated) : null;
            var cause = GameDiagnostics.Cause(playedRate, unplayedRate);
            var (_, median, known, _, _, _) = GameDiagnostics.Read(Path.Combine(SessionDir(g), "scskiller_creates.csv"), Path.GetFileName(g.ExePath), rec.WarmedAt, ExeGone,
                OwnDriver == GpuVendor.Nvidia ? SessionLog.ReadRayQueryKeys(RayQueryKeysPath(gameId)) : null);
            var found = new VerifyRecord(DateTimeOffset.Now, snap.Id, VerifyBuild(g), created, Math.Round(rate, 3), Round(playedRate), Round(unplayedRate), GameDiagnostics.Reaches(cause, median, known), cause);
            AppStore.Locked(Path.Combine(Store.GameDir(gameId), "state.json"), () =>
            {
                var r = Store.LoadGame(gameId);
                r.Verify = found;
                Store.SaveGame(gameId, r);
            });
            Log?.Report($"{g.Name}: check: {hits:N0} of {created:N0} served from the cache (played {Text(playedRate)}, unplayed {Text(unplayedRate)}), cause {cause ?? "unclear"}, reaches {found.Reaches?.ToString() ?? "not judged"}");
            Refresh(g);
            return found;
        }
        finally
        {
            Volatile.Write(ref _verifying, 0);
            try { Directory.Delete(work, true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log?.Report($"{g.Name}: the check's folder stays: {e.Message}"); }
        }

        static double? Round(double? v) => v is { } x ? Math.Round(x, 3) : null;
        static string Text(double? v) => v is { } x ? x.ToString("P0", CultureInfo.InvariantCulture) : "none";
    }

    bool ExeGone(string exe) => !ProcessNames().Contains(Path.GetFileNameWithoutExtension(exe));

    /// <summary>Each shader with the root signature the game created it on, from its recordings: NVIDIA keys on the two.</summary>
    internal static HashSet<string> PlayedPairs(IEnumerable<string> recordings)
    {
        var pairs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in recordings.Where(File.Exists).SelectMany(PsoDb.Read))
            if (r.Tag is 'G' or 'C' or 'S')
                try { var p = PsoDb.Parse(r); foreach (var stage in p.Stages.Values) pairs.Add(stage + "|" + p.Rs); }
                catch (Exception e) when (e is InvalidDataException or ArgumentException or KeyNotFoundException) { }
        return pairs;
    }

    /// <summary>Every stage of the item with its root signature is in <paramref name="pairs"/>: the game compiled it, whatever else it was paired with.</summary>
    internal static bool Played(PsoDb.Rec r, IReadOnlySet<string> pairs)
    {
        try
        {
            var (rs, stages) = r.Tag == 'P' ? (PsoDb.ParseItem(r.Payload).Rs, PsoDb.ParseItem(r.Payload).Stages.Values) : (PsoDb.Parse(r).Rs, PsoDb.Parse(r).Stages.Values);
            return stages.Count > 0 && stages.All(stage => pairs.Contains(stage + "|" + rs));
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or KeyNotFoundException) { return false; }
    }

    /// <summary>A plan's records for a sample of at most <paramref name="max"/> of its pipelines, evenly over the plan so no one
    /// part decides: half ones the game played while there are as many, with the root signatures, templates and NVAPI state they
    /// need. Ray tracing and DirectX 11 stay out: their creates cost otherwise.</summary>
    internal static List<PsoDb.Rec> SampleOf(IEnumerable<PsoDb.Rec> plan, IReadOnlySet<string> played, int max)
    {
        var body = plan.ToList();
        var pipelines = body.Where(r => r.Tag is 'P' or 'G' or 'C' or 'S').ToList();
        var yes = pipelines.Where(r => Played(r, played)).ToList();
        var no = pipelines.Where(r => !Played(r, played)).ToList();
        var fromPlayed = Math.Min(yes.Count, Math.Max(max / 2, max - no.Count));
        var kept = Spaced(yes, fromPlayed).Concat(Spaced(no, Math.Min(no.Count, max - fromPlayed))).ToHashSet();
        var needed = kept.Where(r => r.Tag == 'P').Select(r => PsoDb.ParseItem(r.Payload).Template).ToHashSet(StringComparer.Ordinal);
        var keys = kept.Select(r => r.Key).Concat(needed).ToHashSet(StringComparer.Ordinal);
        return [.. body.Where(r => r.Tag == 'B' || kept.Contains(r) || r.Tag is 'G' or 'C' or 'S' && needed.Contains(r.Key)
            || r.Tag == 'N' && Community.NvTarget(r) is { } t && keys.Contains(t))];
    }

    static IEnumerable<T> Spaced<T>(List<T> from, int n)
    {
        for (var i = 0; i < n; i++) yield return from[(int)((long)i * from.Count / n)];
    }

    /// <summary>scskiller_warm_times.csv, "item,ms,ok" (an item's last line counts): what was created, what took under
    /// <see cref="UnreachedMedianMs"/>, and the same of those the game played.</summary>
    internal static (long Hits, long Created, long PlayedHits, long PlayedCreated) ReadTimes(string file, IReadOnlyList<PsoDb.Rec> items, IReadOnlySet<string> played)
    {
        var last = new Dictionary<long, (double Ms, int Ok)>();
        if (File.Exists(file))
            foreach (var line in File.ReadLines(file))
                if (line.Split(',') is [var i, var m, var o] && long.TryParse(i, CultureInfo.InvariantCulture, out var item)
                    && double.TryParse(m, CultureInfo.InvariantCulture, out var ms) && int.TryParse(o, CultureInfo.InvariantCulture, out var ok))
                    last[item] = (ms, ok);
        var (hits, created, playedHits, playedCreated) = (0L, 0L, 0L, 0L);
        foreach (var (item, (ms, ok)) in last)
        {
            if (ok != 1 || item < 0 || item >= items.Count) continue;
            var hit = ms < UnreachedMedianMs;
            var isPlayed = Played(items[(int)item], played);
            (hits, created) = (hits + (hit ? 1 : 0), created + 1);
            if (isPlayed) (playedHits, playedCreated) = (playedHits + (hit ? 1 : 0), playedCreated + 1);
        }
        return (hits, created, playedHits, playedCreated);
    }

    /// <summary>The numbers of a game's last launch and a plain verdict on its compile, from what's on this PC. Reads the game's
    /// session data only; a Riot game's never.</summary>
    public GameDiagnosticsReport Diagnose(string gameId)
    {
        var s = Find(gameId);
        var g = s.Game;
        var rec = Store.LoadGame(gameId);
        if (GameFiles.IsRiot(g)) return new(null, null, 0, null, WarmVerdict.NoData, null, false, false);
        var rayQuery = OwnDriver == GpuVendor.Nvidia ? SessionLog.ReadRayQueryKeys(RayQueryKeysPath(gameId)) : null;
        var (last, median, known, coverage, over20, played) = GameDiagnostics.Read(Path.Combine(SessionDir(g), "scskiller_creates.csv"), Path.GetFileName(g.ExePath), rec.WarmedAt, ExeGone, rayQuery);
        if (rec.LastPlay is { } window && (played == null || window.To > played)) played = window.To;
        // a check of another driver or build says nothing of this compile
        var verify = rec.Verify is { } v && v.DriverId == Snapshot().Id && v.Build == VerifyBuild(g) ? v : null;
        return new(last, median, known, played, GameDiagnostics.Verdict(rec.WarmedAt != null, IsUnreached(rec), verify?.Reaches, median, known, coverage, over20), verify, NeedsFirstLaunchRun(g, rec), s.RecorderForeign);
    }
}
