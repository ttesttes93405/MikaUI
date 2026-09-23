# MikaUI Class Reference

這份文件是 MikaUI 主要 classes 與 interfaces 的 type reference。

和主 README 不同，這份文件不著重在理念，而是回答比較直接的問題：每個重要型別負責什麼？什麼時候應該注意它？

## 如何閱讀這份文件

這個 package 比較適合分成三層理解：

- core lifecycle types
- Unity runtime adapters
- plugins 與 helper utilities

如果你是第一次接觸這個 package，建議先看 `UIManager`、control tokens，以及各種 UI interfaces。

## Core Lifecycle Types

### `UIManager<TUI, TContainer, TSlotConfig>`

整套 lifecycle 的中央協調者。

主要責任：

- 透過 `IUIElementProvider` 建立 UI
- 建立 parent-child ownership tree
- 回傳代表 UI lifetime ownership 的 token
- 在 create 與 recovery 階段執行 plugins
- 當 token dispose 時，recovery 整棵 subtree

當你想使用 generic lifecycle system，而不是直接綁死 Unity-specific container 時，這個型別就是核心入口。

### `UIControlToken`、`UIControlToken<TUI>` 與 `UIControlToken<TUI, TContainer>`

UI 的 lifetime handle，分三個層級：

- `UIControlToken` — base type，持有 token ID、element ID、名稱與 disposal 邏輯。
- `UIControlToken<TUI>` — 加入 UI instance；`TUI : IBaseUI`。虛擬與視覺 UI 皆適用。
- `UIControlToken<TUI, TContainer>` — sealed；加入 container；`TUI : IVisualUI`。僅供視覺 UI 使用。

主要責任：

- 持有 runtime token ID
- 持有 reusable element ID
- 暴露建立完成的 UI instance
- 在 `Dispose()` 時觸發 recovery

對 caller 來說，這通常是最重要的 runtime object。只要你還持有 token，就代表你還持有 UI 的 lifetime ownership。

`CreateVirtual<T>()` 回傳 `UIControlToken<T>`，其中 `T : IVirtualUI`。  
`Create<T>()` 回傳 `UIControlToken<T, TContainer>`，其中 `T : IVisualUI`。

### `SlotRectConfigs` *(Unity layer)*

在 create UI 時傳入的 layout data。

主要用途：

- 描述 anchors、pivot、size delta 與 anchored position
- 讓 caller 可以覆寫預設 layout，而不是只能依賴 template 的 RectTransform

這個型別定義在 Unity layer，作為 `TSlotConfig` 的具體實作傳入 `UIManager`。

### `IBaseUI`

所有 lifecycle-managed UI 型別的共用 base。

當一個型別需要參與 node tree，無論它是否有視覺呈現，都應該實作這個 interface。

### `IVirtualUI : IBaseUI`

用來標記 lifecycle-managed、但不需要 visual container 的物件。

適合拿來表示 non-visual 的 owned runtime object，例如 state container 或 flow controller。

### `IVisualUI : IBaseUI`

用來標記由 system 管理的 visual UI。

只要是透過標準 visual path 建立的 UI，都應該實作這個 interface。`IVisualUI` 與 `IVirtualUI` 的分離確保了 `CreateVirtual<T>()` 只接受純邏輯型別——誤傳視覺 UI 型別是 compile-time error。

### `IUIEffectable : IVisualUI`

用於每次使用時需要 setup 與 cleanup 的 visual UI。

主要責任：

- 提供 `UseEffect()`；它會執行 setup，並可回傳在 UI recovery 時執行的 cleanup action

可用它重設每次使用的狀態、註冊暫時 listener，或執行其他綁定 lifecycle 的 setup。

### `IUIInit : IVisualUI`

用於 visual UI 一次性 initialization 的 interface。

主要責任：

- 提供 `Init()`，用來執行每個 underlying element instance 只該做一次的 setup

