using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MailTemizleyici.Desktop.ViewModels;

namespace MailTemizleyici.Desktop.Views;

public partial class GroupsView : UserControl
{
    public GroupsView()
    {
        InitializeComponent();
        Loaded += (_, _) => Search.Focus();
    }

    void Allowlist_Click(object sender, RoutedEventArgs e)
    {
        if (new AllowlistDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true && DataContext is GroupsViewModel vm)
            vm.Status = "Güvenilir alanlar kaydedildi. Etkili olması için Analiz ekranında analizi yeniden çalıştırın.";
    }

    void Inspect_Click(object sender, RoutedEventArgs e) => Inspect();

    void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(Grid, (DependencyObject)e.OriginalSource) is DataGridRow) Inspect();
    }

    void Inspect()
    {
        if (Grid.SelectedItem is SenderRow row)
            new SenderMailsDialog(row.Address) { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        (DataContext as GroupsViewModel)?.SetSelection(Grid.SelectedItems.Cast<SenderRow>());
}
