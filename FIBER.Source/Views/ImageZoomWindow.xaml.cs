using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using FIBER.Source.ViewModels;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseWheelEventArgs = System.Windows.Input.MouseWheelEventArgs;
using Key = System.Windows.Input.Key;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace FIBER.Source.Views;

public partial class ImageZoomWindow : Window
{
    private double _zoom = 1.0;
    private readonly List<MediaItemViewModel> _images;
    private int _currentIndex;

    // Переменные для перетаскивания
    private bool _isDragging;
    private Point _lastMousePosition;

    public ImageZoomWindow(List<MediaItemViewModel> images, int startIndex)
    {
        InitializeComponent();
        _images = images;
        _currentIndex = startIndex;
        LoadCurrentImage();
    }

    private void LoadCurrentImage()
    {
        if (_images == null || _currentIndex < 0 || _currentIndex >= _images.Count) return;

        var current = _images[_currentIndex];
        TitleText.Text = $"{current.FileName} ({_currentIndex + 1} из {_images.Count})";
        ZoomImage.Source = current.ImageSource;

        // Сброс масштаба и позиции при переключении
        _zoom = 1.0;
        ImageScale.ScaleX = 1.0;
        ImageScale.ScaleY = 1.0;
        ImageTranslate.X = 0;
        ImageTranslate.Y = 0;

        PrevButton.Visibility = _currentIndex > 0 ? Visibility.Visible : Visibility.Hidden;
        NextButton.Visibility = _currentIndex < _images.Count - 1 ? Visibility.Visible : Visibility.Hidden;
    }

    private void Prev_Click(object sender, RoutedEventArgs e)
    {
        if (_currentIndex > 0)
        {
            _currentIndex--;
            LoadCurrentImage();
        }
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_currentIndex < _images.Count - 1)
        {
            _currentIndex++;
            LoadCurrentImage();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
        else if (e.Key == Key.Left) Prev_Click(sender, e);
        else if (e.Key == Key.Right) Next_Click(sender, e);
    }

    // Обработка зума (событие привязано к Image)
    private void Image_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ZoomImage.Source == null) return;
        e.Handled = true; // Блокируем прокрутку страницы

        double zoomFactor = e.Delta > 0 ? 1.15 : 1.0 / 1.15;
        double newZoom = _zoom * zoomFactor;
        newZoom = Math.Max(0.1, Math.Min(10.0, newZoom)); // Ограничение от 10% до 1000%

        Point mousePos = e.GetPosition(ZoomContainer);
        double scaleChange = newZoom / _zoom;

        // Математика зума относительно курсора мыши
        ImageTranslate.X = mousePos.X - (mousePos.X - ImageTranslate.X) * scaleChange;
        ImageTranslate.Y = mousePos.Y - (mousePos.Y - ImageTranslate.Y) * scaleChange;

        _zoom = newZoom;
        ImageScale.ScaleX = _zoom;
        ImageScale.ScaleY = _zoom;
    }

    // Начало перетаскивания
    private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ZoomImage.Source == null) return;
        _isDragging = true;
        _lastMousePosition = e.GetPosition(ZoomContainer);
        ZoomImage.CaptureMouse(); // Захватываем мышь на самой картинке
        e.Handled = true;
    }

    // Конец перетаскивания
    private void Image_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        ZoomImage.ReleaseMouseCapture();
        e.Handled = true;
    }

    // Процесс перетаскивания
    private void Image_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        Point currentPos = e.GetPosition(ZoomContainer);
        Vector delta = currentPos - _lastMousePosition;

        ImageTranslate.X += delta.X;
        ImageTranslate.Y += delta.Y;

        _lastMousePosition = currentPos;
        e.Handled = true;
    }

    // Страховка: если захват мыши потерян (например, Alt+Tab), сбрасываем перетаскивание
    private void Image_LostMouseCapture(object sender, MouseEventArgs e)
    {
        _isDragging = false;
    }
}