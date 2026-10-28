using System.Windows;
using FIBER.Source.ViewModels;
using DragEventArgs = System.Windows.DragEventArgs;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using UserControl = System.Windows.Controls.UserControl;

namespace FIBER.Source.Views;

// Code-behind формы редактирования события: обработчики drop-зоны медиа
public partial class UpdateEventView : UserControl
{
    public UpdateEventView() => InitializeComponent();

    // Разрешает перетаскивание файлов; иначе — запрещает
    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    // Ничего не делает; оставлен для симметрии
    private void DropZone_DragLeave(object sender, DragEventArgs e) { }

    // Передаёт перетащенные файлы в VM
    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (DataContext is UpdateEventViewModel vm)
            await vm.HandleFilesDropAsync(files);
    }
}