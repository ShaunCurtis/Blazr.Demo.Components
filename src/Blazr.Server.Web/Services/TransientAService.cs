using System.Diagnostics;

namespace Blazr.Server.Web.Services
{
    public class TransientAService
    {
        public Guid Uid { get; init; } = Guid.NewGuid();

        public TransientAService() 
        {
            Debug.WriteLine($"{this.Uid} - {this.GetType().Name} Inatance Created");
        }
    }
}
