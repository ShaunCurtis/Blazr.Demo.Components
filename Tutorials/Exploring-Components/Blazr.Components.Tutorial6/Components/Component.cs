using Blazr.ComponentStarter.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using System.Diagnostics;

namespace Blazr.Components;

public class Component : RazorBase, IComponent, IHandleEvent
{
    private Guid Uid = Guid.NewGuid();
    private RenderHandle _renderHandle;
    private RenderFragment _content;
    private bool _renderPending;
    private bool _isInitialized;

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

    public virtual async Task SetParametersAsync(ParameterView parameters)
    {
        Debug.WriteLine($"{Uid.ShortGuid()} - {this.GetType().Name} - Set Parameters");
        parameters.SetParameterProperties(this);
        await this.ParametersSetAsync();
    }

    protected async Task ParametersSetAsync()
    {
        Task? initTask = null;
        var hasRenderedOnYield = false;

        // If this is the initial call then we need to run the OnInitialized methods
        if (!_isInitialized)
        {
            this.OnInitialized();
            initTask = this.OnInitializedAsync();
            hasRenderedOnYield = await this.CheckIfShouldRunStateHasChanged(initTask);
            _isInitialized = true;
        }

        this.OnParametersSet();
        var task = this.OnParametersSetAsync();

        // check if we need to do the render on Yield i.e.
        //  - this is not the initial run or
        //  - OnInitializedAsync did not yield
        var shouldRenderOnYield = initTask is null || !hasRenderedOnYield;

        if (shouldRenderOnYield)
            await this.CheckIfShouldRunStateHasChanged(task);
        else
            await task;

        // run the final state has changed to update the UI.
        this.StateHasChanged();
    }

    protected virtual void OnInitialized()
    { }

    protected virtual Task OnInitializedAsync()
        => Task.CompletedTask;

    protected virtual void OnParametersSet()
    { }

    protected virtual Task OnParametersSetAsync()
        => Task.CompletedTask;

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

    async Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem item, object? obj)
    {
        var uiTask = item.InvokeAsync(obj);

        await this.CheckIfShouldRunStateHasChanged(uiTask);

        this.StateHasChanged();
    }

    protected async Task<bool> CheckIfShouldRunStateHasChanged(Task task)
    {
        var isCompleted = task.IsCompleted || task.IsCanceled;

        if (!isCompleted)
        {
            this.StateHasChanged();
            await task;
            return true;
        }

        return false;
    }
}
