using System.Text.RegularExpressions;

namespace SCSKiller.Core.Games;

/// <summary>Games installed with the Riot Client, from %ProgramData%\Riot Games\Metadata\&lt;product&gt;.&lt;patchline&gt;\
/// &lt;product&gt;.&lt;patchline&gt;.product_settings.yaml: product_install_full_path is the folder the client installs into
/// ("C:/Riot Games/VALORANT/live"). RiotClientInstalls.json beside Metadata names only the client's own exe. The name is the
/// uninstall entry's (HKCU "Riot Game &lt;product&gt;.&lt;patchline&gt;"), else the product's known one. Every Riot game counts
/// as anti-cheat (<see cref="GameFiles.DetectAntiCheat"/>): Vanguard installs outside the game folder. Exo Shaders: its folder
/// is never listed or read (<see cref="GameFiles.IsRiot"/>): a product's exe is the known name, or one made of its product's.</summary>
public sealed class RiotSource(string? metadataDir = null, Func<string, string?>? registry = null) : IGameSource, ILeavesOut
{
    public Store Store => Store.Other;

    public Func<string, bool> LeftOut { get; set; } = _ => false;

    public static string DefaultMetadataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Riot Games", "Metadata");

    // League's install root holds its client (LeagueClient.exe, the largest exe there); the game is under Game\
    static readonly Dictionary<string, (string Name, string? Exe)> Products = new()
    {
        ["valorant"] = ("VALORANT", @"ShooterGame\Binaries\Win64\VALORANT-Win64-Shipping.exe"), ["league_of_legends"] = ("League of Legends", @"Game\League of Legends.exe"),
        ["bacon"] = ("Legends of Runeterra", @"Game\LoR.exe"), ["lion"] = ("2XKO", @"Lion\Binaries\Win64\Lion-Win64-Shipping.exe"),
    };

    static readonly Regex InstallPath = new(@"^product_install_full_path:[ \t]*[""']?(.*?)[""']?[ \t\r]*$", RegexOptions.Multiline);

    public IReadOnlyList<Game> Discover()
    {
        var dir = metadataDir ?? DefaultMetadataDir;
        if (!Directory.Exists(dir)) return [];
        var games = new List<Game>();
        foreach (var sub in Directory.EnumerateDirectories(dir))
            try
            {
                var key = Path.GetFileName(sub);
                if (key.Split('.') is not [var product, var patchline] || product == "riot_client") continue;
                string install, exe;
                var known = Products.GetValueOrDefault(product);
                try
                {
                    if (InstallPath.Match(File.ReadAllText(Path.Combine(sub, key + ".product_settings.yaml"))) is not { Success: true } m || m.Groups[1].Value.Length == 0) continue;
                    install = GameFiles.DirKey(m.Groups[1].Value);
                    if (LeftOut(install)) continue;
                    if (!File.Exists(exe = Path.Combine(install, known.Exe ?? product + ".exe")) && known.Exe != null) continue;   // still installing
                    if (!Directory.Exists(install)) continue;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { continue; }
                var name = (registry ?? Uninstall)($@"Riot Game {key}") is { } shown && !string.IsNullOrWhiteSpace(shown) ? shown.Trim() : known.Name ?? product;
                if (patchline != "live" && !name.Contains(patchline, StringComparison.OrdinalIgnoreCase)) name += $" ({patchline.ToUpperInvariant()})";
                games.Add(new Game($"riot:{key}", name, Store.Other, install, exe));
            }
            catch (Exception e) when (e is not OperationCanceledException) { GameFiles.Skip("Riot Client", sub, e); }
        return games;
    }

    static string? Uninstall(string entry) => !OperatingSystem.IsWindows() ? null
        : EaSource.ReadRegistry($@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\{entry}\DisplayName")
        ?? EaSource.ReadRegistry($@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Uninstall\{entry}\DisplayName");
}
