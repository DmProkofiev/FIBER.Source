using System.Collections.Generic;
using System.Linq;
using FIBER.Source.Models.Enums;

namespace FIBER.Source.Models.Helpers;

public sealed record PersonRelationKindOption(PersonRelationKind Value, string Display);

public static class PersonRelationKinds
{
    // Дописать до 100 штук
    public static string DisplayNameOfKind(PersonRelationKind kind) => kind switch
    {
        PersonRelationKind.Father => "Отец",
        PersonRelationKind.Mother => "Мать",
        PersonRelationKind.Son => "Сын",
        PersonRelationKind.Daughter => "Дочь",
        PersonRelationKind.Brother => "Брат",
        PersonRelationKind.Sister => "Сестра",
        PersonRelationKind.Husband => "Муж",
        PersonRelationKind.Wife => "Жена",
        PersonRelationKind.Grandfather => "Дедушка",
        PersonRelationKind.Grandmother => "Бабушка",
        PersonRelationKind.Grandson => "Внук",
        PersonRelationKind.Granddaughter => "Внучка",
        PersonRelationKind.Uncle => "Дядя",
        PersonRelationKind.Aunt => "Тётя",
        PersonRelationKind.Nephew => "Племянник",
        PersonRelationKind.Niece => "Племянница",
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
            (PersonRelationKind.Father, _) => "Отец",
            (PersonRelationKind.Mother, _) => "Мать",
            (PersonRelationKind.Son, _) => "Сын",
            (PersonRelationKind.Daughter, _) => "Дочь",
            (PersonRelationKind.Brother, _) => "Брат",
            (PersonRelationKind.Sister, _) => "Сестра",
            (PersonRelationKind.Husband, _) => "Муж",
            (PersonRelationKind.Wife, _) => "Жена",
            (PersonRelationKind.Grandfather, _) => "Дедушка",
            (PersonRelationKind.Grandmother, _) => "Бабушка",
            (PersonRelationKind.Grandson, _) => "Внук",
            (PersonRelationKind.Granddaughter, _) => "Внучка",
            (PersonRelationKind.Uncle, _) => "Дядя",
            (PersonRelationKind.Aunt, _) => "Тётя",
            (PersonRelationKind.Nephew, _) => "Племянник",
            (PersonRelationKind.Niece, _) => "Племянница",
            (PersonRelationKind.Cousin, Gender.Male) => "Двоюродный брат",
            (PersonRelationKind.Cousin, Gender.Female) => "Двоюродная сестра",
            (PersonRelationKind.Cousin, _) => "Двоюродный брат/сестра",
            (PersonRelationKind.Friend, Gender.Male) => "Друг",
            (PersonRelationKind.Friend, Gender.Female) => "Подруга",
            (PersonRelationKind.Friend, _) => "Друг",
            (PersonRelationKind.CloseFriend, Gender.Male) => "Близкий друг",
            (PersonRelationKind.CloseFriend, Gender.Female) => "Близкая подруга",
            (PersonRelationKind.CloseFriend, _) => "Близкий друг",
            (PersonRelationKind.Acquaintance, Gender.Male) => "Знакомый",
            (PersonRelationKind.Acquaintance, Gender.Female) => "Знакомая",
            (PersonRelationKind.Acquaintance, _) => "Знакомый",
            (PersonRelationKind.Neighbor, Gender.Male) => "Сосед",
            (PersonRelationKind.Neighbor, Gender.Female) => "Соседка",
            (PersonRelationKind.Neighbor, _) => "Сосед",
            (PersonRelationKind.Colleague, Gender.Male) => "Коллега",
            (PersonRelationKind.Colleague, Gender.Female) => "Коллега",
            (PersonRelationKind.Colleague, _) => "Коллега",
            (PersonRelationKind.Classmate, Gender.Male) => "Одноклассник",
            (PersonRelationKind.Classmate, Gender.Female) => "Одноклассница",
            (PersonRelationKind.Classmate, _) => "Одноклассник",
            (PersonRelationKind.BusinessPartner, Gender.Male) => "Партнёр",
            (PersonRelationKind.BusinessPartner, Gender.Female) => "Партнёрша",
            (PersonRelationKind.BusinessPartner, _) => "Партнёр",
            (PersonRelationKind.CoFounder, Gender.Male) => "Соучредитель",
            (PersonRelationKind.CoFounder, Gender.Female) => "Соучредительница",
            (PersonRelationKind.CoFounder, _) => "Соучредитель",
            (PersonRelationKind.Mentor, Gender.Male) => "Наставник",
            (PersonRelationKind.Mentor, Gender.Female) => "Наставница",
            (PersonRelationKind.Mentor, _) => "Наставник",
            (PersonRelationKind.Mentee, Gender.Male) => "Подопечный",
            (PersonRelationKind.Mentee, Gender.Female) => "Подопечная",
            (PersonRelationKind.Mentee, _) => "Подопечный",
            (PersonRelationKind.Employer, Gender.Male) => "Работодатель",
            (PersonRelationKind.Employer, Gender.Female) => "Работодатель",
            (PersonRelationKind.Employer, _) => "Работодатель",
            (PersonRelationKind.EmployeeOf, Gender.Male) => "Работник",
            (PersonRelationKind.EmployeeOf, Gender.Female) => "Работница",
            (PersonRelationKind.EmployeeOf, _) => "Работник",
            (PersonRelationKind.Opponent, Gender.Male) => "Оппонент",
            (PersonRelationKind.Opponent, Gender.Female) => "Оппонентка",
            (PersonRelationKind.Opponent, _) => "Оппонент",
            (PersonRelationKind.CoConspirator, Gender.Male) => "Соучастник",
            (PersonRelationKind.CoConspirator, Gender.Female) => "Соучастница",
            (PersonRelationKind.CoConspirator, _) => "Соучастник",
            (PersonRelationKind.Cohabitant, Gender.Male) => "Сожитель",
            (PersonRelationKind.Cohabitant, Gender.Female) => "Сожительница",
            (PersonRelationKind.Cohabitant, _) => "Сожитель",
            (PersonRelationKind.Other, _) => "Другое",
            _ => DisplayNameOfKind(kind)
        };

    public static IReadOnlyList<PersonRelationKindOption> AllOptions { get; } =
        Enum.GetValues<PersonRelationKind>()
            .Select(k => new PersonRelationKindOption(k, DisplayNameOfKind(k)))
            .ToList();
}
