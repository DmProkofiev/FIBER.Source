namespace TestProject;

public class UnitTest1
{
    [Fact]
    public void TestProject_ReferencesMainProject_TypesAreAccessible()
    {
        // Проверяет, что ссылка на FIBER.Source работает и типы видны из тестов
        var kind = FIBER.Source.Models.Enums.PersonRelationKind.Father;
        Assert.Equal(FIBER.Source.Models.Enums.PersonRelationKind.Father, kind);
    }
}
