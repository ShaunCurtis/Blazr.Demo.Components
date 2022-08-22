using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components;
using System.Diagnostics;

namespace Blazr.Components;
public abstract class LeanComponentBase : IComponent
{
    private readonly Guid _id = Guid.NewGuid();
    private readonly string _name;
    protected RenderFragment renderFragment;
    private RenderHandle _renderHandle;
    protected bool initialized;
    private bool _hasNeverRendered = true;
    private bool _hasPendingQueuedRender;

    [Parameter] public Boolean Hidden { get; set; } = false;

    public LeanComponentBase()
    {
        this.renderFragment = builder =>
        {
            if (!this.Hidden)
            {
                Debug.WriteLine($"RENDER-EVENT =>{_name} instance {_id} rendered at {DateTime.Now.ToLongTimeString()}");
                _hasPendingQueuedRender = false;
                _hasNeverRendered = false;
                this.BuildRenderTree(builder);
            }
        };
        _name = this.GetType().Name;
        Debug.WriteLine($"COMPONENT => {_name} instance {_id} created at {DateTime.Now.ToLongTimeString()}");
    }

    void IComponent.Attach(RenderHandle renderHandle)
        => _renderHandle = renderHandle;

    public virtual async Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        var shouldRender = this.ShouldRenderOnParameterChange(initialized);

        if (_hasNeverRendered || shouldRender)
        {
            await this.OnParameterChangeAsync(!initialized);
            this.Render();
        }

        this.initialized = true;
    }

    protected virtual void BuildRenderTree(RenderTreeBuilder builder) { }

    protected virtual ValueTask OnParameterChangeAsync(bool firstRender)
        => ValueTask.CompletedTask;

    protected virtual bool ShouldRenderOnParameterChange(bool initialized)
        => true;

    protected void Render()
    {
        if (_hasPendingQueuedRender)
            return;

        _hasPendingQueuedRender = true;
        _renderHandle.Render(renderFragment);
    }

    protected void StateHasChanged()
        => _renderHandle.Dispatcher.InvokeAsync(Render);
}
