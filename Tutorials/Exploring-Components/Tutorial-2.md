# Exploring Components - Our First Component

First we need a base class to implement the boilerplate component code: thia is `Component`.  It has no content, so is implemented as a class.

Our component implements `IComponent` and inherits from `RazorBase`.  For debugging purposes, it implements a `Guid` based `Uid` to track instances.

```csharp
public class Component : RazorBase, IComponent
{
    private Guid Uid = Guid.NewGuid();

    public Component()
        => Debug.WriteLine($"{Uid} - Basic Component Created");
}
```

Add the `RazorBase` abstract method as a `virtual`:

```csharp
protected override void BuildRenderTree(RenderTreeBuilder builder)
{ }
``` 

Finally implement the `IComponent` interface::

1. Capture the `RenderHandle` in `_renderHandle`.
2. Call `_renderHandle.Render` and pass in the Razor defined `BuildRenderTree` method.
3. Add `Debug` output to track the processes. 

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

Add the component to `Index`, run the code and switch between pages. Yooui will see something like this:

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

Tutorial List:

1. [Introduction](./Introduction.md)
2. [What is a Component?](./Tutorial-1.md)
3. [Our First Component](./Tutorial-2.md)
4. [RenderFragments](./Tutorial-3.md)
5. [Parameters](./Tutorial-4.md)
6. [UI Events](./Tutorial-5.md)
7. [Component Lifecycle Methods](./Tutorial-6.md)
8. [The Rest](./Tutorial-7.md)
9. [Summary](./Final-Summary.md)
