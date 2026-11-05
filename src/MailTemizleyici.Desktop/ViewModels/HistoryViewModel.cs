using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MailTemizleyici.Core;

namespace MailTemizleyici.Desktop.ViewModels;

public sealed record BatchRow(int BatchNo, string DateText, int Total, int Active, string Size)
{
    public string State => Active == 0 ? "Geri alındı" : Active == Total ? "Çöp kutusunda" : "Kısmen geri alındı";
}

public partial class HistoryViewModel : ObservableObject
{
    [ObservableProperty] string status = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanUndoAny))] IReadOnlyList<BatchRow> batches = [];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanUndoSelected))] BatchRow? selected;
    [ObservableProperty] int done;
    [ObservableProperty] int total;

    public string ConfirmText(BatchRow b) =>
        $"{b.Active.ToString("N0", Text.Tr)} mail çöp kutusundan eski etiketleriyle geri getirilecek (işlem no {b.BatchNo}). Devam edilsin mi?";

    public void Load()
    {
        try
        {
            using var db = new MailDatabase(AppPaths.Database);
            Batches = db.GetBatches()
                .Select(b => new BatchRow(b.BatchNo, Text.Date(b.Date), b.Total, b.Active, Text.Size(b.Size))).ToList();
        }
        catch (Exception ex)
        {
            Status = $"Hata: Veritabanı okunamadı. {ex.Message}";
            return;
        }
        if (Batches.Count == 0) Status = "Henüz çöp kutusuna taşıma işlemi yapılmadı.";
        else if (Status.StartsWith("Henüz")) Status = "";
    }

    public bool CanUndoSelected => Selected is { Active: > 0 };

    public bool CanUndoAny => Batches.Any(b => b.Active > 0);

    public string ConfirmAllText =>
        $"{Batches.Sum(b => b.Active).ToString("N0", Text.Tr)} mail ({Batches.Count(b => b.Active > 0)} işlem) çöp kutusundan eski etiketleriyle geri getirilecek. Devam edilsin mi?";

    [RelayCommand]
    void CancelUndo()
    {
        UndoCancelCommand.Execute(null);
        UndoAllCancelCommand.Execute(null);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    async Task UndoAllAsync(CancellationToken ct) =>
        await RunUndoAsync(Batches.Where(b => b.Active > 0).OrderBy(b => b.BatchNo).ToList(), ct);

    [RelayCommand(IncludeCancelCommand = true)]
    async Task UndoAsync(CancellationToken ct)
    {
        await RunUndoAsync([Selected!], ct);
    }

    async Task RunUndoAsync(IReadOnlyList<BatchRow> targets, CancellationToken ct)
    {
        Total = targets.Sum(b => b.Active);
        Done = 0;
        Status = "Gmail'e bağlanılıyor. Tarayıcıda gmail.modify izni istenebilir...";
        try
        {
            using var service = await GmailConnection.CreateServiceAsync(ct, modify: true);
            using var db = new MailDatabase(AppPaths.Database);
            db.EnsureAccount((await GmailConnection.GetProfileAsync(service, ct)).Email);
            var runner = GmailActions.CreateUndoRunner(service, db);
            Status = "Mailler geri getiriliyor...";
            var restored = 0;
            foreach (var batch in targets)
            {
                var baseDone = restored;
                restored += await Task.Run(() => runner.RunAsync(batch.BatchNo, new Progress<int>(n => Done = baseDone + n), ct), ct);
            }
            Status = $"Tamamlandı: {restored.ToString("N0", Text.Tr)} mail geri getirildi.";
        }
        catch (OperationCanceledException)
        {
            Status = "İptal edildi. Geri getirilenler kayıtlıdır; kalanı için yeniden deneyebilirsiniz.";
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
            Status = $"Hata: Geri alma yarıda kaldı, yeniden deneyebilirsiniz. {ex.Message}";
        }
        var keep = Status;
        Load();
        Status = keep;
    }
}
