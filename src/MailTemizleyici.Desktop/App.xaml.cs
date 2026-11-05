using System.Windows;
using Microsoft.Win32;

namespace MailTemizleyici.Desktop;

public partial class App : Application
{
    static bool dark;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var appsUseLight = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme", 1);
        SetTheme(appsUseLight is 0);
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((s, _) => ThemeChrome.Apply((Window)s, dark)));
        if (SystemParameters.ClientAreaAnimation)
            Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("Themes/Motion.xaml", UriKind.Relative) });
    }

    public static void ToggleTheme() => SetTheme(!dark);

    static void SetTheme(bool useDark)
    {
        dark = useDark;
        Current.Resources.MergedDictionaries[0] = new ResourceDictionary
        {
            Source = new Uri(useDark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative)
        };
        foreach (Window window in Current.Windows) ThemeChrome.Apply(window, useDark);
    }
}
