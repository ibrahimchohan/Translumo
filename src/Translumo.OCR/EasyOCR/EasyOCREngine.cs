using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Python.Runtime;
using Translumo.Infrastructure.Constants;
using Translumo.Infrastructure.Language;
using Translumo.Infrastructure.Python;

namespace Translumo.OCR.EasyOCR
{
    public class EasyOCREngine : IOCREngine, IDisposable
    {
        public byte PrimaryPriority => 2;
        public bool SecondaryPrimaryCheck => false;
        public int Confidence => 9;
        public Languages DetectionLanguage => _languageDescriptor.Language;

        private bool _objectsInitialized;
        private bool _readerIsUsed;

        private readonly object _obj = new object();
        private readonly LanguageDescriptor _languageDescriptor;
        private readonly PythonEngineWrapper _pythonEngine;
        private readonly string _modelPath = Path.Combine(Global.ModelsPath, "easyocr");
        private readonly ILogger _logger;

        #region Python objects
        private dynamic _builtinsLib;
        private dynamic _easyOcrLib;
        private dynamic _reader;
        private dynamic _bytes;
        #endregion

        public EasyOCREngine(LanguageDescriptor languageDescriptor, PythonEngineWrapper pythonEngine, ILogger logger)
        {
            _languageDescriptor = languageDescriptor;
            _pythonEngine = pythonEngine;
            _logger = logger;

            _logger.LogTrace($"Initialization EasyOCR from path: '{Global.PythonPath}'");

            Task.Factory.StartNew(EnsureInitialized);
        }

        public string[] GetTextLines(byte[] image)
        {
            Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
            lock (_obj)
            {
                if (!_objectsInitialized)
                {
                    throw new InvalidOperationException($"EasyOCR is not initialized");
                }

                return _pythonEngine.Execute(() =>
                {
                    dynamic ocrResult = _reader.readtext(_bytes.Invoke(image.ToPython()), detail: 0, paragraph: true);
                    _readerIsUsed = true;

                    return (string[])ocrResult;
                });
            }
        }

        public OcrTextBlock[] GetTextBlocks(byte[] image)
        {
            Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
            lock (_obj)
            {
                if (!_objectsInitialized)
                {
                    throw new InvalidOperationException($"EasyOCR is not initialized");
                }

                var lines = _pythonEngine.Execute(() =>
                {
                    // detail=1 returns [[box, text, confidence], ...] where box is 4 corner points [x, y].
                    // paragraph=false keeps line granularity; grouping into blocks is done by TextBlockGrouper
                    // so that both OCR engines produce consistent blocks.
                    dynamic ocrResult = _reader.readtext(_bytes.Invoke(image.ToPython()), detail: 1, paragraph: false);
                    _readerIsUsed = true;

                    PyObject items = ocrResult;
                    var result = new List<OcrTextLine>();
                    var count = items.Length();
                    for (var i = 0; i < count; i++)
                    {
                        using PyObject item = items[i];
                        using PyObject box = item[0];
                        var text = item[1].As<string>();

                        float left = float.MaxValue, top = float.MaxValue, right = float.MinValue, bottom = float.MinValue;
                        var pointsCount = box.Length();
                        for (var p = 0; p < pointsCount; p++)
                        {
                            using PyObject point = box[p];
                            var x = (float)new PyFloat(point[0]).As<double>();
                            var y = (float)new PyFloat(point[1]).As<double>();
                            left = Math.Min(left, x);
                            top = Math.Min(top, y);
                            right = Math.Max(right, x);
                            bottom = Math.Max(bottom, y);
                        }

                        if (right > left && bottom > top)
                        {
                            result.Add(new OcrTextLine(text, new RectangleF(left, top, right - left, bottom - top)));
                        }
                    }

                    return result;
                });

                return TextBlockGrouper.Group(lines, _languageDescriptor.UseSpaceRemover ? string.Empty : " ");
            }
        }

        private void EnsureInitialized()
        {
            lock (_obj)
            {
                try
                {
                    _pythonEngine.Init();

                    if (!_objectsInitialized)
                    {
                        InitializeObjects();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"EasyOCR initialization error");
                }
            }
        }

        private void InitializeObjects() =>
            _pythonEngine.Execute(() =>
            {
                _builtinsLib = _pythonEngine.Import("builtins");
                _easyOcrLib = _pythonEngine.Import("easyocr");
                _reader = _easyOcrLib.Reader(new[] { _languageDescriptor.EasyOcrCode }, model_storage_directory: _modelPath, download_enabled: false, recog_network: _languageDescriptor.EasyOcrModel);
                _bytes = _builtinsLib.bytes;

                _objectsInitialized = true;
            });

        public void Dispose()
        {
            lock (_obj)
            {
                if (_objectsInitialized)
                {
                    _pythonEngine.Execute(() =>
                    {
                        _builtinsLib.Dispose();
                        _easyOcrLib.Dispose();
                        if (_readerIsUsed)
                        {
                            _reader.Dispose();
                        }
                    });

                    _readerIsUsed = false;
                    _objectsInitialized = false;
                }

                _pythonEngine.Dispose();
            }
        }
    }
}
