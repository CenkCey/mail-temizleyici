using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MailTemizleyici.Desktop;

static class ThemeChrome
{
    const int UseImmersiveDarkMode = 20;
    const int CaptionColor = 35;
    const int TextColor = 36;

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    public static void Apply(Window window, bool dark)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == 0) return;
        Set(handle, UseImmersiveDarkMode, dark ? 1 : 0);
        Set(handle, CaptionColor, dark ? 0x550C2A : 0xFFECF3);
        Set(handle, TextColor, dark ? 0xFFF1F6 : 0x3F1225);
    }

    static void Set(nint handle, int attribute, int value) => DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));
}
