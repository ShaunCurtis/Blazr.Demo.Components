# A Deep Dive into the Blazor Component

## What is a Component?

Microsoft defines:

*A component is a self-contained portion of user interface (UI) with processing logic to enable dynamic behavior. Components can be nested, reused, shared among projects, and used in MVC and Razor Pages apps.*

*Components are implemented using a combination of C# and HTML markup in Razor component files with the .razor file extension.*

What is does rather than what it is, and not all strictly true.

From a programming perspective, a component is:

1. A class.
2. Implements `IComponent`.

Nothing more.  It comes to life when it's attached to a `RenderTree`, the component tree used by a `Renderer` to build and update.  The `IComponent` interface proves the `Renderer`s  interface to communicate with and receive communication from a component.
This code compiles and works, but if you add `Minimal` to a page you won't see anything because it doesn't do anything!

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

Before we dive further into components we need to look at the `Renderer` and `RenderTree`, and the application setting.

## The Renderer and the Render Tree

A detailed description of how the `Renderer` and `RenderTree` works is beyond the scope of this article, but you need a basic grasp of the concepts to understand the rendering process.

To quote the MS Documentation:

>The `Renderer` provides mechanisms:
>1. For rendering hierarchies of `IComponent` instances;
>2. Dispatching events to them;
>3. Notifying when the user interface is being updated.


The `Renderer` and `RenderTree` reside in the Client Application in WASM and in the SignalR Hub Session in Server, i.e. one per connected Client Application.

The UI - defined by html code in the DOM [Document Object Model] - is represented in the application as a `RenderTree` and managed by a `Renderer`. Think of the `RenderTree` as a tree with one or more components attached to each branch. Each component is a C# class that implements the `IComponent` interface.  The `Renderer` has a `RenderQueue`, containing `RenderFragments`, which it runs code to update the UI.  Components submit `RenderFragments` onto the queue.  The `Renderer` uses a diffing process to detect changes in the DOM caused by `RenderTree` updates.  It passes these changes to the client code to implement in the Browser DOM and update the displayed page.

The diagram below is a visual representation of the render tree for the out-of-the-box Blazor template.

