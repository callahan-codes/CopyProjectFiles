using CopyProjectFiles.Core.Data;
using CopyProjectFiles.Core.Models;
using CopyProjectFiles.Core.Services;

namespace CopyProjectFiles.Core.Tests;

public class IgnoreRulesTests
{
    [Theory]
    [InlineData("bin", "bin", true)]
    [InlineData("bin/Debug/app.dll", "bin", true)]
    [InlineData("binary.txt", "bin", false)]
    [InlineData("src/obj", "src/obj", true)]
    [InlineData("obj", "src/obj", false)]
    [InlineData("src\\obj\\x.cs", "src/obj", true)]
    public void IsIgnored(string path, string ignore, bool expected) =>
        Assert.Equal(expected, IgnoreRules.IsIgnored(path, [ignore]));

    [Fact]
    public void Simplify_drops_children_of_ignored_folders() =>
        Assert.Equal(["bin", "src/obj"], IgnoreRules.Simplify(["bin/Debug", "bin", "src/obj", "/bin/"]));

    [Fact]
    public void Case_sensitivity_follows_os()
    {
        bool ignored = IgnoreRules.IsIgnored("Bin/x", ["bin"]);
        Assert.Equal(!PathComparison.IsCaseSensitive, ignored);
    }
}

public class ProjectCopyTests
{
    [Fact]
    public void PathsToIgnore_round_trips_through_json_and_updates_count()
    {
        var p = new ProjectCopy { PathsToIgnore = ["bin\\", "obj", "obj"] };
        Assert.Equal("[\"bin\",\"obj\"]", p.PathsToIgnoreJson);
        Assert.Equal(2, p.NumberOfPathsToIgnore);

        var loaded = new ProjectCopy { PathsToIgnoreJson = p.PathsToIgnoreJson };
        Assert.Equal(["bin", "obj"], loaded.PathsToIgnore);
    }

    [Fact]
    public void Corrupt_json_reads_as_empty() =>
        Assert.Empty(new ProjectCopy { PathsToIgnoreJson = "not json" }.PathsToIgnore);
}

public class ValidatorTests
{
    [Fact]
    public void Valid_project_has_no_errors()
    {
        using var t = new TempDir();
        Directory.CreateDirectory(t.Path("src"));
        var p = new ProjectCopy { Name = "a", Path = t.Path("src"), CopyToPath = t.Path("out") };
        Assert.Empty(ProjectValidator.Validate(p));
    }

    [Fact]
    public void Destination_inside_source_is_rejected()
    {
        using var t = new TempDir();
        Directory.CreateDirectory(t.Path("src"));
        var p = new ProjectCopy { Name = "a", Path = t.Path("src"), CopyToPath = t.Path("src", "out") };
        Assert.Contains(ProjectValidator.Validate(p), e => e.Field == ProjectField.CopyToPath);
    }

    [Fact]
    public void Destination_that_contains_source_is_rejected()
    {
        // otherwise "clear destination" would delete the project itself
        using var t = new TempDir();
        Directory.CreateDirectory(t.Path("src"));
        var p = new ProjectCopy { Name = "a", Path = t.Path("src"), CopyToPath = t.Root };
        Assert.Contains(ProjectValidator.Validate(p), e => e.Field == ProjectField.CopyToPath);
    }

    [Fact]
    public void Similar_prefix_is_not_treated_as_nested()
    {
        using var t = new TempDir();
        Directory.CreateDirectory(t.Path("src"));
        var p = new ProjectCopy { Name = "a", Path = t.Path("src"), CopyToPath = t.Path("src-copy") };
        Assert.Empty(ProjectValidator.Validate(p));
    }

    [Fact]
    public void Missing_fields_are_reported()
    {
        var errors = ProjectValidator.Validate(new ProjectCopy());
        Assert.Equal(3, errors.Count);
    }
}

