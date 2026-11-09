using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace FIBER.Source.Contracts.Summaries;

/// <summary>
/// Одно поле для отображения в унифицированной панели деталей сущности (EntityDetailsPanel).
/// Используется для устранения дублирования разметки SearchView между вкладками
/// Личности / Организации / События (DRY).
/// </summary>
public sealed partial class FieldViewModel : ObservableObject
{
    [ObservableProperty] private string _label = string.Empty;
    [ObservableProperty] private string _value = string.Empty;
}
