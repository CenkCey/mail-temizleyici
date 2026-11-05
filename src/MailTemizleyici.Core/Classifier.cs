namespace MailTemizleyici.Core;

public enum Decision { Keep, Trash, Undecided }

public sealed class SenderStats
{
    public string Address { get; set; } = "";
    public string Domain { get; set; } = "";
    public int Count { get; set; }
    public long TotalSize { get; set; }
    public long FirstDate { get; set; } = long.MaxValue;
    public long LastDate { get; set; }
    public int ReadCount { get; set; }
    public bool AnyStarred { get; set; }
    public bool AnyUserLabel { get; set; }
    public bool AnyKeyword { get; set; }
    public bool AnyAttachment { get; set; }
    public bool AnyUnsubscribe { get; set; }
    public int PromoSocialCount { get; set; }
    public int UpdatesCount { get; set; }
    public bool Replied { get; set; }

    public double ReadRatio => Count == 0 ? 0 : (double)ReadCount / Count;
}

public static class Classifier
{
    public const int NoRule = 11;
    const long DayMs = 86_400_000;
    static readonly string[] NoReplyTokens = ["noreply", "no-reply", "donotreply", "notification", "bildirim"];

    public static readonly string[] DefaultAllowlist = ["turkiye.gov.tr", "gib.gov.tr", "google.com"];

    public static readonly string[] SubjectKeywords =
    [
        "fatura", "e-fatura", "dekont", "makbuz", "sözleşme", "poliçe", "bilet", "rezervasyon", "garanti",
        "sipariş", "şifre", "doğrulama", "invoice", "receipt", "contract", "ticket", "warranty"
    ];

    static readonly System.Globalization.CompareInfo TrCompare = System.Globalization.CultureInfo.GetCultureInfo("tr-TR").CompareInfo;

    public static bool HasKeyword(string subject) =>
        SubjectKeywords.Any(k =>
            subject.Contains(k, StringComparison.OrdinalIgnoreCase) ||
            TrCompare.IndexOf(subject, k, System.Globalization.CompareOptions.IgnoreCase) >= 0);

    public static (Decision Decision, int Rule) Classify(SenderStats s, long nowMs, IReadOnlyCollection<string> allowlist)
    {
        if (s.AnyStarred) return (Decision.Keep, 1);
        if (s.Replied) return (Decision.Keep, 2);
        if (s.AnyUserLabel) return (Decision.Keep, 3);
        if (allowlist.Any(d => s.Domain == d || s.Domain.EndsWith("." + d, StringComparison.Ordinal))) return (Decision.Keep, 4);
        if (s.AnyKeyword) return (Decision.Keep, 5);
        if (s.AnyAttachment) return (Decision.Keep, 6);
        if (s.PromoSocialCount == s.Count) return (Decision.Trash, 7);
        if (s.AnyUnsubscribe && s.ReadRatio < 0.10) return (Decision.Trash, 8);
        if (NoReplyTokens.Any(t => s.Address.Contains(t, StringComparison.Ordinal)) && s.LastDate < nowMs - 365 * DayMs)
            return (Decision.Trash, 9);
        if (s.UpdatesCount == s.Count && s.LastDate < nowMs - 730 * DayMs) return (Decision.Trash, 10);
        return (Decision.Undecided, NoRule);
    }

    public static void SaveAllowlist(string path, IEnumerable<string> domains)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllLines(path, domains.Select(d => d.Trim().TrimStart('@').ToLowerInvariant())
            .Where(d => d != "" && !d.Any(char.IsWhiteSpace)).Distinct());
    }

    public static IReadOnlyCollection<string> LoadAllowlist(string path) =>
        File.Exists(path)
            ? File.ReadAllLines(path).Select(l => l.Trim().ToLowerInvariant()).Where(l => l != "").ToList()
            : DefaultAllowlist;
}
