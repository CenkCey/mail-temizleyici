using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using MailTemizleyici.Core;
using MailTemizleyici.Desktop.ViewModels;

namespace MailTemizleyici.Desktop.Views;

public partial class SubscriptionsView : UserControl
{
    public SubscriptionsView() => InitializeComponent();

    void Unsubscribe_Click(object sender, RoutedEventArgs e)
    {
        var vm = (SubscriptionsViewModel)DataContext;
        if (Grid.SelectedItem is not SubscriptionRow { Link: { } link } row || !UnsubscribeLinks.IsOpenable(link))
        {
            vm.Status = "Abonelik bağlantısı olan bir gönderen seçin. Eksikse 'Bağlantıları getir'e basın.";
            return;
        }
        var target = new Uri(link);
        var shown = target.Scheme == "mailto" ? target.AbsolutePath : target.Host;
        var answer = MessageBox.Show(Window.GetWindow(this),
            $"{row.Address} için abonelik bağlantısı tarayıcıda açılacak ({shown}). Yalnızca tanıdığınız göndericilerde abonelikten çıkın; " +
            "şüpheli bir maildeki bağlantı adresinizin aktif olduğunu göndericiye bildirebilir. Devam edilsin mi?",
            "Abonelikten çık", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes) Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
    }

    void CopyFilter_Click(object sender, RoutedEventArgs e)
    {
        var vm = (SubscriptionsViewModel)DataContext;
        var addresses = Grid.SelectedItems.Cast<SubscriptionRow>().Select(r => r.Address).ToList();
        if (addresses.Count == 0)
        {
            vm.Status = "Önce tablodan bir veya daha fazla gönderen seçin.";
            return;
        }
        try
        {
            Clipboard.SetText(GmailLinks.FromQuery(addresses));
            vm.Status = $"{addresses.Count:N0} gönderen için Gmail arama sorgusu panoya kopyalandı. Gmail arama kutusuna yapıştırın, " +
                        "'Filtre oluştur'u seçin ve yapılacak işlemi (örneğin gelen kutusunu atla) kendiniz belirleyin.";
        }
        catch (ExternalException)
        {
            vm.Status = "Hata: Pano şu an kullanılamıyor, yeniden deneyin.";
        }
    }
}
