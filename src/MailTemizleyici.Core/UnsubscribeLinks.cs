using System.Text.RegularExpressions;

namespace MailTemizleyici.Core;

public static partial class UnsubscribeLinks
{
    public static string? Best(string header)
    {
        string? mailto = null;
        foreach (Match m in AngleBrackets().Matches(header))
        {
            var value = m.Groups[1].Value.Trim();
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) continue;
            if (uri.Scheme is "https" or "http") return value;
            if (uri.Scheme == "mailto") mailto ??= value;
        }
        return mailto;
    }

    public static bool IsOpenable(string link) =>
        Uri.TryCreate(link, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" or "mailto";

    [GeneratedRegex("<([^>]+)>")]
    private static partial Regex AngleBrackets();
}
