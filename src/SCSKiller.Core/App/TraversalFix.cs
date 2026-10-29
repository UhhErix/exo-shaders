using SCSKiller.Core.Vulkan;

namespace SCSKiller.Core.App;

/// <summary>
/// The experimental traversal stutter fix (SCSKiller 1.4.1-alpha.1): the D3D12 recorder places a game's committed resources
/// in heaps it pools and reuses, so streaming a new area stops creating (and the driver stops paging in) a heap per
/// resource. The engine only turns it on per game, in the recorder's signed scskiller.ini (<c>traversal_*</c> lines,
/// ScsKiller.TraversalLines); the native d3d12.dll does the rest. Off until chosen, globally (<see cref="Settings.Traversal"/>)
/// or per game (<see cref="GameState.TraversalOverride"/>).
/// Exo Shaders: never in a game with anti-cheat, VAC included, whatever its session or override says (SCSKiller allows it
/// there at the user's risk); only where the recorder itself goes in.
/// </summary>
public static class TraversalFix
{
    /// <summary>What the fix runs with: suballocation on, UAV buffers left as the game creates them, pools up to 4 GB.</summary>
    internal static readonly Dictionary<string, string> Keys = new()
    {
        ["suballocate"] = "1",
        ["uav_buffers"] = "0",
        ["max_pool_mb"] = "4096",
    };

    public const string Title = "Traversal stutter fix (experimental)";
    public const string AskBody = "SCSKiller can try to reduce the stutter when a game streams in a new area. Experimental, and only in games with the recorder.";
    public const string VramLine = "Recommended for GPUs with more than 8 GB of VRAM.";
    public const string LowVramWarning = "Your GPU reports 8 GB of VRAM or less. Games may crash with the fix enabled.";
    public const string SettingsLine = "May reduce stutter when a game loads a new area. Works in games that have the recorder.";
    public const string GameLine = "May reduce stutter when the game loads a new area.";
    public const string RecorderOffLine = "Works while Record while I play is on.";

    public static bool Available => OperatingSystem.IsWindows();

    /// <summary>The one-time question is due: never answered, after the welcome.</summary>
    public static bool Asks(Settings s) => Available && s.WelcomeSeen && !s.TraversalAsked;

    public static Settings Answered(Settings s, TraversalMode mode) => s with { Traversal = mode, TraversalAsked = true };

    /// <summary>The game can run the fix: DirectX 12 through the d3d12.dll recorder (not d3d11.dll, not Vulkan's layer), no
    /// anti-cheat and nothing that keeps the recorder out of it.</summary>
    public static bool Applies(GameState s, VendorCaps caps) =>
        Available && !s.Records11 && !(s.Engine is { } e && VulkanPlan.IsVulkan(e)) && !s.AnyAntiCheat && s.RecorderSkip == null;

    /// <summary>On without a game override: the global setting's All (Recommended turns it on for no game).</summary>
    public static bool ByDefault(Settings st, GameState s) => !s.AnyAntiCheat && st.Traversal == TraversalMode.All;

    public static bool On(Settings st, GameState s, VendorCaps caps) => Applies(s, caps) && (s.TraversalOverride ?? ByDefault(st, s));

    /// <summary>The GPU reports 8 GB of dedicated video memory or less (9 GiB is the line: 8 GB cards report a little over
    /// 8 GiB); null = it reports none.</summary>
    public static bool? LowVram(GpuInfo gpu) => gpu.DedicatedVideoMemory == 0 ? null : gpu.DedicatedVideoMemory < 9UL << 30;

    /// <summary>The game page's line under the switch (a low-VRAM GPU shows <see cref="LowVramWarning"/> beside it instead of
    /// the recommendation).</summary>
    public static string GameNote(GameState s, GpuInfo gpu) =>
        GameLine + (s.RecorderEffective ? "" : " " + RecorderOffLine) + (LowVram(gpu) == true ? "" : " " + VramLine);
}
