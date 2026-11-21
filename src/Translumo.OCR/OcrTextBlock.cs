using System.Drawing;

namespace Translumo.OCR
{
    /// <summary>
    /// A single recognized line of text with its position inside the captured image (pixels).
    /// </summary>
    public sealed class OcrTextLine
    {
        public string Text { get; }
        public RectangleF Bounds { get; }

        public OcrTextLine(string text, RectangleF bounds)
        {
            Text = text;
            Bounds = bounds;
        }
    }

    /// <summary>
    /// A group of lines that belong together (paragraph, speech bubble, subtitle).
    /// Bounds are in pixels relative to the captured image.
    /// </summary>
    public sealed class OcrTextBlock
    {
        public string Text { get; }
        public string[] Lines { get; }
        public RectangleF Bounds { get; }

        /// <summary>Median height of the source lines, used to pick a similar font size.</summary>
        public float LineHeight { get; }

        public OcrTextBlock(string text, string[] lines, RectangleF bounds, float lineHeight)
        {
            Text = text;
            Lines = lines;
            Bounds = bounds;
            LineHeight = lineHeight;
        }
    }
}
