using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazr.Components;

public abstract class BlazorBaseComponent 
{
    private RenderHandle _renderHandle;
    private RenderFragment _content;
    private bool _renderPending;
    private bool _hasNotInitialized = true;
    private bool _hasNeverRendered = true;
    private bool _hasCalledOnAfterRender;

    /// <summary>
    /// Frame/Layout/Wrapper Content that will be render if set
    /// </summary>
    protected virtual RenderFragment? Frame { get; set; }

    /// <summary>
    /// Razor Compiled content of BuildRenderTree.
    /// </summary>
    protected RenderFragment Body { get; init; }

    /// <summary>
    /// Unique Id that can be used to identifiy this instance of the component
    /// </summary>
    public Guid Uid { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Readonly property providing initialization state of the component
    /// </summary>
    protected bool Initialized => !_hasNotInitialized;


    /// <summary>
    /// The current render state of the component
    /// </summary>
    public ComponentState State {
        get {
            if (_renderPending)
                return ComponentState.Rendering;

            if(_hasNeverRendered)
                return ComponentState.Initialized;

            return ComponentState.Rendered;
        } 
    }

    public BlazorBaseComponent()
    {
        this.Body = (builder) => this.BuildRenderTree(builder);

        _content = (builder) =>
        {
            _renderPending = false;
            _hasNeverRendered = false;
            if (Frame is not null)
                Frame.Invoke(builder);
            else
                BuildRenderTree(builder);
        };
    }

    public void Attach(RenderHandle renderHandle)
        => _renderHandle = renderHandle;

    /// <summary>
    /// Renders the component to the supplied <see cref="RenderTreeBuilder"/>.
    /// </summary>
    /// <param name="builder">A <see cref="RenderTreeBuilder"/> that will receive the render output.</param>
    protected abstract void BuildRenderTree(RenderTreeBuilder builder);

    /// <summary>
    /// Calls StateHasChanged and ensures it is applied immediately
    /// by yielding and giving the Renderer thread time to run.
    /// </summary>
    /// <returns></returns>
    public async Task Rendersync()
    {
        this.StateHasChanged();
        await Task.Yield();
    }

    public void StateHasChanged()
    {
        if (_renderPending)
            return;

        var shouldRender = _hasNeverRendered || this.ShouldRender() || _renderHandle.IsRenderingOnMetadataUpdate;

        if (shouldRender)
        {
            _renderPending = true;
            _renderHandle.Render(_content);
        }
    }

    protected virtual bool ShouldRender() => true;

    protected Task InvokeAsync(Action workItem)
        => _renderHandle.Dispatcher.InvokeAsync(workItem);

    protected Task InvokeAsync(Func<Task> workItem)
        => _renderHandle.Dispatcher.InvokeAsync(workItem);
}