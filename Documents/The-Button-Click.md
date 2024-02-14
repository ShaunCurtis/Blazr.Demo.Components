# The Button Click

In this article I hope to enlighten you on what actually happens behind the scenes when you do things like click a button in the Blazor UI.

If you're already conversant with the topic, this is a conceptual exploration.  It's purpose is to enlighten those that aren't.  It's not to provide a detailed functional description of the implementation details.  If you want that, go dig into the code or read some of the deep dive articles by the experts and the code writers.

I'll use a modified version of `Counter` for this demonstration.

### Counter

1. It has an asynchronously next count method: the call yields control behaving like a true asynchronous operation.

2. The `ComponentBase` UI event handler `IHandleEvent.HandleEventAsync`is overridden.  It simply calls the event handler: no calls to `StateHasChanged`.  These are handled within `IncrementCountAsync` so we can see what's really happening.

3. There's a loader alert displayed while the async operation is running.

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

    private async Task IncrementCountAsync()
    {
        _loading = true;

        var awaiter = DoSomeAsyncWork.GetNextAsync(currentCount);
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

### DoSomeAsyncWork

`DoSomeAsyncWork` is the class wrapper for our async operation.  It has one static public method: `GetNextAsync`.  It uses a timer to provide the async activity.  We'll see how it works as we walk through the demo.

```csharp
public class DoSomeAsyncWork
{
    private TaskCompletionSource<int> _taskCompletionSource = new();
    private Timer? _timer;
    private int _count;

    private Task<int> GetAsync(int value, int delay = 1000)
    {
        _count = value;
        _timer = new(OnTimerExpired, null, delay, 0);
        _taskCompletionSource = new();
        return _taskCompletionSource.Task;
    }

    private void OnTimerExpired(object? state)
    {
        _count++;
        _taskCompletionSource.SetResult(_count);
        _timer?.Dispose();
    }

    public static Task<int> GetNextAsync(int value, int delay = 2000)
    {
        var work = new DoSomeAsyncWork();
        return work.GetAsync(value, delay);
    }
}
```

## The Synchronisation Context

All UI based applications use a *synchronisation context* to manage UI activity.  It's purpose is to ensure an orderly application of changes to the UI.

The *Blazor Synchronisation Context* manages operations that apply changes to the DOM. It's designed to:

 - provide a single virtual thread of execution for the UI within an asynchronous operating environment
 - prioritise posted work over new UI events [finish what it's started before processing a new UI event].

At the heart of a *Synchronisation Context* is a message loop/pump/queue [call it what you wish] that processes one or more queues containing delegate code blocks.  In the Blazor context, that's three queues:

1. The normal *Post* queue used by background threads to post continuations back to the *synchronisation context*.  Calling `InvokeAsync` on a component posts the submitted delegate to this queue.
2. The *Render* queue.  This drives render tree and DOM updates  The Renderer posts `RenderFragments` to the *synchronisation context's* *Post* queue.
3. The *UI Event* queue.  This is where UI event delegates are queued.  This queue has a lower priority than the *Post* queue.

We'll see these queues in action shortly.


## The Button Click

When the `Counter` component is rendered, the Blazor JS environment registers a handler with the browser on the button click event.

When you click the button, that event is fired, and transmitted through JSInterop into the Blazor SPA session.  The relevant event handler, in our case `GetNextCountAsync`,  is queued onto the Event Queue.

Let's assume the *synchronisation context* is idle, so it runs `GetNextCountAsync` immediately.

This is our primary execution thread.  The first code block runs synchronously. It sets `_loading` to true and then calls `GetNextAsync`.

This:

1. Sets `_loading` to true. 
2. Calls `GetNextAsync` which:
3. Saves the count internally.
4. Creates a new Timer.
5. Creates a new `TaskCompletionSource<int>`.
6. Returns the `Task` associated with the `TaskCompletionSource<int>` instance.

Lets dissect those actions.

Step 1 updates `_loading`, mutating the state of the component.  It's internal state is now out of sync with it's displayed state.

Step 2 calls `GetNextAsync` and jumps the execution context to an instance of `DoSomeAsyncWork` created by the static method.  

Step 3 saves the count internally. We can increment and return it when the async operation completes.

Step 4 creates a `System.Threading.Timer`.  We pass it a `TimerCallback` delegate [`OnTimerExpired`], a `null` state and a period of `delay` milliseconds.  We set the repeat interval to 0 so it only runs once.

> We need to digress at this point to understand timers.  A DotNetCore application has one [and only one] Timer Queue.  It's an object that implements the singleton pattern.  It has a queue of registered timers and a message loop running on a background thread. At any one time, there may be many *TimeOut* timers running on the timer service.  During normal operations these are destroyed before they time out.  When we create our timer it's automatically added to the queue.  When it completes, the timer service runs the registered `TimerCallback` on a threadpool thread, passing it the provided nullable `state` object.

When we post the timer to the queue, we pass responsibility to invoke the callback to the timer service.

Step 5 creates a `TaskCompletionSource` instance.  

> Another digression. This is an object that provides mechanisms for manually creating and controlling a `Task`.  It's associated `Task` is "running" when the `TaskCompletionSource` initializes: `IsCompleted` is `false`.  We can set it as cancelled, an exception or complete whenever we wish.  the Task captures the current *synchronisation context*.  It uses this to post any registered continuations if `ConfigureAwait` is `true` [the default].  

Step 6 returns the associated `Task`: `IsCompleted` is `false`.

`GetNextAsync` is now complete.  We're back in `IncrementCountAsync` in `Counter`.

It checks the state of the returned Task.  It's incomplete so calls `StateHasChanged`.  We're on the  *synchronisation context* so that's OK.  `StateHasChanged` passes the component's render fragment to the renderer, which posts it to the synchronisation context's *Post* queue.

We now have the running code and a queued post on the *synchronisation context*.

Our next step is to await the awaiter.  In the compiled code any code block following an `await` is bundled up into a separate code block.  Our looks like this:: 

```
{
    currentCount = awaiter.Result;
    _loading = false;
    this.StateHasChanged();
}
```

This is added as a continuation to the *awaiter* and our code block completes execution.  The responsibility for running the continuation is passed to the process behind the awaiter, in our case `TaskCompletionSource`, when it's set to complete.

At this point it's worth looking at what we have:

1. A instance of `DoSomeAsyncWork` in memory.
1. A timer object registered with the Timer service with a reference to the `OnTimerElapsed` method of the `DoSomeAsyncWork` instance.
1. A `Task` owned by the `DoSomeAsyncWork` instance with a continuation associated with it.
1. A queued `RenderFragment` on the *synchronisation context*.

### Running the RenderFragment

The *synchronisation context* message loop now executes the queued render fragment.  This updates the component's DOM fragment based on `Counter`'s state and pushes those updates to the UI.  This triggers an `OnAfterRender` UI event which is queued on the UI Event queue.

 The *synchronisation context* has completed the execution of the render fragment, so it's idle.  It executes any registered `OnAfterRender{Async]` handlers.  We don't have any so it quickly completes.

 Pause.
 
 There's nothing happening until the timer completes.

 When that happens, the timer service schedules the callback on a Threadpool thread.  Note, a threadpool thread, not the *synchronisation context*: the Timer Service has no concept of a *synchronisation context*.

 This code gets executed:

 ```csharp
_count++;
_taskCompletionSource.SetResult(_count);
_timer?.Dispose();
```

The key bit of activity is setting the result on `_taskCompletionSource`.  This:

1. Sets the value of the `Result` property on the Task.
1. Sets the Task's state to completed.
1. *Posts* any continuations to the captured *synchronisation context* if one exists and `ConfigureAwait` has been set to true, or posts any continuations to the Threadpool.


The *synchronisation context* isn't busy, so it runs the posted continuation:

```
{
    currentCount = awaiter.Result;
    _loading = false;
    this.StateHasChanged();
}
```
It sets `currenCount` to the result from the task, sets the `Counter` state, and schedules another render [the details of which we have already covered above].

## Some Task Aways

It's important to realise that a thread can only do one thing at once. It can't watch for something to happen while it's doing something else.

When a method yields control in an `await`, whatever is being awaited is running on another thread.  It must implement the `Awaitable` pattern and is responsible for:

 - assigning any value returned by the awaited method, 
 - setting the awaitable state and 
 - scheduling any registered continuations on the appropriate execution context.  
 
The various incarnations of `Task` are the most common *awaitable* you will use.

When a method yields control, it's finished.  There's no black magic.  If the Task it returns is *Not Completed*, it's passed the buck for completing the job to the process behind the Task.  That process is running on another thread.
