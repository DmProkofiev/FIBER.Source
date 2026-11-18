using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FIBER.Source.Interfaces;
using FIBER.Source.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using ListViewItem = System.Windows.Controls.ListViewItem;

namespace FIBER.Source.Views;

public partial class DuplicateFinderWindow : Window
{
    public event Action<RelationTargetType, int>? NavigateRequested;
    public DuplicateFinderWindow(
        int personId,
        IDuplicateFinderService duplicateFinderService)
    {
        InitializeComponent();
        var notificationService = ((App)System.Windows.Application.Current).Services!.GetRequiredService<INotificationService>();
        var vm = new DuplicateFinderViewModel(personId, duplicateFinderService, notificationService);
        DataContext = vm;
        vm.NavigateRequested += (type, id) => { NavigateRequested?.Invoke(type, id); };
        vm.CloseRequested += () => Close();
        Loaded += async (_, _) => await vm.InitializeAsync();
    }

    public DuplicateFinderWindow(
        int companyId,
        bool isCompanyMode,
        IDuplicateFinderService duplicateFinderService)
    {
        InitializeComponent();
        var notificationService = ((App)System.Windows.Application.Current).Services!.GetRequiredService<INotificationService>();
        var vm = new DuplicateFinderViewModel(companyId, isCompanyMode, duplicateFinderService, notificationService);
        DataContext = vm;
        vm.NavigateRequested += (type, id) => { NavigateRequested?.Invoke(type, id); };
        vm.CloseRequested += () => Close();
        Loaded += async (_, _) => await vm.InitializeAsync();
    }

    private void PersonCandidate_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListViewItem { DataContext: DuplicateCandidate candidate }) return;
        if (DataContext is DuplicateFinderViewModel vm)
            vm.NavigateToCandidateCommand.Execute(candidate);
    }
}
