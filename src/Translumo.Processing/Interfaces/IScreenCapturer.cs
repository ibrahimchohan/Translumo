using System;
using System.Drawing;

namespace Translumo.Processing.Interfaces
{
    public interface IScreenCapturer : IDisposable
    {
        int CaptureAttempts { get; set; }

        /// <summary>
        /// False while the capture source is temporarily unavailable (e.g. the target window is minimized or closed).
        /// </summary>
        bool IsCaptureAvailable { get; }

        void Initialize();
        byte[] CaptureScreen();
        byte[] CaptureScreen(RectangleF captureArea);
    }
}
