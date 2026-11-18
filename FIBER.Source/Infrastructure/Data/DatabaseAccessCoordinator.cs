namespace FIBER.Source.Infrastructure.Data;

/// <summary>Не допускает смену ключа, пока существуют рабочие контексты БД.</summary>
public sealed class DatabaseAccessCoordinator
{
    private readonly object _sync = new();
    private int _readers;
    private bool _exclusive;
    private TaskCompletionSource _changed = NewSignal();

    public async Task<IDisposable> AcquireAsync(bool exclusive = false, CancellationToken ct = default)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            Task changed;
            lock (_sync)
            {
                // Вложенные операции (например, аудит) могут закончиться до смены ключа.
                if (!_exclusive && (!exclusive || _readers == 0))
                {
                    if (exclusive) _exclusive = true;
                    else _readers++;
                    return new Lease(this, exclusive);
                }
                changed = _changed.Task;
            }
            await changed.WaitAsync(ct).ConfigureAwait(false);
        }
    }

    private void Release(bool exclusive)
    {
        lock (_sync)
        {
            if (exclusive) _exclusive = false;
            else _readers--;
            var signal = _changed;
            _changed = NewSignal();
            signal.TrySetResult();
        }
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class Lease(DatabaseAccessCoordinator owner, bool exclusive) : IDisposable
    {
        private DatabaseAccessCoordinator? _owner = owner;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release(exclusive);
    }
}
