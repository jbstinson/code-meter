namespace CodeMeter.Core;

public class PollingService : IDisposable
{
    private readonly Timer _timer;
    internal readonly Func<Task> Callback;

    public PollingService(int intervalSeconds, Func<Task> callback)
    {
        Callback = callback;
        _timer = new Timer(OnTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(intervalSeconds));
    }

    public Task ForceRunAsync() => Callback();

    private void OnTick(object? _) => Callback().GetAwaiter().GetResult();

    public void Dispose() => _timer.Dispose();
}
