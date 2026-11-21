using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using Translumo.MVVM.ViewModels;
using Translumo.Utils;

namespace Translumo.MVVM.Views
{
    public partial class RegionFrameView : Window
    {
        // The outline is drawn just outside the capture area
        private const double FRAME_MARGIN = 3;
        private const double PLACEHOLDER_WIDTH = 640;
        private const double PLACEHOLDER_HEIGHT = 480;

        private RegionFrameViewModel ViewModel => DataContext as RegionFrameViewModel;

        public RegionFrameView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is RegionFrameViewModel oldViewModel)
            {
                oldViewModel.PropertyChanged -= ViewModelOnPropertyChanged;
            }

            if (e.NewValue is RegionFrameViewModel viewModel)
            {
                viewModel.PropertyChanged += ViewModelOnPropertyChanged;
                UpdatePlacement();
            }
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            WindowHelper.ExcludeFromCapture(this);
            UpdatePlacement();
        }

        private void ViewModelOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(RegionFrameViewModel.FrameArea) or nameof(RegionFrameViewModel.IsFrameVisible))
            {
                UpdatePlacement();
            }
        }

        private void UpdatePlacement()
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            if (!viewModel.IsFrameVisible)
            {
                if (IsVisible)
                {
                    Hide();
                }

                return;
            }

            Rect bounds;
            if (viewModel.FrameArea.IsEmpty)
            {
                var workArea = SystemParameters.WorkArea;
                bounds = new Rect(workArea.Left + (workArea.Width - PLACEHOLDER_WIDTH) / 2,
                    workArea.Top + (workArea.Height - PLACEHOLDER_HEIGHT) / 2, PLACEHOLDER_WIDTH, PLACEHOLDER_HEIGHT);
            }
            else
            {
                bounds = WindowHelper.ToDip(this, viewModel.FrameArea);
                bounds.Inflate(FRAME_MARGIN, FRAME_MARGIN);
            }

            Left = bounds.Left;
            Top = bounds.Top;
            Width = Math.Max(bounds.Width, 120);
            Height = Math.Max(bounds.Height, 60);

            if (!IsVisible)
            {
                Show();
            }
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            var menu = MenuButton.ContextMenu;
            if (menu == null)
            {
                return;
            }

            menu.PlacementTarget = MenuButton;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }
}
