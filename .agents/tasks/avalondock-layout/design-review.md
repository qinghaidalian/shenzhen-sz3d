# 设计评审：AvalonDock 布局重构（RVS 风格面板拆分与拖拽行为）

**评审对象**：`d:\kiro\Sz3D\.agents\tasks\avalondock-layout\design.md`
**评审方式**：脱离产出上下文的独立复核。所有关于 AvalonDock 5.0.1 的 IL 断言均经实际程序集核验；所有关于源码的断言均经读源核验。

---

## 核验方法与证据

- **程序集**：`C:\Users\aisha\.nuget\packages\dirkster.avalondock\5.0.1\lib\net10.0-windows7.0\AvalonDock.dll`，FileVersion `5.0.1.0`，与 `Sz3D.Presentation.WPF.csproj` 的 `Dirkster.AvalonDock 5.0.1` 引用一致。
- **类型形状**：用 `MetadataLoadContext`（net10 控制台工具，WPF 引用程序集优先于 NETCore stub）读取成员声明。
- **IL 方法体**：用 `System.Reflection.Metadata` 的 `PEReader` 直接扫描方法体字节码，确认 `_canFloat` 构造默认值与 `get_CanFloat` / `get_AllowFloatingWindows` 的调用方集合。
- **源码**：直读 `MainWindow.xaml` / `MainWindow.xaml.cs`，并全项目检索 `四块` / `IdView` / `panel.view`。

### 已验证为真的断言（Verified Assumptions）

1. **AvalonDock 版本 5.0.1、Nodify 7.3.0** — csproj 中 `Dirkster.AvalonDock 5.0.1`、`Nodify 7.3.0`，与设计一致。
2. **`CanFloat` 在基类 `LayoutContent` 上**（`Boolean`，可写），`LayoutDocument` 与 `LayoutAnchorable` 均继承、均未重声明 → 「`CanFloat` 对 `LayoutDocument` 可用」为真。`_canFloat` 私有字段存在。
3. **`CanFloat` 构造默认值 = `true`** — `LayoutContent..ctor` IL 中确有 `ldc.i4.1; stfld _canFloat`。故对算子图追加 `CanFloat="False"` 是真实行为改变，非 no-op。
4. **`CanFloat` 被运行时浮动逻辑消费**（非仅序列化字段）。`get_CanFloat` 的实际调用方：`DockingManager.StartDraggingFloatingWindowForContent`、`DockingManager.CreateFloatingWindowCore`、`DockingManager.<CreateFloatingWindowForLayoutAnchorableWithoutParent>`、`LayoutItem.CanExecuteFloatCommand`、`LayoutAnchorableItem.CanExecuteDetachToWindowCommand`。即浮动窗口创建入口与 Float 命令 CanExecute 均读取它。
5. **`CanMove` 与 `CanFloat` 独立**：`CanMove` 分别声明在 `LayoutDocument`、`LayoutAnchorable` 各自类上（不在基类），`CanFloat` 在基类。二者无耦合 → 「禁浮动但保留停靠内移动」在类型模型上成立。
6. **`CanClose` 在 `LayoutContent`**，`LayoutDocument` 继承可用（算子图现有 `CanClose="False"` 有效）。
7. **`DockingManager.AllowFloatingWindows` 存在**，且与 `CanFloat` 并列被浮动创建路径消费（`CreateFloatingWindow*`、`StartDraggingFloatingWindowFor*`、`CanExecuteFloatCommand` 等）。故设计「不得改动 DockingManager 级开关、否则连带禁掉视图面板浮动」的警示成立。
8. **当前 `MainWindow.xaml` 结构**与设计 §1.1 / §2.4 描述完全一致：单个 `panel.view` `LayoutAnchorable`（`CanClose=False CanHide=True`）内含 `TabControl` + 两个 `TabItem`（3D 在前含 `CloudViewport`/`CloudVisual`/灯光/占位 `DataTrigger`；2D 在后含 `Image`/占位）；中央 `LayoutDocument panel.graph CanClose=False`；右列工具箱 `DockWidth=300 DockMinWidth=200`；底部日志 `DockHeight=150 DockMinHeight=60`；左列 `DockMinWidth=180`。
9. **`.cs` 现状**与设计 §2.5 的改动基点一致：常量 `IdView/IdGraph/IdToolbox/IdLog`、`RequiredIds`（4 项）、缓存字段 `_viewHost` 等、`LoadLayout` 抓缓存 switch、①a null 校验、`ReattachContent` switch、`DetachFromCurrentParent`、`EnumerateContents`、`CollectPresentIds`/`AllRequiredPresent`（无硬编码、基于 `RequiredIds`）全部存在且与描述吻合。
10. **全项目「四块」占位**仅出现在 `MainWindow.xaml.cs` 行 27、36、130、136、319（RequiredIds 注释、字段区块注释、`LoadLayout` XML doc、步骤①注释、`AllRequiredPresent` XML doc）；无遗漏于别处。设计 §2.5 第 8 点「全文件搜索替换」可完整覆盖。
11. **仓库测试工程现状**：仅 `tests\Sz3D.Core.Operators.Tests`、`tests\Sz3D.Core.Infrastructure.Tests`，无覆盖 `Sz3D.Presentation.WPF` 的测试工程 → §2.9 的描述属实。
12. **`Sz3D.sln` 存在**（设计全文件已用 `Sz3D.sln`，非早期的 `RVS.sln`）。`PointCloudRenderer.BuildModel`、`CrashLogger`（经由代码引用）、`LayoutDtoMapper`/`LayoutRootDto`/`ISerializableLayoutContent` 均为真实可用面。
13. **迁移情形是现实而非假想**：`src\Sz3D.Presentation.WPF\bin\Debug\net10.0-windows\config\layout.xml` 实际存在且仅含 `panel.view`（无 `panel.view3d/2d`）。这正是 A-2 / §2.6 / EC-3 的「旧布局触发必需面板校验失败→回退」场景，M-8 冒烟有真实样本可用。

