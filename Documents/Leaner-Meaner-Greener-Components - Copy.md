# Building Leaner, Meaner, Greener Blazor Components - AKA Rethinking the Blazor Component

Blazor ships with a single developer "Component".  If you add a Razor file it inherits from it by default.

`ComponentBase` rules the Blazor UI world.  You don't have to use it, but probably 99.x% of all developer built components either inherit directly or indirectly from it.

In a world of diversity, we have a one size fits all, swiss army knife solution.  A jack of all trades and master of none.

Most articles treat `ComponentBase` as it!

`ComponentBase` should be just one tool in your toolbox, not the toolbox.  I may be in a minority of one. but I rarely use it.

## Why?

Valid question.  My application runs perfectly well with `ComponentBase`.  Most of mine do, but that's not a reason to stay.

Consider this:

 - Most code in the component's memory footprint is never run.  It's just bloatware: memory occupied doing nothing.
 - Most render events the component generates result in no UI changes.  CPU cycles used achieving nothing.
 - There are some key inheritance issues that it doesn't address.  I'll cover these shortly.

To summarise why not: it occupies memory space that it isn't using and consumes CPU cycles for no purpose.  That's money and energy going down the drain.  

Do you write lean, mean, green code, or bloatware?

Let me illustrate my point.

Here's a "simple" component.  It's a Bootstrap container.

```csharp
<div class="container">
    @ChildContent
</div>
@code {
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
```

Looks very simple and would probably pass code review with the right arguments as to why you need it.

Now take a look at this?  I haven't shown you the 150+ lines - I don't want TLDR!

```csharp
public abstract class ComponentBase : IComponent, IHandleEvent, IHandleAfterRender
{
    // 150+ lines
    // See Appendix for the 150+ lines
}
```

This is what the above component really looks like.  Do you think this would pass code review?  It wouldn't even make it to code review session with me!

## So why does everyone use ComponentBase?

Never asked the right questions, lazy, don't know any better.  Component library suppliers - no idea, there are plenty of clever people around in those organisations.  Whatever green credentials they dispay is greenwashing!  They may have green panels on their roof, but every time one of their components gets rendered it burning more energy that it ought to.

If every component you've written derives from `ComponentBase`, you need to seriously consider why.

To quote from the source code for `ComponentBase`:

> Most of the developer-facing component lifecycle concepts are encapsulated in this base class. The core components rendering system doesn't know about them (it only knows about IComponent). This gives us flexibility to change the lifecycle concepts easily, or for developers to design their own lifecycles as different base classes.
  
I don't think the author of that comment ever expected `ComponentBase` to dominate the Blazor UI.  I look across the component landscape and see no different base classes.  Here are the base classes for two of the popular Blazor libraries available on the market:

```csharp
public class RadzenComponent : ComponentBase, IDisposable
```

```csharp
public abstract class MudComponentBase : ComponentBase
```

Good developers who understand `ComponentBase` are questioning component usage. They believe simple components are too **expensive**.  They write repetitive code ro avoid building too many components into a page.

My answer: Don't throw away the component: write base components that are fit for purpose.

I have three principle base components.  All are based on what I call the **Lean Mean Green Component** - LMGC from now on - that I'll cover in detail below.

## Lean. Mean. Green Strategies

### Simplify the Lifecycle Process

How many of your components use the full gamat of lifecycle methods?  1%, if that.  Simplify and remove a lot of code and expensive Task construction for no purpose.

### Manage Parameter Changes

When a component is rendered, the renderer must decide whether any child components need re-rendering.  It manages a component's parameter state though a `ParametersView` object.  It checks if any child component parameters have changed, and if so, calls `SetParametersAsync` passing in the `ParametersView` object.

The first line of `SetParametersAsync` uses the `ParametersView` to set the component's parameters.
 
```csharp
        parameters.SetParameterProperties(this);
```
There are two issues with this process.  Neither are simple to address:

1. Setting the parameters is a relatively expensive exercise because `ParameterView` uses reflection to find and assign the parameter values.

2. The method by which `ParameeterView` detects state change is relatively crude.  

Here's the code:

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

Callbacks and RenderFragments are objects and always fail the `IsKnownImmutableType` test.

My strategies are:

1. Stick to Immutable types where possible.
2. Live with it.
3. If a component is being used a lot and performance is an issue, do the assignment and change checking manually.  You can often assume that Callbacks and RenderFragments won't change once initially assigned.
4. Stop unnecessary top down component tree renders caused by events at source. See the next strategy. 

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

Based on what we've discussed above we can build a set of new base components. 

### CoreComponentBase

This is the minimum functionality core component.

What's in it:

