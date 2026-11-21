using System.ComponentModel;
using Translumo.Configuration;
using Translumo.Services;
using Translumo.Services.Windows;

namespace Translumo.Overlay
{
    /// <summary>
    /// Shows overlay blocks raised by the processing service in <see cref="TranslationOverlayWindow"/>,
    /// keeps the overlay on top of the tracked window and hides it while the window is covered or minimized.
    /// </summary>
    public sealed class OverlayPresenter
    {
        private readonly ScreenCaptureConfiguration _captureConfiguration;
        private readonly ChatWindowConfiguration _styleConfiguration;
        private readonly WindowTracker _windowTracker;
        private TranslationOverlayWindow _window;

        public OverlayPresenter(ChatUITextMediator mediator, ScreenCaptureConfiguration captureConfiguration,
            ChatWindowConfiguration styleConfiguration, WindowTracker windowTracker)
        {
            _captureConfiguration = captureConfiguration;
            _styleConfiguration = styleConfiguration;
            _windowTracker = windowTracker;

            mediator.OverlayBlocksRaised += MediatorOnOverlayBlocksRaised;
            mediator.OverlayShiftRaised += MediatorOnOverlayShiftRaised;
            captureConfiguration.PropertyChanged += CaptureConfigurationOnPropertyChanged;
            windowTracker.PropertyChanged += WindowTrackerOnPropertyChanged;
        }

        private void MediatorOnOverlayBlocksRaised(object sender, OverlayBlocksEventArgs e)
        {
            if (e.Blocks.Count == 0)
            {
                _window?.ClearBlocks();
                return;
            }

            if (_window == null)
            {
                _window = new TranslationOverlayWindow();
                _window.SetSuppressed(!_windowTracker.OverlayAllowed);
            }

            _window.ShowBlocks(_captureConfiguration.CaptureArea, e.Blocks, _styleConfiguration);
        }

        private void MediatorOnOverlayShiftRaised(object sender, OverlayShiftEventArgs e)
        {
            _window?.ShiftBlocks(e.Dx, e.Dy);
        }

        private void CaptureConfigurationOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ScreenCaptureConfiguration.CaptureArea))
            {
                _window?.UpdateArea(_captureConfiguration.CaptureArea);
            }
        }

        private void WindowTrackerOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WindowTracker.OverlayAllowed))
            {
                _window?.SetSuppressed(!_windowTracker.OverlayAllowed);
            }
        }
    }
}
