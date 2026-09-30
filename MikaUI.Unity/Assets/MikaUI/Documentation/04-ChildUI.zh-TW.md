# 子 UI

[上一篇：生命週期](03-Lifetime.zh-TW.md)

現在玩家希望查看回復藥水的用途。我們會沿用同一個商店，加入可以單獨關閉、也會跟著商店結束的詳情面板。

## 先操作商品詳情

在 demo 的子 UI 範例中，開啟商店並按下「查看詳情」，確認面板顯示「使用後回復 50 點生命值。」。接著試試兩種關閉方式：

1. 按下「關閉詳情」，詳情消失，商店仍然存在。
2. 再次開啟詳情，直接關閉商店，商店與詳情都會消失。

下面會介紹如何在建立詳情時描述這個回收關係，讓框架在商店結束時一併回收詳情。

## VisualSlot

建立詳情時，需要指定它要顯示在哪裡，以及要跟著哪個 UI 回收。`VisualSlot` 同時提供這兩個資訊：

```csharp
var slot = new VisualSlot(shop, shop.DetailContainer);
```

第一個參數是 parent UI，第二個是顯示用的 Transform。parent 必須是同一個 manager 建立、仍然存活的 UI。

## 商店擴充

擴充後的商店 prefab 多了查看詳情的按鈕與容器：

```text
UI_ShopProduct
└── Panel
    ├── ProductName
    ├── Price
    ├── CloseButton
    ├── DetailButton    ← 「查看詳情」
    └── DetailContainer ← 詳情顯示的位置
```

`UI_ShopProduct` 保留原本的顯示與關閉方法，新增以下成員（事件使用 `System.Action`）：

```csharp
[SerializeField] RectTransform detailContainer;

public RectTransform DetailContainer => detailContainer;
public event System.Action<UI_ShopProduct> DetailRequested;

public void RequestDetail()
{
    DetailRequested?.Invoke(this);
}
```

玩家按下「查看詳情」時，`RequestDetail` 把請求交給 `ShopDemo`。

## 詳情流程

在 `ShopDemo` 中加入 `OpenDetail`，沿用原本的 manager 與商品資料：

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

這段流程和開啟商店相同，主要差別是透過 slot 建立詳情，讓它加入商店的回收範圍。

`OpenProductInfo` 則改成以下版本，接收查看詳情的請求：

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

## 商品詳情

詳情 prefab 的結構如下：

```text
UI_ProductDetail    ← RectTransform、UI_ProductDetail、背景
├── ProductName     ← TextMeshProUGUI
├── Description     ← TextMeshProUGUI
└── CloseButton     ← Button，「關閉詳情」
```

`UI_ProductDetail.cs` 沿用商店的等待模式，顯示同一件商品的說明：

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

## 回收關係

回到剛才試過的兩種關閉方式：單獨關閉詳情時，只結束詳情的 token，商店仍然存在；直接關閉商店時，框架會依照 `VisualSlot` 建立的關係，先回收詳情，再回收商店。

詳情回收後，它的 token 也會結束。本例的實例銷毀會完成 `ShowAsync` 的等待，讓詳情流程離開自己的 `using` 區塊。

Transform hierarchy 決定顯示位置，`VisualSlot` 的 parent 決定回收關係。只移動 Transform，並不會建立或改寫 MikaUI 的父子關係。

後續會沿用這個商店，介紹每次開啟的狀態設定與清理。

[回到簡介](01-Introduction.zh-TW.md)
