using SCSKiller.Core.Warming;

namespace SCSKiller.Tests.Platform;

// The warm's memory restarts measured per restart (MemoryRestarts) on the resume points of Phantom Blade Zero's log
// (%LOCALAPPDATA%\SCSKiller\app.log, 2026-11-06 12:12-12:50 UTC): each restart's moved is its resume point less the one its
// process started from.
public class MemoryRestartsTests
{
    [Fact]
    public void Restarts_that_move_little_halve_the_workers_and_stop_memory_restarts_down_to_one()
    {
        var m = new MemoryRestarts();
        // 407,111 -> 407,119 (8), -> 410,184 (3,065), 410,185, 410,187, 410,188, 410,189, 410,190, 410,191, 410,195 (4),
        // 410,195, 410,209 (14), 410,214 (5), 410,214, 410,216 (2), 410,223 (7), 410,223, 411,285 (1,062), 411,286 (1)
        long[] moved = [8, 3065, 1, 2, 1, 1, 1, 1, 3, 0, 14, 5, 0, 2, 7, 0, 1062, 1];
        (int Band, bool Hard)[] want =
        [
            (0, false), (0, false), (0, false), (0, false),   // the first three in a row that move under 500: the third halves 8 workers
            (4, true), (4, true), (4, true), (2, true),        // three more halve again, from the 4 the process ran
            (2, true), (2, true), (1, true), (1, true), (1, true), (1, true), (1, true), (1, true),  // down to one, then held there
            (0, false), (0, false)                             // a restart that moved 1,062 items lifts both
        ];
        int ran = 8;
        for (int i = 0; i < moved.Length; i++)
        {
            m.Restart(moved[i], ran);
            Assert.True((m.Band, m.HardOnly) == want[i], $"restart {i}: band {m.Band}, hard {m.HardOnly}");
            ran = m.Band > 0 ? m.Band : 8;   // the next process runs the band's workers, or all
        }
    }

    [Fact]
    public void A_restart_that_moves_500_items_resets_the_count()
    {
        var m = new MemoryRestarts();
        m.Restart(0, 8);
        m.Restart(0, 8);
        m.Restart(500, 8);   // not little: the two before it don't count toward the three
        m.Restart(0, 8);
        m.Restart(0, 8);
        Assert.False(m.HardOnly);
        Assert.Equal(0, m.Band);
        m.Restart(0, 8);
        Assert.True(m.HardOnly);
        Assert.Equal(4, m.Band);
    }

    [Theory]
    [InlineData(1, 180)]    // a quick db step: the 3-min floor
    [InlineData(6.5, 180)]  // Phantom Blade Zero's, normally
    [InlineData(60, 180)]
    [InlineData(100, 300)]
    [InlineData(180.9, 542)]  // the one that took 180.9 s: 3x is 542.7 s, about 9 min
    [InlineData(400, 900)]  // capped at 15 min
    public void The_start_limit_is_three_db_steps_between_3_and_15_minutes(double dbSeconds, int want)
    {
        Assert.Equal(want, StartLimit.For(dbSeconds));
    }
}
