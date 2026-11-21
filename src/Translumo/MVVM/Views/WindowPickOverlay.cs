using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Translumo.Services.Windows;
using Translumo.Utils;

namespace Translumo.MVVM.Views
{
    /// <summary>
    /// Full-screen picker: highlights the window under the cursor, a click selects it, Esc or right click cancels.
    /// </summary>
    internal sealed class WindowPickOverlay : Window
    {
        public WindowInfo PickedWindow { get; private set; }

        private readonly IReadOnlyList<WindowInfo> _windows;
        private readonly Border _highlight;
        private readonly Border _label;
        private readonly TextBlock _labelText;
        private WindowInfo _hovered;

        public WindowPickOverlay()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            // Almost transparent: still receives the mouse, the screen stays visible
            Background = new SolidColorBrush(Color.FromArgb(24, 0, 0, 0));
            ShowInTaskbar = false;
            Topmost = true;
            Cursor = Cursors.Cross;
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            // Snapshot in Z-order; this application's windows (including this one) are excluded
            _windows = WindowEnumerator.GetWindows();

            var accent = (Brush)Application.Current.TryFindResource("Bar.AccentHover") ?? Brushes.SteelBlue;
            var panel = (Brush)Application.Current.TryFindResource("Bar.Background") ?? Brushes.Black;
            var foreground = (Brush)Application.Current.TryFindResource("Bar.Foreground") ?? Brushes.White;

            _highlight = new Border
            {
                BorderBrush = accent,
                BorderThickness = new Thickness(3),
                Background = new SolidColorBrush(Color.FromArgb(40, 91, 102, 214)),
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            _labelText = new TextBlock { Foreground = foreground, FontSize = 13 };
            _label = new Border
            {
                Background = panel,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 5, 10, 5),
                Child = _labelText,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            var hint = new Border
            {
                Background = panel,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 8, 14, 8),
                IsHitTestVisible = false,
                Child = new TextBlock
                {
                    Foreground = foreground,
                    FontSize = 14,
                    Text = LocalizationManager.GetValue("Str.Picker.PickHint") ?? "Click a window to translate it. Esc to cancel."
                }
            };

            var canvas = new Canvas();
            canvas.Children.Add(_highlight);
            canvas.Children.Add(_label);
            canvas.Children.Add(hint);
            Content = canvas;

            Loaded += (_, _) =>
            {
                var work = SystemParameters.WorkArea;
                hint.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(hint, work.Left - Left + (work.Width - hint.DesiredSize.Width) / 2);
                Canvas.SetTop(hint, work.Top - Top + 24);
                Activate();
                UpdateHighlight();
            };
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            UpdateHighlight();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            UpdateHighlight();
            if (_hovered != null)
            {
                PickedWindow = _hovered;
                DialogResult = true;
            }
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonUp(e);
            DialogResult = false;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
            }
        }

        private void UpdateHighlight()
        {
            NativeWindowMethods.GetCursorPos(out var cursor);
            var hovered = _windows.FirstOrDefault(w => w.Bounds.Contains(cursor.X, cursor.Y));
            _hovered = hovered;
            if (hovered == null)
            {
                _highlight.Visibility = Visibility.Collapsed;
                _label.Visibility = Visibility.Collapsed;
                return;
            }

            var bounds = WindowHelper.ToDip(this, hovered.Bounds);
            bounds.Offset(-Left, -Top);

            Canvas.SetLeft(_highlight, bounds.Left);
            Canvas.SetTop(_highlight, bounds.Top);
            _highlight.Width = bounds.Width;
            _highlight.Height = bounds.Height;
            _highlight.Visibility = Visibility.Visible;

            _labelText.Text = $"{hovered.Title}  ·  {hovered.ProcessName}";
            Canvas.SetLeft(_label, bounds.Left + 8);
            Canvas.SetTop(_label, bounds.Top + 8);
            _label.Visibility = Visibility.Visible;
        }
    }
}
