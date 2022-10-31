namespace Blazr.Components;

public class TotallyMinimal : IComponent
{
    public void Attach(RenderHandle handle) { }

    public Task SetParametersAsync(ParameterView parameters)
        => Task.CompletedTask;
}
