# The Three Component Solution

I have several earlier articles exploring how Blazor components work and why `ComponentBase` is not a very good citizen of the modern world.

In this article I'll decribe how to build three base components you can use in your applications.  They form a hierarchy: changing the inheritance to a higher level base simply adds extra functionality.  The top level component has everything `ComponentBase` has and more.  Consider it as *Black Box Replacement*.  Change the inheritance of say `FetchData` or `Counter` and you won't see a difference.

Before I dive into the detail, consider this simple component which displays a Bootstrap Alert.

```csharp
@if (Message is not null)
{
    <div class="alert @_alertType">
        @this.Message
    </div>
}

@code {
    [Parameter] public string? Message { get; set; }
    [Parameter] public AlertType MessageType { get; set; } = BasicAlert.AlertType.Info;

    private string _alertType => this.MessageType switch
    {
        AlertType.Success => "alert-success",
        AlertType.Warning => "alert-warning",
        AlertType.Error => "alert-danger",
        _ =>  "alert-primary"
    };

    public enum AlertType
    {
        Info,
        Success,
        Error,
        Warning,
    }
}
```

This only uses a small amount of the functionality built into `ComponentBase`.  There's no lifecycle code, UI events or after render code.

Only one sermon:

> Consider how many times instances of this type of component are loaded into memory every day.  And then how many times they get re-rendered.  Lots of calls to lifecycle async methods, constructing and then disposing Task state machines for nothing.  Lot's of memory occupied doing sweet nothing.  That's CPU cycles and memory you (and the planet) are paying for and wasting every second of every day.

Such components need a simpler, smaller footprint base component.

I'll stick my neck out [based on my own experience] and speculate that 99% of all components are candidates for simpler and smaller footprint base components.

## The Components

1. `BlazrUIBase` is a simple UI component with minimal functionality.
 
2. `BlazrControlBase` is a mid level control component with a single lifefcycle method and simple single rendering. 

3. `BlazrComponentBase` is a full `ComponentBase` replacement with some additional Wrapper/Frame functionality.

## BlazrBaseComponent

`BlazrBaseComponent` is a standard class that implements all the basic boiler plate code used by components.  It's abstract and doesn't implement `IComponent`.

It replicates many of the same variables and properties of `ComponentBase`.

The differences are:

1. The `Initialized` flag has changed.  It's reversed and now `protected`, so inheriting classes can access it.  It has a `NotInitialized` opposite, so no need for the awkward `if(!Initialized)` conditional code. 
2. It has a Guid identifier: useful for tracking instances in debugging, and used in some of my more advanced components.
3. It has two `RenderFragments` to implement Wrapper/Frame functionality. `Frame` defines the code to wrap around `Body`. `Frame` is nullable, so if null is not used: the component renders `Body`.

```csharp
public abstract class BlazrBaseComponent
{
    private RenderHandle _renderHandle;
    private RenderFragment _content;
    private bool _renderPending;
    private bool _hasNeverRendered = true;

    protected bool Initialized;
    protected bool NotInitialized => !this.Initialized;

    protected virtual RenderFragment? Frame { get; set; }
    protected RenderFragment Body { get; init; }

    public Guid ComponentUid { get; init; } = Guid.NewGuid();
```

The constructor implements the wrapper functionality.

1. It assigns the render code `BuildRenderTree` to `Body`.
2. It sets up the lambda method assigned to `_content` : the render fragment `StateHasChanged` passes to the Renderer.
3. The lambda method assigns `Frame` to `_content` if it's not null, otherwise it assigns `Body`.
4. It sets `Initialized` to true when it completes.

More about the frame/wrapper functionality later.

