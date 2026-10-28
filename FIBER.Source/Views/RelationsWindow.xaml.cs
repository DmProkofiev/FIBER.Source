using System.Windows;
using FIBER.Source.Interfaces;
using FIBER.Source.Models.Enums;
using FIBER.Source.ViewModels;

namespace FIBER.Source.Views;

// Окно управления связями
public partial class RelationsWindow : Window
{
    public RelationsWindow(
        MediaOwnerType ownerType,
        int ownerId,
        IRelationService relationService,
        IPersonService personService,
        ICompanyService companyService,
        IEventService eventService)
    {
        InitializeComponent();
        var vm = new RelationsViewModel(ownerType, ownerId, relationService, personService, companyService, eventService);
        DataContext = vm;
        Loaded += async (_, _) => await vm.InitializeAsync();
    }

    // Закрывает окно
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
