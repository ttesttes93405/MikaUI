# MikaUI

MikaUI 是一套以 lifecycle 為核心的 Unity UI management system。

它不讓每個 UI 各自決定自己掛在哪裡、由誰持有、何時被清理，而是把這些決策集中到一條受管理的 runtime flow 裡。

目前持有 UI token 的一方，負責操作 UI 與結束它的 lifetime。這是 API 使用約定；持有 UI reference 本身不會強制限制其他程式的存取。

這套 system 的核心目的，是讓 UI 的 ownership 及 lifecycle 變得更加明確。

## 核心理念

這套 system 建立在幾個設計原則上：

- 每個 UI 都應該有清楚的 token 持有者。
- 每個 UI 都應該有明確的 lifetime handle。
- parent UI 應該決定 child UI 的 recovery 順序。
- reuse 與 destruction 應該走同一條 recovery path。
- 共用的 UI 行為應該掛在 lifecycle event 上，而不是分散複製到每個 prefab script 裡。

這套 system 關注：

- 這個 UI 目前由誰擁有？
- 它的 lifetime 什麼時候結束？
- 如果 parent 被 recovery，child 會發生什麼事？
- 如果它可以 reuse，要怎麼乾淨地回到下一次使用？
- 共用的 setup 與 teardown logic 應該放在哪裡？

MikaUI 的價值，就是把這些問題集中在同一個地方處理。

## Ownership Model

如果只講一個最重要的概念，那就是 ownership model。

這裡的 ownership，指的是「持有 UI token 的那一方」，也就是目前真正擁有這個 UI lifetime handle 的人。

這個 token 持有者通常是建立 UI 的呼叫端，但也可以是之後收到 token、接手管理這個 UI 的其他物件。

只要 token 還活著，ownership 就屬於目前持有 token 的那一方；當 token 被 dispose，這段 ownership 才正式結束。

### 透過 manager 建立 UI

UI instance 應該透過 `UIManager` 建立，而不是讓不同 script 任意呼叫 `Instantiate`。

這樣可以確保 create、registration、plugin hook 與 recovery 都走同一條路徑。

### 持有回傳的 token

建立 UI 之後，system 會回傳一個 token，這個 token 就是 UI lifetime handle。

只要 token 還活著，代表目前的 token 持有者仍然持有這個 UI 的 ownership；當 token 被 dispose，ownership 才正式結束，system 也會開始 recovery。

### Parent-child lifecycle 結構是明確建立的

如果 UI 是透過帶有 parent 資訊的 slot 建立，它就會成為該 parent 的 managed tree 一部分。

這不代表 ownership 只屬於 parent，而是代表 system 會把這個 child UI 掛進 parent 的 recovery tree 裡管理。

也就是說，這裡被建立的是一種受管理的 parent-child lifecycle 結構，而不是單純用 parent 來重新定義 ownership。

它強調的是 recovery 關係實際存在於 system 裡，而非依賴於場景 hierarchy：

- root UI 的 ownership 由目前持有 token 的那一方直接持有。
- child UI 會被納入 parent 的 recovery tree。
- 當 parent 被 recovery 時，底下的 UI 一定會先被 recovery。

這種設計可以避免 UI 被遺漏，也讓巢狀 UI flow 更穩定。

### Recovery 是唯一的退出路徑

當 token 被 dispose，system 會執行 recovery。

這條 recovery path 會統一決定 UI 要不要：

- 離開 active tree
- 執行 teardown hook
- 回到 pool
- 或直接被 destroy

這是整個 package 的設計中心。

## 這套 System 解決什麼問題

當專案開始出現以下情況時，MikaUI 會特別有價值：

- UI 從很多互不相干的地方被建立。
- 巢狀 UI 時常遺漏子 UI 的回收。
- UI 的狀態殘留、狀態不穩定。
- 不確定 UI 到底從何開始從何結束。
- manager injection、fitting、teardown 等共同行為被複製到很多 prefab，並且逐漸難以統一管理。
- 其他人總可以拿到你的 UI 並且亂搞一通。

這套 system 可以提供一個明確的 ownership 與 lifecycle model，而不只是依靠團隊默契。

## 如何使用這套 System

最適合解釋這套 system 的方式，是照著 lifecycle flow 來看。

### 1. 註冊 manager 可以建立的 UI

