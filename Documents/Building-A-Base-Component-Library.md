# Building A Base Document Library

In the first psrt of this article I'll show you how you can add extra functionality to `ComponentBase` to address some common problems.

In the second part I'll show you how you can step outside the `ComponentBase` straightjacket and use a library of base components based on the requirement.

## A Better ComponentBase

### Frame/Layout/Wrapper

#### The Problem

One of the major issues with `ComponentBase` is you can't use it as a Frame/Layout/Wrapper component.

An example:

```csharp
// Wrapper.razor
<div class="whatever">
   the content of the child component
<div>
```

```csharp
// Index
@inherits Wrapper

// All this content is rendered inside the wrapper content
<h1>Hello Blazor</h1> 
```

This is trvial, and can be solved differently.  However, I have base forms/pages where only the inner content changes.  In a view form the implementation content looks like this.  `UIViewerFormBase` is the wrapper that contains both the boilerplate code and the content wrapper markup.

```csharp
﻿@namespace Blazr.App.UI
@inherits UIViewerFormBase<Customer, CustomerEntityService>

<div class="row">

    <div class="col-12 col-lg-6 mb-2">
        <BlazrTextViewControl Label="Name" Value="@this.Presenter.Item.CustomerName" />
    </div>

    <div class="col-12 col-lg-6 mb-2">
        <BlazrTextViewControl Label="Unique Id" Value="@this.Presenter.Item.Uid" />
    </div>

</div>
```

#### Implementation

We need two `RenderFragment` properties.

1. `Frame` is where we'll code the frame content in the child component.
2. `Body` is mapped to `BuildRenderTree`.  It's readonly and set in the constructor.


```csharp
    protected virtual RenderFragment? Frame { get; set; }
    protected RenderFragment Body { get; init; }
```

We can now refactor the constructor.

1. `Body` is mapped to BuildRenderTree.  This is the most efficient way to do this.  No expensive lambda expressions to construct on each call.

2. `_content` uses the content in `Frame` if it's not null.  Otherwise it uses the Razor compiled content in `BuildRenderTree`.

```csharp
public BlazorBaseComponent()
{
    this.Body = (builder) => this.BuildRenderTree(builder);

    _content = (builder) =>
    {
        _renderPending = false;
        _hasNeverRendered = false;
        if (Frame is not null)
            Frame.Invoke(builder);
        else
            BuildRenderTree(builder);
    };
}
```

