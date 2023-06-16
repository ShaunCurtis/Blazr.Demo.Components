# Exploring Components - Introduction

Most articles and commentaries about Blazor Components treat `ComponentBase` as the starting point: the black box that does all the work.  

There's a fundimental problem with this approach.  You're thrown in at the deep end with no real understanding of what's actually going on.  The consquence of which is you write a lot of bad code, go up a lot of dead end alleys, and get thoroughly confused in the process.

In desperation you end up asking questions on sites such as Stack Overflow, and people like me answer them.  Many of the answers I've provided since Blazor was first realeased are covered in this set of articles.

I'll show you how to  build components from their core building blocks and in the process learn what is actually going on under the hood.  

The knowledge you gain should help you make fewer mistakes and realise there are no myths or voodoo in how components work: just plain cold hard logic.

Before we start we need to understand some basic facts about components.

1. A component is a class that implements the `IComponent` interface.

2. The Renderer manages the whole render process: not us.  We can't create or destroy components in the render context.

3. The Renderer communicates with components in four ways:

   - Calling `Attach` on the component when it first creates a component.
   - Calling `SetParametersAsync` when any component parameters have changed.
   - Invoking any handlers to registered UI events.
   - Invoking the `AfterRender` handler if one is registered.

4. The component communicates with the Renderer though a `RenderHandle` object provided when it calls `Attach`.

5. DI Services are assigned through reflection, not in the classic way [through the constructor].


We'll explore these facts all these in detail.

1. [Introduction](./Introduction.md)
2. [What is a Component?](./Tutorial-1.md)
3. [Our First Component](./Tutorial-2.md)
4. [RenderFragments](./Tutorial-3.md)
5. [Parameters](./Tutorial-4.md)
6. [UI Events](./Tutorial-5.md)
7. [Component Lifecycle Methods](./Tutorial-6.md)
8. [The Rest](./Tutorial-7.md)
9. [Summary](./Final-Summary.md)
