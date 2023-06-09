using Blazr.ComponentStarter.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using System.Diagnostics;

namespace Blazr.Components;

public class Component : RazorBase, IComponent
{
    private Guid Uid = Guid.NewGuid();
    private RenderHandle _renderHandle;
    private RenderFragment _content;
    private bool _renderPending;

    public Component()
    {
        Debug.WriteLine($"{Uid.ShortGuid()} - {this.GetType().Name} - Created");
        _content = (builder) =>
        {
            Debug.WriteLine($"{Uid} - {this.GetType().Name} - Rendered");
            _renderPending = false;
            this.BuildRenderTree(builder);
        };
    }

    public void Attach(RenderHandle renderHandle)
    {
        _renderHandle = renderHandle;
        Debug.WriteLine($"{Uid.ShortGuid()} - {this.GetType().Name} - Attached");
    }

    public virtual Task SetParametersAsync(ParameterView parameters)
    {
        Debug.WriteLine($"{Uid.ShortGuid()} - {this.GetType().Name} - Set Parameters");
        parameters.SetParameterProperties(this);
        this.StateHasChanged();
        return Task.CompletedTask;
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) { }

    public void StateHasChanged()
    {
        if (_renderPending)
        {
            Debug.WriteLine($"{Uid.ShortGuid()} - {this.GetType().Name} - Render Requested, but one is already Queued");
            return;
        }

        Debug.WriteLine($"{Uid.ShortGuid()} - {this.GetType().Name} - Render Queued");
        _renderPending = true;
        _renderHandle.Render(_content);
    }
}
