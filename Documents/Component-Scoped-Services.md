# Component Scoped Services

One issue that Blazor does not provide is a services container that is scoped to a component's lifetime, and is available to sibling components within the render tree.

The Blazor team provided `OwningComponentBase` in the original Blazor release, but you don't hear much about it anymore.  It's not fit for purpose, and effectively redundant: I don't think anyone uses it anymore.

However, the need hasn't gone away.  There are many situations where you want a shared service that all the components in a form use, but only exists for the lifecycle of the form.  `Scoped` is too broad, and `Transient` too narrow.

This article explains how to build a `ComponentServiceProvider` that provides scoped object instances, and use it in a form or page setting.

## Concept

Each Form/Page/Top Level component [referred to as the Scoped Component] is identified by a `ComponentKey` :  a `Guid`.  All services associated with a `ComponentKey` belong to the scoped component.

`ComponentServiceProvider` provides the service container, and creates and disposes services as required.  It's a scoped service in the main service container.

It provides three public methods through the `IComponentServiceProvider` interface.

```csharp
public interface IComponentServiceProvider
{
    public object? GetOrCreateService(Guid componentKey, Type? serviceType);
    public TService? GetOrCreateService<TService>(Guid componentKey);
    public ValueTask ClearComponentServicesAsync(Guid componentKey);
}
```
The scoped component creates a `ComponentServiceHandle` and cascades that handle.  Any sibling in the render tree can capture that handle and request the required service.

The service handle has the following structure:

```csharp
public readonly struct ComponentServiceHandle
{
    public ComponentServiceHandle(IComponentServiceProvider componentServiceProvider, Guid componentServiceKey);
    public TService? GetService<TService>() where TService : class;
    public object? GetService(Type service);
}        
```
Any component captures the cascade:

```csharp
[CascadingParameter] private ComponentServiceHandle Handle { get; set; } = default!;
```

 and get the necessay service.

```csharp
var scopedService = this.Handle.GetService<IScopedService>();
```

## ComponentServiceProvider

`ComponentServiceProvider`:

1. Maintains a list of services it has constructed.
2. Provides methods to get both interface based and concrete class based services from the main service container.
3. Manages disposal of services.

A `ComponentService` is a simple record.

```csharp
public record ComponentService(Guid ComponentId, Type ServiceType, object ServiceInstance);
```

and maintained in a list:

```csharp
    private List<ComponentService> _componentServices = new List<ComponentService>();
```

It gets the `IServiceProvider` through injection and uses it to resolve and create services.

```csharp
    private IServiceProvider _serviceProvider;

    public ComponentServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
```

It creates instances of classes using the `ActivatorUtilities` utility class.

```csharp
private bool tryCreateService(Type serviceType, [NotNullWhen(true)] out object? service)
{

    service = null;
    try
    {
        service = ActivatorUtilities.CreateInstance(_serviceProvider, serviceType);
        return true;
    }
    catch
    {
        return false;
    }
}
```

And resolves and creates `interface` defined services through the servicve provider;

```csharp
private bool tryCreateInterfaceService(Type serviceType, [NotNullWhen(true)] out object? service)
{
    service = null;
    var concreteService = _serviceProvider.GetService(serviceType);
    if (concreteService is null)
        return false;

    var concreteInterfaceType = concreteService.GetType();

    try
    {
        service = ActivatorUtilities.CreateInstance(_serviceProvider, concreteInterfaceType);
        return true;
    }
    catch
    {
        return false;
    }
}
```

These plug together:

```csharp
private object? getOrCreateService(Guid componentKey, Type? serviceType)
{
    if (serviceType is null || componentKey == Guid.Empty)
        return null;

    // Try getting the service from the collection
    if (this.tryFindComponentService(componentKey, serviceType, out ComponentService? service))
        return service.ServiceInstance;

    // Try creating the service
    if (!this.tryCreateService(serviceType, out object? newService))
        this.tryCreateInterfaceService(serviceType, out newService);

    if (newService is null)
        return null;

    _componentServices.Add(new ComponentService(componentKey, serviceType, newService));

    return newService;
}

private bool tryFindComponentService(Guid componentId, Type serviceType, [NotNullWhenAttribute(true)] out ComponentService? result)
{
    result = _componentServices.SingleOrDefault(item => item.ComponentId == componentId && item.ServiceType == serviceType);
    if (result is default(ComponentService))
        return false;

    return true;
}
```

Services are removed (disposed and de-referenced):

```csharp
private async ValueTask removeServicesAsync(Guid componentKey)
{
    foreach(var componentService in _componentServices.Where(item => item.ComponentId == componentKey))
    {
        if (componentService.ServiceInstance is IDisposable disposable)
            disposable.Dispose();

        if (componentService.ServiceInstance is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();

        _componentServices.Remove(componentService);
    }
}
```

The public methods:

```csharp
public object? GetOrCreateService(Guid componentKey, Type? serviceType)
        => getOrCreateService(componentKey, serviceType);

public TService? GetOrCreateService<TService>(Guid componentKey)
{
    var service = this.getOrCreateService(componentKey, typeof(TService));
    return (TService?)service;
}

public ValueTask ClearComponentServicesAsync(Guid componentKey)
    => removeServicesAsync(componentKey);
```

`ComponentServiceProvider` implements both `IDisposable` and `IAsyncDisposable` to dispose any services in it's service collection.

```csharp
public void Dispose()
{
    Dispose(disposing: true);
    GC.SuppressFinalize(this);
}

public async ValueTask DisposeAsync()
{
    await DisposeAsync(disposing: true);
    GC.SuppressFinalize(this);
}

protected virtual void Dispose(bool disposing)
{
    if (disposedValue || !disposing)
    {
        disposedValue = true;
        return;
    }

    foreach (var componentService in _componentServices)
    {
        if (componentService.ServiceInstance is IDisposable disposable)
            disposable.Dispose();
    }

    disposedValue = true;
}

protected async ValueTask DisposeAsync(bool disposing)
{
    if (asyncdisposedValue || !disposing)
    {
        asyncdisposedValue = true;
        return;
    }

    foreach (var componentService in _componentServices)
    {
        if (componentService.ServiceInstance is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
    }

    asyncdisposedValue = true;
}
```

### ComponentServiceHandle

The `ComponentServiceHandle` is the object cascaded by the the top level form/page, and provides controlled access to the `ComponentServiceProvider`.

```csharp
public readonly struct ComponentServiceHandle
{
    private readonly IComponentServiceProvider _componentServiceProvider;
    private readonly Guid _componentServiceKey;

    public ComponentServiceHandle(IComponentServiceProvider componentServiceProvider, Guid componentServiceKey)
    {
        _componentServiceProvider = componentServiceProvider;
        _componentServiceKey = componentServiceKey;
    }

    public TService? GetService<TService>()
       where TService : class
       => _componentServiceProvider.GetOrCreateService<TService>(_componentServiceKey);

    public object? GetService(Type service)
       => _componentServiceProvider.GetOrCreateService(_componentServiceKey, service);
}        
```

## Implementation

### Cascading Compomnent

```csharp
@inherits BlazrControlBase
@using Blazr.Components;
@inject IComponentServiceProvider _componentServiceProvider

<CascadingValue Value="this.ServiceHandle" IsFixed>
    @this.ChildContent
</CascadingValue>

@code {
    [Parameter] public RenderFragment? ChildContent { get; set; }

    public Guid ServiceKey => this.ComponentUid;

    public ComponentServiceHandle? ServiceHandle { get; private set; } = default!;

    protected override Task OnParametersSetAsync()
    {
        if (this.NotInitialized)
        {
            this.ServiceHandle = new(_componentServiceProvider, this.ServiceKey);
            ArgumentNullException.ThrowIfNull(this.ServiceHandle);
        }

        return base.OnParametersSetAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _componentServiceProvider.ClearComponentServicesAsync(this.ServiceKey);
    }
}
```

### Wrapper Component

```csharp
@inherits BlazrControlBase
@inject IComponentServiceProvider _componentServiceProvider
@namespace Blazr.Components

@code {
    public Guid ServiceKey => this.ComponentUid;

    private ComponentServiceHandle _componentServiceHandle = default!;

    protected override Task OnParametersSetAsync()
    {
        if (!Initialized)
            _componentServiceHandle = new(_componentServiceProvider, this.ComponentUid);

        return base.OnParametersSetAsync();
    }

    protected override RenderFragment Frame => (__builder) =>
    {
        <CascadingValue Value="_componentServiceHandle" IsFixed>
            @this.Body
        </CascadingValue>
    };

    public async ValueTask DisposeAsync()
    {
        await _componentServiceProvider.ClearComponentServicesAsync(this.ServiceKey);
    }
}
```

### Consumer

```csharp
@inherits BlazrControlBase

<h3>Service Consumer</h3>

<div class="bg-dark text-white m-2 p-2">
    <pre>ScopedAService Id: @(_scopedService?.ComponentUid.ToString() ?? "No Service") </pre>
</div>

@code {
    [CascadingParameter] private ComponentServiceHandle Handle { get; set; } = default!;

    private IScopedService? _scopedService;

    protected override Task OnParametersSetAsync()
    {
        if(this.NotInitialized)
        {
            ArgumentNullException.ThrowIfNull(Handle);
            _scopedService = this.Handle.GetService<IScopedService>();
            ArgumentNullException.ThrowIfNull(_scopedService);
        }

        return base.OnParametersSetAsync();
    }
}
```

## Considerations

`ComponentServiceProvider` is not a fully fledged DI container.  It creates a single instance of any class or service that can be created using `ActivatorUtilities`.  It's a simple scoped only container.

If you request a *Singleton* service, it will create a new instance of that service; it won't get the singleton from the root service container.

The same applies to *Transient* services, it will only create and maintain a single instance.

If you want either a singleton or transient service, inject them directly.


