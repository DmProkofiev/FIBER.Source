using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
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
    public bool IsVideo => _media.IsVideo;
    public bool IsAudio => _media.IsAudio;
    public bool IsImageLoading => _media.IsImageLoading;
    public bool LoadFailed => _media.LoadFailed;

    /// <summary>Плеер для видео/аудио (null для изображений).</summary>
    public MediaPlayer? Player => _media.Player;
    public bool IsPlaying => _media.IsPlaying;
    public string CurrentTime => _media.CurrentTime;
    public string TotalTime => _media.TotalTime;
    public double Position => _media.Position;

    /// <summary>Команды воспроизведения, делегирующиеся исходному медиа-элементу.</summary>
    public ICommand PlayCommand => _media.PlayCommand;
    public ICommand PauseCommand => _media.PauseCommand;
    public ICommand StopCommand => _media.StopCommand;

    [ObservableProperty]
    private bool _isSelected;

    public MediaSelectionItem(MediaItemViewModel media)
    {
        _media = media;
        IsSelected = false;
    }
}

/// <summary>VM диалога выбора медиафайлов: разделение по типам, мультивыбор, подтверждение.</summary>
public partial class MediaSelectionViewModel : ObservableObject
{
    private readonly Action<bool> _close;

    public string Title { get; }

    public ObservableCollection<MediaSelectionItem> PhotoItems { get; } = new();
    public ObservableCollection<MediaSelectionItem> VideoItems { get; } = new();
    public ObservableCollection<MediaSelectionItem> AudioItems { get; } = new();

    public bool HasMedia => PhotoItems.Count + VideoItems.Count + AudioItems.Count > 0;
    public bool HasPhotos => PhotoItems.Count > 0;
    public bool HasVideos => VideoItems.Count > 0;
    public bool HasAudios => AudioItems.Count > 0;

    public MediaSelectionViewModel(string title, IEnumerable<MediaItemViewModel> media, Action<bool> close)
    {
        Title = title;

        foreach (var m in media)
        {
            if (m is null) continue;
            var item = new MediaSelectionItem(m);
            if (m.IsImage)
            {
                PhotoItems.Add(item);
                // Декодирование изображения в фоновом потоке, не блокирует GUI.
                _ = m.LoadImageAsync();
            }
            else if (m.IsVideo)
            {
                VideoItems.Add(item);
            }
            else if (m.IsAudio)
            {
                AudioItems.Add(item);
            }
        }

        _close = close;
    }

    /// <summary>Id выбранных медиа (пусто — ничего не выбрано).</summary>
    public IReadOnlyList<int> GetSelectedMediaIds() =>
        PhotoItems.Concat<MediaSelectionItem>(VideoItems).Concat(AudioItems)
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
}
