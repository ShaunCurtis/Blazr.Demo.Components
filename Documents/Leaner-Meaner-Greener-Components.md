# Building Leaner, Meaner, Greener Blazor Components

# Rethinking the Blazor Component

Blazor ships with a single developer "Component".  If you add a Razor file it inherits from it by default.

`ComponentBase` rules the Blazor UI world.  You don't need to use it, but probably 99.x% of all developer built components either inherit directly or indirectly from it.

In a world of diverse requirements. we have a one size fits all world solution.

Read most articles on Components and you would believe component and `ComponentBase` are synonymous!

## Why?

Valid question.  My application runs perfectly well with `ComponentBase`.

Consider this:

 - Most code in the component's memory footprint is never run.  It's just bloatware: memory occupied doing nothing.
 - Most render events the component generates result in zero UI changes.  CPU cycles used to achieve nothing.
 - There are some key inheritance issues that it doesn't address.  I'll cover these shortly.

To me that sounds like a piece of code that wouldn't survive it's first review.

It occupies memory space that it isn't using and consumes CPU cycles for no purpose.  That's money and energy going down the sink.  It's neither lean, mean nor green!

Let's look at some code to illustrate my point.

Here's a "simple" component.  It's a Bootstrap container.

```csharp
<div class="container">
    @ChildContent
</div>
@code {
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
```

This looks very simple and would probably pass code review with the right arguments as to why you need it.

Now take a look at Appendix 1. Why is is not here?  TLDR!  This is what the above component really looks like.  Do you think this would pass code review?  It wouldn't even make it to code review session with me!

## So why does everyone use ComponentBase?

If every component you've written derives from `ComponentBase`, you need to answer that question.  I'm not in that category.

To quote from the source code for `ComponentBase`:

> Most of the developer-facing component lifecycle concepts are encapsulated in this base class. The core components rendering system doesn't know about them (it only knows about IComponent). This gives us flexibility to change the lifecycle concepts easily, or for developers to design their own lifecycles as different base classes.
  
I don't think the author of that comment ever expected `ComponentBase` to dominate the Blazor UI.  I look across the component landscape and see no different base classes.  Here are the base classes for two of the popular Blazor libraries available on the market:

```csharp
public class RadzenComponent : ComponentBase, IDisposable
```

```csharp
public abstract class MudComponentBase : ComponentBase
```

Good developers are questioning component usage. They believe simple components are too **expensive**.  They write repetitive code instead.

My answer: Don't throw away the component.  write components that are fit for purpose.

I have three principle base components.  All are based on what I call the **Lean Mean Green Component** - LMGC from now on - that I'll cover in detail below.

The stategies are:

### Simplify the Lifecycle Process

How many components you write use the full gamat of lifecycle methods?  1%, if that.  Simplify or even remove the methods.  You remove a lot of code and expensive construction of Tasks for no purpose.

The LMGC has a single async method returning a `ValueTask`.  Note the `bool` argument passing in whether this is the first render.

```csharp
protected virtual ValueTask OnParametersChangedAsync(bool firstRender)
  => ValueTask.CompletedTask;
```

### Managing Parameter Changes

When a component is rendered, the renderer must decide whether any child components need re-rendering.  It manages a component's parameter state though a `ParametersView` object.  It checks if any child component parameters have changed, and if so, calls `SetParametersAsync` passing in the `ParametersView` object.

The first line of `SetParametersAsync` uses the `ParametersView` to set the component's parameters.
 
```csharp
        parameters.SetParameterProperties(this);
```
There are two issues with this process.  Neither are simple to address:

1. Setting the parameters is a relatively expensive exercise because `ParameterView` uses reflection to find and assign the parameter values.

2. The method by which `ParameeterView` detects state change is relatively crude.  

```csharp
public static bool MayHaveChanged<T1, T2>(T1 oldValue, T2 newValue)
{
    var oldIsNotNull = oldValue != null;
    var newIsNotNull = newValue != null;

    // Only one is null so different
    if (oldIsNotNull != newIsNotNull)
        return true;

    var oldValueType = oldValue!.GetType();
    var newValueType = newValue!.GetType();

    if (oldValueType != newValueType)
        return true;

    if (!IsKnownImmutableType(oldValueType))
        return true;

    return !oldValue.Equals(newValue);
}

private static bool IsKnownImmutableType(Type type)
    => type.IsPrimitive
        || type == typeof(string)
        || type == typeof(DateTime)
        || type == typeof(Type)
        || type == typeof(decimal)
        || type == typeof(Guid);
```

