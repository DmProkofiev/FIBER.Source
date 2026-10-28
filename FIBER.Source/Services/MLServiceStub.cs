using FIBER.Source.Interfaces;

namespace FIBER.Source.Services;

public class MLServiceStub : IMLService
{
    public async Task<List<int>> FindPotentialDuplicatesAsync(int personId)
    {
        // Заглушка: в будущем здесь будет вызов Python-сервиса или ONNX Runtime
        await Task.Delay(100);
        return new List<int>();
    }

    public async Task<string> ExtractTextFromImageAsync(string imagePath)
    {
        await Task.Delay(100);
        return "Текст не распознан (ML-модуль не подключен)";
    }
}