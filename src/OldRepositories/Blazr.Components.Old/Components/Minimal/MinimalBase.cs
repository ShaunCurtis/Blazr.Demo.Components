namespace Blazr.Components;

public abstract class MinimalBase : IComponent
{
    protected RenderHandle renderHandle;
    private bool _renderPending;
    private RenderFragment _componentFragment;
    protected virtual bool hide { get; set; }

    public MinimalBase()
    {
        _componentFragment = (builder) =>
        {
            _renderPending = false;
            if (!hide)
                BuildRenderTree(builder);
        };
    }

    public void Attach(RenderHandle handle)
        => renderHandle = handle;

    public Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        RequestRender();
        return Task.CompletedTask;
    }

    protected abstract void BuildRenderTree(RenderTreeBuilder builder);

    protected void RequestRender()
    {
        if (!_renderPending)
        {
            _renderPending = true;
            renderHandle.Render(_componentFragment);
        }
    }
}
