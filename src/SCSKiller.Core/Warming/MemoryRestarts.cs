namespace SCSKiller.Core.Warming;

/// <summary>The memory restarts of one warm, each measured against the resume point its process started from (the previous
/// restart's). <see cref="LittleRestarts"/> in a row that moved fewer than <see cref="LittleItems"/> items stop the restarts for
/// memory but at the 60% bound, and the next process runs half the workers, down to one. A restart that moved
/// <see cref="LittleItems"/> or more lifts both. Measured on Phantom Blade Zero (16 GB): 16 restarts moved 39 items between
/// 12:15 and 12:40 UTC, from 410,184 to 410,223, each at a third of RAM or 60% with 0.3-5 GB free.</summary>
public sealed class MemoryRestarts
{
    public const int LittleRestarts = 3, LittleItems = 500;

    int _little;   // restarts in a row that moved fewer than LittleItems

    /// <summary>No restart for memory: only the 60% bound ends a process.</summary>
    public bool HardOnly { get; private set; }

    /// <summary>The workers the next process starts with (0: all).</summary>
    public int Band { get; private set; }

    /// <summary>A memory restart: <paramref name="moved"/> items past the resume point its process started from, on
    /// <paramref name="ran"/> workers.</summary>
    public void Restart(long moved, int ran)
    {
        if (moved >= LittleItems) (_little, HardOnly, Band) = (0, false, 0);
        else if (++_little >= LittleRestarts) (_little, HardOnly, Band) = (0, true, Math.Max(1, ran / 2));
    }
}

/// <summary>The start watchdog's limit (SCSKILLER_WARM_START_S, proxy/warm.cpp): no progress for this long ends a start. Three
/// times the last db step (the step that ended a start), never under 3 min nor over 15 min.</summary>
public static class StartLimit
{
    public const int Floor = 180, Ceiling = 900;

    public static int For(double dbSeconds) => (int)Math.Clamp(3 * dbSeconds, Floor, Ceiling);
}