![Root Render Tree](https://shauncurtis.github.io/articles/assets/Blazor-Components/Root-Render-Tree.png)

## The Client Application

### Blazor Server

Blazor Server defines the `<app>` component in the initial server/html page.  This looks like this:

```html
<app>
    <component type="typeof(App)" render-mode="ServerPrerendered" />
</app>
```
`type` defines the route component class - in this case `App` and `render-mode` defines how the initial server-side render process runs.  You can read about that elsewhere.  The only important bit to understand is that if it pre-renders, the page is rendered twice on the initial load - once by the server to build a static version of the page, and then a second time by the browser client code to build the live version of the page.

The browser client code gets loaded by:

```html
<script src="_framework/blazor.server.js"></script>
```

Once *blazor.server.js* loads, the client application runs in the browser page and a SignalR connection estabished with the server.  To complete the initial load, the Client Application calls the Blazor Hub Session and requests a complete server render of the `App` component.  It then applies the resultant DOM changes to the Client Application DOM - this will principly be the event wiring.

The diagram below shows how a render request is passed to the displayed page.

![Server Rendering](https://shauncurtis.github.io/articles/assets/Blazor-Components/Server-Render.png)

### Blazor Web Assembly

In Blazor WebAssembly the browser receives an Html page with a defined `div` placeholder where the root component should be loaded: 

```html
<div id="app">
    ....
</div>
```

The Client Application gets loaded by:

```html
<script src="_framework/blazor.webassembly.js"></script>
```

Once the WASM code is loaded, it runs `program`.

```csharp
builder.RootComponents.Add<App>("#app");
```

The code tells the Renderer that the `App` class component is the root component for the `RenderTree` and to load it's DOM into the `app` element in the browser DOM.

![Server Rendering](https://shauncurtis.github.io/articles/assets/Blazor-Components/Web-Assembly-Render.png)

The key point to take from this is that although the process by which the root component is defined and loaded is different, there's no difference between in a WebAssembly and Server root component or any sub-component.  You can use the same component. 

#### App.razor
 
*App.razor* is the "standard" root component.  It can be any `IComponent` defined class.

`App` looks like this:

```html
<Router AppAssembly="@typeof(Program).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)" />
    </Found>
    <NotFound>
        <LayoutView Layout="@typeof(MainLayout)">
            <p>Sorry, there's nothing at this address.</p>
        </LayoutView>
    </NotFound>
</Router>
```

It's a Razor component defining one child component, `Router`.  `Router` has two `RenderFragments`, `Found` and `NotFound`.  If `Router` finds a route, and therefore an `IComponent` class, it renders the `RouteView` component and passes it the route class type along with the default `Layout` class.   If no route is found it renders a `LayoutView` and renders the defined content in it's `Body`.

`RouteView` checks if the `RouteData` component has a specific layout class defined.  If so it uses it, otherwise it uses the default layout.  It renders the layout and passes it the type of the component to add to the `Body` RenderFragment.  

## Razor Components

Most components are defined in Razor.  But what's the relationship between a Razor component and `IComponent`?

There isn't one.  The only requirement for a Razor component is that the defined inherited class must implement:

```csharp
protected virtual void BuildRenderTree(RenderTreeBuilder builder);
```

The Razor compiler overrides `BuildRenderTree` with a method containing the `RenderTreeBuilder` code that represents the component's UI.

If no inheritance is defined, it sets the compiled class to inherit from `ComponentBase`.

Here's a simple abstract class which defines `BuilRenderTree`:

```csharp
public abstract class RazorClass
{
    protected abstract void BuildRenderTree(RenderTreeBuilder builder);

    public RenderFragment Content => (builder) => BuildRenderTree(builder);
}
```

A Razor Implementation:

```csharp
// Div.razor
@inherits RazorClass

<h3>My Razor Class Div</h3>
```

And it's usage in a page:

```csharp
@(new Div().Content)
```

## The Component Lifecycle

The component liefcycle is managed by the Renderer.  You have no control over the actual lifecycle.  You can't instanciate a component and pass it to the renderer.

1. The component is instanciated and the `new()` is run.  The ctor method is often overlooked.  At this point the parameters are in their default state and there's no `RenderHandle`.

2. The Renderer calls `Attach` and passes in a `RenderHandle`.  This is the component's communications medium with the Renderer.  Save it to an internal field.

3. The Renderer calls `SetParametersAsync` and passes in a `ParameterView` object.  This is the object the Renderer uses to manage the component's parameters.  This should be used in `SetParametersAsync` and then released.  Don't save it to an internal field.

4. The Renderer calls `SetParametersAsync` whenever it detects that the components parameters "may have changed".

5. If the component implements `IDisposable` or `IAsyncDisposable`, it calls it and then de-references the component.  The GC will then destroy the redundant object.

## Interfaces

A component must implement `IComponent` which is defined as:

```csharp
public interface IComponent
{
    void Attach(RenderHandle renderHandle);
    Task SetParametersAsync(ParameterView parameters);
}
```

It can also implement `IHandleEvent`

```csharp
public interface IHandleEvent
{
    Task HandleEventAsync(EventCallbackWorkItem item, object? arg);
}
```

Which allows you to define a custom event handler for handling all UI events.

And `IHandleAfterRender`

```csharp
public interface IHandleAfterRender
{
    Task OnAfterRenderAsync();
}
```

Which is called after the component is rendered.

### The RendleHandle

To quote Microsoft:

> A RenderHandle structure allows a component to interact with its renderer.

It has two important features:

1. A `Render` method.
2. A property referencing the Dispatcher for the `SynchronisationContext` - i.e. the Dispatcher for the thread on which all UI based code must be run.

### The RenderFragment

A render fragment isn't a block of UI code.  You can't do this:

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

### The HelloWorld Component

To demonstrate the `IComponent` interface We'll build a simple base component and then a  `HelloWorld` component that inherits from it.

The MinimalBase component:

1. It's abstract - it doesn't output anything so don't allow it to be used directly as a component. 
2. Captures the RenderHandle
3. Sets the parameters and requests a render - See the inline comments for detail,

```csharp
public abstract class Minimal1Base : IComponent
{
    protected RenderHandle? renderHandle;

    public void Attach(RenderHandle handle)
        => renderHandle = handle;

    public Task SetParametersAsync(ParameterView parameters)
    {
        // Sets the component parameters to the latest values
        parameters.SetParameterProperties(this);
        // Creates a render fragment as an anonymous function that calls BuildRenderTree
        RenderFragment fragment = (builder) => BuildRenderTree(builder);
        // passes the fragment to the RenderTree to render
        this.renderHandle?.Render(fragment);
        return Task.CompletedTask;
    }

    protected abstract void BuildRenderTree(RenderTreeBuilder builder);
}
```

Our simplest Hello World Razor component looks like this:

```html
@inherits RazorClass
<h3>Hello Blazor</h3>
```

### Building Render Fragments

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

We're defining a `RenderFragment` property and assigning an anonymous method to it that conforms to the `RenderFragment` pattern.  It takes a  `RenderTreeBuilder` and has no return so returns a void.  It uses the provided `RenderTreeBuilder` object to build the content: a simple hello world html div.  Each call to the builder adds what is called a `RenderTreeFrame`.  Note each frame is sequentially numbered.

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
2. Even though the `Renderer` invokes the code, the code is run in the context of the component, and the state of the component when executing happens.

We can take the concept above and just define a class.

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

### Routed Components

Everything's a component, but not all components are equal.  **Routed Components** are a little special.

They contain *@page* routing directives and optionally a *@Layout* directive.

```html
@page "/WeatherForecast"
@page "/WeatherForecasts"
@layout MainLayout
```

You can define these directly on classes like this:

```csharp
[LayoutAttribute(typeof(MainLayout))]
[RouteAttribute("/helloworld")]
public class RendererComponent : IComponent {}
```

The `RouteAttribute` is used by the router to find Routes in the application.

Don't think of routed components as pages. It may seem obvious to do so, but don't.  Lots of web page properties don't apply to routed components.  You will:
 - get confused when routed components don't behave like a page.
 - try and code the component logic as if it is a web page.

## Improving `MinimalBase`

There are some performance improvements we can make to `MinimalBase`.

First is adding in a `Hidden` feature.  Many UI components implement some form of hide/show functionality.  We'll see why shortly.

This code isn't a very efficient way of implementing the component render fragment:

```csharp
RenderFragment fragment = (builder) => BuildRenderTree(builder);
```

The runtime has to build the anonymous function every time the component renders.  We can cache this is ctor like this.

We'll come to `_renderPending` in a minute.  You can see how `Hidden` is implemented.

```csharp
private bool _renderpending;
private RenderFragment _componentFragment;

[Parameter] public bool Hidden { get; set; }

public MinimalBase()
{
    _componentFragment = (builder) =>
    {
        _renderpending = false;
        if (!Hidden)
            BuildRenderTree(builder);
    };
}
```
We can also improve the render code.  The existing code places the render fragment in the queue regardless of whether there's already one queued.

```csharp
this.renderHandle.Render(fragment);
```

The new method uses a private `bool` `_renderPending` to track render state.  If the component render fragment is already queued, it doesn't add another one, the one already in the queue will render the component with the current changes.  `_renderPending` 

```csharp
protected void RequestRender()
{
    if (!_renderPending)
    {
        _renderPending = true;
        this.renderHandle.Render(_componentFragment);
    }
}
```
The final base component is:

```csharp
public abstract class MinimalBase : IComponent
{
    protected RenderHandle renderHandle;
    private bool _renderPending;
    private RenderFragment _componentFragment;

    [Parameter] public bool Hidden { get; set; }

    public MinimalBase()
    {
        _componentFragment = (builder) =>
        {
            _renderPending = false;
            if (!Hidden)
                BuildRenderTree(builder);
        };
    }

    public void Attach(RenderHandle handle)
        => renderHandle = handle;

    public Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        this.RequestRender();
        return Task.CompletedTask;
    }

    protected abstract void BuildRenderTree(RenderTreeBuilder builder);

    protected void RequestRender()
    {
        if (!_renderPending)
        {
            _renderPending = true;
            this.renderHandle.Render(_componentFragment);
        }
    }
}
```

### Rendering



## ComponentBase

`ComponentBase` is the "standard" out-of-the-box Blazor implementation of `IComponent`.  All *.razor* files by default inherit from it.  While you may never step outside `ComponentBase` it's important to understand that it's just one implementation of the `IComponent` interface.  It doesn't define a component.  `OnInitialized` is not a component lifecycle method, it's a `ComponentBase` lifecycle method. 

### ComponentBase Lifecycle and Events

There are articles galore regurgitating the same old basic lifecycle information.  I'm not going to repeat it.  Instead I'm going to concentrate on certain often misunderstood aspects of the lifecycle: there's more to the lifecycle that just the initial component load covered in most of the articles.

We need to consider five types of event:
1. Instantiation of the class
2. Initialization of the component
3. Component parameter changes
4. Component events
5. Component disposal

There are seven exposed Events/Methods and their async equivalents:
1. `SetParametersAsync`
2. `OnInitialized` and `OnInitializedAsync`
3. `OnParametersSet` and `OnParametersSetAsync`
4. `OnAfterRender` and `OnAfterRenderAsync`
5. `Dispose` - if `IDisposable` is implemented
6. `StateHasChanged`
7. `new` - often forgotten.

The standard class instantiation method builds the `RenderFragment` that `StateHasChanged` passes to the  `Renderer` to render the component.  It sets two private class variables to false and runs `BuildRenderTree`.

```csharp
public ComponentBase()
{
    _renderFragment = builder =>
    {
        _hasPendingQueuedRender = false;
        _hasNeverRendered = false;
        BuildRenderTree(builder);
    };
}
```

`SetParametersAsync` sets the properties for the submitted parameters. It only runs `RunInitAndSetParametersAsync` - and thus `OnInitialized` followed by `OnInitializedAsync` - on initialization. It always calls `CallOnParametersSetAsync`.  Note:
1. `CallOnParametersSetAsync` waits on `OnInitializedAsync` to complete before calling `CallOnParametersSetAsync`.
2. `RunInitAndSetParametersAsync` calls `StateHasChanged` if `OnInitializedAsync` task yields before completion. 

```csharp
public virtual Task SetParametersAsync(ParameterView parameters)
{
    parameters.SetParameterProperties(this);
    if (!_initialized)
    {
        _initialized = true;
        return RunInitAndSetParametersAsync();
    }
    else return CallOnParametersSetAsync();
}

private async Task RunInitAndSetParametersAsync()
{
    OnInitialized();
    var task = OnInitializedAsync();
    if (task.Status != TaskStatus.RanToCompletion && task.Status != TaskStatus.Canceled)
    {
        StateHasChanged();
        try { await task;}
        catch { if (!task.IsCanceled) throw; }
    }
    await CallOnParametersSetAsync();

```

`CallOnParametersSetAsync` calls `OnParametersSet` followed by `OnParametersSetAsync`, and finally `StateHasChanged`.  If the `OnParametersSetAsync()` task yields `CallStateHasChangedOnAsyncCompletion` awaits the task and re-runs `StateHasChanged`. 

```csharp
private Task CallOnParametersSetAsync()
{
    OnParametersSet();
    var task = OnParametersSetAsync();
    var shouldAwaitTask = task.Status != TaskStatus.RanToCompletion &&
        task.Status != TaskStatus.Canceled;

    StateHasChanged();

    return shouldAwaitTask ?
        CallStateHasChangedOnAsyncCompletion(task) :
        Task.CompletedTask;
}

private async Task CallStateHasChangedOnAsyncCompletion(Task task)
{
    try { await task; }
    catch 
    {
        if (task.IsCanceled) return;
        throw;
    }
    StateHasChanged();
}
```

Lets look at `StateHasChanged`.  If a render is pending i.e. the renderer hasn't got round to running the queued render request, it closes - whatever changes have been made will be captured in the queued render.  If not, it sets the  `_hasPendingQueuedRender` class flag and calls the Render method on the `RenderHandle`.  This queues `_renderFragement` onto the `Renderer` `RenderQueue`.  When the queue runs `_renderFragment` - see above - it sets the two class flags to false and runs `BuildRenderTree`.

```csharp
protected void StateHasChanged()
{
    if (_hasPendingQueuedRender) return;
    if (_hasNeverRendered || ShouldRender())
    {
        _hasPendingQueuedRender = true;
        try { _renderHandle.Render(_renderFragment);}
        catch {
            _hasPendingQueuedRender = false;
            throw;
        }
    }
}
```

`StateHasChanged` must be run on the UI thread.  When called internally that will always be the case.  However, when wiring up external event handlers that my not be so.  You need to implement these like this:

```csharp
private void OnExternalEvent(object? sender, EventArgs e)
    => this.InvokeAsync(StateHasChanged);
```

`InvokeAsync` is a `ComponentBase` method that invokes the supplied action on the `Dispatcher` provided by the `RenderHandle`.


### And then what no one covers.  

Components receive UI events from the Renderer.  What happens is dictated by two interfaces that components can implement:

 - `IHandleEvent` defines a single method - `Task HandleEventAsync(EventCallbackWorkItem callback, object? arg)` When implemented, the Renderer passes all events to the handler.  When not, it calls the method directly.

 - `IHandleAfterRender` defines a single method - `OnAfterRenderAsync()` which handles the after render process.  If nothing is defined then there is no process.

`ComponentBase` implements both both interfaces.  We'll look at them in more detail shortly.

Some key points to note:

1. `OnInitialized` and `OnInitializedAsync` only get called during initialization.  `OnInitialized` is run first.  If, and only if, `OnInitializedAsync` yields back to the internal calling method `RunInitAndSetParametersAsync`, then `StateHasChanged` get called, providing the opportunity to provide "Loading" information to the user.  `OnInitializedAsync` completes before `OnParametersSet` and `OnParametersSetAsync` are called.

2. `OnParametersSet` and `OnParametersSetAsync` get called whenever the parent component makes changes to the parameter set for the component or a captured cascaded parameter changes.  Any code that needs to respond to parameter changes need to live here. `OnParametersSet` is run first.  Note that if `OnParametersSetAsync` yields, `StateHasChanged` is run after the yield, providing the opportunity to provide "Loading" information to the user.

3. `StateHasChanged` is called after the `OnParametersSet{async}` methods complete to render the component.

4. `OnAfterRender` and `OnAfterRenderAsync` occur at the end of all four events.  `firstRender` is only true on component initialization.  Note that any changes made here to parameters won't get applied to display values until the component re-renders.

5. `StateHasChanged` is called during the initialization process if the conditions stated above are met, after the `OnParametersSet` processes, and any event callback.  Don't call it explicitly during the render or parameter set process unless you need to.  If you do call it you are probably doing something wrong.

## The Render Process

Let's look in detail at how a simple page and component get rendered.

#### SimpleComponent.razor
```html
<div class="h4 bg-success text-white p-2">Loaded</div>
```

#### SimplePage.razor
```csharp
@page "/simple"
<h3>SimplePage</h3>
@if (loaded)
{
    <SimpleComponent></SimpleComponent>
}
else
{
    <div class="h4 bg-danger text-white p-2">Loading.....</div>
}

@code {
    private bool loaded;

    protected async override Task OnInitializedAsync()
    {
        await Task.Delay(2000);
        loaded = true;
    }
}
```

The follow diagram shows a simplified `RenderTree` representing a simple "/" route.

![Root Render Tree](https://shauncurtis.github.io/articles/assets/Blazor-Components/Root-Render-Tree.png)

Note the three nodes in `NavMenu` for the three `NavLink` controls. 

On our page, the render tree looks like the diagram below on first render - we have a yielding `OnInitializedAsync` method, so `StateHasChanged` gets run in the initialization process.

![Simple Page Loading](https://shauncurtis.github.io/articles/assets/Blazor-Components/Simple-Page-Loading.png)

Once initialization completes, `StateHasChanged` is run a second time.  `Loaded` is now `true` and `SimpleComponent` is added to the component `RenderFragment`.  When the `Renderer` runs the `RenderFragment`, `SimpleComponent` is added to the render tree, instantiated and initialized.

![Simple Page Loaded](https://shauncurtis.github.io/articles/assets/Blazor-Components/Simple-Page-Loaded.png)

### Component Content

Change `SimpleComponent` and `SimplePage` to:

#### SimpleComponent.razor
```csharp
<div class="h4 bg-success text-white p-2">@ChildContent</div>

@code {
    [Parameter] public RenderFragment ChildContent { get; set; }
}
```

#### SimplePage.razor
```csharp
@page "/simple"
<h3>SimplePage</h3>
@if (loaded)
{
    <SimpleComponent>
        <button class="btn btn-primary" @onclick="ButtonClick">Click Me</button>
    </SimpleComponent>
}
else
{
    <div class="h4 bg-danger text-white p-2">Loading.....</div>
}

@code {
    private bool loaded;

    protected async override Task OnInitializedAsync()
    {
        await Task.Delay(2000);
        loaded = true;
    }

    protected void ButtonClick(MouseEventArgs e)
    {
        var x = true;
    }
}
```
There is now content in `SimpleComponent`. When the application is run that content gets executed in the context of the parent component.  How?

The answer is in `SimpleComponent`.   Remove the `[Parameter]` attribute from `SimpleComponent` and run the page.  It errors:

```Text
InvalidOperationException: Object of type 'xxx.SimpleComponent' has a property matching the name 'ChildContent', but it does not have [ParameterAttribute] or [CascadingParameterAttribute] applied.
```

If a component has "content" i.e. markup between the opening and closing tags, Blazor expects to find a `Parameter` named `ChildContent` in the component.  The content between the tags is pre-compiled into a `RenderFragment` and then added to the component.  The content of the `RenderFragment` is run in the context of the object that owns it - `SimplePage`.

The content can also be defined like this:

```html
<SimpleComponent>
    <ChildContent>
        <button class="btn btn-primary" @onclick="ButtonClick">
            Click Me
        </button>
    </ChildContent>
</SimpleComponent>
```

The page can also be re-written as below, where it now becomes more obvious who owns the `RenderFragment`.

```csharp
@page "/simple"
<h3>SimplePage</h3>
@if (loaded)
{
    <SimpleComponent>
        @_childContent
    </SimpleComponent>
}
else
{
    <div class="h4 bg-danger text-white p-2">Loading.....</div>
}

@code {

    private bool loaded;

    protected async override Task OnInitializedAsync()
    {
        await Task.Delay(2000);
        loaded = true;
    }

    protected void ButtonClick(MouseEventArgs e)
    {
        var x = true;
    }

    private RenderFragment _childContent => (builder) =>
    {
        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "class", "btn btn-primary");
        builder.AddAttribute(2, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, ButtonClick));
        builder.AddContent(3, "Click Me");
        builder.CloseElement();
    };
}
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

## Component Events

Hidden away are two important interfaces that dictate how components react to UI events.

 - `IHandleEvent`
 - `IHandleAfterRender`

#### IHandleEvent

When the Renderer receives a UI event it checks the compoment to see if it implements `IHandleEvent`.  If so then it passes the call to the handler.

`IHandleEvent` defines the following single method.

```csharp
Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg);
```

`ComponentBase` implements the interface, with the two step call to `StateHasChanged`.

```csharp
Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg)
{
    var task = callback.InvokeAsync(arg);
    var shouldAwaitTask = task.Status != TaskStatus.RanToCompletion &&
        task.Status != TaskStatus.Canceled;

    // After each event, we synchronously re-render (unless !ShouldRender())
    // This just saves the developer the trouble of putting "StateHasChanged();"
    // at the end of every event callback.
    StateHasChanged();

    return shouldAwaitTask ?
        CallStateHasChangedOnAsyncCompletion(task) :
        Task.CompletedTask;
}

private async Task CallStateHasChangedOnAsyncCompletion(Task task)
{
    try
    {
        await task;
    }
    catch // avoiding exception filters for AOT runtime support
    {
        // Ignore exceptions from task cancellations, but don't bother issuing a state change.
        if (task.IsCanceled)
            return;
        throw;
    }
    StateHasChanged();
}
```

If `IHandleEvent` is not implemented it simply calls the handler directly.

```csharp
Task async IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg)
   => await callback.InvokeAsync(arg);
```

#### IHandleAfterRender

When the component completes rendering the Renderer checks the compoment to see if it implements `IHandleAfterRender`.  If so then it passes the call to the handler.

`ComponentBase` implements the interface.

```csharp
Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg)
{
    var task = callback.InvokeAsync(arg);
    var shouldAwaitTask = task.Status != TaskStatus.RanToCompletion &&
        task.Status != TaskStatus.Canceled;

    // After each event, we synchronously re-render (unless !ShouldRender())
    // This just saves the developer the trouble of putting "StateHasChanged();"
    // at the end of every event callback.
    StateHasChanged();

    return shouldAwaitTask ?
        CallStateHasChangedOnAsyncCompletion(task) :
        Task.CompletedTask;
}
```

If `IHandleAfterRender` is not implemented then nothing happens.

```csharp
Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg)
   => return Task.CompletedTask;
```

#### Void UI Events

The following code won't execute as expected in `ComponentBase`:

```csharp
void async ButtonClick(MouseEventArgs e) 
{
  await Task.Delay(2000);
  UpdateADisplayProperty();
}
```

The DisplayProperty doesn't display the current value until another `StateHasChanged` events occurs.  Why? ButtonClick doesn't return anything, so there's no `Task` for the event handler to wait on.  On the `await` yield, it runs to completion running the final `StateHasChanged` before `UpdateADisplayProperty` completes.

This is a band-aid fix - it's bad pactice, **DON'T DO IT**.

```csharp
void async ButtonClick(MouseEventArgs e) 
{
  await Task.Delay(2000);
  UpdateADisplayProperty();
  StateHasChanged();
}
```

The correct solution is:

```csharp
Task async ButtonClick(MouseEventArgs e) 
{
  await Task.Delay(2000);
  UpdateADisplayProperty();
}
```
Now the event handler has a `Task` to await and doesn't execute `StateHasChanged` until `ButtonClick` completes.

## Some Important Less Documented Information and Lessons Learned

### Keep Parameter Properties Simple

Your parameter declarations should look like this:

```csharp
[Parameter] MyClass myClass {get; set;}
```

**DON'T** add code to the getter or setter.  Why?  Any setter must be run as part of the render process and can have a significant impact on render speed and component state.

### Overriding SetParametersAsync

If you override `SetParametersAsync` your method should look like this: 

``` csharp
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // always call first
        parameters.SetParameterProperties(this);
        // Your Code
        .....
        // pass an empty ParameterView, not parameters
        return base.SetParametersAsync(ParameterView.Empty);
    }
