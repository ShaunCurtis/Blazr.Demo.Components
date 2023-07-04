# Building A Base Document Library

In the first part of this article, I'll show you how you can add extra functionality to `ComponentBase` to address the Wrapper/Frame/Layout problem.

In the second part, I'll show you how you can step outside the `ComponentBase` straightjacket and use a library of base components.  Choose the component that best fits the actual requirements of a specific component design.

## ReplicaComponentBase

`ComponentBase` is a closed book [as it should be].  It was written in the early days of Blazor and hasn't changed since.

We need a *black-box* replica to add fuctionality and make some enhancements.  This is `ReplicaComponentBase`.  You'll find the complete class code in tha appendix - it's too long to show here.

It's a functional *black box* replacement: not an exact copy.  Some of the internal lifecycle code has been refactored a little to make the intent clearer.

## Enhancing ComponentBase

### Frame/Layout/Wrapper

One major issue with `ComponentBase` is you can't use it as a Frame/Layout/Wrapper component.

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

## Building A Base Component Library

### BlazrBaseComponent

`BlazrBaseComponent` contains all the real basic boiler plate code used in all components.

It contains many of the same private variables from `ComponentBase`, and the wrapper render fragments.  It doesn't implement `IComponent`: it doesn't need to.

In Addition:

1. The `Initialized` flag has changed.  It's reversed and now `protected`, so inheriting classes can access it.
2. It has a Guid identifier.  This is useful to track instances in debugging.
3. It has a render state property that exposes the state based on the private render control class variables.  Again useful in debugging.

```csharp
public abstract class BlazorBaseComponent
{
    private RenderHandle _renderHandle;
    private RenderFragment _content;
    private bool _renderPending;
    private bool _hasNeverRendered = true;

    protected bool Initialized;

    protected virtual RenderFragment? Frame { get; set; }
    protected RenderFragment Body { get; init; }

    public Guid Uid { get; init; } = Guid.NewGuid();

    public ComponentState State
    {
        get
        {
            if (_renderPending)
                return ComponentState.Rendering;

            if (_hasNeverRendered)
                return ComponentState.Initialized;

            return ComponentState.Rendered;
        }
    }

```

The constructor implements the wrapper functionality. It's similar to the new constructor we built in the previous section.  It sets `Initialized` to true when the component first renders.

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

            this.Initialized = true;
        };
    }
```

The rest of the code is the same as that implemented in `ComponentBase` with the addition of `RenderAsync`.  This method ensures a render occurs when it's called by yielding, and giving the Renderer some UI thread time.

```csharp

    public void Attach(RenderHandle renderHandle)
        => _renderHandle = renderHandle;

    protected abstract void BuildRenderTree(RenderTreeBuilder builder);

    public async Task RenderAsync()
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
```

Note that there are no lifecycle methods or implementation of `SetParametersAsync`.  That is left to the individual library classes.

### BlazrUIBase

This is our simplest implementation.  It looks like this:

```csharp
public class BlazorUIBase : BlazorBaseComponent, IComponent
{
    public Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        this.StateHasChanged();
        return Task.CompletedTask;
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) { }
}
```

It inherits from `BlazorBaseComponent` and implements `IComponent`.  It:

1. Has a fixed `SetParametersAsync`, it's not `virtual`.
2. Has no lifecycle methods.  Simple components don't need them.
3. It doesn't implement `IHandleEvent` i.e. it has no UI event handling.  If you need any, call `StateHasChanged` manually.
4. It doesn't implement `IHandleAfterRender` i.e. it has no after render handling.  If you need it, implement it manually.

#### Demo

A dismissible Alert.  It has a single clickable button

```csharp
@inherits BlazorUIBase

@if (Message is not null)
{
    <div class="alert alert-primary alert-dismissible">
        @this.Message
        <button type="button" class="btn-close" @onclick=this.Dismiss>
        </button>
    </div>
}

@code {
    [Parameter] public string? Message { get; set; }
    [Parameter] public EventCallback<string?> MessageChanged { get; set; }

    private void Dismiss()
        => MessageChanged.InvokeAsync(null);
}
```

And a demo `Index`

```csharp
@page "/"
@page "/SimpleDemo"

