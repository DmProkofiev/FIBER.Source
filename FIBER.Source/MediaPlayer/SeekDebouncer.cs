using System.Windows.Threading;

namespace FIBER.Source.MediaPlayer;

/// <summary>Одна отложенная перемотка; сохраняет именно последнюю позицию пользователя.</summary>
public sealed class SeekDebouncer : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Action<double> _seek;
    private double _position;

    public SeekDebouncer(Action<double> seek)
    {
        _seek = seek;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _timer.Tick += OnTick;
    }

    public bool IsPending => _timer.IsEnabled;

    public void Schedule(double position)
    {
        if (!double.IsFinite(position)) return;
        _position = position;
        _timer.Stop();
        _timer.Start();
    }

    public void Cancel() => _timer.Stop();

    private void OnTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        _seek(_position);
    }

    public void Dispose()
    {
        Cancel();
        _timer.Tick -= OnTick;
    }
}
