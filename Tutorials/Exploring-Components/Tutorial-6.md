# Component Lifecycle Methods

Let's assume we want to load some data into our component from a data source, and update that data when a Parameter changes.

To implement this we need to add functionality to `SetParametersAsync`.

### The Two Step Render

The previous tutorial introduced the two set render.  Run an async process by capturing it's Task.  If it yields schedule a component render by calling `StateHasChanged`.  Wait for everything to complete.  Run a `StateHasChanged` to apply an state change to the rendered output.

We can capture the if behaviour in a generic method:

```csharp
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
```

With this we can refactor `HandleEventAsync`:

```csharp
async Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem item, object? obj)
{
    var uiTask = item.InvokeAsync(obj);
    await this.CheckIfShouldRunStateHasChanged(uiTask);
    this.StateHasChanged();
}
```

### The Parameter Set Methods

Add `OnParametersSet` and `OnParametersSetAsync` virtual methods.

```csharp
protected virtual void OnParametersSet()
{ }

protected virtual Task OnParametersSetAsync()
    => Task.CompletedTask;
```

Add `OnInitialized` and `OnInitializedAsync` virtual methods.

```csharp
protected virtual void OnInitialized()
{ }

protected virtual Task OnInitializedAsync()
    => Task.CompletedTask;
```

These are the four methods that can be overridden in inheriting components.

Add an private state variable to track initialization state.

```csharp
    private bool _isInitialized = true;
```

Add a `ParametersSetAsync` method to our component.  Note `StateHasChanged` has migrated to this method and we call out two child methods.

```csharp
protected async Task ParametersSetAsync()
{
    Task? initTask = null;
    var hasRenderedOnYield = false;

    // If this is the initial call then we need to run the OnInitialized methods
    if (_!isInitialized)
    {
        this.OnInitialized();
        initTask = this.OnInitializedAsync();
        hasRenderedOnYield = await this.CheckIfShouldRunStateHasChanged(initTask);
        _isInitialized = true;
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
```

Tutorial List:

1. [Introduction](./Introduction.md)
2. [What is a Component?](./Tutorial-1.md)
3. [Our First Component](./Tutorial-2.md)
4. [RenderFragments](./Tutorial-3.md)
5. [Parameters](./Tutorial-4.md)
6. [UI Events](./Tutorial-5.md)
7. [Component Lifecycle Methods](./Tutorial-6.md)
8. [The Rest](./Tutorial-7.md)
9. [Summary](./Final-Summary.md)
