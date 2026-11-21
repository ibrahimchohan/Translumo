using System;
using OpenCvSharp;

namespace Translumo.Processing.Overlay
{
    /// <summary>
    /// Detects when the captured content was moved as a whole (scrolling) by phase correlation between
    /// consecutive frames, so overlay blocks can be moved immediately instead of waiting for the next OCR result.
    /// </summary>
    public sealed class ScrollShiftDetector : IDisposable
    {
        // Frames are compared at reduced resolution: faster and less sensitive to noise
        private const int DOWNSCALE = 2;
        // Minimum peak response: below it the change is not a clean shift (video, page change, animation)
        private const double MIN_RESPONSE = 0.2;
        private const float MIN_SHIFT_PX = 3;
        // Larger shifts can't be measured reliably (most of the content left the frame)
        private const float MAX_SHIFT_RATIO = 0.5f;

        private Mat _previous;
        private Mat _window;

        /// <param name="image">Encoded screenshot of the capture area.</param>
        /// <param name="dx">Horizontal content shift in pixels (positive = content moved right).</param>
        /// <param name="dy">Vertical content shift in pixels (positive = content moved down).</param>
        public bool TryDetectShift(byte[] image, out float dx, out float dy)
        {
            dx = dy = 0;
            var current = Prepare(image);
            if (current == null)
            {
                return false;
            }

            var previous = _previous;
            _previous = current;
            if (previous == null || previous.Size() != current.Size())
            {
                previous?.Dispose();
                _window?.Dispose();
                _window = new Mat();
                Cv2.CreateHanningWindow(_window, current.Size(), MatType.CV_32F);
                return false;
            }

            using (previous)
            {
                var shift = Cv2.PhaseCorrelate(previous, current, _window, out var response);
                if (response < MIN_RESPONSE)
                {
                    return false;
                }

                dx = (float)shift.X * DOWNSCALE;
                dy = (float)shift.Y * DOWNSCALE;
            }

            var width = current.Width * DOWNSCALE;
            var height = current.Height * DOWNSCALE;
            var significant = Math.Abs(dx) >= MIN_SHIFT_PX || Math.Abs(dy) >= MIN_SHIFT_PX;
            var measurable = Math.Abs(dx) < width * MAX_SHIFT_RATIO && Math.Abs(dy) < height * MAX_SHIFT_RATIO;
            if (!significant || !measurable)
            {
                dx = dy = 0;
                return false;
            }

            return true;
        }

        public void Reset()
        {
            _previous?.Dispose();
            _previous = null;
        }

        private static Mat Prepare(byte[] image)
        {
            using var gray = Cv2.ImDecode(image, ImreadModes.Grayscale);
            if (gray.Empty() || gray.Width < DOWNSCALE * 8 || gray.Height < DOWNSCALE * 8)
            {
                return null;
            }

            using var small = new Mat();
            Cv2.Resize(gray, small, new Size(gray.Width / DOWNSCALE, gray.Height / DOWNSCALE), 0, 0, InterpolationFlags.Area);
            var result = new Mat();
            small.ConvertTo(result, MatType.CV_32F);
            return result;
        }

        public void Dispose()
        {
            _previous?.Dispose();
            _window?.Dispose();
        }
    }
}
