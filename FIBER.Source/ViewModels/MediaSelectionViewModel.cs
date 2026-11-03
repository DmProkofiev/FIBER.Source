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
    public string TypeLabel => "Фото";
    public ImageSource? ImageSource => _media.ImageSource;
    public bool IsImage => _media.IsImage;
    public bool IsImageLoading => _media.IsImageLoading;
    public bool LoadFailed => _media.LoadFailed;

    [ObservableProperty]
    private bool _isSelected;

    public MediaSelectionItem(MediaItemViewModel media)
    {
        _media = media;
        IsSelected = false;
    }

    /// <summary>Загружает изображение в фоновом потоке (декодирование не блокирует GUI).</summary>
    public Task LoadImageAsync() => _media.LoadImageAsync();

    /// <summary>Освобождает ресурсы исходного медиа-элемента.</summary>
    public void Dispose() => _media.Dispose();
}

/// <summary>VM диалога выбора медиафайлов: список изображений, мультивыбор, подтверждение.</summary>
public partial class MediaSelectionViewModel : ObservableObject, IDisposable
{
    private readonly Action<bool> _close;
    private bool _disposed;

    public string Title { get; }

    public ObservableCollection<MediaSelectionItem> PhotoItems { get; } = new();

    public bool HasMedia => PhotoItems.Count > 0;
    public bool HasPhotos => PhotoItems.Count > 0;

    public MediaSelectionViewModel(string title, IEnumerable<MediaItemViewModel> media, Action<bool> close)
    {
        Title = title;

        foreach (var m in media)
        {
            if (m is null) continue;
            if (!m.IsImage) continue;
            var item = new MediaSelectionItem(m);
            PhotoItems.Add(item);
        }

        _close = close;

        // Декодирование изображений в фоне параллельно — не блокирует GUI.
        _ = LoadImagesInBackgroundAsync();
    }

    /// <summary>Декодирует все изображения в фоне параллельно.</summary>
    private async Task LoadImagesInBackgroundAsync()
    {
        var imageItems = PhotoItems.Where(i => i.IsImage).ToList();
        if (imageItems.Count == 0) return;

        await Task.WhenAll(imageItems.Select(item => item.LoadImageAsync()));
    }

    /// <summary>Id выбранных медиа (пусто — ничего не выбрано).</summary>
    public IReadOnlyList<int> GetSelectedMediaIds() =>
        PhotoItems.Where(m => m.IsSelected && m.MediaId > 0).Select(m => m.MediaId).ToList();

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var m in PhotoItems) m.IsSelected = true;
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var m in PhotoItems) m.IsSelected = false;
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

    /// <summary>Освобождает все медиа-ресурсы при закрытии окна.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var item in PhotoItems)
        {
            item.Dispose();
        }
        PhotoItems.Clear();
    }
}
