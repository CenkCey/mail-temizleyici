using Google;

namespace MailTemizleyici.Core;

public sealed class InventoryUnfinishedException() : Exception("Inventory is not finished.");

public sealed record TrashCandidate(string Id, string Sender, long Size, bool IsStarred);

public sealed record BatchInfo(int BatchNo, long Date, int Total, int Active, long Size);

public sealed record LoggedMessage(string Id, string Labels);

public sealed record PlanGroup(string Sender, IReadOnlyList<string> Ids, long Size, int StarredCount = 0);

public sealed record TrashPlan(IReadOnlyList<PlanGroup> Groups, int SkippedStarred)
{
    public int MessageCount => Groups.Sum(g => g.Ids.Count);
    public int IncludedStarred => Groups.Sum(g => g.StarredCount);
    public long TotalSize => Groups.Sum(g => g.Size);
    public IEnumerable<string> Ids => Groups.SelectMany(g => g.Ids);

    public static TrashPlan Build(MailDatabase db, bool includeStarred = false)
    {
        if (db.LoadState()?.FinishedAt == null) throw new InventoryUnfinishedException();
        var candidates = db.GetTrashCandidates();
        var groups = candidates
            .Where(c => includeStarred || !c.IsStarred)
            .GroupBy(c => c.Sender)
            .Select(g => new PlanGroup(g.Key, g.Select(c => c.Id).ToList(), g.Sum(c => c.Size), g.Count(c => c.IsStarred)))
            .ToList();
        return new TrashPlan(groups, includeStarred ? 0 : candidates.Count(c => c.IsStarred));
    }

    public TrashPlan Trial(int maxMessages = 100)
    {
        var picked = new List<PlanGroup>();
        var total = 0;
        foreach (var g in Groups.OrderBy(g => g.Ids.Count))
        {
            if (picked.Count > 0 && total + g.Ids.Count > maxMessages) break;
            picked.Add(g);
            total += g.Ids.Count;
        }
        return this with { Groups = picked };
    }
}

public sealed class TrashRunner(
    MailDatabase db,
    Func<IReadOnlyList<string>, CancellationToken, Task> trash,
    TimeSpan retryDelay,
    int chunkSize = 1000,
    int maxRetries = 6)
{
    public async Task<int> RunAsync(TrashPlan plan, IProgress<int>? progress, CancellationToken ct)
    {
        var batchNo = db.NextBatchNo();
        var done = 0;
        foreach (var chunk in plan.Ids.Chunk(chunkSize))
        {
            ct.ThrowIfCancellationRequested();
            db.LogActions(chunk, "Trash", batchNo);
            try
            {
                await Backoff.RetryAsync(async () => { await trash(chunk, ct); return true; }, retryDelay, maxRetries, ct);
            }
            catch (GoogleApiException)
            {
                db.RemoveLog(chunk, batchNo);
                throw;
            }
            done += chunk.Length;
            progress?.Report(done);
        }
        return batchNo;
    }
}

public sealed class UndoRunner(
    MailDatabase db,
    Func<IReadOnlyList<string>, IReadOnlyList<string>, CancellationToken, Task> restore,
    TimeSpan retryDelay,
    int chunkSize = 1000,
    int maxRetries = 6)
{
    static readonly string[] RestorableSystem = ["INBOX", "UNREAD", "STARRED", "IMPORTANT"];

    public static string[] RestorableLabels(string labels) =>
        labels.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => RestorableSystem.Contains(l) || l.StartsWith("CATEGORY_", StringComparison.Ordinal) || l.StartsWith("Label_", StringComparison.Ordinal))
            .Order().ToArray();

    public async Task<int> RunAsync(int batchNo, IProgress<int>? progress, CancellationToken ct)
    {
        var done = 0;
        foreach (var group in db.GetActiveBatchMessages(batchNo).GroupBy(m => string.Join(",", RestorableLabels(m.Labels))))
        {
            var addLabels = group.Key.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var chunk in group.Select(m => m.Id).Chunk(chunkSize))
            {
                ct.ThrowIfCancellationRequested();
                await Backoff.RetryAsync(async () => { await restore(chunk, addLabels, ct); return true; }, retryDelay, maxRetries, ct);
                db.MarkUndone(chunk, batchNo);
                done += chunk.Length;
                progress?.Report(done);
            }
        }
        return done;
    }
}