public class CopyEngineTests
{
    private static ProjectCopy MakeProject(TempDir t, params string[] ignore)
    {
        t.File("src/Program.cs");
        t.File("src/.env", "secret");
        t.File("src/.gitignore");
        t.File("src/bin/Debug/app.dll");
        t.File("src/bin/Debug/app.pdb");
        t.File("src/lib/Program.cs", "dup");
        t.File("src/lib/Util.cs");
        return new ProjectCopy { Name = "p", Path = t.Path("src"), CopyToPath = t.Path("out"), PathsToIgnore = ignore };
    }

    [Fact]
    public void Plan_skips_ignored_and_counts_them()
    {
        using var t = new TempDir();
        var plan = CopyEngine.Plan(MakeProject(t, "bin", ".env"));

        Assert.Equal(3, plan.IgnoredCount);
        Assert.Equivalent(
            new[] { ".gitignore", "Program.cs", "lib/Program.cs", "lib/Util.cs" },
            plan.Files.Select(f => f.RelativePath));
    }

    [Fact]
    public async Task Keep_structure_mirrors_folders()
    {
        using var t = new TempDir();
        var plan = CopyEngine.Plan(MakeProject(t, "bin"));
        var result = await CopyEngine.ExecuteAsync(plan, new CopyOptions { KeepStructure = true });

        Assert.Equal(5, result.Copied);
        Assert.Equal(0, result.Failed);
        Assert.True(File.Exists(t.Path("out", "lib", "Util.cs")));
        Assert.True(File.Exists(t.Path("out", ".env"))); // hidden files are included unless ignored
        Assert.False(Directory.Exists(t.Path("out", "bin")));
    }

    [Fact]
    public async Task Flatten_renames_collisions()
    {
        using var t = new TempDir();
        var plan = CopyEngine.Plan(MakeProject(t, "bin"));
        var result = await CopyEngine.ExecuteAsync(plan, new CopyOptions { KeepStructure = false });

        Assert.Equal(5, result.Copied);
        Assert.True(File.Exists(t.Path("out", "Program.cs")));
        Assert.True(File.Exists(t.Path("out", "Program (1).cs")));
        Assert.Empty(Directory.GetDirectories(t.Path("out")));
    }

    [Fact]
    public async Task Clear_destination_removes_old_contents()
    {
        using var t = new TempDir();
        t.File("out/stale.txt");
        t.File("out/old/deep.txt");
        var plan = CopyEngine.Plan(MakeProject(t, "bin"));

        await CopyEngine.ExecuteAsync(plan, new CopyOptions { KeepStructure = false, ClearDestination = true });

        Assert.False(File.Exists(t.Path("out", "stale.txt")));
        Assert.False(Directory.Exists(t.Path("out", "old")));
    }

    [Fact]
    public async Task Refuses_to_run_with_unsafe_destination()
    {
        using var t = new TempDir();
        var project = MakeProject(t);
        project.CopyToPath = t.Root; // parent of the source
        var plan = CopyEngine.Plan(project);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CopyEngine.ExecuteAsync(plan, new CopyOptions { ClearDestination = true }));
        Assert.True(File.Exists(t.Path("src", "Program.cs")));
    }

    [Fact]
    public async Task Reports_progress()
    {
        using var t = new TempDir();
        var plan = CopyEngine.Plan(MakeProject(t, "bin"));
        var reports = new List<CopyProgress>();
        var progress = new SyncProgress<CopyProgress>(reports.Add);

        await CopyEngine.ExecuteAsync(plan, new CopyOptions(), progress);

        Assert.Equal(plan.Files.Count, reports.Count);
        Assert.Equal(plan.Files.Count, reports[^1].Done);
    }

    [Fact]
    public void Symlinked_folder_loop_does_not_hang()
    {
        if (OperatingSystem.IsWindows()) return; // creating symlinks needs admin/dev mode there
        using var t = new TempDir();
        t.File("src/a.txt");
        Directory.CreateSymbolicLink(t.Path("src", "loop"), t.Path("src"));

        var plan = CopyEngine.Plan(new ProjectCopy { Name = "p", Path = t.Path("src"), CopyToPath = t.Path("out") });
        Assert.Equal("a.txt", Assert.Single(plan.Files).RelativePath);
    }

    private sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}