### 未验证或与事实不符的断言（Unverified / Wrong Assumptions）

- **U-1（未静态证实，设计已标注）**：`LayoutAnchorablePane` 默认选中其**第一个**子项，从而使 3D 初始可见。此为控件层运行时行为，本次评审未从 IL 证实（§2.2 已将其列为依赖默认行为、不设 `IsSelected`，并以 M-1 冒烟兜底）。见 Finding 2。
- **U-2（未静态证实，设计已标注）**：§2.6 论证 A 的核心——「旧布局回退时，`DockManager.Layout` setter 卸下旧树后，游离子树上的 live 内容其『活动可视父』已不存在，故重挂不触发 WPF『元素已是另一元素子级』异常」——属运行时行为，无法纯静态 100% 证明。设计已诚实声明并以 M-8 冒烟 + 论证 B 退路兜底。见 Finding 1。
- **U-3（环境约束，非设计缺陷）**：`CanFloat="False"` 阻止「拖出窗口外是否 100% 不浮动」的交互终态需桌面冒烟确认（M-5）。IL 证据已覆盖命令与拖拽创建两条路径，置信度高；设计 R-1 已给退路（方案 b）。
- 说明：`tech-stack` 规则表把该库写作「AvalonDock (Xceed)」，而实际依赖为 `Dirkster.AvalonDock`。设计全程正确使用 Dirkster 实现，故此为规则文档与实仓的既有出入，**不是本设计的缺陷**，不计入发现。

---

## 发现清单（Findings）

### Finding 1 — 迁移回退路径的 live 内容重挂安全性仍属运行时假设（MEDIUM）

**位置**：§2.6「论证 A」、INV-2、EC-3。

**问题**：设计把「旧 `panel.view` 布局 → 必需面板缺失 → `ResetToDefaultLayout` → 默认 XML 回放时重挂 `_view3dHost`/`_view2dHost`」判为安全，依据是 AvalonDock `DockingManager.Layout` setter 卸下旧树后旧 presenter 不在活动可视树、故 `lc.Content = host` 不触发 WPF 父冲突。该论证的关键一步（旧内容宿主的可视/逻辑父在 Layout 替换后确已释放）未经本评审静态证实，属运行时行为。更关键的是：现有代码的 `DetachFromCurrentParent(host, root)` **只在传入的 `root`（新树）内查找并摘除残留父**，对「host 仍挂在一棵已被 Layout setter 换下、但仍被某控件逻辑引用的旧树」这种跨树残留**无能为力**。拆分前只有一份 `_viewHost`，拆分后变为两份且首次出现「新树里没有旧 ContentId、旧 host 从未进入新树」的拓扑，是**新引入的父状态轨迹**，不能仅凭「与现状同构」免证。

**为何是 MEDIUM 而非 HIGH**：设计并未声称已证伪风险，而是：(a) 给出了可执行的 M-8 冒烟（放仅含 `panel.view` 的 `layout.xml` 启动，验证不崩溃且 3D/2D 内容正常显示），且仓库里恰好已有这样一个 `layout.xml`，冒烟成本极低；(b) 预置了论证 B 退路（在 `ResetToDefaultLayout` ① 之后、② 之前对五块 host 逐个 `ContentControl.Content = null` / `RemoveLogicalChild` 显式 detach），并说明其对 §2.5 的影响。风险有界且有兜底。

**具体修复（择一，须在设计中从「可选退路」提升为「实现时必做校验点」）**：

