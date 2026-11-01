using FIBER.Source.Services.Search;

namespace FIBER.Source.Tests.Unit;

public class SqliteLikeTests
{
    [Fact]
    public void Escape_EscapesBackslash()
    {
        Assert.Equal("\\\\", SqliteLike.Escape("\\"));
    }

    [Fact]
    public void Escape_EscapesPercent()
    {
        Assert.Equal("\\%", SqliteLike.Escape("%"));
    }

    [Fact]
    public void Escape_EscapesUnderscore()
    {
        Assert.Equal("\\_", SqliteLike.Escape("_"));
    }

    [Fact]
    public void Escape_EscapesBracket()
    {
        Assert.Equal("\\[", SqliteLike.Escape("["));
    }



    [Fact]
    public void ToTitleCase_FirstLetterUpper_RestLower()
    {
        Assert.Equal("Hello", SqliteLike.ToTitleCase("hello"));
        Assert.Equal("Hello", SqliteLike.ToTitleCase("HELLO"));
        Assert.Equal("Hello world", SqliteLike.ToTitleCase("hELLO WORLD"));
    }

    [Fact]
    public void ToTitleCase_Empty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SqliteLike.ToTitleCase(string.Empty));
    }

    [Fact]
    public void CaseVariants_ReturnsDistinctVariants()
    {
        var variants = SqliteLike.CaseVariants("test").ToList();
        Assert.Contains("test", variants);
        Assert.Contains("TEST", variants);
        Assert.Contains("Test", variants);
    }

    [Fact]
    public void CaseVariants_NoDuplicates()
    {
        var variants = SqliteLike.CaseVariants("TEST").ToList();
        Assert.Equal(variants.Distinct().Count(), variants.Count);
    }

    [Fact]
    public void CaseVariants_SingleChar_ReturnsVariants()
    {
        var variants = SqliteLike.CaseVariants("a").ToList();
        Assert.Contains("a", variants);
        Assert.Contains("A", variants);
    }
}
