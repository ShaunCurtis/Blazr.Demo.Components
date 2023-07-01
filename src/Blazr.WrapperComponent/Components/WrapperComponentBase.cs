using Microsoft.AspNetCore.Components;

namespace Blazr.WrapperComponent.Components
{
    public class WrapperComponentBase : BlazrComponentBase
    {
        //protected RenderFragment Content => (builder) => this.BuildRenderTree(builder);
        protected RenderFragment Content { get; set; }

        public WrapperComponentBase() : base() 
        {
            Content = (builder) => this.BuildRenderTree(builder);
        }
    }
}
