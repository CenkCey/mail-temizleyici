namespace MailTemizleyici.Core;

public static class SubscriptionSync
{
    const int ChunkSize = 50;

    public static async Task<int> RunAsync(
        MailDatabase db,
        Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyDictionary<string, string>>> getHeaders,
        TimeSpan retryDelay, CancellationToken ct, int maxRetries = 6)
    {
        var latest = db.LatestUnsubscribeMessages();
        var senderOf = latest.ToDictionary(x => x.MessageId, x => x.Sender);
        var saved = 0;
        foreach (var chunk in latest.Select(x => x.MessageId).Chunk(ChunkSize))
        {
            ct.ThrowIfCancellationRequested();
            var headers = await Backoff.RetryAsync(() => getHeaders(chunk, ct), retryDelay, maxRetries, ct);
            var links = headers
                .Select(h => (Sender: senderOf[h.Key], Link: UnsubscribeLinks.Best(h.Value)))
                .Where(x => x.Link != null)
                .Select(x => (x.Sender, Link: x.Link!))
                .ToList();
            db.SaveUnsubscribeLinks(links);
            saved += links.Count;
        }
        return saved;
    }
}
