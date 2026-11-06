using System.Net;
using SCSKiller.Core.App;

namespace SCSKiller.Tests.Platform;

// The Exo build's local-only rules (App/ExoBuild.cs): the hosts it never contacts, the anti-cheat modules it never records or
// injects into, and the request gate in RouteFailover. The gate is tested with localOnly on, whatever the build's constant.
public class ExoBuildTests
{
    sealed class CountingHandler : HttpMessageHandler
    {
        public readonly List<Uri> Sent = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Sent.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    [Theory]
    [InlineData("https://api.scskiller.com/healthz")]
    [InlineData("https://api.scskiller.io/")]
    [InlineData("https://dl.scskiller.io/packages.json")]
    [InlineData("https://scskiller.com/")]
    [InlineData("https://www.patreon.com/oauth2/authorize")]
    public void SCSKiller_and_Patreon_hosts_are_blocked(string url) => Assert.True(ExoBuild.BlockedHost(new Uri(url)));

    [Theory]
    [InlineData("https://github.com/WorkingRobot/OodleUE/releases/download/x/gcc-x64-release.zip")]
    [InlineData("https://api.nuget.org/v3-flatcontainer/x.nupkg")]
    [InlineData("https://scskiller.com.evil.example/")]
    public void Other_hosts_are_not_blocked(string url) => Assert.False(ExoBuild.BlockedHost(new Uri(url)));

    [Theory]
    [InlineData("EasyAntiCheat.exe", "EasyAntiCheat")]
    [InlineData("EasyAntiCheat_EOS.sys", "EasyAntiCheat")]
    [InlineData("BEService.exe", "BEService")]
    [InlineData("BEClient_x64.dll", "BEClient")]
    [InlineData("vgk.sys", "vgk")]
    [InlineData("vgc.exe", "vgc")]
    [InlineData("FaceItService.exe", "FACEIT")]
    [InlineData("GameGuard.des", "GameGuard")]
    [InlineData("Ricochet_x64.dll", "Ricochet")]
    [InlineData("mhyprot2.sys", "mhyprot")]
    [InlineData("XIGNCODE3.exe", "XIGNCODE")]
    public void Known_anti_cheat_modules_are_named(string file, string module) => Assert.Equal(module, ExoBuild.AntiCheatModule(file));

    [Theory]
    [InlineData("PBZ.exe")]
    [InlineData("d3d12.dll")]
    [InlineData("steam_api64.dll")]
    public void Ordinary_game_files_are_not_anti_cheat(string file) => Assert.Null(ExoBuild.AntiCheatModule(file));

    [Fact]
    public void A_module_two_folders_down_refuses_the_game_and_a_deeper_one_is_not_seen()
    {
        var root = Path.Combine(Path.GetTempPath(), "exo-ac-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Game", "Engine", "Binaries"));
            File.WriteAllText(Path.Combine(root, "Game", "Game.exe"), "");
            Assert.Null(ExoBuild.AntiCheatModuleIn(Path.Combine(root, "Game")));

            Directory.CreateDirectory(Path.Combine(root, "Game", "EasyAntiCheat"));
            File.WriteAllText(Path.Combine(root, "Game", "EasyAntiCheat", "EasyAntiCheat_EOS.sys"), "");
            Assert.Equal("EasyAntiCheat", ExoBuild.AntiCheatModuleIn(Path.Combine(root, "Game")));

            Directory.CreateDirectory(Path.Combine(root, "Deep", "A", "B", "C"));
            File.WriteAllText(Path.Combine(root, "Deep", "A", "B", "C", "vgk.sys"), "");
            Assert.Null(ExoBuild.AntiCheatModuleIn(Path.Combine(root, "Deep")));   // three folders down: beyond the depth of two
            File.WriteAllText(Path.Combine(root, "Deep", "A", "B", "vgc.exe"), "");
            Assert.Equal("vgc", ExoBuild.AntiCheatModuleIn(Path.Combine(root, "Deep")));   // two folders down: inside
            Assert.Null(ExoBuild.AntiCheatModuleIn(Path.Combine(root, "Missing")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task A_local_only_client_sends_nothing_to_SCSKiller_or_Patreon()
    {
        var inner = new CountingHandler();
        var client = new HttpMessageInvoker(new RouteFailover(inner, localOnly: true));
        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://api.scskiller.com/healthz"), default));
        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "https://api.scskiller.io/community"), default));
        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://www.patreon.com/api/oauth2/v2/identity"), default));
        Assert.Empty(inner.Sent);

        using var ok = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://github.com/x/releases/download/a.zip"), default);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Single(inner.Sent);   // other hosts (the codec downloads) pass through
    }

    [Fact]
    public async Task Without_local_only_the_routes_are_tried_as_before()
    {
        var inner = new CountingHandler();
        var client = new HttpMessageInvoker(new RouteFailover(inner, localOnly: false));
        using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://api.scskiller.com/healthz"), default);
        Assert.Single(inner.Sent);
    }
}
