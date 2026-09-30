# 快速上手

[簡介](01-Introduction.zh-TW.md) → 快速上手 → [生命週期](03-Lifetime.zh-TW.md)

我們先從一個簡單的商店開始：開啟視窗、顯示回復藥水，等待玩家關閉後回收 UI。

## 準備

範例使用 MikaUI、UGUI、TextMesh Pro 與標準的 `Task` 語法。本篇使用已配置好資源與按鈕的 demo。先操作商店，再閱讀對應的程式。

## 先操作商店

開啟 demo 的商店場景並進入 Play Mode：

1. 按下「開啟商店」，確認畫面顯示「回復藥水」與「100 金幣」。
2. 按下「關閉」，商店會從畫面上消失。
3. 再次按下「開啟商店」，確認商品資訊再次出現。

你已經走過一次建立、操作與回收 UI 的流程。demo 目前使用用後銷毀的設定，所以每次開啟都會建立新的商店實例。

## 開啟與關閉流程

先看 `ShopDemo.cs` 的 `OpenShop`，它描述了剛才的操作：

```csharp
public async void OpenShop()
{
    using (var token = await uiManager.Create<UI_Shop>(sortingOrder: 0))
    {
        await token.UI.ShowAsync(product);
    }
}
```

`Create` 建立商店並回傳 token，透過 `token.UI` 可以操作這次建立的商店。`ShowAsync` 顯示商品，並等待玩家按下「關閉」。等待結束後，程式離開 `using` 區塊，MikaUI 就會回收商店。

這裡的 `ShowAsync` 是 demo 自訂的方法。你可以先使用 demo 提供的實作，把注意力放在「建立 UI、等待操作、結束 token」這段流程。

## 商店 UI

商店 prefab 的結構如下：

```text
UI_Shop             ← RectTransform、UI_Shop
└── Panel           ← 背景與排版
    ├── ProductName ← TextMeshProUGUI
    ├── Price       ← TextMeshProUGUI
    └── CloseButton ← Button，「關閉」
```

`UI_Shop.cs` 負責顯示商品，以及把關閉按鈕的操作轉成可以等待的 Task。如果你還不熟悉 `TaskCompletionSource`，可以先略過等待機制的細節，繼續閱讀後面的商品資料與 manager 設定。

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

`IVisualUI` 讓這個元件可以透過 MikaUI 建立。顯示商品與等待關閉的行為由 demo 的 `ShowAsync` 實作，你可以依自己的 UI 操作流程定義相應的方法。

`ShowAsync` 進入時將關閉按鈕的點擊事件綁定到 `RequestClose`。玩家點擊後，`closed.Task` 的等待結束，方法移除綁定，再讓外層流程離開 `using` 區塊，由 MikaUI 回收商店。

## 商品資料

`ShopProduct.cs` 保存商品的名稱、價格與說明：

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

商店先使用名稱與價格。稍後的商品詳情也會使用這份資料。

## Manager 設定

了解操作流程後，再看 `ShopDemo.cs` 如何準備 manager。以下是完整程式，其中的 `OpenShop` 就是前面讀過的流程：

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

`uiElementSource` 提供可建立的 UI，`CanvasProvider` 提供顯示用的 Canvas。demo 已經指定好來源、Canvas 與 prefab；這裡先使用預設插件與用後銷毀的設定。你暫時只需要知道 manager 會透過這些設定找到商店 prefab，並將它顯示在 Canvas 上。

`ShopDemo` 結束時也會結束 manager，回收仍然開著的 UI。

下一篇會進一步解釋 token 與 `using` 如何表達 UI 的存活範圍。

[下一篇：生命週期](03-Lifetime.zh-TW.md)
