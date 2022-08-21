# Building Leaner, Meaner, Greener Blazor Components

Blazor ships with a single developer "Component".  

You don't need to use it, but probably 99.x% of all developer built components either inherit directly or indirectly from it.  It's a one size fits all world.

# My pages render OK, why should I care?

Pretty good question, and to be frank, I have applications that work perfectly well with the standard `ComponentBase`.

To answer you, it's profligate.  In the new pay per cycle and memory footprint computing, it occupies memory space that it's not using and consumes CPU cycles for no purpose.  That memory space and those CPU cycles burn power, contributing to global warming.

Consider this about your `ComponentBase` inheriting component:

 - Most of the code in the component's memory footprint is never run.  It's just bloatware: memory occupied doing nothing.
 - Most of the render events the component generates result in zero UI changes.  CPU cycles used to achieve nothing.

If you're happy with that, move on.  If, like me, you want leaner, meaner, greener code, read on.

The source code for `ComponentBase` contains the following comment that you've almost certainly never read:

> Most of the developer-facing component lifecycle concepts are encapsulated in this base class. The core components rendering system doesn't know about them (it only knows about IComponent). This gives us flexibility to change the lifecycle concepts easily, or for developers to design their own lifecycles as different base classes.
  
I look across the component landscape and see no different base classes.  Here are the base classes for two of the popular Blazor libraries available on the market:

```csharp
public class RadzenComponent : ComponentBase, IDisposable
```

```csharp
public abstract class MudComponentBase : ComponentBase
```

I've even read comments from good devlopers saying restrict component usage, write repetitive markup code, every component you load is expensive.  Throw out the baby with the bath water!

## ComponentBase

Here's what `ComponentBase` looks like.  I've crunched it down as much as possible but it's still long, scroll on...

```csharp
public abstract class ComponentBase : IComponent, IHandleEvent, IHandleAfterRender
{
    private readonly RenderFragment _renderFragment;
    private RenderHandle _renderHandle;
    private bool _initialized;
    private bool _hasNeverRendered = true;
    private bool _hasPendingQueuedRender;
    private bool _hasCalledOnAfterRender;

    public ComponentBase()
    {
        _renderFragment = builder =>
        {
            _hasPendingQueuedRender = false;
            _hasNeverRendered = false;
            BuildRenderTree(builder);
        };
    }

    protected virtual void BuildRenderTree(RenderTreeBuilder builder) { }
    protected virtual void OnInitialized() { }
    protected virtual Task OnInitializedAsync() => Task.CompletedTask;
    protected virtual void OnParametersSet() { }
    protected virtual Task OnParametersSetAsync() => Task.CompletedTask;
    protected virtual bool ShouldRender() => true;
    protected virtual void OnAfterRender(bool firstRender) { }
    protected virtual Task OnAfterRenderAsync(bool firstRender) => Task.CompletedTask;
    protected Task InvokeAsync(Action workItem) => _renderHandle.Dispatcher.InvokeAsync(workItem);
    protected Task InvokeAsync(Func<Task> workItem) => _renderHandle.Dispatcher.InvokeAsync(workItem);

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

    void IComponent.Attach(RenderHandle renderHandle)
    {
        if (_renderHandle.IsInitialized)
            throw new InvalidOperationException($"The render handle is already set. Cannot initialize a {nameof(ComponentBase)} more than once.");

        _renderHandle = renderHandle;
    }

    public virtual Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        if (!_initialized)
        {
            _initialized = true;

            return RunInitAndSetParametersAsync();
        }
        else
            return CallOnParametersSetAsync();
    }

    private async Task RunInitAndSetParametersAsync()
    {
        OnInitialized();
        var task = OnInitializedAsync();

        if (task.Status != TaskStatus.RanToCompletion && task.Status != TaskStatus.Canceled)
        {
            StateHasChanged();

            try
            {
                await task;
            }
            catch
            {
                if (!task.IsCanceled)
                    throw;
            }
        }

        await CallOnParametersSetAsync();
    }

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

    Task IHandleAfterRender.OnAfterRenderAsync()
    {
        var firstRender = !_hasCalledOnAfterRender;
        _hasCalledOnAfterRender |= true;

        OnAfterRender(firstRender);

        return OnAfterRenderAsync(firstRender);
    }
}
```

## Slimming Down

