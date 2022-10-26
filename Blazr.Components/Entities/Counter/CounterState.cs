namespace Blazr.Components;

public class CounterState
{
    public int Counter { get; private set; }

    public Action<int>? CounterUpdated;

    public void IncrementCounter()
    {
        this.Counter++;
        this.CounterUpdated?.Invoke(this.Counter);
    }
}

