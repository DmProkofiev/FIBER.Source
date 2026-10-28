namespace FIBER.Source.Interfaces;

public interface ITrayService : IDisposable
{
    void Initialize(Action onOpen, Action onExit);
    void ShowMinimizeNotification();
}