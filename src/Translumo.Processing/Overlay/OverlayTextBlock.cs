using System.Drawing;

namespace Translumo.Processing.Overlay
{
    /// <summary>
    /// A translated text block to be drawn over the source text.
    /// Bounds are in physical pixels relative to the top-left corner of the capture area.
    /// </summary>
    public sealed class OverlayTextBlock
    {
        public string Original { get; }
        public string Translation { get; }
        public RectangleF Bounds { get; }
        public float LineHeight { get; }

        public OverlayTextBlock(string original, string translation, RectangleF bounds, float lineHeight)
        {
            Original = original;
            Translation = translation;
            Bounds = bounds;
            LineHeight = lineHeight;
        }
    }
}
