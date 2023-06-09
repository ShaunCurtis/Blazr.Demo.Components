# Exploring Components - Introduction

Most articles and commentaries about Blazor Components treat `ComponentBase` as the starting point: the black box that does all the work.  

There's a fundimental problem with this approach.  You're thrown in at the deep end with no real understanding of what's actually going on.  The consquence of which is you write a lot of bad code, go up a lot of dead end alleys, and get thoroughly confused in the process.

In desperation you end up asking questions on sites such as Stack Overflow, and people like me asnswer them.  Almost all thr answers I've provided since Blazor was first realeased are covered in this set of articles.

This set of articles is intended to clear most of that haze. We'll build components from their core building blocks and in the process learn what is actually going on under the hood.  

With such knowledge, you should make fewer mistakes and realise there are no myths or voodoo in how components work.

Before we start we need to understand sone basic facts about components.

1. A component is a class that implements the `IComponent` interface.

2. The Renderer manages the whole render process: not us.  We can't create or destroy components in the render context.

3. The Renderer communicates with components in four ways:

   -. Calling `Attach` on the component when it first creates a component.
   - Calling `SetParametersAsync` when any component parameters have changed.
   - Invoking any handlers to registered UI events.
   - Invoking the `AfterRender` handler is one is registered.

4. The component commuicates with the Renderer though a `RenderHandle` object provided when it calls `Attach`.

5. DI Services are assigned through reflection, not in the classic way [through the constructor].


We'll explore these facts all these in detail.

1. [What is a Component?](./Tutorial1.md)
