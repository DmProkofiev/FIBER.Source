using System.Windows;
using FIBER.Source.MediaPlayer;
using UserControl = System.Windows.Controls.UserControl;

namespace FIBER.Source.Views.Search;

public partial class VideoPlayerControl : UserControl
{
    private readonly MediaControlPlayback _playback;

    public static readonly DependencyProperty FilePathProperty = DependencyProperty.Register(
        nameof(FilePath), typeof(string), typeof(VideoPlayerControl), new PropertyMetadata(null, OnFilePathChanged));
    public static readonly DependencyProperty MediaServiceProperty = DependencyProperty.Register(
        nameof(MediaService), typeof(IMediaPlayerService), typeof(VideoPlayerControl), new PropertyMetadata(null, OnMediaServiceChanged));
    public static readonly DependencyProperty FileNameProperty = DependencyProperty.Register(
        nameof(FileName), typeof(string), typeof(VideoPlayerControl), new PropertyMetadata(string.Empty));

    public string FilePath { get => (string)GetValue(FilePathProperty); set => SetValue(FilePathProperty, value); }
    public string FileName { get => (string)GetValue(FileNameProperty); set => SetValue(FileNameProperty, value); }
    public IMediaPlayerService? MediaService { get => (IMediaPlayerService?)GetValue(MediaServiceProperty); set => SetValue(MediaServiceProperty, value); }

    public VideoPlayerControl()
    {
        InitializeComponent();
        _playback = new MediaControlPlayback(VideoMediaElement, VideoSlider);
        Loaded += async (_, _) =>
        {
            _playback.SetService(MediaService, FilePath);
            if (DataContext is ViewModels.MediaItemViewModel media) await media.LoadPlaybackFileAsync();
            else if (DataContext is ViewModels.MediaSelectionItem item) await item.Media.LoadPlaybackFileAsync();
        };
        Unloaded += (_, _) => _playback.Unload();
    }

    private static void OnFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (VideoPlayerControl)d;
        if (control.IsLoaded) control._playback?.SetFile(e.NewValue as string);
    }
    private static void OnMediaServiceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (VideoPlayerControl)d;
        control._playback?.SetService(e.NewValue as IMediaPlayerService, control.FilePath);
    }

    private void Play_Click(object sender, RoutedEventArgs e) => _playback.Play();
    private void Pause_Click(object sender, RoutedEventArgs e) => _playback.Pause();
    private void Stop_Click(object sender, RoutedEventArgs e) => _playback.Stop();
}
