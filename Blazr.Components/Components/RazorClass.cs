using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazr.Components;

public abstract class RazorClass
{
    protected abstract void BuildRenderTree(RenderTreeBuilder builder);

    public RenderFragment Content => (builder) => BuildRenderTree(builder);
}