There are two strategies I'll pursue in this article:

1. Working with the existing `ComponentBase` to reduce processor activity.
2. Developing alternative skinny components to reduce the size footprint and processor activity.


## Our Test Component

The simple component below exemplifies many of the main issues.

It's a button component you would use in a table view on each row for doing something with the record concerned.  It has a `RenderFragment` and an `EventCallback`.

```csharp
<div class="border border-2 border-dark bg-light p-2">
    <button class="btn btn-sm @Colour" @onclick=this.Clicked>@this.ChildContent</button>
    <span>Rendered at @DateTime.Now.ToLongTimeString()</span>
</div>

@code {
    [Parameter] public Guid RecordId { get; set; }
    [Parameter] public string Colour { get; set; } = "btn-primary";
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public EventCallback<Guid>? OnClick { get; set; }

    private void Clicked()
        => this.OnClick?.InvokeAsync(RecordId);
}
```

Here's a test page:

```
@page "/"
<h3>Test</h3>

<Button1>Say Hello</Button1>
<div class="m-2 p-2">
    <button class="btn btn-sm btn-primary" @onclick=this.Click>Do Something</button>
</div>

<div class="bg-dark text-white m-2 p-2">
    @($"Rendered at {DateTime.Now.ToLongTimeString()}")
</div>

@code {
    void Click()
    { }
}
```

I've included time stamps in both the component and demo page so you can see when the last render event occurs.  It's a simple, very effective tool to use to monitor render events.

Click on the main page button, and.., the component renders.  No reason it should, but it does. *Colateral Rendering*.

Our component looks simple, but that's just a surface veneer.  Under the hood it really looks like this:

```csharp
public class TheRealButton : IComponent, IHandleEvent, IHandleAfterRender
{
    private readonly RenderFragment _renderFragment;
    private RenderHandle _renderHandle;
    private bool _initialized;
    private bool _hasNeverRendered = true;
    private bool _hasPendingQueuedRender;
    private bool _hasCalledOnAfterRender;

    public TheRealButton()
    {
        _renderFragment = builder =>
        {
            _hasPendingQueuedRender = false;
            _hasNeverRendered = false;
            BuildRenderTree(builder);
        };
    }

    [Parameter] public Guid RecordId { get; set; }
    [Parameter] public string Colour { get; set; } = "btn-primary";
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public EventCallback<Guid>? OnClick { get; set; }

    private void Clicked()
    => this.OnClick?.InvokeAsync(RecordId);

    protected virtual void BuildRenderTree(RenderTreeBuilder builder) 
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "border border-2 border-dark bg-light p-2");
        builder.OpenElement(2, "button");
        builder.AddAttribute(3, "class", "btn" + " btn-sm" + " " + (Colour));
        builder.AddAttribute(4, "onclick", Microsoft.AspNetCore.Components.EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(this, this.Clicked));
        builder.AddContent(5, this.ChildContent);
        builder.CloseElement();
        builder.AddMarkupContent(6, "\r\n");
        builder.OpenElement(7, "span");
        builder.AddContent(8, "Rendered at ");
        builder.AddContent(9, DateTime.Now.ToLongTimeString());
        builder.CloseElement();
        builder.CloseElement();
    }

    protected virtual void OnInitialized() { }
    protected virtual Task OnInitializedAsync() => Task.CompletedTask;
    protected virtual void OnParametersSet() { }
    protected virtual Task OnParametersSetAsync() => Task.CompletedTask;
    protected virtual bool ShouldRender() => true;
    protected virtual void OnAfterRender(bool firstRender) { }
    protected virtual Task OnAfterRenderAsync(bool firstRender) => Task.CompletedTask;
    protected Task InvokeAsync(Action workItem) => _renderHandle.Dispatcher.InvokeAsync(workItem);
    protected Task InvokeAsync(Func<Task> workItem) => _renderHandle.Dispatcher.InvokeAsync(workItem);

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

    void IComponent.Attach(RenderHandle renderHandle)
    {
        if (_renderHandle.IsInitialized)
            throw new InvalidOperationException($"The render handle is already set. Cannot initialize a {nameof(ComponentBase)} more than once.");

        _renderHandle = renderHandle;
    }

    public virtual Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        if (!_initialized)
        {
            _initialized = true;

            return RunInitAndSetParametersAsync();
        }
        else
            return CallOnParametersSetAsync();
    }

    private async Task RunInitAndSetParametersAsync()
    {
        OnInitialized();
        var task = OnInitializedAsync();

        if (task.Status != TaskStatus.RanToCompletion && task.Status != TaskStatus.Canceled)
        {
            StateHasChanged();

            try
            {
                await task;
            }
            catch
            {
                if (!task.IsCanceled)
                    throw;
            }
        }

        await CallOnParametersSetAsync();
    }

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

    Task IHandleAfterRender.OnAfterRenderAsync()
    {
        var firstRender = !_hasCalledOnAfterRender;
        _hasCalledOnAfterRender |= true;

        OnAfterRender(firstRender);

        return OnAfterRenderAsync(firstRender);
    }
}
```

