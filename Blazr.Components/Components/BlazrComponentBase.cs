namespace Blazr.Components;

public abstract class BlazrComponentBase : IComponent
{
    protected RenderHandle renderHandle;
    private bool _renderPending;
    private RenderFragment _componentFragment;
    private bool _firstRender = true;
    protected bool shouldRender = true;

    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public bool Hidden { get; set; } = false;

    public BlazrComponentBase()
    {
        _componentFragment = (builder) =>
        {
            _renderPending = false;
            if (!this.Hidden && this.shouldRender)
                BuildComponent(builder);
        };
    }

    public void Attach(RenderHandle handle)
        => renderHandle = handle;

    public async Task SetParametersAsync(ParameterView parameters)
    {
        this.shouldRender = ShouldRenderOnParameterChange(parameters, _firstRender);
        await OnParametersChangedAsync(_firstRender);
        this.RequestRender();
    }

    protected virtual Task OnParametersChangedAsync(bool firstRender)
        => Task.CompletedTask;

    protected virtual bool ShouldRenderOnParameterChange(ParameterView parameters, bool firstRender)
    {
        parameters.SetParameterProperties(this);
        return true;
    }

    protected virtual void BuildComponent(RenderTreeBuilder builder)
        => BuildRenderTree(builder);

    protected abstract void BuildRenderTree(RenderTreeBuilder builder);

    protected void RequestRender()
    {
        if (!_renderPending && this.shouldRender)
        {
            _renderPending = true;
            this.renderHandle.Render(_componentFragment);
        }
    }

    protected void StateHasChanged()
        => renderHandle.Dispatcher.InvokeAsync(RequestRender);
}
