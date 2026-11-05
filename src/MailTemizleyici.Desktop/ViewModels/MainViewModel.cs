using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MailTemizleyici.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    readonly ConnectionViewModel connection = new();
    InventoryViewModel inventory = new();
    AnalysisViewModel analysis = new();
    GroupsViewModel groups = new();
    PreviewViewModel preview = new();
    HistoryViewModel history = new();
    SubscriptionsViewModel subscriptions = new();

    [ObservableProperty] object current;
    [ObservableProperty] string title = "Bağlantı";

    public MainViewModel()
    {
        current = connection;
        connection.AccountChanged += ResetAccountViews;
    }

    void ResetAccountViews()
    {
        inventory = new();
        analysis = new();
        groups = new();
        preview = new();
        history = new();
        subscriptions = new();
    }

    void Show(object page, string name)
    {
        Current = page;
        Title = name;
    }

    [RelayCommand] void ShowConnection() => Show(connection, "Bağlantı");

    [RelayCommand]
    void ShowInventory()
    {
        inventory.Load();
        Show(inventory, "Envanter");
    }

    [RelayCommand]
    void ShowAnalysis()
    {
        analysis.Load();
        Show(analysis, "Analiz");
    }

    [RelayCommand]
    void ShowGroups()
    {
        groups.Load();
        Show(groups, "Gönderen grupları");
    }

    [RelayCommand]
    void ShowPreview()
    {
        preview.Load();
        Show(preview, "Önizleme");
    }

    [RelayCommand]
    void ShowSubscriptions()
    {
        subscriptions.Load();
        Show(subscriptions, "Abonelikler");
    }

    [RelayCommand]
    void ShowHistory()
    {
        history.Load();
        Show(history, "Geçmiş");
    }
}
