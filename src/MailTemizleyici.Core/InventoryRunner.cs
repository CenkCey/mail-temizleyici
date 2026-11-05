namespace MailTemizleyici.Core;

public sealed record IdPage(IReadOnlyList<string> Ids, string? NextPageToken);

public sealed class InventoryRunner(
    MailDatabase db,
    Func<string?, CancellationToken, Task<IdPage>> listPage,
    Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyList<MessageRow>>> getMetadata,
    TimeSpan retryDelay,
    int maxRetries = 6)
{
    public async Task RunAsync(IProgress<int>? progress, CancellationToken ct)
    {
        var state = db.LoadState();
        if (state?.FinishedAt != null)
        {
            progress?.Report(state.FetchedCount);
            return;
        }
        var started = state?.StartedAt ?? DateTime.UtcNow.ToString("o");
        var token = state?.PageToken;
        var fetched = state?.FetchedCount ?? 0;

        do
        {
            ct.ThrowIfCancellationRequested();
            var page = await Backoff.RetryAsync(() => listPage(token, ct), retryDelay, maxRetries, ct);
            var rows = await Backoff.RetryAsync(() => getMetadata(page.Ids, ct), retryDelay, maxRetries, ct);
            db.Upsert(rows);
            fetched += page.Ids.Count;
            token = page.NextPageToken;
            db.SaveState(new InventoryState(token, fetched, started, token == null ? DateTime.UtcNow.ToString("o") : null));
            progress?.Report(fetched);
        } while (token != null);
    }
}
