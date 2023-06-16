# The Rest

There's other functionality that a component needs.

## ShouldRender

There are times when you want to turn off rendering i.e. stop `StateHasChanged` from queuing a render fragment.

```
protected virtual bool ShouldRender()
    => true;
```

This then requires refactoring the `StateHasChanged` code.

1. Add a `_hasNeverRendered` as the component should render for the first time even if `ShouldRender()` returns false.
2. Add a check on `IsRenderingOnMetadataUpdate` on the `RenderHandle` to render if we have had a hot reload.

```csharp
    private bool _hasNeverRendered = true;

    public void StateHasChanged()
    {
        if (_renderPending)
            return;

        var shouldRender = _hasNeverRendered || ShouldRender() || _renderHandle.IsRenderingOnMetadataUpdate;

        if (shouldRender)
        {
            _renderPending = true;
            _renderHandle.Render(_content);
        }
    }
```

## InvokeAsync

Component code can be run by event handlers we pass as callbacks or register with events.  This code can potentially be run on a different thread to the UI context.

The `RenderHandle` provides a reference to the UI Thread Dispatcher we can use to invoke the UI specific code in the correct thread context.

We implement this for both `Func` and `Action` delegates:

```csharp
    protected Task InvokeAsync(Action workItem)
    => _renderHandle.Dispatcher.InvokeAsync(workItem);

    protected Task InvokeAsync(Func<Task> workItem)
        => _renderHandle.Dispatcher.InvokeAsync(workItem);
```

So we can do this in methods we pass as handlers:

```csharp
await this.InvokeAsync(StateHasChanged);
```

### AfterRender Methods

By default, the Renderer doesn't raise any events in the component when it has completed rendering it.

For most components this is fine.  There's no need to do anything after a component has rendered: all state changes should take place prior to queuing a render.  

However, where the component needs to interact with JSInterop, this can only happen after the component has first rendered.

To receive notifications, a component implements the `IHandleAfterRender` interface, and it's single method `OnAfterRenderAsync`.

```csharp
public class Component : RazorBase, IComponent, IHandleEvent, IHandleAfterRender
```

We need a state variable to capture if this is the first "after render".

```csharp
private bool _hasCalledOnAfterRender;
```

And two virtual methods [sync and async] for inheritance:
  
```csharp
protected virtual void OnAfterRender(bool firstRender) { }

protected virtual Task OnAfterRenderAsync(bool firstRender) => Task.CompletedTask;
```

Finally the interface handler:

```csharp
Task IHandleAfterRender.OnAfterRenderAsync()
{
    var firstRender = !_hasCalledOnAfterRender;
    _hasCalledOnAfterRender = true;

    OnAfterRender(firstRender);

    return OnAfterRenderAsync(firstRender);
}
```

Tutorial List:

1. [Introduction](./Introduction.md)
2. [What is a Component?](./Tutorial-1.md)
3. [Our First Component](./Tutorial-2.md)
4. [RenderFragments](./Tutorial-3.md)
5. [Parameters](./Tutorial-4.md)
6. [UI Events](./Tutorial-5.md)
7. [Component Lifecycle Methods](./Tutorial-6.md)
8. [The Rest](./Tutorial-7.md)
9. [Summary](./Final-Summary.md)