先建立一個 `DefaultUIElementSource` asset，把允許建立的 UI template 註冊進去。

每個 source entry 會定義：

- `UIName`
- `UITemplate`

建立 UI 時，便會以這個 source 的設定來建立。

### 2. 針對你的 runtime context 建立 manager

在 Unity 實作裡，`UIManager` 通常會由以下幾個部分組成：

- UI source catalog
- 可選的 pool root
- canvas root
- canvas template
- 預設 plugin set

範例：

```csharp
var pool = new UnityBoundedEffectablePool<DefaultUIElementProvider.IUIElementSource>(static source => 8, poolRoot);
var uiElementProvider = new DefaultUIElementProvider(uiElementSource.GetSources(), pool);

var uiManager = new UnityUIManager(
    uiElementProvider,
    new CanvasProvider(canvasRoot, canvasTemplate),
    plugins: null
);
```

不需要 pooling 時，傳入 `pool: null`，使用預設的用後銷毀物件池：

```csharp
var uiElementProvider = new DefaultUIElementProvider(uiElementSource.GetSources(), pool: null);
```

容量函式對每個 source 計算一次。`0` 表示不保留閒置實例，負數是無效設定。
pool 負責將閒置實例移到呼叫端提供的 `poolRoot` 下，以及銷毀無法保留的實例，Core 的 instance use lifecycle 負責效果清理與 ElementID；
TokenID 與樹狀回收仍由 UIManager 管理。
manager dispose 時會依序 dispose provider 與 pool。

`plugins: null` 會安裝預設插件；傳入非 null 集合會取代整套預設插件（空集合表示不安裝插件）。要保留預設行為並加入自己的插件，使用 `MikaUI.Plugin.PluginCreator.Create(canvasProvider, extraPlugins)`，並傳入同一個 `CanvasProvider`。

容量限制只計算每個 source 的閒置實例，不限制同時開啟的 UI 數量。source 預設以物件 reference 區分，閒置實例依 FIFO 重用。pool 不會自行停用 GameObject；建議提供已停用的 `poolRoot`，並由呼叫端管理它的 lifetime。

場景或 owner 結束時，先呼叫 `uiManager.Dispose()`，再銷毀 canvas root 與 pool root。manager 會回收所有活躍 UI、卸載插件並 dispose provider；provider 會 dispose pool，清除閒置實例。

### 3. 建立 UI，並保留 token

以 screen-level UI 為例：

```csharp
using (var token = await uiManager.Create<UI_HelloWorld>(sortingOrder: 0))
{
    await token.UI.Invoke("World");
}
```

這個 pattern 的重點是：

- 向 manager 要一個 UI
- 透過 token 使用這個 UI
- 當 UI lifetime 結束時，dispose token

`using` block 在這裡不僅僅是語法糖，它可以更直覺的表達這個 UI 的存活範圍。

### 4. 透過 slot 建立 child UI

當某個 UI 應該被納入另一個 UI 的 recovery tree 時，要透過帶有 parent 關係的 slot 建立。

child UI 會被插入 parent UI 的指定位置，並且在 parent UI recovery 時，也一定會先 recovery 自己。

以下假設 `UI_Child` 是 visual UI、`FlowController` 實作 `IVirtualUI` 並具有 public 無參數建構子：

```csharp
var child = await uiManager.Create<UI_Child>(new VisualSlot(parentToken.UI, childContainer));
var flow = await uiManager.CreateVirtual<FlowController>(new VirtualSlot(parentToken.UI));
```

### 5. 讓 recovery 接管 cleanup

不再把 cleanup logic 分散到各個呼叫端手上。

當 UI lifetime 結束時，直接 dispose token，讓 manager 進入 recovery sequence。

這樣 cleanup 才會在以下事情上維持一致：

- parent-child ordering
- pool return
- canvas unregister
- plugin teardown
- 最終 destruction

## Reuse、Init 與 Recovery

這套 system 很重視一件事：first-time setup 與 per-use reset 是兩件不同的事。

### `IUIInit`

`IUIInit` 適合處理 visual UI 的一次性 element initialization。

像是穩定的 reference 綁定，或只應該對同一個 underlying element 做一次的 setup，都適合放在這裡。這個 interface 只適用於透過 `Create<T>()` 建立的 visual UI。