不要把它當成每次開啟 UI 的 reset hook。這個 interface 繼承自 `IVisualUI`，只適用於透過 `Create<T>()` 建立的 visual UI，不適用於 `CreateVirtual<T>()`。

### `IUITransitionable : IVisualUI`

用於參與 transition timing 的 visual UI interface。

主要責任：

- 暴露 visible 與 hidden state 的 await point

如果 UI 有 show / hide animation，而且你想讓 token handoff 跟動畫時序協調，這個 interface 就很重要。

### `IVirtualSlot`

只定義 lifecycle ownership 的 parent 關係。

當建立的物件不需要具體 visual container，只需要 parent context 時，就會用到它。

### `ISlot<TContainer> : IVirtualSlot`

同時定義 parent 關係與實際 container。

當 visual UI 需要被 attach 到真正的 runtime container 時，就應該使用它。因為 `ISlot<TContainer>` 繼承 `IVirtualSlot`，任何帶有 container 的 slot 也同時滿足 virtual parent 關係。

### `VirtualSlot`

`IVirtualSlot` 的預設實作。

當 child relationship 很重要，但又不需要 visual container 時，這會是最方便的型別。

### `IUIElementProvider<TUI, TContainer>`

UI element 的 abstract factory 與 recovery boundary。

主要責任：

- 依 type 與 name 產生 visual UI element
- 依 type 產生 virtual UI element
- 定義 element 被建立後要如何 recovery

這個型別把 lifecycle orchestration 與具體 instantiation / pooling 策略分開。

### `UIElement<TContainer>` 與 `VirtualUIElement`

provider 端用來描述 create 與 recovery 的 descriptor。

一般使用者不一定會直接操作這兩個型別，但 provider 會用它們來定義 element 如何被建立，以及之後如何被 recovery。

### `IPlugin<TUI, TContainer, TSlotConfig>`

plugin 的 base contract。

主要責任：

- 透過 `SortingOrder` 宣告執行順序
- 接收被安裝的 manager instance

如果你要把 cross-cutting behavior 掛進 lifecycle event，這就是入口。

### `IPluginUICreatedHandler<TUI, TContainer, TSlotConfig>`

在 visual UI 建立後執行。

適合放那些應該在 UI 進入 active tree 時一律執行的 setup logic。

### `IPluginUIWillRecoveryHandler<TUI, TContainer>`

在 visual UI recovery 前執行。

適合放那些必須在 UI 仍處於 active 狀態時先做的 teardown 準備。

### `IPluginUIRecoveredHandler`

在 visual UI recovery 完成後執行。

適合處理 recovery 後的 bookkeeping。

### `IPluginVirtualUICreatedHandler`

在 virtual UI 建立後執行。

如果 virtual lifecycle object 也要吃到同一套共用 setup 行為，就會用到它。

## Internal Core Helpers

這些型別屬於 internal lifecycle implementation。它們很重要，但通常不是 public API 的第一入口。

### `NodeManager`

負責維護 ownership tree。

主要責任：

- 追蹤 root nodes 與 child 關係
- 把建立出來的 UI object 對應回 node
- 在 create 與 recovery 階段 attach / detach node

parent-child recovery 順序之所以能夠 deterministic，就是靠這個結構。

### `PluginCombiner<TUI, TContainer, TSlotConfig>`

負責排序 plugins 並 dispatch lifecycle events。

主要責任：

- 依 `SortingOrder` 排序 plugins
- 以 forward order 執行 create handlers
- 以 reverse order 執行 recovery handlers

這讓 plugin 的 setup 與 teardown 可以形成對稱的順序。

### `Logger`

core manager 與 node system 使用的簡單 logging abstraction。

當你希望 core lifecycle layer 可以回報事件或錯誤，但又不想直接綁死 Unity logging 時，這個型別就有用。

### `MikaTask`

core layer 使用的 custom awaitable。

它讓 package 可以提供 async-like API，同時把內部實作細節藏在較低層。

