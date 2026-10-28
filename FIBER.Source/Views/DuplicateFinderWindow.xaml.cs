using System.Windows;
using FIBER.Source.Interfaces;
using FIBER.Source.ViewModels;

namespace FIBER.Source.Views;

// Окно поиска дубликатов
public partial class DuplicateFinderWindow : Window
{
    // Создаёт VM и запускает первичный поиск
    public DuplicateFinderWindow(
        int personId,
        IDuplicateFinderService duplicateFinderService)
    {
        InitializeComponent();
        var vm = new DuplicateFinderViewModel(personId, duplicateFinderService);
        DataContext = vm;
        Loaded += async (_, _) => await vm.InitializeAsync();
    }

    // Закрывает окно
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}