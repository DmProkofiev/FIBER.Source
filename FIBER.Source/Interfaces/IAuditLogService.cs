using FIBER.Source.Models;

namespace FIBER.Source.Interfaces;

public interface IAuditLogService
{
    Task LogAsync(string action, string details = "");
    Task<List<AuditLogEntry>> GetRecentLogsAsync(int count = 100);
}