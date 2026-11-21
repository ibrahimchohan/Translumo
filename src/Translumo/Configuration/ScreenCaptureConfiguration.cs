using System;
using System.Drawing;
using System.Xml.Serialization;
using Translumo.Utils;

namespace Translumo.Configuration
{
    public enum CaptureTargetMode : byte
    {
        /// <summary>Fixed rectangle on the screen.</summary>
        Area = 0,
        /// <summary>Follows the selected application window.</summary>
        Window = 1
    }

    public class ScreenCaptureConfiguration : BindableBase
    {
        private readonly object _areaLock = new object();
        private RectangleF _captureArea;
        private CaptureTargetMode _mode;
        private string _targetProcessName;
        private string _targetWindowTitle;
        private string _targetWindowClass;
        private RectangleF _targetRelativeArea;
        private volatile bool _captureSuspended;

        /// <summary>
        /// Area to capture in physical screen pixels. In <see cref="CaptureTargetMode.Window"/> mode it is kept up to date
        /// by the window tracker. Read from the capture thread, written from the UI thread.
        /// </summary>
        public RectangleF CaptureArea
        {
            get
            {
                lock (_areaLock)
                {
                    return _captureArea;
                }
            }
            set
            {
                lock (_areaLock)
                {
                    if (_captureArea == value)
                    {
                        return;
                    }

                    _captureArea = value;
                }

                OnPropertyChanged();
            }
        }

        public CaptureTargetMode Mode
        {
            get => _mode;
            set => SetProperty(ref _mode, value);
        }

        /// <summary>Process name of the target window (e.g. "msedge"), used to find the window again after restart.</summary>
        public string TargetProcessName
        {
            get => _targetProcessName;
            set => SetProperty(ref _targetProcessName, value);
        }

        public string TargetWindowTitle
        {
            get => _targetWindowTitle;
            set => SetProperty(ref _targetWindowTitle, value);
        }

        public string TargetWindowClass
        {
            get => _targetWindowClass;
            set => SetProperty(ref _targetWindowClass, value);
        }

        /// <summary>
        /// Capture area relative to the top-left corner of the target window (physical pixels).
        /// Empty means the whole content area of the window (page area for browsers).
        /// </summary>
        public RectangleF TargetRelativeArea
        {
            get => _targetRelativeArea;
            set => SetProperty(ref _targetRelativeArea, value);
        }

        /// <summary>Handle of the currently tracked window (runtime only).</summary>
        [XmlIgnore]
        public IntPtr TargetHandle { get; set; }

        /// <summary>
        /// True while the target window is closed or minimized: capturing would read whatever is behind it.
        /// </summary>
        [XmlIgnore]
        public bool CaptureSuspended
        {
            get => _captureSuspended;
            set => _captureSuspended = value;
        }

        [XmlIgnore]
        public bool HasTargetWindow => !string.IsNullOrEmpty(TargetProcessName);
    }
}
