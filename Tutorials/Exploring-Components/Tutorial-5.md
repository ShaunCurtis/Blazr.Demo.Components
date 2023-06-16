# UI Events

Update `BasicComponent` to the following:

```csharp
@using System.Diagnostics;
@inherits Component

<div hidden="@_hidden" class="alert alert-primary m-2">
    @_message
</div>

<div class="m-2">
    <button class="btn btn-primary" @onclick=this.UpdateMessage>Update</button>

</div>

@code {
    private string? _message;
    private bool _hidden => _message is null || _message == string.Empty;

    public void UpdateMessage()
    {
        _message = $"Updated at {DateTime.Now.ToLongTimeString()}";
        Debug.WriteLine(_message);
    }
}
```

And `Index` back to:

```csharp
@page "/"

<PageTitle>Index</PageTitle>

<h1>Hello, world!</h1>

<BasicComponent />

@code {
}
```

When you run this code and click on the button, the UI doesn't update.  If you check the output you will find that `_message` has updated.  Why?

By default the Renderer calls the hooked up handler for an event directly.  This simply updates the value.  It doesn't magically trigger a UI update.  To do so we need to call `StateHasChanged`.

```csharp
    public void UpdateMessage()
    {
        _message = $"Updated at {DateTime.Now.ToLongTimeString()}";
        Debug.WriteLine(_message);
        this.StateHasChanged();
    }
``` 

If we look at `Index` from the previous tutorial we see a button getting clicked and the UI updating.

What is the difference?

Components can implement `IHandleEvent`. which looks like this:

```csharp
public interface IHandleEvent
{
    Task HandleEventAsync(EventCallbackWorkItem item, object? arg);
}
```

We can implement the interface on `Component`:

```csharp
public class Component : RazorBase, IComponent, IHandleEvent
{
//...
    async Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem item, object? obj)
    {
        await item.InvokeAsync(obj);

        this.StateHasChanged();
    }
}
```

And `BasicComponent` drops back to:

```csharp
    public void UpdateMessage()
    {
        _message = $"Updated at {DateTime.Now.ToLongTimeString()}";
        Debug.WriteLine(_message);
    }
```

It now works as expected.

### Async Behaviour

Update `BasicComponent` to demonstrate asynchronous behaviour by adding a yield into the handler.

```csharp
    public async void UpdateMessage()
    {
        await Task.Yield();
        _message = $"Updated at {DateTime.Now.ToLongTimeString()}";
        Debug.WriteLine(_message);
    }
```

If you now run this code nothing happens, and is run run it again it shows the last update - check the debug output for the current value.  It's one step behind.

This is a very important lesson to learn.  The pattern `async void Method` is an absolute **NONO** in UI event handlers.  

What happens is the UI handler is expecting the component handler to return a `Task` and will await it before calling `StateHasChanged`.  If you return a `void` then it has nothing to await, and will run to completion as soon as the component handler yields.  Synchronous code doesn't yield, so there no issue.

Update the code to the following: 

```csharp
//...
<div class="m-2">
    <button class="btn btn-primary" @onclick=this.UpdateMessageAsync>Update</button>
</div>

@code {
    public async Task UpdateMessageAsync()
    {
        await Task.Yield();
        _message = $"Updated at {DateTime.Now.ToLongTimeString()}";
        Debug.WriteLine(_message);
    }
}
```

This now works and demonstrates the important async naming convention used in C# code.  If you code is Task based, add `Async` to the end of the method name.

For the final scenario in this section update `UpdateMessageAsync` to the following.  We are now expecting our async code to take a while to complete [emulated using `Task.Delay`] so add somne status text to `_message`.  

```csharp
@code {
    public async Task UpdateMessageAsync()
    {
        _message = "Getting the Update";
        Debug.WriteLine(_message);
        await Task.Delay(2000);
        _message = $"Updated at {DateTime.Now.ToLongTimeString()}";
        Debug.WriteLine(_message);
    }
}
``` 

If you run this no status text appears, though it is set as evidenced by the debug log.

The reason is we only have one render, at the completion of the method.  We can implement a double render on yield [as `ComponentState` does] like this in `Component`:

```csharp

    async Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem item, object? obj)
    {
        var uiTask = item.InvokeAsync(obj);

        var isCompleted = uiTask.IsCompleted || uiTask.IsCanceled;

        if (!isCompleted)
        {
            Debug.WriteLine($"{Uid.ShortGuid()} - {this.GetType().Name} - Event Handler Yielded, Render Queued");
            this.StateHasChanged();
            await uiTask;
        }

        this.StateHasChanged();
    }
```

With any handler that doesn't yield, `isCompleted` will be true at this point `if (!isCompleted)`, so there's only one yield.

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
