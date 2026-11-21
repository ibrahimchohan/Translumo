using System;
using System.Windows;
using System.Windows.Input;
using Translumo.Utils;

namespace Translumo.MVVM.Views
{
    public partial class ControlBarView : Window
    {
        private const double BOTTOM_OFFSET = 90;

        public ControlBarView()
        {
            InitializeComponent();
            Loaded += (_, _) => PlaceAtBottomCenter();
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            // Keep the bar out of screenshots so it is never recognized as text
            WindowHelper.ExcludeFromCapture(this);
        }

        private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void PlaceAtBottomCenter()
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Left + (workArea.Width - ActualWidth) / 2;
            Top = workArea.Bottom - ActualHeight - BOTTOM_OFFSET;
        }
    }
}
