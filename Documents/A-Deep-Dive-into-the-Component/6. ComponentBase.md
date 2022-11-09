# ComponentBase

`ComponentBase` is the "standard" out-of-the-box Blazor implementation of `IComponent`.  All *.razor* files by default inherit from it.  It's important to understand that it's just one implementation of the `IComponent` interface.  It doesn't define a component.  `OnInitialized` is not a component lifecycle method, it's a `ComponentBase` lifecycle method.

## ComponentBase Lifecycle and Events

There are articles galore regurgitating the same old basic lifecycle information.  I'm not going to repeat it.  Instead I'm going to concentrate on certain often misunderstood aspects of the lifecycle: there's more to the lifecycle that just the initial component load covered in most of the articles.

We need to consider five types of event:
1. Instantiation of the class
2. Initialization of the component
3. Component parameter changes
4. Component events
5. Component disposal

There are seven exposed Events/Methods and their async equivalents:
1. `SetParametersAsync`
2. `OnInitialized` and `OnInitializedAsync`
3. `OnParametersSet` and `OnParametersSetAsync`
4. `OnAfterRender` and `OnAfterRenderAsync`
5. `Dispose` - if `IDisposable` is implemented
6. `StateHasChanged`
7. `new` - often forgotten.

The standard class instantiation method builds the `RenderFragment` that `StateHasChanged` passes to the  `Renderer` to render the component.  It sets two private class variables to false and runs `BuildRenderTree`.

```csharp
public ComponentBase()
{
    _renderFragment = builder =>
    {
        _hasPendingQueuedRender = false;
        _hasNeverRendered = false;
        BuildRenderTree(builder);
    };
}
```

`SetParametersAsync` sets the properties for the submitted parameters. It only runs `RunInitAndSetParametersAsync` - and thus `OnInitialized` followed by `OnInitializedAsync` - on initialization. It always calls `CallOnParametersSetAsync`.  

Note:

1. `CallOnParametersSetAsync` waits on `OnInitializedAsync` to complete before calling `CallOnParametersSetAsync`.
2. `RunInitAndSetParametersAsync` calls `StateHasChanged` if `OnInitializedAsync` task yields before completion. 

```csharp
public virtual Task SetParametersAsync(ParameterView parameters)
{
    parameters.SetParameterProperties(this);
    if (!_initialized)
    {
        _initialized = true;
        return RunInitAndSetParametersAsync();
    }
    else return CallOnParametersSetAsync();
}

private async Task RunInitAndSetParametersAsync()
{
    OnInitialized();
    var task = OnInitializedAsync();
    if (task.Status != TaskStatus.RanToCompletion && task.Status != TaskStatus.Canceled)
    {
        StateHasChanged();
        try { await task;}
        catch { if (!task.IsCanceled) throw; }
    }
    await CallOnParametersSetAsync();

```

`CallOnParametersSetAsync` calls `OnParametersSet` followed by `OnParametersSetAsync`, and finally `StateHasChanged`.  If the `OnParametersSetAsync()` task yields `CallStateHasChangedOnAsyncCompletion` awaits the task and re-runs `StateHasChanged`. 

```csharp
private Task CallOnParametersSetAsync()
{
    OnParametersSet();
    var task = OnParametersSetAsync();
    var shouldAwaitTask = task.Status != TaskStatus.RanToCompletion &&
        task.Status != TaskStatus.Canceled;

    StateHasChanged();

    return shouldAwaitTask ?
        CallStateHasChangedOnAsyncCompletion(task) :
        Task.CompletedTask;
}

private async Task CallStateHasChangedOnAsyncCompletion(Task task)
{
    try { await task; }
    catch 
    {
        if (task.IsCanceled) return;
        throw;
    }
    StateHasChanged();
}
```

