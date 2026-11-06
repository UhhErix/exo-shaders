using SCSKiller.Core.App;

namespace SCSKiller.Tests.Platform;

/// <summary>The kept work (a stopped compile's dbs, beside work\) is moved in and out only of a work folder
/// <see cref="ScsKiller.IsWorkDir"/> names: any other folder is left exactly as it is, kept copy included.</summary>
public sealed class KeptWorkTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "scskiller-keptwork-" + Guid.NewGuid().ToString("N"));

    public KeptWorkTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, true); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    [Theory]
    [InlineData("work", true)]
    [InlineData("work-2", true)]
    [InlineData(ScsKiller.VerifyWork, true)]
    [InlineData("work-", false)]
    [InlineData("work-x", false)]
    [InlineData("worker", false)]
    [InlineData("kept-work", false)]
    [InlineData("stage-1", false)]
    public void IsWorkDir_names_only_the_work_folders(string name, bool expected) =>
        Assert.Equal(expected, ScsKiller.IsWorkDir(name));

    [Fact]
    public void KeepWork_then_TakeKeptWork_moves_the_work_files_out_and_back()
    {
        var work = Path.Combine(_root, "work");
        var kept = Path.Combine(_root, "kept-work");
        Directory.CreateDirectory(work);
        File.WriteAllText(Path.Combine(work, "scskiller.db"), "dbs");

        ScsKiller.KeepWork(work, kept, () => "made-from-x", null);

        Assert.False(File.Exists(Path.Combine(work, "scskiller.db")));
        Assert.True(File.Exists(Path.Combine(kept, "scskiller.db")));
        Assert.True(ScsKiller.TakeKeptWork(kept, work, "made-from-x", null));
        Assert.Equal("dbs", File.ReadAllText(Path.Combine(work, "scskiller.db")));
        Assert.False(Directory.Exists(kept));
    }

    [Fact]
    public void KeepWork_moves_nothing_and_deletes_no_kept_work_for_a_folder_that_is_not_a_work_folder()
    {
        var notWork = Path.Combine(_root, "notes");
        var kept = Path.Combine(_root, "kept-work");
        Directory.CreateDirectory(notWork);
        Directory.CreateDirectory(kept);
        File.WriteAllText(Path.Combine(notWork, "scskiller.db"), "mine");
        File.WriteAllText(Path.Combine(kept, "scskiller.db"), "earlier");

        ScsKiller.KeepWork(notWork, kept, () => "made-from-x", null);

        Assert.Equal("mine", File.ReadAllText(Path.Combine(notWork, "scskiller.db")));
        Assert.Equal("earlier", File.ReadAllText(Path.Combine(kept, "scskiller.db")));
    }

    [Fact]
    public void TakeKeptWork_leaves_the_kept_work_alone_for_a_folder_that_is_not_a_work_folder()
    {
        var work = Path.Combine(_root, "work");
        var kept = Path.Combine(_root, "kept-work");
        var notWork = Path.Combine(_root, "notes");
        Directory.CreateDirectory(work);
        File.WriteAllText(Path.Combine(work, "scskiller.db"), "dbs");
        ScsKiller.KeepWork(work, kept, () => "made-from-x", null);

        Assert.False(ScsKiller.TakeKeptWork(kept, notWork, "made-from-x", null));

        Assert.False(Directory.Exists(notWork));
        Assert.Equal("dbs", File.ReadAllText(Path.Combine(kept, "scskiller.db")));
    }
}
