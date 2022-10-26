# The Supporting Cast

This section looks at the objects used in the render process.

## Interfaces

We've already seen `IComponent` which is defined as:

```csharp
public interface IComponent
{
    void Attach(RenderHandle renderHandle);
    Task SetParametersAsync(ParameterView parameters);
}
```

Components can also implement two other Renderer related events.

`IHandleEvent` defines a custom event handler for all UI events.

```csharp
public interface IHandleEvent
{
    Task HandleEventAsync(EventCallbackWorkItem item, object? arg);
}
```

`IHandleAfterRender` defines the after render handler.

```csharp
public interface IHandleAfterRender
{
    Task OnAfterRenderAsync();
}
```

## The RenderHandle

The component receives a `RendleHandle` instance when the Renderer calls `Attach`.  It's designed to be stored locally by the component and used to communicate with the Renderer.

To quote Microsoft:

> A RenderHandle structure allows a component to interact with its renderer.

It's primary functionality is:

1. A `Render` method.
2. A property referencing the Dispatcher for the `SynchronisationContext` - i.e. the Dispatcher for the thread on which all UI based code must be run.

## The RenderFragment

It's not a block of UI code.  You can't do this:

```csharp
RenderFragment someUi = "<div>Hello Blazor</div>";
```

To quote the official Microsoft documentation.

*A RenderFragement represents a segment of UI content, implemented as a delegate that writes the content to a RenderTreeBuilder.*

The `RenderTreeBuilder` is even more succinct:

*Provides methods for building a collection of RenderTreeFrame entries.*

A `RenderFragment` is a delegate defined as follows:

```csharp
public delegate void RenderFragment(RenderTreeBuilder builder);
```

If you're new to delegates think of them as a pattern definition.  Any function that conforms to the pattern defined by the `RenderFragment` delegate can passed as a `RenderFragment`.  

The pattern dictates your method must:

1. Have one, and only one, parameter of type `RenderTreeBuilder`.
2. Return a `void`.


This method conforms to the pattern:

```csharp
private void DoNothing(RenderTreeBuilder builder)
{}
```

This is something a little more normal.  Note this defines an anonymous function that is assigned to the class field.

```csharp
    private RenderFragment _childContent => (builder) =>
    {
        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "class", "btn btn-primary");
        builder.AddAttribute(2, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, ButtonClick));
        builder.AddContent(3, "Click Me");
        builder.CloseElement();
    };
```