using SCSKiller.Core.App;

namespace SCSKiller.Tests.Platform;

// What "the PC is idle" means for "when idle" work, fed readings. Ways it could go wrong: a browser, a build, a video or
// a call with no key pressed reading as idle; two minutes without input being enough; a locked PC needing the full ten
// minutes; a processor reading not yet a minute long (the sampler just started) counting as quiet; the engine's own
// compile counting against it, so it pauses itself.
public sealed class PcIdleTests
{
    static readonly PcIdle.Readings Away = new(TimeSpan.FromMinutes(10), Quiet: false, Presenting: false, Sound: false,
        Microphone: false, OthersCpu: 0.05, Game: false, Battery: false);

    [Fact]
    public void Nobody_at_it_and_nothing_going_is_idle() => Assert.True(PcIdle.Idle(Away));

    [Fact]
    public void Input_must_be_ten_minutes_old_or_one_while_locked_or_dark()
    {
        Assert.False(PcIdle.Idle(Away with { Input = TimeSpan.FromMinutes(9.9) }));
        Assert.False(PcIdle.Idle(Away with { Input = TimeSpan.FromMinutes(2) }));
        Assert.True(PcIdle.Idle(Away with { Input = TimeSpan.FromMinutes(1), Quiet = true }));
        Assert.False(PcIdle.Idle(Away with { Input = TimeSpan.FromSeconds(59), Quiet = true }));
    }

    [Fact]
    public void Any_sign_of_use_is_not_idle()
    {
        Assert.False(PcIdle.Idle(Away with { Presenting = true }));
        Assert.False(PcIdle.Idle(Away with { Sound = true }));
        Assert.False(PcIdle.Idle(Away with { Microphone = true }));
        Assert.False(PcIdle.Idle(Away with { OthersCpu = 0.15 }));
        Assert.False(PcIdle.Idle(Away with { OthersCpu = null }));
        Assert.False(PcIdle.Idle(Away with { Game = true }));
        Assert.False(PcIdle.Idle(Away with { Battery = true }));
        Assert.False(PcIdle.Idle(Away with { Quiet = true, Sound = true }));
    }

    [Fact]
    public void Processor_use_is_others_over_the_last_minute()
    {
        // 30 intervals of 2 s: 1 of 10 units busy elsewhere, plus 5 units of the service's own compile.
        var minute = Enumerable.Repeat(new PcIdle.Interval(2000, Busy: 60, All: 100, Own: 50), 30).ToList();
        Assert.Equal(0.10, PcIdle.OthersShare(minute, PcIdle.CpuWindow)!.Value, 3);
        Assert.Null(PcIdle.OthersShare(minute.Take(10).ToList(), PcIdle.CpuWindow));   // 20 s measured
        // An older busy stretch beyond the minute doesn't count, a busy last minute does.
        var busyThenQuiet = Enumerable.Repeat(new PcIdle.Interval(2000, 90, 100, 0), 30).Concat(minute).ToList();
        Assert.Equal(0.10, PcIdle.OthersShare(busyThenQuiet, PcIdle.CpuWindow)!.Value, 3);
        var quietThenBusy = minute.Concat(Enumerable.Repeat(new PcIdle.Interval(2000, 90, 100, 0), 30)).ToList();
        Assert.Equal(0.90, PcIdle.OthersShare(quietThenBusy, PcIdle.CpuWindow)!.Value, 3);
    }
}
