using SCSKiller.Core;
using SCSKiller.Core.App;
using SCSKiller.Core.Planning;
using SCSKiller.Core.Warming;

namespace SCSKiller.Tests.Platform;

/// <summary>SCSKiller 1.4.0-beta.1 and 1.4.1-alpha.1's pure rules as Exo Shaders carries them (written from the alpha's
/// binaries): the traversal fix's reach, its signed ini lines, the rule fallback's band, the right-away queue notes, the
/// driver-update picker and NVIDIA's turned-off cache.</summary>
public class AlphaFourOneTests
{
    static readonly Game G = new("steam:1", "Fake", Store.Steam, @"X:\Fake", @"X:\Fake\fake.exe");
    static readonly EngineInfo D3D12 = new("Unreal", "5.4", null, "D3D12", false, null);
    static readonly VendorCaps Nvidia = new("nvidia-1", true, true, true, PerStageCache: true);

    static GameState S(EngineInfo? engine = null, AntiCheat ac = AntiCheat.None, bool vac = false, bool? traversal = null) =>
        new GameState(G, engine ?? D3D12, ac, GameStatus.Warmed, "", null, null, null, null, null, null, null, true, null)
            with { Vac = vac, TraversalOverride = traversal };

    static Settings Defaults(TraversalMode mode = TraversalMode.Off) => AppStore.DefaultSettings with { Traversal = mode };

    /// <summary>Exo Shaders keeps the traversal fix out of every game with anti-cheat, VAC included, whatever a risk or
    /// offline session or the game's own override says; SCSKiller allows those at the user's risk.</summary>
    [Fact]
    public void The_traversal_fix_never_reaches_a_game_with_anti_cheat()
    {
        Assert.True(TraversalFix.Applies(S(), Nvidia));
        Assert.False(TraversalFix.Applies(S(ac: AntiCheat.EasyAntiCheat) with { RiskEligible = true }, Nvidia));
        Assert.False(TraversalFix.Applies(S(ac: AntiCheat.BattlEye) with { OfflineEligible = true }, Nvidia));
        Assert.False(TraversalFix.Applies(S(vac: true), Nvidia));
        Assert.False(TraversalFix.On(Defaults(TraversalMode.All), S(ac: AntiCheat.Other, traversal: true), Nvidia));
        Assert.False(TraversalFix.On(Defaults(TraversalMode.All), S(vac: true, traversal: true), Nvidia));
    }

    /// <summary>It runs where the D3D12 recorder does: not with the D3D11 recorder, not on Vulkan, not where the recorder is
    /// skipped. All turns it on, Recommended doesn't, and a game's override wins either way.</summary>
    [Fact]
    public void The_traversal_fix_follows_the_setting_and_the_game_override()
    {
        Assert.False(TraversalFix.Applies(S() with { Records11 = true }, Nvidia));
        Assert.False(TraversalFix.Applies(S(new EngineInfo("Carved", "-", null, "Vulkan", false, null)), Nvidia));
        Assert.False(TraversalFix.Applies(S() with { RecorderSkip = ScsKiller.SkipUnsupported }, Nvidia));
        Assert.False(TraversalFix.On(Defaults(), S(), Nvidia));
        Assert.True(TraversalFix.On(Defaults(TraversalMode.All), S(), Nvidia));
        Assert.False(TraversalFix.On(Defaults(TraversalMode.Recommended), S(), Nvidia));
        Assert.True(TraversalFix.On(Defaults(), S(traversal: true), Nvidia));
        Assert.False(TraversalFix.On(Defaults(TraversalMode.All), S(traversal: false), Nvidia));
        Assert.Equal((TraversalMode.All, true), (TraversalFix.Answered(Defaults(), TraversalMode.All).Traversal, TraversalFix.Answered(Defaults(), TraversalMode.All).TraversalAsked));
    }

