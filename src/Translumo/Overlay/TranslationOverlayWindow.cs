using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Translumo.Configuration;
using Translumo.Processing.Overlay;
using Translumo.Utils;

namespace Translumo.Overlay
{
    /// <summary>
    /// Transparent, click-through, always-on-top window that draws translated text blocks over the capture area.
    /// The window is excluded from screen capture so its own text is never fed back into OCR.
    /// </summary>
    internal sealed class TranslationOverlayWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x11;

        private const double MIN_FONT_SIZE = 8;
        private const double BLOCK_PADDING = 2;
        // Font size relative to the source line height (line boxes include ascenders/descenders)
        private const double LINE_HEIGHT_TO_FONT_RATIO = 0.85;
        private const int FIT_ITERATIONS = 8;

        private readonly Canvas _canvas = new Canvas();

        [DllImport("user32.dll")]
        private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);

        public TranslationOverlayWindow()
        {
            Title = "Translumo overlay";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            Focusable = false;
            IsHitTestVisible = false;
            Content = _canvas;

            // Create the handle now: click-through styles, capture exclusion and DPI apply before the first Show()
            new WindowInteropHelper(this).EnsureHandle();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwnd = new WindowInteropHelper(this).Handle;
            var exStyle = Win32Interfaces.GetWindowLong(hwnd, GWL_EXSTYLE);
            Win32Interfaces.SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);

            // Required: without it the overlay text would be captured, recognized and translated again
            SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
        }

        private System.Drawing.RectangleF _area;
        private bool _hasBlocks;
        private bool _suppressed;

        /// <summary>
        /// Moves the overlay over the capture area (physical screen pixels). Blocks keep their position relative
        /// to the area, so they follow the target window when it is moved.
        /// </summary>
        public void UpdateArea(System.Drawing.RectangleF captureArea)
        {
            if (captureArea.IsEmpty || captureArea == _area)
            {
                return;
            }

            _area = captureArea;
            var dpi = VisualTreeHelper.GetDpi(this);
            Left = captureArea.X / dpi.DpiScaleX;
            Top = captureArea.Y / dpi.DpiScaleY;
            Width = captureArea.Width / dpi.DpiScaleX;
            Height = captureArea.Height / dpi.DpiScaleY;
        }

        /// <summary>
        /// Hides the overlay without dropping its blocks (target window is covered, minimized or the user hid it).
        /// </summary>
        public void SetSuppressed(bool suppressed)
        {
            _suppressed = suppressed;
            UpdateVisibility();
        }

        /// <param name="captureArea">Capture area in physical screen pixels.</param>
        /// <param name="blocks">Blocks with bounds in physical pixels relative to the capture area.</param>
        public void ShowBlocks(System.Drawing.RectangleF captureArea, IReadOnlyList<OverlayTextBlock> blocks, ChatWindowConfiguration style)
        {
            if (blocks.Count == 0 || captureArea.IsEmpty)
            {
                ClearBlocks();
                return;
            }

            UpdateArea(captureArea);

            var dpi = VisualTreeHelper.GetDpi(this);
            var background = new SolidColorBrush(style.BackgroundColor) { Opacity = style.BackgroundOpacity };
            var foreground = new SolidColorBrush(style.FontColor);
            background.Freeze();
            foreground.Freeze();
            var typeface = new Typeface(FontFamily, FontStyles.Normal, style.FontBold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);

            _canvas.Children.Clear();
            foreach (var block in blocks)
            {
                var element = CreateBlockElement(block, dpi, typeface, background, foreground, style.TextAlignment);
                Canvas.SetLeft(element, block.Bounds.X / dpi.DpiScaleX);
                Canvas.SetTop(element, block.Bounds.Y / dpi.DpiScaleY);
                _canvas.Children.Add(element);
            }

            _hasBlocks = true;
            UpdateVisibility();
        }

        /// <summary>
        /// Moves all blocks by the given offset (physical pixels), e.g. when the content was scrolled.
        /// </summary>
        public void ShiftBlocks(float dx, float dy)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            foreach (UIElement child in _canvas.Children)
            {
                Canvas.SetLeft(child, Canvas.GetLeft(child) + dx / dpi.DpiScaleX);
                Canvas.SetTop(child, Canvas.GetTop(child) + dy / dpi.DpiScaleY);
            }
        }

        public void ClearBlocks()
        {
            _canvas.Children.Clear();
            _hasBlocks = false;
            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            var visible = _hasBlocks && !_suppressed;
            if (visible && !IsVisible)
            {
                Show();
            }
            else if (!visible && IsVisible)
            {
                Hide();
            }
        }

        private FrameworkElement CreateBlockElement(OverlayTextBlock block, DpiScale dpi, Typeface typeface, Brush background,
            Brush foreground, TextAlignment alignment)
        {
            var width = block.Bounds.Width / dpi.DpiScaleX;
            var height = block.Bounds.Height / dpi.DpiScaleY;
            var maxFontSize = Math.Max(MIN_FONT_SIZE, block.LineHeight / dpi.DpiScaleY * LINE_HEIGHT_TO_FONT_RATIO);
            var innerWidth = Math.Max(1, width - BLOCK_PADDING * 2);
            var innerHeight = Math.Max(1, height - BLOCK_PADDING * 2);
            var fontSize = FitFontSize(block.Translation, innerWidth, innerHeight, maxFontSize, typeface, dpi.PixelsPerDip);

            var text = new TextBlock
            {
                Text = block.Translation,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = alignment,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = typeface.FontFamily,
                FontWeight = typeface.Weight,
                FontSize = fontSize,
                Foreground = foreground
            };

            // MinHeight instead of Height: if the translation does not fit even at the minimum font size,
            // the box grows downwards instead of cutting the text.
            return new Border
            {
                Width = width,
                MinHeight = height,
                Padding = new Thickness(BLOCK_PADDING),
                CornerRadius = new CornerRadius(3),
                Background = background,
                Child = text
            };
        }

        /// <summary>
        /// Largest font size (up to the source text size) at which the text fits the box.
        /// </summary>
        private static double FitFontSize(string text, double width, double height, double maxFontSize, Typeface typeface, double pixelsPerDip)
        {
            bool Fits(double size)
            {
                var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, size,
                    Brushes.Black, pixelsPerDip)
                {
                    MaxTextWidth = width
                };

                return formatted.Height <= height && formatted.WidthIncludingTrailingWhitespace <= width + 0.5;
            }

            if (Fits(maxFontSize))
            {
                return maxFontSize;
            }

            double low = MIN_FONT_SIZE, high = maxFontSize;
            for (var i = 0; i < FIT_ITERATIONS && high - low > 0.5; i++)
            {
                var mid = (low + high) / 2;
                if (Fits(mid))
                {
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }

            return Math.Floor(low);
        }
    }
}
