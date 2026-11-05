namespace MailTemizleyici.Core;

public sealed record ChunkResult<T>(IReadOnlyList<T> Items, IReadOnlyList<string> Failed, bool RateLimited, string? Error);

public static class ThrottledFetch
{
    public static async Task<List<T>> RunAsync<T>(
        IReadOnlyList<string> ids, Throttle throttle, int unitsPerItem,
        Func<IReadOnlyList<string>, CancellationToken, Task<ChunkResult<T>>> fetch,
        Action<IReadOnlyList<T>>? onItems, int maxFailedRounds, CancellationToken ct)
    {
        var all = new List<T>();
        var queue = new Queue<string>(ids);
        var failedRounds = 0;
        while (queue.Count > 0)
        {
            var chunk = new List<string>();
            while (chunk.Count < throttle.BatchSize && queue.Count > 0) chunk.Add(queue.Dequeue());
            await throttle.AcquireAsync(chunk.Count * unitsPerItem, ct);
            var result = await fetch(chunk, ct);
            all.AddRange(result.Items);
            if (result.Items.Count > 0) onItems?.Invoke(result.Items);
            if (result.Failed.Count == 0)
            {
                failedRounds = 0;
                throttle.OnSuccess();
                continue;
            }
            foreach (var id in result.Failed) queue.Enqueue(id);
            throttle.OnFailure(result.RateLimited);
            failedRounds = result.Items.Count > 0 ? 0 : failedRounds + 1;
            if (failedRounds > maxFailedRounds)
                throw new InvalidOperationException($"Gmail kept rejecting the requests: {result.Error}");
        }
        return all;
    }
}
