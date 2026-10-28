namespace FIBER.Source.ViewModels;

// Одна связь для отображения в SearchView: тип связи и объект, с которым связь.
// Только для просмотра — CRUD выполняется в режиме редактирования записи.
public sealed class RelationItemViewModel
{
    public RelationItemViewModel(string kind, string target)
    {
        Kind = kind;
        Target = target;
    }

    public string Kind { get; }
    public string Target { get; }
}
