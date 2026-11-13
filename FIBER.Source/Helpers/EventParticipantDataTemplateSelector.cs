using System.Windows;
using System.Windows.Controls;
using FIBER.Source.ViewModels;
using Application = System.Windows.Application;

namespace FIBER.Source.Helpers;

/// <summary>
/// Выбор разметки объекта связи для SearchView:
/// для событийных участников (событие ↔ участник) используется EventParticipantTemplate,
/// для остальных связей — RelationItemTemplate.
/// </summary>
public class EventParticipantDataTemplateSelector : DataTemplateSelector
{
    public override DataTemplate? SelectTemplate(object item, DependencyObject? context)
    {
        var template = item is RelationItemViewModel r && r.IsEventParticipant
            ? Application.Current?.FindResource("EventParticipantTemplate") as DataTemplate
            : Application.Current?.FindResource("RelationItemTemplate") as DataTemplate;

        return template;
    }
}
