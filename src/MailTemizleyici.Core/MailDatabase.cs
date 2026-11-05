using Microsoft.Data.Sqlite;

namespace MailTemizleyici.Core;

public sealed record MessageRow(
    string Id, string ThreadId, string SenderAddress, string SenderName, string Domain,
    string Subject, long Date, long Size, string Labels, bool IsRead, bool IsStarred,
    bool IsImportant, string Category, bool HasUnsubscribe, bool HasAttachment, string Recipients = "");

public sealed record SenderMail(string Id, string ThreadId, string Subject, long Date, long Size, bool IsRead, bool IsStarred);

public sealed record InventoryState(string? PageToken, int FetchedCount, string StartedAt, string? FinishedAt);

public sealed class AccountMismatchException(string? stored, string current)
    : Exception($"Database belongs to {stored ?? "an unknown account"}, signed in as {current}.")
{
    public string? Stored { get; } = stored;
    public string Current { get; } = current;
}

public sealed class MailDatabase : IDisposable
{
    readonly SqliteConnection _db;

    public MailDatabase(string path)
    {
        if (path != ":memory:") Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        _db = new SqliteConnection($"Data Source={path};Pooling=False");
        _db.Open();
        Exec("""
            CREATE TABLE IF NOT EXISTS message (
                id TEXT PRIMARY KEY, thread_id TEXT NOT NULL, sender_address TEXT NOT NULL,
                sender_name TEXT NOT NULL, domain TEXT NOT NULL, subject TEXT NOT NULL,
                date INTEGER NOT NULL, size INTEGER NOT NULL, labels TEXT NOT NULL,
                is_read INTEGER NOT NULL, is_starred INTEGER NOT NULL, is_important INTEGER NOT NULL,
                category TEXT NOT NULL, has_unsubscribe INTEGER NOT NULL, has_attachment INTEGER NOT NULL,
                recipients TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS message_sender ON message (sender_address);
            CREATE TABLE IF NOT EXISTS inventory_state (
                id INTEGER PRIMARY KEY CHECK (id = 1), last_page_token TEXT,
                fetched_count INTEGER NOT NULL, started_at TEXT NOT NULL, finished_at TEXT);
            CREATE TABLE IF NOT EXISTS sender_summary (
                sender_address TEXT PRIMARY KEY, domain TEXT NOT NULL, count INTEGER NOT NULL,
                total_size INTEGER NOT NULL, first_date INTEGER NOT NULL, last_date INTEGER NOT NULL,
                read_ratio REAL NOT NULL, user_replied INTEGER NOT NULL, has_unsubscribe INTEGER NOT NULL,
                suggested_decision TEXT NOT NULL, rule INTEGER NOT NULL, user_decision TEXT);
            CREATE TABLE IF NOT EXISTS sent_contact (address TEXT PRIMARY KEY);
            CREATE TABLE IF NOT EXISTS action_log (
                id INTEGER PRIMARY KEY AUTOINCREMENT, message_id TEXT NOT NULL, action TEXT NOT NULL,
                date INTEGER NOT NULL, batch_no INTEGER NOT NULL, undone INTEGER NOT NULL DEFAULT 0);
            CREATE INDEX IF NOT EXISTS action_log_batch ON action_log (batch_no);
            CREATE TABLE IF NOT EXISTS account (id INTEGER PRIMARY KEY CHECK (id = 1), email TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS sender_unsubscribe (sender_address TEXT PRIMARY KEY, link TEXT NOT NULL);
            """);
    }

