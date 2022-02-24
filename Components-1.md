# Components Part 1

In Blazor, like many other SPAs, components aren't just part of the UI, they are the UI.

They are deceptive. Start up your first Blazor project and they seem relatively simple.  But once you get started and add some complexity, things start to not happen the way you think they should.  You want component A, B and C to communicate with each other.  You get lost in the complexity of the wiring and the state of your data.

This article is here to help clear that fog, show you fundimentally how components work, how to decouple your data from yor UI and how to communicate state changes.

## Demo Project

I'll use a demo solution in parts of this article.  The starting point is the out-of-the-box Blazor Server template.

## Component Fundimentals

What is a component?

1. It's a class.
2. It implements the `IComponent` interface.  This is the interface the Renderer uses to communicate with the component.

That's it, and to prove it let's create a serious bare bones component.

Add a C# class to a *Components* folder.  The base code is shown below.

```csharp
using Microsoft.AspNetCore.Components;

namespace Blazr.Components;

public class DivClass : IComponent
{
    private RenderHandle _renderHandle;

    public void Attach(RenderHandle renderHandle)
    {
        _renderHandle = renderHandle;
    }

    public Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        return Task.CompletedTask;
    }
}
```

It implements `IComponent`.  There are two methods:

1. `Attach` is called after the class in instantiated, when the Renderer "attaches" the class to the render tree.  It passes the class a `RenderHandle`: the `Dispatcher` property gives the component access to the thread dispatcher, we'll see where we use this later, and the `Render` method loads a `RenderFragment` onto the Renderer's queue.  In the class we assign the passed `RenderFragment` reference to a local private field.

2. `SetParametersAsync` is called after `Attach` and then when any class defind `Parameters` have changed.  In the class we assign the passed in data in the `ParameteVies` object to the local properties.  In this instance there aren't any.  Note here that's it's very important to do this as the first line in `SetParametersAsync` to free up the resource to the Renderer.  I'll show you later how to call `base.SetParametersAsync` if you need to.

The important points to note from this are:

1. Component to Renderer communication is through `RenderHandle.Render`.  The component passes the Renderer `RenderFragmebts` to process.  The component doesn't do any rendering, the Renderer does.
2. Renderer to Component communication after the initialization process is through `SetParametersAsync`.  The Renderer tells the component that one or more of it's parameter defined properties has changed.  It's up to the component to decide what to do about it.

If you add this component to the `Index` page everything will compile and run, but there will be no output.  If you place break points in `Attach` and `SetParametersAsync`, you will see them get hit.

```
ssd
```


### The RenderFragment

Perhaps the most inportant component concept to understand.

A `RenderFragment` is a delegate.

```csharp
public delegate void RenderFragment(RenderTreeBuilder builder);
```

Any method that matches the delegate pattern can be assigned to a delegate.

So:

```csharp
private void BuildComponent(RenderTreeBuilder builder)
{
   builder.AddMarkupContent(0, "<div><h3>Hello Blazor</h3></div>");
}
```

Can be assigned as follows:

```csharp
private RenderFragment _renderFragment;

public DivClass()
{
    _renderFragment = BuildComponent;
}
``` 

And then used like this:

```csharp
public Task SetParametersAsync(ParameterView parameters)
{
    parameters.SetParameterProperties(this);
    _renderHandle.Render(_renderFragment);
    return Task.CompletedTask;
}
```

If you add this code to the class you will get some rendered content.