## Unity Runtime Types

### `UIManager`

`UIManager<MonoBehaviour, Transform>` 的 Unity-specific wrapper。

主要責任：

- 依 sorting order 建立 screen-level UI
- 透過 `CanvasProvider` 要求 canvas
- 把 token 註冊到 canvas，並在 dispose 後解除註冊

如果你的使用情境是 Unity + `Transform` container + canvas-based layering，這通常就是直接使用的 manager 類型。

### `DefaultUIElementSource`

用來保存 UI template catalog 的 ScriptableObject。

主要責任：

- 保存可用 UI template 清單
- 讓每個 template 對應到一個 `UIName`

這是宣告「manager 可以建立哪些 UI」的標準方式。

### `DefaultUIElementProvider`

`IUIElementProvider<MonoBehaviour, Transform>` 的預設 Unity 實作。

主要責任：

- 依 UI type 與 name lookup template
- instantiate 或 reuse pooled UI
- 把 reusable UI 放回 pool root
- 在 recovery 時 destroy non-reusable UI

除非專案有自己的一套 prefab loading 或 pooling 策略，不然通常直接用它就可以。

### `CanvasProvider`

依 sorting order 管理 canvas allocation 與 reuse。

主要責任：

- 建立或 reuse canvas
- 追蹤哪些 token 正在使用某個 canvas
- 當沒有人使用時停用 canvas

這讓 screen-layer 管理不需要散落在各個 UI script 裡。

### `VisualSlot`

`ISlot<Transform>` 的 Unity 實作。

當你要把 UI attach 到具體的 `Transform`，同時還想保留 parent UI 關係時，就會使用它。

## Built-In Plugins

### `PluginCreator`

預設 Unity plugin set 的 factory。

當你希望直接取得標準行為組合，而不是自己手動組裝 default plugins 時，就會使用它。

### `InjectUIManagerPlugin<TUI, TContainer, TSlotConfig>`

把安裝好的 manager 注入到 created object 的相容欄位中。

當 UI object 需要存取 manager，但你又不想讓每個 caller 都自己 wiring dependency 時，這個 plugin 很有用。

### `UIInitPlugin<TUI, TContainer, TSlotConfig>`

為實作 `IUIInit` 的 visual UI 執行 `Init()`。

它讓一次性的 initialization 跟 lifecycle 綁在一起，而不是散落在 caller code 中。Virtual UI 不參與這個 plugin。

### `UIRenamePlugin`

替 Unity UI instance 在 create 與 recover 時重新命名。

這個 plugin 主要是為了 scene hierarchy 的除錯與可讀性。

### `FitContainerPlugin`

把 `SlotRectConfigs` 或 template 的 RectTransform 值套用到建立出的 UI。

當你希望 UI attach 到 slot 時能保持一致的 layout 行為，就會依賴這個 plugin。

## Utility Types

### `UITransitionUtilities`

用於 token handoff 的 transition helper。

主要責任：

- 等待 target UI 變成 visible
- 等待 source UI 進入 hidden 狀態
- 在 handoff 完成後視需要 dispose 舊 token

如果 UI lifecycle 包含動畫化的顯示切換，這個 utility 會很有幫助。target 型別必須同時實作 `IVisualUI` 與 `IUITransitionable`。

## 建議閱讀順序

如果你想從 API surface 往內理解這個 package，建議照這個順序看：

1. `UIManager`
2. `UIControlToken`
3. `IBaseUI`、`IVirtualUI`、`IVisualUI`、`IUIEffectable`、`IUIInit`、`IUITransitionable`
4. `ISlot` 與 `IVirtualSlot`
5. `DefaultUIElementSource` 與 `DefaultUIElementProvider`
6. `IPlugin` 與 built-in plugins
7. `CanvasProvider` 與 `UITransitionUtilities`

這個順序也比較貼近大多數專案實際採用這個 package 的方式。
