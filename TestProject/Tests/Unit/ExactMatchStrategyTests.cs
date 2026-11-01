using FIBER.Source.Models;
using FIBER.Source.Models.Enums;
using FIBER.Source.Services.Comparison;

namespace FIBER.Source.Tests.Unit;

public class ExactMatchStrategyTests
{
    private readonly ExactMatchStrategy _strategy = new();

    [Fact]
    public void Compare_NullInputs_ReturnsZero()
    {
        Assert.Equal(0.0, _strategy.Compare(null!, null!));
        Assert.Equal(0.0, _strategy.Compare(new Person(), null!));
        Assert.Equal(0.0, _strategy.Compare(null!, new Person()));
    }

    [Fact]
    public void Compare_SamePassport_ReturnsOne()
    {
        var a = new Person { Status = IdentityStatus.Identified, LastName = "Иванов", FirstName = "Иван", Passport = "1234 567890" };
        var b = new Person { Status = IdentityStatus.Identified, LastName = "Петров", FirstName = "Пётр", Passport = "1234 567890" };

        Assert.Equal(1.0, _strategy.Compare(a, b));
    }

    [Fact]
    public void Compare_DifferentPassport_ReturnsZero()
    {
        var a = new Person { Status = IdentityStatus.Identified, LastName = "Иванов", FirstName = "Иван", Passport = "1234 567890" };
        var b = new Person { Status = IdentityStatus.Identified, LastName = "Петров", FirstName = "Пётр", Passport = "0987 654321" };

        Assert.Equal(0.0, _strategy.Compare(a, b));
    }

    [Fact]
    public void Compare_OnlyOneHasPassport_ReturnsZero()
    {
        var a = new Person { Status = IdentityStatus.Identified, LastName = "Иванов", FirstName = "Иван", Passport = "1234 567890" };
        var b = new Person { Status = IdentityStatus.Identified, LastName = "Петров", FirstName = "Пётр" };

        Assert.Equal(0.0, _strategy.Compare(a, b));
    }

    [Fact]
    public void Compare_BothNoPassport_SameFullNameAndBirthDate_ReturnsOne()
    {
        var a = new Person
        {
            Status = IdentityStatus.Identified,
            LastName = "Иванов",
            FirstName = "Иван",
            SecondName = "Иванович",
            BirthDate = new DateTime(1990, 1, 1)
        };
        var b = new Person
        {
            Status = IdentityStatus.Identified,
            LastName = "ИВАНОВ",
            FirstName = "ИВАН",
            SecondName = "ИВАНОВИЧ",
            BirthDate = new DateTime(1990, 1, 1)
        };

        Assert.Equal(1.0, _strategy.Compare(a, b));
    }

    [Fact]
    public void Compare_BothNoPassport_DifferentBirthDate_ReturnsZero()
    {
        var a = new Person
        {
            Status = IdentityStatus.Identified,
            LastName = "Иванов",
            FirstName = "Иван",
            BirthDate = new DateTime(1990, 1, 1)
        };
        var b = new Person
        {
            Status = IdentityStatus.Identified,
            LastName = "Иванов",
            FirstName = "Иван",
            BirthDate = new DateTime(1991, 1, 1)
        };

        Assert.Equal(0.0, _strategy.Compare(a, b));
    }

    [Fact]
    public void Compare_Anonymous_ReturnsZero()
    {
        var a = new Person { Status = IdentityStatus.Anonymous };
        var b = new Person { Status = IdentityStatus.Anonymous };

        Assert.Equal(0.0, _strategy.Compare(a, b));
    }

    [Fact]
    public void Compare_SameSecondName_Matches()
    {
        var a = new Person
        {
            Status = IdentityStatus.Identified,
            LastName = "Иванов",
            FirstName = "Иван",
            SecondName = "Иванович",
            BirthDate = new DateTime(1990, 1, 1)
        };
        var b = new Person
        {
            Status = IdentityStatus.Identified,
            LastName = "Иванов",
            FirstName = "Иван",
            SecondName = "ИВАНОВИЧ",
            BirthDate = new DateTime(1990, 1, 1)
        };

        Assert.Equal(1.0, _strategy.Compare(a, b));
    }

    [Fact]
    public void Compare_DifferentSecondName_ReturnsZero()
    {
        var a = new Person
        {
            Status = IdentityStatus.Identified,
            LastName = "Иванов",
            FirstName = "Иван",
            SecondName = "Иванович",
            BirthDate = new DateTime(1990, 1, 1)
        };
        var b = new Person
        {
            Status = IdentityStatus.Identified,
            LastName = "Иванов",
            FirstName = "Иван",
            SecondName = "Петрович",
            BirthDate = new DateTime(1990, 1, 1)
        };

        Assert.Equal(0.0, _strategy.Compare(a, b));
    }

    [Fact]
    public void Compare_MissingSecondName_ReturnsZero()
    {
        var a = new Person
        {
            Status = IdentityStatus.Identified,
            LastName = "Иванов",
            FirstName = "Иван",
            BirthDate = new DateTime(1990, 1, 1)
        };
        var b = new Person
        {
            Status = IdentityStatus.Identified,
            LastName = "Иванов",
            FirstName = "Иван",
            SecondName = "Иванович",
            BirthDate = new DateTime(1990, 1, 1)
        };

        Assert.Equal(0.0, _strategy.Compare(a, b));
    }
}
