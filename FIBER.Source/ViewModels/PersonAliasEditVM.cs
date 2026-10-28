using CommunityToolkit.Mvvm.ComponentModel;
using FIBER.Source.Contracts.Inputs;
using FIBER.Source.Models;
using FIBER.Source.Models.Enums;
using System.Collections.Generic;

namespace FIBER.Source.ViewModels;

// Редактируемая строка псевдонима
public partial class PersonAliasEditVM : ObservableObject
{
    public int? Id { get; set; }

    [ObservableProperty] private string _value = string.Empty;
    [ObservableProperty] private AliasType _type = AliasType.Alias;

    public static IReadOnlyList<AliasTypeOption> AvailableTypes { get; } = new List<AliasTypeOption>
    {
        new(AliasType.Alias, "Псевдоним"),
        new(AliasType.Handle, "Никнейм"),
        new(AliasType.Other, "Другое")
    };

    public PersonAliasInput ToInput(bool isPrimary = false) =>
        new(Id, Value.Trim(), Type, isPrimary);

    public static PersonAliasEditVM FromModel(PersonAlias a) => new()
    {
        Id = a.Id,
        Value = a.Value,
        Type = a.Type
    };
}

public sealed record AliasTypeOption(AliasType Value, string Display)
{
    public override string ToString() => Display;
}