- 方案 A（推荐，零额外代码但强制验证）：把 M-8 从「兜底冒烟」提升为**实现验收的强制门禁项**，并在实现步骤里明确「若 M-8 观察到 `InvalidOperationException`（元素已是另一元素子级）或任何重挂异常，立即启用论证 B」。即：设计需声明 M-8 为 blocking，而非 nice-to-have。
- 方案 B（更稳，主动消除跨树残留）：直接在 `ResetToDefaultLayout()` 重置后、回放前显式 detach，使 live 内容不依赖「旧 presenter 自动释放」这一运行时假设：

  ```csharp
  // ResetToDefaultLayout(): ① 之后、② 之前
  DockManager.Layout = new LayoutRoot();          // ① 卸下旧树
  foreach (var host in new[] { _view3dHost, _view2dHost, _graphHost, _toolboxHost, _logHost })
  {
      if (host is FrameworkElement fe && fe.Parent is ContentControl cc)
          cc.Content = null;                       // 主动摘除残留逻辑父，消除重挂父冲突
  }
  ApplyLayoutXml(_defaultLayoutXml);               // ② 回放默认布局并重挂
  ```

  若采方案 B，须在 §2.5 改动清单新增一条（当前清单明确「不新增条目」，与此冲突，需同步）。

---

### Finding 2 — 初始选中 3D 标签依赖未证实的「pane 默认选首项」行为，且未给确定性兜底（MEDIUM）

**位置**：§2.2「默认哪个标签在前/选中」、FR-3、AC-1、Finding 7（附录）。

**问题**：设计断言「`LayoutAnchorablePane` 默认选中其第一个子项，故把 3D 声明在前即初始可见，无需显式 `IsActive`/`IsSelected`」。本评审未能从 IL 静态证实该选择默认行为；而且观察到仓库现存 `layout.xml` 里每个 `LayoutAnchorable` 都带 `IsSelected="true"`，说明 AvalonDock 的选中态是**显式持久化**的属性，默认未必落在首项（取决于 inflate 后 `LastActivationTimeStamp`/激活顺序）。若默认不是 3D，AC-1「初始选中 3D」将不达成，而这是一个确定性可控的属性，不该留给未证实的默认行为。

**具体修复**：在 3D 的 `LayoutAnchorable` 上显式声明初始选中，去除对默认行为的依赖：

```xml
<avalondock:LayoutAnchorable Title="3D 视图" ContentId="panel.view3d"
                             CanClose="False" CanHide="True"
                             IsSelected="True" IsActive="True">
```

同时 §2.2 把「依赖 pane 默认选首项」改为「显式 `IsSelected=True`（必要时并 `IsActive=True`）保证确定性」，并在 §2.10 XAML 清单第 1 条补上该属性。保留 M-1 冒烟作最终确认。

> 注：`IsSelected` 会被持久化进 `layout.xml`，仅影响**默认布局 / 回退布局**的初始呈现，不影响用户后续自定义布局的恢复（AC-6），与 Finding 5 的「尊重用户布局」不冲突。

---

### Finding 3 — AC-6 必需面板清单与 `AllRequiredPresent` 的「浮动/隐藏仍算存在」语义未在验收层写死（NIT）

**位置**：AC-6、EC-1、EC-2、§2.8。

**问题**：AC-6 要求「包含全部必需面板则恢复用户布局」，EC-1/EC-2 说明浮动或隐藏态的面板经 `EnumerateContents`（基于 `Descendents()`）仍可枚举、仍通过校验。该行为依赖 `LayoutRoot.Descendents()` 覆盖 `FloatingWindows`/`Hidden` 分区——现有 `EnumerateContents` 注释也这么声称，但 AC 层没有把「用户把某视图面板浮动/隐藏后重启仍恢复、不误判缺失→回退」列为显式验收点。这是现有机制、本次不改代码，但验收覆盖面有缺口。

**具体修复**：在 §2.9 冒烟补一条 M-9：「把 3D（或 2D）面板浮动为独立窗口后正常退出，重启应恢复该浮动态、不触发必需面板缺失回退」，使 EC-1 可被验收覆盖。无需改代码。

---

## 结论

- 四个核查重点中，(1) 算子图禁浮动方案、(3) `.cs` 持久化改动覆盖面、(4) 不破坏 Nodify/chrome，均已由 IL + 源码核验**成立且完整**。(2) 2D/3D 同 pane 成一个 Tab 组的结构做法**正确**，但其「初始选中 3D」子项依赖未证实的默认行为（Finding 2）。
- 存在 **2 个 MEDIUM**（Finding 1 迁移重挂安全性仍为运行时假设且跨树残留未被 `DetachFromCurrentParent` 覆盖；Finding 2 初始选中依赖未证实默认、缺确定性兜底）与 **1 个 NIT**（Finding 3）。
- 按机械判定：HIGH+MEDIUM = 2 > 0 → **CHANGES_REQUESTED**。
- 两个 MEDIUM 均为「小改即可消除」：Finding 2 加一个 `IsSelected="True"` 属性；Finding 1 要么把 M-8 升为 blocking 门禁、要么直接采纳论证 B 的显式 detach（并同步 §2.5 清单）。修完即可通过。
