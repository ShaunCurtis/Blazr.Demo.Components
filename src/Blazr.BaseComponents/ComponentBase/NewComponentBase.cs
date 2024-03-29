﻿//============================================================
//   Code from the ASPNetCore Repository
//   Original Licence:
//
//   Licensed to the .NET Foundation under one or more agreements.
//   The .NET Foundation licenses this file to you under the MIT license.

//   https://github.com/dotnet/aspnetcore/blob/main/src/Components/Components/src/ComponentBase.cs
//   Author of Modifications: Shaun Curtis, Cold Elm Coders
//   License: Use And Donate
//   If you use it, donate something to a charity somewhere
//============================================================

using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazr.Components;

public class NewComponentBase : IComponent, IHandleEvent, IHandleAfterRender
{
    private RenderHandle _renderHandle;
    private RenderFragment _content;
    private bool _renderPending;
    private bool _hasNotInitialized = true;
    private bool _hasNeverRendered = true;
    private bool _hasCalledOnAfterRender;

    /// <summary>
    /// Frame/Layout/Wrapper Content that will be render if set
    /// </summary>
    protected virtual RenderFragment? Frame { get; set; }

    /// <summary>
    /// Razor Compiled content of BuildRenderTree.
    /// </summary>
    protected RenderFragment Body { get; init; }

    /// <summary>
    /// Unique Id that can be used to identifiy this instance of the component
    /// </summary>
    public Guid Uid { get; init; } = Guid.NewGuid();

    public NewComponentBase()
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

    public void Attach(RenderHandle renderHandle)
        => _renderHandle = renderHandle;

    public virtual async Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        await this.ParametersSetAsync();
    }

    protected async Task ParametersSetAsync()
    {
        Task? initTask = null;
        var hasRenderedOnYield = false;

        // If this is the initial call then we need to run the OnInitialized methods
        if (_hasNotInitialized)
        {
            this.OnInitialized();
            initTask = this.OnInitializedAsync();
            hasRenderedOnYield = await this.CheckIfShouldRunStateHasChanged(initTask);
            _hasNotInitialized = false;
        }

        this.OnParametersSet();
        var task = this.OnParametersSetAsync();

        // check if we need to do the render on Yield i.e.
        //  - this is not the initial run or
        //  - OnInitializedAsync did not yield
        var shouldRenderOnYield = initTask is null || !hasRenderedOnYield;

        if (shouldRenderOnYield)
            await this.CheckIfShouldRunStateHasChanged(task);
        else
            await task;

        // run the final state has changed to update the UI.
        this.StateHasChanged();
    }

    protected virtual void OnInitialized() { }

    protected virtual Task OnInitializedAsync() => Task.CompletedTask;

    protected virtual void OnParametersSet() { }

    protected virtual Task OnParametersSetAsync() => Task.CompletedTask;

    protected virtual void OnAfterRender(bool firstRender) { }

    protected virtual Task OnAfterRenderAsync(bool firstRender) => Task.CompletedTask;

    protected virtual void BuildRenderTree(RenderTreeBuilder builder) { }

    protected virtual bool ShouldRender() => true;

    /// <summary>
    /// Calls StateHasChanged and ensures it is applied immediately
    /// by yielding and giving the Renderer thread time to run.
    /// </summary>
    /// <returns></returns>
    public async Task Rendersync()
    {
        this.StateHasChanged();
        await Task.Yield();
    }

    public void StateHasChanged()
    {
        if (_renderPending)
            return;

        var shouldRender = _hasNeverRendered || ShouldRender() || _renderHandle.IsRenderingOnMetadataUpdate;

        if (shouldRender)
        {
            _renderPending = true;
            _renderHandle.Render(_content);
        }
    }

    async Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem item, object? obj)
    {
        var uiTask = item.InvokeAsync(obj);

        await this.CheckIfShouldRunStateHasChanged(uiTask);

        this.StateHasChanged();
    }

    Task IHandleAfterRender.OnAfterRenderAsync()
    {
        var firstRender = !_hasCalledOnAfterRender;
        _hasCalledOnAfterRender = true;

        OnAfterRender(firstRender);

        return OnAfterRenderAsync(firstRender);
    }

    protected async Task<bool> CheckIfShouldRunStateHasChanged(Task task)
    {
        var isCompleted = task.IsCompleted || task.IsCanceled;

        if (!isCompleted)
        {
            this.StateHasChanged();
            await task;
            return true;
        }

        return false;
    }

    protected Task InvokeAsync(Action workItem)
        => _renderHandle.Dispatcher.InvokeAsync(workItem);

    protected Task InvokeAsync(Func<Task> workItem)
        => _renderHandle.Dispatcher.InvokeAsync(workItem);
}