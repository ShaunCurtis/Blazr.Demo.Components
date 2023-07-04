# Building A Base Document Library

In this article I'll show you how to build a component library based on `ComponentBase`.

We'll build three versions:

1. A simple UI component with minimal functionality that can be used for many basic UI components.
2. A mid level control component that has a single lifefcycle method and simple single rendering.
3. A full repacement for `ComponentBase` with some additional Wrapper/Frame functionality.

The goal is to provide a set of components from which you can choose the implementation that best fits the specific component design.

## ReplicaComponentBase

`ComponentBase` is a closed book [as it should be].  It was written in the early days of Blazor and hasn't changed since.

`ReplicaComponentBase` is a functional *black box* replacement: not an exact copy.  Some of the internal lifecycle code has been refactored a little to make the intent clearer.

This is the stsrting point for our code.

## BlazrBaseComponent

`BlazrBaseComponent` contains all the basic boiler plate code used by the components.

It contains many of the same private variables from `ComponentBase`.  It doesn't implement `IComponent`: it doesn't need to.

1. The `Initialized` flag has changed.  It's reversed and now `protected`, so inheriting classes can access it.
2. It has a Guid identifier.  This is useful to track instances in debugging.
3. It has a render state property that exposes the state based on the private render control class variables.  Again useful in debugging.
4. It has two `RenderFragments` for the Wrapper/Frame functionality.

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

The constructor implements the wrapper functionality.

1. It assigns the render code `BuildRenderTree` to `Body`.
2. It sets up the lambda method for the render fragment that will be passed to the Renderer by `StateHasChanged`.
3. The expression uses the `Frame` renderfragment if it's not null.
4. It sets `Initialized` to true.

We'll see the frame/wrapper functionality demonstrated later.

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

Note: there are no lifecycle methods or implementation of `SetParametersAsync`.  That's left to the individual library classes.

## BlazrUIBase

This is our simple implementation.  It looks like this:

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

It inherits from `BlazorBaseComponent` and implements `IComponent`.

1. It has a fixed `SetParametersAsync`.  It's not `virtual` so you can't override it.
2. It has no lifecycle methods.  Simple components don't need them.
3. It doesn't implement `IHandleEvent` i.e. it has no UI event handling.  If you need any, call `StateHasChanged` manually.
4. It doesn't implement `IHandleAfterRender` i.e. it has no after render handling.  If you need it, implement it manually.

### Demo

A dismissible Alert with a clickable close button.

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

And the demo `Index`

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

It's intersting that there's no manual calls to `StateHasChanged`, even though it would seem that one is needed [to update the alert when the close button is clicked].

`Index` inherits from `ComponentBase` so `StateHasChanged` is automatically called by the UI handler when it's triggered.

1. The Alert `Dismiss` invokes `MessageChanged` passing a `null` string.
2. The UI handler invokes the Bind handler in `Index`.
3. The Bind handler [created by the Razor Compiler] updates `_message` [to `null`].
4. The UI Handler completes and calls `StateHasChanged`.
5. `Index` renders, and the Renderer detects the `Message` parameter on `Alert` has changed.
6. The Renderer calls `SetParametersAsync` on `Alert` and passes in the modified `ParameterView`.
7. `Alert` renders, hiding the alert.

> Important Point : Always test whether you actually need to call `StateHasChanged`.

### BlazrControlBase

`BlazrControlBase` is the intermediate level component.

It:

1. Implements the `OnParametersSetAsync` lifecycle method.
2. Implements a single render UI event handler.

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

The demo page looks just like a normal `ComponentBase` page.  That's intentional.  Note that the component has access to the initialization state of the component: there's no longer a need for `OnInitialized`.  There's also no need for a sync version of either method.  You can run synchronous code in `OnParametersSetAsync`.  You just need to return a completed Task at the end of the process [the same as the empty version of the method].     

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

## Implementing the Wrapper/Frame Functionality

A Demo `Wrapper` component.  

Note the wrapper is defined in the `Frame` render fragment, and uses the Razor build in `__builder` RenderTreeBuilder instance.


```csharp
@inherits BlazrControlBase

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


## Manually Implementing OnAfterRender

It's very simple to manually implement `OnAfterRender`.


```csharp
@implements IHandleAfterRender

//...  markup

@code {
    // Implement if need to detect first after render
    private bool _firstRender = true;

    Task IHandleAfterRender.OnAfterRenderAsync()
    {
        if (_firstRender)
        {
            // Do first render stuff
            _firstRender = false;
        }

        // Do subsequent render stuff
    }
}
```


## Summing Up

Hopefully I've demonstrated that you don't need to stick with `ComponentBase` straightjacket.  `BlazrComponentBase` is a functional equivalent to `ComponentBase`.  You only need to change the inheritance.

The three components are upwardly compatible.  Just change the inheritance to add functionality.

Once you start using them, you'll find that `BlazrControlBase` fits most needs.

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
