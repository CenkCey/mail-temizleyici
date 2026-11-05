namespace MailTemizleyici.Core;

public static class AttachmentSync
{
    public static async Task<int> RunAsync(
        MailDatabase db, Func<string?, CancellationToken, Task<IdPage>> listPage,
        TimeSpan retryDelay, CancellationToken ct, int maxRetries = 6)
    {
        var ids = new List<string>();
        string? token = null;
        do
        {
            ct.ThrowIfCancellationRequested();
            var page = await Backoff.RetryAsync(() => listPage(token, ct), retryDelay, maxRetries, ct);
            ids.AddRange(page.Ids);
            token = page.NextPageToken;
        } while (token != null);
        db.MarkAttachments(ids);
        return ids.Count;
    }
}
