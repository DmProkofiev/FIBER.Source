using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FIBER.Source.Helpers;
using FIBER.Source.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;

namespace FIBER.Source.ViewModels;

/// <summary>Один медиаэлемент в списке выбора экспорта.</summary>
public partial class ExportMediaItem : ObservableObject
{
    public int MediaId { get; }
    public string DisplayName { get; }
    public string TypeLabel { get; }
    public ImageSource? ImageSource { get; }

    [ObservableProperty]
    private bool _isSelected = true;

    public ExportMediaItem(MediaItemViewModel media)
    {
        MediaId = media.MediaId ?? 0;
        DisplayName = media.FileName;
        TypeLabel = media.IsImage ? "Фото" : media.IsVideo ? "Видео" : "Аудио";
        ImageSource = media.ImageSource;
    }
}

/// <summary>VM диалога экспорта: формат, папка, выбор медиа.</summary>
public partial class ExportDialogViewModel : ObservableObject
{
    private readonly Action<bool> _close;

    public string ObjectTitle { get; }
    public ObservableCollection<ExportMediaItem> MediaItems { get; }
    public bool HasMedia => MediaItems.Count > 0;

    [ObservableProperty]
    private ExportFormat _selectedFormat = ExportFormat.Word;

    [ObservableProperty]
    private string _targetFolder = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public bool IsWordFormat
    {
        get => SelectedFormat == ExportFormat.Word;
        set { if (value) SelectedFormat = ExportFormat.Word; }
    }

    public bool IsTxtFormat
    {
        get => SelectedFormat == ExportFormat.Txt;
        set { if (value) SelectedFormat = ExportFormat.Txt; }
    }

    partial void OnSelectedFormatChanged(ExportFormat value)
    {
        OnPropertyChanged(nameof(IsWordFormat));
        OnPropertyChanged(nameof(IsTxtFormat));
    }

    public ExportDialogViewModel(string objectTitle, IEnumerable<MediaItemViewModel> media, Action<bool> close)
    {
        ObjectTitle = objectTitle;
        MediaItems = new ObservableCollection<ExportMediaItem>(media.Select(m => new ExportMediaItem(m)));
        _close = close;
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Куда экспортировать?",
            Multiselect = false
        };
        if (dialog.ShowDialog() == true)
            TargetFolder = dialog.FolderName;
    }

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
    private void Export()
    {
        if (string.IsNullOrWhiteSpace(TargetFolder))
        {
            Notification.Show("Внимание", "Выберите папку для экспорта", NotificationType.Warning);
            return;
        }
        _close(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        _close(false);
    }

    public IReadOnlyList<int> GetSelectedMediaIds() =>
        MediaItems.Where(m => m.IsSelected && m.MediaId > 0).Select(m => m.MediaId).ToList();
}