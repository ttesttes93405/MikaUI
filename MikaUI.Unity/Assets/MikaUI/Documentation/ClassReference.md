# MikaUI Class Reference

This document provides a type reference for the main classes and interfaces in MikaUI.

Unlike the main README, this page does not focus on philosophy. It answers a narrower question: what does each important type do, and when does it matter?

## How To Read This Page

This package is easiest to understand in three layers:

- Core lifecycle types
- Unity runtime adapters
- Plugins and helper utilities

If you are new to the package, start with `UIManager`, the control tokens, and the UI interfaces.

## Core Lifecycle Types

### `UIManager<TUI, TContainer, TSlotConfig>`

The central lifecycle coordinator.

Main responsibilities:

- Creates UI through an `IUIElementProvider`.
- Builds the parent-child ownership tree.
- Returns tokens that represent UI lifetime ownership.
- Executes plugins during create and recovery.
- Recovers an entire subtree when a token is disposed.

Use this type when you want the generic lifecycle system without committing to Unity-specific containers.

### `UIControlToken`, `UIControlToken<TUI>`, and `UIControlToken<TUI, TContainer>`

The lifetime handle for a created UI, available in three levels:

- `UIControlToken` — base type, carries token ID, element ID, name, and disposal logic.
- `UIControlToken<TUI>` — adds the UI instance; `TUI : IBaseUI`. Used for both virtual and visual UIs.
- `UIControlToken<TUI, TContainer>` — sealed; adds the container; `TUI : IVisualUI`. Used for visual UIs only.

Main responsibilities:

- Carries the runtime token ID.
- Carries the reusable element ID.
- Exposes the created UI instance.
- Triggers recovery when `Dispose()` is called.

For callers, this is the most important runtime object. Holding the token means holding the UI lifetime.

`CreateVirtual<T>()` returns `UIControlToken<T>` where `T : IVirtualUI`.  
`Create<T>()` returns `UIControlToken<T, TContainer>` where `T : IVisualUI`.

### `SlotRectConfigs` *(Unity layer)*

Layout data passed during UI creation.

Main uses:

- Describes anchors, pivot, size delta, and anchored position for slot fitting.
- Lets callers override layout instead of relying only on the template RectTransform.

This type is defined in the Unity layer as a concrete implementation of `TSlotConfig`.

### `IBaseUI`

Common base for all lifecycle-managed UI objects.

Implement this when a type needs to participate in the node tree regardless of whether it has a visual representation.

### `IVirtualUI : IBaseUI`

Marker interface for lifecycle-managed objects that do not need a visual container.

Use it for non-visual owned runtime objects such as state containers or flow controllers.

### `IVisualUI : IBaseUI`

Marker interface for visual UI managed by the system.

Any UI created through the standard visual path should implement this interface. Separating `IVisualUI` from `IVirtualUI` ensures that `CreateVirtual<T>()` only accepts pure-logic types — passing a visual UI type to it is a compile-time error.

### `IUIEffectable : IVisualUI`

Interface for visual UI that needs setup and cleanup for each use cycle.

Main responsibility:

- Provides `UseEffect()`, which runs setup and can return a cleanup action that runs when the UI is recovered.

Use this to reset per-use state, register temporary listeners, or perform other lifecycle-bound setup. `IUIReuseable` is obsolete; migrate implementations to `IUIEffectable`.

### `IUIInit : IVisualUI`

Interface for one-time initialization of visual UIs.

Main responsibility:

- Provides `Init()` for stable setup that should run once per underlying element instance.

Do not use this as a per-open reset hook. This interface extends `IVisualUI`, so it is only applicable to visual UIs managed through `Create<T>()`, not `CreateVirtual<T>()`.

### `IUITransitionable : IVisualUI`

Interface for visual UI that participates in transition timing.

Main responsibility:

- Exposes visibility and hidden-state await points.

Use this when a UI has show and hide animations that should coordinate with token handoff.

### `IVirtualSlot`

Defines only the parent relationship for lifecycle ownership.

Use this when the created object does not need a concrete visual container.

### `ISlot<TContainer> : IVirtualSlot`

Defines both a parent relationship and a concrete container.

Use this when a visual UI should be attached to a real runtime container. Because `ISlot<TContainer>` inherits `IVirtualSlot`, any slot with a container also satisfies the virtual parent relationship.

### `VirtualSlot`

Default implementation of `IVirtualSlot`.

Use this when a child relationship matters but no visual container is needed.

### `IUIElementProvider<TUI, TContainer>`

Abstract factory and recovery boundary for UI elements.

Main responsibilities:

- Produces a visual UI element by type and name.
- Produces a virtual UI element by type.
- Defines how created elements are recovered.
- Is disposed by the manager after active elements are recovered.

This type separates lifecycle orchestration from concrete instantiation and pooling.

### `UIElement<TContainer>` and `VirtualUIElement`

Provider-side creation and recovery descriptors.

These are not usually the first types that users interact with directly, but providers use them to describe how an element is created and later recovered.

### `IPlugin<TUI, TContainer, TSlotConfig>`

Base plugin contract.

Main responsibilities:

- Declares execution order through `SortingOrder`.
- Receives the installed manager instance.

This is the entry point for attaching cross-cutting behavior to lifecycle events.

