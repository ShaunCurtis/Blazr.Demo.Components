namespace Blazr.ComponentRendering;

public record CounterData(int Counter);

public class CounterState : StateBase<CounterData>
{
    private int _counter;
    public int Counter
    {
        get => _counter;
        set => SetAndNotifyIfChanged(ref _counter, value, "Counter");
    }

    public CounterState()
        => this.Load(new(Counter: 0));

    public override void Load(CounterData record)
    {
        _baseRecord = record;
        _counter = record.Counter;
    }

    public override CounterData AsRecord()
        => new CounterData(Counter: _counter);
}
