using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using Translumo.Infrastructure.Language;

namespace Translumo.OCR.WindowsOCR
{
    public class WindowsOCREngine : IOCREngine
    {
        public Languages DetectionLanguage => LanguageDescriptor.Language;
        public virtual int Confidence => 5;
        public virtual byte PrimaryPriority => 10;
        public bool SecondaryPrimaryCheck => true;

        protected readonly OcrEngine MsEngine;
        protected readonly LanguageDescriptor LanguageDescriptor;

        protected ImageFormat ImgFormat;

        public WindowsOCREngine(LanguageDescriptor languageDescriptor)
        {
            LanguageDescriptor = languageDescriptor;
            MsEngine = OcrEngine.TryCreateFromLanguage(new Language(LanguageDescriptor.Code));
            ImgFormat = ImageFormat.Tiff;
        }

        public string[] GetTextLines(byte[] image)
        {
            OcrResult result = Recognize(image);
            if (result == null)
                return [];

            return result.Lines
                .Select(GetLineText)
                .ToArray();
        }

        public OcrTextBlock[] GetTextBlocks(byte[] image)
        {
            OcrResult result = Recognize(image);
            if (result == null)
                return [];

            var lines = result.Lines
                .Where(line => line.Words.Count > 0)
                .Select(line => new OcrTextLine(GetLineText(line), GetLineBounds(line)))
                .ToArray();

            return TextBlockGrouper.Group(lines, LanguageDescriptor.UseSpaceRemover ? string.Empty : " ");
        }

        private OcrResult Recognize(byte[] image)
        {
            if (MsEngine == null)
                return null;

            using var stream = new MemoryStream(image);
            using Bitmap bitmap = PreProcess(new Bitmap(stream));

            SoftwareBitmap softwareBitmap;
            using (var randStream = new InMemoryRandomAccessStream())
            {
                bitmap.Save(randStream.AsStream(), ImgFormat);
                var decoder = BitmapDecoder.CreateAsync(randStream).AsTask().Result;
                softwareBitmap = decoder.GetSoftwareBitmapAsync().AsTask().Result;
            }

            using (softwareBitmap)
            {
                return MsEngine.RecognizeAsync(softwareBitmap).AsTask().Result;
            }
        }

        private string GetLineText(OcrLine line)
        {
            return LanguageDescriptor.UseSpaceRemover ? line.Text.Replace(" ", string.Empty) : line.Text;
        }

        private static RectangleF GetLineBounds(OcrLine line)
        {
            var rects = line.Words.Select(word => word.BoundingRect).ToArray();
            var left = rects.Min(r => r.X);
            var top = rects.Min(r => r.Y);
            var right = rects.Max(r => r.X + r.Width);
            var bottom = rects.Max(r => r.Y + r.Height);

            return new RectangleF((float)left, (float)top, (float)(right - left), (float)(bottom - top));
        }

        protected virtual Bitmap PreProcess(Bitmap bitmap)
        {
            return bitmap;
        }
    }
}
