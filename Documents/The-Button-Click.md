# The Button Click


What really happens when you click a button in the Blazor UI is a little hazy for many.  In this post/article I'll provide a fairly high level conceptual demonstration of what's going on behind the scenes.

If you're already conversant with the topic, this is probably not for you.  The real code implementation is a little different and more complex.  Go dig into the code base on GitHub, examime generated code in SharpLabs, or read some of the deep dive articles by the experts and the code writers.

### Counter

I use a modified version of `Counter` for this demonstration.

1. It has an asynchronous `IncrementCount` method that yields control and behaves like a true asynchronous operation.

2. `IHandleEvent.HandleEventAsync` is overridden: it simply calls the event handler, no built in calls to `StateHasChanged`.  These are now in `IncrementCountAsync` so we can see what's really happening.

3. There's a loader alert displayed while the async operation is running to show the UI is responsive.

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

`DoSomeAsyncWork` is the class wrapper for the async operation.  It has one static public method: `GetNextAsync`.  It uses a timer to provide the async functionality.  We'll see how it works as we walk through the demo.

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

All UI based applications use a *synchronisation context* to manage UI activity.

The *Blazor Synchronisation Context* manages operations that apply changes to the DOM. It's designed to:

 - provide a single virtual thread of execution for the UI within an asynchronous operating environment,
 - prioritise posted work over new UI events [finish what it's started before processing a new UI event].

At the heart of a *Synchronisation Context* is a message loop/pump/queue [call it what you wish] that processes delegates posted on queues.  

> A short digression on delegates if you don't know what they are.  A delegate is a type that represents references to methods with a particular signature: parameter list and return type. When you instantiate a delegate, you can associate its instance with any method with a compatible signature and return type. You can invoke (or call) the method through the delegate instance.

In the Blazor context there's:

1. The normal *Post* queue used by background threads to post continuations back to the *synchronisation context*.  Calling `InvokeAsync` on a component posts the submitted delegate to this queue.
2. The *Render* queue.  This drives render tree and DOM updates  The Renderer posts `RenderFragments` to the *synchronisation context's* *Post* queue.  Calling `StateHasChanged` places a render fragment in this queue.
3. The *UI Event* queue.  This is where UI event delegates are queued.  This queue has a lower priority than the *Post* queue.  A button click or input update ends up in this queue.

We'll see these queues in action shortly.

## The Button Click

When `Counter` is rendered, the Blazor JS environment registers a handler with the browser on the button click event.

When you click the button, that event is fired, and transmitted through JSInterop into the Blazor SPA session.  The relevant event handler, in our case `GetNextCountAsync`, is queued on the UI Event Queue.

Assuming the *synchronisation context* is idle, `GetNextCountAsync` executes immediately.  This is our primary execution code block.

The execution sequence is:

1. Sets `_loading` to true in the `Counter` instance. 
2. Jumps object context to `GetNextAsync`.
3. Creates an instance of `DoSomeAsyncWork`.
4. Calls `GetAsync` on the instance.
5. Saves the count internally in the instance.
6. Creates a new Timer on the Timer Service.
7. Creates a new `TaskCompletionSource<int>` instance.
8. Returns the `Task` associated with the `TaskCompletionSource<int>` instance.

Dissecting this:

Step 1 updates `_loading`, mutating the state of the component.  It's internal state is now out of sync with it's displayed state.

Steps 2, 3 and 4 call `GetNextAsync` which jumps the object context to an instance of `DoSomeAsyncWork` created by the static method.  

Step 5 saves the count internally. It can be incremented and returned when the async operation completes.

Step 6 creates a `System.Threading.Timer`, passing it a `TimerCallback` delegate [`OnTimerExpired`], a `null` state and a period of `delay` milliseconds.  The repeat interval is 0 so it only runs once.

> A short digression on timers.  A DotNetCore application has one [and only one] Timer : I refer to this as the Timer Service.  It's an object that implements the singleton pattern.  It has a queue of registered timers and a message loop to service that queue running on a background thread. It manages system *TimeOut* timers as well as timers we create.  When we create our timer, it's automatically added to the queue.  When it completes, the timer service runs the registered `TimerCallback` on a threadpool thread, passing it the provided nullable `state` object.

When we post the timer to the queue, we pass responsibility to invoke the call back to the timer service.

Step 7 creates a `TaskCompletionSource` instance.  

> Another digression. `TaskCompletionSource` provides mechanisms for manually creating and controlling a `Task`.  It's associated `Task` is "running" when the `TaskCompletionSource` initializes: `IsCompleted` is `false`.  It can be set to cancelled, an exception or complete at any time.  When initialized, the Task captures the current *synchronisation context* which it uses to post registered continuations if `ConfigureAwait` is `true` [the default].  

Step 8 returns the associated `Task`: note `IsCompleted` is `false`.

`GetNextAsync` is now complete.  Object execution is now back in `IncrementCountAsync` in `Counter`.  It checks the state of the returned Task.  

It's incomplete so calls `StateHasChanged`: that's OK because the execution context is the *synchronisation context*.  `StateHasChanged` passes the component render fragment to the renderer, which wraps it in an anonymous method and posts it to the *synchronisation context*'s *Post* queue.

Note that the *synchronisation context* now has a queued post as well as the running code.

The next step is to await the awaiter provided by the returned task.  

> Yet another digression. `async .... await` will only await a method that returns an object that implements the *awaitable* pattern: a `GetAwaiter` method that returns an object that implements the *awaiter* pattern.  `Task` in all it's guises implements this pattern. 

In the compiled code any code block following an `await` is bundled up into a separate code block.  Our's looks like this:: 

```
{
    currentCount = awaiter.Result;
    _loading = false;
    this.StateHasChanged();
}
```

The main code block's final action is to add this continuation to the *awaiter*.  It passes responsibility for running the continuation to the process behind the *awaitable*.  In our case `TaskCompletionSource`, when it completes.

At this point it's worth looking at what we have:


1. The main code block running on the *synchronisation context* has completed.
1. An instance of `DoSomeAsyncWork` in memory.
1. A timer object registered with the Timer service with a reference to the `OnTimerElapsed` method of the `DoSomeAsyncWork` instance.
1. A `Task` owned by the `DoSomeAsyncWork` instance with a continuation associated with it.
1. A queued `RenderFragment` on the *synchronisation context*.
 
### Running the RenderFragment

The *synchronisation context* message loop now executes the queued render fragment.  This updates the component's DOM section based on `Counter`'s state and pushes those updates to the UI.  In our case it displays the alert.  This in turn triggers an `OnAfterRender` UI event which gets queued on the UI Event queue.

 The render fragment execution is complete so the *synchronisation context* is idle.  It executes any registered `OnAfterRender{Async]` handlers.  We don't have any, so it quickly completes.

 ### The Wait

 Pause.
 
 There's nothing happening. Our primary execution thread has run to completion.  The Ui has been updated to reflect the current state of `Countwr`.  The *synchronisation context* is idle.  The timer hasn't expired.

 When the timer completes, the timer service schedules the callback on a Threadpool thread.  Note, a threadpool thread, not the *synchronisation context*: the Timer Service has no concept of a *synchronisation context*.

 This code gets executed on that thread:

 ```csharp
_count++;
_taskCompletionSource.SetResult(_count);
_timer?.Dispose();
```

The key bit of activity is setting the result on `_taskCompletionSource`.  

Behind the scenes, the `TaskCompletionSource`:

1. Sets the value of the `Result` property.
1. Sets the Task's state to completed.
1. *Posts* any continuations registered on the Task to the captured *synchronisation context* if one exists and `ConfigureAwait` has been set to true.  If not, posts any continuations to the Threadpool.

The important action is the execution context switching.  The callback code runs on a background threadpool thread, but the continuations are switched to the saved *synchronisation context*.

The *synchronisation context* isn't busy, so it runs the posted continuation:

```
{
    currentCount = awaiter.Result;
    _loading = false;
    this.StateHasChanged();
}
```
It sets `currentCount` to the result from the task, sets the `Counter` state, and schedules another render [the details of which we have already covered above].

## Some Take Aways

UI code is executed as blocks of code posted to the *synchronisation context* as delegates.  

A thread can only do one thing at once. It can't watch for something to happen while it's doing something else.

When a method yields control in an `await`, whatever is being awaited is running on another thread.  It must implement the `Awaitable` pattern and is responsible for:

 - assigning any value returned by the awaited process, 
 - setting the *awaitable* state, and 
 - scheduling any registered continuations on the appropriate execution context.  
 
The various incarnations of `Task` are the most common *awaitables* you will use.

When a method yields control, it's finished.  There's no black magic.  If the Task returned by the method is *Not Completed*, the method has passed the buck to another process running on another thread to complete the job.

In reality the compiler totally reworks the code in every `async .. await` method into a new method and an internal *Async State Machine* where the original method is split into code blocks based on await statements.  I'll cover the *Async State Machine* in another article.
