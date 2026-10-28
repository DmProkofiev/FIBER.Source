using System.Collections.Generic;
using System.Threading.Tasks;

namespace FIBER.Source.Interfaces;

/// <summary>Формат экспорта.</summary>
public enum ExportFormat
{
    Word,
    Txt
}

/// <summary>Контракт сервиса экспорта сущностей в отдельную папку с медиа.</summary>
public interface IExportService
{
    Task<string> ExportPersonAsync(
        int personId, string parentFolder, ExportFormat format,
        IReadOnlyList<int>? selectedMediaIds = null);

    Task<string> ExportCompanyAsync(
        int companyId, string parentFolder, ExportFormat format,
        IReadOnlyList<int>? selectedMediaIds = null);

    Task<string> ExportEventAsync(
        int eventId, string parentFolder, ExportFormat format,
        IReadOnlyList<int>? selectedMediaIds = null);
}