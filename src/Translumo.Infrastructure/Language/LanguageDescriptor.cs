namespace Translumo.Infrastructure.Language
{
    public class LanguageDescriptor
    {
        public Languages Language { get; set; }

        public string Code { get; set; }

        public string EasyOcrCode { get; set; }

        public string IsoCode { get; set; }

        public string EasyOcrModel { get; set; }

        /// <summary>
        /// File name of the EasyOCR recognition model. When set and the file is missing from the models folder,
        /// EasyOCR is allowed to download it (models for newer languages are not in the bundled components).
        /// </summary>
        public string EasyOcrModelFile { get; set; }

        /// <summary>Windows OCR has a recognizer for this language (e.g. there is none for Vietnamese).</summary>
        public bool WindowsOcrSupported { get; set; } = true;

        /// <summary>
        /// ML model that scores how much recognized text looks like real text. Null: a heuristic score is used.
        /// </summary>
        public string TextScorePredictorModel { get; set; }

        public string[] SupportedNamedBlocks { get; set; }

        public bool UseEndPunctuation { get; set; }

        public bool UseWordTokenizer { get; set; }

        public bool UseSpaceRemover { get; set; }

        public bool TranslationOnly { get; set; } = false;

        public bool Asian { get; set; } = false;

        public bool RegionalVariant { get; set; } = false;

        public override bool Equals(object obj)
        {
            var langDesc = obj as LanguageDescriptor;
            if (langDesc is null)
            {
                return false;
            }

            return langDesc.Language == this.Language;
        }

        public override int GetHashCode()
        {
            return Language.GetHashCode();
        }
    }
}
