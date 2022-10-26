using Microsoft.AspNetCore.Components;

namespace Blazr.Components;

public class Minimal : IComponent
{
    public void Attach(RenderHandle handle)
        =>  handle.Render( (builder) => builder.AddMarkupContent(0, "<h1>Hello from Minimal</h1>") );

    public Task SetParametersAsync(ParameterView parameters)
        => Task.CompletedTask;
}
