using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Threading;
using Translumo.Configuration;
using Translumo.Utils;
using static Translumo.Services.Windows.NativeWindowMethods;

namespace Translumo.Services.Windows
{
    public enum TargetWindowState : byte
    {
        /// <summary>Capture mode is a fixed screen area, no window is tracked.</summary>
        AreaMode = 0,
        NoWindowSelected = 1,
        WindowNotFound = 2,
        WindowMinimized = 3,
        Tracking = 4
    }

    /// <summary>
    /// In window capture mode keeps <see cref="ScreenCaptureConfiguration.CaptureArea"/> on top of the selected window,
    /// suspends capture while the window is closed or minimized, and tells the overlay when it must be hidden
    /// because another window covers the target.
    /// </summary>
    public sealed class WindowTracker : BindableBase
    {
        public TargetWindowState State
        {
            get => _state;
            private set => SetProperty(ref _state, value);
        }

        /// <summary>False when drawing the overlay would cover another window (target is minimized or overlapped).</summary>
        public bool OverlayAllowed
        {
            get => _overlayAllowed;
            private set => SetProperty(ref _overlayAllowed, value);
        }

        public ScreenCaptureConfiguration Configuration { get; }

        private const int TRACK_INTERVAL_MS = 50;
        private const int SEARCH_INTERVAL_MS = 1000;
        // Share of the capture area another window must cover before the overlay is hidden
        private const double MAX_COVERED_RATIO = 0.1;
        private const int GW_HWNDPREV = 3;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        private readonly DispatcherTimer _timer;
        private readonly uint _ownProcessId = (uint)Environment.ProcessId;
        private TargetWindowState _state;
        private bool _overlayAllowed = true;
        private DateTime _lastSearchUtc = DateTime.MinValue;

        public WindowTracker(ScreenCaptureConfiguration configuration)
        {
            Configuration = configuration;
            Configuration.PropertyChanged += ConfigurationOnPropertyChanged;

            _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(TRACK_INTERVAL_MS) };
            _timer.Tick += (_, _) => Update();
            _timer.Start();
            Update();
        }

        /// <summary>
        /// Starts tracking the window; the capture area becomes its content area (page area for browsers).
        /// </summary>
        public void SetTarget(WindowInfo window)
        {
            Configuration.TargetHandle = window.Handle;
            Configuration.TargetProcessName = window.ProcessName;
            Configuration.TargetWindowTitle = window.Title;
            Configuration.TargetWindowClass = window.ClassName;
            Configuration.TargetRelativeArea = RectangleF.Empty;
            Configuration.Mode = CaptureMode.Window;
            Update();
        }

        /// <summary>
        /// Applies an area selected on the screen. If window mode is active and the area lies on the tracked window,
        /// it becomes a sub-area of that window (moves with it); otherwise capture switches to the fixed area.
        /// </summary>
        public void SetSelectedArea(RectangleF screenArea)
        {
            if (Configuration.Mode == CaptureMode.Window && State == TargetWindowState.Tracking)
            {
                var windowBounds = (RectangleF)WindowEnumerator.GetBounds(Configuration.TargetHandle);
                var inside = RectangleF.Intersect(windowBounds, screenArea);
                if (!inside.IsEmpty && inside.Width * inside.Height >= 0.5f * screenArea.Width * screenArea.Height)
                {
                    inside.Offset(-windowBounds.X, -windowBounds.Y);
                    Configuration.TargetRelativeArea = inside;
                    Update();
                    return;
                }
            }

            Configuration.Mode = CaptureMode.Area;
            Configuration.CaptureArea = screenArea;
            Update();
        }

        /// <summary>Use the whole content area of the tracked window again.</summary>
        public void ResetRelativeArea()
        {
            Configuration.TargetRelativeArea = RectangleF.Empty;
            Update();
        }

        /// <summary>Search for the target window again right away (e.g. after the user reopened it).</summary>
        public void Refresh()
        {
            Configuration.TargetHandle = IntPtr.Zero;
            _lastSearchUtc = DateTime.MinValue;
            Update();
        }