    public void Upsert(IEnumerable<MessageRow> rows)
    {
        using var tx = _db.BeginTransaction();
        using var cmd = Cmd("""
            INSERT OR REPLACE INTO message VALUES
            ($id,$thread,$addr,$name,$domain,$subject,$date,$size,$labels,$read,$star,$imp,$cat,$unsub,$att,$rcpt)
            """, tx);
        foreach (var r in rows)
        {
            cmd.Parameters.Clear();
            Bind(cmd, ("$id", r.Id), ("$thread", r.ThreadId), ("$addr", r.SenderAddress), ("$name", r.SenderName),
                ("$domain", r.Domain), ("$subject", r.Subject), ("$date", r.Date), ("$size", r.Size),
                ("$labels", r.Labels), ("$read", r.IsRead), ("$star", r.IsStarred), ("$imp", r.IsImportant),
                ("$cat", r.Category), ("$unsub", r.HasUnsubscribe), ("$att", r.HasAttachment), ("$rcpt", r.Recipients));
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public void EnsureAccount(string email)
    {
        var stored = Scalar("SELECT email FROM account WHERE id = 1") as string;
        if (stored is null)
        {
            if (Count() > 0) throw new AccountMismatchException(null, email);
            using var insert = Cmd("INSERT INTO account (id, email) VALUES (1, $e)");
            Bind(insert, ("$e", email));
            insert.ExecuteNonQuery();
        }
        else if (!string.Equals(stored, email, StringComparison.OrdinalIgnoreCase))
            throw new AccountMismatchException(stored, email);
    }

    public void MarkAttachments(IEnumerable<string> ids)
    {
        using var tx = _db.BeginTransaction();
        Cmd("UPDATE message SET has_attachment = 0", tx).ExecuteNonQuery();
        using var cmd = Cmd("UPDATE message SET has_attachment = 1 WHERE id = $id", tx);
        foreach (var id in ids)
        {
            cmd.Parameters.Clear();
            Bind(cmd, ("$id", id));
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public Dictionary<string, int> GetStarredCounts()
    {
        using var cmd = Cmd("SELECT sender_address, COUNT(*) FROM message WHERE is_starred = 1 GROUP BY sender_address");
        using var r = cmd.ExecuteReader();
        var map = new Dictionary<string, int>();
        while (r.Read()) map[r.GetString(0)] = r.GetInt32(1);
        return map;
    }

    public HashSet<string> KnownIds(IEnumerable<string> ids)
    {
        var known = new HashSet<string>();
        using var cmd = Cmd("SELECT 1 FROM message WHERE id = $id");
        foreach (var id in ids)
        {
            cmd.Parameters.Clear();
            Bind(cmd, ("$id", id));
            if (cmd.ExecuteScalar() != null) known.Add(id);
        }
        return known;
    }

    public List<(string Sender, string MessageId)> LatestUnsubscribeMessages()
    {
        using var cmd = Cmd("""
            SELECT m.sender_address, m.id FROM message m
            WHERE m.has_unsubscribe = 1
              AND m.date = (SELECT MAX(date) FROM message WHERE sender_address = m.sender_address AND has_unsubscribe = 1)
            GROUP BY m.sender_address
            """);
        using var r = cmd.ExecuteReader();
        var list = new List<(string, string)>();
        while (r.Read()) list.Add((r.GetString(0), r.GetString(1)));
        return list;
    }

    public void SaveUnsubscribeLinks(IEnumerable<(string Sender, string Link)> links)
    {
        using var tx = _db.BeginTransaction();
        using var cmd = Cmd("INSERT OR REPLACE INTO sender_unsubscribe VALUES ($a, $l)", tx);
        foreach (var (sender, link) in links)
        {
            cmd.Parameters.Clear();
            Bind(cmd, ("$a", sender), ("$l", link));
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public Dictionary<string, string> GetUnsubscribeLinks()
    {
        using var cmd = Cmd("SELECT sender_address, link FROM sender_unsubscribe");
        using var r = cmd.ExecuteReader();
        var map = new Dictionary<string, string>();
        while (r.Read()) map[r.GetString(0)] = r.GetString(1);
        return map;
    }

    public int CountTrashedByApp() =>
        Convert.ToInt32(Scalar("SELECT COUNT(DISTINCT message_id) FROM action_log WHERE action = 'Trash' AND undone = 0"));

    public int Count() => Convert.ToInt32(Scalar("SELECT COUNT(*) FROM message"));

    public IEnumerable<MessageRow> ReadMessages()
    {
        using var cmd = Cmd("""
            SELECT id, thread_id, sender_address, sender_name, domain, subject, date, size, labels,
                   is_read, is_starred, is_important, category, has_unsubscribe, has_attachment, recipients
            FROM message
            """);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            yield return new MessageRow(
                r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5),
                r.GetInt64(6), r.GetInt64(7), r.GetString(8), r.GetBoolean(9), r.GetBoolean(10), r.GetBoolean(11),
                r.GetString(12), r.GetBoolean(13), r.GetBoolean(14), r.GetString(15));
    }

    public List<SenderMail> GetSenderMessages(string address)
    {
        using var cmd = Cmd("""
            SELECT id, thread_id, subject, date, size, is_read, is_starred
            FROM message WHERE sender_address = $addr ORDER BY date DESC
            """);
        Bind(cmd, ("$addr", address));
        using var r = cmd.ExecuteReader();
        var list = new List<SenderMail>();
        while (r.Read())
            list.Add(new SenderMail(r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt64(3), r.GetInt64(4), r.GetBoolean(5), r.GetBoolean(6)));
        return list;
    }

    public InventoryState? LoadState()
    {
        using var cmd = Cmd("SELECT last_page_token, fetched_count, started_at, finished_at FROM inventory_state WHERE id = 1");
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new InventoryState(
            r.IsDBNull(0) ? null : r.GetString(0), r.GetInt32(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3));
    }

    public void SaveState(InventoryState s)
    {
        using var cmd = Cmd("""
            INSERT OR REPLACE INTO inventory_state (id, last_page_token, fetched_count, started_at, finished_at)
            VALUES (1, $token, $count, $started, $finished)
            """);
        Bind(cmd, ("$token", s.PageToken), ("$count", s.FetchedCount), ("$started", s.StartedAt), ("$finished", s.FinishedAt));
        cmd.ExecuteNonQuery();
    }

    public void SaveAnalysis(IReadOnlyList<SenderSummary> senders, IEnumerable<string> sentContacts)
    {
        using var tx = _db.BeginTransaction();
        var decided = new Dictionary<string, string>();
        using (var read = Cmd("SELECT sender_address, user_decision FROM sender_summary WHERE user_decision IS NOT NULL", tx))
        using (var r = read.ExecuteReader())
            while (r.Read()) decided[r.GetString(0)] = r.GetString(1);

        Cmd("DELETE FROM sender_summary", tx).ExecuteNonQuery();
        Cmd("DELETE FROM sent_contact", tx).ExecuteNonQuery();

        using var insert = Cmd("""
            INSERT INTO sender_summary VALUES
            ($addr,$domain,$count,$size,$first,$last,$ratio,$replied,$unsub,$suggested,$rule,$user)
            """, tx);
        foreach (var s in senders)
        {
            insert.Parameters.Clear();
            Bind(insert, ("$addr", s.SenderAddress), ("$domain", s.Domain), ("$count", s.Count), ("$size", s.TotalSize),
                ("$first", s.FirstDate), ("$last", s.LastDate), ("$ratio", s.ReadRatio), ("$replied", s.UserReplied),
                ("$unsub", s.HasUnsubscribe), ("$suggested", s.Suggested.ToString()), ("$rule", s.Rule),
                ("$user", decided.GetValueOrDefault(s.SenderAddress)));
            insert.ExecuteNonQuery();
        }

        using var contact = Cmd("INSERT OR IGNORE INTO sent_contact VALUES ($a)", tx);
        foreach (var a in sentContacts)
        {
            contact.Parameters.Clear();
            Bind(contact, ("$a", a));
            contact.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public List<SenderSummary> GetSenders()
    {
        using var cmd = Cmd("""
            SELECT sender_address, domain, count, total_size, first_date, last_date, read_ratio,
                   user_replied, has_unsubscribe, suggested_decision, rule, user_decision
            FROM sender_summary ORDER BY total_size DESC
            """);
        using var r = cmd.ExecuteReader();
        var list = new List<SenderSummary>();
        while (r.Read())
            list.Add(new SenderSummary(
                r.GetString(0), r.GetString(1), r.GetInt32(2), r.GetInt64(3), r.GetInt64(4), r.GetInt64(5),
                r.GetDouble(6), r.GetBoolean(7), r.GetBoolean(8), Enum.Parse<Decision>(r.GetString(9)), r.GetInt32(10),
                r.IsDBNull(11) ? null : Enum.Parse<Decision>(r.GetString(11))));
        return list;
    }

    public List<string> GetSentContacts()
    {
        using var cmd = Cmd("SELECT address FROM sent_contact ORDER BY address");
        using var r = cmd.ExecuteReader();
        var list = new List<string>();
        while (r.Read()) list.Add(r.GetString(0));
        return list;
    }

    public void SetUserDecision(IEnumerable<string> addresses, Decision? decision)
    {
        using var tx = _db.BeginTransaction();
        using var cmd = Cmd("UPDATE sender_summary SET user_decision = $d WHERE sender_address = $a", tx);
        foreach (var a in addresses)
        {
            cmd.Parameters.Clear();
            Bind(cmd, ("$d", decision?.ToString()), ("$a", a));
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public List<TrashCandidate> GetTrashCandidates()
    {
        using var cmd = Cmd("""
            SELECT m.id, m.sender_address, m.size, m.is_starred
            FROM message m JOIN sender_summary s ON s.sender_address = m.sender_address
            WHERE COALESCE(s.user_decision, s.suggested_decision) = 'Trash'
              AND m.labels NOT LIKE '%TRASH%'
              AND m.id NOT IN (SELECT message_id FROM action_log WHERE action = 'Trash' AND undone = 0)
            ORDER BY m.sender_address, m.id
            """);
        using var r = cmd.ExecuteReader();
        var list = new List<TrashCandidate>();
        while (r.Read()) list.Add(new TrashCandidate(r.GetString(0), r.GetString(1), r.GetInt64(2), r.GetBoolean(3)));
        return list;
    }

    public int NextBatchNo() => Convert.ToInt32(Scalar("SELECT COALESCE(MAX(batch_no), 0) + 1 FROM action_log"));

    public void LogActions(IEnumerable<string> ids, string action, int batchNo)
    {
        using var tx = _db.BeginTransaction();
        using var cmd = Cmd("INSERT INTO action_log (message_id, action, date, batch_no) VALUES ($id,$action,$date,$batch)", tx);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var id in ids)
        {
            cmd.Parameters.Clear();
            Bind(cmd, ("$id", id), ("$action", action), ("$date", now), ("$batch", batchNo));
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public void RemoveLog(IEnumerable<string> ids, int batchNo)
    {
        using var tx = _db.BeginTransaction();
        using var cmd = Cmd("DELETE FROM action_log WHERE batch_no = $batch AND message_id = $id", tx);
        foreach (var id in ids)
        {
            cmd.Parameters.Clear();
            Bind(cmd, ("$batch", batchNo), ("$id", id));
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public void MarkUndone(IEnumerable<string> ids, int batchNo)
    {
        using var tx = _db.BeginTransaction();
        using var cmd = Cmd("UPDATE action_log SET undone = 1 WHERE batch_no = $batch AND message_id = $id", tx);
        foreach (var id in ids)
        {
            cmd.Parameters.Clear();
            Bind(cmd, ("$batch", batchNo), ("$id", id));
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public List<BatchInfo> GetBatches()
    {
        using var cmd = Cmd("""
            SELECT a.batch_no, MIN(a.date), COUNT(*), SUM(CASE WHEN a.undone = 0 THEN 1 ELSE 0 END), COALESCE(SUM(m.size), 0)
            FROM action_log a LEFT JOIN message m ON m.id = a.message_id
            WHERE a.action = 'Trash' GROUP BY a.batch_no ORDER BY a.batch_no DESC
            """);
        using var r = cmd.ExecuteReader();
        var list = new List<BatchInfo>();
        while (r.Read()) list.Add(new BatchInfo(r.GetInt32(0), r.GetInt64(1), r.GetInt32(2), r.GetInt32(3), r.GetInt64(4)));
        return list;
    }

    public List<LoggedMessage> GetActiveBatchMessages(int batchNo)
    {
        using var cmd = Cmd("""
            SELECT a.message_id, COALESCE(m.labels, '')
            FROM action_log a LEFT JOIN message m ON m.id = a.message_id
            WHERE a.batch_no = $batch AND a.action = 'Trash' AND a.undone = 0
            """);
        Bind(cmd, ("$batch", batchNo));
        using var r = cmd.ExecuteReader();
        var list = new List<LoggedMessage>();
        while (r.Read()) list.Add(new LoggedMessage(r.GetString(0), r.GetString(1)));
        return list;
    }

    SqliteCommand Cmd(string sql, SqliteTransaction? tx = null)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = tx;
        return cmd;
    }

    static void Bind(SqliteCommand cmd, params (string Name, object? Value)[] values)
    {
        foreach (var (name, value) in values)
            cmd.Parameters.AddWithValue(name, value switch { null => DBNull.Value, bool b => b ? 1 : 0, _ => value });
    }

    void Exec(string sql)
    {
        using var cmd = Cmd(sql);
        cmd.ExecuteNonQuery();
    }

    object? Scalar(string sql)
    {
        using var cmd = Cmd(sql);
        return cmd.ExecuteScalar();
    }

    public void Dispose() => _db.Dispose();
}
