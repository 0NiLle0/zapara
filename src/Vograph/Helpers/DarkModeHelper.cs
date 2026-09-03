using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Vograph.Helpers
{
    public static class DarkModeHelper
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public static void EnableDarkTitleBar(Window window)
        {
            try
            {
                var helper = new WindowInteropHelper(window);
                // Ensure handle is created
                if (helper.Handle == IntPtr.Zero)
                {
                    window.SourceInitialized += (s, e) => Apply(helper.Handle);
                }
                else
                {
                    Apply(helper.Handle);
                }
            }
            catch { }
        }

        private static void Apply(IntPtr handle)
        {
            try
            {
                int useDark = 1;
                // Try new attribute first, then fallback
                if (DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int)) != 0)
                {
                    DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDark, sizeof(int));
                }
            }
            catch { }
        }
    }
}
