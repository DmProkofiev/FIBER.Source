using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace FIBER.Source.ViewModels;

/// <summary>Один медиаэлемент в списке выбора медиафайлов.</summary>
public partial class MediaSelectionItem : ObservableObject
{
    private readonly MediaItemViewModel _media;

    public int MediaId => _media.MediaId ?? 0;
    public string DisplayName => _media.FileName;
    public string FilePath => _media.FilePath;
    public string TypeLabel => _media.IsImage ? "Фото" : (_media.IsVideo ? "Видео" : "Аудио");
    public bool IsImage => _media.IsImage;
    public bool IsVideo => _media.IsVideo;
    public bool IsAudio => _media.IsAudio;
    public bool IsImageLoading => _media.IsImageLoading;

    /// <summary>Признак неудачной загрузки (пробрасывает PropertyChanged из исходного VM).</summary>
    public bool LoadFailed => _media.LoadFailed;

    /// <summary>Источник изображения (пробрасывает PropertyChanged из исходного VM).</summary>
    public ImageSource? ImageSource => _media.ImageSource;

    [ObservableProperty]
    private bool _isSelected;

    public MediaSelectionItem(MediaItemViewModel media)
    {
        _media = media;
        IsSelected = false;

        // Пробрасываем PropertyChanged от исходного MediaItemViewModel,
        // чтобы UI обновлялся при изменении ImageSource, IsImageLoading и т.п.
        _media.PropertyChanged += Media_PropertyChanged;
    }

    /// <summary>Загружает изображение в фоновом потоке (декодирование не блокирует GUI).</summary>
    public Task LoadImageAsync() => _media.LoadImageAsync();

    /// <summary>Освобождает ресурсы. Не утилизирует исходный MediaItemViewModel,
    /// так как он принадлежит кэшу MediaService и используется повторно.</summary>
    public void Dispose()
    {
        _media.PropertyChanged -= Media_PropertyChanged;
    }

    private void Media_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MediaItemViewModel.ImageSource)
            || e.PropertyName == nameof(MediaItemViewModel.IsImageLoading)
            || e.PropertyName == nameof(MediaItemViewModel.LoadFailed))
        {
            OnPropertyChanged(e.PropertyName);
        }
    }
}

/// <summary>VM диалога выбора медиафайлов: список изображений/видео/аудио, мультивыбор, подтверждение.</summary>
public partial class MediaSelectionViewModel : ObservableObject, IDisposable
{
    private readonly Action<bool> _close;
    private bool _disposed;

    public string Title { get; }

    public ObservableCollection<MediaSelectionItem> PhotoItems { get; } = new();
    public ObservableCollection<MediaSelectionItem> VideoItems { get; } = new();
    public ObservableCollection<MediaSelectionItem> AudioItems { get; } = new();

    public bool HasMedia => PhotoItems.Count > 0 || VideoItems.Count > 0 || AudioItems.Count > 0;
    public bool HasPhotos => PhotoItems.Count > 0;
    public bool HasVideos => VideoItems.Count > 0;
    public bool HasAudio => AudioItems.Count > 0;

    public MediaSelectionViewModel(string title, IEnumerable<MediaItemViewModel> media, Action<bool> close)
    {
        Title = title;

        foreach (var m in media)
        {
            if (m is null) continue;
            var item = new MediaSelectionItem(m);
            if (m.IsImage) PhotoItems.Add(item);
            else if (m.IsVideo) VideoItems.Add(item);
            else if (m.IsAudio) AudioItems.Add(item);
        }

        _close = close;

        // Декодирование изображений в фоне параллельно — не блокирует GUI.
        _ = LoadImagesInBackgroundAsync();
    }

    /// <summary>Ограничение параллельной декодировки изображений (не более 3 одновременных),
    /// чтобы не создавать конфликтов при чтении BLOB из SQLCipher БД.</summary>
    private static readonly SemaphoreSlim _imageLoadSemaphore = new(3, 3);

    /// <summary>Декодирует все изображения в фоне с ограничением параллелизма.</summary>
    private async Task LoadImagesInBackgroundAsync()
    {
        var imageItems = PhotoItems.Where(i => i.IsImage).ToList();
        if (imageItems.Count == 0) return;

        // Ограничиваем параллельность: не более 3 изображений загружаются одновременно.
        var tasks = imageItems.Select(async item =>
        {
            await _imageLoadSemaphore.WaitAsync();
            try
            {
                await item.LoadImageAsync();
            }
            finally
            {
                _imageLoadSemaphore.Release();
            }
        });
        await Task.WhenAll(tasks);
    }

    /// <summary>Id выбранных медиа (пусто — ничего не выбрано).</summary>
    public IReadOnlyList<int> GetSelectedMediaIds() =>
        PhotoItems.Concat(VideoItems).Concat(AudioItems)
            .Where(m => m.IsSelected && m.MediaId > 0)
            .Select(m => m.MediaId)
            .ToList();

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var m in PhotoItems) m.IsSelected = true;
        foreach (var m in VideoItems) m.IsSelected = true;
        foreach (var m in AudioItems) m.IsSelected = true;
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var m in PhotoItems) m.IsSelected = false;
        foreach (var m in VideoItems) m.IsSelected = false;
        foreach (var m in AudioItems) m.IsSelected = false;
    }

    [RelayCommand]
    private void Add()
    {
        if (GetSelectedMediaIds().Count == 0)
        {
            Notification.Show("Внимание", "Выберите медиафайл", NotificationType.Warning);
            return;
        }
        _close(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        _close(false);
    }

    /// <summary>Очищает коллекции при закрытии окна. Не утилизирует MediaItemViewModel,
    /// так как они принадлежат кэшу MediaService.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var item in PhotoItems.Concat(VideoItems).Concat(AudioItems))
        {
            item.Dispose();
        }
        PhotoItems.Clear();
        VideoItems.Clear();
        AudioItems.Clear();
    }
}
