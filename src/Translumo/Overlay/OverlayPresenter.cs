using Translumo.Configuration;
using Translumo.Services;

namespace Translumo.Overlay
{
    /// <summary>
    /// Shows overlay blocks raised by the processing service in <see cref="TranslationOverlayWindow"/>.
    /// </summary>
    public sealed class OverlayPresenter
    {
        private readonly ScreenCaptureConfiguration _captureConfiguration;
        private readonly ChatWindowConfiguration _styleConfiguration;
        private TranslationOverlayWindow _window;

        public OverlayPresenter(ChatUITextMediator mediator, ScreenCaptureConfiguration captureConfiguration,
            ChatWindowConfiguration styleConfiguration)
        {
            _captureConfiguration = captureConfiguration;
            _styleConfiguration = styleConfiguration;

            mediator.OverlayBlocksRaised += MediatorOnOverlayBlocksRaised;
        }

        private void MediatorOnOverlayBlocksRaised(object sender, OverlayBlocksEventArgs e)
        {
            if (e.Blocks.Count == 0)
            {
                _window?.ClearBlocks();
                return;
            }

            _window ??= new TranslationOverlayWindow();
            _window.ShowBlocks(_captureConfiguration.CaptureArea, e.Blocks, _styleConfiguration);
        }
    }
}
