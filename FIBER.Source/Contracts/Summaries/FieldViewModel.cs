using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace FIBER.Source.Contracts.Summaries;

public sealed partial class FieldViewModel : ObservableObject
{
    [ObservableProperty] private string _label = string.Empty;
    [ObservableProperty] private string _value = string.Empty;
}
