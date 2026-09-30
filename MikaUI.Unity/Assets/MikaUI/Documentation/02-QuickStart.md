# Quick Start

[Introduction](01-Introduction.md) → Quick Start → [Lifetime](03-Lifetime.md)

Let's start with a simple shop: open a window, display a healing potion, wait for the player to close it, and release the UI.

## Before You Start

The example uses MikaUI, UGUI, TextMesh Pro, and standard C# `Task` syntax. This chapter assumes the demo's assets and buttons are already configured. Try the shop first, then read the code.

## Try the Shop

Open the demo's shop scene and enter Play Mode:

1. Click the open-shop button (「開啟商店」). Check that the shop shows a healing potion (「回復藥水」) priced at 100 gold (「100 金幣」).
2. Click the close button (「關閉」). The shop disappears.
3. Open the shop again. The product information appears again.

You have now created, used, and released a UI. The demo destroys UI after use, so each opening creates a new shop instance.

## Opening and Closing

The `OpenShop` method in `ShopDemo.cs` describes the flow you just tried:

```csharp
public async void OpenShop()
{
    using (var token = await uiManager.Create<UI_Shop>(sortingOrder: 0))
    {
        await token.UI.ShowAsync(product);
    }
}
```

`Create` creates the shop and returns a token. `token.UI` gives you access to that shop. `ShowAsync` displays the product and waits for the player to click close. When the wait ends, the code leaves the `using` block and MikaUI releases the shop.

`ShowAsync` is a custom demo method. For now, focus on the flow: create the UI, wait for the player, and end the token's lifetime.

## The Shop UI

The shop prefab has this structure:

```text
UI_Shop             ← RectTransform, UI_Shop
└── Panel           ← Background and layout
    ├── ProductName ← TextMeshProUGUI
    ├── Price       ← TextMeshProUGUI
    └── CloseButton ← Button, close
```

`UI_Shop.cs` displays the product and turns the close-button click into a `Task` you can await. If you are new to `TaskCompletionSource`, you can skip the waiting mechanism for now and continue to the product data and manager setup.

```csharp
using System.Threading.Tasks;
using MikaUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_Shop : MonoBehaviour, IVisualUI
{
    [SerializeField] TMP_Text productNameText;
    [SerializeField] TMP_Text priceText;
    [SerializeField] Button closeButton;

    TaskCompletionSource<bool> closed;

    public async Task ShowAsync(ShopProduct product)
    {
        closed = new TaskCompletionSource<bool>();
        closeButton.onClick.AddListener(RequestClose);
        productNameText.text = product.Name;
        priceText.text = $"{product.Price} 金幣";

        await closed.Task;

        closeButton.onClick.RemoveListener(RequestClose);
    }

    public void RequestClose()
    {
        closed?.TrySetResult(true);
    }
}
```

Implementing `IVisualUI` lets MikaUI create this component. The demo's `ShowAsync` method handles displaying the product and waiting for the player. You can define your own methods to suit your UI.

When `ShowAsync` starts, it connects the close button to `RequestClose`. Clicking the button completes `closed.Task`. The method then removes the listener and returns, allowing the outer flow to leave `using` and release the shop.

The Chinese strings in these examples match the demo's labels.

## Product Data

`ShopProduct.cs` stores the product's name, price, and description:

```csharp
public sealed class ShopProduct
{
    public string Name { get; }
    public int Price { get; }
    public string Description { get; }

    public ShopProduct(string name, int price, string description)
    {
        Name = name;
        Price = price;
        Description = description;
    }
}
```

The shop uses the name and price. The details panel will use the same data later.

## Manager Setup

Now that you know the flow, let's look at how `ShopDemo.cs` prepares the manager. Here is the full script, including the `OpenShop` method from above:

```csharp
using MikaUI;
using UnityEngine;

public class ShopDemo : MonoBehaviour
{
    [SerializeField] DefaultUIElementSource uiElementSource;
    [SerializeField] RectTransform canvasRoot;
    [SerializeField] Canvas canvasTemplate;

    readonly ShopProduct product = new ShopProduct(
        "回復藥水", 100, "使用後回復 50 點生命值。");

    UnityUIManager uiManager;

    void Start()
    {
        uiManager = new UnityUIManager(
            new DefaultUIElementProvider(uiElementSource.GetSources(), pool: null),
            new CanvasProvider(canvasRoot, canvasTemplate),
            plugins: null);
    }

    public async void OpenShop()
    {
        using (var token = await uiManager.Create<UI_Shop>(sortingOrder: 0))
        {
            await token.UI.ShowAsync(product);
        }
    }

    void OnDestroy()
    {
        uiManager?.Dispose();
    }
}
```

`uiElementSource` supplies the available UI prefabs. `CanvasProvider` supplies the Canvas used to display them. The demo already assigns the source, Canvas, and prefab. This setup uses the default plugins and destroys UI after use.

For now, you only need to know that these settings let the manager find the shop prefab and display it on a Canvas.

When `ShopDemo` is destroyed, it also disposes the manager, releasing any UI that is still open.

The next chapter explains how tokens and `using` define a UI's lifetime.

[Next: Lifetime](03-Lifetime.md)