1. It inherits from `IComponent`.
2. All the internal class fields are `protected` so can br accessed and set in child components.
2. It has no UI event handler to drive an automatic render request.  Call `StateHasChanged` when you want to make a render request.
3. There's no AfterRender infrastructure.  Implement it if you need to.
4. There are two `StateHasChanged` methods.  
   1. `StateHasChanged` is the same as the familiar `StateHasChanged`.
   2. `InvokeStateHasChanged` ensures `StateHasChanged` is called on the UI thread.
5. There's no lifecycle events.
6. A `BuildRenderTree` method for compatibility with Razor components.
7. It caches `renderFragment` for efficiency.  

```csharp
public abstract class CoreComponentBase : IComponent
{
    protected RenderFragment renderFragment;
    protected internal RenderHandle renderHandle;
    protected bool hasPendingQueuedRender = false;
    protected internal bool hasNeverRendered = true;

    public CoreComponentBase()
    {
        this.renderFragment = builder =>
        {
            hasPendingQueuedRender = false;
            hasNeverRendered = false;
            this.BuildRenderTree(builder);
        };
    }

    protected virtual void BuildRenderTree(RenderTreeBuilder builder) { }

    protected void StateHasChanged()
    {
        if (hasPendingQueuedRender)
            return;

        hasPendingQueuedRender = true;
        renderHandle.Render(this.renderFragment);
    }

    protected void InvokeStateHasChanged()
        => renderHandle.Dispatcher.InvokeAsync(StateHasChanged);

    public void Attach(RenderHandle renderHandle)
        => this.renderHandle = renderHandle;

    public virtual Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        this.StateHasChanged();
        return Task.CompletedTask;
    }
}
```

### UICoreComponentBase

This adds some basic UI functionality.

What's in it:

1. Implements two methods to provide built in efficient functionality to hide the contents of the component.
    1. A `Hidden` Parameter to mimic the hidden html attribute that cn be set externally.
    2. A class `hide` field that can be set internally in children.
2. A `ChildContent` Parameter for component content.

```csharp
public abstract class UICoreComponentBase : CoreComponentBase
{
    protected bool hide;

    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Parameter] public bool Hidden { get; set; } = false;

    public UICoreComponentBase()
    {
        this.renderFragment = builder =>
        {
            hasPendingQueuedRender = false;
            hasNeverRendered = false;
            if (!this.Hidden || !this.hide)
                this.BuildRenderTree(builder);
        };
    }
}
```

### UIComponentBase

`UIComponentBase`adds a single lifefcycle event `OnParametersChangedAsync` with a `bool` to indicate first render.  The return `bool` defines if `StatwHasChanged` is called.  

`OnParametersChangedAsync` can be used to chack what parameters have changed and decide if a render is necessary. 

```csharp
public abstract class UIComponentBase : UICoreComponentBase
{
    protected bool initialized;

    protected virtual ValueTask<bool> OnParametersChangedAsync(bool firstRender)
        => ValueTask.FromResult(true);

    public override async Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);

        var dorender = await this.OnParametersChangedAsync(!initialized)
            || hasNeverRendered
            || !hasPendingQueuedRender;

            if (dorender)
                this.StateHasChanged();

        this.initialized = true;
    }
}
```

### Adding Automated UI Rendering

If you need automated UI rendering, implement `IHandleEvent`.

For a single render:

```csharp
@implements IHandleEvent

//...
@code {
    public async Task HandleEventAsync(EventCallbackWorkItem callback, object? arg)
    {
        await callback.InvokeAsync(arg);
        StateHasChanged();
    }
}
```

For a double event:

```csharp
@implements IHandleEvent

//...
@code {
    public async Task HandleEventAsync(EventCallbackWorkItem callback, object? arg)
    {
        var task = callback.InvokeAsync(arg);
        if (task.Status != TaskStatus.RanToCompletion && task.Status != TaskStatus.Canceled)
        {
            StateHasChanged();
            await task;
        }
        StateHasChanged();
    }
}
```

### Adding OnAfterRender

If you need to implement the `OnAfterRender` event, implement `IHandleAfterRender`.

```csharp
@implements IHandleAfterRender

//...

@code {
    private bool _hasCalledOnAfterRender;

    public Task OnAfterRenderAsync()
    {
        var firstRender = !_hasCalledOnAfterRender;
        _hasCalledOnAfterRender |= true;

        // your code here

        return Task.CompletedTask;
    }
}
```

## Render Cascades

One of the most important strategies to implement is avoiding render cascades.

If you render a component with sub-components that have object parameters, the Renderer will call `SetParametersAsync` on the sub-components regardless of an real state change.  Unless you have implemented stop strategies in those components rendering will cascade down through the tree.