```csharp
    public BlazrBaseComponent()
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

The rest of the code is the same as implemented in `ComponentBase`.

`RenderAsync` renders the component immediately.  It works by calling `StateHasChanged` and then yielding by calling `await Task.Yield()`. This frees the UI Synchronisation Context: the Renderer services it's queue and renders the component.

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

Note: there are no lifecycle methods or implementation of `SetParametersAsync`.  It's the responsibility of the individual library classes to implement `IComponent`.  They can choose to lock `SetParametersAsync` by not making it `virtual`.

## BlazrUIBase

This is our simple implementation.

```csharp
public class BlazrUIBase : BlazrBaseComponent, IComponent
{
    public Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        this.StateHasChanged();
        return Task.CompletedTask;
    }
}
```

It inherits from `BlazrBaseComponent` and implements `IComponent`.

1. It has a fixed `SetParametersAsync`: it's can't be overridden.
2. It has no lifecycle methods.  Simple components don't need them.
3. It doesn't implement `IHandleEvent` i.e. it has no UI event handling.  If you need any, call `StateHasChanged` manually.
4. It doesn't implement `IHandleAfterRender` i.e. it has no after render handling.  If you need it, implement it manually.

## BlazrUIBase Demo

We've seen the `BasicAlert` above.  We can go a little further and implement a dismissible `Alert` version.

```csharp
@inherits BlazrUIBase

@if (Message is not null)
{
    <div class="alert @_alertType alert-dismissible">
        @this.Message
        <button type="button" class="btn-close" @onclick=this.Dismiss>
        </button>
    </div>
}

@code {
    [Parameter] public string? Message { get; set; }
    [Parameter] public EventCallback<string?> MessageChanged { get; set; }
    [Parameter] public AlertType MessageType { get; set; } = Alert.AlertType.Info;

    private void Dismiss()
        => MessageChanged.InvokeAsync(null);
    
    //... AlertType and _alertType code
}
```

And the demo `AlertPage`.

```csharp
@page "/AlertPage"
@inherits BlazrControlBase
<PageTitle>Index</PageTitle>

<h1>Hello, world!</h1>

Welcome to your new app.

<div class="m-2">
    <button class="btn btn-success" @onclick="() => this.SetMessageAsync(_timeString)">Set Message</button>
    <button class="btn btn-danger" @onclick="() => this.SetMessageAsync(null)">Clear Message</button>
</div>

<div class="m-3 p-2 border border-1 border-success rounded-3">
    <h5>Dismisses Correctly</h5>
    <Alert @bind-Message=@_message1 MessageType=Alert.AlertType.Success />
</div>

<div class="m-3 p-2 border border-1 border-danger rounded-3">
    <h5>Does Not Dismiss</h5>
    <Alert Message=@_message2 MessageType=Alert.AlertType.Error />
</div>

@code {
    private string? _message1;
    private string? _message2;
    private string _timeString => $"Set at {DateTime.Now.ToLongTimeString()}";

    private Task SetMessageAsync(string? message)
    {
        _message1 = message;
        _message2 = message;
        this.StateHasChanged();
        return Task.CompletedTask;
    }

}
```

There are some important points to digest.

`Alert` implements the *Component Bind* pattern: A `Message` incoming getter parameter and a `MessageChanged` outgoing `EventCallback` setter parameter.   The parent can bind a variable/property to the component like this `@bind-Message=_message`.

`Alert` has a UI event, but there's no `IHandleEvent` handler implemented.  The Render still handles the event: it calls the UI event method directly.  There's automatic call to `StateAsChanged()`. 

In the Demo page there are two instances of `Alert`.  One is wired by the `Message` parameter, two is wired through `@bind-Message`.

When you run the code and click on the buttons, Two doesn't dismiss the Alert.  The're nothing wired to `MessageChanged`.

Intriguingly, One works without any calls to `StateHasChanged`.

`Index` inherits from `BlazrControlBase`, so `StateHasChanged` is automatically called by the UI event handler.

1. The Alert `Dismiss` invokes `MessageChanged` passing a `null` string.
2. The UI handler invokes the Bind handler in `Index`.
3. The Bind handler [created by the Razor Compiler] updates `_message` to `null`.
4. The UI Handler completes and calls `StateHasChanged`.
5. `Index` renders. 
1. The Renderer detects the `Message` parameter on `Alert` has changed.  It calls `SetParametersAsync` on `Alert` passing in the modified `ParameterView`.
7. `Alert` renders: `Message` is `null` so it hides the alert.

> The important lesson to learn is : Always test whether you actually need to call `StateHasChanged`.

### AlertPage Inheriting BlazrUIBase

We can downgrade the inheritance on `AlertPage` to `BlazrUIBase`.  

Once you do so, nothing updates.  No Alert appears because there's no `StateHasChanged()` calls happening [and no UI Render Updates] when UI events occur.

We can fix that by adding calls to `StateHasChanged` where they are needed.

Binding will no longer work as advertised.

Add a handler for the MessageChangedb callback.  Note it calls `StateHasChanged` once it's set `_message1`.  Now, when the component dismisses and `MessageChanged` is invoked, the parent renders and triggers a render of `Alert`.   

```csharp
private Task OnUpdateMessage(string? value)
{
    _message1 = value;
    this.StateHasChanged();
    return Task.CompletedTask;
}
```

Change the binding on the `Alert` component:

```
<Alert @bind-Message:get=_message1 @bind-Message:set=this.OnUpdateMessage MessageType=Alert.AlertType.Success />
```

And Update `SetMessageAsync` to call `StateHasChanged`.

```csharp
private Task SetMessageAsync(string? message)
{
    _message1 = message;
    _message2 = message;
    this.StateHasChanged();
    return Task.CompletedTask;
}
```

## BlazrControlBase

`BlazrControlBase` is the intermediate level component.  It's my workhorse.

It:

1. Implements the `OnParametersSetAsync` lifecycle method.
2. Implements a single render UI event handler.
3. `SetParametersAsync` is fixed, you can't override it.

```csharp
public abstract class BlazrControlBase : BlazrBaseComponent, IComponent, IHandleEvent
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