<PageTitle>Index</PageTitle>

<h1>Hello, world!</h1>

<div class="m-2">
    <button class="btn btn-primary" @onclick=this.UpdateMessage>Update Alert</button>
</div>

<Alert @bind-Message=@_message />

@code {
    private string? _message;

    private void UpdateMessage()
    {
        _message = $"Clicked at {DateTime.Now.ToLongTimeString()}";
    }
}
```

There's no manual calls to `StateHasChanged`, even though it would seem that one is needed to update the alert when the close button is clicked.

`Index` inherits from `ComponentBase` so `StateHasChanged` is automatically called by the UI handler when it's triggered.

1. The Alert `Dismiss` invokes `MessageChanged` passing a `null` string.
2. The UI handler invokes the Bind handler in `Index`.
3. The Bind handler [created by the Razor Compiler] updates `_message` [to `null`].
4. The UI Handler completes and calls `StateHasChanged`.
5. `Index` renders, and Renderer detects the `Message` parameter on `Alert` has changed.
6. The Renderer calls `SetParametersAsync` on `Alert` to inform the component of the change.
7. `Alert` renders, hiding the alert.

The morale to this episode is: always test whether you actually need a call to `StateHasChanged`.

### BlazrControlBase

`BlazrControlBase` is the intermediate level component.

It:

1. Implements the `OnParametersSetAsync` lifecycle method.
2. Implements a simple, single render UI event handler.

```csharp
public abstract class BlazrControlBase : BlazorBaseComponent, IComponent, IHandleEvent
{
    public async Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        await this.OnParametersSetAsync();
        this.StateHasChanged();
    }

    protected virtual Task OnParametersSetAsync()
        => Task.CompletedTask;  

    async Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem item, object? obj)
    {
        await item.InvokeAsync(obj);
        this.StateHasChanged();
    }
}
```

#### Demo

The demo page looks just like a normal `ComponentBase` page.  That's intentional.  Note that the component has access to initialization state of the component so there's no real need for `OnInitialized`.  There's also no need for a sync version of either.  You can run sync code in `OnParametersSetAsync`.  You just need to retuen a completed Task at the end of the process.     

```csharp
@page "/Country/{Id:int}"
@inherits BlazrControlBase
<h3>Country Viewer</h3>

<div class="bg-dark text-white m-2 p-2">
    @if (_record is not null)
    {
        <pre>Id : @_record.Id </pre>
        <pre>Name : @_record.Name </pre>
    }
    else
    {
        <pre>No Record Loaded</pre>
    }
</div>

@code {
    [Parameter] public int Id { get; set; }

    private CountryRecord? _record;

    protected override async Task OnParametersSetAsync()
    {
        if (!this.Initialized)
            _record = await CountryProvider.GetRecordAsync(this.Id);
    }

    public record CountryRecord(int Id, string Name);

    public static class CountryProvider
    {
        public static IEnumerable<CountryRecord> _countries = new List<CountryRecord>
            {
             new(1, "UK"),
             new(2, "France"),
             new(3, "Portugal"),
             new(4, "Spain"),
            };

        public static async ValueTask<CountryRecord?> GetRecordAsync(int id)
        {
            // fake an async operation
            await Task.Delay(100);
            return _countries.FirstOrDefault(item => item.Id == id);
        }
    }
}
```

### `BlazrComponentBase`

This is the full `ComponentBase` implementation with the add ons.

```csharp
public class BlazrComponentBase : BlazorBaseComponent, IComponent, IHandleEvent, IHandleAfterRender
{
    private bool _hasCalledOnAfterRender;

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
        if (!Initialized)
        {
            this.OnInitialized();
            initTask = this.OnInitializedAsync();
            hasRenderedOnYield = await this.CheckIfShouldRunStateHasChanged(initTask);
            Initialized = true;
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
}
```

## Summing Up

Hopefully I've demonstrated that you don't need to stick with `ComponentBase`.  `BlazrComponentBase` is a functional equivalent to `ComponentBase`.  You only need to change the inheritance.

The three components are upwardly compatible.  Just change the inheritance to add functionality.


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
