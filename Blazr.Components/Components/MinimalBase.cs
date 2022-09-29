using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Mvc;

namespace Blazr.Components;

public abstract class MinimalBase : IComponent
{
    protected RenderHandle renderHandle;
    private bool _renderPending;
    private RenderFragment _componentFragment;
    protected virtual bool shouldHide { get; set; }
    
    public MinimalBase()
    {
        _componentFragment = (builder) =>
        {
            _renderPending = false;
            if (!this.shouldHide)
                BuildRenderTree(builder);
        };
    }

    public void Attach(RenderHandle handle)
        => renderHandle = handle;

    public Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        this.RequestRender();
        return Task.CompletedTask;
    }

    protected abstract void BuildRenderTree(RenderTreeBuilder builder);

    protected void RequestRender()
    {
        if (!_renderPending)
        {
            _renderPending = true;
            this.renderHandle.Render(_componentFragment);
        }
    }
}