```

Set the parameters in the first line and call the base method passing `ParameterView.Empty`.  Don't try to pass `parameters` - you will get an error.

### Parameters as Immutable

Never set Parameters in your code.  If you want to make or track changes do this:

```csharp
    [Parameter] public int MyParameter { get; set; }
    private int _MyParameter;
    public event EventHandler MyParameterChanged;

    public async override Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        if (!_MyParameter.Equals(MyParameter))
        {
            _MyParameter = MyParameter;
            MyParameterChanged?.Invoke(_MyParameter, EventArgs.Empty);
        }
        await base.SetParametersAsync(ParameterView.Empty);
    }
```

### Iterators

A common problem occurs when a `For` iterator is used to loop through a collection to build a `select` or a data table.  A typical example is shown below:

```csharp
@for (var counter = 0; counter < this.myList.Count; counter++)
{
    <button class="btn btn-dark m-3" @onclick="() => ButtonClick(this.myList[counter])">@this.myList[counter]</button>
}
@for (var counter = 0; counter < this.myList.Count; counter++)
{
    <button class="btn btn-dark m-3" @onclick="() => ButtonClick(counter)">@this.myList[counter]</button>
}
<div>Value = @this.value </div>

@code {
    private List<int> myList => new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
    private int value;

    private Task ButtonClick(int value)
    {
        this.value = value;
        return Task.CompletedTask;
    }
}
```

If you click on a button in the first row, you will get a *Index was out of range* error.  Click on a button in the second row and value is always 10.  The reason is that the interator has completed before you click a button, at which time `counter` is 10.

To fix the problem, set a local variable within the loop as shown below.

```csharp
@for (var counter = 0; counter < this.myList.Count; counter++)
{
    var item = this.myList[counter];
    <button class="btn btn-dark m-3" @onclick="() => ButtonClick(item)">@item</button>
}
@for (var counter = 0; counter < this.myList.Count; counter++)
{
    var item = this.myList[counter];
    var thiscount = counter;
    <button class="btn btn-info m-3" @onclick="() => ButtonClick(thiscount)">@item</button>
}
```

The best solution is to use `ForEach`.

```csharp
@foreach  (var item in this.myList)
{
    <button class="btn btn-primary m-3" @onclick="() => ButtonClick(item)">@item</button>
}
```
### Component Numbering

It's seems logical to use iterators to automate the numbering of component elements.  DON'T.  The numbering system is used by the diffing engine to decide which bits of the DOM need updating and which bits don't.  Numbering must be consistent within a `RenderFragment`.  You can use `OpenRegion` and `CloseRegion` to define a region with it's own number space.  [See this gist for a more detailed explanation](https://gist.github.com/SteveSandersonMS/ec232992c2446ab9a0059dd0fbc5d0c3). 

## Building Components

Components can be defined in three ways:
1. As a *.razor* file with an code inside an *@code* block.
2. As a *.razor* file and a code behind *.razor.cs* file.
3. As a pure *.cs* class file inheriting from *ComponentBase* or a *ComponentBase* inherited class, or implementing *IComponent*.

##### All in One Razor File

HelloWorld.razor

```html
<div>
@HelloWorld
</div>

