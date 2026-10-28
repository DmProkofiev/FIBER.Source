using FIBER.Source.Models;

namespace FIBER.Source.Interfaces;

public interface IComparisonStrategy
{
    string Name { get; }
    double Compare(Person a, Person b);
}