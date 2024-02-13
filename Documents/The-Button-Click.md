# The Button Click

In this article I will hopefully enlighten you on what actually happnes behind the scenes.  This is a conceptual exploration: the real implementations are a little different.

I've taken counter and changed it a little:

1. We get the next count asynchronously: the call yields control behaving like a true asynchronous method.  
2. I've overidden The `ComponentBase` UI event handler `IHandleEvent.HandleEventAsync` to just call the actual event handler with no calls to `StateHasChanged`.  These are handled within `IncrementCountAsync` so we can see what is really happening.

```csharp
@page "/counter"
@implements IHandleEvent
<PageTitle>Counter</PageTitle>

<h1>Counter</h1>

<p role="status">Current count: @currentCount</p>

<button class="btn btn-primary" @onclick="IncrementCountAsync">Click me</button>

@if(_loading)
{
    <div class="alert alert-danger m-2">Loading</div>
}

@code {
    private int currentCount = 0;
    private bool _loading = false;
    private DoSomeAsyncWork _doSomeAsyncWork = new();

    private async Task IncrementCountAsync()
    {
        _loading = true;

        var awaiter = _doSomeAsyncWork.GetNextAsync(currentCount);
        if(!awaiter.IsCompleted)
        {
            this.StateHasChanged();
           currentCount = await awaiter;
        }
        _loading = false;
        this.StateHasChanged();
    }

    async Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem item, object? obj)
    {
        await item.InvokeAsync(obj);
    }
}
```

`DoSomeAsyncWork` looks like this.  It uses a timer to simulate asynbc activity.  We'll see how it works as we walk through the events.

```csharp
public class DoSomeAsyncWork
{
    private TaskCompletionSource<int> _taskCompletionSource = new();
    private Timer? _timer;
    private int _count;

    public Task<int> GetNextAsync(int value, int delay = 2000)
    {
        _taskCompletionSource = new();
        _count = value;
        _timer = new(OnTimerExpired, null, delay, 0);
        return _taskCompletionSource.Task;
    }

    private void OnTimerExpired(object? state)
    {
        _count++;
        _taskCompletionSource.SetResult(_count);
        _timer?.Dispose();
    }
}
```

## The Synchronisation Context

Some words.


## The Button Click

When the `Counter` component is rendered, the Blazor JS environment registers a handler with the browser on the button click event.

When you click the button that event is fired, and transmitted through JSInterop into the Blazor SPA session UI Event Handler which *posts* `GetNextCountAsync` onto the Event Queue.  Let's assume the Synchronisation Context's message queue is idle, so it runs `GetNextCountAsync` immediately.

The code runs in the context of the instance of `Counter` on the *Synchronisation Context*.  The first block of code is synchronous. It sets `_loading` to true and then calls `GetNextAsync`.

This:

1. Creates a new `TaskCompletionSource<int>`.
2. Saves the count internally.
3. Creates a new Timer.
4. Returns the `Task` associated with the `TaskCompletionSource<int>` instance.

Lets disect those actions.

A `TaskCompletionSource` is an mechanism for manually creating and controlling a `Task`.

```csharp
_taskCompletionSource = new();
```

creates a new *running* instance: it's *Not Complete*.  At any time we can set it as cancelled, an exception or complete.  What we do at the completion of this sequence is return it as is i.e. `IsComplete` is `false`.  As an aside, the Task captures the *synchronisation context* from the execution thread when it's created.  This means that if `ConfigureAwait` is `true` it will have the *synchronisation context* to post any continuations to when it completes.

We save the count internally so we can increment it and return the incremented value when we set the `TaskCompletionSource<int>` to complete.

We create a *raw* `System.Threading.Timer`.  We pass it a `TimerCallback` delegate [`OnTimerExpired`], a `null` state and a period of `delay` milliseconds.  We set the repeat interval to 0 i.e. it only runs once.

We need to digress a little and understand timers.  An application has one [and only one] Timer Queue.  It runs on it's own background thread and services all registered timers. At any one time there will potentially be many *TimeOut* timers running on backgroiund tasks.  During normal operations they are destroyed before they time out.  Our timer gets added to the queue, and when it completes, the timer service runs the registered `TimerCallback` on a threadpool thread, passing it the provided `state` object [which is nullable].

So when we post the timer to the queue, we keep a reference to it, but pass the management and the responsibility to invoke the callback to the timer service.  Our execution thread is finished with it.  It returns the Task.

We're now back in `IncrementCountAsync` in `Counter`.

It checks the state of the returned Task.  As it's incomplete it calls `StateHasChanged`.  That works becuse our current excecution thread is the synchronisation context.  What that does is queues a component render: actually posting a RenderFragment to the synchronisation context's queue.

At this point the synchronisation context is executing our code block and has a render fragment queued behind us.

Out next step is to await the awaiter.  What this entails is bundling up the code following the await: 

```
{
    currentCount = awaiter.Result;
    _loading = false;
    this.StateHasChanged();
}
```

And adding as a continuation to the awaiter:

```csharp
await awaiter.ContinueWith(t =>
{
    var a = t.Result;
    _loading = false;
    this.StateHasChanged();
});
```

So our current code block completes.  The responsibility for running the continuation is passed to the awaiter after it has completed.
 

