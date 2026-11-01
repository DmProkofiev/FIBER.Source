using FIBER.Source.Models;
using FIBER.Source.Models.Enums;
using FIBER.Source.Services.Comparison;

namespace FIBER.Source.Tests.Unit;

public class MetadataStrategyTests
{
    private readonly MetadataStrategy _strategy = new();

    [Fact]
    public void Compare_NullInputs_ReturnsZero()
    {
        Assert.Equal(0.0, _strategy.Compare(null!, null!));
        Assert.Equal(0.0, _strategy.Compare(new Person(), null!));
        Assert.Equal(0.0, _strategy.Compare(null!, new Person()));
    }

    [Fact]
    public void Compare_AllFieldsMatch_ReturnsOne()
    {
        var a = new Person
        {
            Passport = "1234 567890",
            Phone = "+7 900 111-22-33",
            Mail = "Test@Mail.Ru",
            Address = "ул. Ленина, 1",
            SocialMedia = "vk.com/test",
            Car = "Toyota",
            City = "Москва",
            Job = "Инженер",
            CountryCode = "RU"
        };
        var b = new Person
        {
            Passport = "1234 567890",
            Phone = "+7 (900) 111 22 33",
            Mail = "test@mail.ru",
            Address = "ул. Ленина, 1",
            SocialMedia = "VK.COM/TEST",
            Car = "toyota",
            City = "москва",
            Job = "инженер",
            CountryCode = "ru"
        };

        Assert.Equal(1.0, _strategy.Compare(a, b));
    }

    [Fact]
    public void Compare_NoComparableFields_ReturnsZero()
    {
        var a = new Person { LastName = "Иванов" };
        var b = new Person { LastName = "Петров" };

        Assert.Equal(0.0, _strategy.Compare(a, b));
    }

    [Fact]
    public void Compare_PartialMatch_ReturnsWeightedScore()
    {
        var a = new Person { Passport = "1234 567890", Phone = "+7 900 111-22-33" };
        var b = new Person { Passport = "1234 567890", Phone = "+7 900 222-33-44" };

        // Passport вес 5.0, Phone вес 3.0. Совпал только паспорт.
        // score = 5.0 / (5.0 + 3.0) = 0.625
        var score = _strategy.Compare(a, b);

        Assert.Equal(0.625, score);
    }

    [Fact]
    public void Compare_Phone_DigitsOnlyComparison()
    {
        var a = new Person { Phone = "8-900-111-22-33" };
        var b = new Person { Phone = "8 (900) 111 22 33" };

        // Оба дают 89001112233 → совпадают, вес 3.0, только ОДНО поле сравнимо.
        // Штраф за малое число полей: score ограничен 0.5.
        Assert.Equal(0.5, _strategy.Compare(a, b));
    }

    [Fact]
    public void Compare_Mail_CaseInsensitive()
    {
        var a = new Person { Mail = "User@Example.COM" };
        var b = new Person { Mail = "user@example.com" };

        // Только ОДНО поле Mail сравнимо. Штраф за малое число полей: score ограничен 0.5.
        Assert.Equal(0.5, _strategy.Compare(a, b));
    }

    [Fact]
    public void Compare_OneSideEmpty_FieldNotCompared()
    {
        var a = new Person { Passport = "1234 567890" };
        var b = new Person { Phone = "+7 900 111-22-33" };

        // Нет общих сравнимых полей → 0.0
        Assert.Equal(0.0, _strategy.Compare(a, b));
    }
}
