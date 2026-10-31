using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FIBER.Source.Helpers.Converters;

/// <summary>
/// Преобразует количество в <see cref="Visibility"/>: больше нуля — Visible, иначе Collapsed.
/// Используется для показа кнопки «Назад» при непустой истории переходов по связям.
/// </summary>
public sealed class PositiveCountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int count = value is int number ? number : 0;
        return count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
