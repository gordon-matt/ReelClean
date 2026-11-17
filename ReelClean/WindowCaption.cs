using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace ReelClean;

static class WindowCaption
{
    public static void UseDarkFrame(Window window)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var handle = window.TryGetPlatformHandle()?.Handle ?? 0;
        if (handle == 0)
        {
            return;
        }

        int dark = 1;
        _ = DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));

        int caption = 0x00221510;
        int text = 0x00FCF5F1;
        int border = 0x00533D30;
        _ = DwmSetWindowAttribute(handle, 35, ref caption, sizeof(int));
        _ = DwmSetWindowAttribute(handle, 36, ref text, sizeof(int));
        _ = DwmSetWindowAttribute(handle, 34, ref border, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
