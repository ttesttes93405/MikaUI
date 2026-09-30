# Introduction

## What is MikaUI?

MikaUI is a framework for managing Unity UGUI. It provides a shared way to create and release UI, including windows and their child UI.

Imagine you are building a shop. A player opens the shop, views product details, and closes the window. As you add features, you need to coordinate cleanup across scripts and make sure the details panel does not stay open after the shop closes.

With MikaUI, you define a UI's lifetime and its relationship to other UI when you create it. The code that opens the shop can create it, wait for the player, and release it in one flow. When you add a details panel, the framework also handles releasing it before the shop.

This guide uses that shop to introduce the main concepts.

## Basic Usage

Here is a minimal example, with the manager and prefab already set up:

```csharp
using (var token = await uiManager.Create<UI_ShopProduct>(sortingOrder: 0))
{
    await token.UI.ShowAsync(product);
}
```

`Create` creates the shop and returns a token. Use `token.UI` to access the shop and pass in the product data.

`ShowAsync` is a custom method in this example. It displays the product and waits for the player to close the shop. When the wait ends, the code leaves the `using` block and MikaUI releases the shop.

This example shows two core features:

- **UI creation:** Create UI through a manager with a consistent flow.
- **Lifetime management:** Use a token to define how long the UI stays alive. MikaUI releases it when that lifetime ends.

For now, focus on this flow. The Quick Start walks you through the demo shop before explaining the code behind it.

## Child UI

A product details panel should close on its own or close together with the shop.

When you create the panel, you can assign the shop as its parent. MikaUI releases child UI before releasing the parent, so the same flow handles the closing order.

Later in the guide, we will add this panel to the shop and set up that relationship.

## Learning Path

This guide assumes you know the basics of Unity, C#, and UGUI and are using the configured demo. You will first try the shop, learn how it opens and closes, then add child UI and cleanup behavior.

- [Quick Start](02-QuickStart.md): Try the shop and follow its opening and closing flow.
- [Lifetime](03-Lifetime.md): Learn how tokens define a UI's lifetime and release it.
- [Child UI](04-ChildUI.md): Add product details and define how child UI closes with its parent.
