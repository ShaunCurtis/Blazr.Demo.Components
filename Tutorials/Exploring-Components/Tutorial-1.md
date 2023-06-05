# Exploring Components - What is a Component?

A component is a class that implements the `IComponent` interface.

So if it's a class, what is a *Razor Component*?

As a programmer you have two ways to define a component.

1. Firstly you can define a C# class like this:

```csharp
using Microsoft.AspNetCore.Components;

public class Basic1 : IComponent
{
    public void Attach(RenderHandle renderHandle) { }

    public Task SetParametersAsync(ParameterView parameters)
        =>  Task.CompletedTask;
}
```

> Make sure you use the correct `IComponent` definition - `using Microsoft.AspNetCore.Components`

And then add it to `Index`.

It doesn't do anything, but it runs.

2. Secondly you can define a component in a Razor file.  This one is the template produced when you add one.

```csharp
<div class="alert alert-primary m-2">
    Hello Blazor
</div>

@code { }
```

Add it to `Index` and it will display the header.

During compelation the Razor file gets compiled by the Razor compiler into a C# file.  It takes your collection of C#, Html markup and component definitions and turns it into blocks of `RenderTreeBuilder` logic and standard C# code.  We'll look at how to view this generated 
code a little later.

By default the Razor compiler uses `ComponentBase`as its inherited class.  If you want to use a different class, you need to specify it.

We will use Razor components throughout the article, so let's look at how we can change that behaviour.

> All components in the repository accompanying this article are in the *Components* directory and thus in the *Project/Components* namespace.

Add a `RazorBase` class.  It's abstract so can't be used directly.

```csharp
public abstract class RazorBase
{
    public abstract void BuilderRenderTree(RenderTreeBuilder builder); 
}
```

Modify our test Razor component to inherit from it.

```csharp
@inherits RazorBase

<div class="alert alert-primary m-2">
    Hello Blazor
</div>

@code {
}
```

This code compiles, but you can't add the component to `Index`.  The editor dosen't recognise it as a component as it doesn't implement `IComponent`. 

## Our First Rendering Component

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

### The RenderFragment

`RenderFragment` is the building block of components.  Many people are baffled by it.  It's not a class, but a `Delegate`.  The full definition is:

```csharp
public delegate void RenderFragment(RenderTreeBuilder builder);
```

Compare this with our `BuilderRenderTree`:

```csharp
void BuilderRenderTree(RenderTreeBuilder builder)
```

`BuilderRenderTree` fits the pattern and can therefore be assigned to a property or arguement defined as a `RenderFragment`.

So:

```csharp
    private RenderFragment _content => BuildRenderTree;
```

Assigns the `RenderTreeBuilder` method to the delegate instance.

In the component we use the `RenderHandle` instance to pass the `RenderFragment` to the Renderer queue.  `BuildRenderTree` contains the code that builds out the visible content of the component.

Note that a `RenderFragment` is really just a reference.  The code within the fragment is run in the context of it's owner i.e. the component.

### The Render Queue

It's important to understand that calling `Render` on the `RenderHandle` doesn't render the component.  It simply places the `RenderFragment` in the Renderer's queue.  A separate `Renderer` process [running on the UI context thread] services that queue.

To service the queue, the Renderer needs thread time.  If your code runs runs synchronously, it only get's that time when your code completes. We'll look in more detail at the implications shortly.
