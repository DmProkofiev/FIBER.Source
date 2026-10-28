using System.Windows;
using System.Windows.Input;
using FIBER.Source.ViewModels;
using UserControl = System.Windows.Controls.UserControl;
using DragEventArgs = System.Windows.DragEventArgs;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;

namespace FIBER.Source.Views;

public partial class CreatePersonView : UserControl
{
    public CreatePersonView()
    {
        InitializeComponent();
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void DropZone_DragLeave(object sender, DragEventArgs e)
    {
        // Можно добавить визуальную реакцию на уход курсора
    }

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (DataContext is CreatePersonViewModel vm)
            {
                await vm.HandleFilesDropAsync(files);
            }
        }
    }
}