using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MailTemizleyici.Core;

namespace MailTemizleyici.Desktop.ViewModels;

public sealed record PlanRow(string Sender, string Mails, string Size);

public partial class PreviewViewModel : ObservableObject
{
    TrashPlan full = new([], 0);
    TrashPlan plan = new([], 0);

    [ObservableProperty] string status = "";
    [ObservableProperty] string summary = "";
    [ObservableProperty] bool trialOnly = true;
    [ObservableProperty] bool includeStarred;
    [ObservableProperty] IReadOnlyList<PlanRow> rows = [];
    [ObservableProperty] bool hasPlan;
    [ObservableProperty] int done;
    [ObservableProperty] int total;

    public string ConfirmText =>
        $"{plan.MessageCount.ToString("N0", Text.Tr)} mail, {plan.Groups.Count.ToString("N0", Text.Tr)} gönderen grubundan " +
        $"çöp kutusuna taşınacak ({Text.Size(plan.TotalSize)}). Mailler silinmez; çöp kutusunda 30 gün boyunca durur ve " +
        "Geçmiş ekranından geri alınabilir." +
        (plan.IncludedStarred > 0
            ? $" DİKKAT: Bunların {plan.IncludedStarred.ToString("N0", Text.Tr)} tanesi yıldızlı mail. Geri alırsanız yıldızları da geri gelir."
            : "");

    partial void OnTrialOnlyChanged(bool value) => Render();

    partial void OnIncludeStarredChanged(bool value) => Load();

    public void Load()
    {
        try
        {
            using var db = new MailDatabase(AppPaths.Database);
            full = TrashPlan.Build(db, IncludeStarred);
        }
        catch (InventoryUnfinishedException)
        {
            full = new TrashPlan([], 0);
            Render();
            Summary = "Envanter bitmeden önizleme yapılamaz. Envanter ekranından başlıkların tamamını alın ve analizi çalıştırın.";
            return;
        }
        catch (Exception ex)
        {
            Status = $"Hata: Veritabanı okunamadı. {ex.Message}";
            return;
        }
        Render();
    }

    void Render()
    {
        plan = TrialOnly ? full.Trial() : full;
        HasPlan = plan.MessageCount > 0;
        Rows = plan.Groups.OrderByDescending(g => g.Size)
            .Select(g => new PlanRow(g.Sender, g.Ids.Count.ToString("N0", Text.Tr), Text.Size(g.Size))).ToList();
        Summary = HasPlan
            ? $"Deneme ekranı (henüz hiçbir şey yapılmadı): {plan.MessageCount.ToString("N0", Text.Tr)} mail, " +
              $"{plan.Groups.Count.ToString("N0", Text.Tr)} gönderen, {Text.Size(plan.TotalSize)} yer açılır."
            : "Çöp kutusuna taşınacak mail yok. Gönderen gruplarında Sil kararı verin.";
        if (full.SkippedStarred > 0)
            Summary += $" Yıldızlı {full.SkippedStarred.ToString("N0", Text.Tr)} mail güvenlik için atlanır; dahil etmek için kutuyu işaretleyin.";
        if (plan.IncludedStarred > 0)
            Summary += $" Planda {plan.IncludedStarred.ToString("N0", Text.Tr)} yıldızlı mail var.";
        Status = HasPlan ? "Bu ekran yalnızca önizlemedir." : "";
        OnPropertyChanged(nameof(ConfirmText));
    }

    [RelayCommand(IncludeCancelCommand = true)]
    async Task TrashAsync(CancellationToken ct)
    {
        var toMove = plan;
        Total = toMove.MessageCount;
        Done = 0;
        Status = "Gmail'e bağlanılıyor. Tarayıcıda gmail.modify izni istenebilir...";
        try
        {
            using var service = await GmailConnection.CreateServiceAsync(ct, modify: true);
            using var db = new MailDatabase(AppPaths.Database);
            db.EnsureAccount((await GmailConnection.GetProfileAsync(service, ct)).Email);
            var runner = GmailActions.CreateTrashRunner(service, db);
            Status = "Mailler çöp kutusuna taşınıyor...";
            var batch = await Task.Run(() => runner.RunAsync(toMove, new Progress<int>(n => Done = n), ct), ct);
            Status = $"Tamamlandı: {Done.ToString("N0", Text.Tr)} mail çöp kutusuna taşındı (işlem no {batch}). Geri almak için Geçmiş ekranını kullanın.";
        }
        catch (OperationCanceledException)
        {
            Status = "İptal edildi. Taşınanlar Geçmiş ekranından geri alınabilir.";
        }
        catch (AccountMismatchException ex)
        {
            Status = Text.AccountMismatch(ex);
        }
        catch (FileNotFoundException)
        {
            Status = Text.MissingClient;
        }
        catch (Exception ex)
        {
            Status = $"Hata: İşlem yarıda kaldı. Taşınanlar Geçmiş ekranında görünür. {ex.Message}";
        }
        var result = Status;
        Load();
        Status = result;
    }
}