Lets look at `StateHasChanged`.  If a render is pending i.e. the renderer hasn't got round to running the queued render request, it closes - whatever changes have been made will be captured in the queued render.  If not, it sets the  `_hasPendingQueuedRender` class flag and calls the Render method on the `RenderHandle`.  This queues `_renderFragement` onto the `Renderer` `RenderQueue`.  When the queue runs `_renderFragment` - see above - it sets the two class flags to false and runs `BuildRenderTree`.

```csharp
protected void StateHasChanged()
{
    if (_hasPendingQueuedRender) return;
    if (_hasNeverRendered || ShouldRender())
    {
        _hasPendingQueuedRender = true;
        try { _renderHandle.Render(_renderFragment);}
        catch {
            _hasPendingQueuedRender = false;
            throw;
        }
    }
}
```

`StateHasChanged` must be run on the UI thread.  When called internally that will always be the case.  However, when wiring up external event handlers that my not be so.  You need to implement these like this:

```csharp
private void OnExternalEvent(object? sender, EventArgs e)
    => this.InvokeAsync(StateHasChanged);
```

`InvokeAsync` is a `ComponentBase` method that invokes the supplied action on the `Dispatcher` provided by the `RenderHandle`.


### And then what no one covers.  

Components receive UI events from the Renderer.  What happens is dictated by two interfaces that components can implement:

 - `IHandleEvent` defines a single method - `Task HandleEventAsync(EventCallbackWorkItem callback, object? arg)` When implemented, the Renderer passes all UI generated events to the handler.  When not, it invokes the method directly on the component.

 - `IHandleAfterRender` defines a single method - `OnAfterRenderAsync()` which handles the after render process.  If nothing is defined then there is no process.

`ComponentBase` implements both both interfaces.

Some key points to note:

1. `OnInitialized` and `OnInitializedAsync` are only during initialization.  `OnInitialized` is run first.  If, and only if, `OnInitializedAsync` yields back to the internal calling method `RunInitAndSetParametersAsync`, `StateHasChanged` get called, providing the opportunity to provide "Loading" information to the user.  `OnInitializedAsync` completes before `OnParametersSet` and `OnParametersSetAsync` are called.

2. `OnParametersSet` and `OnParametersSetAsync` are called whenever the parent component renders and the renderer detects changes to the parameter set for the component.  Any code that needs to respond to parameter changes needs to live here. `OnParametersSet` is run first.  Note that if `OnParametersSetAsync` yields, `StateHasChanged` is run after the yield, providing the opportunity to provide "Loading" information to the user.

3. `StateHasChanged` is called after the `OnParametersSet{async}` methods complete to render the component.

4. `OnAfterRender` and `OnAfterRenderAsync` occur at the end of all four events.  `firstRender` is only true on component initialization.  Note that any changes made here to parameters won't get applied to display values until the component re-renders.

5. `StateHasChanged` is called during the initialization process if the conditions stated above are met, after the `OnParametersSet` processes, and any event callback.  Don't call it explicitly during the render or parameter set process unless you need to.  If you do call it you are probably doing something wrong.

## Navigating to Self

Consider a route component: `/WeatherDisplay/{Id:int}` which has forward and back buttons to navigate to the last and next WeatherForecast record.  The record is loaded in `OnInitializedAsync`

Go to `WeatherDisplay/1` and click the forward button.  This calls `NavigationManager.NavigateYo("/WeatherDisplay/2"):`.

Nothing happens.  Record 2 is not loaded.

This is a classic example where you are expecting the routed component to react like a web page.  You are navigating to yourself, but the route hasn't changed and the router provides the saem route to `RouteView` in `App`.  The renderer renders it.  The component os the same, just the `Id` parameter has changed so it calls `SetParametersAsync` which calls `OnParametersSet{Async}`.  There's no new component created so no call to `OnInitializedAsync{Async}`.  In such designs, you need to track the Id parameter in `OnParametersSet{Async}` and load the record when it changes, make deeper changes to `ComponentBase` or use a different base component.

