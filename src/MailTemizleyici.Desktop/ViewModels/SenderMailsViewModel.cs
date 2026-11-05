using CommunityToolkit.Mvvm.ComponentModel;
using MailTemizleyici.Core;

namespace MailTemizleyici.Desktop.ViewModels;

public sealed record MailRow(string ThreadId, string DateText, string Subject, string Size, string ReadText);

public partial class SenderMailsViewModel : ObservableObject
{
    [ObservableProperty] string status = "";
    [ObservableProperty] IReadOnlyList<MailRow> rows = [];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanOpen))] MailRow? selected;

    public string Address { get; }

    public bool CanOpen => Selected is not null;

    public SenderMailsViewModel(string address)
    {
        Address = address;
        try
        {
            using var db = new MailDatabase(AppPaths.Database);
            Rows = db.GetSenderMessages(address).Select(m => new MailRow(
                m.ThreadId, Text.Date(m.Date), m.Subject.Length == 0 ? "(konu yok)" : m.Subject, Text.Size(m.Size),
                (m.IsStarred ? "Yıldızlı, " : "") + (m.IsRead ? "Okundu" : "Okunmadı"))).ToList();
            Status = $"{Rows.Count.ToString("N0", Text.Tr)} mail. İçerik uygulamaya indirilmez; seçtiğiniz mail Gmail'de açılır.";
        }
        catch (Exception ex)
        {
            Status = $"Hata: Veritabanı okunamadı. {ex.Message}";
        }
    }

    public string UrlFor(MailRow mail) => GmailLinks.Thread(AppPaths.ActiveAccount, mail.ThreadId);
}
