# The Render and UI Events

A detailed description of thr Render process is beyond the scope of this article, however you need a basic grasp of the concepts to understand the rendering process.

To quote the MS Documentation:

>The `Renderer` provides mechanisms:
>1. For rendering hierarchies of `IComponent` instances;
>2. Dispatching events to them;
>3. Notifying when the user interface is being updated.

## The Renderer and the Render Tree

The `Renderer` and `RenderTree` reside in the Client Application in WASM and in the SignalR Hub Session in Server, i.e. one per connected Client Application.

The UI - defined by html code in the DOM [Document Object Model] - is represented in the application as a `RenderTree` and managed by a `Renderer`. Think of the `RenderTree` as a tree with one or more components attached to each branch. Each component is a C# class  implementing the `IComponent` interface.  

The `Renderer` maintains a `RenderQueue` of `RenderFragments`.  Components submit `RenderFragments` to the queue.  The Renderer services this queue and invokes any queued render fragements.  

The `Renderer` has a diffing process that detects changes in the DOM caused by `RenderTree` updates.  It passes these changes to the client code to implement in the Browser DOM and update the displayed page.

The diagram below is a visual representation of the render tree for the out-of-the-box Blazor template.

![Root Render Tree](https://shauncurtis.github.io/articles/assets/Blazor-Components/Root-Render-Tree.png)

## UI Events

The Render manages UI events, feeding registered events back from the DOM into the RenderTree component instances that defined them.  

Hidden away are two important interfaces that dictate how this happens.

 - `IHandleEvent`
 - `IHandleAfterRender`

### IHandleEvent

When the Renderer receives a registered UI event it either:
1. Calls `IHandleEvent.HandleEventAsync` if the component implements `IHandleEvent`.  
2. Calls the handler directly.

`IHandleEvent` defines a single method.

```csharp
Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg);
```

`ComponentBase` implements the interface, with the two step call to `StateHasChanged`.  Ensure you're fundimental understandoing of this piece of code.  It will save you a lot of time in the future.

```csharp
Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg)
{
    var task = callback.InvokeAsync(arg);
    var shouldAwaitTask = task.Status != TaskStatus.RanToCompletion &&
        task.Status != TaskStatus.Canceled;

    // After each event, we synchronously re-render (unless !ShouldRender())
    // This just saves the developer the trouble of putting "StateHasChanged();"
    // at the end of every event callback.
    StateHasChanged();

    return shouldAwaitTask ?
        CallStateHasChangedOnAsyncCompletion(task) :
        Task.CompletedTask;
}

private async Task CallStateHasChangedOnAsyncCompletion(Task task)
{
    try
    {
        await task;
    }
    catch // avoiding exception filters for AOT runtime support
    {
        // Ignore exceptions from task cancellations, but don't bother issuing a state change.
        if (task.IsCanceled)
            return;
        throw;
    }
    StateHasChanged();
}
```

If `IHandleEvent` is not implemented it simply calls the handler directly.

```csharp
Task async IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg)
   => await callback.InvokeAsync(arg);
```

### IHandleAfterRender

When the component completes rendering the Renderer checks the compoment to see if it If a implements `IHandleAfterRender`the Renderer calls `HandleEventAsync`.  If it doesn't then the renderer doesn't track the event on the component and nothing happens.

`ComponentBase` implements the interface.

```csharp
Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg)
{
    var task = callback.InvokeAsync(arg);
    var shouldAwaitTask = task.Status != TaskStatus.RanToCompletion &&
        task.Status != TaskStatus.Canceled;

    // After each event, we synchronously re-render (unless !ShouldRender())
    // This just saves the developer the trouble of putting "StateHasChanged();"
    // at the end of every event callback.
    StateHasChanged();

    return shouldAwaitTask ?
        CallStateHasChangedOnAsyncCompletion(task) :
        Task.CompletedTask;
}
```

If `IHandleAfterRender` is not implemented then nothing happens.

```csharp
Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg)
   => return Task.CompletedTask;
```

### Void UI Events

The following code won't execute as expected in `ComponentBase`:

```csharp
void async ButtonClick(MouseEventArgs e) 
{
  await Task.Delay(2000);
  UpdateADisplayProperty();
}
```

The DisplayProperty doesn't display the current value until another `StateHasChanged` events occurs.  Why? ButtonClick doesn't return anything, so there's no `Task` for the event handler to wait on.  On the `await` yield, it runs to completion running the final `StateHasChanged` before `UpdateADisplayProperty` completes.

This is a band-aid fix - it's bad pactice, **DON'T DO IT**.

```csharp
void async ButtonClick(MouseEventArgs e) 
{
  await Task.Delay(2000);
  UpdateADisplayProperty();
  StateHasChanged();
}
```

The correct solution is:

```csharp
Task async ButtonClick(MouseEventArgs e) 
{
  await Task.Delay(2000);
  UpdateADisplayProperty();
}
```
Now the event handler has a `Task` to await and doesn't execute `StateHasChanged` until `ButtonClick` completes.

