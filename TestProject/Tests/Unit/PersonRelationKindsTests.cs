using FIBER.Source.Models.Enums;
using FIBER.Source.Models.Helpers;

namespace FIBER.Source.Tests.Unit;

public class PersonRelationKindsTests
{
    [Fact]
    public void InverseOf_Parent_ReturnsChild()
    {
        Assert.Equal(PersonRelationKind.Child, PersonRelationKinds.InverseOf(PersonRelationKind.Parent));
    }

    [Fact]
    public void InverseOf_Child_ReturnsParent()
    {
        Assert.Equal(PersonRelationKind.Parent, PersonRelationKinds.InverseOf(PersonRelationKind.Child));
    }

    [Fact]
    public void InverseOf_Symmetric_ReturnsSame()
    {
        Assert.Equal(PersonRelationKind.Sibling, PersonRelationKinds.InverseOf(PersonRelationKind.Sibling));
        Assert.Equal(PersonRelationKind.Spouse, PersonRelationKinds.InverseOf(PersonRelationKind.Spouse));
        Assert.Equal(PersonRelationKind.Friend, PersonRelationKinds.InverseOf(PersonRelationKind.Friend));
        Assert.Equal(PersonRelationKind.Colleague, PersonRelationKinds.InverseOf(PersonRelationKind.Colleague));
        Assert.Equal(PersonRelationKind.BusinessPartner, PersonRelationKinds.InverseOf(PersonRelationKind.BusinessPartner));
    }

    [Fact]
    public void InverseOf_Employer_ReturnsEmployeeOf()
    {
        Assert.Equal(PersonRelationKind.EmployeeOf, PersonRelationKinds.InverseOf(PersonRelationKind.Employer));
        Assert.Equal(PersonRelationKind.Employer, PersonRelationKinds.InverseOf(PersonRelationKind.EmployeeOf));
    }

    [Fact]
    public void InverseOf_Mentor_ReturnsMentee()
    {
        Assert.Equal(PersonRelationKind.Mentee, PersonRelationKinds.InverseOf(PersonRelationKind.Mentor));
        Assert.Equal(PersonRelationKind.Mentor, PersonRelationKinds.InverseOf(PersonRelationKind.Mentee));
    }

    [Fact]
    public void InverseOf_Grandparent_ReturnsGrandchild()
    {
        Assert.Equal(PersonRelationKind.Grandchild, PersonRelationKinds.InverseOf(PersonRelationKind.Grandparent));
        Assert.Equal(PersonRelationKind.Grandparent, PersonRelationKinds.InverseOf(PersonRelationKind.Grandchild));
    }

    [Fact]
    public void InverseOf_UncleAunt_ReturnsNephewNiece()
    {
        Assert.Equal(PersonRelationKind.NephewNiece, PersonRelationKinds.InverseOf(PersonRelationKind.UncleAunt));
        Assert.Equal(PersonRelationKind.UncleAunt, PersonRelationKinds.InverseOf(PersonRelationKind.NephewNiece));
    }

    [Fact]
    public void InverseOf_Other_ReturnsOther()
    {
        Assert.Equal(PersonRelationKind.Other, PersonRelationKinds.InverseOf(PersonRelationKind.Other));
    }

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
    public void DisplayNameForRelated_ParentMale_ReturnsОтец()
    {
        Assert.Equal("Отец", PersonRelationKinds.DisplayNameForRelated(PersonRelationKind.Parent, Gender.Male));
    }

    [Fact]
    public void DisplayNameForRelated_ParentFemale_ReturnsМать()
    {
        Assert.Equal("Мать", PersonRelationKinds.DisplayNameForRelated(PersonRelationKind.Parent, Gender.Female));
    }

    [Fact]
    public void DisplayNameForRelated_ParentUnknown_ReturnsРодитель()
    {
        Assert.Equal("Родитель", PersonRelationKinds.DisplayNameForRelated(PersonRelationKind.Parent, Gender.Unknown));
    }

    [Fact]
    public void DisplayNameForRelated_SpouseFemale_ReturnsЖена()
    {
        Assert.Equal("Жена", PersonRelationKinds.DisplayNameForRelated(PersonRelationKind.Spouse, Gender.Female));
    }

    [Fact]
    public void DisplayNameForRelated_FriendFemale_ReturnsПодруга()
    {
        Assert.Equal("Подруга", PersonRelationKinds.DisplayNameForRelated(PersonRelationKind.Friend, Gender.Female));
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
