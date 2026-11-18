using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Slider = System.Windows.Controls.Slider;

namespace FIBER.Source.MediaPlayer;

/// <summary>Общая синхронизация слайдера и жизненного цикла аудио/видео-контролов.</summary>
internal sealed class MediaControlPlayback
{
    private readonly Slider _slider;
    private readonly IMediaPlayerService _localService;
    private readonly DispatcherTimer _positionTimer;
    private readonly SeekDebouncer _seek;
    private IMediaPlayerService _service;
    private bool _updating;

    public MediaControlPlayback(MediaElement element, Slider slider, IMediaPlayerService? service = null)
    {
        _slider = slider;
        _service = _localService = service ?? new MediaPlayerService(element);
        _seek = new SeekDebouncer(value => _service.Position = Math.Clamp(value, 0, _service.Duration));
        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _positionTimer.Tick += (_, _) => UpdateSlider();
        slider.ValueChanged += (_, args) =>
        {
            if (!_updating && _service.Duration > 0) _seek.Schedule(args.NewValue);
        };
        element.MediaOpened += (_, _) => UpdateSlider();
        element.MediaEnded += (_, _) => Stop();
        element.MediaFailed += (_, _) => Stop();
    }

    public void SetService(IMediaPlayerService? service, string? path)
    {
        Stop();
        _service = service ?? _localService;
        SetFile(path);
    }

    public void SetFile(string? path)
    {
        Stop();
        _service.FilePath = path ?? string.Empty;
    }

    public void Play()
    {
        _service.Play();
        _positionTimer.Start();
    }

    public void Pause()
    {
        _service.Pause();
        _positionTimer.Stop();
    }

    public void Stop()
    {
        _seek.Cancel();
        _positionTimer.Stop();
        _service.Stop();
        _updating = true;
        try { _slider.Value = 0; }
        finally { _updating = false; }
    }

    public void Unload()
    {
        Stop();
        _service.FilePath = string.Empty;
    }

    private void UpdateSlider()
    {
        if (_seek.IsPending || _slider.IsMouseCaptureWithin) return;
        _updating = true;
        try
        {
            _slider.Maximum = Math.Max(0, _service.Duration);
            _slider.Value = _service.Position;
        }
        finally { _updating = false; }
    }
}
