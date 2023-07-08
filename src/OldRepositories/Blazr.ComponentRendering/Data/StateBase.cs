namespace Blazr.ComponentRendering;

public abstract class StateBase<TRecord>
{
    protected TRecord? _baseRecord;
   
    public bool IsDirty => _baseRecord?.Equals(this.AsRecord()) ?? false;

    public abstract void Load(TRecord record);

    public abstract TRecord AsRecord();
    public event EventHandler<bool>? StateUpdated;
    public event EventHandler<string?>? FieldUpdated;

    public void NotifyFieldChanged(string? field)
    {
        this.FieldUpdated?.Invoke(null, field);
        this.StateUpdated?.Invoke(null, IsDirty);
    }

    protected void SetAndNotifyIfChanged<TType>(ref TType currentValue, TType value, string fieldName)
    {
        if (!value?.Equals(currentValue) ?? currentValue is not null)
        {
            currentValue = value;
            this.NotifyFieldChanged(fieldName);
        }
    }
}