OK, I promise, no more long blocks of code, I think I've made my point!

## Strategies to prevent *Colateral Rendering*

*Colateral Rendering* is when a component re-renders either:
 - due to a UI event that hasn't actually changed anything, 
 - or the Renderer has called `SetParametersAsync` because the component has an object parameter (that hasn't actually changed, but it doesn't know that!).


### Don't use Objects

The simplest way is to restrict parameters to primitives.  Data can (and should) move into ViewServices.  Unfortunately `EventCallbacks` and `RenderFragments` are the staple diet of most components and they are objects. 

### Design

All too often components pass around data: a record, and list of records,...  Data management does not belong in the UI.  Use view objects, registered as DI services, to hold and manage data.

### Take Control of the Render Decision

We have no control over the Renderer, it's a black box.  The first point we can intercept is the call to `SetParametersAsync`.  

Here's what the `ComponentBase` method looks like.

```csharp
    public virtual Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);  // 1
        if (!_initialized)
        {
            _initialized = true;

            return RunInitAndSetParametersAsync(); // 2
        }
        else
            return CallOnParametersSetAsync();  // 3
    }
```

1. `parameters.SetParameterProperties(this)` applies the values provided in the `ParameterView` to the current component `this`.  It does this through refection so is in itself and "expensive" process.
2. If it's the first call run the Initialization lifecycle methods.
3. Run the  ParametersSet lifecycle methods.

The new version:
1. Sets the parameters.
2. Uses a record to keep track of changes and compare for changes.
3. Sets the first two objects on first render only.
3. Uses `ShouldRender` to control whether rendering actually happens.

```csharp
@implements IHandleEvent
@implements IHandleAfterRender

<div class="border border-2 border-dark bg-light p-2">
    <button class="btn btn-sm @Colour" @onclick=this.Clicked>@this.ChildContent</button>
    <span>Rendered at @DateTime.Now.ToLongTimeString()</span>
</div>

@code {
    private bool _isInitialized = false;
    private ChangeData _changeData = new ChangeData();

    [Parameter] public Guid RecordId { get; set; }
    [Parameter] public string Colour { get; set; } = "btn-secondary";
    [Parameter] public EventCallback<Guid> OnClick { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private record ChangeData
    {
        public Guid RecordId { get; init; }
        public string Colour { get; init; } = string.Empty;
    }

    private void Clicked()
        => this.OnClick.InvokeAsync(RecordId);

    public async override Task SetParametersAsync(ParameterView parameters)
    {
        // Assign the parameters
        parameters.SetParameterProperties(this);
        // Check if the significant parameters have changed
        var shouldRender = ShouldRenderOnParameterChange();
        // call the base which triggers the component lifecycle stuff with an empty set of parameters.
        if (shouldRender)
            await base.SetParametersAsync(ParameterView.Empty);

        _isInitialized = true;
    }

    protected bool ShouldRenderOnParameterChange()
    {
        var data = new ChangeData { RecordId = this.RecordId, Colour = this.Colour };
        var changed = data != _changeData;
        _changeData = data;
        return changed;
    }
}
```  

Our new button now works as expected.  We've prevented almost all the *Colateral Rendering*.  The only event that now remains is the component re-rendering when we click the button.  There's nothing to re-render, so can we prevent it?

### Take Control of UI Events

The Renderer passes registered UI events to Components.  How it does this is dictated by two interfaces:

 - `IHandleEvent` defines a single method - `Task HandleEventAsync(EventCallbackWorkItem callback, object? arg)` When implemented, it passes all events to the handler.  When not, it calls the method directly.

 - `IHandleAfterRender` defines a single method - `OnAfterRenderAsync()` which handles the after render process.  If nothing is defined then there is no process.

