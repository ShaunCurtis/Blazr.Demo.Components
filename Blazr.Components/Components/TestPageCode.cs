namespace Blazr.Components;

[RouteAttribute("/TestPageCode")]
public partial class Test : MinimalBase
{
    protected override void BuildRenderTree(RenderTreeBuilder __builder)
    {
        __builder.AddMarkupContent(1, "\r\n\r\n");
        __builder.OpenComponent<MinimalComponent>(2);
        __builder.AddAttribute(3, "ChildContent", __content2);
    }

    private RenderFragment __content2 => __builder2 =>
    {
        __builder2.AddMarkupContent(4, "\r\n    Value : ");
        __builder2.AddContent(5, this.Value);
    };

    private int Value = 4;
}

