using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazr.Components;

public abstract class Minimal1Base : IComponent
{
    protected RenderHandle? renderHandle;

    public void Attach(RenderHandle handle)
        => renderHandle = handle;

    public Task SetParametersAsync(ParameterView parameters)
    {
        // Sets the component parameters to the latest values
        parameters.SetParameterProperties(this);
        // Creates a render fragment as an anonymous function that calls BuildRenderTree
        RenderFragment fragment = (builder) => BuildRenderTree(builder);
        // passes the fragment to the RenderTree to render
        this.renderHandle?.Render(fragment);
        return Task.CompletedTask;
    }

    protected abstract void BuildRenderTree(RenderTreeBuilder builder);
}
