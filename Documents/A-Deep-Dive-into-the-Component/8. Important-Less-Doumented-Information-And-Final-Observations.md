# Some Final Observations

1. There's a tendency to pile too much code into `OnInitialized` and `OnInitializedAsync` and then use events to drive `StateHasChanged` updates in the component tree.  Get the relevant code into the right places in the lifecycle and you won't need the events.

2. There's a temptation to start with the non-async versions (because they're easier to implement) and only use the async versions when you have to, when the opposite should be true.  Most web based activities are inherently async in nature.  I never use the non-async versions - I work on the principle that at some point I'm going to need to add async behaviour.
   
3. `StateHasChanged` is called far to often, normally because code is in the wrong place in the component lifecycle, or the events have been coded incorrectly.  Ask yourself a challenging "Why?" when you type `StateHasChanged`.

4. Components are underused in the UI.  The same code/markup blocks are used repeatedly.  The same rules apply to code/markup blocks as to C# code.

5. Once you really, REALLY understand components, you stop fighting thw UI to achieve what you want.  You code will be cleaner and work as designed.
   