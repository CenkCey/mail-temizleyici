using MailTemizleyici.Core;

namespace MailTemizleyici.Tests;

public class ConnectionTests
{
    [Fact]
    public void Scopes_AreReadonlyOnly()
    {
        Assert.Equal(["https://www.googleapis.com/auth/gmail.readonly"], GmailConnection.Scopes);
    }

    [Fact]
    public void ClientSecret_LivesInAppDataFolder()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        Assert.Equal(Path.Combine(appData, "MailTemizleyici", "client_secret.json"), AppPaths.ClientSecret);
        Assert.StartsWith(AppPaths.Dir, AppPaths.ClientSecret);
    }

    [Fact]
    public void DatabaseFor_NoAccount_UsesPlaceholderFile()
    {
        Assert.Equal(Path.Combine(AppPaths.Dir, "veri", "hesap-yok.db"), AppPaths.DatabaseFor(null));
    }

    [Fact]
    public void DatabaseFor_Account_IsOneFilePerAccountIgnoringCase()
    {
        Assert.Equal(Path.Combine(AppPaths.Dir, "veri", "me@example.com.db"), AppPaths.DatabaseFor("Me@example.com"));
        Assert.NotEqual(AppPaths.DatabaseFor("a@example.com"), AppPaths.DatabaseFor("b@example.com"));
    }

    [Fact]
    public void DatabaseFor_InvalidFileNameChars_AreReplaced()
    {
        var file = Path.GetFileName(AppPaths.DatabaseFor("a/b:c@x.com"));
        Assert.Equal("a_b_c@x.com.db", file);
    }

    [Fact]
    public async Task Connect_WithoutClientSecret_Throws()
    {
        if (File.Exists(AppPaths.ClientSecret)) return;
        await Assert.ThrowsAsync<FileNotFoundException>(() => GmailConnection.ConnectAsync(CancellationToken.None));
    }
}
