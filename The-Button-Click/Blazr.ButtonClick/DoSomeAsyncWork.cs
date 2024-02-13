namespace Blazr.ButtonClick;

public class DoSomeAsyncWork
{
    private TaskCompletionSource<int> _taskCompletionSource = new();
    private Timer? _timer;
    private int _count;

    public Task<int> GetNextAsync(int value, int delay = 2000)
    {
        _taskCompletionSource = new();
        _count = value;
        _timer = new(OnTimerExpired, null, delay, 0);
        return _taskCompletionSource.Task;
    }

    private void OnTimerExpired(object? state)
    {
        _count++;
        _taskCompletionSource.SetResult(_count);
        _timer?.Dispose();
    }
}
