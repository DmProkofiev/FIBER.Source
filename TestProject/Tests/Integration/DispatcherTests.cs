using System.Windows.Threading;
using FIBER.Source.Infrastructure.Helpers;
using FIBER.Source.MediaPlayer;

namespace TestProject.Tests.Integration;

public sealed class DispatcherTests
{
    [Fact]
    public Task PlaybackTimerDoesNotOverwritePendingSeekAndStopCancelsIt() => OnDispatcherAsync(async () =>
    {
        var service = new FakePlayer();
        var slider = new System.Windows.Controls.Slider { Maximum = 100 };
        var playback = new MediaControlPlayback(new System.Windows.Controls.MediaElement(), slider, service);
        playback.Play();
        slider.Value = 75;
        await Task.Delay(700);
        Assert.Equal([75.0], service.Seeks);
        Assert.Equal(75, slider.Value);
        slider.Value = 90;
        playback.Stop();
        await Task.Delay(450);
        Assert.Single(service.Seeks);
        Assert.Equal(0, slider.Value);
        slider.Value = 30;
        playback.Unload();
        await Task.Delay(450);
        Assert.Single(service.Seeks);
        Assert.Equal(string.Empty, service.FilePath);
    });

    private sealed class FakePlayer : IMediaPlayerService
    {
        private double _position = 5;
        public List<double> Seeks { get; } = [];
        public string FilePath { get; set; } = "test.wav";
        public double Position { get => _position; set { _position = value; Seeks.Add(value); } }
        public double Duration => 100;
        public bool IsPlaying { get; private set; }
        public void Play() => IsPlaying = true;
        public void Pause() => IsPlaying = false;
        public void Stop() { _position = 0; IsPlaying = false; }
    }

    [Fact]
    public Task DebounceAppliesLastUserValueOnceAndCancellationPreventsSeek() => OnDispatcherAsync(async () =>
    {
        var calls = new List<double>();
        using var debounce = new SeekDebouncer(calls.Add);
        debounce.Schedule(10);
        debounce.Schedule(30);
        debounce.Schedule(75);
        Assert.True(debounce.IsPending);
        await Task.Delay(650);
        Assert.Equal([75.0], calls);
        Assert.False(debounce.IsPending);
        debounce.Schedule(90);
        debounce.Cancel();
        await Task.Delay(450);
        Assert.Single(calls);
        debounce.Schedule(50);
        debounce.Dispose();
        await Task.Delay(450);
        Assert.Single(calls);
    });

    [Fact]
    public Task ServiceWorkLeavesDispatcherAndCallerResumesOnDispatcher() => OnDispatcherAsync(async () =>
    {
        var ui = Environment.CurrentManagedThreadId;
        var worker = await WorkAsync();
        Assert.NotEqual(ui, worker);
        Assert.Equal(ui, Environment.CurrentManagedThreadId);

        static async Task<int> WorkAsync()
        {
            await BackgroundWork.SwitchToThreadPool();
            Assert.Null(SynchronizationContext.Current);
            return Environment.CurrentManagedThreadId;
        }
    });

    private static Task OnDispatcherAsync(Func<Task> test)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(new Action(async () =>
            {
                try { await test(); completion.SetResult(); }
                catch (Exception ex) { completion.SetException(ex); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            }));
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }
}
