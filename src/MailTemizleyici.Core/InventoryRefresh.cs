namespace MailTemizleyici.Core;

public static class InventoryRefresh
{
    public static async Task<int> RunAsync(
        MailDatabase db,
        Func<string?, CancellationToken, Task<IdPage>> listPage,
        Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyList<MessageRow>>> getMetadata,
        TimeSpan retryDelay, IProgress<int>? progress, CancellationToken ct, int maxRetries = 6)
    {
        var state = db.LoadState();
        if (state?.FinishedAt == null) throw new InvalidOperationException("Inventory is not finished.");

        var added = 0;
        string? token = null;
        do
        {
            ct.ThrowIfCancellationRequested();
            var page = await Backoff.RetryAsync(() => listPage(token, ct), retryDelay, maxRetries, ct);
            var known = db.KnownIds(page.Ids);
            var fresh = page.Ids.Where(id => !known.Contains(id)).ToList();
            if (fresh.Count > 0)
            {
                var rows = await Backoff.RetryAsync(() => getMetadata(fresh, ct), retryDelay, maxRetries, ct);
                db.Upsert(rows);
                added += fresh.Count;
                progress?.Report(added);
            }
            token = fresh.Count < page.Ids.Count ? null : page.NextPageToken;
        } while (token != null);

        db.SaveState(state with { FetchedCount = db.Count() });
        return added;
    }
}
