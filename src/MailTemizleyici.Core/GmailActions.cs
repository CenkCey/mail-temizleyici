using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;

namespace MailTemizleyici.Core;

public static class GmailActions
{
    public static TrashRunner CreateTrashRunner(GmailService service, MailDatabase db) =>
        new(db, (ids, ct) => service.Users.Messages.BatchModify(
            new BatchModifyMessagesRequest { Ids = ids.ToList(), AddLabelIds = ["TRASH"], RemoveLabelIds = ["INBOX"] }, "me")
            .ExecuteAsync(ct), TimeSpan.FromSeconds(1));

    public static UndoRunner CreateUndoRunner(GmailService service, MailDatabase db) =>
        new(db, (ids, addLabels, ct) => service.Users.Messages.BatchModify(
            new BatchModifyMessagesRequest { Ids = ids.ToList(), AddLabelIds = addLabels.ToList(), RemoveLabelIds = ["TRASH"] }, "me")
            .ExecuteAsync(ct), TimeSpan.FromSeconds(1));
}