    /// <summary>The fix's lines are part of the recorder's signed ini: SCSKiller's own file with them reads as ours, and one
    /// whose value was edited (or isn't a plain number) doesn't.</summary>
    [Fact]
    public void Traversal_lines_are_signed_with_the_ini()
    {
        const string sig = "0123456789abcdef0123456789abcdef01234567";
        string Ini(string traversal) => "[scskiller]\r\n; written by SCSKiller: record the pipelines this game creates. Removed by 'uninstall recorder'.\r\nmode=record\r\n"
            + "; the game's install folder: the recorder checks its entries for anti-cheat too\r\nroot=..\\..\r\n"
            + "; the game's exe when SCSKiller last found no anti-cheat in its folders: the recorder records only while it is unchanged\r\n"
            + "exe_size=10\r\nexe_time=20\r\n" + traversal + $"sig={sig}\r\n";
        var ours = Ini("traversal_suballocate=1\r\ntraversal_uav_buffers=0\r\ntraversal_max_pool_mb=4096\r\n");
        Assert.True(ScsKiller.OurIniBytes(System.Text.Encoding.ASCII.GetBytes(Ini("")), null, "..\\.."));
        Assert.True(ScsKiller.OurIniBytes(System.Text.Encoding.ASCII.GetBytes(ours), null, "..\\.."));
        Assert.False(ScsKiller.OurIniBytes(System.Text.Encoding.ASCII.GetBytes(ours.Replace("max_pool_mb=4096", "max_pool_mb=04096")), null, "..\\.."));
        Assert.False(ScsKiller.OurIniBytes(System.Text.Encoding.ASCII.GetBytes(Ini("traversal_unknown=1\r\n")), null, "..\\.."));
    }

    /// <summary>The rule takes over for the stage sets no recorded root signature covers when it rebuilt half or more of the
    /// recorded ones, short of the 99% where it builds them all.</summary>
    [Fact]
    public void The_rule_fallback_is_the_band_between_half_and_all()
    {
        Assert.False(PlanBuilder.RuleFallback(0, 0));
        Assert.False(PlanBuilder.RuleFallback(49, 100));
        Assert.True(PlanBuilder.RuleFallback(50, 100));
        Assert.True(PlanBuilder.RuleFallback(98, 100));
        Assert.False(PlanBuilder.RuleFallback(99, 100));
    }

    /// <summary>A right-away item waits on its own like an idle one; the picker's four choices map onto the two settings
    /// and back; NVIDIA's cache is off at a size of 0 only.</summary>
    [Fact]
    public void Right_away_items_driver_choices_and_a_turned_off_cache()
    {
        Assert.True(ScsKiller.WaitsOnItsOwn(new QueueItem("a", QueueStage.Waiting, null, null, ScsKiller.RightAwayNote)));
        Assert.True(ScsKiller.WaitsOnItsOwn(new QueueItem("a", QueueStage.Waiting, null, null, ScsKiller.WhenIdleNote)));
        Assert.False(ScsKiller.WaitsOnItsOwn(new QueueItem("a", QueueStage.Waiting, null, null, null)));
        Assert.False(ScsKiller.WaitsOnItsOwn(new QueueItem("a", QueueStage.Paused, null, null, ScsKiller.WhenIdleNote)));
        foreach (var choice in new[] { 0, 1, 2, 3 })
            Assert.Equal(choice, DriverUpdateChoice.Of(DriverUpdateChoice.With(Defaults(), choice)));
        Assert.True(DriverUpdateChoice.With(Defaults(), 2) is { OnDriverUpdate: DriverUpdateMode.WhenIdle, DriverRightAway: true });
        Assert.True(ScsKiller.CacheOff(GpuVendor.Nvidia, new CacheLimit(0, false)));
        Assert.False(ScsKiller.CacheOff(GpuVendor.Nvidia, new CacheLimit(null, false)));
        Assert.False(ScsKiller.CacheOff(GpuVendor.Amd, new CacheLimit(0, false)));
        Assert.Equal("The NVIDIA shader cache is turned off", Format.CacheOffLine(GpuVendor.Nvidia, new CacheLimit(0, true)));
    }

    /// <summary>A crash storm stops a compile without being a give-up at a spot (no skip window), and a DirectX 11 launch's
    /// frame lines don't claim to tell shader stutters apart.</summary>
    [Fact]
    public void A_crash_storm_is_no_give_up_and_DirectX_11_lines_are_unsplit()
    {
        Assert.False(Warmer.GaveUp(Warmer.CrashStormError));
        Assert.True(Warmer.GaveUp(Warmer.HungError));
        Assert.Equal("1% low 40 fps", Format.Session(new FrameSession(1, TimeSpan.FromMinutes(5), 40, 3, D3D11: true)));
        Assert.Equal("1% low 40 fps · 3 stutters", Format.Session(new FrameSession(1, TimeSpan.FromMinutes(5), 40, 3)));
    }
}
