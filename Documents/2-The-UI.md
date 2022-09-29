# The Blazor UI

Blazor has two modes of deployment.  Blazor Server and Blazor WASM.  You should regard them as two modes of deployment, not two different ways of developing and application.

You should design your application to run in either mode, and your UI components should be deployment agnostic i.e. run in either mode.

In this section we'll take a quick look at the differences between the teo and how they affect UI development. 

## Blazor Server

Blazor Server defines the `<app>` component in the initial server/html page.  This looks like this:

```html
<app>
    <component type="typeof(App)" render-mode="ServerPrerendered" />
</app>
```
`type` defines the route component class - in this case `App` and `render-mode` defines how the initial server-side render process runs.  You can read about that elsewhere.  The only important bit to understand is that if it pre-renders, the load page is rendered twice on the initial load - once by the server to build a static version of the page, and then a second time by the browser client code to build the live version of the page.  Note this applies to just the intial load page.  Every page therafter is loaded once.

The browser client code gets loaded by:

```html
<script src="_framework/blazor.server.js"></script>
```

Once *blazor.server.js* loads, the client application runs in the browser page and a SignalR connection is estabished with the server.  To complete the initial load, the Client Application calls the Blazor Hub Session and requests a complete server render of the `App` component.  It applies the resultant DOM changes to the Client Application DOM - this will principly be the event wiring.

The diagram below shows how a render request is passed to the displayed page.

![Server Rendering](https://shauncurtis.github.io/articles/assets/Blazor-Components/Server-Render.png)

## Blazor Web Assembly

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

The key point to take from this is that although the process by which the root component is defined and loaded is different, there's no difference in a WebAssembly and Server root component or any sub-component.  You can use the same component. 

## App.razor
 
*App.razor* is the "standard" root component.  It can be any `IComponent` defined class.

The standard `App` looks like this:

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

