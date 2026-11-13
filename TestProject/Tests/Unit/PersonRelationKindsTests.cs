using FIBER.Source.Models.Enums;
using FIBER.Source.Models.Helpers;

namespace FIBER.Source.Tests.Unit;

public class PersonRelationKindsTests
{
    [Fact]
    public void DisplayNameOfKind_AllKinds_ReturnNonEmpty()
    {
        foreach (var kind in Enum.GetValues<PersonRelationKind>())
        {
            var name = PersonRelationKinds.DisplayNameOfKind(kind);
            Assert.False(string.IsNullOrWhiteSpace(name), $"Пустое отображение для {kind}");
        }
    }

    [Fact]
    public void DisplayNameForRelated_FatherMale_ReturnsОтец()
    {
        Assert.Equal("Отец", PersonRelationKinds.DisplayNameForRelated(PersonRelationKind.Father, Gender.Male));
    }

    [Fact]
    public void DisplayNameForRelated_FatherFemale_ReturnsОтец()
    {
        Assert.Equal("Отец", PersonRelationKinds.DisplayNameForRelated(PersonRelationKind.Father, Gender.Female));
    }

    [Fact]
    public void DisplayNameForRelated_MotherFemale_ReturnsМать()
    {
        Assert.Equal("Мать", PersonRelationKinds.DisplayNameForRelated(PersonRelationKind.Mother, Gender.Female));
    }

    [Fact]
    public void DisplayNameForRelated_SonMale_ReturnsСын()
    {
        Assert.Equal("Сын", PersonRelationKinds.DisplayNameForRelated(PersonRelationKind.Son, Gender.Male));
    }

    [Fact]
    public void DisplayNameForRelated_DaughterFemale_ReturnsДочь()
    {
        Assert.Equal("Дочь", PersonRelationKinds.DisplayNameForRelated(PersonRelationKind.Daughter, Gender.Female));
    }

    [Fact]
    public void DisplayNameForRelated_HusbandMale_ReturnsМуж()
    {
        Assert.Equal("Муж", PersonRelationKinds.DisplayNameForRelated(PersonRelationKind.Husband, Gender.Male));
    }

    [Fact]
    public void DisplayNameForRelated_WifeFemale_ReturnsЖена()
    {
        Assert.Equal("Жена", PersonRelationKinds.DisplayNameForRelated(PersonRelationKind.Wife, Gender.Female));
    }

    [Fact]
    public void AllOptions_ContainsAllKinds()
    {
        var options = PersonRelationKinds.AllOptions;
        Assert.Equal(Enum.GetValues<PersonRelationKind>().Length, options.Count);
        foreach (var kind in Enum.GetValues<PersonRelationKind>())
        {
            Assert.Contains(options, o => o.Value == kind);
        }
    }
}
