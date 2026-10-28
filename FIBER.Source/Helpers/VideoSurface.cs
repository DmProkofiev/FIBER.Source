using System;
using System.Windows;
using System.Windows.Media;

namespace FIBER.Source.Helpers;

public class VideoSurface : FrameworkElement
{
    public static readonly DependencyProperty PlayerProperty =
        DependencyProperty.Register(nameof(Player), typeof(MediaPlayer), typeof(VideoSurface),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public MediaPlayer? Player
    {
        get => (MediaPlayer?)GetValue(PlayerProperty);
        set => SetValue(PlayerProperty, value);
    }

    public VideoSurface()
    {
        Loaded += (s, e) => CompositionTarget.Rendering += OnRendering;
        Unloaded += (s, e) => CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (Player != null && Player.NaturalVideoWidth > 0)
        {
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (Player == null) return;

        int vw = Player.NaturalVideoWidth;
        int vh = Player.NaturalVideoHeight;
        if (vw <= 0 || vh <= 0) return;

        double videoAspect = (double)vw / vh;
        double widgetAspect = ActualWidth / ActualHeight;
        Rect rect;

        if (videoAspect > widgetAspect)
        {
            double h = ActualWidth / videoAspect;
            rect = new Rect(0, (ActualHeight - h) / 2, ActualWidth, h);
        }
        else
        {
            double w = ActualHeight * videoAspect;
            rect = new Rect((ActualWidth - w) / 2, 0, w, ActualHeight);
        }

        dc.DrawVideo(Player, rect);
    }
}