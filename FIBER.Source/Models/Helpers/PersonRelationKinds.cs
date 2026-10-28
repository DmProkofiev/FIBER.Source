using System;
using System.Collections.Generic;
using System.Linq;
using FIBER.Source.Models.Enums;

namespace FIBER.Source.Models.Helpers;

public sealed record PersonRelationKindOption(PersonRelationKind Value, string Display);

public static class PersonRelationKinds
{
    public static PersonRelationKind InverseOf(PersonRelationKind kind) => kind switch
    {
        PersonRelationKind.Parent => PersonRelationKind.Child,
        PersonRelationKind.Child => PersonRelationKind.Parent,
        PersonRelationKind.Grandparent => PersonRelationKind.Grandchild,
        PersonRelationKind.Grandchild => PersonRelationKind.Grandparent,
        PersonRelationKind.UncleAunt => PersonRelationKind.NephewNiece,
        PersonRelationKind.NephewNiece => PersonRelationKind.UncleAunt,
        PersonRelationKind.Mentor => PersonRelationKind.Mentee,
        PersonRelationKind.Mentee => PersonRelationKind.Mentor,
        PersonRelationKind.Employer => PersonRelationKind.EmployeeOf,
        PersonRelationKind.EmployeeOf => PersonRelationKind.Employer,
        PersonRelationKind.Other => PersonRelationKind.Other,
        _ => kind
    };

    public static string DisplayNameOfKind(PersonRelationKind kind) => kind switch
    {
        PersonRelationKind.Parent => "Родитель",
        PersonRelationKind.Child => "Ребёнок",
        PersonRelationKind.Sibling => "Брат/сестра",
        PersonRelationKind.Spouse => "Супруг(а)",
        PersonRelationKind.Grandparent => "Дедушка/бабушка",
        PersonRelationKind.Grandchild => "Внук/внучка",
        PersonRelationKind.UncleAunt => "Дядя/тётя",
        PersonRelationKind.NephewNiece => "Племянник(ца)",
        PersonRelationKind.Cousin => "Двоюродный брат/сестра",
        PersonRelationKind.Friend => "Друг",
        PersonRelationKind.CloseFriend => "Близкий друг",
        PersonRelationKind.Acquaintance => "Знакомый",
        PersonRelationKind.Neighbor => "Сосед",
        PersonRelationKind.Colleague => "Коллега",
        PersonRelationKind.Classmate => "Одноклассник",
        PersonRelationKind.BusinessPartner => "Партнёр",
        PersonRelationKind.CoFounder => "Соучредитель",
        PersonRelationKind.Mentor => "Наставник",
        PersonRelationKind.Mentee => "Подопечный",
        PersonRelationKind.Employer => "Работодатель",
        PersonRelationKind.EmployeeOf => "Работник",
        PersonRelationKind.Opponent => "Оппонент",
        PersonRelationKind.CoConspirator => "Соучастник",
        PersonRelationKind.Cohabitant => "Сожитель",
        PersonRelationKind.Other => "Другое",
        _ => kind.ToString()
    };

    public static string DisplayNameForRelated(PersonRelationKind kind, Gender relatedGender)
        => (kind, relatedGender) switch
        {
            (PersonRelationKind.Parent, Gender.Male) => "Отец",
            (PersonRelationKind.Parent, Gender.Female) => "Мать",
            (PersonRelationKind.Parent, _) => "Родитель",
            (PersonRelationKind.Child, Gender.Male) => "Сын",
            (PersonRelationKind.Child, Gender.Female) => "Дочь",
            (PersonRelationKind.Child, _) => "Ребёнок",
            (PersonRelationKind.Sibling, Gender.Male) => "Брат",
            (PersonRelationKind.Sibling, Gender.Female) => "Сестра",
            (PersonRelationKind.Sibling, _) => "Брат/сестра",
            (PersonRelationKind.Spouse, Gender.Male) => "Муж",
            (PersonRelationKind.Spouse, Gender.Female) => "Жена",
            (PersonRelationKind.Spouse, _) => "Супруг(а)",
            (PersonRelationKind.Grandparent, Gender.Male) => "Дедушка",
            (PersonRelationKind.Grandparent, Gender.Female) => "Бабушка",
            (PersonRelationKind.Grandparent, _) => "Дедушка/бабушка",
            (PersonRelationKind.Grandchild, Gender.Male) => "Внук",
            (PersonRelationKind.Grandchild, Gender.Female) => "Внучка",
            (PersonRelationKind.Grandchild, _) => "Внук/внучка",
            (PersonRelationKind.UncleAunt, Gender.Male) => "Дядя",
            (PersonRelationKind.UncleAunt, Gender.Female) => "Тётя",
            (PersonRelationKind.UncleAunt, _) => "Дядя/тётя",
            (PersonRelationKind.NephewNiece, Gender.Male) => "Племянник",
            (PersonRelationKind.NephewNiece, Gender.Female) => "Племянница",
            (PersonRelationKind.NephewNiece, _) => "Племянник(ца)",
            (PersonRelationKind.Cousin, Gender.Male) => "Двоюродный брат",
            (PersonRelationKind.Cousin, Gender.Female) => "Двоюродная сестра",
            (PersonRelationKind.Cousin, _) => "Двоюродный брат/сестра",
            (PersonRelationKind.Mentor, Gender.Female) => "Наставница",
            (PersonRelationKind.Mentee, Gender.Female) => "Подопечная",
            (PersonRelationKind.EmployeeOf, Gender.Female) => "Работница",
            (PersonRelationKind.Friend, Gender.Female) => "Подруга",
            (PersonRelationKind.CloseFriend, Gender.Female) => "Близкая подруга",
            (PersonRelationKind.Neighbor, Gender.Female) => "Соседка",
            (PersonRelationKind.Classmate, Gender.Female) => "Одноклассница",
            (PersonRelationKind.Acquaintance, Gender.Female) => "Знакомая",
            (PersonRelationKind.Opponent, Gender.Female) => "Оппонентка",
            (PersonRelationKind.CoConspirator, Gender.Female) => "Соучастница",
            (PersonRelationKind.Cohabitant, Gender.Female) => "Сожительница",
            (PersonRelationKind.CoFounder, Gender.Female) => "Соучредительница",
            (PersonRelationKind.BusinessPartner, Gender.Female) => "Партнёрша",
            _ => DisplayNameOfKind(kind)
        };

    public static IReadOnlyList<PersonRelationKindOption> AllOptions { get; } =
        Enum.GetValues<PersonRelationKind>()
            .Select(k => new PersonRelationKindOption(k, DisplayNameOfKind(k)))
            .ToList();
}