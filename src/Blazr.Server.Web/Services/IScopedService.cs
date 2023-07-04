using System.Diagnostics;

namespace Blazr.Server.Web.Services
{
    public interface IScopedService
    {
        public Guid Uid { get; init; }
    }
}
