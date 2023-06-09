# Render Fragments

## The RenderFragment

The `RenderFragment` is the building block of components.  Many people are baffled by it.  It's not a class, but a `Delegate`.  The full definition is:

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

## The Render Queue

It's important to understand that calling `Render` on the `RenderHandle` doesn't render the component.  It simply places the `RenderFragment` in the Renderer's queue.  A separate `Renderer` process [running on the UI context thread] services that queue.

To service the queue, the Renderer needs thread time.  If your code runs runs synchronously, it only get's that time when your code completes. We'll look in more detail at the implications shortly.

## Building Render Fragments

There are several ways to build render fragments, some that aren't obvious until you try them.

In a C# file the only way is standard C# code and `RenderTreeBuilder` methods.

You can assign anonymous methods as a getter to a readonly property:

```csharp
public RenderFragment MyFragment = (builder) => 
{
    var message = "Hello Blazor";
    builder.AddContent(0, message);
};
``` 

Or define a method:

```csharp
public void GetContent(RenderTreeBuilder builder)
{
    var message = "Hello Blazor";
    builder.AddContent(0, message);
}
```

In Razor files you have greater flexibility and can create concoctions of C#, html markup and component definitions that the Razor Compiler can intepret and compile.

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

We've added a `_renderPending` flag which is reset when the actual code is run by the renderer.

We can also now implement `StateHasChanged` to encapsulate the render logic.  It uses `_renderPending` to determine if a render request is already queued.  A queued requeat will render the component with thw current changes, so there is no need to queue a second request.

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
