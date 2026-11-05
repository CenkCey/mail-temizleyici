namespace MailTemizleyici.Core;

public static class AppPaths
{
    public const string DataDirVariable = "MAILTEMIZLEYICI_DATA_DIR";

    static string? active;
    static bool activeLoaded;

    public static string Dir => Environment.GetEnvironmentVariable(DataDirVariable) is { Length: > 0 } custom
        ? custom
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MailTemizleyici");

    public static string ClientSecret => Path.Combine(Dir, "client_secret.json");

    public static string Allowlist => Path.Combine(Dir, "allowlist.txt");

    static string ActiveFile => Path.Combine(Dir, "active_account.txt");

    public static string? ActiveAccount
    {
        get
        {
            if (!activeLoaded)
            {
                var text = File.Exists(ActiveFile) ? File.ReadAllText(ActiveFile).Trim() : "";
                active = text.Length > 0 ? text : null;
                activeLoaded = true;
            }
            return active;
        }
    }

    public static string Database => DatabaseFor(ActiveAccount);

    public static string DatabaseFor(string? email)
    {
        var name = email is null
            ? "hesap-yok"
            : string.Concat(email.ToLowerInvariant().Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return Path.Combine(Dir, "veri", name + ".db");
    }

    public static void SetActiveAccount(string email)
    {
        active = email.ToLowerInvariant();
        activeLoaded = true;
        Directory.CreateDirectory(Dir);
        File.WriteAllText(ActiveFile, active);
    }
}
