# Exploring The Blazor Component

The Blazor component, how it works and how it fits into the Blazor Framework is something of an enigma to many.  What a first introduction in one of the Blazor templates seems a relatively simple and benign animal takes on a very different character as the complexity increases.

## Who controls Components?

Before we dive into the component itself, we need to understand who creates and manages the component lifecycle.  It's not us, the coder.  We can't create an instance of a component and then pass it into the render process.

Consider this code.  This is a section of a `RenderFragment` where `builder` is a `RenderTreeBuilder`.

The code tells the Renderer to add a div and inside that div place a component of type `MyComponent`.

```csharp
builder.OpenElement(0, "div");
builder.OpenComponent<MyComponent>(1, "div");
//....
builder.CloseComponent();
builder.CloseElement();
```
If on the next time the RenderFragment is run it looks like this, then the Renderer disposes of the component [if it needs to] and releases it's reference to it.

```csharp
builder.OpenElement(0, "div");
builder.CloseElement();
```

## The Events that Shape a Component

There are two processes that drive activity in a component:

1. The construct/SetParameters process which occurs as part of the render cycle.
2. The UI event process where the Renderer calls an event handler in the component in response to UI activity : button clicks and OnAfterRender are examples.

It's important to understand this separation.

### Construct/SetParameters Process

We've seen above how the renderer knows when to create a component.

When the component doesn't already exist, the Renderer:

1. Creates an instance of the component.  Any constructor must have no arguments: `new()`.
2. Sets the `Inject` labelled properties by getting the relevant DI instances, and assigning them through reflection.  It raises an exception if no DI service exists. 
3. It maps the component to the relevant place in the RenderTree and calls `Attach`.  The component gets a `RenderHandle` that it stores locally.  The `RenderHandle` is the component's only communications channel with the Renderer.
4. It builds a list of all the `[Parameter]` and `[CascadingParameter]` properties defined in the component.
5. It calls `SetParametersAsync` on the component, passing in a `ParameterView` instance with all the current values for the defined parameters. 

When the component exists, the Renderer:
1. Runs a comparison over the Parameters list it holds for the component.
2. Calls `SetParametersAsync` if anything has changed, passing in the updated list as a  `ParameterView`.  We'll discuss this process in detail later.  

When the component is no longer in the RenderTree it:

1. Calls `Dispose/DisposeAsync` if the component implements `IDisposable/IAsyncDisposable`.
2. Drops any references to the component.  The GC can destroy the instance and clean up. 

We'll look at `SetParametersAsync`` in more detail shortly.

### UI Events

The Renderer holds a map of events that the component has defined and the event handlers it must call when they occur.

There are two types with defined handlers:

1. Normal UI events such as button clicks and input changes. Handled by `IHandleEvent` which defines a single method `HandleEventAsync`.  If a handler is not defined the the event method is called directly.  If the handler exists it's called.
2. The AfterRender event. Handled by `IHandleAfterRender` which defines a single method `HandleEventAsync`.  If a handler is no defined then nothing happens.

At this point in the discussion it's important to note that these exist, and that `OnAfterRender` is a UI event, not part of the Parameter setting process.  We'll look at them in more detail shortly. 

## SetParametersAsync

The `OnInitialized{Async}` and `OnParametersSet{Async}` methods you learn about on page 1 of the Blazor manual are sub-routines of `SetParametersAsync` defined in `ComponentBase`.  They are not an integral part of the component lifecycle.  They are part of the `ComponentBase` implementation of `SetParametersAsync`. 
