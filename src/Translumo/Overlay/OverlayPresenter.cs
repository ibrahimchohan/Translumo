using System;
using System.Collections.Generic;
using System.ComponentModel;
using Translumo.Configuration;
using Translumo.Processing.Overlay;
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

        // Last shown frame and the scroll shift applied to it since, to redraw it when the style changes
        private IReadOnlyList<OverlayTextBlock> _lastBlocks = Array.Empty<OverlayTextBlock>();
        private float _shiftX;
        private float _shiftY;

        public OverlayPresenter(ChatUITextMediator mediator, ScreenCaptureConfiguration captureConfiguration,
            ChatWindowConfiguration styleConfiguration, WindowTracker windowTracker)
        {
            _captureConfiguration = captureConfiguration;
            _styleConfiguration = styleConfiguration;
            _windowTracker = windowTracker;

            mediator.OverlayBlocksRaised += MediatorOnOverlayBlocksRaised;
            mediator.OverlayShiftRaised += MediatorOnOverlayShiftRaised;
            captureConfiguration.PropertyChanged += CaptureConfigurationOnPropertyChanged;
            styleConfiguration.PropertyChanged += StyleConfigurationOnPropertyChanged;
            windowTracker.PropertyChanged += WindowTrackerOnPropertyChanged;
        }

        private void MediatorOnOverlayBlocksRaised(object sender, OverlayBlocksEventArgs e)
        {
            _lastBlocks = e.Blocks;
            _shiftX = _shiftY = 0;

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
            _shiftX += e.Dx;
            _shiftY += e.Dy;
            _window?.ShiftBlocks(e.Dx, e.Dy);
        }

        private void StyleConfigurationOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Font size, colors, opacity, alignment: redraw the current frame right away
            if (_window == null || _lastBlocks.Count == 0)
            {
                return;
            }

            _window.ShowBlocks(_captureConfiguration.CaptureArea, _lastBlocks, _styleConfiguration);
            if (_shiftX != 0 || _shiftY != 0)
            {
                _window.ShiftBlocks(_shiftX, _shiftY);
            }
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
