# Exploring Component Rendering

Blazor ships with a single developer "Component".  If you add a Razor file it inherits from it by default.

`ComponentBase` rules the Blazor UI world.  You don't have to use it, but probably 99.x% of all developer built components either inherit directly or indirectly from it.

In this article, I will explore how components that inherit from `ComponentBase`render.  You can find another article here that looks at building alternative leaner and meaner base components.

This article is set in the context of `Counter` page.  We will look at how, why and when rendering occurs and how we can take greater control of the process.

Why should we?  

1. The Render process is expensive: it uses a lot of CPU cycles. 

2. In server mode. whether you pay for cycles, or assign resources to containers or virtual machines it matters.

3. In WASM mode running in the browser it affects performance.

4. In either it can affect you coding decisions.  How many components do I have in this page, will it affect to UX?  Should I revert to repetitive html code for performance? 

## The Render Cascade

It's easier to demonstrate this that describe it in detail.

First an object to hold our data.  Why this is a record rather than a class will become evident later.  And yes, in this instance why do we need an object, we only have one piece of data.  True, but not often the case in real life.

```csharp
public record CounterData(int Counter);
```

For the purposes of this article we add some code to each component to capture the time when the component's parameters are updated and when a request is made to render the component.

The code:

1. Parameter Set time is captured by overriding `SetParametersAsync` and looging the time.
2. Parameter Render request time is set by overriding `ShouldRender` and logging the time the method is called.  `ShouldRender` is not called on the first render, so we override `OnInitializedAsync` and add the yielding `Task.Delay` to cause a double render: once on the yield and second when the await completes.

```csharp
private string ParameterSetTime = string.Empty;
private string RenderTime = string.Empty;
    
public override Task SetParametersAsync(ParameterView parameters)
{
    parameters.SetParameterProperties(this);
    this.ParameterSetTime = DateTime.Now.ToLongTimeString();
    return base.SetParametersAsync(ParameterView.Empty);
}

protected override Task OnInitializedAsync()
    => Task.Delay(1);

protected override bool ShouldRender()
{
    this.RenderTime = DateTime.Now.ToLongTimeString();
    return base.ShouldRender();
}
```

And the UI code:

```csharp
<div class="bg-dark text-white p-1 m-1 mt-0">
    Parameter Set Time : @this.ParameterSetTime  -  Rendered at : @this.RenderTime
</div>
```

Two components:

`IntDisplay`

```caharp
<div class="bg-secondary text-white m-1 mb-0 p-1">
    <h5>@this.Title</h5>
    <div>Counter : @Data </div>
</div>
<div class="bg-dark text-white p-1 m-1 mt-0">
    Parameter Set Time : @this.ParameterSetTime  -  Rendered at : @this.RenderTime
</div>


@code {
    [Parameter, EditorRequired] public int Data { get; set; } = 0;
    [Parameter] public string Title { get; set; } = "Int Display";

    private string ParameterSetTime = string.Empty;
    private string RenderTime = string.Empty;

    public override Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        this.ParameterSetTime = DateTime.Now.ToLongTimeString();
        return base.SetParametersAsync(ParameterView.Empty);
    }

    protected override Task OnInitializedAsync()
        => Task.Delay(1);

    protected override bool ShouldRender()
    {
        this.RenderTime = DateTime.Now.ToLongTimeString();
        return base.ShouldRender();
    }
}
```

And `BasicCounterDisplay`:

```csharp
<div class="bg-secondary text-white m-1 mb-0 p-1">
    <h5>@this.Title</h5>
    <div>Counter : @this.Data.Counter </div>
</div>
<div class="bg-dark text-white p-1 m-1 mt-0">
    Parameter Set Time : @this.ParameterSetTime  -  Rendered at : @this.RenderTime
</div>


@code {
    [Parameter, EditorRequired] public CounterData Data { get; set; } = new(Counter: 0);
    [Parameter] public string Title { get; set; } = "Counter Display";

    private string ParameterSetTime = string.Empty;
    private string RenderTime = string.Empty;

    protected override Task OnInitializedAsync()
        => Task.Delay(1);

    public override Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        this.ParameterSetTime = DateTime.Now.ToLongTimeString();
        return base.SetParametersAsync(ParameterView.Empty);
    }

    protected override bool ShouldRender()
    {
        this.RenderTime = DateTime.Now.ToLongTimeString();
        return true;
    }
}
```

Our new `Counter` component:

```csharp
@page "/c1"
<PageTitle>Counter</PageTitle>

<h1>Basic Counter</h1>

<IntDisplay Data=this.currentCount />
<BasicCounterDisplay Data=this.data />

<button class="btn btn-primary ms-2 me-2" @onclick="IncrementCount">Int Counter</button>
<button class="btn btn-primary me-2" @onclick="IncrementCount1">Counter 1</button>

<div class="bg-dark text-white p-1 mt-5">
    Parameter Set Time : @this.ParameterSetTime  -  Rendered at : @this.RenderTime
</div>


@code {
    private int currentCount = 0;
    private CounterData data = new(Counter: 0);

    private void IncrementCount()
        => currentCount++;

    private void IncrementCount1()
        => data = data with {Counter = data.Counter + 1 };

    private string ParameterSetTime = string.Empty;
    private string RenderTime = string.Empty;
    
    public override Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        this.ParameterSetTime = DateTime.Now.ToLongTimeString();
        return base.SetParametersAsync(ParameterView.Empty);
    }

    protected override bool ShouldRender()
    {
        this.RenderTime = DateTime.Now.ToLongTimeString();
        return base.ShouldRender();
    }
}
```

