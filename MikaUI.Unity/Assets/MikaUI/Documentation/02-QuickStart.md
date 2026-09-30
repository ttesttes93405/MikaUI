# Quick Start

[Introduction](01-Introduction.md) → Quick Start → [Lifetime](03-Lifetime.md)

Let's start with the shop demo: open a product information window, display a healing potion, wait for the player to close it, and release the UI.

## Before You Start

The example uses MikaUI, UGUI, TextMesh Pro, and standard C# `Task` syntax. This chapter assumes the demo's assets and buttons are already configured. Try the product information window first, then read the code.

## Try Product Information

Open `Samples/ShopDemo/ShopDemo.unity` and enter Play Mode:

1. Click the open-product button ("Open ShopProduct"). Check that the window shows a healing potion ("Healing Potion") priced at 100 gold ("100 Gold").
2. Click the close button ("Close"). The product information window disappears.
3. Click "Open ShopProduct" again. The product information appears again.

You have now created, used, and released a UI. The demo destroys UI after use, so each opening creates a new `UI_ShopProduct` instance.

## Opening and Closing

The `OpenProductInfo` method in `ShopDemo.cs` describes the flow you just tried:

```csharp
public async void OpenProductInfo()
{
    using (var token = await uiManager.Create<UI_ShopProduct>(sortingOrder: 0))
    {
        await token.UI.ShowAsync(product);
    }
}
```

`Create` creates the product information UI and returns a token. `token.UI` gives you access to that UI. `ShowAsync` displays the product and waits for the player to click close. When the wait ends, the code leaves the `using` block and MikaUI releases the product information UI.

`ShowAsync` is a custom demo method. For now, focus on the flow: create the UI, wait for the player, and end the token's lifetime.

## The Product Information UI

`Prefabs/UI_ShopProduct.prefab` has this structure:

```text
UI_ShopProduct      ← RectTransform, UI_ShopProduct
├── Background      ← Image, dimmed backdrop
└── Panel           ← Image, background and layout
    ├── ProductName ← TextMeshProUGUI
    ├── Price       ← TextMeshProUGUI
    └── CloseButton ← Button, "Close"
```

`UI_ShopProduct.cs` displays the product and turns the close-button click into a `Task` you can await. If you are new to `TaskCompletionSource`, you can skip the waiting mechanism for now and continue to the product data and manager setup.

```csharp
using System.Threading.Tasks;
using MikaUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_ShopProduct : MonoBehaviour, IVisualUI
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
        priceText.text = $"{product.Price} Gold";

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

When `ShowAsync` starts, it connects the close button to `RequestClose`. Clicking the button completes `closed.Task`. The method then removes the listener and returns, allowing the outer flow to leave `using` and release the product information UI.

The English strings in these examples match the demo's labels.

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

The product information UI uses the name and price. The details panel will use the same data later.

## Manager Setup

Now that you know the flow, let's look at how `ShopDemo.cs` prepares the manager. Here is the full script, including the `OpenProductInfo` method from above:

```csharp
using MikaUI;
using UnityEngine;
using UnityEngine.UI;

public class ShopDemo : MonoBehaviour
{
    [SerializeField] DefaultUIElementSource uiElementSource;
    [SerializeField] RectTransform canvasRoot;
    [SerializeField] Canvas canvasTemplate;
    [SerializeField] Button openShopButton;

    readonly ShopProduct product = new ShopProduct(
        "Healing Potion", 100, "Restores 50 HP when used.");

    UnityUIManager uiManager;

    void Start()
    {
        uiManager = new UnityUIManager(
            new DefaultUIElementProvider(uiElementSource.GetSources(), pool: null),
            new CanvasProvider(canvasRoot, canvasTemplate),
            plugins: null);

        openShopButton.onClick.AddListener(OpenProductInfo);
    }

    public async void OpenProductInfo()
    {
        using (var token = await uiManager.Create<UI_ShopProduct>(sortingOrder: 0))
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

`uiElementSource` supplies the available UI prefabs. `CanvasProvider` supplies the Canvas used to display them. The demo already assigns the source, Canvas, prefab, and open button. `Start` connects `openShopButton` to `OpenProductInfo`. This setup uses the default plugins and destroys UI after use.

For now, you only need to know that these settings let the manager find the `UI_ShopProduct` prefab and display it on a Canvas.

When `ShopDemo` is destroyed, it also disposes the manager, releasing any UI that is still open.

The next chapter explains how tokens and `using` define a UI's lifetime.

[Next: Lifetime](03-Lifetime.md)
