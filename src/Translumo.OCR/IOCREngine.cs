using Translumo.Infrastructure.Language;

namespace Translumo.OCR
{
    public interface IOCREngine
    {
        byte PrimaryPriority { get; }
        bool SecondaryPrimaryCheck { get; }
        int Confidence { get; }
        Languages DetectionLanguage { get; }

        string[] GetTextLines(byte[] image);

        /// <summary>
        /// Recognizes text and keeps its position: lines are grouped into blocks with pixel bounds
        /// relative to the image. Used by the on-screen overlay mode.
        /// </summary>
        OcrTextBlock[] GetTextBlocks(byte[] image);
    }
}
