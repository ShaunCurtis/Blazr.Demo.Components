# Razor Components

Developers write most components in Razor.  There's plenty of reference material on writing in Razor, I'll not regurgitate what you can read elsewhere.  What I want to look at is the relationship between Razor Components, Blazor Components and `IComponent`?

The truth is there isn't one.

To demonstrate here's a base class that can be used as `@inherits` in a Razor component:

```csharp
public abstract class RazorBase
{
    protected abstract void BuildRenderTree(RenderTreeBuilder builder);

    public RenderFragment Content => (builder) => BuildRenderTree(builder);
}
```

And a test Razor component:

```csharp
// Div.razor
@inherits RazorBase

<h3>My Razor Class Div</h3>
``````
It will compile and you can reference the class, but you can't use it as a component.

You can however do this in a page:

```csharp
@(new Div().Content)
```

The only requirement for inheritance in a Razor component is that the inherited class implements:

```csharp
protected virtual void BuildRenderTree(RenderTreeBuilder builder);
```

When the Razor compiler compiles the Razor markup, it overrides `BuilderRenderTree` with the Razor compiled RenderTreeBuilder instruction code.

What normally happens is no inheritance is explicitly defined, so the compiler sets inheritance to `ComponentBase`.

## Building Render Fragments

There are two ways to build render fragments.

Firstly we can use the RenderTreeBuilder.  This is how the Razor compiler builds a class from a Razor component file. 

```csharp
@inherits RazorClass

@HelloWorld

@code {
    protected RenderFragment HelloWorld => (RenderTreeBuilder builder) =>
    {
        builder.OpenElement(0, "div");
        builder.AddContent(1, "Hello Razor 2");
        builder.CloseElement();
    };
}
```

This defines a `RenderFragment` property and assigns to it a block of code in an anonymous method that conforms to the `RenderFragment` pattern.  It takes a  `RenderTreeBuilder` and has no return so returns a void.  It uses the provided `RenderTreeBuilder` object to build the content: a simple hello world html div.  Each call to the builder adds what is called a `RenderTreeFrame`.  Each frame is compile time sequentially numbered. Do not use run time counter to assign this number.

Or like this:

```csharp
@inherits MinimalBase

@HelloWorld

@code {
    protected RenderFragment HelloWorld => (RenderTreeBuilder builder) =>
    {
        <div>Hello Blazor 3</div>
    };
}
```

Here we're mixing C# code and markup.  The Razor compiler recognises this and compiles the code correctly.

It's important to understand two points:
1. The component itself never "runs" the `RenderFragement`.  It is passed to the Renderer which Invokes it.
2. Even though the `Renderer` invokes the code, the code is run in the context of the component, and uses the state of the component when it executes.

The functionality above can be defined directly in a class.

```csharp
public class DivClass : MinimalBase
{
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddContent(1, "Hello Razor 2");
        builder.CloseElement();
    }
}
```
## ChildContent

Consider the following markup:

```csharp
<MinimalComponent>
    Value: @this.Value
</MinimalComponent>
@code {
    private int Value = 4;
}
```

There is content relating to the parent component within `MyControl`.  In the above code the Razor compiler will expect `MyControl` to have a `RenderFragment` parameter named `ChildContent` where it places the compiled fragment that represents the content.  If one doesn't exist, the runtime will throw an error.

```
Message	"Object of type 'Blazr.Components.Components.MinimalComponent' does not have a property matching the name 'ChildContent'."
```

To understand this, here's the actual class the Razor compiler builds.  I've removed all the commenting and refactored it a little to make it more readable.

```csharp
[RouteAttribute("/")]
public partial class Test : MinimalBase
{
    protected override void BuildRenderTree(RenderTreeBuilder __builder)
    {
        __builder.AddMarkupContent(1, "\r\n\r\n");
        __builder.OpenComponent<MinimalComponent>(2);
        __builder.AddAttribute(3, "ChildContent", __content2);
    }

    private RenderFragment __content2 => __builder2 =>
    {
        __builder2.AddMarkupContent(4, "\r\n    Value : ");
        __builder2.AddContent(5, this.Value);
    };

    private int Value = 4;
}
```

The compiler has:

1. Compiled the markup into the parent's `BuildRenderTree` method as a set of `RenderTreeBuilder` methods.
2. Compiled the code block defined within `<MininalComponent>` into a RenderFragment defined in the parent class.
3. Used the `OpenComponent` method on `RenderTreeBuilder` to define the `MinimalComponent`.
4. Defined the `ChildContent` attribute/parameter on `MinimalComponent` as the `RenderFragment` defined in `__content2. 

The reason why you get a runtime error and not a compiler error is that the compiler doesn't know that the `MinimalComponent` doesn't have a `ChildContent` attribute.  It can't differentiate correctly between attributes and parameters at compile time.

You can also specify which `RenderFragment` to use like this:

```csharp
<MinimalComponent>
    <ChildContent>
        Value: @this.Value
    </ChildContent>
</MinimalComponent>
```

A component is not limited to a single `RenderFragment`.  A table component could look like this:

```html
<TableComponent>
    <Header>
        ...
    </Header>
    <Rows>
        ...
    </Rows>
    <Footer>
        ...
    </Footer>
</TableComponent>
```
