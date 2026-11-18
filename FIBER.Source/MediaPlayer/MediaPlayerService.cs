using System;
using System.Windows.Controls;
using System.Windows.Media;

namespace FIBER.Source.MediaPlayer
{
    /// <summary>
    /// Реализация <see cref="IMediaPlayerService"/>, обёртка над WPF MediaElement.
    /// Инкапсулирует всю логику воспроизведения, чтобы View не зависела от MediaElement напрямую.
    /// </summary>
    public sealed class MediaPlayerService : IMediaPlayerService
    {
        private readonly MediaElement _mediaElement;
        private static MediaPlayerService? _currentPlaying;
        private bool _isPlaying;

        public MediaPlayerService(MediaElement mediaElement)
        {
            _mediaElement = mediaElement ?? throw new ArgumentNullException(nameof(mediaElement));
            _mediaElement.MediaEnded += (_, _) => Stop();
            _mediaElement.MediaFailed += (_, _) => Stop();
        }

        public string FilePath
        {
            get => _mediaElement.Source?.LocalPath ?? string.Empty;
            set
            {
                Stop();
                _mediaElement.Source = string.IsNullOrWhiteSpace(value) ? null : new Uri(value, UriKind.Absolute);
            }
        }

        public double Position
        {
            get => _mediaElement.Position.TotalSeconds;
            set
            {
                if (double.IsFinite(value))
                    _mediaElement.Position = TimeSpan.FromSeconds(Math.Clamp(value, 0, Duration));
            }
        }

        public double Duration
        {
            get => _mediaElement.NaturalDuration.HasTimeSpan
                ? _mediaElement.NaturalDuration.TimeSpan.TotalSeconds
                : 0.0;
        }

        public bool IsPlaying => _isPlaying;

        public void Play()
        {
            if (_mediaElement.Source == null) return;

            // Только одно видео может проигрываться одновременно
            if (_currentPlaying != null && _currentPlaying != this)
            {
                _currentPlaying.Stop();
            }
            _currentPlaying = this;

            _mediaElement.Play();
            _isPlaying = true;
        }

        public void Pause()
        {
            _mediaElement.Pause();
            _isPlaying = false;
        }

        public void Stop()
        {
            _mediaElement.Stop();
            _isPlaying = false;
            if (_currentPlaying == this)
                _currentPlaying = null;
        }
    }
}
