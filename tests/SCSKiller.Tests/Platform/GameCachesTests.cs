using System.Runtime.InteropServices;
using System.Text;
using SCSKiller.Core;
using SCSKiller.Core.App;

namespace SCSKiller.Tests.Platform;

/// <summary>Synthetic D3DSCache folders: dbs with the runtime's app_id table, made with Windows' own SQLite.</summary>
static class FakeD3DSCache
{
    /// <summary>A folder like the runtime's: a db naming <paramref name="exePath"/> plus its -shm and an empty -wal. broken: the
    /// db is not SQLite at all, and its -wal names the exe (a WAL scan alone must not attribute it).</summary>
    public static string Folder(string root, string hash, string exePath, bool broken = false)
    {
        var dir = Path.Combine(root, hash);
        Directory.CreateDirectory(dir);
        var db = Path.Combine(dir, "F4EB2D6C-ED2B-4BDD-AD9D-F913287E6768.dxcache");
        if (broken)
        {
            File.WriteAllBytes(db, new byte[4096]);
            File.WriteAllBytes(db + "-wal", Encoding.Latin1.GetBytes("\x01\x02" + exePath + "\x00"));
        }
        else
        {
            Db(db, exePath);
            File.WriteAllBytes(db + "-wal", []);
        }
        File.WriteAllBytes(db + "-shm", new byte[32768]);
        return dir;
    }

    /// <summary>An app_id row as a WAL holds it (alpha.3 reads only whole records): its header (its size, the rowid alias's
    /// type 0, exe_path's text type), then the path in UTF-8.</summary>
    public static byte[] WalRow(string exePath)
    {
        var path = Encoding.UTF8.GetBytes(exePath);
        var type = 13 + 2 * path.Length;
        byte[] varint = type < 128 ? [(byte)type] : [(byte)(0x80 | type >> 7), (byte)(type & 0x7F)];
        return [(byte)(2 + varint.Length), 0, .. varint, .. path];
    }

    public static void Db(string path, params string[] exePaths)
    {
        Assert.Equal(0, sqlite3_open(path, out var db));
        try
        {
            Exec(db, "create table app_id(id INTEGER PRIMARY KEY, exe_path TEXT, app_name TEXT, engine_name TEXT, app_version INTEGER, engine_version INTEGER, app_profile_version INTEGER)");
            foreach (var p in exePaths) Exec(db, $"insert into app_id(exe_path) values ('{p.Replace("'", "''")}')");
        }
        finally { sqlite3_close(db); }
    }

    /// <summary>The adapter db NVIDIA's driver from 621 keeps in a D3DSCache folder, for an RTX 3070.</summary>
    public const string AdapterName = "6F75932F-7DFC-4FB0-B4B8-12DE1AC415DA_VEN_10DE&DEV_2484&SUBSYS_87BC&REV_A1.dxcache";

