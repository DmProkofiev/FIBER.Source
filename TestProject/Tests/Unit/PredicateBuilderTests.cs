using System.Linq.Expressions;
using FIBER.Source.Services.Search;

namespace FIBER.Source.Tests.Unit;

public class PredicateBuilderTests
{
    [Fact]
    public void Empty_ReturnsTruePredicate()
    {
        var pred = PredicateBuilder.Empty<PersonModel>();
        var result = pred.Compile()(new PersonModel());
        Assert.True(result);
    }

    [Fact]
    public void False_ReturnsFalsePredicate()
    {
        var pred = PredicateBuilder.False<PersonModel>();
        var result = pred.Compile()(new PersonModel());
        Assert.False(result);
    }

    [Fact]
    public void And_CombinesPredicates()
    {
        var left = PredicateBuilder.Empty<PersonModel>();
        var right = PredicateBuilder.False<PersonModel>();
        var combined = left.And(right);
        Assert.False(combined.Compile()(new PersonModel()));
    }

    [Fact]
    public void Or_CombinesPredicates()
    {
        var left = PredicateBuilder.False<PersonModel>();
        var right = PredicateBuilder.Empty<PersonModel>();
        var combined = left.Or(right);
        Assert.True(combined.Compile()(new PersonModel()));
    }

    [Fact]
    public void And_WithEmpty_IsRight()
    {
        var emptyPred = PredicateBuilder.Empty<PersonModel>();
        var right = PredicateBuilder.False<PersonModel>();
        var combined = emptyPred.And(right);
        Assert.False(combined.Compile()(new PersonModel()));
    }

    [Fact]
    public void Or_WithFalse_IsRight()
    {
        var falsePred = PredicateBuilder.False<PersonModel>();
        var right = PredicateBuilder.Empty<PersonModel>();
        var combined = falsePred.Or(right);
        Assert.True(combined.Compile()(new PersonModel()));
    }

    private sealed class PersonModel
    {
        public int Id { get; set; }
    }
}
