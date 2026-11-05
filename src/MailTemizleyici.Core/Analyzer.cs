namespace MailTemizleyici.Core;

public sealed record SenderSummary(
    string SenderAddress, string Domain, int Count, long TotalSize, long FirstDate, long LastDate,
    double ReadRatio, bool UserReplied, bool HasUnsubscribe, Decision Suggested, int Rule, Decision? UserDecision)
{
    public Decision Effective => UserDecision ?? Suggested;
}

public static class Analyzer
{
    public static void Run(MailDatabase db, DateTimeOffset now, IReadOnlyCollection<string> allowlist)
    {
        var stats = new Dictionary<string, SenderStats>();
        var sent = new HashSet<string>();

        foreach (var m in db.ReadMessages())
        {
            var labels = m.Labels.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (labels.Contains("SENT") || labels.Contains("DRAFT"))
            {
                foreach (var r in m.Recipients.Split(',', StringSplitOptions.RemoveEmptyEntries)) sent.Add(r);
                continue;
            }
            if (!stats.TryGetValue(m.SenderAddress, out var s))
                stats[m.SenderAddress] = s = new SenderStats { Address = m.SenderAddress, Domain = m.Domain };
            s.Count++;
            s.TotalSize += m.Size;
            s.FirstDate = Math.Min(s.FirstDate, m.Date);
            s.LastDate = Math.Max(s.LastDate, m.Date);
            if (m.IsRead) s.ReadCount++;
            s.AnyStarred |= m.IsStarred;
            s.AnyUserLabel |= labels.Any(l => l.StartsWith("Label_", StringComparison.Ordinal));
            s.AnyKeyword |= Classifier.HasKeyword(m.Subject);
            s.AnyAttachment |= m.HasAttachment;
            s.AnyUnsubscribe |= m.HasUnsubscribe;
            if (m.Category is "PROMOTIONS" or "SOCIAL") s.PromoSocialCount++;
            if (m.Category == "UPDATES") s.UpdatesCount++;
        }

        var nowMs = now.ToUnixTimeMilliseconds();
        var summaries = stats.Values.Select(s =>
        {
            s.Replied = sent.Contains(s.Address);
            var (decision, rule) = Classifier.Classify(s, nowMs, allowlist);
            return new SenderSummary(s.Address, s.Domain, s.Count, s.TotalSize, s.FirstDate, s.LastDate,
                s.ReadRatio, s.Replied, s.AnyUnsubscribe, decision, rule, null);
        }).ToList();

        db.SaveAnalysis(summaries, sent);
    }
}
