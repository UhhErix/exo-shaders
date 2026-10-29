using System.Diagnostics;

namespace SCSKiller.Core.Games;

/// <summary>The game page's "Open folder": the exe's folder with the exe selected (Windows), else the folder itself.</summary>
public static class ShowExe
{
    /// <summary>False when the exe's folder is gone.</summary>
    public static bool Open(string exe)
    {
        string? dir = Path.GetDirectoryName(exe);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;
        using var p = OperatingSystem.IsWindows() && File.Exists(exe)
            ? Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = "/select,\"" + exe + "\"" })
            : Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
        return true;
    }
}
