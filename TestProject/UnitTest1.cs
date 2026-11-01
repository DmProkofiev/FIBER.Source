namespace TestProject;

public class UnitTest1
{
    [Fact]
    public void TestProject_ReferencesMainProject_TypesAreAccessible()
    {
        // Проверяет, что ссылка на FIBER.Source работает и типы видны из тестов
        var kind = FIBER.Source.Models.Enums.PersonRelationKind.Parent;
        Assert.Equal(FIBER.Source.Models.Enums.PersonRelationKind.Child,
            FIBER.Source.Models.Helpers.PersonRelationKinds.InverseOf(kind));
    }
}
