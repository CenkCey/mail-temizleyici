using System.Windows;
using System.Windows.Controls;
using MailTemizleyici.Desktop.ViewModels;

namespace MailTemizleyici.Desktop.Views;

public partial class PreviewView : UserControl
{
    public PreviewView() => InitializeComponent();

    void Start_Click(object sender, RoutedEventArgs e)
    {
        var vm = (PreviewViewModel)DataContext;
        if (!vm.HasPlan || vm.TrashCommand.IsRunning) return;
        var dialog = new ConfirmTrashDialog(vm.ConfirmText) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) vm.TrashCommand.Execute(null);
    }
}
