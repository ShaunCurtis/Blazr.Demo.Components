# Component Scoped Services

One issue that Blazor does not address is a services container that is scoped to a component and it's sibling sub-components.

The Blazor team built `OwningComponentBase` in the original Blazor release, but you don't hear much about it anymore.  It's not fit for purpose, and effectively redundant: I don't think anyone uses it anymore.

However, the need hasn't gone away.  There are many situations where you want a shared service that all the components in a form use, but only exists for the lifecycle of the form.  `Scoped` is too broad, and `Transient` too narrow.

This article explains how to build a `ComponentServiceProvider` and use it in a form or page setting.

## IComponentServiceProvider

An interface to define the functionality.

```csharp
public interface IComponentServiceProvider
{
    public object? GetOrCreateService(Guid componentKey, Type? serviceType);
    public TService? GetOrCreateService<TService>(Guid componentKey);
    public ValueTask ClearComponentServicesAsync(Guid componentKey);
}
```

## ComponentServiceProvider

Each Form/Page/Top Level component is identified by a `ComponentKey` :  a `Guid`.

`ComponentServiceProvider`:

1. Maintains a list of services it has constructed

