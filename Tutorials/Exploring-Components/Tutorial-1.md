# Exploring Components - What is a Component?

A component is a class that implements the `IComponent` interface.

The difference between a component and a standard class is context.  You can create an instance of a component, but that won't get it rendered.  You can't pass an instance of a component class to the Renderer.  Components are created and destroyed by the Renderer. 

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

It doesn't do anything, but it compiles and runs.

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

We use Razor components throughout the article, so let's look at how we can change that behaviour.

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

This code compiles, but you can't add the component to `Index`.  The editor dosen't recognise it as a component: it doesn't implement `IComponent`. 

We'll look a how to da that next - [Our First Component](./Tutorial-2.md)

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
