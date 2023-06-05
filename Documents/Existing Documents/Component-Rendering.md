# Exploring Component Rendering

>> Under Construction

Blazor ships with a single developer "Component".  Add a Razor file and it inherits from it by default - no `@inherits` required.

`ComponentBase` rules the Blazor UI world.  You don't have to use it, but probably 99.x% of all developer built components either inherit directly or indirectly from it.

This article explores the render process of components that inherit from `ComponentBase`.  You can find another article here that looks at building alternative leaner and meaner base components.

The context for this article is the out-of-the-box `Counter` page.  We examine how, why and when rendering occurs.

## The RenderFragment

Before diving into renderering, you need to understadn what a `RenderFragment` is.  

A `RenderFragment` is a delegate defined as:

```csharp
public delegate void RenderFragment(RenderTreeBuilder builder);
```

This method can be assigned to a `RenderFragment` instance.

```csharp
@using Microsoft.AspNetCore.Components.Rendering;

@Content

@code {
    private RenderFragment Content => RenderMe;

    private void RenderMe(RenderTreeBuilder builder )
    {
        builder.AddMarkupContent(0, "<div>Hello World</div>");
    }
}
```

You will often see this method of declaring a fragment:

```csharp
@using Microsoft.AspNetCore.Components.Rendering;

@Content

@code {
    private RenderFragment Content = (builder) =>
    {
        builder.AddMarkupContent(0, "<div>Hello World</div>");
    };
}
```

And in Razor this works (`__builder` is the built in `RenderTreeBuilder` provided by the Razor compiler):

```csharp
@code {
    private RenderFragment Content = (__builder) =>
    {
        <div>Hello World</div>
    };
}
```

The `ComponentBase` render fragment is defined in the Ctor.  This caches the fragment for performance.
`_hasPendingQueuedRender` and `_hasNeverRendered` are booleans to control the render process.

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

## What is a Render?

The Renderer has a queue called the RenderQueue which it services whenever it gets processor time to do so.  

It:

1. Runs any items on the queue.
2. Checks for changes to it's DOM.
3. Transmits changes to the browser DOM which updates the UI.

The queue consists of a list of `RenderFragment` objects.

Here's `StateHasChanged` as implemented in `ComponentBase`.

`_hasPendingQueuedRender` prevents multiple copies of the component's render fragment being placed on the RenderQueue.  If one is already queued, `_hasPendingQueuedRender` will be `true`, and the queued fragment will handle any component stste changes.

`(_hasNeverRendered || ShouldRender() || _renderHandle.IsRenderingOnMetadataUpdate` ensures the component renders at least once.

`_renderHandle.Render(_renderFragment)` places the component render fragment `_renderFragment` on the Renderer's queue.

```csharp
    protected void StateHasChanged()
    {
        if (_hasPendingQueuedRender)
            return;

        if (_hasNeverRendered || ShouldRender() || _renderHandle.IsRenderingOnMetadataUpdate)
        {
            _hasPendingQueuedRender = true;

            try
            {
                _renderHandle.Render(_renderFragment);
            }
            catch
            {
                _hasPendingQueuedRender = false;
                throw;
            }
        }
    }
```

`ShouldRender` is an overridable method used to control if rendering takes place.

## What Triggers A Render

As you can see a render is triggered on `CompinentBase` by calling `StateHasChanged`.  

So who calls `StateHasChanged`?

1. `SetParametersAsync`.
2. The `IHandleEvent` handler.
3. You manually.

### SetParametersAsync

`SetParametersAsync` calls `StateHasChanged` once or twice.  If the `OnInitialized{Async}/OnParametersSet{Async}` lifecycle methods run synchronously then it is called at the end of the process.  If the methods yields, it's called on the first yield and then again at the end of the process.

### ComnmponentBase's IHandleEvent

`ComponentBase` implements a `IHandleEvent`.  This is called by the Renderer to handle any UI events such `OnClick` and `OnChange`.

It looks like this.  You can see the same pattern as above.  It calls `StateHasChanged` either oncve or twice, depending on whether `callback` yields.

```csharp
Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg)
{
    var task = callback.InvokeAsync(arg);
    var shouldAwaitTask = task.Status != TaskStatus.RanToCompletion &&
        task.Status != TaskStatus.Canceled;

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
    catch 
    {
        if (task.IsCanceled)
            return;

        throw;
    }
    StateHasChanged();
}
```



