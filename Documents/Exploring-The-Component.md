# Exploring The Blazor Component

The Blazor component, how it works and how it fits into the Blazor Framework is something of an enigma to many.  What on first introduction in the Blazor templates seems relatively simple and benign takes on a very different character when you try and use it in anger.

## Twin Existances

Components are two faced.  They have:

1. A `DOM` face: the html markup and js events that exist in the Browser UI.

2. A DotNetCore face: a C# object instance that implements `IComponent`.

The two are linked by the `RenderFragment` that builds out the DOM from the `RenderTreeBuilder` instructions defined in the `RenderFragment`.

The DOM face is rebuilt each time the component renders. A diffing engine minimizes the update process to only those sections of the DOM that have changed.

 - Changes in the component state are only reflected in the DOM after a render event occurs.  
  
 - Changes in the DOM, such a data entry in an input field, are passed into the component through UI events.  Thi leads to inconsistences between the DOM and the RenderTree until the component renders and the DOM refreshes.

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

It's important to understand this separation.  While both are driven by the Renderer, they are not tightly coupled.  There is no guarantee when a render event driven by a call to `StateHasChanged` will actually execute. 

### Construct/SetParameters Process

We've seen in the section above how compomnents are defined in `RenderTreeBuilder`` code.  When the render encounters a component defimition, the first decision is: Does the compoonent already exist in the RenderTree.

#### New Component

1. Creates an instance of the component.  Any constructor must have no arguments: `new()`.
 
2. Sets the `Inject` labelled properties by getting the relevant DI instances, and assigning them through reflection.  It raises an exception if no DI service exists
  
3. Maps the component to the relevant place in the RenderTree and calls `Attach`.  The component gets a `RenderHandle` that it stores locally.  The `RenderHandle` is the component's only communications channel with the Renderer.
 
4. Builds a list of all the `[Parameter]` and `[CascadingParameter]` properties defined in the component.
 
5. Calls `SetParametersAsync` on the component, passing in a `ParameterView` instance with all the current values for the defined parameters. 

#### Existing Component

1. Runs a comparison over the Parameters list it holds for the component.
 
2. Calls `SetParametersAsync` if anything has changed, passing in the updated list as a  `ParameterView`.  We'll discuss this process in detail later.  

#### Disposal

1. Calls `Dispose/DisposeAsync` if the component implements `IDisposable/IAsyncDisposable`.
2. Drops any references to the component.  The GC can destroy the instance and clean up. 

### UI Events

The Renderer holds a map of events that the component has defined and the event handlers it must call when they occur.

There are two types with defined handlers:

1. Normal UI events such as button clicks and input changes. Handled by `IHandleEvent` which defines a single method `HandleEventAsync`.  If a handler is not defined the the event method is called directly.  If the handler exists it's called.
 
2. The AfterRender event. Handled by `IHandleAfterRender` which defines a single method `HandleEventAsync`.  If a handler is no defined then nothing happens.

At this point in the discussion it's important to note that these exist, and that `OnAfterRender` is a UI event, not part of the Parameter setting process.  We'll look at them in more detail shortly. 

## Constructors

Component constructors must be empty constructors.  DI services are not defined in constructors.  

`ComponentBase` defines the following constructor.

The construtor builds and caches the render fragment that represents the DOM content of the component and assigns it to `_content`.  `_content` is what `StateHasChanged` queues onto the RenderQueue.  `BuildRenderTree` is where the markup in a Razor file is compiled into `RenderTreeBuilder` code.

```csharp
    private RenderFragment _content;
    private bool _renderPending;
    private bool _hasNeverRendered = true;

    public ReplicaComponentBase()
    {
        _content = (builder) =>
        {
            _renderPending = false;
            _hasNeverRendered = false;
            BuildRenderTree(builder);
        };
    }
```

If you add a constructor to your component you must call base.

```csharp
public MyComponent()
    : base()
    {
        //...
    }
```

## Attach

Attach is called when the Renderer attsches the component to the RenderTree.  The Render creates a `RenderHandle` that it passes to the Component.

```csharp
    private RenderHandle _renderHandle;

    public void Attach(RenderHandle renderHandle)
        => _renderHandle = renderHandle;
```

The `RenderHandle` instance is the only communications channel the Component has with the Renderer. It:

1. Provides a method to add a `RenderFragment` to the RenderQueue.
 
2. Provides access to the `Dispatcher` on the `Synchronisation Context` so delegates and handlers can invoke UI specific code [such as `StteHasChanged`] on the `Synchronisation Context`.

## SetParametersAsync

The `OnInitialized{Async}` and `OnParametersSet{Async}` methods you learn about on page 1 of the Blazor manual are sub-routines of `SetParametersAsync` defined in `ComponentBase`.

They are not an integral part of the component lifecycle: they are part of the `ComponentBase` implementation of `SetParametersAsync`. 

The first run:

1. Set Parameter values from the ParamterView.
2. Run `OnInitialized`.
3. Start `OnInitialzedAsync`. 
4. If `OnInitialzedAsync` yields run `StateHasChanged` which should render the component.
5. Wait for `OnInitialzedAsync` to complete.
6. Run `OnParametersSet`.
7. Start `OnParametersSetAsync`. 
4. If `OnParametersSetAsync` yields and we have't yet run a render,  run `StateHasChanged` which should render the component.
5. Wait for `OnParametersSetAsync` to complete.
6. Run `StateHasChanged`.

Subsequent runs:

1. Set Parameter values from the ParamterView.
6. Run `OnParametersSet`.
7. Start `OnParametersSetAsync`. 
4. If `OnParametersSetAsync` yields, run `StateHasChanged` which should render the component.
5. Wait for `OnParametersSetAsync` to complete.
6. Run `StateHasChanged`.

#### Yielding

In the Task context, yielding occurs in a true async routine where the executing task needs to wait on some external process [to reply to a request].  The task manager wraps the code following the await [the continuation] in a Task and adds it to [normally the end of] the queue.  It moves on to running the next Task in the queue [probably the RenderQueue manager task].

### UI Event Process

The other side of the Renderer is the UI event handler.  When the Renderer renders a component it maps the various registered C# event handlers to their Js conterparts, and raises those event handlers when the Js events are raised in the browser.

Components can implement:

 - `IEventHandler` an event handler that all normal UI events are routed through.  If it's not implemented the individual events handlers are called.
  
 - `IAfterRenderHandler` an event handler thar is called when the browser completes rendering of the DOM.  If it's not implemented then nothing happens.

We can see the `ComponentBase` implementations below.

Note the double render event in `HandleEventAsync` if `item` yields. 

```csharp
    async Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem item, object? obj)
    {
        var uiTask = item.InvokeAsync(obj);
        await this.CheckIfShouldRunStateHasChanged(uiTask);
        this.StateHasChanged();
    }

    Task IHandleAfterRender.OnAfterRenderAsync()
    {
        var firstRender = !_hasCalledOnAfterRender;
        _hasCalledOnAfterRender = true;
        OnAfterRender(firstRender);
        return OnAfterRenderAsync(firstRender);
    }

    protected async Task<bool> CheckIfShouldRunStateHasChanged(Task task)
    {
        var isCompleted = task.IsCompleted || task.IsCanceled;
        if (!isCompleted)
        {
            this.StateHasChanged();
            await task;
            return true;
        }
        return false;
    }
```