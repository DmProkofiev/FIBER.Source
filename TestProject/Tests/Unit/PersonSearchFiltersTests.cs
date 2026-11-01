using FIBER.Source.Services.Search;

namespace FIBER.Source.Tests.Unit;

public class PersonSearchFiltersTests
{
    [Fact]
    public void JoinFullName_AllParts_ReturnsJoined()
    {
        var result = PersonSearchFilters.JoinFullName("Иванов", "Иван", "Иванович");
        Assert.Equal("Иванов Иван Иванович", result);
    }

    [Fact]
    public void JoinFullName_MissingSecondName_ReturnsTwoParts()
    {
        var result = PersonSearchFilters.JoinFullName("Иванов", "Иван", null);
        Assert.Equal("Иванов Иван", result);
    }

    [Fact]
    public void JoinFullName_OnlyLastName_ReturnsLastName()
    {
        var result = PersonSearchFilters.JoinFullName("Иванов", null, null);
        Assert.Equal("Иванов", result);
    }

    [Fact]
    public void JoinFullName_AllEmpty_ReturnsEmpty()
    {
        var result = PersonSearchFilters.JoinFullName(null, null, null);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void JoinFullName_WhitespaceParts_AreSkipped()
    {
        var result = PersonSearchFilters.JoinFullName("  ", "Иван", "   ");
        Assert.Equal("Иван", result);
    }

    [Fact]
    public void DigitsOnly_KeepsDigits()
    {
        Assert.Equal("79001112233", PersonSearchFilters.DigitsOnly("+7 (900) 111-22-33"));
    }

    [Fact]
    public void DigitsOnly_Null_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, PersonSearchFilters.DigitsOnly(null));
    }

    [Fact]
    public void DigitsOnly_NoDigits_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, PersonSearchFilters.DigitsOnly("abc"));
    }

    [Fact]
    public void Normalize_UsesSearchTermNormalizeWord()
    {
        Assert.Equal("hello world", PersonSearchFilters.Normalize("  Hello   World  "));
    }
}
