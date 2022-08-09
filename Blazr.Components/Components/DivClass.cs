using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazr.Components;

public class DivClass : IComponent
{
    private RenderHandle _renderHandle;
    private RenderFragment _renderFragment;

    public DivClass()
    {
        _renderFragment = BuildComponent;
    }
    public void Attach(RenderHandle renderHandle)
    {
        _renderHandle = renderHandle;
    }

    public Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        _renderHandle.Render(_renderFragment);
        return Task.CompletedTask;
    }

    private void BuildComponent(RenderTreeBuilder builder)
    {
        builder.AddMarkupContent(0, "<div><h3>Hello Blazor</h3></div>");
    }

}

