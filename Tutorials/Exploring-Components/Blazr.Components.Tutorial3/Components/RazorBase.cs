using Microsoft.AspNetCore.Components.Rendering;

namespace Blazr.Components;

public abstract class RazorBase
{
    protected abstract void BuildRenderTree(RenderTreeBuilder builder);
}
