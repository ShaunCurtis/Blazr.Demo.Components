using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazr.Components;
public abstract class LeanComponentBase : IComponent
{
    protected RenderFragment renderFragment;
    private RenderHandle _renderHandle;
    protected bool initialized;
    private bool _hasNeverRendered = true;
    private bool _hasPendingQueuedRender;
    protected bool _hidden;

    [Parameter] public Boolean Hidden { get; set; } = false;

    public LeanComponentBase()
    {
        this.renderFragment = builder =>
        {
            if (!this.Hidden)
            {
                _hasPendingQueuedRender = false;
                _hasNeverRendered = false;
                this.BuildRenderTree(builder);
            }
        };
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

    private void Render()
    {
        if (_hasPendingQueuedRender)
            return;

        _hasPendingQueuedRender = true;

        try
        {
            _renderHandle.Render(renderFragment);
        }
        catch
        {
            _hasPendingQueuedRender = false;
            throw;
        }
    }

    protected void StateHasChanged()
        => _renderHandle.Dispatcher.InvokeAsync(Render);
}
