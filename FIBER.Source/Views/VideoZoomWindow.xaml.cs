using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FIBER.Source.MediaPlayer;
using FIBER.Source.ViewModels;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseWheelEventArgs = System.Windows.Input.MouseWheelEventArgs;
using Key = System.Windows.Input.Key;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using DragStartedEventArgs = System.Windows.Controls.Primitives.DragStartedEventArgs;
using DragCompletedEventArgs = System.Windows.Controls.Primitives.DragCompletedEventArgs;

namespace FIBER.Source.Views
{
    /// Окно просмотра видео с зумом, перемещением, листаиванием и элементами управления.
    public partial class VideoZoomWindow : Window
    {
        private double _zoom = 1.0;
        private readonly List<MediaItemViewModel> _videos;
        private int _currentIndex;

        private readonly IMediaPlayerService _mediaPlayerService;

        // Переменные для перетаскивания
        private bool _isDragging;
        private Point _lastMousePosition;

        // Флаг: пользователь тащит ползунок (чтобы таймер не перезаписывал Value)
        private bool _isSliderDragging;

        // Таймер обновления положения ползунка
        private readonly DispatcherTimer _positionTimer;

        public VideoZoomWindow(List<MediaItemViewModel> videos, int startIndex)
        {
            InitializeComponent();
            _videos = videos;
            _currentIndex = startIndex;

            // Создаём MediaElement-сервис для этого окна
            _mediaPlayerService = new MediaPlayerService(ZoomMediaElement);

            _positionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _positionTimer.Tick += PositionTimer_Tick;

            LoadCurrentVideo();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Фокус для обработки клавиш
            Keyboard.Focus(this);
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            // Гарантированно освобождаем ресурсы MediaElement при выгрузке окна
            _positionTimer?.Stop();
            _mediaPlayerService?.Stop();
            if (ZoomMediaElement != null)
            {
                ZoomMediaElement.Source = null;
                ZoomMediaElement.Stop();
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _positionTimer.Stop();
            _mediaPlayerService.Stop();

            // Освобождаем ресурсы MediaElement, чтобы не оставались висячие иконки в трее
            if (ZoomMediaElement != null)
            {
                ZoomMediaElement.Source = null;
                ZoomMediaElement.Stop();
            }
        }

        private void LoadCurrentVideo()
        {
            if (_videos == null || _currentIndex < 0 || _currentIndex >= _videos.Count) return;

            var current = _videos[_currentIndex];
            TitleText.Text = $"{current.FileName} ({_currentIndex + 1} из {_videos.Count})";

            // Сброс масштаба и позиции при переключении
            _zoom = 1.0;
            VideoScale.ScaleX = 1.0;
            VideoScale.ScaleY = 1.0;
            VideoTranslate.X = 0;
            VideoTranslate.Y = 0;

            // Останавливаем предыдущее видео и загружаем новое
            _mediaPlayerService.Stop();
            _mediaPlayerService.FilePath = current.FilePath;

            PrevButton.Visibility = _currentIndex > 0 ? Visibility.Visible : Visibility.Hidden;
            NextButton.Visibility = _currentIndex < _videos.Count - 1 ? Visibility.Visible : Visibility.Hidden;

            UpdatePlayButtonState();
        }

        private void UpdatePlayButtonState()
        {
            // Все три кнопки всегда видны — пользователь сам выбирает действие
        }

        private void Prev_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex > 0)
            {
                _currentIndex--;
                LoadCurrentVideo();
            }
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex < _videos.Count - 1)
            {
                _currentIndex++;
                LoadCurrentVideo();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
            else if (e.Key == Key.Left) Prev_Click(sender, e);
            else if (e.Key == Key.Right) Next_Click(sender, e);
            else if (e.Key == Key.Space)
            {
                if (_mediaPlayerService.IsPlaying)
                    _mediaPlayerService.Pause();
                else
                    _mediaPlayerService.Play();
                UpdatePlayButtonState();
            }
        }

        // ===== Управление воспроизведением =====

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            _mediaPlayerService.Play();
            _positionTimer.Start();
            UpdatePlayButtonState();
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            _mediaPlayerService.Pause();
            _positionTimer.Stop();
            UpdatePlayButtonState();
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            _mediaPlayerService.Stop();
            _positionTimer.Stop();
            // Полный сброс воспроизведения
            _mediaPlayerService.Position = 0;
            PositionSlider.Value = 0;
            UpdatePlayButtonState();
        }

        // Зум и перемещение

        private void Media_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ZoomMediaElement.Source == null) return;
            e.Handled = true;

            double zoomFactor = e.Delta > 0 ? 1.15 : 1.0 / 1.15;
            double newZoom = _zoom * zoomFactor;
            newZoom = Math.Max(0.1, Math.Min(10.0, newZoom));

            Point mousePos = e.GetPosition(ZoomContainer);
            double scaleChange = newZoom / _zoom;

            VideoTranslate.X = mousePos.X - (mousePos.X - VideoTranslate.X) * scaleChange;
            VideoTranslate.Y = mousePos.Y - (mousePos.Y - VideoTranslate.Y) * scaleChange;

            _zoom = newZoom;
            VideoScale.ScaleX = _zoom;
            VideoScale.ScaleY = _zoom;
        }

        private void Media_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ZoomMediaElement.Source == null) return;
            _isDragging = true;
            _lastMousePosition = e.GetPosition(ZoomContainer);
            ZoomMediaElement.CaptureMouse();
            e.Handled = true;
        }

        private void Media_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            ZoomMediaElement.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void Media_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;

            Point currentPos = e.GetPosition(ZoomContainer);
            Vector delta = currentPos - _lastMousePosition;

            VideoTranslate.X += delta.X;
            VideoTranslate.Y += delta.Y;

            _lastMousePosition = currentPos;
            e.Handled = true;
        }

        private void Media_LostMouseCapture(object sender, MouseEventArgs e)
        {
            _isDragging = false;
        }

        private void Media_MediaOpened(object sender, RoutedEventArgs e)
        {
            PositionSlider.Maximum = _mediaPlayerService.Duration;
        }

        // ===== Ползунок позиции =====

        private void PositionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isSliderDragging || _mediaPlayerService == null)
                return;

            if (PositionSlider.Maximum > 0)
            {
                _mediaPlayerService.Position = PositionSlider.Value;
            }
        }

        private void PositionSlider_DragStarted(object sender, DragStartedEventArgs e)
        {
            _isSliderDragging = true;
        }

        private void PositionSlider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _isSliderDragging = false;
            if (_mediaPlayerService != null && PositionSlider.Maximum > 0)
            {
                _mediaPlayerService.Position = PositionSlider.Value;
            }
        }

        private void PositionTimer_Tick(object? sender, EventArgs e)
        {
            if (_isSliderDragging || _mediaPlayerService == null)
                return;

            if (_mediaPlayerService.Duration > 0)
            {
                PositionSlider.Value = _mediaPlayerService.Position;
            }

            // Автоматический переход к следующему видео при завершении
            if (_mediaPlayerService.Position >= _mediaPlayerService.Duration - 0.5 &&
                _mediaPlayerService.Duration > 0)
            {
                Next_Click(this, new RoutedEventArgs());
            }
        }
    }
}
