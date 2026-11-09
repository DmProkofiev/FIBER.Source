using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FIBER.Source.Contracts.Summaries;
using FIBER.Source.Models.Enums;
using FIBER.Source.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace FIBER.Source.Views.Search;

/// <summary>
/// Унифицированная панель деталей выбранной сущности для SearchView.
/// Устраняет дублирование разметки между вкладками Личности / Организации / События.
/// </summary>
public partial class EntityDetailsPanel : UserControl
{
    public EntityDetailsPanel() => InitializeComponent();

    #region Dependency properties

    public static readonly DependencyProperty EntityDisplayNameProperty =
        DependencyProperty.Register(nameof(EntityDisplayName), typeof(string), typeof(EntityDetailsPanel),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty FieldsProperty =
        DependencyProperty.Register(nameof(Fields), typeof(ObservableCollection<FieldViewModel>), typeof(EntityDetailsPanel),
            new PropertyMetadata(null));

    public static readonly DependencyProperty RelationsProperty =
        DependencyProperty.Register(nameof(Relations), typeof(ObservableCollection<RelationItemViewModel>), typeof(EntityDetailsPanel),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ImagesProperty =
        DependencyProperty.Register(nameof(Images), typeof(ObservableCollection<MediaItemViewModel>), typeof(EntityDetailsPanel),
            new PropertyMetadata(null));

    public static readonly DependencyProperty VideosProperty =
        DependencyProperty.Register(nameof(Videos), typeof(ObservableCollection<MediaItemViewModel>), typeof(EntityDetailsPanel),
            new PropertyMetadata(null));

    public static readonly DependencyProperty AudioProperty =
        DependencyProperty.Register(nameof(Audio), typeof(ObservableCollection<MediaItemViewModel>), typeof(EntityDetailsPanel),
            new PropertyMetadata(null));

    public static readonly DependencyProperty HasSelectionProperty =
        DependencyProperty.Register(nameof(HasSelection), typeof(bool), typeof(EntityDetailsPanel),
            new PropertyMetadata(false));

    public static readonly DependencyProperty NavigationHistoryCountProperty =
        DependencyProperty.Register(nameof(NavigationHistoryCount), typeof(int), typeof(EntityDetailsPanel),
            new PropertyMetadata(0));

    public static readonly DependencyProperty EditCommandProperty =
        DependencyProperty.Register(nameof(EditCommand), typeof(ICommand), typeof(EntityDetailsPanel), new PropertyMetadata(null));

    public static readonly DependencyProperty DeleteCommandProperty =
        DependencyProperty.Register(nameof(DeleteCommand), typeof(ICommand), typeof(EntityDetailsPanel), new PropertyMetadata(null));

    public static readonly DependencyProperty ExportCommandProperty =
        DependencyProperty.Register(nameof(ExportCommand), typeof(ICommand), typeof(EntityDetailsPanel), new PropertyMetadata(null));

    public static readonly DependencyProperty DuplicatesCommandProperty =
        DependencyProperty.Register(nameof(DuplicatesCommand), typeof(ICommand), typeof(EntityDetailsPanel), new PropertyMetadata(null));

    public static readonly DependencyProperty ClearCommandProperty =
        DependencyProperty.Register(nameof(ClearCommand), typeof(ICommand), typeof(EntityDetailsPanel), new PropertyMetadata(null));

    public static readonly DependencyProperty NavigateBackCommandProperty =
        DependencyProperty.Register(nameof(NavigateBackCommand), typeof(ICommand), typeof(EntityDetailsPanel), new PropertyMetadata(null));

    #endregion

    #region CLR wrappers

    public string EntityDisplayName
    {
        get => (string)GetValue(EntityDisplayNameProperty);
        set => SetValue(EntityDisplayNameProperty, value);
    }

    public ObservableCollection<FieldViewModel> Fields
    {
        get => (ObservableCollection<FieldViewModel>)GetValue(FieldsProperty);
        set => SetValue(FieldsProperty, value);
    }

    public ObservableCollection<RelationItemViewModel> Relations
    {
        get => (ObservableCollection<RelationItemViewModel>)GetValue(RelationsProperty);
        set => SetValue(RelationsProperty, value);
    }

    public ObservableCollection<MediaItemViewModel> Images
    {
        get => (ObservableCollection<MediaItemViewModel>)GetValue(ImagesProperty);
        set => SetValue(ImagesProperty, value);
    }

    public ObservableCollection<MediaItemViewModel> Videos
    {
        get => (ObservableCollection<MediaItemViewModel>)GetValue(VideosProperty);
        set => SetValue(VideosProperty, value);
    }

    public ObservableCollection<MediaItemViewModel> Audio
    {
        get => (ObservableCollection<MediaItemViewModel>)GetValue(AudioProperty);
        set => SetValue(AudioProperty, value);
    }

    public bool HasSelection
    {
        get => (bool)GetValue(HasSelectionProperty);
        set => SetValue(HasSelectionProperty, value);
    }

    public int NavigationHistoryCount
    {
        get => (int)GetValue(NavigationHistoryCountProperty);
        set => SetValue(NavigationHistoryCountProperty, value);
    }

    public ICommand EditCommand
    {
        get => (ICommand)GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    public ICommand DeleteCommand
    {
        get => (ICommand)GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    public ICommand ExportCommand
    {
        get => (ICommand)GetValue(ExportCommandProperty);
        set => SetValue(ExportCommandProperty, value);
    }

    public ICommand DuplicatesCommand
    {
        get => (ICommand)GetValue(DuplicatesCommandProperty);
        set => SetValue(DuplicatesCommandProperty, value);
    }

    public ICommand ClearCommand
    {
        get => (ICommand)GetValue(ClearCommandProperty);
        set => SetValue(ClearCommandProperty, value);
    }

    public ICommand NavigateBackCommand
    {
        get => (ICommand)GetValue(NavigateBackCommandProperty);
        set => SetValue(NavigateBackCommandProperty, value);
    }

    #endregion

    // Обработка двойного клика на изображении — delegating to VM command
    private void Image_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (sender is not System.Windows.FrameworkElement element) return;
        if (element.DataContext is not MediaItemViewModel mediaVm) return;

        // Ищем ZoomImageCommand в DataContext UserControl (SearchViewModel)
        if (DataContext is SearchViewModel vm)
            vm.ZoomImageCommand.Execute(mediaVm);
    }

    // Обработка двойного клика на контейнере видео — открывает VideoZoomWindow
    private void VideoBorder_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (sender is not System.Windows.FrameworkElement element) return;
        if (element.DataContext is not MediaItemViewModel mediaVm) return;

        if (DataContext is SearchViewModel vm)
            vm.ZoomVideoCommand?.Execute(mediaVm);
    }
}
