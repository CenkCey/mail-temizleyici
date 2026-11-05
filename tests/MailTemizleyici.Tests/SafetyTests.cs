using MailTemizleyici.Core;

namespace MailTemizleyici.Tests;

public class SafetyTests
{
    static readonly string[] Forbidden =
    [
        "Messages.Delete", "Messages.BatchDelete", "BatchDelete(", "Threads.Delete",
        "Scope.MailGoogleCom", "\"https://mail.google.com/\"", "Scope.GmailSettings"
    ];

    static string SourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MailTemizleyici.sln"))) dir = dir.Parent;
        return dir == null ? throw new DirectoryNotFoundException("MailTemizleyici.sln not found above the test output.") : dir.FullName;
    }

    static IEnumerable<string> SourceFiles() =>
        Directory.EnumerateFiles(Path.Combine(SourceRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    [Fact]
    public void Source_NeverUsesPermanentDeleteOrBroadScopes()
    {
        var hits = SourceFiles()
            .SelectMany(f => Forbidden.Where(File.ReadAllText(f).Contains).Select(p => $"{Path.GetFileName(f)}: {p}"))
            .ToList();
        Assert.Empty(hits);
    }

    [Fact]
    public void Source_RequestsOnlyReadonlyAndModifyScopes()
    {
        Assert.Equal(["https://www.googleapis.com/auth/gmail.readonly"], GmailConnection.Scopes);
        Assert.Equal(["https://www.googleapis.com/auth/gmail.modify"], GmailConnection.ModifyScopes);
    }

    [Fact]
    public void Source_FindsItsOwnFiles()
    {
        Assert.NotEmpty(SourceFiles());
    }
}