    /// <summary>An adapter db in <paramref name="dir"/> as the runtime writes it (its schema read from one on 621.12): one
    /// compiler row (<c>abi:version:family</c>), one app_id row naming <paramref name="exePath"/>, and each of
    /// <paramref name="keys"/> (hex) with a value.</summary>
    public static string Adapter(string dir, string exePath, string compiler, params string[] keys)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, AdapterName);
        var (abi, version, family) = compiler.Split(':') is [var a, var v, var f] ? (a, v, f) : throw new ArgumentException(compiler);
        Assert.Equal(0, sqlite3_open(path, out var db));
        try
        {
            Exec(db, "create table keys(keyid INTEGER PRIMARY KEY, key BLOB, last_accessed REAL); create unique index key_index on keys(key); "
                + "create index lru on keys(last_accessed DESC); create table vals(keyid INTEGER REFERENCES keys(keyid) ON DELETE CASCADE, type INTEGER, value BLOB); "
                + "create unique index value_index on vals(keyid, type); create table groupkeys(keyid INTEGER PRIMARY KEY, key BLOB, version INTEGER); "
                + "create unique index groupkey_index on groupkeys(key); create table groups(groupkeyid INTEGER REFERENCES groupkeys(keyid) ON DELETE CASCADE, valuekeyid INTEGER); "
                + "create table app_id(id INTEGER PRIMARY KEY, exe_path TEXT, app_name TEXT, engine_name TEXT, app_version INTEGER, engine_version INTEGER, app_profile_version INTEGER); "
                + "create table compiler_id(id INTEGER PRIMARY KEY, abi INTEGER, version INTEGER, family TEXT)");
            Exec(db, $"insert into compiler_id values (0, {abi}, {version}, '{family}'); insert into app_id(id, exe_path) values (0, '{exePath.Replace("'", "''")}')");
            foreach (var k in keys) Exec(db, $"insert into keys(key, last_accessed) values (x'{k}', 0); insert into vals values (last_insert_rowid(), 0, x'{k}')");
        }
        finally { sqlite3_close(db); }
        return path;
    }

    /// <summary>The runtime's open of <paramref name="db"/> under another driver: WAL on, <paramref name="compiler"/> named in it, and
    /// while that connection is open (the WAL not folded back), the db, its WAL and -shm copied into <paramref name="to"/>.</summary>
    public static void Recompiled(string db, string compiler, string to)
    {
        var (abi, version, family) = compiler.Split(':') is [var a, var v, var f] ? (a, v, f) : throw new ArgumentException(compiler);
        Assert.Equal(0, sqlite3_open(db, out var h));
        try
        {
            Exec(h, "pragma journal_mode=wal");
            Exec(h, $"update compiler_id set abi = {abi}, version = {version}, family = '{family}'");
            Directory.CreateDirectory(to);
            foreach (var ext in new[] { "", "-wal", "-shm" })
            {
                using var from = new FileStream(db + ext, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var copy = File.Create(Path.Combine(to, Path.GetFileName(db) + ext));
                from.CopyTo(copy);
            }
        }
        finally { sqlite3_close(h); }
    }

    /// <summary>The keys a db holds, in hex, read as it is on disk.</summary>
    public static List<string> Keys(string db)
    {
        var keys = new List<string>();
        Assert.True(D3DSCache.Query(db, "select hex(key) from keys order by key", st => keys.Add(Marshal.PtrToStringUTF8(D3DSCache.sqlite3_column_text(st, 0))!)));
        return keys;
    }

    static void Exec(nint db, string sql) => Assert.Equal(0, sqlite3_exec(db, sql, 0, 0, 0));

    [DllImport("winsqlite3.dll")] static extern int sqlite3_open([MarshalAs(UnmanagedType.LPUTF8Str)] string file, out nint db);
    [DllImport("winsqlite3.dll")] static extern int sqlite3_exec(nint db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, nint callback, nint arg, nint error);
    [DllImport("winsqlite3.dll")] static extern int sqlite3_close(nint db);
}

