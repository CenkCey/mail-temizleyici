using System.Windows;
using MailTemizleyici.Core;

namespace MailTemizleyici.Desktop.Views;

public partial class AllowlistDialog : Window
{
    public AllowlistDialog()
    {
        InitializeComponent();
        Domains.Text = string.Join(Environment.NewLine, Classifier.LoadAllowlist(AppPaths.Allowlist));
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        Classifier.SaveAllowlist(AppPaths.Allowlist, Domains.Text.Split('\n'));
        DialogResult = true;
    }
}
