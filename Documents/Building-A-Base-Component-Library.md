# Building A Base Document Library

In the first part of this article I'll show you how you can add extra functionality to `ComponentBase` to address some common problems.

In the second part I'll show you how you can step outside the `ComponentBase` straightjacket and use a library of base components based on the requirement.

## ReplicaComponentBase

`ComponentBase` is a closed book [as it should be].  It was written in the early days of Blazor and hasn't changed since.

To add fuctionality and make some enhancements, we need to create a *black-box* replica.  This is `ReplicaComponentBase`.  You'll find the complete class code in tha appendix - it's too long to show here.

It's a *black box* replacement: it's not an exact copy.  Some of the internal lifecycle code has been refactored a little to make the intent clearer.

## An Enhanced ComponentBase

### Frame/Layout/Wrapper

One of the major issues with `ComponentBase` is you can't use it as a Frame/Layout/Wrapper component.

An example:

```csharp
// Wrapper.razor
<div class="whatever">
   the content of the child component
<div>
```

```csharp
// Index
@inherits Wrapper

// All this content is rendered inside the wrapper content
<h1>Hello Blazor</h1> 
```

This is trivial, and can be solved differently.  However, I have base forms/pages where only the inner content changes.  In a view form the implementation content looks like this.  `UIViewerFormBase` is the wrapper that contains both the boilerplate code and the content wrapper markup.

```csharp
﻿@namespace Blazr.App.UI
@inherits UIViewerFormBase<Customer, CustomerEntityService>

<div class="row">

    <div class="col-12 col-lg-6 mb-2">
        <BlazrTextViewControl Label="Name" Value="@this.Presenter.Item.CustomerName" />
    </div>

    <div class="col-12 col-lg-6 mb-2">
        <BlazrTextViewControl Label="Unique Id" Value="@this.Presenter.Item.Uid" />
    </div>

</div>
```

#### Implementation

We need two `RenderFragment` properties.

1. `Frame` is where we'll code the frame content in the child component.
2. `Body` is mapped to `BuildRenderTree`.  It's readonly and set in the constructor.


```csharp
    protected virtual RenderFragment? Frame { get; set; }
    protected RenderFragment Body { get; init; }
```

We can now refactor the constructor.

1. `Body` is mapped to BuildRenderTree.  This is the most efficient way to do this.  No expensive lambda expressions to construct on each call.

2. `_content` uses the content in `Frame` if it's not null.  Otherwise it uses the Razor compiled content in `BuildRenderTree`.

```csharp
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
```

#### Demo

The Demo `Wrapper` component.  Note the wrapper is defined in the `Frame` render fragment, and uses the Razor build in `__builder` RenderTreeBuilder instance.

```
@inherits NewComponentBase

@*Code Here is redundant*@

@code {
    protected override RenderFragment Frame => (__builder) => 
    {
        <h2 class="text-primary">Welcome To Blazor</h2>
        <div class="border border-1 border-primary rounded-3 bg-light p-2">
            @this.Body
        </div>
    };
}
```

And `Index` inheriting from `Wrapper`.

```csharp
@page "/"
@page "/WrapperDemo"

@inherits Wrapper

<PageTitle>Index</PageTitle>

<h1>Hello, world!</h1>

Welcome to your new app.

<SurveyPrompt />

```

What you get is:

![Wrapper Demo](../assets/BlazrComponentBase/Wrapper-Demo.png)



## Appendix

### ReplicaComponentBase

```csharp
public class ReplicaComponentBase : IComponent, IHandleEvent, IHandleAfterRender
{
    private RenderHandle _renderHandle;
    private RenderFragment _content;
    private bool _renderPending;
    private bool _hasNotInitialized = true;
    private bool _hasNeverRendered = true;
    private bool _hasCalledOnAfterRender;

    public ReplicaComponentBase()
    {

        _content = (builder) =>
        {
            _renderPending = false;
            _hasNeverRendered = false;
            BuildRenderTree(builder);
        };
    }

    public void Attach(RenderHandle renderHandle)
        => _renderHandle = renderHandle;

    public virtual async Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        await this.ParametersSetAsync();
    }

    protected async Task ParametersSetAsync()
    {
        Task? initTask = null;
        var hasRenderedOnYield = false;

        // If this is the initial call then we need to run the OnInitialized methods
        if (_hasNotInitialized)
        {
            this.OnInitialized();
            initTask = this.OnInitializedAsync();
            hasRenderedOnYield = await this.CheckIfShouldRunStateHasChanged(initTask);
            _hasNotInitialized = false;
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

    protected virtual void OnInitialized() { }

    protected virtual Task OnInitializedAsync() => Task.CompletedTask;

    protected virtual void OnParametersSet() { }

    protected virtual Task OnParametersSetAsync() => Task.CompletedTask;

    protected virtual void OnAfterRender(bool firstRender) { }

    protected virtual Task OnAfterRenderAsync(bool firstRender) => Task.CompletedTask;

    protected virtual void BuildRenderTree(RenderTreeBuilder builder) { }

    protected virtual bool ShouldRender() => true;

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

    async Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem item, object? obj)
    {
        var uiTask = item.InvokeAsync(obj);

        await this.CheckIfShouldRunStateHasChanged(uiTask);

        this.StateHasChanged();
    }

    Task IHandleAfterRender.OnAfterRenderAsync()
    {
        var firstRender = !_hasCalledOnAfterRender;
        _hasCalledOnAfterRender = true;

        OnAfterRender(firstRender);

        return OnAfterRenderAsync(firstRender);
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

    protected Task InvokeAsync(Action workItem)
        => _renderHandle.Dispatcher.InvokeAsync(workItem);

    protected Task InvokeAsync(Func<Task> workItem)
        => _renderHandle.Dispatcher.InvokeAsync(workItem);
}
```