The principle way to minimize this is:
1. To call `StateHasChabged` in the correct point in the render tree.
2. Use base components at the top of the tree that don't automatically trigger render events.

## Some Demonstration Implementations

### The Counter Page

This demonstration shows how to rebuild the Counter page.

#### CounterState

We need a state object to track the counter state,

```csharp
public class CounterState
{
    public int Counter { get; private set; }

    public Action<int>? CounterUpdated;

    public void IncrementCounter()
    {
        this.Counter++;
        this.CounterUpdated?.Invoke(this.Counter);
    }
}
```

#### CounterComponent.razor 

`CounterComponent` displays the Counter.  It inherits from `UIComponentBase` and implements `IDisposable`.

It's a little more intricate than a standard component but is pretty self explanatory.

```csharp
@namespace Blazr.Components
@implements IDisposable
@inherits UIComponentBase

<div class="alert alert-info">
    @this.Counter
</div>

@code {
    [CascadingParameter, EditorRequired] private CounterState State { get; set; } = default!;
    private int Counter;

    protected override ValueTask<bool> OnParametersChangedAsync(bool firstRender)
    {
        if (firstRender)
        {
            if (this.State is null)
                throw new NullReferenceException("State cannot be null in Component");

            this.State.CounterUpdated += this.OnCounterUpdated;
        }
        return ValueTask.FromResult(true);
    }

    private void OnCounterUpdated(int counter)
    {
        this.Counter = counter;
        this.StateHasChanged();
    }

    public void Dispose()
        => this.State.CounterUpdated += this.OnCounterUpdated;
}
```

#### Counter.Razor

`Counter` implements `UICoreComponentBase`: it doesn't need the lifecycle event.  it creates an instance of `CounterState`, cascades it and updates it on the button click.  There are three instances of `CounterComponent` to demonstrate the multi-cast functionality of the event.

I've left the olde counter code in place so you can see that it no longer updates.  `IncrementCounter` no longer triggers a render of the route component, and therfore no longer triggers a render cascade.

```csharp
@page "/counter"
@inherits UICoreComponentBase
<PageTitle>Counter</PageTitle>

<h1>Counter</h1>

<p role="status">Current count: @currentCount</p>
<CascadingValue Value="this.counterState">
    <CounterComponent />
    <CounterComponent />
    <CounterComponent />
</CascadingValue>

<button class="btn btn-primary" @onclick="IncrementCount">Click me</button>

@code {
    private int currentCount = 0;
    private CounterState counterState = new CounterState();

    private void IncrementCount()
    {
        currentCount++;
        this.counterState.IncrementCounter();
    }
}
```

### A Weather Record Viewer

This demonstrates selective rendering in `SetParametersAsync`.  The forward and back buttons move up and down the record set and reload the routw.  The component tracks the current record with `_id` and in `OnParametersChangedAsync` checks the updated parameter `Id`.  It only renders (returns true) when Id has changed.  


```csharp
@page "/WeatherView/{Id:int}"
@inherits UIComponentBase
@inject NavigationManager NavManager

<h3>WeatherViewer</h3>

<div class="row mb-2">
    <div class="col-3">
        Date
    </div>
    <div class="col-3">
        @this.record.Date
    </div>
</div>
<div class="row mb-2">
    <div class="col-3">
        Temperature &deg;C
    </div>
    <div class="col-3">
        @this.record.TemperatureC
    </div>
</div>
<div class="row mb-2">
    <div class="col-3">
        Summary
    </div>
    <div class="col-6">
        @this.record.Summary
    </div>
</div>
<div class="m-2">
    <button class="btn btn-dark" @onclick="() => this.Move(-1)">Previous</button> 
    <button class="btn btn-primary" @onclick="() => this.Move(1)">Next</button>
</div>

@code {
    private int _id;
    private WeatherForecast record = new();

    [Parameter] public int Id { get; set; } = 0;

    protected override async ValueTask<bool> OnParametersChangedAsync(bool firstRender)
    {
        var recordChanged = !this.Id.Equals(_id);

        if (recordChanged)
        {
            _id = this.Id;
            this.record = await GetForecast(this.Id);
        }

        return recordChanged;
    }

    private static async ValueTask<WeatherForecast> GetForecast(int id)
    {
        await Task.Delay(100);
        return new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(id)),
                TemperatureC = id,
                Summary = "Testing"
            };
    }

    private void Move(int value)
        => this.NavManager.NavigateTo($"/WeatherView/{_id + value}");
}
```

## Conclusions

If this article isn't a wake up call to serious Blazor developers, I've failed!

What will it take to get you out of the `ComponentBase` confort zone.  We all think we write lean, mean, efficient code.  But when a lot of it is built on a bloatware base class, aren't we just we kidding ourselves?

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
