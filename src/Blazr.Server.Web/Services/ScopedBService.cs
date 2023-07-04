using System.Diagnostics;

namespace Blazr.Server.Web.Services;

public class ScopedBService: IScopedService
{
    public Guid Uid { get; init; } = Guid.NewGuid();

    public ScopedBService() 
    {
        Debug.WriteLine($"{this.Uid} - {this.GetType().Name} Inatance Created");
    }
}
