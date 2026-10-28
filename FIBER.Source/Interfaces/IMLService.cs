using FIBER.Source.Models;

namespace FIBER.Source.Interfaces;

public interface IMLService
{
    // Пример: предсказание возможных дубликатов на основе ML
    Task<List<int>> FindPotentialDuplicatesAsync(int personId);

    // Пример: распознавание текста с фото (OCR)
    Task<string> ExtractTextFromImageAsync(string imagePath);
}