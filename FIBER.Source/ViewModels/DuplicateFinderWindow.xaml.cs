using System;
using System.Windows;
using FIBER.Source.Interfaces;
using FIBER.Source.ViewModels;

namespace FIBER.Source.Views;

// Окно поиска дубликатов (личности или организации — в зависимости от конструктора)
public partial class DuplicateFinderWindow : Window
{
    // Создаёт VM и запускает первичный поиск (режим личности)
    public DuplicateFinderWindow(
        int personId,
        IDuplicateFinderService duplicateFinderService)
    {
        InitializeComponent();
        var vm = new DuplicateFinderViewModel(personId, duplicateFinderService);
        DataContext = vm;
        Loaded += async (_, _) => await vm.InitializeAsync();
    }

    // Режим поиска дубликатов организаций
    public DuplicateFinderWindow(
        int companyId,
        bool isCompanyMode,
        IDuplicateFinderService duplicateFinderService)
    {
        InitializeComponent();
        var vm = new DuplicateFinderViewModel(companyId, isCompanyMode, duplicateFinderService);
        DataContext = vm;
        Loaded += async (_, _) => await vm.InitializeAsync();
    }

    // Закрывает окно
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}