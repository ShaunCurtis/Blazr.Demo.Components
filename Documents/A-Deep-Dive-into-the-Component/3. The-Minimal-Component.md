# The Minimal Component

In the introduction we saw a very minimal component.  In this chapter we transform that base into a fully functional base component.

Our first pass is to:

1. Capture and save the `RenderHandle`.
2. Render the component whenever `SetParametersAsync` is called.
3. Provide the virtual method for the Razor compiler to override.
4. Make it `abstract` as this is a base class. 

```csharp
public abstract class Minimal1Base : IComponent
{
    protected RenderHandle? renderHandle;

    public void Attach(RenderHandle handle)
        => renderHandle = handle;

    public Task SetParametersAsync(ParameterView parameters)
    {
        // Sets the component parameters to the latest values
        parameters.SetParameterProperties(this);
        // Creates a render fragment as an anonymous function that calls BuildRenderTree
        RenderFragment fragment = (builder) => BuildRenderTree(builder);
        // passes the fragment to the RenderTree to render
        this.renderHandle?.Render(fragment);
        return Task.CompletedTask;
    }

    // This is the method the Razor compiler will override with the render fragment built from the Razor markup
    protected abstract void BuildRenderTree(RenderTreeBuilder builder);
}
```

The Hello World Razor component looks like this:

```html
@inherits RazorClass
<h3>Hello Blazor</h3>
```

## Optmizing MinimalBase

The current code isn't a very efficient.

Consider:

```csharp
RenderFragment fragment = (builder) => BuildRenderTree(builder);
```

The runtime has to build the same anonymous function every time the component renders.  That's a relatively expensive operation.  We can solve that by caching it in the ctor.

First some state fields:

```csharp
    protected RenderHandle renderHandle;
    private bool _renderPending;
    private RenderFragment _componentFragment;
    protected virtual bool hide { get; set; }
```

The render fragment is the code the Render runs.  `hide` provides an efficient way to show/hide the component output.  

```csharp
    public MinimalBase()
    {
        _componentFragment = (builder) =>
        {
            _renderPending = false;
            if (!this.hide)
                BuildRenderTree(builder);
        };
    }
```
The render code can also be improved.  The existing code queues `_componentFragment` regardless of whether it's already queued.

```csharp
this.renderHandle.Render(fragment);
```

The new method uses a private `bool` `_renderPending` to track render state.  If `_componentFragment` is already queued, it doesn't queue it again. The last changes will be applied when the already queued fragment runs.

```csharp
protected void RequestRender()
{
    if (!_renderPending)
    {
        _renderPending = true;
        this.renderHandle.Render(_componentFragment);
    }
}
```
The final base component:

```csharp
public abstract class MinimalBase : IComponent
{
    protected RenderHandle renderHandle;
    private bool _renderPending;
    private RenderFragment _componentFragment;
    protected virtual bool hide { get; set; }
   
    public MinimalBase()
    {
        _componentFragment = (builder) =>
        {
            _renderPending = false;
            if (!this.hide)
                BuildRenderTree(builder);
        };
    }

    public void Attach(RenderHandle handle)
        => renderHandle = handle;

    public Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        this.RequestRender();
        return Task.CompletedTask;
    }

    protected abstract void BuildRenderTree(RenderTreeBuilder builder);

    protected void RequestRender()
    {
        if (!_renderPending)
        {
            _renderPending = true;
            this.renderHandle.Render(_componentFragment);
        }
    }
}
```
## Some Examples

To demonstrate the new base component in action we need to build some real components that inherit from it

Here are two simple but fully functional Bootstrap UI Components:

**BootstrapAlert**
```csharp
@inherits MinimalBase

<div class="alert @this.Colour">@this.Message</div>

@code {
    protected override bool shouldHide => this.Hidden;

    [Parameter] public bool Hidden { get; set; }
    [Parameter] public string Colour { get; set; } = "alert-primary";
    [Parameter] public string Message { get; set; } = "Bootstrap Alert";
}
```
**BootstrapButton**

```csharp
@inherits MinimalBase

<button class="btn @this.Colour" @onclick=this.Clicked >@this.Text</button>

@code {
    protected override bool shouldHide => this.Hidden;

    [Parameter] public bool Hidden { get; set; }
    [Parameter] public string Colour { get; set; } = "btn-primary";
    [Parameter] public string Text { get; set; } = "Button";
    [Parameter] public EventCallback<MouseEventArgs> Clicked { get; set; }
}
```

Here are the two components in action in a test page:

```csharp
@page "/"
@inherits MinimalBase

<BootstrapAlert Hidden=this.hidden Message="Hello Blazor" />

<BootstrapButton Colour="btn-primary" Text="Update" Clicked=this.Clicked />

@code {
    private bool hidden;

    private void Clicked()
    {
        this.hidden = !this.hidden;
        this.RequestRender();
    }
}
```