Callbacks and RenderFragments are objects and allways fail the `IsKnownImmutableType` test.

My strategy is this:

1. Stick to Immutable types where possible.
2. Live with it.
3. If a component is being used a lot and performance is an issue, do the assignment and change checking manually.  You can often assume that Callbacks and RenderFragments won't change once initially assigned. 

### Don't Render when you don't need to

You should only re-render a component when you need to.  Don't do it by default, which is what `ComponentBase` does.  

Here's the `ComponentBase` handler for UI events:

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
```

If you don't implement `IHandleEvent` then you are repsonsible for calling `StateHasChanged` when you need to.

### Do You need `AfterRender`?

`ComponentBase` implements a set of after render events.

```csharp
    Task IHandleAfterRender.OnAfterRenderAsync()
    {
        var firstRender = !_hasCalledOnAfterRender;
        _hasCalledOnAfterRender |= true;

        OnAfterRender(firstRender);

        return OnAfterRenderAsync(firstRender);
    }
```

Probably 99% of components don't need them.  So manually implement `IHnadleAfterRender` on the rare occasions you need it.

## Our Lean. Mean. Green Components

Based on what we've discussed above we can build a new component. 

```csharp
public abstract class LeanComponentBase : IComponent
{
    protected RenderFragment renderFragment;
    private RenderHandle _renderHandle;
    protected bool initialized;
    private bool _hasNeverRendered = true;
    private bool _hasPendingQueuedRender;

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

        if (_hasNeverRendered || shouldRender)
        {
            await this.OnParametersChangedAsync(!initialized);
            this.Render();
        }

        this.initialized = true;
    }

    protected virtual void BuildRenderTree(RenderTreeBuilder builder) { }

    protected virtual ValueTask OnParametersChangedAsync(bool firstRender)
        => ValueTask.CompletedTask;

    protected virtual bool ShouldRenderOnParameterChange(bool initialized)
        => true;

    protected void Render()
    {
        if (_hasPendingQueuedRender)
            return;

        _hasPendingQueuedRender = true;
        _renderHandle.Render(renderFragment);
    }

    protected void StateHasChanged()
        => _renderHandle.Dispatcher.InvokeAsync(Render);
}
```

## ComponentBase

Here's what `ComponentBase` looks like.  I've crunched it down as much as possible but it's still long, scroll on...

Hopefully, you get the point.

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
2. Uses a record to track and detect parameter changes.
3. Sets the first two objects on first render only.
3. Uses `shouldRender` to control whether rendering actually happens.

```csharp
@implements IHandleEvent
@implements IHandleAfterRender

<div class="border border-2 border-dark bg-light p-2">
    <button class="btn btn-sm @Colour" @onclick=this.Clicked>@this.ChildContent</button>
    <span>Rendered at @DateTime.Now.ToLongTimeString()</span>
</div>

