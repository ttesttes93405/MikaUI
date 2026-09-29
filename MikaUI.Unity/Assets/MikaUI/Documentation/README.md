# MikaUI

MikaUI is a UI management system for Unity with explicit lifecycle control.

Rather than letting each UI decide where it is attached, who controls it, or when it is cleaned up, MikaUI routes those decisions through a single managed runtime flow.

Only the party currently holding the UI token, and therefore the ownership, should be able to operate on that UI.

The main goal of this system is to make UI ownership and lifecycle explicit.

## Core Philosophy

This system is built on a few design rules:

- A UI should have a clear token holder.
- A UI should have an explicit lifetime handle.
- A parent UI should own the recovery order of its children.
- Reuse and destruction should go through the same recovery path.
- Shared UI behavior should be attached to lifecycle events, not duplicated inside every prefab script.

It is designed to answer questions like:

- Who owns this UI?
- When does its lifetime end?
- What happens to its children when it is recovered?
- If it is reusable, how does it return in a clean state?
- Where should common setup and teardown logic live?

MikaUI keeps those answers in one place.

## Ownership Model

The ownership model is the core idea behind this package.

Here, ownership means "the party currently holding the UI token." That is the party that currently controls the UI through its lifetime handle.

In most cases, that token holder is the caller that created the UI, but it may also be another object that later receives the token and takes over management of the UI.

As long as the token is alive, ownership belongs to whoever currently holds it. Once the token is disposed, that ownership formally ends.

### Create through the manager

UI instances should be created through `UIManager`, not through ad hoc `Instantiate` calls scattered across the project.

That ensures creation, registration, plugin hooks, and recovery all follow the same path.

### Hold the returned token

Creating a UI returns a token. That token is the lifetime handle for that UI instance.

As long as the token is alive, the current token holder owns the UI. When the token is disposed, that ownership ends and the system begins recovery.

### The parent-child lifecycle structure is explicit

If a UI is created with a parent slot, it becomes part of the parent's managed tree.

This does not mean ownership belongs to the parent by definition. It means the system places the child UI into the parent's recovery tree and manages it there.

In other words, this creates a managed parent-child lifecycle structure, not a redefinition of ownership through the parent.

It makes the recovery relationship explicit in the system itself rather than leaving it implied by the scene hierarchy:

- A root UI is owned directly by whoever currently holds the token.
- Child UIs are included in the parent's recovery tree.
- When a parent is recovered, its child UIs are always recovered first.

This prevents UIs from being left behind and makes nested UI flows more reliable.

### Recovery is the single exit path

When a token is disposed, the system runs recovery.

That recovery path is where the system decides whether the UI should:

- Leave the active tree
- Run teardown hooks
- Return to a pool
- Or be destroyed

This is one of the core design rules of the package.

## What Problems This Solves

MikaUI becomes especially useful when a project starts showing problems like these:

- UI is created from many unrelated places.
- Child UIs in nested flows are frequently missed during recovery.
- UI state is left behind or becomes unstable.
- It becomes unclear where a UI actually begins and ends.
- Shared behavior such as manager injection, fitting, or teardown is copied across many prefabs and becomes harder to manage consistently over time.
- Other code can reach into your UI and manipulate it without clear ownership boundaries.

This system gives the project a clear ownership and lifecycle model instead of relying on team conventions alone.

## How To Use The System

The easiest way to explain the system is to walk through its lifecycle flow.

### 1. Register what the manager may create

Create a `DefaultUIElementSource` asset and register the UI templates in it.

Each source entry defines:

- `UIName`
- `UITemplate`

When a UI is created, the source configuration determines how it is resolved.

### 2. Build the manager for your runtime context

In the Unity implementation, the manager is usually composed of the following parts:

- A UI source catalog
- An optional pool root
- A canvas root
- A canvas template
- The default plugin set

Example:

```csharp
var pool = new UnityBoundedEffectablePool<DefaultUIElementProvider.IUIElementSource>(static source => 8, poolRoot);
var uiElementProvider = new DefaultUIElementProvider(uiElementSource.GetSources(), pool);

var uiManager = new UnityUIManager(
    uiElementProvider,
    canvasRoot,
    canvasTemplate,
    plugins: null
);
```

Without pooling, construct the provider with only the sources:

```csharp
var uiElementProvider = new DefaultUIElementProvider(uiElementSource.GetSources());
```

The capacity callback is evaluated once per source. `0` keeps no idle instances;
negative capacities are invalid. Manager disposal disposes the provider and pool.
The pool owns `poolRoot` and instance destruction; Core's instance use lifecycle owns
effect cleanup and ElementID. UIManager owns TokenID and tree recovery.

