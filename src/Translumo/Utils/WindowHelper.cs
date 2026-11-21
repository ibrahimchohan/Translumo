using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Translumo.Utils
{
    internal static class WindowHelper
    {
        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x,
            int y, int width, int height, uint flags);

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr
            lParam);

        const int GWL_EXSTYLE = -20;
        const int WS_EX_DLGMODALFRAME = 0x0001;
        const int SWP_NOSIZE = 0x0001;
        const int SWP_NOMOVE = 0x0002;
        const int SWP_NOZORDER = 0x0004;
        const int SWP_FRAMECHANGED = 0x0020;
        const uint WM_SETICON = 0x0080;

        private const uint WDA_EXCLUDEFROMCAPTURE = 0x11;

        [DllImport("user32.dll")]
        static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);

        /// <summary>
        /// Excludes the window from screen capture (Windows 10 2004+), so OCR never reads the app's own UI.
        /// Call after the window handle is created (SourceInitialized).
        /// </summary>
        public static void ExcludeFromCapture(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd != IntPtr.Zero)
            {
                SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
            }
        }

        /// <summary>Converts a rectangle in physical screen pixels to device-independent units of the given visual.</summary>
        public static Rect ToDip(System.Windows.Media.Visual visual, System.Drawing.RectangleF physical)
        {
            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(visual);
            return new Rect(physical.X / dpi.DpiScaleX, physical.Y / dpi.DpiScaleY,
                physical.Width / dpi.DpiScaleX, physical.Height / dpi.DpiScaleY);
        }

        public static void RemoveIcon(Window window)
        {
            // Get this window's handle
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            // Change the extended window style to not show a window icon
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_DLGMODALFRAME);
            // Update the window's non-client area to reflect the changes
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE |
                                                        SWP_NOZORDER | SWP_FRAMECHANGED);
        }
    }
}
