using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Input;
using System.Windows.Threading;
using Translumo.Configuration;
using Translumo.Services.Windows;
using Translumo.Utils;
using RelayCommand = Microsoft.Toolkit.Mvvm.Input.RelayCommand;

namespace Translumo.MVVM.ViewModels
{
    /// <summary>
    /// Outline of the capture region with its number, a menu and a status panel
    /// ("window is not open", "nothing selected"). Shown while translation is stopped or when something needs attention.
    /// </summary>
    public sealed class RegionFrameViewModel : BindableBase
    {
        public string RegionNumber => "1";

        /// <summary>Region in physical screen pixels; empty when nothing was selected yet.</summary>
        public RectangleF FrameArea
        {
            get => _frameArea;
            private set => SetProperty(ref _frameArea, value);
        }

        public bool IsFrameVisible
        {
            get => _isFrameVisible;
            private set => SetProperty(ref _isFrameVisible, value);
        }

        public bool HasProblem
        {
            get => _hasProblem;
            private set => SetProperty(ref _hasProblem, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>The window is known but closed/minimized: searching again makes sense.</summary>
        public bool CanRefresh
        {
            get => _canRefresh;
            private set => SetProperty(ref _canRefresh, value);
        }

        public bool CanUseWholeWindow => _captureConfiguration.Mode == CaptureTargetMode.Window && !_captureConfiguration.TargetRelativeArea.IsEmpty;

        public ICommand SelectWindowCommand => new RelayCommand(_controller.SelectWindow);
        public ICommand SelectAreaCommand => new RelayCommand(_controller.SelectArea);
        public ICommand RefreshCommand => new RelayCommand(_windowTracker.Refresh);
        public ICommand UseWholeWindowCommand => new RelayCommand(_windowTracker.ResetRelativeArea);
        public ICommand HideFrameCommand => new RelayCommand(() =>
        {
            _hiddenByUser = true;
            Update();
        });

        private readonly ChatWindowViewModel _controller;
        private readonly ScreenCaptureConfiguration _captureConfiguration;
        private readonly WindowTracker _windowTracker;
        private RectangleF _frameArea;
        private bool _isFrameVisible;
        private bool _hasProblem;
        private string _statusMessage;
        private bool _canRefresh;
        private bool _hiddenByUser;

        public RegionFrameViewModel(ChatWindowViewModel controller, ScreenCaptureConfiguration captureConfiguration, WindowTracker windowTracker)
        {
            _controller = controller;
            _captureConfiguration = captureConfiguration;
            _windowTracker = windowTracker;

            _captureConfiguration.PropertyChanged += CaptureConfigurationOnPropertyChanged;
            _windowTracker.PropertyChanged += (_, _) => Update();

            var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(300) };
            timer.Tick += (_, _) => Update();
            timer.Start();

            Update();
        }

        private void CaptureConfigurationOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ScreenCaptureConfiguration.Mode) or nameof(ScreenCaptureConfiguration.TargetProcessName)
                or nameof(ScreenCaptureConfiguration.TargetRelativeArea))
            {
                // A new target was chosen: show its frame again
                _hiddenByUser = false;
                OnPropertyChanged(nameof(CanUseWholeWindow));
            }

            Update();
        }

        private void Update()
        {
            var processName = _captureConfiguration.TargetProcessName;
            string message = null;
            var canRefresh = false;
            switch (_windowTracker.State)
            {
                case TargetWindowState.AreaMode when _captureConfiguration.CaptureArea.IsEmpty:
                    message = LocalizationManager.GetValue("Str.Region.NoTarget");
                    break;
                case TargetWindowState.NoWindowSelected:
                    message = LocalizationManager.GetValue("Str.Region.NoWindow");
                    break;
                case TargetWindowState.WindowNotFound:
                    message = string.Format(LocalizationManager.GetValue("Str.Region.WindowNotOpen") ?? "{0}", processName);
                    canRefresh = true;
                    break;
                case TargetWindowState.WindowMinimized:
                    message = string.Format(LocalizationManager.GetValue("Str.Region.WindowMinimized") ?? "{0}", processName);
                    canRefresh = true;
                    break;
            }

            HasProblem = message != null;
            StatusMessage = message;
            CanRefresh = canRefresh;
            FrameArea = _captureConfiguration.CaptureArea;

            var isRunning = _controller.Model.TranslationIsRunning;
            var coveredByOtherWindow = _windowTracker.State == TargetWindowState.Tracking && !_windowTracker.OverlayAllowed;
            IsFrameVisible = !_hiddenByUser && (HasProblem || (!isRunning && !coveredByOtherWindow));
        }
    }
}
