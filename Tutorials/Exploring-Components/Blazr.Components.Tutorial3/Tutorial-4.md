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

The normal method to apply the provided `ParameterView` instance to the component in to call `SetParameterProperties` on the `ParameterView` instance [as shown below].

```csharp
public Task SetParametersAsync(ParameterView parameters)
{
    parameters.SetParameterProperties(this);
    Debug.WriteLine($"{Uid} - {this.GetType().Name} - SetParametersAsync Called");
    this.StateHasChanged(); 
    return Task.CompletedTask;
}
```

This is a relatively expensive process: it uses reflection to find and assign values.  If you want to "speed up" the process, you can assign parameters manually.

In our case:

```csharp
private void SetParameters(ParameterView parameters)
{
    this.Message = parameters.GetValueOrDefault<string>("Message") ?? "Not Set";
}
```

And: 

```csharp
public Task SetParametersAsync(ParameterView parameters)
{
    this.SetParameters(parameters);
    Debug.WriteLine($"{Uid} - {this.GetType().Name} - SetParametersAsync Called");
    this.StateHasChanged();
    return Task.CompletedTask;
}
```

It may be more coding intensive, but will speed up rendering.    Think about it for components that are used many times on a page: for instance a column or row component in a grid.
