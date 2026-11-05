using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MailTemizleyici.Core;

namespace MailTemizleyici.Desktop.ViewModels;

public partial class GroupsViewModel : ObservableObject
{
    ListCollectionView? view;
    IReadOnlyList<SenderRow> selected = [];

    [ObservableProperty] ICollectionView? rows;
    [ObservableProperty] string status = "";
    [ObservableProperty] string selectionText = "Seçili gönderen yok";
    [ObservableProperty] bool hasRows;
    [ObservableProperty] bool hasSelection;
    [ObservableProperty] string searchText = "";
    [ObservableProperty] string filter = "All";
    [ObservableProperty] int minCount = 1;

    partial void OnSearchTextChanged(string value) => Refresh();

    partial void OnFilterChanged(string value) => Refresh();

    partial void OnMinCountChanged(int value) => Refresh();

    public void Load()
    {
        try
        {
            using var db = new MailDatabase(AppPaths.Database);
            var starred = db.GetStarredCounts();
            view = new ListCollectionView(db.GetSenders().Select(s => new SenderRow(s, starred.GetValueOrDefault(s.SenderAddress))).ToList()) { Filter = Matches };
        }
        catch (Exception ex)
        {
            Status = $"Hata: Veritabanı okunamadı. {ex.Message}";
            return;
        }
        Rows = view;
        HasRows = !view.IsEmpty;
        SetSelection([]);
        UpdateStatus();
    }

    public void SetSelection(IEnumerable<SenderRow> items)
    {
        selected = items.ToList();
        HasSelection = selected.Count > 0;
        SelectionText = HasSelection ? $"{selected.Count:N0} gönderen seçili" : "Seçili gönderen yok";
    }

    [RelayCommand]
    void SetFilter(string value) => Filter = value;

    [RelayCommand]
    void SetMin(string value) => MinCount = int.Parse(value);

    [RelayCommand]
    void Decide(string value)
    {
        if (selected.Count == 0) return;
        Decision? decision = value == "Clear" ? null : Enum.Parse<Decision>(value);
        try
        {
            using var db = new MailDatabase(AppPaths.Database);
            db.SetUserDecision(selected.Select(r => r.Address), decision);
        }
        catch (Exception ex)
        {
            Status = $"Hata: Karar kaydedilemedi. {ex.Message}";
            return;
        }
        foreach (var row in selected) row.UserDecision = decision;
        Refresh();
    }

    bool Matches(object item)
    {
        var row = (SenderRow)item;
        if (Filter == "Starred" ? row.StarredCount == 0 : Filter != "All" && row.Effective.ToString() != Filter) return false;
        if (row.Count < MinCount) return false;
        return SearchText.Length == 0 || row.Address.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    void Refresh()
    {
        if (view == null) return;
        view.Refresh();
        UpdateStatus();
    }

    void UpdateStatus() =>
        Status = HasRows ? $"{view!.Count:N0} gönderen gösteriliyor" : "Henüz analiz yok. Önce Analiz ekranında analizi çalıştırın.";
}
