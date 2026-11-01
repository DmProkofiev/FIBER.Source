using FIBER.Source.Models;
using FIBER.Source.Models.Enums;
using FIBER.Source.Services.Comparison;

namespace FIBER.Source.Tests.Unit;

public class LevenshteinStrategyTests
{
    private readonly LevenshteinStrategy _strategy = new();

    [Fact]
    public void Compare_NullInputs_ReturnsZero()
    {
        Assert.Equal(0.0, _strategy.Compare(null!, null!));
        Assert.Equal(0.0, _strategy.Compare(new Person(), null!));
        Assert.Equal(0.0, _strategy.Compare(null!, new Person()));
    }

    [Fact]
    public void Compare_IdenticalNames_ReturnsHighScore()
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
            BirthDate = new DateTime(1990, 1, 1)
        };

        var score = _strategy.Compare(a, b);

        Assert.InRange(score, 0.9, 1.0);
    }

    [Fact]
    public void Compare_TypoInName_ReturnsHighScore()
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
            FirstName = "Иванн",
            BirthDate = new DateTime(1990, 1, 1)
        };

        var score = _strategy.Compare(a, b);

        Assert.True(score > 0.7, $"Ожидался высокий скор для опечатки, получен {score}");
    }

    [Fact]
    public void Compare_CompletelyDifferentNames_ReturnsLowScore()
    {
        var a = new Person { Status = IdentityStatus.Identified, LastName = "Иванов", FirstName = "Иван" };
        var b = new Person { Status = IdentityStatus.Identified, LastName = "Петров", FirstName = "Пётр" };

        var score = _strategy.Compare(a, b);

        Assert.True(score < 0.6, $"Ожидался низкий скор для разных имён, получен {score}");
    }

    [Fact]
    public void Compare_Anonymous_UsesPrimaryAlias()
    {
        var a = new Person
        {
            Status = IdentityStatus.Anonymous,
            BirthDate = new DateTime(1990, 1, 1),
            Aliases = new List<PersonAlias> { new() { Value = "Бандит", IsPrimary = true } }
        };
        var b = new Person
        {
            Status = IdentityStatus.Anonymous,
            BirthDate = new DateTime(1990, 1, 1),
            Aliases = new List<PersonAlias> { new() { Value = "Бандит", IsPrimary = true } }
        };

        var score = _strategy.Compare(a, b);

        Assert.InRange(score, 0.9, 1.0);
    }

    [Fact]
    public void Compare_Anonymous_NoAlias_ReturnsZero()
    {
        var a = new Person { Status = IdentityStatus.Anonymous };
        var b = new Person { Status = IdentityStatus.Anonymous };

        Assert.Equal(0.0, _strategy.Compare(a, b));
    }

    [Fact]
    public void Compare_BirthDateSameDay_FactorIsOne()
    {
        var a = new Person { Status = IdentityStatus.Identified, LastName = "Иванов", FirstName = "Иван", BirthDate = new DateTime(1990, 5, 5) };
        var b = new Person { Status = IdentityStatus.Identified, LastName = "Иванов", FirstName = "Иван", BirthDate = new DateTime(1990, 5, 5) };

        var score = _strategy.Compare(a, b);

        Assert.Equal(1.0, score);
    }

    [Fact]
    public void Compare_BirthDateMissing_Both_FactorIsZeroSix()
    {
        var a = new Person { Status = IdentityStatus.Identified, LastName = "Иванов", FirstName = "Иван" };
        var b = new Person { Status = IdentityStatus.Identified, LastName = "Иванов", FirstName = "Иван" };

        var score = _strategy.Compare(a, b);

        Assert.Equal(0.6, score);
    }
}
