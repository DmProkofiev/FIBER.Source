using System;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;

namespace FIBER.Source.Services.Comparison;

public class ExactMatchStrategy : IComparisonStrategy
{
    public string Name => "Точное совпадение";

    public double Compare(Person a, Person b)
    {
        if (string.Equals(a.FullName, b.FullName, StringComparison.OrdinalIgnoreCase) &&
            a.BirthDate == b.BirthDate)
        {
            return 1.0;
        }
        return 0.0;
    }
}