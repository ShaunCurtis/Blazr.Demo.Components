# Introduction

## What is a Component?

Microsoft defines:

*A component is a self-contained portion of user interface (UI) with processing logic to enable dynamic behavior. Components can be nested, reused, shared among projects, and used in MVC and Razor Pages apps.*

*Components are implemented using a combination of C# and HTML markup in Razor component files with the .razor file extension.*

What is does rather than what it is, and not all strictly true.

From a programming perspective, a component is:

1. A class.
2. Implements `IComponent`.

That's it.

This code fulfills the minimum requirements and will compile and work.  Add it to page and you won't see anything because it doesn't do anything!

```csharp
public class Minimal : IComponent
{
    public void Attach(RenderHandle handle)
    { }

    public Task SetParametersAsync(ParameterView parameters)
        =>  Task.CompletedTask;
}
```

To get some output we need to add a one line of code:

```csharp
public class Minimal : IComponent
{
    public void Attach(RenderHandle handle)
        =>  handle.Render( (builder) => builder.AddMarkupContent(0, "<h1>Hello from Minimal</h1>") );

    public Task SetParametersAsync(ParameterView parameters)
        => Task.CompletedTask;
}
```

## The Renderer and the Render Tree

A detailed description of how the `Renderer` and `RenderTree` works is beyond the scope of this article, but you need a basic grasp of the concepts to understand the rendering process.

To quote the MS Documentation:

>The `Renderer` provides mechanisms:
>1. For rendering hierarchies of `IComponent` instances;
>2. Dispatching events to them;
>3. Notifying when the user interface is being updated.


The `Renderer` and `RenderTree` reside in the Client Application in WASM and in the SignalR Hub Session in Server, i.e. one per connected Client Application.

The Renderer defines and manages the UI - the DOM [Document Object Model] - in a `RenderTree`. Think of the `RenderTree` as a tree with one or more components attached to each branch.

The `Renderer` has a `RenderQueue` of  `RenderFragments`, which it continuously services.  Components place `RenderFragments` in the queue and the Renderer runs the code within the fragment to update the component's section of the DOM.  The `Renderer` uses a diffing process to detect changes in the DOM caused by `RenderTree` updates.  It passes these changes to the client code to implement in the Browser DOM and update the displayed page.

The diagram below is a visual representation of the render tree for the out-of-the-box Blazor template.

![Root Render Tree](https://shauncurtis.github.io/articles/assets/Blazor-Components/Root-Render-Tree.png)

