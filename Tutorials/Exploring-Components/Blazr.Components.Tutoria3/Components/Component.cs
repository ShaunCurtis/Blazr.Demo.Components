using Blazr.ComponentStarter.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using System.Diagnostics;

namespace Blazr.Components;

public class Component : RazorBase, IComponent
{
    private Guid Uid = Guid.NewGuid();
    private RenderHandle _renderHandle;

    public Component()
        => Debug.WriteLine($"{Uid.ShortGuid()} - Basic Component Created");

    public void Attach(RenderHandle renderHandle)
    {
        _renderHandle = renderHandle;
        Debug.WriteLine($"{Uid.ShortGuid()} - Basic Component Attached");
    }

    public Task SetParametersAsync(ParameterView parameters)
    {
        _renderHandle.Render(BuildRenderTree);
        Debug.WriteLine($"{Uid.ShortGuid()} - Basic Component Rendered");
        return Task.CompletedTask;
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    { }
}
