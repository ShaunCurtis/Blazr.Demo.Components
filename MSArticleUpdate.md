# Build reusable UI components with Blazor

## Components

The Blazor Component architecture requires all components to implement the `IComponent` interface.  

Blazor provides `ComponentBase` as it's standard implementation of `IComponent.  It's` used by many of the out-of-the-box components such as `InputText`.  All Razor defined components inherit from `ComponentBase` by default.

Some of the following discussion, such as the lifecycle methods, relate to `ComponentBase` rather that to components *per se*. 

You do not have to use `ComponentBase` for components.  You are free to develop your own component infrastructure and libraries.  The only requirement is thet must implement `IComponent`.
