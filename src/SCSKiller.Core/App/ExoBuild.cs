namespace SCSKiller.Core.App;

/// <summary>The Exo build: the engine that Exo Launcher ships (engine/src/ExoShaders.Service, compiled with EXO_BUILD). It is
/// local-only: no request to SCSKiller's servers or to Patreon, no sign-in, sharing or upload, and no recorder session in a game
/// that has anti-cheat (the "at your own risk" and offline sessions are gone, and a game whose folder holds a known anti-cheat
/// module is refused for recording and injection). Upstream builds compile with the constant off, so a merge keeps this code
/// as it is. The Oodle and zlib downloads from GitHub stay: they are third-party and licensed (engine/CONTRACT.md).</summary>
public static class ExoBuild
{
    public const bool Enabled =
#if EXO_BUILD
        true;
#else
        false;
#endif

    /// <summary>SCSKiller's servers and Patreon: an Exo build sends no request to these hosts (any subdomain too).</summary>
    public static readonly string[] BlockedDomains = ["scskiller.com", "scskiller.io", "scskiller.xyz", "patreon.com"];

    /// <summary>Known anti-cheat modules, by the start of the file's name without its extension (case-insensitive). A game
    /// folder holding one is never recorded or injected into.</summary>
    public static readonly string[] AntiCheatModules =
        ["EasyAntiCheat", "BEService", "BEClient", "BattlEye", "vgk", "vgc", "FACEIT", "GameGuard", "Ricochet", "mhyprot", "XIGNCODE"];

    public static bool BlockedHost(Uri uri) =>
        BlockedDomains.Any(d => uri.Host.Equals(d, StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith("." + d, StringComparison.OrdinalIgnoreCase));

    /// <summary>The list's entry that <paramref name="fileName"/> is, null for none.</summary>
    public static string? AntiCheatModule(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        return AntiCheatModules.FirstOrDefault(m => stem.StartsWith(m, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The first known anti-cheat module in the install folder, looking <paramref name="depth"/> folders down (the modules sit
    /// in the game's root or in its anti-cheat's own folder); null for none, or for a folder that can't be read.</summary>
    public static string? AntiCheatModuleIn(string installDir, int depth = 2)
    {
        if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir)) return null;
        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(installDir))
            {
                if (File.Exists(entry) && AntiCheatModule(Path.GetFileName(entry)) is { } found) return found;
                if (depth > 0 && Directory.Exists(entry) && AntiCheatModuleIn(entry, depth - 1) is { } inside) return inside;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { }
        return null;
    }
}