### `IUIEffectable`

`IUIEffectable` 用在 visual UI 每次使用時需要 setup 與 cleanup 的情況。

`UseEffect()` 應該負責把 UI reset 回下一次使用前的乾淨狀態，並可回傳在 recovery 時執行的 cleanup action。

例如重新打開「商城視窗」時，把搜尋條件、暫存選取、提示文字或暫時性 listener 清乾淨，就屬於這一層的責任。

預設 instance lifecycle 會在 create plugins（包括 injection 與 `Init()`）完成後呼叫 `UseEffect()`。即使沒有 pooling，這個 setup/cleanup 仍會執行。同一實例重用時維持相同的 ElementID，每次 create 都取得新的 TokenID。cleanup 失敗時實例會被銷毀；已被 Unity destroy 的實例不執行 effect cleanup，也不會回到 pool。

### Recovery semantics

對已設定 pool 的 `IUIEffectable` UI 來說，recovery 的意思是：

- 離開 active tree
- 執行 cleanup
- 移回 pool root
- 等待下次 reuse

沒有 pool 或 source pool 已滿時，recovery 會在 cleanup 後銷毀 UI。

所以這套 package 的中心不是 destruction，而是 lifecycle completion。

##  擴展插件

plugin 可以處理跨 UI 共用的行為，可以參與的階段包括：

- Manager installation
- UI created
- UI will recover
- UI recovered
- Virtual UI created

Unity 預設 plugin 已經處理了一些基礎行為，例如：

- 注入 manager instance
- 為實作 `IUIInit` 的 visual UI 執行 `Init()`
- 替建立中的 UI 與 recovering UI 重新命名
- 套用 RectTransform fitting
- 偵測意外的 GameObject destruction，並於下一 frame 回收 token subtree 與閒置 canvas

使用 Plugin 可以更彈性的綁定共通的功能，也可以選擇性的使用與移除功能。

## Unity 層

核心把 ownership 與 lifecycle 跟 Unity 呈現細節分開。

Unity 層額外提供像這樣的 runtime 行為：

- 依 sorting order 配置 canvas
- 自動 reuse canvas
- RectTransform fitting
- 從 `DefaultUIElementSource` 做 prefab lookup
- 可選擇為 `IUIEffectable` UI 提供逐 source 容量限制的 pooling

圍繞著 ownership 與 lifecycle ，再做一層 Unity 的 adapter。

## Virtual UI

這個 package 透過共用的 `IBaseUI` base 支援兩種 UI 物件：

- `IVisualUI` — 需要 GameObject 與 container 的 visual UI，透過 `Create<T>()` 建立。
- `IVirtualUI` — 不需要 GameObject，但需要參與同一套 ownership / lifecycle model 的 non-visual 物件，透過 `CreateVirtual<T>()` 建立。

兩者明確分離，誤把 `IVisualUI` 型別傳入 `CreateVirtual<T>()` 是 compile-time error。

`IVirtualUI` 適合用在那些沒有視覺呈現、但應該跟 token-based ownership model 共存的 runtime object。

常見用途包括：

- flow controller
- state holder
- 非視覺的 UI process
- 需要跟著 visual parent 一起 recovery 的 runtime object

這讓 visual 與 non-visual 的 UI 相關物件，都可以共用同一套以 token 持有者為核心的 ownership system。

## 想先避免的常見誤解

- token 持有者才是目前真正擁有這段 lifetime 的那一方。
- ownership 的意思是誰 currently 持有 token，而不只是誰是 parent。
- child UI 不是只要掛在某個 transform 底下就算被擁有，它必須用正確的 parent context 建立。
- `Init()` 不等於每次開啟 UI 時都會做的 reset。
- reuse 不等於 UI 永遠不會被結束。
- 手動 destroy 或 disable UI，等於繞過這套 ownership model。

## 總結

MikaUI 最適合被理解成一套給 Unity UI 使用的 ownership 與 lifecycle framework。

它讓每個 UI 都有清楚的 create path、明確的 lifetime handle、以 token 持有者為核心的 ownership，以及一條決定 reuse 或 destruction 的 recovery path。

如果這個核心觀念在文件中被說清楚，後續 API 反而會比較容易理解。

更細的型別說明請參考 [Class Reference](ClassReference.zh-TW.md)。
