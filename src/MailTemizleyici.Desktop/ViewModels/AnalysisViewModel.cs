using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MailTemizleyici.Core;

namespace MailTemizleyici.Desktop.ViewModels;

public sealed record DecisionCard(string Label, Decision Decision, string Senders, string Mails, string Size);

public sealed record TopSender(string Address, string Mails, string Size);

public partial class AnalysisViewModel : ObservableObject
{
    [ObservableProperty] string status = "Analiz henüz çalıştırılmadı.";
    [ObservableProperty] bool hasData;
    [ObservableProperty] string totalMails = "-";
    [ObservableProperty] string totalSenders = "-";
    [ObservableProperty] string totalSize = "-";
    [ObservableProperty] IReadOnlyList<DecisionCard> cards = [];
    [ObservableProperty] IReadOnlyList<TopSender> topBySize = [];
    [ObservableProperty] IReadOnlyList<TopSender> topByCount = [];

    [RelayCommand]
    async Task RunAsync()
    {
        Status = "Analiz yapılıyor...";
        try
        {
            await Task.Run(() =>
            {
                using var db = new MailDatabase(AppPaths.Database);
                Analyzer.Run(db, DateTimeOffset.UtcNow, Classifier.LoadAllowlist(AppPaths.Allowlist));
            });
            Load();
        }
        catch (Exception ex)
        {
            Status = $"Hata: Analiz yapılamadı. Envanter sürüyorsa bitmesini bekleyin. {ex.Message}";
        }
    }

    public void Load()
    {
        List<SenderSummary> senders;
        try
        {
            using var db = new MailDatabase(AppPaths.Database);
            senders = db.GetSenders();
        }
        catch (Exception ex)
        {
            Status = $"Hata: Veritabanı okunamadı. {ex.Message}";
            return;
        }

        HasData = senders.Count > 0;
        if (!HasData)
        {
            Status = "Henüz analiz yok. Önce Envanter ekranından başlıkları alın, sonra analizi çalıştırın.";
            return;
        }

        TotalMails = senders.Sum(s => (long)s.Count).ToString("N0", Text.Tr);
        TotalSenders = senders.Count.ToString("N0", Text.Tr);
        TotalSize = Text.Size(senders.Sum(s => s.TotalSize));
        Cards = new[] { Decision.Keep, Decision.Trash, Decision.Undecided }.Select(d =>
        {
            var group = senders.Where(s => s.Effective == d).ToList();
            return new DecisionCard(Text.Label(d), d, group.Count.ToString("N0", Text.Tr),
                group.Sum(s => (long)s.Count).ToString("N0", Text.Tr), Text.Size(group.Sum(s => s.TotalSize)));
        }).ToList();
        TopBySize = senders.OrderByDescending(s => s.TotalSize).Take(10).Select(Top).ToList();
        TopByCount = senders.OrderByDescending(s => s.Count).Take(10).Select(Top).ToList();
        Status = "Analiz hazır. Kararları Gönderen grupları ekranında gözden geçirin.";
    }

    static TopSender Top(SenderSummary s) => new(s.SenderAddress, s.Count.ToString("N0", Text.Tr), Text.Size(s.TotalSize));
}
