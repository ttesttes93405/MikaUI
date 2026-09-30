# 生命週期

[上一篇：快速上手](02-QuickStart.zh-TW.md)

上一個範例用 `using` 包住商店操作。這一篇會解釋它如何管理 UI，以及什麼時候需要直接結束 token。

## Token

建立 UI 時，manager 會回傳一個 token。它讓你取得 UI，也代表這次使用的生命週期。

```csharp
using (var token = await uiManager.Create<UI_ShopProduct>(sortingOrder: 0))
{
    await token.UI.ShowAsync(product);
}
```

`token.UI` 是建立好的商店。`ShowAsync` 等待玩家完成操作，程式離開區塊時，`using` 會自動呼叫 token 的 `Dispose()`，啟動回收。

在這個流程中，開啟商店的呼叫端持有 token，也負責讓這次使用正常結束。MikaUI 用這種持有責任來表達 ownership。

## 等待與回收

`using` 的範圍應該包含整段 UI 操作。如果只顯示商店就離開區塊，商店也會立即回收。

快速上手因此讓 `ShowAsync` 回傳一個等待關閉的 Task。關閉按鈕完成等待，回收則由外層的 `using` 處理。

```text
建立商店 → 顯示商品 → 等待玩家關閉 → 離開 using → 回收商店
```

目前使用用後銷毀的設定，回收會銷毀商店實例。之後加入 pooling 時，實例也可以被保留，交給下次使用。

## Dispose

有些 UI 需要跨越多個方法或事件存在。這時可以把 token 保留在欄位中，在流程結束時明確呼叫 `Dispose()`。

例如之後要讓切換遊戲模式的程式直接關閉商店，可以用這種持有方式：

```csharp
// 另一種持有方式的片段；本指南的商店仍沿用 using。
shopToken = await uiManager.Create<UI_ShopProduct>(sortingOrder: 0);

// 遊戲模式切換時：
shopToken.Dispose();
shopToken = null;
```

兩種寫法都透過 token 啟動同一套回收流程。選擇哪一種，取決於你的程式如何表達 UI 操作的開始與結束。

## 存取範圍

token 結束後，`IsDisposed` 會變成 `true`。再透過它取得 `UI`，會拋出 `ControlTokenDisposedException`。

如果其他物件保留了普通的 UI reference，也要配合生命週期停止使用它。token 提供管理約定，並不會限制所有 C# reference 的存取。

正常關閉應走 token 的回收流程。`SetActive(false)` 只改變顯示狀態，不會結束 token；直接 `Destroy` 會繞過正常關閉流程。

下一篇會在同一個商店加入商品詳情，讓詳情可以單獨關閉，也能跟著商店一起結束。

[下一篇：子 UI](04-ChildUI.zh-TW.md)