        private void ConfigurationOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ScreenCaptureConfiguration.Mode))
            {
                Update();
            }
        }

        private void Update()
        {
            if (Configuration.Mode != CaptureMode.Window)
            {
                Configuration.CaptureSuspended = false;
                State = TargetWindowState.AreaMode;
                OverlayAllowed = true;
                return;
            }

            if (!Configuration.HasTargetWindow)
            {
                Suspend(TargetWindowState.NoWindowSelected);
                return;
            }

            var handle = Configuration.TargetHandle;
            if (handle == IntPtr.Zero || !IsWindow(handle))
            {
                handle = FindTargetWindow();
                Configuration.TargetHandle = handle;
                if (handle == IntPtr.Zero)
                {
                    Suspend(TargetWindowState.WindowNotFound);
                    return;
                }
            }

            if (IsIconic(handle) || !IsWindowVisible(handle) || IsCloaked(handle))
            {
                Suspend(TargetWindowState.WindowMinimized);
                return;
            }

            var area = GetCaptureArea(handle);
            if (area.Width < 1 || area.Height < 1)
            {
                Suspend(TargetWindowState.WindowMinimized);
                return;
            }

            Configuration.CaptureArea = area;
            Configuration.CaptureSuspended = false;
            State = TargetWindowState.Tracking;
            OverlayAllowed = !IsCovered(handle, area);
        }

        private void Suspend(TargetWindowState state)
        {
            Configuration.CaptureSuspended = true;
            State = state;
            OverlayAllowed = false;
        }

        private RectangleF GetCaptureArea(IntPtr handle)
        {
            var relative = Configuration.TargetRelativeArea;
            if (relative.IsEmpty)
            {
                return WindowEnumerator.GetContentBounds(handle);
            }

            var bounds = (RectangleF)WindowEnumerator.GetBounds(handle);
            relative.Offset(bounds.X, bounds.Y);
            return RectangleF.Intersect(bounds, relative);
        }

        private IntPtr FindTargetWindow()
        {
            if ((DateTime.UtcNow - _lastSearchUtc).TotalMilliseconds < SEARCH_INTERVAL_MS)
            {
                return IntPtr.Zero;
            }

            _lastSearchUtc = DateTime.UtcNow;
            var candidates = WindowEnumerator.GetWindows()
                .Where(w => string.Equals(w.ProcessName, Configuration.TargetProcessName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Same title > same window class > topmost window of the process
            var match = candidates.FirstOrDefault(w => w.Title == Configuration.TargetWindowTitle)
                ?? candidates.FirstOrDefault(w => w.ClassName == Configuration.TargetWindowClass)
                ?? candidates.FirstOrDefault();

            if (match != null)
            {
                Configuration.TargetWindowTitle = match.Title;
            }

            return match?.Handle ?? IntPtr.Zero;
        }

        /// <summary>
        /// True if a window above the target in Z-order covers a noticeable part of the capture area.
        /// </summary>
        private bool IsCovered(IntPtr target, RectangleF area)
        {
            var areaSize = (double)area.Width * area.Height;
            var current = GetWindow(target, GW_HWNDPREV);
            while (current != IntPtr.Zero)
            {
                if (IsWindowVisible(current) && !IsIconic(current) && !IsCloaked(current))
                {
                    var exStyle = NativeWindowMethods.GetWindowLong(current, GWL_EXSTYLE);
                    GetWindowThreadProcessId(current, out var processId);
                    if ((exStyle & WS_EX_TRANSPARENT) == 0 && processId != _ownProcessId)
                    {
                        var covered = RectangleF.Intersect(WindowEnumerator.GetBounds(current), area);
                        if (!covered.IsEmpty && covered.Width * covered.Height / areaSize > MAX_COVERED_RATIO)
                        {
                            return true;
                        }
                    }
                }

                current = GetWindow(current, GW_HWNDPREV);
            }

            return false;
        }
    }
}
