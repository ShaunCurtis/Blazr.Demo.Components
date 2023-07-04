using System.Diagnostics;

namespace Blazr.Server.Web.Services
{
    public class ScopedAService : IScopedService
    {
        public Guid Uid { get; init; } = Guid.NewGuid();

        public ScopedAService() 
        {
            Debug.WriteLine($"{this.Uid} - {this.GetType().Name} Inatance Created");
        }
    }
}
