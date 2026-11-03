using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace FIBER.Source.ViewModels;

/// <summary>Один медиаэлемент в списке выбора медиафайлов.</summary>
public partial class MediaSelectionItem : ObservableObject
{
    private readonly MediaItemViewModel _media;

    public int MediaId => _media.MediaId ?? 0;
    public string DisplayName => _media.FileName;
    public string TypeLabel => _media.IsImage ? "Фото" : _media.IsVideo ? "Видео" : "Аудио";
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
}

/// <summary>VM диалога выбора медиафайлов: список всех медиа, мультивыбор, подтверждение.</summary>
public partial class MediaSelectionViewModel : ObservableObject
{
    private readonly Action<bool> _close;

    public string Title { get; }

    public ObservableCollection<MediaSelectionItem> MediaItems { get; }

    public bool HasMedia => MediaItems.Count > 0;

    public MediaSelectionViewModel(string title, IEnumerable<MediaItemViewModel> media, Action<bool> close)
    {
        Title = title;
        MediaItems = new ObservableCollection<MediaSelectionItem>();

        foreach (var m in media)
        {
            if (m is null) continue;
            MediaItems.Add(new MediaSelectionItem(m));
            if (m.IsImage) _ = m.LoadImageAsync();
        }

        _close = close;
    }

    /// <summary>Id выбранных медиа (пусто — ничего не выбрано).</summary>
    public IReadOnlyList<int> GetSelectedMediaIds() =>
        MediaItems.Where(m => m.IsSelected && m.MediaId > 0).Select(m => m.MediaId).ToList();

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var m in MediaItems) m.IsSelected = true;
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var m in MediaItems) m.IsSelected = false;
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
}
