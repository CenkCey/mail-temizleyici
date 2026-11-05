using System.Windows;

namespace MailTemizleyici.Desktop.Views;

public partial class ConfirmTrashDialog : Window
{
    public ConfirmTrashDialog(string message)
    {
        InitializeComponent();
        Message.Text = message;
        Loaded += (_, _) => Cancel.Focus();
    }

    void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