@code {
[Parameter]
public string HelloWorld {get; set;} = "Hello?";
}
```

##### Code Behind

HelloWorld.razor

```html
@inherits ComponentBase
@namespace CEC.Blazor.Server.Pages

<div>
@HelloWorld
</div>
```
HelloWorld.razor.cs

```csharp
namespace CEC.Blazor.Server.Pages
{
    public partial class HelloWorld : ComponentBase
    {
        [Parameter]
        public string HelloWorld {get; set;} = "Hello?";
    }
}
```

##### C# Class

HelloWorld.cs

```csharp
namespace CEC.Blazor.Server.Pages
{
    public class HelloWorld : ComponentBase
    {
        [Parameter]
        public string HelloWorld {get; set;} = "Hello?";

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddContent(1, (MarkupString)this._Content);
            builder.CloseElement();
        }
    }
}
```

## Inheritance

If you are creating abstract components from `ComponentBase` don't override `OnInitialized{Async}` and `OnParametersSet{Async}` in the abstract classes with your custom code.  This presents a timing problem: do you run `base.OnInitializedAsync` at the start or end of `this.OnInitializedAsync`.

Lets look an example.  We have a `ViewRecordComponent<TRecord>` that loads a record as part of the initilization.  The best way to implement this is to override `SetParametersAsync` and add a `LoadRecordAsync` method to the core lifecycle sequence.  Here's what the code could look like:

```csharp
public async override Task SetParametersAsync(ParameterView parameters)
{
    parameters.SetParameterProperties(this);
    await this.LoadRecordAsync();
    await base.SetParametersAsync(ParameterView.Empty);
}

protected virtual Task LoadRecordAsync()
{
    // load code
}
```

You've coded exactly when `LoadRecordAsync` is run.

## Some Observations

1. There's a tendency to pile too much code into `OnInitialized` and `OnInitializedAsync` and then use events to drive `StateHasChanged` updates in the component tree.  Get the relevant code into the right places in the lifecycle and you won't need the events.

2. There's a temptation to start with the non-async versions (because they're easier to implement) and only use the async versions when you have to, when the opposite should be true.  Most web based activities are inherently async in nature.  I never use the non-async versions - I work on the principle that at some point I'm going to need to add async behaviour.
   
3. `StateHasChanged` is called far to often, normally because code is in the wrong place in the component lifecycle, or the events have been coded incorrectly.  Ask yourself a challenging "Why?" when you type `StateHasChanged`.

4. Components are underused in the UI.  The same code/markup blocks are used repeatedly.  The same rules apply to code/markup blocks as to C# code.

5. Once you really, REALLY understand components, writing Blazor code becomes a totally "different" experience.
   