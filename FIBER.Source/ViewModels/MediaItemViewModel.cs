using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;
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
    private System.Windows.Media.MediaPlayer? _player;

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

    /// <summary>Источник изображения в памяти (загружается по требованию, не блокирует GUI).</summary>
    public ImageSource? ImageSource
    {
        get => _imageSource;
        private set => SetProperty(ref _imageSource, value);
    }
    private ImageSource? _imageSource;

    /// <summary>Идёт ли сейчас загрузка изображения (для индикатора в UI).</summary>
    public bool IsImageLoading
    {
        get => _isImageLoading;
        private set => SetProperty(ref _isImageLoading, value);
    }
    private bool _isImageLoading;

    /// <summary>Сырые байты изображения (для отложенной декодировки).</summary>
    private byte[]? _imageData;

    /// <summary>Сервис медиа для ленивой загрузки BLOB из БД по требованию.</summary>
    private readonly IMediaService? _mediaService;

    /// <summary>Идентификатор вложения для ленивой загрузки из БД.</summary>
    private readonly int? _mediaIdForLazyLoad;

    /// <summary>Плеер для видео/аудио (null до инициализации).</summary>
    public System.Windows.Media.MediaPlayer? Player => _player;

    /// <summary>Идентификатор MediaAttachment.</summary>
    public int? MediaId { get; set; }

    /// <summary>Удалять FilePath при Dispose.</summary>
    public bool DeleteOnDispose { get; set; }

    /// <summary>SHA-256 hex содержимого файла — для защиты от дублей при перетаскивании.</summary>
    public string? ContentHash { get; set; }

    /// <summary>Признак неудачной загрузки.</summary>
    [ObservableProperty] private bool _loadFailed;

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
    /// <remarks>Изображение не декодируется автоматически — используйте <see cref="LoadImageAsync"/></remarks>
    public MediaItemViewModel(byte[] data, string originalName)
    {
        FilePath = string.Empty;
        FileName = originalName;

        var ext = Path.GetExtension(originalName)?.ToLowerInvariant();
        IsImage = ext is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp";

        if (!IsImage)
        {
            LoadFailed = true;
            return;
        }

        // Сохраняем байты для отложенной декодировки — не блокируем GUI при создании списка
        _imageData = data;
    }

    /// <summary>Конструктор для ленивой загрузки изображения из БД по MediaId.</summary>
    /// <remarks>BLOB-данные не читаются из базы до вызова <see cref="LoadImageAsync"/></remarks>
    public MediaItemViewModel(IMediaService mediaService, int mediaId, string originalName)
    {
        FilePath = string.Empty;
        FileName = originalName;

        var ext = Path.GetExtension(originalName)?.ToLowerInvariant();
        IsImage = ext is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp";
        IsVideo = ext is ".mp4" or ".avi" or ".wmv";
        IsAudio = ext is ".mp3" or ".wav" or ".wma";

        // Изображения — ленивая загрузка BLOB. Видео/аудио — файл уже в Temp (передан через другой конструктор).
        // Не помечаем как неудачные — загрузка произойдёт по требованию.
        _mediaService = mediaService;
        _mediaIdForLazyLoad = mediaId;
        MediaId = mediaId;
    }

    /// <summary>Ограничение параллельной декодировки изображений, чтобы не перегрузить GUI поток.</summary>
    private static readonly SemaphoreSlim _imageLoadSemaphore = new(4);

    /// <summary>Асинхронно создаёт BitmapImage из сохранённых байтов и устанавливает ImageSource.</summary>
    /// <remarks>Вызвать по требованию (когда элемент попадает в зону видимости).</remarks>
    public async Task LoadImageAsync()
    {
        if (ImageSource is not null) return;

        // Если байтов ещё нет, но есть сервис и ID — читаем BLOB из БД по требованию
        if (_imageData is null && _mediaService is not null && _mediaIdForLazyLoad is int mediaId)
        {
            _imageData = await _mediaService.GetMediaDataAsync(mediaId);
            if (_imageData is null)
            {
                LoadFailed = true;
                return;
            }
        }

        if (_imageData is null) return;

        await _imageLoadSemaphore.WaitAsync();
        try
        {
            IsImageLoading = true;

            BitmapImage? bitmap = null;
            await Task.Run(() =>
            {
                using var ms = new MemoryStream(_imageData);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                bitmap = bmp;
            });

            if (System.Windows.Application.Current?.Dispatcher is { } uiDispatcher)
            {
                await uiDispatcher.InvokeAsync(() =>
                {
                    ImageSource = bitmap;
                    _imageData = null; // освобождаем байты после декодирования
                });
            }
            else
            {
                ImageSource = bitmap;
                _imageData = null;
            }
        }
        catch
        {
            LoadFailed = true;
        }
        finally
        {
            IsImageLoading = false;
            _imageLoadSemaphore.Release();
        }
    }

    /// <summary>Создаёт MediaPlayer с отложенной разблокировкой звука.</summary>
    private void InitializePlayer()
    {
        if (_player != null) return;
        if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath)) { LoadFailed = true; return; }

        try
        {
            _player = new System.Windows.Media.MediaPlayer
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

    /// <summary>Освобождает ресурсы; удаляет временный файл при необходимости.
    /// Кэшированные VM (DeleteOnDispose = false) не утилизируются полностью,
    /// так как они belong к общему кэшу MediaService и используются повторно.</summary>
    public void Dispose()
    {
        if (!DeleteOnDispose)
        {
            // Кэшированный VM — только останавливаем таймер обновления позиции,
            // MediaPlayer и temp-файл остаются для повторного использования.
            _timer?.Stop();
            return;
        }

        _timer?.Stop();
        _player?.Close();

        if (!string.IsNullOrEmpty(FilePath))
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch { }
        }
    }
}