# Parameters

Our current component renders static content.  It's not lot of use for most situations.  

Our example would be much more generic if we could dynamically change the alert content dynamically.

`Parameters` provide a mechanism to pass data into the component.  They are declared as standard read/write public properties with the `[Parameters] attribute.

Here's an example with the added `EditorRequired` attribute to drive warnings in the Development environment UI.

```csharp
//...
<div class="alert alert-primary m-2">
    @Message
</div>

@code {
    [Parameter, EditorRequired] public string Message { get; set; } = "Not Set";
     //...
}
```

If you now Add the component to `Index`:

```csharp
<ParameterBasicComponent1 Message="Hello Blazor" />
```

When you run this you will see the alert is "Not Set".  The value set in `Index` is not being applied.

Look at `public Task SetParametersAsync(ParameterView parameters)`.  The name provides a lot of information.  When the renderer first attaches a component to the render tree it creates a `ParameterView` instance that contains the initial values of the parameters the component defines.

`SetParametersAsync` is called on a component:
1. When it's first attached to the Render Tree.
2. When it's parent renders and the Renderer detects changes to any sibling component's `ParameterView`.

This is important.  There's no background process "detecting" `Parameter` changes and rendering a component when one changes.

The normal method to apply the provided `ParameterView` instance to the component in to call `SetParameterProperties` on the `ParameterView` instance.  Update `Component` as below:

```csharp
public Task SetParametersAsync(ParameterView parameters)
{
    parameters.SetParameterProperties(this);
    Debug.WriteLine($"{Uid} - {this.GetType().Name} - SetParametersAsync Called");
    this.StateHasChanged(); 
    return Task.CompletedTask;
}
```

While this is the normal approach to updating paranmeters, it is a relatively expensive process: it uses reflection to find and assign values.

You can speed up individual component implementations by assigning  parameters manually.

First change `SetParametersAsync` to `virtual` so we can override it.

```csharp
public virtual Task SetParametersAsync(ParameterView parameters)
```

We can then do this in our component:

```csharp
    public override Task SetParametersAsync(ParameterView parameters)
    {
        this.Message = parameters.GetValueOrDefault<string>("Message");
        return base.SetParametersAsync(ParameterView.Empty);
    }
```
Note:

1. We do the assignment directly using `GetValueOrDefault` on the `ParameterView` object.
2. We call the base method and pass in an empty `ParameterView` object so when if calls `parameters.SetParameterProperties(this)` it doesn't do anything.

It may be more coding intensive, but will speed up rendering. Think about it for components that are used many times on a page: for instance a column or row component in a grid.

> Think: you write the code once, yet it could get run millions of times in your applications lifetime.  

I've also implemented *Show/Hide* in the component.  As this is just html I've usee the `hidden` html attribute.

```csharp
<div hidden="@_hidden" class="alert alert-primary m-2">
    @Message
</div>

@code {
    private bool _hidden => Message is null || Message == string.Empty;
}
```

The final `Index` looks like this and demostrates the functionality we have added.

```csharp
@page "/"

<PageTitle>Index</PageTitle>

<h1>Hello, world!</h1>

<BasicComponent Message=@_message />

<div>
    <button class="btn btn-success" @onclick=Update>Update</button>
    <button class="btn btn-danger" @onclick=Clear>Clear</button>
</div>

@code {
    private string? _message;

    private void Update()
    => _message = $"Updated at {DateTime.Now.ToLongTimeString()}";

    private void Clear()
    => _message = null;
}
```