`ComponentBase` defines a custom event handler for all UI events.

```csharp
Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg)
{
    // Gets a task and runs the method passed as callback.
    var task = callback.InvokeAsync(arg);
    // If the task it's yielded set shouldAwaitTask
    var shouldAwaitTask = task.Status != TaskStatus.RanToCompletion &&
        task.Status != TaskStatus.Canceled;
    // Call state has changed
    // this will either be called if callback ran synchronously to completion or the task yielded
    StateHasChanged();

    // if the Task is stil running pass it to CallStateHasChangedOnAsyncCompletion
    // othwise return a completed task i.e. we were a block of synchronous code
    return shouldAwaitTask ?
        CallStateHasChangedOnAsyncCompletion(task) :
        Task.CompletedTask;
}

private async Task CallStateHasChangedOnAsyncCompletion(Task task)
{
    try
    {
        // await the task
        await task;
    }
    catch 
    {
        if (task.IsCanceled)
            return;
        throw;
    }
    // when it completes run a `StateHasChanged` to update the UI with any possible changes
    StateHasChanged();
}
``` 

We can disable this whole process by overriding `IHandleEvent.HandleEventAsync`.

```csharp
@implements IHandleEvent
//..
Task IHandleEvent.HandleEventAsync(
     EventCallbackWorkItem callback, object? arg) => callback.InvokeAsync(arg);
```

This new version simply invokes the specified UI event: there's no calls to `StateHasChanged`.

We can also dispense with the OnAfterRender event cycle by overriding `IHandleAfterRender.OnAfterRenderAsync` like this.  It returns a completed Task.

```csharp
@implements IHandleAfterRender
//..
Task IHandleAfterRender.OnAfterRenderAsync()
    => Task.CompletedTask;
```

The final version of our component:

```csharp
@implements IHandleEvent
@implements IHandleAfterRender

<div class="border border-2 border-dark bg-light p-2">
    <button class="btn btn-sm @Colour" @onclick=this.Clicked>@this.ChildContent</button>
    <span>Rendered at @DateTime.Now.ToLongTimeString()</span>
</div>

@code {
    private bool _isInitialized = false;
    private ChangeData _changeData = new ChangeData();

    [Parameter] public Guid RecordId { get; set; }
    [Parameter] public string Colour { get; set; } = "btn-secondary";
    [Parameter] public EventCallback<Guid> OnClick { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private record ChangeData
    {
        public Guid RecordId { get; init; }
        public string Colour { get; init; } = string.Empty;
    }

    private void Clicked()
        => this.OnClick.InvokeAsync(RecordId);

    public async override Task SetParametersAsync(ParameterView parameters)
    {
        // Assign the parameters
        parameters.SetParameterProperties(this);
        // Check if the significant parameters have changed
        var shouldRender = ShouldRenderOnParameterChange();
        // call the base which triggers the component lifecycle stuff with an empty set of parameters.
        if (shouldRender)
            await base.SetParametersAsync(ParameterView.Empty);

        _isInitialized = true;
    }

    protected bool ShouldRenderOnParameterChange()
    {
        var data = new ChangeData { RecordId = this.RecordId, Colour = this.Colour };
        var changed = data != _changeData;
        _changeData = data;
        return changed;
    }
}
```
### Render Fragment in Razor Libraries

The Razor compiler builds c# classes from the markup in Razor files.  The markup code is compiled into a method that overrides `void BuildRenderTree(RenderTreeBuilder builder)`.

We can use this to build lightweight render fragments.  The BootStrap Alert is a good example. 

Here's a base Razor class:

```csharp
public abstract class RazorBase
{
    protected abstract void BuildRenderTree(RenderTreeBuilder builder);
}
```

Which can then be used to build a library class:

```csharp
// Library.razor
@inherits RazorBase
@code {
    public static RenderFragment Alert(AlertData value) => (__builder) =>
        {
            @if (value.IsLoaded)
            {
                <div class="alert @value.Color">
                    @((MarkupString)value.Message)
                </div>
            }
        };
}
```

And use like this:

