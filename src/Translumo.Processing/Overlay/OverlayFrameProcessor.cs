using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Translumo.Infrastructure.Collections;
using Translumo.Infrastructure.Constants;
using Translumo.OCR;
using Translumo.Translation;

namespace Translumo.Processing.Overlay
{
    /// <summary>
    /// Translates OCR text blocks for the overlay mode. Keeps a per-block translation cache so that
    /// text which only moved (scrolling, window move) is re-positioned without new translation requests,
    /// and suppresses publishing of frames that did not change.
    /// </summary>
    public sealed class OverlayFrameProcessor
    {
        private const int CACHE_CAPACITY = 500;
        private const int MAX_PARALLEL_REQUESTS = 3;
        // Number of consecutive frames without text before the overlay is cleared (avoids flicker on OCR misses)
        private const int EMPTY_FRAMES_BEFORE_CLEAR = 2;
        // Positions are compared in buckets of this size (px) to ignore OCR jitter
        private const int POSITION_BUCKET_PX = 4;
        private const char BATCH_SEPARATOR = '\n';

        private readonly object _sync = new object();
        private readonly LimitedDictionary<string, string> _cache = new LimitedDictionary<string, string>(CACHE_CAPACITY, StringComparer.Ordinal);
        // Signature of the published frame: empty = nothing shown, null = unknown (forces publishing)
        private string _lastSignature = string.Empty;
        private const string InvalidatedSignature = "\0invalidated";
        private int _emptyFrames;

        /// <summary>True when the overlay currently shows translated blocks.</summary>
        public bool HasVisibleBlocks
        {
            get
            {
                lock (_sync)
                {
                    return !string.IsNullOrEmpty(_lastSignature);
                }
            }
        }

        /// <summary>
        /// The UI moved the shown blocks (scrolling): the next frame must be published even if it matches the last one.
        /// </summary>
        public void InvalidatePublishedFrame()
        {
            lock (_sync)
            {
                _lastSignature = InvalidatedSignature;
            }
        }

        /// <summary>
        /// Drops cached translations (translator or language changed) and forces the next frame to be published.
        /// </summary>
        public void Reset()
        {
            lock (_sync)
            {
                _cache.Clear();
                _lastSignature = null;
                _emptyFrames = 0;
            }
        }

        public async Task<IReadOnlyList<OverlayTextBlock>> TranslateAsync(IReadOnlyList<OcrTextBlock> blocks, ITranslator translator,
            CancellationToken cancellationToken)
        {
            var keys = blocks.Select(block => Normalize(block.Text)).ToArray();
            List<string> missing;
            lock (_sync)
            {
                missing = keys.Distinct().Where(key => key.Length > 0 && !_cache.ContainsKey(key)).ToList();
            }

            if (missing.Count > 0)
            {
                var translations = await TranslateMissingAsync(missing, translator, cancellationToken).ConfigureAwait(false);
                lock (_sync)
                {
                    foreach (var (source, translation) in translations)
                    {
                        _cache[source] = translation;
                    }
                }
            }

            var result = new List<OverlayTextBlock>(blocks.Count);
            lock (_sync)
            {
                for (var i = 0; i < blocks.Count; i++)
                {
                    if (_cache.TryGetValue(keys[i], out var translation) && !string.IsNullOrWhiteSpace(translation))
                    {
                        result.Add(new OverlayTextBlock(blocks[i].Text, translation, blocks[i].Bounds, blocks[i].LineHeight));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the frame to send to the UI, or null when the overlay already shows the same content.
        /// </summary>
        public IReadOnlyList<OverlayTextBlock> GetFrameToPublish(IReadOnlyList<OverlayTextBlock> frame)
        {
            lock (_sync)
            {
                if (frame.Count == 0)
                {
                    _emptyFrames++;
                    if (_lastSignature != string.Empty && _emptyFrames >= EMPTY_FRAMES_BEFORE_CLEAR)
                    {
                        _lastSignature = string.Empty;
                        return Array.Empty<OverlayTextBlock>();
                    }

                    return null;
                }

                _emptyFrames = 0;
                var signature = string.Join("|", frame.Select(block =>
                    $"{Bucket(block.Bounds.X)},{Bucket(block.Bounds.Y)},{Bucket(block.Bounds.Width)},{Bucket(block.Bounds.Height)}:{block.Translation}"));
                if (signature == _lastSignature)
                {
                    return null;
                }

                _lastSignature = signature;
                return frame;
            }
        }

        private static async Task<IReadOnlyList<(string Source, string Translation)>> TranslateMissingAsync(List<string> sources,
            ITranslator translator, CancellationToken cancellationToken)
        {
            if (sources.Count > 1)
            {
                // One request for the whole frame: faster, cheaper and gives the translator more context.
                // Falls back to per-block requests if the translator merged or split lines.
                var batchResult = await translator.TranslateTextAsync(string.Join(BATCH_SEPARATOR, sources)).ConfigureAwait(false);
                var parts = (batchResult ?? string.Empty)
                    .Split(BATCH_SEPARATOR)
                    .Select(part => part.Trim())
                    .Where(part => part.Length > 0)
                    .ToArray();
                if (parts.Length == sources.Count)
                {
                    return sources.Zip(parts, (source, translation) => (source, translation)).ToArray();
                }
            }

            using var throttler = new SemaphoreSlim(MAX_PARALLEL_REQUESTS);
            var tasks = sources.Select(async source =>
            {
                await throttler.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var translation = await translator.TranslateTextAsync(source).ConfigureAwait(false);
                    return (source, translation?.Trim() ?? string.Empty);
                }
                finally
                {
                    throttler.Release();
                }
            });

            return await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private static string Normalize(string text)
        {
            return RegexStorage.MultipleSpacesRegex.Replace(text ?? string.Empty, " ").Trim();
        }

        private static int Bucket(float value) => (int)Math.Round(value / POSITION_BUCKET_PX);
    }
}
