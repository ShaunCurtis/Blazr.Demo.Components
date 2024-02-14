# The Button Click

In this article I hope to enlighten you on what actually happens behind the scenes when you do things like click a button in the Blazor UI.

If you're are already conversant with the topic, this is a conceptual exploration.  It's purpose is to enlighten those that aren't.  It's not to provide a detailed functional description of the implementation details.  If you want that, go dig into the code or read some of the deep dive articles by the experts and the writers of the code.


For my demonstration code, I've taken `Counter` and changed it a little:

1. It now gets the next count asynchronously: the call yields control behaving like a true asynchronous operation.

2. The `ComponentBase` UI event handler `IHandleEvent.HandleEventAsync`is overridden to just call the actual event handler: there are no calls to `StateHasChanged`.  These are handled within `IncrementCountAsync` so we can see what's really happening.

3. There's a loader alert that is shown while the async operation is running.

Here's `Counter`:

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

`DoSomeAsyncWork` looks like this.  It uses a timer to provide the async activity.  We'll see how it works as we walk through the events.

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

All UI based applications utilize a *Synchronisation Context* to manage activity on the UI objects.  It's purpose is to ensure an orderly application of changes to the UI.

The *Blazor Synchronisation Context* manages operations that apply changes to the DOM. It's designed to provide a single virtual thread of execution for the UI within an asynchronous operating environment and prioritise posted work over new UI event activity [finish what it's started before processing a new UI event].

At the heart of a *Synchronisation Context* is a message loop that processes one or more queues containing code blocks in the form of delegates.  In the Blazor context, that's three queues:

1. The *Post* queue used by background threads to post continuations back to the *Synchronisation Context*.  Calling `InvokeAsync` on a component posts the submitted delegate to this queue.
2. The *Render* queue.  This drives Render Tree and DOM updates  The Renderer posts `RenderFragments` to the *Post* queue.
3. The *UI Event* queue.  This is where UI event handler delegates are queued.  This queue has a lower priority than the *Post* queue.

We'll see these queues in action shortly.


## The Button Click

When the `Counter` component is rendered, the Blazor JS environment registers a handler with the browser on the button click event.

When you click the button, that event is fired, and transmitted through JSInterop into the Blazor SPA session to the UI Event Handler which queues `GetNextCountAsync` onto the Event Queue.  Let's assume the Synchronisation Context's message queue is idle, so it runs `GetNextCountAsync` immediately.

This is our primary execution thread and runs in the context of the instance of `Counter` on the *Synchronisation Context*.  The first block of code runs synchronously. It sets `_loading` to true and then calls `GetNextAsync`.

This:

1. Sets `_loading` to true. 
2. Calls `GetNextAsync` which:
3. Saves the count internally.
4. Creates a new Timer.
5. Creates a new `TaskCompletionSource<int>`.
6. Returns the `Task` associated with the `TaskCompletionSource<int>` instance.

Lets dissect those actions.

Step 1 updates `_loading`, mutating the state of the component.  It's internal state is now out of sync with it's displayed state.

Step 2 calls `GetNextAsync` and jumps the execution context to to the `_doSomeAsyncWork` instance of `DoSomeAsyncWork` held by `Counter`.  

Step 3 saves the count internally, so we can increment it and return the incremented value when we complete.

Step 4 creates a *raw* `System.Threading.Timer`.  We pass it a `TimerCallback` delegate [`OnTimerExpired`], a `null` state and a period of `delay` milliseconds.  We set the repeat interval to 0 so it only runs once.

We need to digress a this point to understand timers.  An DotNetCore application has one [and only one] Timer Queue.  It's an object that implements the singleton pattern.  It has a queue of registered timers and a message loop running on a background thread. At any one time, there may be many *TimeOut* timers running on the timer service.  During normal operations these are destroyed before they time out.  When we create our timer it's automatically added to the queue.  When it completes, the timer service runs the registered `TimerCallback` on a threadpool thread, passing it the provided nullable `state` object.

When we post the timer to the queue, we keep a reference to it, but pass the management and the responsibility to invoke the callback to the timer service.

Step 5 creates a `TaskCompletionSource` instance.  This is an object that provides mechanisms for manually creating and controlling a `Task`.  It initializes with it associated `Task` in the *running* state: it's *Not Complete*.  We can set it as cancelled, an exception or complete whenever we wish.  the Task captures the *synchronisation context* from the execution.  It uses this to post any registered continuations if `ConfigureAwait` is `true` [the default].  

Step 6 returns the associated `Task`: `IsComplete` is `false`.

`GetNextAsync` is now complete.  We're now back in `IncrementCountAsync` in `Counter`.

It checks the state of the returned Task.  Here, it's incomplete so calls `StateHasChanged`.  That works because our current execution thread is the synchronisation context.  This passes the component's render fragment to the renderer which posts it to the synchronisation context's *Post* queue.

The *synchronisation context* now has a queued post behind our executing code.

Our next step is to await the awaiter.  In the compiled code any code block following an `await` is bundled into a code block.  Our looks like this:: 

```
{
    currentCount = awaiter.Result;
    _loading = false;
    this.StateHasChanged();
}
```

This is added as a continuation to the awaiter Task and our code block completes execution.  The responsibility for running the continuation is passed to the TaskCompletionSource when it's set to complete.

At this point it's worth looking at what we have:

1. The `_doSomeAsyncWork` object in memory.
1. A timer object registered with the Timer service with a reference to the `OnTimerElapsed` method of `_doSomeAsyncWork`.
1. A `Task` owned by `_doSomeAsyncWork` with a continuation associated with it.
1. `Counter` holding a reference to `_doSomeAsyncWork`.
1. A queued `RenderFragment` on the *synchronisation context*.

### Running the RenderFragment

The *synchronisation context* message loop executes the queued render fragment.  This updates the component's DOM fragment based on `Counter`'s state and updates the UI.  This triggers a `OnAfterRender` UI event which is queued on the UI Event queue.

 At this point the *synchronisation context* has completed the execution of the render fragment, so is idle.  It executes any registered `OnAfterRender{Async]` handlers.  We don't have any so it completes.

 Pause.  There's nothing happening until the timer completes.

 When that happens, it schedules the callback on a threadpool thread.  Note, a threadpool thread, not the *synchronisation context*: the Timer Service has no concept of a *synchronisation context*.

 This code gets executed:

 ```csharp
_count++;
_taskCompletionSource.SetResult(_count);
_timer?.Dispose();
```

The key bit of activity is setting the result on `_taskCompletionSource`.  This:

1. Sets the value of the `Result` property on the Task.
1. Sets the Task's state to completed.
1. *Posts* any continuations to the captured *synchronisation context* if one exists and `ConfigureAwait` has been set to true.  Or posts any continuations to the Threadpool.


The *synchronisation context* isn't busy, so it runs the posted continuation:

```
{
    currentCount = awaiter.Result;
    _loading = false;
    this.StateHasChanged();
}
```
It gets the result from the task, sets the `Counter` state and schedules another render [the details of which we have already covered above].

## Some Task Aways

It's important to realise that a thread can only do one thing at once. It can't watch for something to happen while it's doing something else.

When a method yields control in an `await`, whatever is being awaited is running on another thread.  It must implement the `Awaitable` pattern and is responsible for assigning any value returned by the awaited method, setting the awaitable state and scheduling any registered continuations on the appropriate execution context.  The various incarnations of `Task` are the most common *awaitable* you will use.

When a method yields control, it's finished.  There's no black magic.  If the Task it returns is *Not Complete*, it's just handed passed the buck for completing the job to someone else.
