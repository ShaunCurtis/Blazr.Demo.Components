/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================
using System.Diagnostics.CodeAnalysis;

namespace Blazr.Components;

public interface IComponentServiceProvider
{
    public object? GetOrCreateService(Guid componentKey, Type? serviceType);
    public TService? GetOrCreateService<TService>(Guid componentKey);
    public ValueTask ClearComponentServicesAsync(Guid componentKey);
}
