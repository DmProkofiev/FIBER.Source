using FIBER.Source.Models.Enums;

namespace FIBER.Source.ViewModels;

// Опция статуса в UpdatePerson: 2 варианта (Identified / Not identified)
public sealed record StatusOption(IdentityStatus Value, string Display)
{
    public override string ToString() => Display;
}

// Опция фильтра в Search: All / Not identified / Identified
public sealed record IdentityFilter(bool? IsIdentified, string Display)
{
    public override string ToString() => Display;
}