```csharp
@page "/"
<PageTitle>Index</PageTitle>

<h3>Blazor Component Testing </h3>

@(Library.Alert(data))

<div class="m-2 p-2">
    <button class="btn btn-sm btn-primary" @onclick=this.SetAlert>Set Alert</button>
</div>

@code {
    private bool toggle = false;
    private AlertData data = new AlertData();

    void SetAlert()
    {
        if (toggle)
            data = new AlertData { Color = "alert-danger", Message = $"Happened at {DateTime.Now.ToLongTimeString()}" };
        else
            data = new AlertData { Color = "alert-success", Message = $"Happened at {DateTime.Now.ToLongTimeString()}" };

        toggle = !toggle;
    }
}
```


### The Ultimate ComponentBase

We can take what we've learned above and build a more efficient `ComponentBase`.

What's in it:
1. It inherits from `IComponent`.
2. It has no specific UI event handler, so you need to implement manual render events within any UI event handlers.
3. There's no AfterRender infrastructure.
4. There's a single lifefcycle event `OnParametersChangedAsync` with an bool to indicate first render.
5. `ShouldRenderOnParameterChange` checks sgnificant parameters and returns true if any have changed.
6. There are two `StateHasChanged` methods.  
   1. An internal `Render` method which mimics the old `StateHasChanged` and used internally.
   2. A protected StateHasChanged which ensures `Render` is called on the UI thread.
7. `renderFragment` is `protected` so can be set in child components.


```csharp
public abstract class LeanComponentBase : IComponent
{
    protected RenderFragment renderFragment;
    private RenderHandle _renderHandle;
    protected bool initialized;
    private bool _hasNeverRendered = true;
    private bool _hasPendingQueuedRender;
    private bool _hidden;

    [Parameter] public Boolean Hidden { get; set; } = false;

    public LeanComponentBase()
    {
        this.renderFragment = builder =>
        {
            if (!this.Hidden)
            {
                _hasPendingQueuedRender = false;
                _hasNeverRendered = false;
                this.BuildRenderTree(builder);
            }
        };
    }

    void IComponent.Attach(RenderHandle renderHandle)
        => _renderHandle = renderHandle;

    public virtual async Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        var shouldRender = this.ShouldRenderOnParameterChange(initialized);

        if (_hasNeverRendered || shouldRender || _renderHandle.IsRenderingOnMetadataUpdate)
        {
            await this.OnParameterChangeAsync(!initialized);
            this.Render();
        }

        this.initialized = true;
    }

    protected virtual void BuildRenderTree(RenderTreeBuilder builder) { }

    protected virtual ValueTask OnParameterChangeAsync(bool firstRender)
        => ValueTask.CompletedTask;

    protected virtual bool ShouldRenderOnParameterChange(bool initialized)
    {
        var tripwire = new TripWire();

        tripwire.TripOnFalse(this.Hidden == _hidden);
        _hidden = this.Hidden;

        return tripwire.IsTripped;
    }

    private void Render()
    {
        if (_hasPendingQueuedRender)
            return;

        _hasPendingQueuedRender = true;

        try
        {
            _renderHandle.Render(renderFragment);
        }
        catch
        {
            _hasPendingQueuedRender = false;
            throw;
        }
    }

    protected void StateHasChanged()
        => _renderHandle.Dispatcher.InvokeAsync(Render);
}
```

Button can now look like this:

```csharp
@inherits LeanComponentBase

<div class="border border-2 border-dark bg-light p-2">
    <button class="btn btn-sm @Colour" @onclick=this.Clicked>@this.ChildContent</button>
    <span>Rendered at @DateTime.Now.ToLongTimeString()</span>
</div>

@code {
    [Parameter] public Guid RecordId { get; set; }
    [Parameter] public string Colour { get; set; } = "btn-secondary";
    [Parameter] public EventCallback<Guid> OnClick { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private ChangeData _changeData = new ChangeData();

    private void Clicked()
        => this.OnClick.InvokeAsync(RecordId);

    protected bool ShouldRenderOnParameterChange()
    {
        var data = new ChangeData { RecordId = this.RecordId, Colour = this.Colour };
        var changed = data != _changeData;
        _changeData = data;
        return changed;
    }

    private record ChangeData
    {
        public Guid RecordId { get; init; }
        public string Colour { get; init; } = string.Empty;
    }
}
``` 

## Some Examples

### The Loading Component


## Appendix

