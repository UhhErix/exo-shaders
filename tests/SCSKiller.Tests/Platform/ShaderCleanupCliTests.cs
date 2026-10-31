using System.Diagnostics;
using SCSKiller.Core.App;

namespace SCSKiller.Tests.Platform;

/// <summary>
/// Windows' automatic disk cleanup and D3DSCache (upstream's D3DSCache.KeepFromCleanup, the CLI's shader-cleanup and the
/// fork's older name for it, cleanup-guard). It fails if either name changes anything on an argument it doesn't know, or
/// answers with another name's usage. Nothing here writes the registry.
/// </summary>
public sealed class ShaderCleanupCliTests
{
    [Theory]
    [InlineData("shader-cleanup", "shader-cleanup keep|allow (Windows)")]
    [InlineData("cleanup-guard", "cleanup-guard [on|off]")]
    public void The_cli_refuses_an_argument_it_does_not_know_before_touching_anything(string command, string usage)
    {
        if (!OperatingSystem.IsWindows() || Elevated.CliExe() is not { } cli) return;
        var root = Directory.CreateTempSubdirectory("scskiller-cleanup-").FullName;
        try
        {
            var file = Path.Combine(root, "result.json");
            var before = D3DSCache.CleanupKept();
            var psi = new ProcessStartInfo(cli, [command, "bogus", Elevated.YesArg, Elevated.ResultArg, file]) { RedirectStandardError = true, RedirectStandardOutput = true };
            psi.Environment["SCSKILLER_DATA_DIR"] = Path.Combine(root, "data");
            using var p = Process.Start(psi)!;
            p.StandardOutput.ReadToEnd();
            p.StandardError.ReadToEnd();
            p.WaitForExit();
            Assert.Equal(new Elevated.Result(false, usage), Elevated.ReadResult(file, p.ExitCode));
            Assert.Equal(before, D3DSCache.CleanupKept());
        }
        finally { Directory.Delete(root, true); }
    }
}
