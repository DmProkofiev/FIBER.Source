namespace FIBER.Source.Interfaces;

public interface ITrayService : IDisposable
{
    Task InitializeAsync(Action onOpen, Action onExit);
    void ShowMinimizeNotification();
}