### 3. Create the UI and keep the token

For a screen-level UI:

```csharp
using (var token = await uiManager.Create<UI_HelloWorld>(sortingOrder: 0))
{
    await token.UI.Invoke("World");
}
```

This pattern highlights three key ideas:

- Ask the manager to create the UI.
- Use the UI through the returned token.
- End the lifetime by disposing the token.

The `using` block is not just syntax sugar. It makes the UI lifetime explicit at the call site.

### 4. Create child UI through a slot

When a UI should become part of another UI's recovery tree, create it through a slot that carries the parent relationship.

The child UI is inserted at the position defined by the parent UI. When the parent UI recovers, the child UI is guaranteed to recover first.

### 5. Let recovery handle cleanup

Cleanup logic should not be scattered across multiple callers.

When the UI lifetime ends, dispose the token and let the manager run recovery.

That keeps cleanup consistent across:

- Parent-child ordering
- Pool return
- Canvas unregister
- Plugin teardown
- Final destruction

## Reuse, Init, And Recovery

One important detail is that the system treats first-time setup and per-use reset as separate responsibilities.

### `IUIInit`

Use `IUIInit` for one-time element initialization of visual UIs.

Use it for stable setup that should happen only once for a given element instance. This interface applies only to visual UIs created through `Create<T>()`.

### `IUIEffectable`

Use `IUIEffectable` when a visual UI needs setup and cleanup for each use cycle.

`UseEffect()` is where runtime state should be reset for the next use cycle; it may return a cleanup action that runs on recovery.

Clear temporary text, selection state, temporary listeners, and other per-use state here. `IUIReuseable` is obsolete; migrate existing implementations to `IUIEffectable`.

### Recovery semantics

For `IUIEffectable` UI with a pool configured, recovery means:

- Leave the active tree
- Run cleanup
- Move back to the pool root
- Wait for the next use

Without a pool, or when the source pool is full, recovery destroys the UI after cleanup.

This package is not centered on destruction. It is centered on lifecycle completion.

## Plugin Extension

Plugins handle behavior shared across multiple UIs.

Plugins can participate in:

- Manager installation
- UI created
- UI will recover
- UI recovered
- Virtual UI created

The default Unity package already uses plugins for basic behaviors such as:

- Injecting the manager instance
- Running `Init()` for visual UIs that implement `IUIInit`
- Renaming created and recovering UIs
- Fitting RectTransform values to the target container

Plugins make it easier to attach common behavior in a flexible way and enable or remove that behavior when needed.

## Unity-Specific Layer

The core system separates ownership and lifecycle from Unity-specific presentation details.

The Unity layer adds practical runtime behavior such as:

- Canvas allocation by sorting order
- Automatic canvas reuse
- RectTransform fitting
- Prefab lookup from `DefaultUIElementSource`
- Optional pooling for `IUIEffectable` UIs with a per-source capacity

The Unity layer acts as an adapter around that ownership and lifecycle core.

## Virtual UI

The package supports two kinds of UI objects through a shared `IBaseUI` base:

- `IVisualUI` — for visual UIs that require a GameObject and a container. Created through `Create<T>()`.
- `IVirtualUI` — for non-visual objects that participate in the same ownership and lifecycle model without requiring a GameObject. Created through `CreateVirtual<T>()`.

The separation means passing an `IVisualUI` type to `CreateVirtual<T>()` is a compile-time error, making the distinction explicit.

`IVirtualUI` is useful when a runtime object should share the token-based ownership model but has no visual representation.

Typical uses include:

- Flow controllers
- State holders
- Non-visual UI processes
- Runtime objects that should recover with a visual parent

This allows visual and non-visual UI-related objects to share the same ownership system centered on the token holder.

## Common Misunderstandings To Prevent

- The token holder is the party that actually owns the current UI lifetime.
- Ownership means whoever is currently holding the token, not simply whoever is the parent.
- A child UI is not owned only because it sits under a transform. It must be created with the correct parent context.
- `Init()` is not the same as per-open reset.
- Reuse does not mean keeping a UI alive forever.
- Manually destroying or disabling UI bypasses the ownership model.

## Summary

MikaUI is best understood as an ownership and lifecycle framework for Unity UI.

It gives each UI a clear creation path, an explicit lifetime handle, ownership centered on the token holder, and a single recovery path that decides reuse or destruction.

Once that idea is clear in the documentation, the rest of the API becomes much easier to understand.

For detailed type responsibilities, see `ClassReference.md`.
For documentation structure and additional document scope, see `DocumentationOverview.md`.
