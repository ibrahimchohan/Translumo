using System;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;
using Translumo.Configuration;
using Translumo.Infrastructure.Language;
using Translumo.MVVM.Models;
using Translumo.Processing.Configuration;
using Translumo.Services.Windows;
using Translumo.Translation.Configuration;
using Translumo.Utils;
using RelayCommand = Microsoft.Toolkit.Mvvm.Input.RelayCommand;

namespace Translumo.MVVM.ViewModels
{
    /// <summary>
    /// Floating control bar: start/stop, capture target (area or window), overlay switch and quick access to settings.
    /// </summary>
    public sealed class ControlBarViewModel : BindableBase
    {
        public TextProcessingConfiguration TextProcessing { get; }
        public ChatWindowViewModel Chat { get; }

        public bool IsRunning
        {
            get => _isRunning;
            private set => SetProperty(ref _isRunning, value);
        }

        public bool IsCollapsed
        {
            get => _isCollapsed;
            set => SetProperty(ref _isCollapsed, value);
        }

        public string LanguagesCaption
        {
            get => _languagesCaption;
            private set => SetProperty(ref _languagesCaption, value);
        }

        public string TargetCaption
        {
            get => _targetCaption;
            private set => SetProperty(ref _targetCaption, value);
        }

        /// <summary>Current text size: overlay scale when the overlay is on, otherwise the text window font size.</summary>
        public string FontSizeCaption => TextProcessing.OverlayMode
            ? $"{_styleConfiguration.OverlayFontScalePercent}%"
            : _styleConfiguration.FontSize.ToString();

        public bool IsWindowMode => _captureConfiguration.Mode == CaptureTargetMode.Window;

        public ICommand ToggleTranslationCommand => new RelayCommand(() =>
        {
            Chat.ToggleTranslation();
            RefreshRunningState();
        });
        public ICommand SelectAreaCommand => new RelayCommand(Chat.SelectArea);
        public ICommand SelectWindowCommand => new RelayCommand(Chat.SelectWindow);
        public ICommand ToggleChatCommand => new RelayCommand(Chat.ToggleChatVisibility);
        public ICommand OpenSettingsCommand => new RelayCommand(Chat.ToggleSettings);
        public ICommand ToggleCollapseCommand => new RelayCommand(() => IsCollapsed = !IsCollapsed);
        public ICommand IncreaseFontCommand => new RelayCommand(() => ChangeFontSize(+1));
        public ICommand DecreaseFontCommand => new RelayCommand(() => ChangeFontSize(-1));

        private const int OVERLAY_FONT_SCALE_STEP = 10;
        private const int MIN_CHAT_FONT_SIZE = 1;
        private const int MAX_CHAT_FONT_SIZE = 80;

        private readonly TranslationConfiguration _translationConfiguration;
        private readonly ScreenCaptureConfiguration _captureConfiguration;
        private readonly WindowTracker _windowTracker;
        private readonly ChatWindowConfiguration _styleConfiguration;
        private readonly DispatcherTimer _stateTimer;
        private bool _isRunning;
        private bool _isCollapsed;
        private string _languagesCaption;
        private string _targetCaption;

        public ControlBarViewModel(ChatWindowViewModel chat, TextProcessingConfiguration textProcessing,
            TranslationConfiguration translationConfiguration, ScreenCaptureConfiguration captureConfiguration, WindowTracker windowTracker)
        {
            Chat = chat;
            TextProcessing = textProcessing;
            _translationConfiguration = translationConfiguration;
            _captureConfiguration = captureConfiguration;
            _windowTracker = windowTracker;
            _styleConfiguration = chat.Model.Configuration;

            _styleConfiguration.PropertyChanged += (_, _) => OnPropertyChanged(nameof(FontSizeCaption));
            TextProcessing.PropertyChanged += (_, _) => OnPropertyChanged(nameof(FontSizeCaption));
            _translationConfiguration.PropertyChanged += (_, _) => UpdateLanguagesCaption();
            _captureConfiguration.PropertyChanged += CaptureConfigurationOnPropertyChanged;
            _windowTracker.PropertyChanged += (_, _) => UpdateTargetCaption();

            // Translation can also be started/stopped by hotkeys or stop itself on errors
            _stateTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(300) };
            _stateTimer.Tick += (_, _) => RefreshRunningState();
            _stateTimer.Start();

            UpdateLanguagesCaption();
            UpdateTargetCaption();
        }

        private void CaptureConfigurationOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ScreenCaptureConfiguration.Mode)
                || e.PropertyName == nameof(ScreenCaptureConfiguration.TargetProcessName))
            {
                OnPropertyChanged(nameof(IsWindowMode));
                UpdateTargetCaption();
            }
        }

        private void ChangeFontSize(int direction)
        {
            if (TextProcessing.OverlayMode)
            {
                _styleConfiguration.OverlayFontScalePercent += direction * OVERLAY_FONT_SCALE_STEP;
            }
            else
            {
                _styleConfiguration.FontSize = Math.Clamp(_styleConfiguration.FontSize + direction, MIN_CHAT_FONT_SIZE, MAX_CHAT_FONT_SIZE);
            }
        }

        private void RefreshRunningState()
        {
            IsRunning = Chat.Model.TranslationIsRunning;
        }

        private void UpdateLanguagesCaption()
        {
            LanguagesCaption = $"{GetLanguageName(_translationConfiguration.TranslateFromLang)} → {GetLanguageName(_translationConfiguration.TranslateToLang)}";
        }

        private void UpdateTargetCaption()
        {
            TargetCaption = _captureConfiguration.Mode == CaptureTargetMode.Window && _captureConfiguration.HasTargetWindow
                ? _captureConfiguration.TargetProcessName
                : LocalizationManager.GetValue("Str.Region.AreaMode");
        }

        private static string GetLanguageName(Languages language)
        {
            return LocalizationManager.GetValue($"Str.Languages.{language}") ?? language.ToString();
        }
    }
}
