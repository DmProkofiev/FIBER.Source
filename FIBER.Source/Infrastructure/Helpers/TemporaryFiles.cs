using System.Diagnostics;
using System.IO;

namespace FIBER.Source.Infrastructure.Helpers;

public static class TemporaryFiles
{
    public static string Root { get; } = Path.Combine(Path.GetTempPath(), "FIBER");
    public static string SessionDirectory { get; } = Path.Combine(Root, Guid.NewGuid().ToString("N"));

    public static Task DeleteAsync(string path) => Task.Run(() =>
    {
        try { File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("Не удалось удалить временный файл {0}: {1}", path, ex);
        }
    });

    // Текущая сессия содержит в том числе ещё не сохранённые вложения.
    public static Task CleanupAsync(bool includeCurrentSession = false) => Task.Run(() =>
    {
        if (!Directory.Exists(Root)) return;
        foreach (var entry in Directory.EnumerateFileSystemEntries(Root))
        {
            if (!includeCurrentSession && string.Equals(entry, SessionDirectory, StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                if (Directory.Exists(entry)) Directory.Delete(entry, recursive: true);
                else File.Delete(entry);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Trace.TraceWarning("Не удалось очистить временный путь {0}: {1}", entry, ex);
            }
        }
    });
}
