using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using UserControl = System.Windows.Controls.UserControl;
using Button = System.Windows.Controls.Button;
using Slider = System.Windows.Controls.Slider;
using TextBlock = System.Windows.Controls.TextBlock;
using Grid = System.Windows.Controls.Grid;
using Border = System.Windows.Controls.Border;
using StackPanel = System.Windows.Controls.StackPanel;
using RoutedEventArgs = System.Windows.RoutedEventArgs;
using RoutedPropertyChangedEventArgs = System.Windows.RoutedPropertyChangedEventArgs<double>;
using FIBER.Source.MediaPlayer;

namespace FIBER.Source.Views.Search
{
    public partial class VideoPlayerControl : UserControl
    {
        private IMediaPlayerService _mediaPlayerService;
        private readonly DispatcherTimer _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        private bool _isUpdatingSlider;

        public static readonly DependencyProperty FilePathProperty =
            DependencyProperty.Register(nameof(FilePath), typeof(string), typeof(VideoPlayerControl),
                new PropertyMetadata(null, OnFilePathChanged));

        public static readonly DependencyProperty MediaServiceProperty =
            DependencyProperty.Register(nameof(MediaService), typeof(IMediaPlayerService), typeof(VideoPlayerControl),
                new PropertyMetadata(null, OnMediaServiceChanged));

        public string FilePath
        {
            get => (string)GetValue(FilePathProperty);
            set => SetValue(FilePathProperty, value);
        }

        /// <summary>
        /// Внешний медиасервис (DI). Если не задан — создаётся локальный экземпляр.
        /// </summary>
        public IMediaPlayerService MediaService
        {
            get => (IMediaPlayerService)GetValue(MediaServiceProperty);
            set => SetValue(MediaServiceProperty, value);
        }

        public VideoPlayerControl()
        {
            InitializeComponent();
            VideoMediaElement.MediaOpened += VideoMedia_MediaOpened;
            _mediaPlayerService = MediaService ?? new MediaPlayerService(VideoMediaElement);
            InitializePositionTimer();
        }

        private static void OnMediaServiceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is VideoPlayerControl control)
            {
                control._mediaPlayerService = control.MediaService ?? new MediaPlayerService(control.VideoMediaElement);
            }
        }

        private static void OnFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is VideoPlayerControl control && !string.IsNullOrEmpty(control.FilePath) && control._mediaPlayerService != null)
            {
                control._mediaPlayerService.FilePath = control.FilePath;
            }
        }

        private void InitializePositionTimer()
        {
            _positionTimer.Tick += PositionTimer_Tick;
        }

        private void StartPositionTimer()
        {
            if (!_positionTimer.IsEnabled)
                _positionTimer.Start();
        }

        private void StopPositionTimer()
        {
            if (_positionTimer.IsEnabled)
                _positionTimer.Stop();
        }

        private void PositionTimer_Tick(object? sender, EventArgs e)
        {
            if (_isUpdatingSlider || _mediaPlayerService == null)
                return;

            if (_mediaPlayerService.Duration > 0)
            {
                _isUpdatingSlider = true;
                try
                {
                    VideoSlider.Value = _mediaPlayerService.Position;
                }
                finally
                {
                    _isUpdatingSlider = false;
                }
            }
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            _mediaPlayerService?.Play();
            StartPositionTimer();
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            _mediaPlayerService?.Pause();
            StopPositionTimer();
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            _mediaPlayerService?.Stop();
            StopPositionTimer();
            // Сброс позиции воспроизведения и ползунка в начало
            if (_mediaPlayerService != null)
            {
                _mediaPlayerService.Position = 0;
                VideoSlider.Value = 0;
            }
        }

        private void VideoSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingSlider || _mediaPlayerService == null)
                return;

            _mediaPlayerService.Position = VideoSlider.Value;
        }

        private void VideoMedia_MediaOpened(object sender, RoutedEventArgs e)
        {
            VideoSlider.Maximum = _mediaPlayerService.Duration;
        }
    }
}
