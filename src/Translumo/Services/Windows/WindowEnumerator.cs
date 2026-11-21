using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using Translumo.Utils.IntertopStruct;
using static Translumo.Services.Windows.NativeWindowMethods;

namespace Translumo.Services.Windows
{
    public sealed class WindowInfo
    {
        public IntPtr Handle { get; init; }
        public string Title { get; init; }
        public string ProcessName { get; init; }
        public string ClassName { get; init; }
        public Rectangle Bounds { get; init; }
        public bool IsBrowser { get; init; }

        public override string ToString() => $"{Title} ({ProcessName})";
    }

    /// <summary>
    /// Lists top-level application windows the user can pick as a translation target.
    /// </summary>
    public static class WindowEnumerator
    {
        private static readonly HashSet<string> BrowserProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "msedge", "chrome", "firefox", "brave", "opera", "opera_gx", "vivaldi", "arc", "chromium", "yandex", "waterfox", "librewolf", "floorp", "zen"
        };

        // Chromium-based browsers render the page into a child window of this class
        private const string CHROMIUM_CONTENT_CLASS = "Chrome_RenderWidgetHostHWND";

        public static bool IsBrowserProcess(string processName) => processName != null && BrowserProcesses.Contains(processName);

        /// <summary>
        /// Visible top-level windows in Z-order (topmost first), excluding this application's own windows.
        /// </summary>
        public static IReadOnlyList<WindowInfo> GetWindows()
        {
            var ownProcessId = (uint)Environment.ProcessId;
            var result = new List<WindowInfo>();
            var processNames = new Dictionary<uint, string>();

            EnumWindows((hWnd, _) =>
            {
                var info = TryGetWindowInfo(hWnd, ownProcessId, processNames);
                if (info != null)
                {
                    result.Add(info);
                }

                return true;
            }, IntPtr.Zero);

            return result;
        }

        public static WindowInfo GetWindowInfo(IntPtr hWnd)
        {
            return TryGetWindowInfo(hWnd, 0, new Dictionary<uint, string>());
        }

        /// <summary>
        /// Topmost application window under the given screen point (excluding this application's windows).
        /// </summary>
        public static WindowInfo GetWindowAt(Point screenPoint)
        {
            return GetWindows().FirstOrDefault(w => w.Bounds.Contains(screenPoint));
        }

        /// <summary>
        /// Area of the window that shows the content: the page area for Chromium-based browsers,
        /// otherwise the client area (without title bar and borders).
        /// </summary>
        public static Rectangle GetContentBounds(IntPtr hWnd)
        {
            var content = FindChromiumContentWindow(hWnd);
            var rect = content != IntPtr.Zero ? GetFrameBounds(content) : GetClientBounds(hWnd);
            var bounds = ToRectangle(rect);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                bounds = ToRectangle(GetFrameBounds(hWnd));
            }

            return bounds;
        }

        public static Rectangle GetBounds(IntPtr hWnd) => ToRectangle(GetFrameBounds(hWnd));

        public static string GetProcessName(IntPtr hWnd)
        {
            GetWindowThreadProcessId(hWnd, out var processId);
            return GetProcessName(processId, new Dictionary<uint, string>());
        }

        private static WindowInfo TryGetWindowInfo(IntPtr hWnd, uint excludedProcessId, Dictionary<uint, string> processNames)
        {
            if (!IsWindowVisible(hWnd) || GetWindow(hWnd, GW_OWNER) != IntPtr.Zero || IsCloaked(hWnd))
            {
                return null;
            }

            var exStyle = NativeWindowMethods.GetWindowLong(hWnd, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOOLWINDOW) != 0)
            {
                return null;
            }

            var title = GetText(hWnd);
            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            GetWindowThreadProcessId(hWnd, out var processId);
            if (processId == excludedProcessId)
            {
                return null;
            }

            var bounds = ToRectangle(GetFrameBounds(hWnd));
            if (bounds.Width <= 1 || bounds.Height <= 1)
            {
                return null;
            }

            var processName = GetProcessName(processId, processNames);
            return new WindowInfo
            {
                Handle = hWnd,
                Title = title,
                ProcessName = processName,
                ClassName = GetClass(hWnd),
                Bounds = bounds,
                IsBrowser = IsBrowserProcess(processName)
            };
        }

        private static string GetProcessName(uint processId, Dictionary<uint, string> cache)
        {
            if (cache.TryGetValue(processId, out var name))
            {
                return name;
            }

            try
            {
                using var process = Process.GetProcessById((int)processId);
                name = process.ProcessName;
            }
            catch (Exception)
            {
                name = string.Empty;
            }

            cache[processId] = name;
            return name;
        }

        private static IntPtr FindChromiumContentWindow(IntPtr hWnd)
        {
            IntPtr best = IntPtr.Zero;
            long bestArea = 0;
            EnumChildWindows(hWnd, (child, _) =>
            {
                if (IsWindowVisible(child) && GetClass(child) == CHROMIUM_CONTENT_CLASS)
                {
                    var rect = GetFrameBounds(child);
                    long area = (long)(rect.Right - rect.Left) * (rect.Bottom - rect.Top);
                    if (area > bestArea)
                    {
                        bestArea = area;
                        best = child;
                    }
                }

                return true;
            }, IntPtr.Zero);

            return best;
        }

        private static Rectangle ToRectangle(RECT rect) => Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }
}