public class FileTreeServiceTests
{
    [Fact]
    public void Lists_folders_first_with_relative_paths()
    {
        using var t = new TempDir();
        t.File("b.txt");
        t.File("a/inner.txt");

        var root = FileTreeService.GetChildren(t.Root);
        Assert.Equal(["a", "b.txt"], root.Select(e => e.RelativePath));
        Assert.True(root[0].IsDirectory);

        var inner = FileTreeService.GetChildren(t.Root, "a");
        Assert.Equal("a/inner.txt", Assert.Single(inner).RelativePath);
        Assert.True(FileTreeService.HasChildren(t.Root, "a"));
    }

    [Fact]
    public void Missing_folder_returns_empty() =>
        Assert.Empty(FileTreeService.GetChildren("/definitely/not/here"));
}

public class IgnoreEditorTests
{
    private static TempDir MakeTree()
    {
        var t = new TempDir();
        t.File("a/x.txt");
        t.File("a/y.txt");
        t.File("a/sub/z.txt");
        t.File("a/sub/w.txt");
        t.File("b.txt");
        return t;
    }

    [Fact]
    public void Exclude_folder_absorbs_children() =>
        Assert.Equal(["a"], IgnoreEditor.Exclude(["a/x.txt", "a/sub"], "a"));

    [Fact]
    public void Include_folder_clears_everything_inside()
    {
        using var t = MakeTree();
        Assert.Equal(["b.txt"], IgnoreEditor.Include(t.Root, ["a/x.txt", "a/sub", "b.txt"], "a"));
    }

    [Fact]
    public void Include_file_inside_ignored_folder_keeps_siblings_ignored()
    {
        using var t = MakeTree();
        var result = IgnoreEditor.Include(t.Root, ["a"], "a/sub/z.txt");
        Assert.Equivalent(new[] { "a/x.txt", "a/y.txt", "a/sub/w.txt" }, result);

        // and the plan agrees: only z.txt and b.txt get copied
        var plan = CopyEngine.Plan(new ProjectCopy { Path = t.Root, PathsToIgnore = result });
        Assert.Equivalent(new[] { "a/sub/z.txt", "b.txt" }, plan.Files.Select(f => f.RelativePath));
    }

    [Fact]
    public void GetState_reports_partial_folders()
    {
        string[] ignores = ["a/sub/w.txt"];
        Assert.Null(IgnoreEditor.GetState(ignores, "a", isDirectory: true));
        Assert.Null(IgnoreEditor.GetState(ignores, "a/sub", isDirectory: true));
        Assert.False(IgnoreEditor.GetState(ignores, "a/sub/w.txt", isDirectory: false));
        Assert.True(IgnoreEditor.GetState(ignores, "a/x.txt", isDirectory: false));
        Assert.True(IgnoreEditor.GetState(ignores, "ab", isDirectory: true));
    }
}

public class RepositoryTests
{
    [Fact]
    public async Task Crud_round_trip()
    {
        using var t = new TempDir();
        await using var repo = new SqliteProjectRepository(t.Path("test.db"));

        var added = await repo.AddAsync(new ProjectCopy { Name = "one", Path = "/a", CopyToPath = "/b", PathsToIgnore = ["bin"] });
        Assert.True(added.Id > 0);

        var loaded = await repo.GetAsync(added.Id);
        Assert.NotNull(loaded);
        Assert.Equal(["bin"], loaded!.PathsToIgnore);

        loaded.Name = "renamed";
        loaded.PathsToIgnore = ["bin", "obj"];
        Assert.True(await repo.UpdateAsync(loaded));

        var all = await repo.GetAllAsync();
        Assert.Equal("renamed", Assert.Single(all).Name);
        Assert.Equal(2, all[0].NumberOfPathsToIgnore);

        Assert.True(await repo.DeleteAsync(added.Id));
        Assert.Empty(await repo.GetAllAsync());
        Assert.False(await repo.DeleteAsync(added.Id));
    }
}
