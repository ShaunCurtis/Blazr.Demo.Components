using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace Blazr.Components.Services
{
    public class ComponentServiceHandle
    {
        private readonly ComponentServiceProvider _componentServiceProvider;
        
        public ComponentServiceHandle(ComponentServiceProvider componentServiceProvider) 
        {
            _componentServiceProvider = componentServiceProvider;
        }

        public TService? GetService<TService>(Guid serviceKey)
           where TService : class
           => _componentServiceProvider.GetOrCreateService<TService>(serviceKey);
    }
}
