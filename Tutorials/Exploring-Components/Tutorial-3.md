# Render Fragments

## The RenderFragment

The `RenderFragment` is the building block of components.  It baffles many people.  

Why can't I do this:

```csharp
RenderFrsgment fragment = "<div>Hello</div>";
```
It's not a class, but a `delegate`.  The full definition is:

```csharp
public delegate void RenderFragment(RenderTreeBuilder builder);
```

Compare this with our `BuilderRenderTree`:

```csharp
void BuilderRenderTree(RenderTreeBuilder builder)
```

`BuilderRenderTree` matches the pattern and can therefore be assigned to a `RenderFragment` property or used as a `RenderFragment` arguement.

So:

```csharp
    private RenderFragment _content => BuildRenderTree;
```

Assigns the `RenderTreeBuilder` method to the delegate instance.

In the component we use the `RenderHandle` instance to pass a `RenderFragment` to the Renderer queue.  `BuildRenderTree` defines the code that builds out the visible content of the component.

A `RenderFragment` is a reference.  When it's invokes, it's run in the context of it's owner i.e. the component.

## The Render Queue

It's very important to understand that calling `Render` on the `RenderHandle` doesn't render the component.  It simply places the `RenderFragment`on the Renderer's queue.  The `Renderer` runs a separate process [on the UI context thread] that services the queue.

If your code runs runs synchronously [on the UI thread], the Renderer process only get's time to service that queue when your code completes. We'll look in more detail at the implications shortly.

## Building Render Fragments

There are several ways to build render fragments, some not at all obvious.

In a C# file that's either:

 - In methods such as `RenderTreeBuilder` that fit the `RenderFrsgment` pattern.

```csharp
public void GetContent(RenderTreeBuilder builder)
{
    var message = "Hello Blazor";
    builder.AddContent(0, message);
}
```

Or as anonymous methods assigned as a getters to a property:

```csharp
public RenderFragment MyFragment = (builder) => 
{
    var message = "Hello Blazor";
    builder.AddContent(0, message);
};
``` 

In Razor files you have greater flexibility.  You can use the methods above and create concoctions of C#, html markup and component definitions.

```csharp
    public RenderFragment MyFragment = (builder) =>
    {
        var message = "Hello Blazor";
        <span>@message</span>
    };
``` 

or:

```csharp
public void GetContent(RenderTreeBuilder builder)
{
        var message = "Hello Blazor";
        <span>@message</span>
}
```

And then assign the method to a `RenderFragment`:

```csharp
    public RenderFragment MyFragment1 => this.GetContent;
```

which opens up all sorts of dynamic possibilities:

```csharp
    public RenderFragment MyFragment1 => _isRendering 
        ? this.GetRenderingContent
        : this.GetRenderedContent;
```


## Optimizing our First Component

With this knwowledge we can optimize our first component's rendering.

Building lambda expressions on the fly like this is expensive, and therefore relatively slow.  To solve this, we create the component render fragment and assign it once in the constructor.

```csharp
private Guid Uid = Guid.NewGuid();
private RenderFragment _content;
private bool _renderPending;
private RenderHandle _renderHandle;

public Component()
{
    _content = (builder) =>
    {
        Debug.WriteLine($"{Uid} - {this.GetType().Name} - Rendered");
        _renderPending = false;
        this.BuildRenderTree(builder);
    };

    Debug.WriteLine($"{Uid} - {this.GetType().Name} - Created");
}
```

`StateHasChanged` encapsulates the render logic.  It uses `_renderPending` to track if a render request is already queued.  A queued requeat will render the component with thw current changes, so there is no need to queue a second request.

If no request is pending, we set the flag and queue the request.

```csharp
    public void StateHasChanged()
    {
        if (_renderPending)
        {
            Debug.WriteLine($"{Uid} - {this.GetType().Name} - Render Requested, but onbe is already Pending");
            return;
        }

        Debug.WriteLine($"{Uid} - {this.GetType().Name} - Render Queued");
        _renderPending = true;
        _renderHandle.Render(_content);
    }
```

Finally changes to `SetParametersAsync` to call `StateHasChanged`:

```csharp
    public Task SetParametersAsync(ParameterView parameters)
    {
        Debug.WriteLine($"{Uid} - {this.GetType().Name} - SetParametersAsync Called");
        this.StateHasChanged();
        return Task.CompletedTask;
    }
```
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
