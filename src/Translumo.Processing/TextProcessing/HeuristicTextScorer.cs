using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Translumo.Processing.TextProcessing
{
    /// <summary>
    /// Text validity score for languages without a trained prediction model. Produces values on the same scale as the
    /// ML model (0 = garbage, ~5 = clean text), based on how much of the text consists of letters of the language
    /// and on typical OCR noise patterns.
    /// </summary>
    public static class HeuristicTextScorer
    {
        private const float MAX_SCORE = 5f;
        private const int SHORT_TEXT_LENGTH = 3;

        /// <param name="text">Pre-processed text.</param>
        /// <param name="alphabetRegex">Zero-width regex matching each letter of the language alphabet.</param>
        /// <param name="ideographic">CJK text: single-character words are normal, there is no letter case.</param>
        public static float Score(string text, Regex alphabetRegex, bool ideographic)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0f;
            }

            var nonSpace = text.Count(c => !char.IsWhiteSpace(c));
            if (nonSpace == 0)
            {
                return 0f;
            }

            var letters = alphabetRegex.Matches(text).Count;
            var digits = text.Count(char.IsDigit);
            // Digits are neutral (prices, numbers in subtitles), other symbols count against the text
            var letterRatio = (float)letters / Math.Max(1, nonSpace - digits);
            var score = MAX_SCORE * Math.Min(1f, letterRatio);

            if (nonSpace < SHORT_TEXT_LENGTH)
            {
                score *= 0.5f;
            }

            if (ideographic)
            {
                return score;
            }

            var words = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 3)
            {
                // OCR noise often looks like many single-character "words": "l i | . ,"
                var singleCharWords = words.Count(w => w.Length == 1);
                if (singleCharWords > words.Length / 2)
                {
                    score *= 0.5f;
                }
            }

            // Mixed case inside words ("tHiS") is typical for misrecognized text
            var mixedCaseWords = words.Count(w => w.Length > 2 && w.Skip(1).Any(char.IsUpper) && w.Skip(1).Any(char.IsLower));
            if (words.Length > 0 && mixedCaseWords > words.Length / 3)
            {
                score *= 0.7f;
            }

            return score;
        }
    }
}
