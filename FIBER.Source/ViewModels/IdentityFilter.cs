using FIBER.Source.Models.Enums;

namespace FIBER.Source.ViewModels;

// Опция фильтра в Search: All / Not identified / Identified
public sealed record IdentityFilter(bool? IsIdentified, string Display)
{
    public override string ToString() => Display;
}
