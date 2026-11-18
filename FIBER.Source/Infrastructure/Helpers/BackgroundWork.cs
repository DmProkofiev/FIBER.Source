using System.Runtime.CompilerServices;

namespace FIBER.Source.Infrastructure.Helpers;

public static class BackgroundWork
{
    /// <summary>
    /// SQLite не имеет настоящего async I/O. На входе в сервис переносим продолжение
    /// на пул потоков, включая синхронную часть EF и файловые API без async-аналогов.
    /// Вызывающая ViewModel после await возвращается в свой UI-контекст.
    /// </summary>
    public static ConfiguredTaskAwaitable SwitchToThreadPool() =>
        Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
}