@code {
    private bool _isInitialized = false;
    private record ChangeData(Guid RecordId, string Colour);
    private ChangeData _changeData = new ChangeData(Guid.Empty, string.Empty);

    [Parameter] public Guid RecordId { get; set; }
    [Parameter] public string Colour { get; set; } = "btn-secondary";
    [Parameter] public EventCallback<Guid> OnClick { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private void Clicked()
        => this.OnClick.InvokeAsync(RecordId);

    public async override Task SetParametersAsync(ParameterView parameters)
    {
        // Assign the parameters
        parameters.SetParameterProperties(this);
        // Check if the significant parameters have changed
        var shouldRender = ShouldRenderOnParameterChange();
        // call the base which triggers the component lifecycle stuff with an empty set of parameters.
        if (shouldRender || !_isInitialized)
            await base.SetParametersAsync(ParameterView.Empty);

        _isInitialized = true;
    }

    protected bool ShouldRenderOnParameterChange()
    {
        var data = new ChangeData(this.RecordId, this.Colour );
        var parameterChange = data != _changeData;
        _changeData = data;
        return parameterChange;
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
    private record ChangeData(Guid RecordId, string Colour);
    private ChangeData _changeData = new ChangeData(Guid.Empty, string.Empty);

    [Parameter] public Guid RecordId { get; set; }
    [Parameter] public string Colour { get; set; } = "btn-secondary";
    [Parameter] public EventCallback<Guid> OnClick { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

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
        var data = new ChangeData(this.RecordId, this.Colour );
        var parameterChange = data != _changeData;
        _changeData = data;
        return parameterChange;
    }

    Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg)
        => callback.InvokeAsync(arg);

    Task IHandleAfterRender.OnAfterRenderAsync()
        => Task.CompletedTask;

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
   1. `Render` which mimics the old `StateHasChanged` and used internally.
   2. `StateHasChanged` which ensures `Render` is called on the UI thread and should be uyused in any event handler.
7. `renderFragment` is `protected` so can be set in child components.


```csharp
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazr.Components;
public abstract class LeanComponentBase : IComponent
{
    protected RenderFragment renderFragment;
    private RenderHandle _renderHandle;
    protected bool initialized;
    private bool _hasNeverRendered = true;
    private bool _hasPendingQueuedRender;

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

        if (_hasNeverRendered || shouldRender)
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
        => true;

    protected void Render()
    {
        if (_hasPendingQueuedRender)
            return;

        _hasPendingQueuedRender = true;
        _renderHandle.Render(renderFragment);
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

## An Example

Using the Blazor Server template we'll rebuild `FetchData`.

##  WeatherForecastService

1. Move the list to the service.
2. Create an Add Method.
3. Provide a list changed event.

```csharp
public class WeatherForecastService
{
    private List<WeatherForecast> weatherForecasts { get; set; } = new List<WeatherForecast>();

    public IEnumerable<WeatherForecast> WeatherForecasts => this.weatherForecasts;
    public event EventHandler? ListChanged;

    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    public void GetForecasts()
    {
        if (!weatherForecasts.Any())
            this.weatherForecasts =
                Enumerable.Range(1, 5).Select(index => new WeatherForecast
                {
                    Date = DateTime.Now.AddDays(index),
                    TemperatureC = Random.Shared.Next(-20, 55),
                    Summary = Summaries[Random.Shared.Next(Summaries.Length)]
                }).ToList();
    }

    public void AddRecord()
    {
        this.weatherForecasts.Add(new WeatherForecast
        {
            Date = DateTime.Now,
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        });
        ListChanged?.Invoke(this, EventArgs.Empty);
    }
}
```

### The Loading Component

```csharp
@inherits LeanComponentBase

@if (this.IsLoading)
{
    <div>Loading....</div>
}
else
{
    @this.ChildContent
}

@code {
    private record ChangeData(bool IsLoading);
    private ChangeData changeData = new ChangeData(false);
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override bool ShouldRenderOnParameterChange(bool initialized)
    {
        var data = new ChangeData(this.IsLoading);
        var parameterChange = data != changeData;
        changeData = data;
        return parameterChange;
    }
}
```

### The Grid Control

```csharp
@inherits LeanComponentBase
@typeparam TRecord where TRecord : class, new()

@if (this.HasRecords)
{
    <table class="@this.Class">
        <thead>
            <CascadingValue Name="IsHeader" Value="true">
                <tr>
                    @this.ChildContent!(new TRecord())
                </tr>
            </CascadingValue>
        </thead>
        <tbody>
            @foreach (var item in this.Records!)
            {
                <tr @key=item>
                    @ChildContent!(item)
                </tr>
            }
        </tbody>
    </table>
}
else
{
    <div class="alert alert-warning">
        No Records to Display
    </div>
}

@code {
    [Parameter] public IEnumerable<TRecord>? Records { get; set; }
    [Parameter] public string Class { get; set; } = "table";
    [Parameter, EditorRequired] public RenderFragment<TRecord>? ChildContent { get; set; }

    private bool HasRecords => Records?.Count() > 0;
    private record ChangeData(bool Hidden, string Class, Guid Update);
    private ChangeData changeData = new ChangeData(false, string.Empty, Guid.Empty);

    public void ListUpdated()
        => this.StateHasChanged();

    protected override bool ShouldRenderOnParameterChange(bool initialized)
    {
        var data = new ChangeData(this.Hidden, this.Class, this.UpdateId);
        var parameterChange = data != changeData;
        changeData = data;
        return parameterChange;
    }
}
```

### The Grid Column

This includes a realistic `EventCallback` for sorting, through we don't actually implement it here. 

```csharp
@inherits LeanComponentBase
@if(this.IsHeader)
{
    <th @onclick=SortAction>@((MarkupString)this.Header)</th>
}
else
{
    <td>@((MarkupString)this.Value)</td>
}
@code {
    [Parameter, EditorRequired] public string Header { get; set; } = string.Empty;
    [Parameter] public string Value { get; set; } = string.Empty;
    [CascadingParameter(Name ="IsHeader")] private bool IsHeader { get; set; }
    [Parameter] public EventCallback<string> Sort { get; set; }

    private record ChangeData(string Header, string Value);
    private ChangeData changeData = new ChangeData(string.Empty, string.Empty);

    protected override bool ShouldRenderOnParameterChange(bool initialized)
    {
        var data = new ChangeData(this.Header, this.Value);
        var parameterChange = data != changeData;
        changeData = data;
        return parameterChange;
    }

    private void SortAction()
        => Sort.InvokeAsync(Header);
}
```

### FetchData

```csharp
@page "/fetchdata"
@using Blazr.Components.LeanComponents
@inherits LeanComponentBase
@implements IDisposable

<PageTitle>Weather forecast</PageTitle>

@using Blazr.Components.Data
@inject WeatherForecastService ForecastService

<h1>Weather forecast</h1>

<p>This component demonstrates fetching data from a service.</p>

<div class="m-2 text-end">
    <button class="btn btn-primary" @onclick=AddRecord>Add Record</button>
</div>

<Loading IsLoading=this.IsLoading>
    <GridControl @ref=this.grid TRecord=WeatherForecast Records=this.ForecastService.WeatherForecasts>
        <GridColumn Header="Date" Value="@context.Date.ToShortDateString()" Sort=this.OnSort />
        <GridColumn Header="Temp &deg;C" Value="@context.TemperatureF.ToString()" Sort=this.OnSort />
        <GridColumn Header="Temp &deg;F" Value="@context.TemperatureF.ToString()" Sort=this.OnSort />
        <GridColumn Header="Summary" Value="@context.Summary" Sort=this.OnSort />
    </GridControl>
</Loading>

@code {
    private bool IsLoading;
    private GridControl<WeatherForecast>? grid;

    protected override async ValueTask OnParameterChangeAsync(bool firstRender)
    {
        if (firstRender)
        {
            this.IsLoading = true;
            this.Render();
            ForecastService.GetForecasts();
            await Task.Delay(1000);
            this.IsLoading = false;
            this.ForecastService.ListChanged += OnListChanged;
        }
    }

    private void OnSort(string column)
    { }

    private void AddRecord(MouseEventArgs e)
        => this.ForecastService.AddRecord();

    private void OnListChanged(object? sender, EventArgs e)
    => grid?.ListUpdated();

    public void Dispose()
    => this.ForecastService.ListChanged -= OnListChanged;
}
```

## Conclusions



## Appendix

In the Repo you will find a `ComponentBase` version of `FetchData` and some extra logging code added to `LeanComponentBase` and a copy of `ComponentBase` called `BlazrComponentBase`.

With these in place we can log render events in the two base components.

Here's the results for a 2 row grid.

First `LeanComponentBase`.

```text
COMPONENT => Index instance ba03e6fa-eb91-42c2-b1b1-5fec18021b14 created at 17:23:20
RENDER-EVENT =>Index instance ba03e6fa-eb91-42c2-b1b1-5fec18021b14 rendered at 17:23:20
COMPONENT => Loading instance 34257332-c94c-4e38-a747-8bc8d97e79b6 created at 17:23:20
RENDER-EVENT =>Loading instance 34257332-c94c-4e38-a747-8bc8d97e79b6 rendered at 17:23:20
RENDER-EVENT =>Index instance ba03e6fa-eb91-42c2-b1b1-5fec18021b14 rendered at 17:23:21
RENDER-EVENT =>Loading instance 34257332-c94c-4e38-a747-8bc8d97e79b6 rendered at 17:23:21
COMPONENT => GridControl`1 instance 80403378-6dce-439d-945b-9825980d381a created at 17:23:21
RENDER-EVENT =>GridControl`1 instance 80403378-6dce-439d-945b-9825980d381a rendered at 17:23:21
COMPONENT => GridColumn instance 9f81418b-469a-4c86-85da-4e5f80bc197b created at 17:23:21
COMPONENT => GridColumn instance 369b8223-473f-4a7a-bc48-2a0d3c2108e7 created at 17:23:21
COMPONENT => GridColumn instance 80821aa2-f795-4d91-bc9c-cfb9af0f1d6e created at 17:23:21
COMPONENT => GridColumn instance 8dd9b28c-01df-4a4a-b1d2-88a70ae24192 created at 17:23:21
COMPONENT => GridColumn instance 42738be2-2cff-4404-ac47-49e1d25a1019 created at 17:23:21
COMPONENT => GridColumn instance 5f1c0bdc-b89e-4d86-9187-fd9a464371cc created at 17:23:21
COMPONENT => GridColumn instance 385205e4-c80c-42f8-9ece-e950a7a5920e created at 17:23:21
COMPONENT => GridColumn instance d775d1cc-796b-42e4-bf2c-049fecdbb904 created at 17:23:21
COMPONENT => GridColumn instance e18c436d-f9b8-47a0-b7c0-f59fb684dfa8 created at 17:23:21
COMPONENT => GridColumn instance 5d0923c0-68ca-476d-a325-0b2a337f2941 created at 17:23:21
COMPONENT => GridColumn instance 61cd9da1-05b2-4a7a-a505-410eddb9a0c3 created at 17:23:21
COMPONENT => GridColumn instance 1eec4cf0-abe2-48d2-9b05-c9508769f12d created at 17:23:21
RENDER-EVENT =>GridColumn instance 9f81418b-469a-4c86-85da-4e5f80bc197b rendered at 17:23:21
RENDER-EVENT =>GridColumn instance 369b8223-473f-4a7a-bc48-2a0d3c2108e7 rendered at 17:23:21
RENDER-EVENT =>GridColumn instance 80821aa2-f795-4d91-bc9c-cfb9af0f1d6e rendered at 17:23:21
RENDER-EVENT =>GridColumn instance 8dd9b28c-01df-4a4a-b1d2-88a70ae24192 rendered at 17:23:21
RENDER-EVENT =>GridColumn instance 42738be2-2cff-4404-ac47-49e1d25a1019 rendered at 17:23:21
RENDER-EVENT =>GridColumn instance 5f1c0bdc-b89e-4d86-9187-fd9a464371cc rendered at 17:23:21
RENDER-EVENT =>GridColumn instance 385205e4-c80c-42f8-9ece-e950a7a5920e rendered at 17:23:21
RENDER-EVENT =>GridColumn instance d775d1cc-796b-42e4-bf2c-049fecdbb904 rendered at 17:23:21
RENDER-EVENT =>GridColumn instance e18c436d-f9b8-47a0-b7c0-f59fb684dfa8 rendered at 17:23:21
RENDER-EVENT =>GridColumn instance 5d0923c0-68ca-476d-a325-0b2a337f2941 rendered at 17:23:21
RENDER-EVENT =>GridColumn instance 61cd9da1-05b2-4a7a-a505-410eddb9a0c3 rendered at 17:23:21
RENDER-EVENT =>GridColumn instance 1eec4cf0-abe2-48d2-9b05-c9508769f12d rendered at 17:23:21
```

And on clicking the add button only the grid control gets rendered and four new columns get added and rendered. 

```text
RENDER-EVENT =>GridControl`1 instance 80403378-6dce-439d-945b-9825980d381a rendered at 17:25:08
COMPONENT => GridColumn instance 568b9b4c-e3d8-4c71-b244-1a5e66205ce4 created at 17:25:08
COMPONENT => GridColumn instance 9beecea5-df6a-4e8f-a3d8-899e96ead0b9 created at 17:25:08
COMPONENT => GridColumn instance 23899ee1-bbe0-4a39-a507-07665c718d4e created at 17:25:08
COMPONENT => GridColumn instance addf591d-2c20-4a73-a14d-abfb864006d7 created at 17:25:08
RENDER-EVENT =>GridColumn instance 568b9b4c-e3d8-4c71-b244-1a5e66205ce4 rendered at 17:25:08
RENDER-EVENT =>GridColumn instance 9beecea5-df6a-4e8f-a3d8-899e96ead0b9 rendered at 17:25:08
RENDER-EVENT =>GridColumn instance 23899ee1-bbe0-4a39-a507-07665c718d4e rendered at 17:25:08
RENDER-EVENT =>GridColumn instance addf591d-2c20-4a73-a14d-abfb864006d7 rendered at 17:25:08
The thread 0x1cdc has exited with code 0 (0x0).
```

`ComponentBase` is similar to `LeanComponentBase` on initial creation as we would expect.

```text
COMPONENT => FetchData instance 3f87fba8-30dc-461b-bd5b-f180aae0bd58 created at 17:32:15
RENDER-EVENT =>FetchData instance 3f87fba8-30dc-461b-bd5b-f180aae0bd58 rendered at 17:32:15
COMPONENT => Loading instance 7c762c4d-b93a-43b7-adee-ce68c79f7405 created at 17:32:15
RENDER-EVENT =>Loading instance 7c762c4d-b93a-43b7-adee-ce68c79f7405 rendered at 17:32:15
RENDER-EVENT =>FetchData instance 3f87fba8-30dc-461b-bd5b-f180aae0bd58 rendered at 17:32:16
RENDER-EVENT =>Loading instance 7c762c4d-b93a-43b7-adee-ce68c79f7405 rendered at 17:32:16
COMPONENT => GridControl`1 instance 644149be-52ef-42ca-8332-ab594ea64a50 created at 17:32:16
RENDER-EVENT =>GridControl`1 instance 644149be-52ef-42ca-8332-ab594ea64a50 rendered at 17:32:16
COMPONENT => GridColumn instance 371def64-e74b-483d-b583-35616acf72fe created at 17:32:16
COMPONENT => GridColumn instance f1901b3b-263e-454c-bc95-df2883c6b91d created at 17:32:16
COMPONENT => GridColumn instance 8d8578fb-aec6-4b17-884a-32483720efa5 created at 17:32:16
COMPONENT => GridColumn instance 7c9e18a4-1843-48f9-832f-5c6a20e2e525 created at 17:32:16
COMPONENT => GridColumn instance 76e0d3de-6d85-4d6f-825f-05b1987e1160 created at 17:32:16
COMPONENT => GridColumn instance 099065d8-a43f-4c71-af5b-b576bbc646d6 created at 17:32:16
COMPONENT => GridColumn instance 254a9e2a-d1a5-47d2-900e-6c38273acf9a created at 17:32:16
COMPONENT => GridColumn instance 50804af3-f5fd-473e-9477-0aa803afbd26 created at 17:32:16
COMPONENT => GridColumn instance 612fe6dd-1800-4c72-b5cc-eaff2480f806 created at 17:32:16
COMPONENT => GridColumn instance 6b6672d7-95b0-4e13-9d68-bed747b8a425 created at 17:32:16
COMPONENT => GridColumn instance bfe97860-3539-4317-a4e6-4ffb62d93f2b created at 17:32:16
COMPONENT => GridColumn instance 65ecf0d5-3fe5-43d0-91b4-938d7c98d4c7 created at 17:32:16
RENDER-EVENT =>GridColumn instance 371def64-e74b-483d-b583-35616acf72fe rendered at 17:32:16
RENDER-EVENT =>GridColumn instance f1901b3b-263e-454c-bc95-df2883c6b91d rendered at 17:32:16
RENDER-EVENT =>GridColumn instance 8d8578fb-aec6-4b17-884a-32483720efa5 rendered at 17:32:16
RENDER-EVENT =>GridColumn instance 7c9e18a4-1843-48f9-832f-5c6a20e2e525 rendered at 17:32:16
RENDER-EVENT =>GridColumn instance 76e0d3de-6d85-4d6f-825f-05b1987e1160 rendered at 17:32:16
RENDER-EVENT =>GridColumn instance 099065d8-a43f-4c71-af5b-b576bbc646d6 rendered at 17:32:16
RENDER-EVENT =>GridColumn instance 254a9e2a-d1a5-47d2-900e-6c38273acf9a rendered at 17:32:16
RENDER-EVENT =>GridColumn instance 50804af3-f5fd-473e-9477-0aa803afbd26 rendered at 17:32:16
RENDER-EVENT =>GridColumn instance 612fe6dd-1800-4c72-b5cc-eaff2480f806 rendered at 17:32:16
RENDER-EVENT =>GridColumn instance 6b6672d7-95b0-4e13-9d68-bed747b8a425 rendered at 17:32:16
RENDER-EVENT =>GridColumn instance bfe97860-3539-4317-a4e6-4ffb62d93f2b rendered at 17:32:16
RENDER-EVENT =>GridColumn instance 65ecf0d5-3fe5-43d0-91b4-938d7c98d4c7 rendered at 17:32:16
```

However on the button click there's a lot of *Colateral Rendering*: as well as creating and rendering the new row columns, all the existing GridColumns get re-rendered. 

```text
RENDER-EVENT =>FetchData instance 3f87fba8-30dc-461b-bd5b-f180aae0bd58 rendered at 17:32:32
RENDER-EVENT =>Loading instance 7c762c4d-b93a-43b7-adee-ce68c79f7405 rendered at 17:32:32
RENDER-EVENT =>GridControl`1 instance 644149be-52ef-42ca-8332-ab594ea64a50 rendered at 17:32:32
COMPONENT => GridColumn instance 7849c351-e452-479f-b3dc-fdc2230f76f0 created at 17:32:32
COMPONENT => GridColumn instance bbe96e55-8cfe-4b5b-858d-352a4face797 created at 17:32:32
COMPONENT => GridColumn instance 14580cc8-e23f-4559-b871-a6c8a194987f created at 17:32:32
COMPONENT => GridColumn instance 5fb9e7f3-2158-4dfd-88cc-58c9dc969ac3 created at 17:32:32
RENDER-EVENT =>GridColumn instance 371def64-e74b-483d-b583-35616acf72fe rendered at 17:32:32
RENDER-EVENT =>GridColumn instance f1901b3b-263e-454c-bc95-df2883c6b91d rendered at 17:32:32
RENDER-EVENT =>GridColumn instance 8d8578fb-aec6-4b17-884a-32483720efa5 rendered at 17:32:32
RENDER-EVENT =>GridColumn instance 7c9e18a4-1843-48f9-832f-5c6a20e2e525 rendered at 17:32:32
RENDER-EVENT =>GridColumn instance 76e0d3de-6d85-4d6f-825f-05b1987e1160 rendered at 17:32:32
RENDER-EVENT =>GridColumn instance 099065d8-a43f-4c71-af5b-b576bbc646d6 rendered at 17:32:32
RENDER-EVENT =>GridColumn instance 254a9e2a-d1a5-47d2-900e-6c38273acf9a rendered at 17:32:32
RENDER-EVENT =>GridColumn instance 50804af3-f5fd-473e-9477-0aa803afbd26 rendered at 17:32:32
RENDER-EVENT =>GridColumn instance 7849c351-e452-479f-b3dc-fdc2230f76f0 rendered at 17:32:32
RENDER-EVENT =>GridColumn instance bbe96e55-8cfe-4b5b-858d-352a4face797 rendered at 17:32:32
RENDER-EVENT =>GridColumn instance 14580cc8-e23f-4559-b871-a6c8a194987f rendered at 17:32:32
RENDER-EVENT =>GridColumn instance 5fb9e7f3-2158-4dfd-88cc-58c9dc969ac3 rendered at 17:32:32
RENDER-EVENT =>GridColumn instance 612fe6dd-1800-4c72-b5cc-eaff2480f806 rendered at 17:32:32
RENDER-EVENT =>GridColumn instance 6b6672d7-95b0-4e13-9d68-bed747b8a425 rendered at 17:32:32
RENDER-EVENT =>GridColumn instance bfe97860-3539-4317-a4e6-4ffb62d93f2b rendered at 17:32:32
RENDER-EVENT =>GridColumn instance 65ecf0d5-3fe5-43d0-91b4-938d7c98d4c7 rendered at 17:32:32
```

## Appendix

### ComponentBase

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
