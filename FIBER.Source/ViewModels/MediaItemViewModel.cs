using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Interfaces;
using FIBER.Source.Infrastructure.Helpers;
using FIBER.Source.MediaPlayer;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FIBER.Source.ViewModels;
/// VM медиа-элемента. Декодирование изображений и извлечение видео/аудио
public partial class MediaItemViewModel : ObservableObject, IDisposable
{
    //Плеер WPF для видео/аудио
    private System.Windows.Media.MediaPlayer? _player;

    // Таймер обновления позиции
    private DispatcherTimer? _timer;

    // Таймер дебаунса для Seek при изменении слайдера
    private SeekDebouncer? _seek;
    private bool _updatingPosition;
    private bool _disposed;
    private Task? _imageLoadTask;
    private Task? _playbackLoadTask;
    private bool _playerReady;
    private bool _pendingPlay;
    public string FilePath
    {
        get => _filePath;
        private set => SetProperty(ref _filePath, value);
    }
    private string _filePath = string.Empty;
    public string FileName { get; }
    public bool IsImage { get; }
    public bool IsVideo { get; }
    public bool IsAudio { get; }
    public ImageSource? ImageSource
    {
        get => _imageSource;
        private set => SetProperty(ref _imageSource, value);
    }
    private ImageSource? _imageSource;
    public bool IsImageLoading
    {
        get => _isImageLoading;
        private set => SetProperty(ref _isImageLoading, value);
    }
    private bool _isImageLoading;
    private byte[]? _imageData;
    private readonly IMediaService? _mediaService;
    private readonly int? _mediaIdForLazyLoad;
    public System.Windows.Media.MediaPlayer? Player => _player;
    public int? MediaId { get; set; }
    public bool DeleteOnDispose { get; set; }
    public string? ContentHash { get; set; }
    [ObservableProperty] private bool _loadFailed;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private double _position;
    [ObservableProperty] private string _currentTime = "0:00";
    [ObservableProperty] private string _totalTime = "0:00";
    public bool IsDragging { get; set; }
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
            _ = LoadImageAsync();
        }
    }

    // Конструктор для изображения из байтов
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
        _imageData = data;
    }

    // Конструктор для ленивой загрузки изображения из БД по MediaId
    public MediaItemViewModel(IMediaService mediaService, int mediaId, string originalName)
    {
        FilePath = string.Empty;
        FileName = originalName;

        var ext = Path.GetExtension(originalName)?.ToLowerInvariant();
        IsImage = ext is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp";
        IsVideo = ext is ".mp4" or ".avi" or ".wmv";
        IsAudio = ext is ".mp3" or ".wav" or ".wma";
        _mediaService = mediaService;
        _mediaIdForLazyLoad = mediaId;
        MediaId = mediaId;
    }
    private static readonly SemaphoreSlim _imageLoadSemaphore = new(4);
    public Task LoadImageAsync() => _imageLoadTask ??= LoadImageCoreAsync();
    public Task LoadPlaybackFileAsync() => _playbackLoadTask ??= LoadPlaybackFileCoreAsync();
    private async Task LoadPlaybackFileCoreAsync()
    {
        if (_disposed || !(IsVideo || IsAudio) || !string.IsNullOrEmpty(FilePath)) return;
        try
        {
            if (_mediaService is null || MediaId is not int id) return;
            var path = await _mediaService.GetMediaFilePathAsync(id);
            if (_disposed) await TemporaryFiles.DeleteAsync(path);
            else
            {
                DeleteOnDispose = true;
                FilePath = path;
            }
        }
        catch { if (!_disposed) LoadFailed = true; }
    }

    private async Task LoadImageCoreAsync()
    {
        if (_disposed || !IsImage || ImageSource is not null) return;
        IsImageLoading = true;
        await _imageLoadSemaphore.WaitAsync();
        try
        {
            if (_disposed) return;
            var data = _imageData;
            if (data is null && _mediaService is not null && _mediaIdForLazyLoad is int mediaId)
                data = await _mediaService.GetMediaDataAsync(mediaId);
            else if (data is null && !string.IsNullOrEmpty(FilePath))
                data = await File.ReadAllBytesAsync(FilePath);
            if (data is null) { LoadFailed = true; return; }

            BitmapImage? bitmap = null;
            await Task.Run(() =>
            {
                using var ms = new MemoryStream(data);
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
                    if (!_disposed) ImageSource = bitmap;
                    _imageData = null; // освобождаем байты после декодирования
                });
            }
            else
            {
                if (!_disposed) ImageSource = bitmap;
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

    // Создаёт MediaPlayer с отложенной разблокировкой звука
    private void InitializePlayer()
    {
        if (_disposed || _player != null) return;
        if (string.IsNullOrEmpty(FilePath)) { LoadFailed = true; return; }

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

            _player.MediaFailed += (_, _) =>
            {
                LoadFailed = true;
                _playerReady = false;
                _pendingPlay = false;
                IsPlaying = false;
                _timer?.Stop();
                _seek?.Cancel();
            };

            _player.MediaEnded += (_, _) =>
            {
                IsPlaying = false;
                _timer?.Stop();
                _seek?.Cancel();
                SetPlaybackPosition(0);
                CurrentTime = "0:00";
            };

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _timer.Tick += (_, _) =>
            {
                if (_player != null && _player.NaturalDuration.HasTimeSpan && !IsDragging && _seek?.IsPending != true)
                {
                    var total = _player.NaturalDuration.TimeSpan.TotalSeconds;
                    if (total > 0)
                    {
                        var current = _player.Position.TotalSeconds;
                        SetPlaybackPosition(current / total * 100);
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

    // Обновляет позицию плеера при изменении слайдера
    partial void OnPositionChanged(double value)
    {
        if (_disposed || _updatingPosition || _player == null || !_playerReady || !_player.NaturalDuration.HasTimeSpan)
            return;

        _seek ??= new SeekDebouncer(position =>
        {
            if (_player != null && _playerReady && _player.NaturalDuration.HasTimeSpan)
            {
                var total = _player.NaturalDuration.TimeSpan.TotalSeconds;
                if (total > 0)
                {
                    _player.Position = TimeSpan.FromSeconds(total * Math.Clamp(position, 0, 100) / 100.0);
                    CurrentTime = FormatTime(_player.Position);
                }
            }
        });
        _seek.Schedule(value);
    }

    private void SetPlaybackPosition(double value)
    {
        _updatingPosition = true;
        try { Position = value; }
        finally { _updatingPosition = false; }
    }

    // Запуск или отложенный запуск воспроизведения
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

    [RelayCommand]
    private void Pause()
    {
        _pendingPlay = false;
        if (_player == null) return;
        _player.Pause();
        IsPlaying = false;
        _timer?.Stop();
    }

    [RelayCommand]
    private void Stop()
    {
        _pendingPlay = false;
        _seek?.Cancel();
        if (_player == null) return;
        _player.Stop();
        IsPlaying = false;
        _timer?.Stop();
        SetPlaybackPosition(0);
        CurrentTime = "0:00";
    }

    private static string FormatTime(TimeSpan t) =>
        t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");

    // свобождает плеер и таймеры независимо от владения файлом
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pendingPlay = false;
        IsPlaying = false;
        _timer?.Stop();
        _player?.Close();
        _player = null;
        _seek?.Dispose();
        _seek = null;
        _imageData = null;
        ImageSource = null;
        if (DeleteOnDispose && !string.IsNullOrEmpty(FilePath))
            _ = TemporaryFiles.DeleteAsync(FilePath);
    }
}
