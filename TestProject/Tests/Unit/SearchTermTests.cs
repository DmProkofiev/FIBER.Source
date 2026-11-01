using FIBER.Source.Services.Search;

namespace FIBER.Source.Tests.Unit;

public class SearchTermTests
{
    [Fact]
    public void Parse_NullOrEmpty_ReturnsEmpty()
    {
        var term = SearchTerm.Parse(null);
        Assert.False(term.HasAnything);
        Assert.Empty(term.Words);
        Assert.Null(term.FullDate);
        Assert.Null(term.Year);

        term = SearchTerm.Parse(string.Empty);
        Assert.False(term.HasAnything);
    }

    [Fact]
    public void Parse_OnlyWhitespace_ReturnsEmpty()
    {
        var term = SearchTerm.Parse("   ");
        Assert.False(term.HasAnything);
    }

    [Fact]
    public void Parse_NameOnly_HasName()
    {
        var term = SearchTerm.Parse("Иванов Иван");
        Assert.True(term.HasName);
        Assert.Equal(2, term.Words.Count);
        Assert.Null(term.FullDate);
        Assert.Null(term.Year);
    }

    [Fact]
    public void Parse_NameWithDate_HasNameAndDate()
    {
        var term = SearchTerm.Parse("Иванов 01.01.1990");
        Assert.True(term.HasName);
        Assert.True(term.HasDate);
        Assert.Equal(new DateTime(1990, 1, 1), term.FullDate);
        Assert.Single(term.Words);
    }

    [Fact]
    public void Parse_NameWithYear_HasNameAndYear()
    {
        var term = SearchTerm.Parse("Иванов 1990");
        Assert.True(term.HasName);
        Assert.True(term.HasDate);
        Assert.Null(term.FullDate);
        Assert.Equal(1990, term.Year);
        Assert.Single(term.Words);
    }

    [Fact]
    public void Parse_OnlyDate_NoName()
    {
        var term = SearchTerm.Parse("01.01.1990");
        Assert.False(term.HasName);
        Assert.True(term.HasDate);
        Assert.Equal(new DateTime(1990, 1, 1), term.FullDate);
    }

    [Fact]
    public void Parse_OnlyYear_NoName()
    {
        var term = SearchTerm.Parse("1990");
        Assert.False(term.HasName);
        Assert.True(term.HasDate);
        Assert.Equal(1990, term.Year);
    }

    [Fact]
    public void Parse_YearOutOfRange_TreatedAsWord()
    {
        var term = SearchTerm.Parse("1800");
        Assert.True(term.HasName);
        Assert.Equal("1800", term.Words[0]);
        Assert.Null(term.Year);
    }

    [Fact]
    public void NormalizeWord_TrimsAndLowercases()
    {
        var result = SearchTerm.NormalizeWord("  Hello   World  ");
        Assert.Equal("hello world", result);
    }

    [Fact]
    public void NormalizeWord_CollapsesMultipleSpaces()
    {
        var result = SearchTerm.NormalizeWord("a   b    c");
        Assert.Equal("a b c", result);
    }

    [Fact]
    public void NormalizeWord_Null_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SearchTerm.NormalizeWord(null));
    }
}
