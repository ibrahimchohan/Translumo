using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Translumo.Services.Windows;
using Translumo.Utils;

namespace Translumo.MVVM.Views
{
    /// <summary>
    /// Lets the user choose the window to translate: from a list (browsers first) or by clicking it on screen.
    /// </summary>
    public partial class WindowPickerWindow : Window
    {
        public WindowInfo SelectedWindow { get; private set; }

        private IReadOnlyList<WindowInfo> _windows = Array.Empty<WindowInfo>();

        public WindowPickerWindow()
        {
            InitializeComponent();
            LoadWindows();
            Loaded += (_, _) => SearchBox.Focus();
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            WindowHelper.ExcludeFromCapture(this);
        }

        private void LoadWindows()
        {
            // Browsers first (the main use case), then the rest in Z-order
            _windows = WindowEnumerator.GetWindows()
                .OrderByDescending(w => w.IsBrowser)
                .ToList();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var filter = SearchBox.Text?.Trim();
            WindowsList.ItemsSource = string.IsNullOrEmpty(filter)
                ? _windows
                : _windows.Where(w => w.Title.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
                                      || w.ProcessName.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
                    .ToList();
        }

        private void Accept(WindowInfo window)
        {
            if (window == null)
            {
                return;
            }

            SelectedWindow = window;
            DialogResult = true;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

        private void Refresh_Click(object sender, RoutedEventArgs e) => LoadWindows();

        private void WindowsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectButton.IsEnabled = WindowsList.SelectedItem != null;
        }

        private void WindowsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Accept(WindowsList.SelectedItem as WindowInfo);
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            Accept(WindowsList.SelectedItem as WindowInfo);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void PickOnScreen_Click(object sender, RoutedEventArgs e)
        {
            // Hide() would end the modal dialog, so make it invisible instead while the user picks a window
            Opacity = 0;
            try
            {
                var picker = new WindowPickOverlay();
                if (picker.ShowDialog() == true && picker.PickedWindow != null)
                {
                    Accept(picker.PickedWindow);
                }
            }
            finally
            {
                Opacity = 1;
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
            }
            else if (e.Key == Key.Enter)
            {
                Accept(WindowsList.SelectedItem as WindowInfo ?? (WindowsList.Items.Count > 0 ? WindowsList.Items[0] as WindowInfo : null));
            }
            else if (e.Key == Key.Down && SearchBox.IsKeyboardFocused && WindowsList.Items.Count > 0)
            {
                WindowsList.SelectedIndex = 0;
                (WindowsList.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem)?.Focus();
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Border or Grid or TextBlock && e.ButtonState == MouseButtonState.Pressed)
            {
                try
                {
                    DragMove();
                }
                catch (InvalidOperationException)
                {
                    // DragMove throws if the button was released meanwhile
                }
            }
        }
    }
}
