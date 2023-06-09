# The Rest

`ComponentBase` implements various other functionality.

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
    public void StateHasChanged()
    {
        if (_renderPending)
        {
            Debug.WriteLine($"{Uid.ShortGuid()} - {this.GetType().Name} - Render Requested, but one is already Queued");
            return;
        }

        var shouldRender = _hasNeverRendered || ShouldRender() || _renderHandle.IsRenderingOnMetadataUpdate;

        if (shouldRender)
        {
            Debug.WriteLine($"{Uid.ShortGuid()} - {this.GetType().Name} - Render Queued");
            _renderPending = true;
            _renderHandle.Render(_content);
        }
    }
```

## InvokeAsync

We can add render specific code to event handlers that we pass as callbacks or register with events.  In both these instances, the code can potentially be run on a different thread to the UI context.

The `RenderHandle` provides a reference to the Dispatcher on the UI thread that we can use to run the UI specific code.

We can provide two methods:

```csharp
    protected Task InvokeAsync(Action workItem)
    => _renderHandle.Dispatcher.InvokeAsync(workItem);

    protected Task InvokeAsync(Func<Task> workItem)
        => _renderHandle.Dispatcher.InvokeAsync(workItem);
```

So where we need to we can:

```csharp
await this.InvokeAsync(StateHasChanged);
```

### AfterRender Methods

By default the Renderer doesn't inform the component that it has completed rendering it.

For most components this is fine.  There's no need to do anything after a component has rendered.  However, where the component to needs to interact with JSInterop, this can only happen after the component has rendered.

To receive notifications, the component needs to implement the `IHandleAfterRender` interface, and it's single method `OnAfterRenderAsync`.

```csharp
public class Component : RazorBase, IComponent, IHandleEvent, IHandleAfterRender
```

A state variable to capture if this is the first after render.

```csharp
private bool _hasCalledOnAfterRender;
```

And two methods for inheriting classes to implement code:
  
```csharp
protected virtual void OnAfterRender(bool firstRender)
{ }
```

```csharp
protected virtual Task OnAfterRenderAsync(bool firstRender)
    => Task.CompletedTask;
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