### `IPluginUICreatedHandler<TUI, TContainer, TSlotConfig>`

Runs after a visual UI is created.

Use this for setup logic that should run whenever a UI enters the active tree.

### `IPluginUIWillRecoveryHandler<TUI, TContainer>`

Runs before a visual UI is recovered.

Use this for teardown preparation that should run while the UI still exists in its active form.

### `IPluginUIRecoveredHandler`

Runs after a visual UI is recovered.

Use this for post-recovery bookkeeping.

### `IPluginVirtualUICreatedHandler`

Runs after a virtual UI is created.

Use this when virtual lifecycle objects should participate in the same shared setup behavior.

## Internal Core Helpers

These types are part of the internal lifecycle implementation. They are important for understanding the package, but they are not the main public entry points.

### `NodeManager`

Maintains the ownership tree.

Main responsibilities:

- Tracks root nodes and child relationships.
- Maps created UI objects back to their nodes.
- Attaches and detaches nodes during create and recovery.

This is the internal structure that makes parent-child recovery order deterministic.

### `PluginCombiner<TUI, TContainer, TSlotConfig>`

Orders plugins and dispatches lifecycle events.

Main responsibilities:

- Sorts plugins by `SortingOrder`.
- Runs create handlers in forward order.
- Runs recovery handlers in reverse order.

This is what makes plugin setup and teardown symmetric.

### `Logger`

Simple logging abstraction used by the core manager and node system.

Use this when you want the core lifecycle layer to report events or errors without binding directly to Unity logging.

### `MikaTask`

Custom awaitable used by the core layer.

This type allows the package to expose async-like APIs without exposing internal implementation details each time.

## Unity Runtime Types

### `UIManager`

Unity-specific wrapper around `UIManager<MonoBehaviour, Transform>`.

Main responsibilities:

- Creates screen-level UI by sorting order.
- Requests canvases from `CanvasProvider`.
- Registers and unregisters tokens against canvases.

Use this when building UI directly in Unity with `Transform` containers and canvas-based layering.

### `DefaultUIElementSource`

ScriptableObject catalog for UI templates.

Main responsibilities:

- Stores the list of available UI templates.
- Associates each template with a `UIName`.

This is the standard way to declare what the manager is allowed to create.

### `DefaultUIElementProvider`

Default Unity implementation of `IUIElementProvider<MonoBehaviour, Transform>`.

Main responsibilities:

- Looks up templates by UI type and name.
- Instantiates UI, or rents an idle instance from an optional pool.
- Connects Core's instance use lifecycle with a Unity pool on creation and recovery.

`UnityBoundedEffectablePool` is a Unity pool backed
by Core's bounded FIFO storage. The pool retains only `IUIEffectable` UI, moves idle
instances under `poolRoot`, and destroys instances it cannot retain. Core's element
instance use lifecycle runs effect cleanup and preserves an instance's ElementID across reuse.
The provider disposes its pool when the manager is disposed.

### `CanvasProvider`

Manages canvas allocation and reuse by sorting order.

Main responsibilities:

- Creates or reuses canvases.
- Tracks which tokens are using which canvas.
- Disables canvases when nothing is using them.

This keeps screen-layer management out of individual UI scripts.

### `VisualSlot`

Unity implementation of `ISlot<Transform>`.

Use this when you need to attach a UI to a concrete `Transform` and optionally relate it to a parent UI.

## Built-In Plugins

### `PluginCreator`

Factory for the default Unity plugin set.

Use this when you want the standard behavior bundle instead of manually assembling the default plugins.

### `InjectUIManagerPlugin<TUI, TContainer, TSlotConfig>`

Injects the installed manager into a compatible field on the created object.

Use this when UI objects need access to the manager without requiring each caller to wire that dependency manually.

### `UIInitPlugin<TUI, TContainer, TSlotConfig>`

Runs `Init()` for visual UIs that implement `IUIInit`.

This keeps one-time initialization attached to the lifecycle instead of placing it in ad hoc caller code. Virtual UIs do not participate in this plugin.

### `UIRenamePlugin`

Renames created and recovering Unity UI instances.

This is mainly a debugging and readability helper for the scene hierarchy.

### `FitContainerPlugin`

Applies `SlotRectConfigs` or template RectTransform values to the created UI.

Use this to keep UI placement consistent when attaching instances into slots.

## Utility Types

### `UITransitionUtilities`

Transition helper for handing off between UI tokens.

Main responsibilities:

- Waits for the target UI to become visible.
- Waits for the source UI to become hidden.
- Optionally disposes the previous token after the handoff.

Use this when animated visibility changes are part of the intended lifecycle. The target type must implement both `IVisualUI` and `IUITransitionable`.

## Suggested Reading Order

If you are trying to understand the package from the API surface inward, read the types in this order:

1. `UIManager`
2. `UIControlToken`
3. `IBaseUI`, `IVirtualUI`, `IVisualUI`, `IUIEffectable`, `IUIInit`, `IUITransitionable`
4. `ISlot` and `IVirtualSlot`
5. `DefaultUIElementSource` and `DefaultUIElementProvider`
6. `IPlugin` and the built-in plugins
7. `CanvasProvider` and `UITransitionUtilities`

This order mirrors the way most projects adopt the package in practice.
