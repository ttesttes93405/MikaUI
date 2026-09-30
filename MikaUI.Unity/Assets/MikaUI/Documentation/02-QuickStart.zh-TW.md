# 快速上手

[簡介](01-Introduction.zh-TW.md) → 快速上手 → [生命週期](03-Lifetime.zh-TW.md)

我們先從商店 demo 開始：開啟商品資訊視窗、顯示回復藥水，等待玩家關閉後回收 UI。

## 準備

範例使用 MikaUI、UGUI、TextMesh Pro 與標準的 `Task` 語法。本篇使用已配置好資源與按鈕的 demo。先操作商品資訊視窗，再閱讀對應的程式。

## 先操作商品資訊視窗

開啟 `Samples/ShopDemo/ShopDemo.unity` 並進入 Play Mode：

1. 按下「Open ShopProduct」，確認畫面顯示「Healing Potion」與「100 Gold」。
2. 按下「Close」，商品資訊視窗會從畫面上消失。
3. 再次按下「Open ShopProduct」，確認商品資訊再次出現。

你已經走過一次建立、操作與回收 UI 的流程。demo 目前使用用後銷毀的設定，所以每次開啟都會建立新的 `UI_ShopProduct` 實例。

## 開啟與關閉流程

先看 `ShopDemo.cs` 的 `OpenProductInfo`，它描述了剛才的操作：

```csharp
public async void OpenProductInfo()
{
    using (var token = await uiManager.Create<UI_ShopProduct>(sortingOrder: 0))
    {
        await token.UI.ShowAsync(product);
    }
}
```

`Create` 建立商品資訊 UI 並回傳 token，透過 `token.UI` 可以操作這次建立的 UI。`ShowAsync` 顯示商品，並等待玩家按下「Close」。等待結束後，程式離開 `using` 區塊，MikaUI 就會回收商品資訊 UI。

這裡的 `ShowAsync` 是 demo 自訂的方法。你可以先使用 demo 提供的實作，把注意力放在「建立 UI、等待操作、結束 token」這段流程。

## 商品資訊 UI

`Prefabs/UI_ShopProduct.prefab` 的結構如下：

```text
UI_ShopProduct      ← RectTransform、UI_ShopProduct
├── Background      ← Image，半透明遮罩
└── Panel           ← Image，背景與排版
    ├── ProductName ← TextMeshProUGUI
    ├── Price       ← TextMeshProUGUI
    └── CloseButton ← Button，「Close」
```

`UI_ShopProduct.cs` 負責顯示商品，以及把關閉按鈕的操作轉成可以等待的 Task。如果你還不熟悉 `TaskCompletionSource`，可以先略過等待機制的細節，繼續閱讀後面的商品資料與 manager 設定。

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

`IVisualUI` 讓這個元件可以透過 MikaUI 建立。顯示商品與等待關閉的行為由 demo 的 `ShowAsync` 實作，你可以依自己的 UI 操作流程定義相應的方法。

`ShowAsync` 進入時將關閉按鈕的點擊事件綁定到 `RequestClose`。玩家點擊後，`closed.Task` 的等待結束，方法移除綁定，再讓外層流程離開 `using` 區塊，由 MikaUI 回收商品資訊 UI。

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

商品資訊 UI 先使用名稱與價格。稍後的商品詳情也會使用這份資料。

## Manager 設定

了解操作流程後，再看 `ShopDemo.cs` 如何準備 manager。以下是完整程式，其中的 `OpenProductInfo` 就是前面讀過的流程：

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

`uiElementSource` 提供可建立的 UI，`CanvasProvider` 提供顯示用的 Canvas。demo 已經指定好來源、Canvas、prefab 與開啟按鈕，`Start` 會將 `openShopButton` 綁定到 `OpenProductInfo`；這裡先使用預設插件與用後銷毀的設定。你暫時只需要知道 manager 會透過這些設定找到 `UI_ShopProduct` prefab，並將它顯示在 Canvas 上。

`ShopDemo` 結束時也會結束 manager，回收仍然開著的 UI。

下一篇會進一步解釋 token 與 `using` 如何表達 UI 的存活範圍。

[下一篇：生命週期](03-Lifetime.zh-TW.md)
