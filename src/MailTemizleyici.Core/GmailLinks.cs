namespace MailTemizleyici.Core;

public static class GmailLinks
{
    public static string Thread(string? account, string threadId) =>
        $"https://mail.google.com/mail/{(account is null ? "u/0/" : "?authuser=" + Uri.EscapeDataString(account))}#all/{Uri.EscapeDataString(threadId)}";

    public static string FromQuery(IEnumerable<string> addresses) =>
        $"from:({string.Join(" OR ", addresses.Distinct())})";
}
