using System.Diagnostics;

namespace Blazr.Components;

public abstract class MinimalDebugBase : IComponent, IDisposable
{
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected RenderHandle renderHandle;
    private bool _renderPending;
    private RenderFragment _componentFragment;
    private Guid Uid = Guid.NewGuid();
    private string ClassName => this.GetType().Name;
    protected virtual bool hide { get; set; }

    public MinimalDebugBase()
    {
        Debug.WriteLine($"{ClassName} - instance : {Uid.ToString()} Ctor at {DateTime.Now.ToLongTimeString()}");
        _componentFragment = (builder) =>
        {
            _renderPending = false;
            if (!this.hide)
            {
                Debug.WriteLine($"{ClassName} - instance : {Uid.ToString()} rendered at {DateTime.Now.ToLongTimeString()}");
                BuildRenderTree(builder);
            }
        };
    }

    public void Attach(RenderHandle handle)
        => renderHandle = handle;

    public Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        Debug.WriteLine($"{ClassName} - instance : {Uid.ToString()} parameters set at {DateTime.Now.ToLongTimeString()}");
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

    public void Dispose()
    {
        Debug.WriteLine($"{ClassName} - instance : {Uid.ToString()} disposed at {DateTime.Now.ToLongTimeString()}");
    }
}
