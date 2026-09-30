# Child UI

[Previous: Lifetime](03-Lifetime.md)

Now the player wants to know what the healing potion does. We will add a details panel to the same shop. It can close on its own, and it also closes when the shop closes.

## Try Product Details

In the demo's child UI example, open the shop and click the view-details button (「查看詳情」). Check that the panel says the potion restores 50 health (「使用後回復 50 點生命值。」). Then try both ways to close it:

1. Click close details (「關閉詳情」). The panel disappears and the shop stays open.
2. Open the details again, then close the shop. Both disappear.

Let's see how to define this relationship when creating the panel, so MikaUI releases it when the shop closes.

## VisualSlot

When creating the panel, you need to specify where it appears and which UI it closes with. `VisualSlot` provides both:

```csharp
var slot = new VisualSlot(shop, shop.DetailContainer);
```

The first argument is the parent UI. The second is the Transform used to display the child. The parent must be a UI created by the same manager and must still be alive.

## Extending the Shop

Add a details button and a container to the shop prefab:

```text
UI_ShopProduct
└── Panel
    ├── ProductName
    ├── Price
    ├── CloseButton
    ├── DetailButton    ← View details
    └── DetailContainer ← Where the details appear
```

Keep the existing display and close methods in `UI_ShopProduct`, and add these members. The event uses `System.Action`:

```csharp
[SerializeField] RectTransform detailContainer;

public RectTransform DetailContainer => detailContainer;
public event System.Action<UI_ShopProduct> DetailRequested;

public void RequestDetail()
{
    DetailRequested?.Invoke(this);
}
```

When the player clicks view details, `RequestDetail` passes the request to `ShopDemo`.

## Opening the Details

Add `OpenDetail` to `ShopDemo`, using the existing manager and product data:

```csharp
async void OpenDetail(UI_ShopProduct shop)
{
    var slot = new VisualSlot(shop, shop.DetailContainer);
    using (var token = await uiManager.Create<UI_ProductDetail>(slot))
    {
        await token.UI.ShowAsync(product);
    }
}
```

This follows the same flow as opening the shop. The main difference is the slot: it makes the panel a child of the shop for release purposes.

Update `OpenProductInfo` to listen for details requests:

```csharp
public async void OpenProductInfo()
{
    using (var token = await uiManager.Create<UI_ShopProduct>(sortingOrder: 0))
    {
        var shop = token.UI;
        shop.DetailRequested += OpenDetail;
        await shop.ShowAsync(product);
        shop.DetailRequested -= OpenDetail;
    }
}
```

## The Details UI

The details prefab has this structure:

```text
UI_ProductDetail    ← RectTransform, UI_ProductDetail, background
├── ProductName     ← TextMeshProUGUI
├── Description     ← TextMeshProUGUI
└── CloseButton     ← Button, close details
```

`UI_ProductDetail.cs` uses the same waiting pattern as the shop and displays the product's description:

```csharp
using System.Threading.Tasks;
using MikaUI;
using TMPro;
using UnityEngine;

public class UI_ProductDetail : MonoBehaviour, IVisualUI
{
    [SerializeField] TMP_Text productNameText;
    [SerializeField] TMP_Text descriptionText;

    TaskCompletionSource<bool> closed;

    public Task ShowAsync(ShopProduct product)
    {
        closed = new TaskCompletionSource<bool>();
        productNameText.text = product.Name;
        descriptionText.text = product.Description;
        return closed.Task;
    }

    public void RequestClose()
    {
        closed?.TrySetResult(true);
    }

    void OnDestroy()
    {
        RequestClose();
    }
}
```

## Releasing Parent and Child UI

When you close the details panel on its own, only its token is disposed. The shop stays open. When you close the shop, MikaUI follows the relationship defined by `VisualSlot`: it releases the details first, then the shop.

Releasing the details also disposes its token. In this example, destroying the instance completes the wait in `ShowAsync`, allowing the details flow to leave its own `using` block.

The Transform hierarchy determines where UI appears. The parent in `VisualSlot` determines how UI is released. Moving a Transform alone does not create or change MikaUI's parent-child relationship.

Later chapters will use this shop to explain state setup and cleanup for each opening.

[Back to Introduction](01-Introduction.md)
