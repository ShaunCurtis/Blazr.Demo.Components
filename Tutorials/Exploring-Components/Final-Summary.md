# Exploring Components - Summary

The intent of this set of tutorials was to explore in detail the Blazor Component.  In short: take your understanding of components to the next level.

We've created a base component called `Component`.  It's probsbly no suprise that we've created is a replica of `ComponentBase`.

You can review the official `ComponentBase` code here - https://github.com/dotnet/aspnetcore/blob/main/src/Components/Components/src/ComponentBase.cs.

It's a black box replica, not a copy.  The public properties are identical, and provide the same functionality. Internally there are differences in coding patterns for certain functionality.  The greatest divergence is in the implementation of `OnParametersSet{Async}` and `OnInitialized{Async}`

Those differences are intentional.  Code styles and C# functionality has changed since `ComponentBase` was first written.  Blazor was in it's infancy.  I hope that my code is more readable and understandable.  You can be the judge.

Before departing I'd like to throw a challenge at you.

Consider this:

How much of the functionality in the component do you use in a single component.  How many CPU cycles [and they cost money in a hosted service environment] ar going down the drain running code that doesn't do anything.

Take the time to read the following article:

https://github.com/ShaunCurtis/Blazr.Components/blob/master/Documents/Leaner-Meaner-Greener-Components.md


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



