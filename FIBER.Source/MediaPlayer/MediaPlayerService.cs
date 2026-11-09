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

        public MediaPlayerService(MediaElement mediaElement)
        {
            _mediaElement = mediaElement ?? throw new ArgumentNullException(nameof(mediaElement));
        }

        public string FilePath
        {
            get => _mediaElement.Source?.AbsolutePath ?? string.Empty;
            set => _mediaElement.Source = string.IsNullOrWhiteSpace(value)
                ? null
                : new Uri(value);
        }

        public double Position
        {
            get => _mediaElement.Position.TotalSeconds;
            set => _mediaElement.Position = TimeSpan.FromSeconds(value);
        }

        public double Duration
        {
            get => _mediaElement.NaturalDuration.HasTimeSpan
                ? _mediaElement.NaturalDuration.TimeSpan.TotalSeconds
                : 0.0;
        }

        public bool IsPlaying =>
            _mediaElement.NaturalDuration.HasTimeSpan &&
            _mediaElement.Position.TotalSeconds > 0 &&
            _mediaElement.Position.TotalSeconds < _mediaElement.NaturalDuration.TimeSpan.TotalSeconds;

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
        }

        public void Pause() => _mediaElement.Pause();

        public void Stop()
        {
            _mediaElement.Stop();
            if (_currentPlaying == this)
                _currentPlaying = null;
        }
    }
}