// Everything here is under %TEMP%; the real D3DSCache and Saved folders are never read.
public class GameCachesTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "scskiller-caches-test-" + Guid.NewGuid().ToString("N")[..8]);
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    static Game Steam(string install) =>
        new("steam:1", "Fake", Store.Steam, install, Path.Combine(install, "Fake", "Binaries", "Win64", "Fake-Win64-Shipping.exe"));

    [Fact]
    public void A_D3DSCache_folder_is_the_games_only_when_every_db_reads_and_every_path_it_names_is_the_games_exe()
    {
        var g = Steam(@"C:\Games\Steam\steamapps\common\Fake Game");
        var d3ds = Path.Combine(_root, "D3DSCache");
        var ours = FakeD3DSCache.Folder(d3ds, "01", g.ExePath.ToUpperInvariant());   // the path's case doesn't matter
        FakeD3DSCache.Folder(d3ds, "02", @"C:\Other\Fake-Win64-Shipping.exe");       // same exe name, another path
        FakeD3DSCache.Folder(d3ds, "03", g.ExePath, broken: true);                   // unparsable db: nothing, whatever the WAL says

        var walOnly = Path.Combine(d3ds, "04");   // a db without the table yet: its rows are only in the WAL
        Directory.CreateDirectory(walOnly);
        File.WriteAllBytes(Path.Combine(walOnly, "a.dxcache"), []);
        File.WriteAllBytes(Path.Combine(walOnly, "a.dxcache-wal"), [.. "junk"u8, .. FakeD3DSCache.WalRow(g.ExePath), .. "more"u8]);

        var mixed = FakeD3DSCache.Folder(d3ds, "05", g.ExePath);   // a second db names another exe: not positively the game's
        FakeD3DSCache.Db(Path.Combine(mixed, "other.dxcache"), @"C:\Other\other.exe");

        var alsoBroken = FakeD3DSCache.Folder(d3ds, "06", g.ExePath);   // a readable db beside an unreadable one
        File.WriteAllText(Path.Combine(alsoBroken, "b.dxcache"), "not a database, just text long enough to have a header....................................................................................");

        Directory.CreateDirectory(Path.Combine(d3ds, "07"));   // empty

        Assert.Equal([ours, walOnly], D3DSCache.FoldersOf(d3ds, g).Order());
        Assert.Null(D3DSCache.ExePaths(Path.Combine(d3ds, "03")));
        Assert.Empty(D3DSCache.FoldersOf(Path.Combine(_root, "missing"), g));
    }

    /// <summary>A folder whose db names another exe is decided without its WAL (one held exclusively would make it a doubt);
    /// the db's paths are kept until the db changes.</summary>
    [Fact]
    public void A_folder_another_exe_owns_is_decided_from_its_db_without_reading_the_WAL()
    {
        var g = Steam(@"C:\Games\Steam\steamapps\common\Fake Game");
        var other = FakeD3DSCache.Folder(Path.Combine(_root, "D3DSCache"), "01", @"C:\Other\other.exe");
        var db = Path.Combine(other, "F4EB2D6C-ED2B-4BDD-AD9D-F913287E6768.dxcache");
        using (new FileStream(db + "-wal", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.False(D3DSCache.IsGames(other, g));
            using (new FileStream(db, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert.False(D3DSCache.IsGames(other, g));   // its paths kept: the db isn't read again
        }

        File.Delete(db);   // the db rewritten for the game: read again
        FakeD3DSCache.Db(db, g.ExePath);
        File.SetLastWriteTimeUtc(db, DateTime.UtcNow.AddMinutes(1));
        Assert.True(D3DSCache.IsGames(other, g));
        using (new FileStream(db + "-wal", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.Null(D3DSCache.IsGames(other, g));   // the game's: its WAL is read, and can't be: a doubt
    }

    /// <summary>A compile or a test deletes the folders its own exes left since it started, nothing else.</summary>
    [Fact]
    public void A_test_deletes_only_the_D3DSCache_folders_of_its_own_exes()
    {
        var (d3ds, mine) = (Path.Combine(_root, "D3DSCache"), Path.Combine(_root, "scskiller-app-test-1"));
        var started = DateTime.UtcNow;
        var selftest = FakeD3DSCache.Folder(d3ds, "01", Path.Combine(mine, "admit", "selftest.exe"));
        var staged = FakeD3DSCache.Folder(d3ds, "02", Path.Combine(mine, "data", "work", "stage-1-1", "scsk-cp-1.exe").ToUpperInvariant());
        FakeD3DSCache.Folder(d3ds, "03", Path.Combine(_root, "scskiller-app-test-10", "selftest.exe"));   // another test's
        FakeD3DSCache.Folder(d3ds, "04", @"C:\Games\Fake\Fake.exe");                                     // a game
        FakeD3DSCache.Folder(d3ds, "05", Path.Combine(mine, "b.exe"), broken: true);                     // in doubt
        var mixed = FakeD3DSCache.Folder(d3ds, "06", Path.Combine(mine, "c.exe"));
        FakeD3DSCache.Db(Path.Combine(mixed, "other.dxcache"), @"C:\Games\Fake\Fake.exe");
        var older = FakeD3DSCache.Folder(d3ds, "07", Path.Combine(mine, "d.exe"));
        Directory.SetCreationTimeUtc(older, started.AddMinutes(-5));                                     // before this test

        Assert.Equal([selftest, staged], D3DSCache.Made(d3ds, mine, started).Order());
        Assert.Empty(D3DSCache.Made(Path.Combine(_root, "missing"), mine, started));
    }

    [Fact]
    public void A_path_only_in_the_WAL_is_found_with_non_ascii_characters()
    {
        var g = Steam(@"C:\Users\José\Games\Fake Game");
        var walOnly = Path.Combine(_root, "D3DSCache", "01");
        Directory.CreateDirectory(walOnly);
        File.WriteAllBytes(Path.Combine(walOnly, "a.dxcache"), []);
        File.WriteAllBytes(Path.Combine(walOnly, "a.dxcache-wal"), [.. "junk"u8, .. FakeD3DSCache.WalRow(g.ExePath), .. "more"u8]);   // SQLite's text: UTF-8
        Assert.Equal([walOnly], D3DSCache.FoldersOf(Path.Combine(_root, "D3DSCache"), g));

        // a fragment of a path before a byte that isn't UTF-8 text: not joined to the path after it
        File.WriteAllBytes(Path.Combine(walOnly, "a.dxcache-wal"), [.. @"C:\partial"u8, 0xFF, .. FakeD3DSCache.WalRow(g.ExePath), 0]);
        Assert.Equal([g.ExePath], D3DSCache.ExePaths(walOnly)!);
    }

    /// <summary>A WAL or db is read a chunk at a time, never into one array (a game's 2.6 GB WAL overflowed it, and every fold
    /// failed on it): a row across two reads, a path longer than a read, and rows at the very start and end are each found.</summary>
    [Fact]
    public void Rows_are_found_across_reads_without_reading_the_file_whole()
    {
        string[] paths = [@"C:\Games\A\a.exe", @"D:\Spiele\Ä Game\Binaries\Win64\" + new string('x', 80) + ".exe", @"C:\b.exe"];
        byte[] bytes = [.. FakeD3DSCache.WalRow(paths[0]), .. Enumerable.Repeat((byte)'j', 300), .. FakeD3DSCache.WalRow(paths[1]),
            0xFF, .. @"C:\partial"u8, .. new byte[257], .. FakeD3DSCache.WalRow(paths[2])];
        for (var chunk = 1; chunk <= 64; chunk++)
        {
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            D3DSCache.ScanRows(new MemoryStream(bytes), found, chunk, after: 128);
            Assert.Equal(paths.Order(), found.Order());
        }
    }

    /// <summary>A folder the runtime made for another program, one of whose dbs can't be read without its WAL (a game that quit
    /// with its pipelines still in it), is that program's: never one of ours and never a doubt, which kept a compiled game's
    /// fold pending for good. Its WAL isn't read. A folder no db reads in is decided by the paths in its bytes.</summary>
    [Fact]
    public void Another_programs_folder_whose_db_needs_its_WAL_is_never_a_doubt()
    {
        var (d3ds, mine) = (Path.Combine(_root, "D3DSCache"), Path.Combine(_root, "games", "steam_1", "work"));
        var started = DateTime.UtcNow;
        const string game = @"c:\program files (x86)\steam\steamapps\common\Other\Other-Win64-Shipping.exe";
        var other = FakeD3DSCache.Folder(d3ds, D3DSCache.FolderName(game), game);   // its small db reads
        var adapter = Path.Combine(other, "6F75932F-7DFC-4FB0-B4B8-12DE1AC415DA_VEN_10DE&DEV_2484&SUBSYS_87BC&REV_A1.dxcache");
        File.WriteAllBytes(adapter, new byte[4096]);   // its main file alone isn't a db
        File.WriteAllBytes(adapter + "-wal", FakeD3DSCache.WalRow(game));

        string Unreadable(string name, string named)   // no db reads, its bytes name an exe
        {
            var dir = Path.Combine(d3ds, name);
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "a.dxcache"), new byte[4096]);
            File.WriteAllBytes(Path.Combine(dir, "a.dxcache-wal"), FakeD3DSCache.WalRow(named));
            return dir;
        }
        const string gone = @"C:\Games\Gone\gone.exe";
        Unreadable(D3DSCache.FolderName(gone), gone);                                       // named for another program: its
        var staged = Path.Combine(mine, "stage-1-1", "Other-Win64-Shipping.exe");
        var ours = Unreadable(D3DSCache.FolderName(staged), staged);                        // a staged exe's: the fold finds it damaged
        var unknown = Unreadable("123abc", gone);                                           // named for no path it holds: a doubt

        var doubt = new List<string>();
        using (new FileStream(adapter + "-wal", FileMode.Open, FileAccess.ReadWrite, FileShare.None))   // a read of it would be a doubt
            Assert.Equal([ours], D3DSCache.Made(d3ds, [mine], started, doubt: doubt));
        Assert.Equal([unknown], doubt);
    }

    internal const string Old = "1128466500354048:1128466500354048:NVIDIA_AMPERE_GA104";   // 621.01's, as this PC's dbs name it
    internal const string New = "1128470231318528:1128470231318528:NVIDIA_AMPERE_GA104";   // 621.12's

    /// <summary>After a driver update, a compile's staged or waiting adapter db of the earlier driver is known by its compiler:
    /// against this driver's once a warm on it told it, else, for a db older than the driver's install, against what every db of
    /// that adapter written since names. A db written since, another GPU's, and one in doubt never are.</summary>
    [Fact]
    public void An_earlier_drivers_adapter_db_is_known_by_this_drivers_compiler()
    {
        var root = Path.Combine(_root, "D3DSCache");
        var installed = DateTime.UtcNow.AddHours(-1);
        string Db(string folder, string compiler, DateTime? written = null)
        {
            var db = FakeD3DSCache.Adapter(Path.Combine(root, folder), $@"C:\Games\{folder}.exe", compiler, "0A");
            if (written is { } w) File.SetLastWriteTimeUtc(db, w);
            return db;
        }
        var staged = Path.GetDirectoryName(Db("01", Old, installed.AddDays(-4)))!;   // a fold that waited across the update
        var fresh = Path.GetDirectoryName(Db("02", New))!;                          // a compile on this driver
        Db("03", New);                                                              // what programs wrote since
        Db("04", Old, installed.AddDays(-1));                                       // written before: never counted
        var db = Path.Combine(staged, FakeD3DSCache.AdapterName);

        Assert.Equal([db], D3DSCache.EarlierDriver(staged, root, null, installed, staged: true));
        Assert.Empty(D3DSCache.EarlierDriver(fresh, root, null, installed, staged: true));   // written since: this driver's
        Assert.Empty(D3DSCache.EarlierDriver(staged, root, null, null, staged: true));       // nothing tells this driver's: merged as before
        Assert.Equal([db], D3DSCache.EarlierDriver(staged, root, New, null, staged: true));  // a warm on this driver told it
        Assert.Empty(D3DSCache.EarlierDriver(fresh, root, New, null, staged: true));
        Assert.Empty(D3DSCache.EarlierDriver(staged, root, "1:1:INTEL_XE2", null, staged: true));   // another GPU's compiler
        File.WriteAllBytes(Db("05", Old), [0x6E, 0x6F]);
        Assert.Empty(D3DSCache.EarlierDriver(Path.Combine(root, "05"), root, New, null, staged: true));   // not a db: a doubt

        // a program's WAL may hold the compiler the runtime wrote at its last open: such a db isn't counted
        string[] programs = [Path.Combine(root, "03", FakeD3DSCache.AdapterName), Path.Combine(fresh, FakeD3DSCache.AdapterName)];
        foreach (var p in programs) File.WriteAllBytes(p + "-wal", [1]);
        Assert.Empty(D3DSCache.EarlierDriver(staged, root, null, installed, staged: true));
        foreach (var p in programs) File.Delete(p + "-wal");
        Db("06", "1128470231318529:1128470231318529:NVIDIA_AMPERE_GA104");   // dbs since that disagree: not known
        Assert.Empty(D3DSCache.EarlierDriver(staged, root, null, installed, staged: true));

        // a moved install's folder waiting: the runtime made its db this driver's at the game's last run, in the WAL it left;
        // a staged db's WAL adds pipelines only, so its file tells
        var moved = Path.Combine(root, "07");
        FakeD3DSCache.Recompiled(Db("08", Old, installed.AddDays(-4)), New, moved);
        Assert.Empty(D3DSCache.EarlierDriver(moved, root, New, null, staged: false));
        Assert.Equal([Path.Combine(moved, FakeD3DSCache.AdapterName)], D3DSCache.EarlierDriver(moved, root, New, null, staged: true));
    }

    [Fact]
    public void An_Xbox_game_matches_its_WindowsApps_package_path_by_family_and_path_in_Content()
    {
        var content = @"D:\XboxGames\Fake Game\Content";
        var g = new Game("xbox:Pub.FakeGame_8wekyb3d8bbwe", "Fake", Store.Xbox, content, content + @"\Fake\Binaries\WinGDK\Fake-WinGDK-Shipping.exe");
        Assert.True(D3DSCache.IsGameExe(@"C:\Program Files\WindowsApps\Pub.FakeGame_1.0.12.0_x64__8wekyb3d8bbwe\Fake\Binaries\WinGDK\Fake-WinGDK-Shipping.exe", g));
        Assert.True(D3DSCache.IsGameExe(@"C:\Program Files\WindowsApps\PUB.FAKEGAME_1.0.9.0_x64__8wekyb3d8bbwe\fake\binaries\wingdk\fake-wingdk-shipping.exe", g));   // an older version's
        Assert.True(D3DSCache.IsGameExe(g.ExePath, g));
        Assert.False(D3DSCache.IsGameExe(@"C:\Program Files\WindowsApps\Pub.Other_1.0.12.0_x64__8wekyb3d8bbwe\Fake\Binaries\WinGDK\Fake-WinGDK-Shipping.exe", g));
        Assert.False(D3DSCache.IsGameExe(@"C:\Program Files\WindowsApps\Pub.FakeGame_1.0.12.0_x64__otherpublisher\Fake\Binaries\WinGDK\Fake-WinGDK-Shipping.exe", g));
        Assert.False(D3DSCache.IsGameExe(@"C:\Program Files\WindowsApps\Pub.FakeGame_1.0.12.0_x64__8wekyb3d8bbwe\Fake-WinGDK-Shipping.exe", g));   // another path in the package
        Assert.False(D3DSCache.IsGameExe(@"C:\Program Files\WindowsApps\Pub.FakeGame_1.0.12.0_x64__8wekyb3d8bbwe\Fake\Binaries\WinGDK\Launcher.exe", g));
        Assert.False(D3DSCache.IsGameExe(@"C:\Users\me\AppData\Local\SCSKiller\games\x\work\stage\Fake-WinGDK-Shipping.exe", g));   // a staged warm copy
        Assert.False(D3DSCache.IsGameExe(@"C:\Program Files\WindowsApps\Pub.FakeGame_1.0.12.0_x64__8wekyb3d8bbwe\Fake\Binaries\WinGDK\Fake-WinGDK-Shipping.exe",
            g with { Store = Store.Steam, Id = "steam:1" }));   // only for Xbox games
    }

    [Fact]
    public void Unreals_user_pipeline_cache_is_found_by_its_project_name_in_Saved_folders_and_never_in_the_install()
    {
        var local = Path.Combine(_root, "Local");
        var myGames = Path.Combine(_root, "My Games");
        var g = Steam(Path.Combine(local, "Programs", "Fake Game"));   // installed under %LOCALAPPDATA%\Programs
        Assert.Equal("Fake", UnrealUserCache.Project(g.ExePath));
        Assert.Null(UnrealUserCache.Project(@"C:\Games\Other\other.exe"));

        string F(params string[] parts)
        {
            var path = Path.Combine([_root, .. parts]);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, new byte[10]);
            return path;
        }
        string[] ours =
        [
            F("Local", "Fake", "Saved", "Fake_PCD3D_SM6.upipelinecache"),
            F("Local", "Iris", "Saved_Steam_7656", "fake_PCD3D_SM5.upipelinecache"),
            F("My Games", "Fake Game", "Saved", "Fake_SF_VULKAN_SM6.stable.upipelinecache"),
        ];
        F("Local", "Fake", "Saved", "FakeOther_PCD3D_SM6.upipelinecache");    // another project
        F("Local", "Fake", "Saved", "Fake_Two_PCD3D_SM6.upipelinecache");     // project "Fake_Two"
        F("Local", "Fake", "Saved", "Config", "Fake_PCD3D_SM6.upipelinecache");   // not directly in Saved
        F("Local", "Fake", "SavedGames", "Fake_PCD3D_SM6.upipelinecache");
        F("Local", "Programs", "Fake Game", "Fake", "Content", "PipelineCaches", "Windows", "Fake_PCD3D_SM6.stable.upipelinecache");   // shipped

        Assert.Equal(ours.Order(), UnrealUserCache.FilesOf(g, [local, myGames, Path.Combine(_root, "missing")]).Order());

        var inInstall = Steam(Path.Combine(_root, "Local2", "FakeInstall"));   // an install one level under a root
        F("Local2", "FakeInstall", "Saved", "Fake_PCD3D_SM6.upipelinecache");   // looks like the game's Saved, but inside the install
        var outside = F("Local2", "Fake", "Saved", "Fake_PCD3D_SM6.upipelinecache");
        Assert.Equal([outside], UnrealUserCache.FilesOf(inInstall, [Path.Combine(_root, "Local2")]));
    }
}
