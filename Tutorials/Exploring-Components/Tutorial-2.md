# Exploring Components - Our First Component

First we need to create a base class to implement the boilerplate code our components will use called `Component`.  It will contain no content so we'll implement it as a class.

Our component implements `IComponent` and inherits from `RazorBase`.  It has a `Guid` Uid for debug tracking and outputs debug data to *Output*.

```csharp
public class Component : RazorBase, IComponent
{
    private Guid Uid = Guid.NewGuid();

    public Component()
        => Debug.WriteLine($"{Uid} - Basic Component Created");
}
```

Add in the `RazorBase` abstract method as a `virtual`:

```csharp
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    { }
``` 

Finally we need to implement the `IComponent` interface.  We:

1. Capture the `RenderHandle` into `_renderHandle`.
2. Call `_renderHandle.Render` and pass in the `BuildRenderTree` method.
3. Add `Debug` output so we can see each each process happening. 

```csharp
    public void Attach(RenderHandle renderHandle)
    {
        _renderHandle = renderHandle;
        Debug.WriteLine($"{Uid} - Basic Component Attached");
    }

    public Task SetParametersAsync(ParameterView parameters)
    {
        _renderHandle.Render(BuildRenderTree);
        Debug.WriteLine($"{Uid} - Basic Component Rendered");
        return Task.CompletedTask;
    }
```

If you now add the component to `Index`, run the code and switch between pages you'll see something like this:

```text
ae5f83 - Basic Component Created
ae5f83 - Basic Component Attached
ae5f83 - Basic Component Rendered
// page away and then back
6baf18 - Basic Component Created
6baf18 - Basic Component Attached
6baf18 - Basic Component Rendered
```

 A new component is created when you navigate to the page.

