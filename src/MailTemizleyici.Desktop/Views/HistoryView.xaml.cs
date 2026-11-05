using System.Windows;
using System.Windows.Controls;
using MailTemizleyici.Desktop.ViewModels;

namespace MailTemizleyici.Desktop.Views;

public partial class HistoryView : UserControl
{
    public HistoryView() => InitializeComponent();

    void UndoAll_Click(object sender, RoutedEventArgs e)
    {
        var vm = (HistoryViewModel)DataContext;
        if (!vm.CanUndoAny || vm.UndoAllCommand.IsRunning) return;
        var answer = MessageBox.Show(Window.GetWindow(this), vm.ConfirmAllText, "Tümünü geri almayı onayla",
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes) vm.UndoAllCommand.Execute(null);
    }

    void Undo_Click(object sender, RoutedEventArgs e)
    {
        var vm = (HistoryViewModel)DataContext;
        if (!vm.CanUndoSelected || vm.UndoCommand.IsRunning) return;
        var answer = MessageBox.Show(Window.GetWindow(this), vm.ConfirmText(vm.Selected!), "Geri almayı onayla",
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes) vm.UndoCommand.Execute(null);
    }
}
