# Lifetime

[Previous: Quick Start](02-QuickStart.md)

The previous example wrapped the shop interaction in `using`. This chapter explains how that manages the UI and when to dispose a token directly.

## Tokens

When you create UI, the manager returns a token. It gives you access to the UI and represents the lifetime of this use.

```csharp
using (var token = await uiManager.Create<UI_Shop>(sortingOrder: 0))
{
    await token.UI.ShowAsync(product);
}
```

`token.UI` is the shop you created. `ShowAsync` waits for the player to finish. When the code leaves the block, `using` automatically calls the token's `Dispose()` method to start releasing the UI.

The caller that opens the shop holds the token and is responsible for ending its use. This responsibility is what ownership means in MikaUI.

## Waiting and Releasing

The `using` block should cover the entire UI interaction. If you leave the block right after displaying the shop, MikaUI releases it immediately.

That is why the Quick Start's `ShowAsync` returns a `Task` that waits for the close button. The button completes the wait; the outer `using` handles releasing the UI.

```text
Create shop → Display product → Wait for close → Leave using → Release shop
```

With the current setup, releasing the shop destroys its instance. If you add pooling later, the instance can be kept for reuse.

## Dispose

Some UI needs to stay alive across several methods or events. In that case, store the token in a field and call `Dispose()` when the flow ends.

For example, if changing game modes should close the shop, you could hold its token like this:

```csharp
// An alternative ownership pattern; this guide's shop still uses using.
shopToken = await uiManager.Create<UI_Shop>(sortingOrder: 0);

// When the game mode changes:
shopToken.Dispose();
shopToken = null;
```

Both approaches use the token to start the same release flow. Choose the one that fits how your code starts and ends the interaction.

## Access After Disposal

Once a token is disposed, `IsDisposed` becomes `true`. Accessing its `UI` property then throws `ControlTokenDisposedException`.

If another object holds a regular reference to the UI, it must also stop using it when the lifetime ends. Tokens define how the UI is managed; they do not prevent access through every C# reference.

Close UI through the token's release flow. `SetActive(false)` changes visibility without ending the token. Calling `Destroy` directly bypasses the normal closing flow.

Next, we will add product details to the same shop. The panel will close on its own or together with the shop.

[Next: Child UI](04-ChildUI.md)