Consider.

You can now do this, which makes `OnInitialized{Async}` redundant.

```csharp
   protected override async Task OnParametersSetAsync()
    {
        if (this.NotInitialized)
        {
            // do initialization stuff here
        }
    }
```

You don't need a *sync* version of `OnParametersSet()`.  There's no difference in overhead between:

```csharp
private Task DoParametersSet()
{
    OnParametersSet();
    return OnParametersSetAsync();
}

protected virtual void OnParametersSet()
{
    // Some sync code
}

protected virtual Task OnParametersSetAsync()
    => Task.CompletedTask;
```

And:

```csharp
protected virtual Task OnParametersSetAsync() 
{
    // some sync code
    return Task.CompletedTask;
}
```

I'd like to make it return a `ValueTask`, but we loose compatibility. 

## BlazrControlBase Demo

The demo page looks like a normal `ComponentBase` page.  That's intentional.  The component now has access to the initialization state of the component though `Initialized`.

### Modified Weather Forecast Data Pipeline

First the modified Weather Forecast data class and service.

```csharp
public class WeatherForecast
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public int TemperatureC { get; set; }
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
    public string? Summary { get; set; }
}
```

```csharp
namespace Blazr.Server.Web.Data;

public class WeatherForecastService
{
    private List<WeatherForecast> _forecasts;
    private static readonly string[] Summaries = new[]
        { "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"};

    public WeatherForecastService()
        => _forecasts = this.GetForecasts();

    public async Task<IEnumerable<WeatherForecast>> GetForecastsAsync()
    {
        await Task.Delay(100);
        return _forecasts.AsEnumerable();
    }

    public async Task<WeatherForecast?> GetForecastAsync(int id)
    {
        await Task.Delay(100);
        return _forecasts.FirstOrDefault(item => item.Id == id);
    }

    private List<WeatherForecast> GetForecasts()
    {
        var date = DateOnly.FromDateTime(DateTime.Now);
        return Enumerable.Range(1, 10).Select(index => new WeatherForecast
        {
            Id = index,
            Date = date.AddDays(index),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        }).ToList();
    }
}
```

### WeatherForecastViewer

I want to demonstrate various features so there's a set of buttons that use routing [rather than a button event handler that just updates the id and display].  They all route to the same page and just modify the Id - `/WeatherForecast/1`.

The markup is self-evident.  It's not efficient: it's keep it simple demo code.

