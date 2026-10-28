using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FIBER.Source.ViewModels;

/// <summary>
/// VM медиа-элемента. Изображения загружаются сразу в память, для видео MediaPlayer
/// создаётся неторопливо (для первого кадра), для аудио — откладывается до Play.
/// </summary>
public partial class MediaItemViewModel : ObservableObject, IDisposable
{
    /// <summary>Плеер WPF для видео/аудио.</summary>
    private MediaPlayer? _player;

    /// <summary>Таймер обновления позиции.</summary>
    private DispatcherTimer? _timer;

    /// <summary>Признак готовности плеера (MediaOpened).</summary>
    private bool _playerReady;

    /// <summary>Признак отложенного воспроизведения после открытия.</summary>
    private bool _pendingPlay;

    /// <summary>Путь к файлу на диске (пусто для изображений из памяти).</summary>
    public string FilePath { get; }

    /// <summary>Отображаемое имя файла.</summary>
    public string FileName { get; }

    /// <summary>Признак изображения.</summary>
    public bool IsImage { get; }

    /// <summary>Признак видео.</summary>
    public bool IsVideo { get; }

    /// <summary>Признак аудио.</summary>
    public bool IsAudio { get; }

    /// <summary>Источник изображения в памяти.</summary>
    public ImageSource? ImageSource { get; }

    /// <summary>Плеер для видео/аудио (null до инициализации).</summary>
    public MediaPlayer? Player => _player;

    /// <summary>Идентификатор MediaAttachment.</summary>
    public int? MediaId { get; set; }

    /// <summary>Удалять FilePath при Dispose.</summary>
    public bool DeleteOnDispose { get; set; }

    /// <summary>Признак неудачной загрузки.</summary>
    public bool LoadFailed { get; internal set; }

    /// <summary>Признак воспроизведения.</summary>
    [ObservableProperty] private bool _isPlaying;

    /// <summary>Позиция в процентах.</summary>
    [ObservableProperty] private double _position;

    /// <summary>Текущее время.</summary>
    [ObservableProperty] private string _currentTime = "0:00";

    /// <summary>Общая длительность.</summary>
    [ObservableProperty] private string _totalTime = "0:00";

    /// <summary>Флаг перетаскивания слайдера.</summary>
    public bool IsDragging { get; set; }

    /// <summary>Конструктор для файла на диске.</summary>
    public MediaItemViewModel(string filePath, string originalName)
    {
        FilePath = filePath;
        FileName = originalName;

        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        IsImage = ext is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp";
        IsVideo = ext is ".mp4" or ".avi" or ".wmv";
        IsAudio = ext is ".mp3" or ".wav" or ".wma";

        if (IsImage)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                ImageSource = bitmap;
            }
            catch { LoadFailed = true; }
        }
        else if (IsVideo)
        {
            // Эагерная инициализация — нужна для отрисовки первого кадра.
            // IsMuted=true предотвращает утечку звука до нажатия Play.
            InitializePlayer();
        }
        // Audio: полностью отложено до Play — это устраняет баг с миллисекундным звуком.
    }

    /// <summary>Конструктор для изображения из байтов.</summary>
    public MediaItemViewModel(byte[] data, string originalName)
    {
        FilePath = string.Empty;
        FileName = originalName;

        var ext = Path.GetExtension(originalName)?.ToLowerInvariant();
        IsImage = ext is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp";

        if (!IsImage) { LoadFailed = true; return; }

        try
        {
            using var ms = new MemoryStream(data);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();
            ImageSource = bitmap;
        }
        catch { LoadFailed = true; }
    }

    /// <summary>Создаёт MediaPlayer с отложенной разблокировкой звука.</summary>
    private void InitializePlayer()
    {
        if (_player != null) return;
        if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath)) { LoadFailed = true; return; }

        try
        {
            _player = new MediaPlayer
            {
                ScrubbingEnabled = IsVideo,
                Volume = 1.0,
                IsMuted = true // звук заблокирован до Play
            };

            _player.MediaOpened += (_, _) =>
            {
                _playerReady = true;
                if (_player.NaturalDuration.HasTimeSpan)
                    TotalTime = FormatTime(_player.NaturalDuration.TimeSpan);

                if (_pendingPlay)
                {
                    _pendingPlay = false;
                    _player.IsMuted = false;
                    _player.Play();
                    IsPlaying = true;
                    _timer?.Start();
                }
            };

            _player.MediaFailed += (_, _) => { LoadFailed = true; _playerReady = false; };

            _player.MediaEnded += (_, _) =>
            {
                IsPlaying = false;
                _timer?.Stop();
                Position = 0;
                CurrentTime = "0:00";
            };

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _timer.Tick += (_, _) =>
            {
                if (_player != null && _player.NaturalDuration.HasTimeSpan && !IsDragging)
                {
                    var total = _player.NaturalDuration.TimeSpan.TotalSeconds;
                    if (total > 0)
                    {
                        var current = _player.Position.TotalSeconds;
                        Position = current / total * 100;
                        CurrentTime = FormatTime(_player.Position);
                    }
                }
            };

            _player.Open(new Uri(FilePath, UriKind.Absolute));
        }
        catch
        {
            LoadFailed = true;
            _player = null;
        }
    }

    /// <summary>Обновляет позицию плеера при перетаскивании слайдера.</summary>
    partial void OnPositionChanged(double value)
    {
        if (_player != null && _playerReady && _player.NaturalDuration.HasTimeSpan && IsDragging)
        {
            var total = _player.NaturalDuration.TimeSpan.TotalSeconds;
            _player.Position = TimeSpan.FromSeconds(total * value / 100.0);
            CurrentTime = FormatTime(_player.Position);
        }
    }

    /// <summary>Запуск или отложенный запуск воспроизведения.</summary>
    [RelayCommand]
    private void Play()
    {
        if (_player == null) InitializePlayer();
        if (_player == null) return;

        if (!_playerReady)
        {
            _pendingPlay = true;
            return;
        }

        _player.IsMuted = false;
        _player.Play();
        IsPlaying = true;
        _timer?.Start();
    }

    /// <summary>Пауза.</summary>
    [RelayCommand]
    private void Pause()
    {
        if (_player == null) return;
        _player.Pause();
        IsPlaying = false;
        _timer?.Stop();
    }

    /// <summary>Остановка.</summary>
    [RelayCommand]
    private void Stop()
    {
        if (_player == null) return;
        _player.Stop();
        IsPlaying = false;
        _timer?.Stop();
        Position = 0;
        CurrentTime = "0:00";
    }

    /// <summary>Форматирует длительность.</summary>
    private static string FormatTime(TimeSpan t) =>
        t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");

    /// <summary>Освобождает ресурсы; удаляет временный файл при необходимости.</summary>
    public void Dispose()
    {
        _timer?.Stop();
        _player?.Close();

        if (DeleteOnDispose && !string.IsNullOrEmpty(FilePath))
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch { }
        }
    }
}