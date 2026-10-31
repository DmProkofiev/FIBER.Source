using System;
using System.Windows;
using System.Windows.Input;
using FIBER.Source.Interfaces;
using FIBER.Source.ViewModels;
using ListViewItem = System.Windows.Controls.ListViewItem;

namespace FIBER.Source.Views;

// Окно поиска дубликатов (личности или организации — в зависимости от конструктора)
public partial class DuplicateFinderWindow : Window
{
    // Переход к найденному кандидату: тип объекта и его Id (обрабатывается SearchView).
    public event Action<RelationTargetType, int>? NavigateRequested;

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

    // Переход по Id кандидата: закрывает окно и сообщает SearchView, какой объект открыть.
    private void PersonCandidate_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListViewItem { DataContext: DuplicateCandidate candidate }) return;

        var navigate = NavigateRequested;
        Close();
        navigate?.Invoke(RelationTargetType.Person, candidate.Person.Id);
    }

    // Закрывает окно
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}