The code I want to look at in detail is `OnParametersSetAsync`.

1. It uses `NotInitialized` to only get the WeatherForecast list on initialization.  In `ComponentBase` thia code would have been in `OnInitializedAsync`.
2. It checks the Id status: `hasRecordChanged`.  I use a bool here so we are clear what's happening.  Your code should be expressive: the compiler will optimize this, you don't need to. 
3. It only gets the new record if the Id has changed.

```csharp
@page "/WeatherForecast/{Id:int}"
@inject WeatherForecastService service
@inherits BlazrControlBase

<h3>Country Viewer</h3>

<div class="bg-dark text-white m-2 p-2">
    @if (_record is not null)
    {
        <pre>Id : @_record.Id </pre>
        <pre>Name : @_record.Date </pre>
        <pre>Temp C : @_record.TemperatureC </pre>
        <pre>Temp F : @_record.TemperatureF </pre>
        <pre>Summary : @_record.Summary </pre>
    }
    else
    {
        <pre>No Record Loaded</pre>
    }
</div>

<div class="m-3 text-end">
    <div class="btn-group">
        @foreach (var forecast in _forecasts)
        {
            <a class="btn @this.SelectedCss(forecast.Id)" href="@($"/WeatherForecast/{forecast.Id}")">@forecast.Id</a>
        }
    </div>
</div>
@code {
    [Parameter] public int Id { get; set; }

    private WeatherForecast? _record;
    private IEnumerable<WeatherForecast> _forecasts = Enumerable.Empty<WeatherForecast>();

    private int _id;

    private string SelectedCss(int value)
        => _id == value ? "btn-primary" : "btn-outline-primary";

    protected override async Task OnParametersSetAsync()
    {
        if (NotInitialized)
            _forecasts = await service.GetForecastsAsync();

        var hasRecordChanged = this.Id != _id;

        _id = this.Id;

        if (hasRecordChanged)
            _record = await service.GetForecastAsync(this.Id);
    }
}
```

### `BlazrComponentBase`

The full `ComponentBase` implementation is too long to include here: it's in the Appendix.


## The Extra BaseComponent Features

All the base components come with some extras.

## The Wrapper/Frame Functionality

A Demo `Wrapper` component.  

Note the wrapper is defined in the `Frame` render fragment, and uses the Razor built-in `__builder` RenderTreeBuilder instance.

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

### RenderAsync

When you move to the single render-on-completion or manual render UI event handling, you [the coder] get control of when you do intermediate renders.  This is where `RenderAsync` comes in.  When you call it [in Task based methods] it ensures the component is rendered immediately.

The following page demonstrates:

```
@page "/Load"
@inherits BlazrControlBase
<h3>SequentialLoadPage</h3>

<div class="bg-dark text-white m-2 p-2">
    <pre>@this.Log.ToString()</pre>
</div>
@code {
    private StringBuilder Log = new();

    protected override async Task OnParametersSetAsync()
    {
        await GetData();
    }

    private async Task GetData()
    {
        for(var counter = 1; counter <= 10; counter++)
        {
            this.Log.AppendLine($"Fetched Record {counter}");
            await this.RenderAsync();
            await Task.Delay(500);
        }
    }
}
```

Miss out `await this.RenderAsync();` and you only get the final result.  If you ran this code in `CompoinentBase` you would get the first render, and then nothing would happen till the last.  Comment out `RenderAsync`, change the inheritance and try it. 

## Manually Implementing OnAfterRender

If you need to implement `OnAfterRender` it's relatively simple.


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

Hopefully I've demonstrated why there's no need to use that expensive `ComponentBase` in your Blazor applications.  Take the plunge.

The three components I'vw shown are upwardly compatible.  If there's not enough functionality in one move up.

Once you start using them, you'll find that `BlazrControlBase` satisfies almost all your needs.  Confession: I never use `BlazorComponentBase`

## Appendix

The full class code for `BlazrComponentBase`.

```csharp
public class BlazrComponentBase : BlazrBaseComponent, IComponent, IHandleEvent, IHandleAfterRender
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
        if (this.NotInitialized)
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
