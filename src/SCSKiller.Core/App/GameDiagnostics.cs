// Exo Shaders - unofficial fork of SCSKiller, based on SCSKiller (https://github.com/BlueHeisenberg/SCSKiller).
// Copyright (C) 2026 Exo Shaders contributors. GPL-3.0-or-later (see LICENSE, LICENSE-EXCEPTION.txt).

using System.Globalization;

namespace SCSKiller.Core.App;

/// <summary>One launch's creates in numbers. <see cref="Creates"/>: everything the game asked the driver for, library loads
/// left out. <see cref="Known"/> (the exact key is in the last compile's list) is inside <see cref="TupleKnown"/> (its stages
/// and root signature are, which is what NVIDIA keys on); <see cref="Unknown"/> is neither. Medians are of PSOs only, each
/// pipeline's first create in this launch, RayQuery PSOs (a floor up to 60 ms even cached) left out. <see cref="Over20Share"/>:
/// of <see cref="Creates"/>, at or over <see cref="ScsKiller.UnreachedMedianMs"/>, where 3 ms counts too many hits as slow.</summary>
public sealed record LaunchNumbers(DateTimeOffset? Start, long Creates, long Known, long TupleKnown, long Unknown, long StateObjects, long StateObjectsWarmed,
    double? KnownMedianMs, double? UnknownMedianMs, double Over20Share, bool Running);

public enum WarmVerdict { NoData, Helped, Partly, Missed }

/// <summary>What the bounded check (<see cref="ScsKiller.VerifyAsync"/>) found. <see cref="HitRate"/>: of the sample created, under
/// <see cref="ScsKiller.UnreachedMedianMs"/>; <see cref="PlayedHitRate"/>: of those the game itself compiled in play (what they
/// name, by stage and root signature, is in its recording); null with none in the sample. <see cref="Reaches"/>: whether a
/// compile gets to this game; null when no launch judged it yet. <see cref="Cause"/>: <see cref="VerifyCause"/>'s.</summary>
public sealed record VerifyRecord(DateTimeOffset At, string? DriverId, string? Build, long Sampled, double HitRate, double? PlayedHitRate, double? UnplayedHitRate, bool? Reaches, string? Cause);

public static class VerifyCause
{
    /// <summary>The warm's own entries and the game's are both served to a warm: keys match and nothing was lost.</summary>
    public const string None = "none";
    /// <summary>What the game compiled is served and what only the warm compiled is gone: the cache lost the warm's entries.</summary>
    public const string Retention = "retention";
    /// <summary>What only the warm compiled is served and what the game compiled isn't: the two use different keys.</summary>
    public const string Lookup = "lookup";
    /// <summary>Neither is served: nothing here reaches this cache (a reset, or another driver's).</summary>
    public const string CacheLost = "cacheLost";
    /// <summary>The game hasn't compiled anything the sample names, so only the warm's own entries were tested.</summary>
    public const string Unplayed = "unplayed";
}

/// <summary>The numbers <c>game.diagnostics</c> answers: small, local, nothing about the game's files.</summary>
public sealed record GameDiagnosticsReport(LaunchNumbers? Last, double? FirstLaunchKnownMedianMs, long FirstLaunchKnown, DateTimeOffset? LastPlayed, WarmVerdict Verdict,
    VerifyRecord? Verify, bool NeedsFirstLaunch, bool RecorderForeign);

/// <summary>Reads the recorder's scskiller_creates.csv for its known and tuple_known columns, which <see cref="SessionLog"/> has
/// no use for. A launch splits as there: a #session opens one, a row after its #end opens another.</summary>
public static class GameDiagnostics
{
    sealed class Launch
    {
        public long? Start, End;
        public string Exe = "";
        public double LastT;
        public long Creates, Known, Tuple, Unknown, So, SoKnown, Over20;
        public readonly Dictionary<string, (double Ms, bool Known, bool Tuple)> Psos = new(StringComparer.Ordinal);
    }

