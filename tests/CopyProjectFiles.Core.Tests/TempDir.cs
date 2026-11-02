namespace CopyProjectFiles.Core.Tests;

/// <summary>A throwaway folder that deletes itself after the test.</summary>
internal sealed class TempDir : IDisposable
{
    public string Root { get; } = Directory.CreateTempSubdirectory("cpf-test-").FullName;

    public string Path(params string[] parts) => System.IO.Path.Combine([Root, .. parts]);

    public string File(string relative, string content = "x")
    {
        string full = Path(relative.Split('/'));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        System.IO.File.WriteAllText(full, content);
        return full;
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); } catch { }
    }
}
