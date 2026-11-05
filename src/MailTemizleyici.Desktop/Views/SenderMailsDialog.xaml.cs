using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MailTemizleyici.Desktop.ViewModels;

namespace MailTemizleyici.Desktop.Views;

public partial class SenderMailsDialog : Window
{
    readonly SenderMailsViewModel vm;

    public SenderMailsDialog(string address)
    {
        InitializeComponent();
        vm = new SenderMailsViewModel(address);
        DataContext = vm;
        Title = $"{address} mailleri";
    }

    void Open_Click(object sender, RoutedEventArgs e) => Open();

    void List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(List, (DependencyObject)e.OriginalSource) is DataGridRow) Open();
    }

    void Open()
    {
        if (vm.Selected is { } mail)
            Process.Start(new ProcessStartInfo(vm.UrlFor(mail)) { UseShellExecute = true });
    }
}