    /// <summary>The last launch of <paramref name="exe"/> and the first that started after <paramref name="warmedAt"/> and ended
    /// (its #end, a later launch, or its exe is gone: <paramref name="exeGone"/>), which judges the warm: a later launch runs
    /// on what the game's own run left in the cache.</summary>
    public static (LaunchNumbers? Last, double? FirstKnownMedian, long FirstKnown, double? FirstCoverage, double? FirstOver20, DateTimeOffset? LastPlayed) Read(
        string csv, string? exe, DateTimeOffset? warmedAt, Func<string, bool>? exeGone = null, IReadOnlySet<string>? rayQuery = null)
    {
        var launches = new List<Launch>();
        if (File.Exists(csv))
        {
            using var stream = new FileStream(csv, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            Launch? cur = null;
            while (reader.ReadLine() is { } line)
            {
                var f = line.Split(',');
                if (line.StartsWith('#'))
                {
                    long ms = f.Length > 1 && long.TryParse(f[1], CultureInfo.InvariantCulture, out var x) ? x : 0;
                    if (f[0] == "#session") launches.Add(cur = new Launch { Start = ms, Exe = f.Length > 2 ? string.Join(',', f[2..]).Trim() : "" });
                    else if (f[0] == "#end" && cur != null) cur.End = ms;
                    continue;
                }
                if (f.Length < 5 || f[1].Length != 1 || !double.TryParse(f[0], CultureInfo.InvariantCulture, out var t) || !double.TryParse(f[4], CultureInfo.InvariantCulture, out var created)) continue;
                if (cur == null || cur.End != null) launches.Add(cur = new Launch());
                cur.LastT = Math.Max(cur.LastT, t);
                var kind = f[1][0];
                if (!char.IsUpper(kind)) continue;   // from the game's own pipeline library
                bool known = f[2] == "1", tuple = known || f[3] == "1";
                cur.Creates++;
                cur.Known += known ? 1 : 0;
                cur.Tuple += tuple ? 1 : 0;
                cur.Unknown += tuple ? 0 : 1;
                cur.Over20 += created >= ScsKiller.UnreachedMedianMs ? 1 : 0;
                if (kind is 'R' or 'A')
                {
                    cur.So++;
                    cur.SoKnown += known ? 1 : 0;
                }
                else if (f.Length > 5 && rayQuery?.Contains(f[5]) != true) cur.Psos.TryAdd(f[5], (created, known, tuple));
            }
        }
        var own = launches.Where(l => l.Start == null || !SessionLog.OtherExe(l.Exe, exe)).ToList();
        if (own.Count == 0) return (null, null, 0, null, null, null);
        bool Ended(Launch l) => l.End != null || l != launches[^1] || exeGone?.Invoke(l.Exe.Length > 0 ? l.Exe : exe ?? "") == true;
        var last = own[^1];
        var first = warmedAt is { } at ? own.FirstOrDefault(l => l.Start > at.ToUnixTimeMilliseconds() && Ended(l) && l.Creates > 0) : null;
        // what an earlier launch created may have come from its own compile, not the warm
        var before = first == null ? [] : own.TakeWhile(l => l != first).SelectMany(l => l.Psos.Keys).ToHashSet(StringComparer.Ordinal);
        var judged = first?.Psos.Where(p => p.Value.Known && !before.Contains(p.Key)).Select(p => p.Value.Ms).ToList();
        DateTimeOffset? played = last.End is { } end ? DateTimeOffset.FromUnixTimeMilliseconds(end)
            : last.Start is { } st ? DateTimeOffset.FromUnixTimeMilliseconds(st).AddMilliseconds(last.LastT) : null;
        var numbers = new LaunchNumbers(last.Start is { } s0 ? DateTimeOffset.FromUnixTimeMilliseconds(s0) : null, last.Creates, last.Known, last.Tuple, last.Unknown, last.So, last.SoKnown,
            Median(last.Psos.Values.Where(p => p.Known).Select(p => p.Ms)), Median(last.Psos.Values.Where(p => !p.Tuple).Select(p => p.Ms)),
            last.Creates > 0 ? Math.Round((double)last.Over20 / last.Creates, 3) : 0, !Ended(last));
        return (numbers, judged is { Count: > 0 } ? Median(judged) : null, judged?.Count ?? 0, first is { Creates: > 0 } ? (double)first.Tuple / first.Creates : null,
            first is { Creates: > 0 } ? (double)first.Over20 / first.Creates : null, played);
    }

    static double? Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToList();
        return sorted.Count == 0 ? null : Math.Round(sorted[sorted.Count / 2], 1);
    }

    /// <summary>A plain verdict on the last warm. Missed: the engine's verdict (<see cref="ScsKiller.IsUnreached"/>), the check's, or
    /// the warmed pipelines of the first launch after it still compiled. Helped: they didn't, most of what it asked for was
    /// covered and little took long. Partly: the rest. NoData: no warm, or no ended launch with enough warmed pipelines to judge.</summary>
    public static WarmVerdict Verdict(bool warmed, bool engineUnreached, bool? reaches, double? firstKnownMedian, long firstKnown, double? firstCoverage, double? firstOver20)
    {
        if (!warmed) return WarmVerdict.NoData;
        if (reaches == false || engineUnreached && reaches != true) return WarmVerdict.Missed;
        if (firstKnownMedian is not { } median || firstKnown < ScsKiller.MinJudgedCreates) return WarmVerdict.NoData;
        if (median >= ScsKiller.UnreachedMedianMs) return reaches == true ? WarmVerdict.Partly : WarmVerdict.Missed;
        return firstCoverage >= 0.9 && firstOver20 < 0.05 ? WarmVerdict.Helped : WarmVerdict.Partly;
    }

    /// <summary>What the check's sample says about why (<see cref="VerifyCause"/>): a rate at or over 0.8 is served, under 0.3 isn't,
    /// in between tells nothing.</summary>
    public static string? Cause(double? played, double? unplayed)
    {
        if (played == null) return unplayed >= 0.8 ? VerifyCause.Unplayed : unplayed < 0.3 ? VerifyCause.CacheLost : null;
        if (unplayed == null) return played >= 0.8 ? VerifyCause.None : played < 0.3 ? VerifyCause.Lookup : null;
        return (played >= 0.8, unplayed >= 0.8, played < 0.3, unplayed < 0.3) switch
        {
            (true, true, _, _) => VerifyCause.None,
            (true, _, _, true) => VerifyCause.Retention,
            (_, true, true, _) => VerifyCause.Lookup,
            (_, _, true, true) => VerifyCause.CacheLost,
            _ => null,
        };
    }

    /// <summary>Whether a compile gets to this game: unknown (null) until a launch judged the warm. Its warmed pipelines compiled
    /// and the sample shows the cache lost them or keys differ: no. Served both ways: yes, whatever the first launch did.</summary>
    public static bool? Reaches(string? cause, double? firstKnownMedian, long firstKnown)
    {
        if (firstKnown < ScsKiller.MinJudgedCreates || firstKnownMedian is not { } median) return cause is VerifyCause.CacheLost or VerifyCause.Lookup ? false : null;
        if (median < ScsKiller.UnreachedMedianMs) return true;
        return cause is VerifyCause.None ? true : cause is null or VerifyCause.Unplayed ? null : false;
    }
}
