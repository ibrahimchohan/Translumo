using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Translumo.OCR
{
    /// <summary>
    /// Groups OCR lines into blocks (paragraphs, speech bubbles, subtitles) by geometric proximity.
    /// </summary>
    public static class TextBlockGrouper
    {
        // Lines whose heights differ more than this ratio are treated as different blocks (e.g. title vs body)
        private const float MAX_HEIGHT_RATIO = 1.8f;
        // Max vertical gap between stacked lines of the same block, relative to line height
        private const float MAX_LINE_GAP = 0.9f;
        // Min vertical overlap for two fragments to be on the same row, relative to line height
        private const float MIN_ROW_OVERLAP = 0.5f;
        // Max horizontal gap between fragments of the same row, relative to line height
        private const float MAX_ROW_GAP = 1.2f;

        public static OcrTextBlock[] Group(IReadOnlyList<OcrTextLine> lines, string separator)
        {
            var valid = lines
                .Where(l => !string.IsNullOrWhiteSpace(l.Text) && l.Bounds.Width > 0 && l.Bounds.Height > 0)
                .ToArray();
            if (valid.Length == 0)
            {
                return Array.Empty<OcrTextBlock>();
            }

            var parent = Enumerable.Range(0, valid.Length).ToArray();
            int Find(int i)
            {
                while (parent[i] != i)
                {
                    parent[i] = parent[parent[i]];
                    i = parent[i];
                }

                return i;
            }

            for (var i = 0; i < valid.Length; i++)
            {
                for (var j = i + 1; j < valid.Length; j++)
                {
                    if (AreMergeable(valid[i].Bounds, valid[j].Bounds))
                    {
                        parent[Find(i)] = Find(j);
                    }
                }
            }

            return Enumerable.Range(0, valid.Length)
                .GroupBy(Find)
                .Select(group => BuildBlock(group.Select(i => valid[i]).ToList(), separator))
                .OrderBy(block => block.Bounds.Top)
                .ThenBy(block => block.Bounds.Left)
                .ToArray();
        }

        private static bool AreMergeable(RectangleF a, RectangleF b)
        {
            var hMin = Math.Min(a.Height, b.Height);
            var hMax = Math.Max(a.Height, b.Height);
            if (hMax / hMin > MAX_HEIGHT_RATIO)
            {
                return false;
            }

            // Negative gap means overlap
            var vGap = Math.Max(a.Top, b.Top) - Math.Min(a.Bottom, b.Bottom);
            var hGap = Math.Max(a.Left, b.Left) - Math.Min(a.Right, b.Right);

            // Fragments of the same row split by OCR
            if (-vGap >= MIN_ROW_OVERLAP * hMin)
            {
                return hGap <= MAX_ROW_GAP * hMin;
            }

            // Stacked lines of one paragraph: close vertically and overlapping horizontally
            return vGap <= MAX_LINE_GAP * hMin && hGap < 0;
        }

        private static OcrTextBlock BuildBlock(List<OcrTextLine> lines, string separator)
        {
            var rows = new List<List<OcrTextLine>>();
            foreach (var line in lines.OrderBy(l => CenterY(l.Bounds)))
            {
                var row = rows.LastOrDefault();
                if (row != null)
                {
                    var rowHeight = row.Min(l => l.Bounds.Height);
                    if (Math.Abs(CenterY(row[0].Bounds) - CenterY(line.Bounds)) <= MIN_ROW_OVERLAP * Math.Min(rowHeight, line.Bounds.Height))
                    {
                        row.Add(line);
                        continue;
                    }
                }

                rows.Add(new List<OcrTextLine> { line });
            }

            var rowTexts = rows
                .Select(row => string.Join(separator, row.OrderBy(l => l.Bounds.Left).Select(l => l.Text.Trim())))
                .ToArray();

            var bounds = lines.Select(l => l.Bounds).Aggregate(RectangleF.Union);
            var heights = lines.Select(l => l.Bounds.Height).OrderBy(h => h).ToArray();
            var medianHeight = heights[heights.Length / 2];

            return new OcrTextBlock(string.Join(separator, rowTexts), rowTexts, bounds, medianHeight);
        }

        private static float CenterY(RectangleF rect) => rect.Top + rect.Height / 2;
    }
}
