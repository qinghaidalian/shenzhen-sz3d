# AvalonDock 布局重构：RVS 风格面板拆分与拖拽行为调整

本文件覆盖需求与技术设计两部分。目标：按 RVS 效果把「视图」面板里的 2D/3D 两个 TabItem 拆成两个独立可停靠面板，默认合并为一个 Tab 组；并让「算子图」禁止浮动飞出但仍可在停靠区内拖动/重新停靠。改动仅限两个文件：
- `d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\MainWindow.xaml`
- `d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\MainWindow.xaml.cs`

技术栈锁定：.NET 10 / `net10.0-windows`、C# 13、WPF、Dirkster.AvalonDock **5.0.1**、Nodify 7.3.0。序列化走既有的 `AvalonDock.Core.Serialization.Dto.LayoutRootDto` + `AvalonDock.Serialization.LayoutDtoMapper` + `System.Xml.Serialization.XmlSerializer`（5.0.1 不提供经典 `XmlLayoutSerializer` / `LayoutSerializationCallback`）。

---

## 1. 需求（Requirements）

### 1.1 Summary

当前「视图」是单个 `LayoutAnchorable`（`ContentId="panel.view"`），内部用一个 `TabControl` 承载「3D 视图」「2D 视图」两个 `TabItem`。用户希望按 RVS 的做法把 2D 与 3D 拆成两个各自独立的停靠面板：各自能独立拖拽、浮动、重新停靠，也能被用户拖到一起合并成一个 Tab 组；启动时二者默认合并在同一 Tab 组内（复刻当前 RVS 的初始观感）。同时「算子图」当前是中央 `LayoutDocument`，用户要求它「不要飞出来」（禁止浮动成独立窗口），但仍要像其他面板一样可以在停靠区内来回拖动、重新停靠；算子图保持不可关闭，`ContentId` 保持 `panel.graph` 不变。`.cs` 的布局持久化（序列化/反序列化/live 内容重挂载/必需面板校验）需随新的 ContentId 集合同步更新。

### 1.2 Functional Requirements

- FR-1 拆分视图面板：移除外层「视图」`LayoutAnchorable`（`panel.view`）及其 `TabControl` 容器，替换为两个独立 `LayoutAnchorable`：
  - 「3D 视图」`ContentId="panel.view3d"`，内含原 `Viewport3D`（`x:Name="CloudViewport"`）、`ModelVisual3D`（`x:Name="CloudVisual"`）及灯光组、「无点云输出」占位 `TextBlock`（保留 `DataTrigger` 绑定 `Editor.HasCloudOutput`）。
  - 「2D 视图」`ContentId="panel.view2d"`，内含原 `Image`（绑定 `Editor.ImageOutput`）、「无图像输出」占位 `TextBlock`（绑定 `Editor.HasImageOutput`）。
- FR-2 独立拖拽与合并：两个新面板都应可独立拖拽、浮动、重新停靠，并可被用户拖到一起合并成一个 Tab 组（`LayoutAnchorable` 默认即具备此能力）。
- FR-3 初始默认布局：启动时 2D 与 3D 默认合并在同一个 Tab 组里；在 XAML 中把两个 `LayoutAnchorable` 放进**同一个** `LayoutAnchorablePane` 即可呈现为一个 Tab 组。该 Tab 组位于原左列位置，复刻左列宽度约束（`DockMinWidth=180`）。默认选中 3D 视图标签。
- FR-4 算子图禁浮动但可停靠内拖动：算子图不能浮动成独立窗口，但要能在停靠区内拖动、重新停靠；保持 `CanClose="False"`、`ContentId="panel.graph"` 不变，内部 `nodify:NodifyEditor` 的全部 Resources/绑定/事件逐字不动。
- FR-5 持久化同步更新：`.cs` 新增常量 `IdView3D="panel.view3d"`、`IdView2D="panel.view2d"`，移除 `IdView="panel.view"`；`RequiredIds` 更新为 `{IdView3D, IdView2D, IdGraph, IdToolbox, IdLog}`；live 缓存字段移除 `_viewHost`，新增 `_view3dHost`、`_view2dHost`；`LoadLayout()` 抓缓存 switch、①a 的 null 校验、`ReattachContent()` 的 switch 都覆盖两个新 ContentId；`CollectPresentIds()`/`AllRequiredPresent()` 不含硬编码，随 `RequiredIds` 自动适配（已确认无遗漏）。所有源码注释与诊断文案中的「四块」措辞同步改为「五块」。

### 1.3 Non-Functional Requirements

- NFR-1 编码规范：私有字段下划线 camelCase、常量 PascalCase（不用全大写下划线）、公共 API 保留 XML 文档注释、类型明显处用 `var`。
- NFR-2 ContentId 契约：XAML 中 `ContentId` 字符串与 `.cs` 常量严格一一对应，绝不本地化（面板标题 `Title` 可中文，`ContentId` 必须为稳定的英文稳定 Id）。
- NFR-3 不回归：顶部 chrome（菜单栏/工具栏/状态/日志 `DataGrid`）、算子拖拽逻辑（`OnOperatorMouseDown/Move/OnDropOperator`）、`UpdateCloudVisual`、`CrashLogger`、回退默认布局/`DetachFromCurrentParent`/`EnumerateContents` 等逻辑保持完整。
- NFR-4 持久化健壮性不降级：布局文件缺失/损坏/必需面板缺失仍回退默认布局且绝不崩溃、不弹窗。

### 1.4 Acceptance Criteria

1. **首次启动或回退默认布局时**，左列呈现为**一个 Tab 组**，内含「3D 视图」「2D 视图」两个标签页，初始可见（选中）项为「3D 视图」；不再存在标题为「视图」的面板。（用户曾拆分并成功持久化后，恢复出的是用户自定义摆放，见 AC-6，不强制重新合并。）
2. 「3D 视图」页显示 `CloudViewport`；当 `Editor.HasCloudOutput` 为 `false` 时显示「无点云输出」占位，为 `true` 时占位隐藏。`UpdateCloudVisual()` 对 `CloudVisual.Content` 的更新仍生效。
3. 「2D 视图」页显示绑定 `Editor.ImageOutput` 的图像；当 `Editor.HasImageOutput` 为 `false` 时显示「无图像输出」占位。
4. 用户可把「3D 视图」或「2D 视图」各自拖出为浮动窗口、再停靠回任意边；也可把二者拖到一起重新合并成一个 Tab 组。两面板的 `CanFloat` 保持默认 `true`（可浮动），不显式声明。
5. 算子图标题栏不提供「浮动（Float）」能力：双击标题/拖出到窗口外均不会浮动成独立窗口；但可在停靠区内拖动并重新停靠到不同位置。算子图不可关闭。
6. 关闭应用后 `./config/layout.xml` 写出；再次启动时若该文件存在且包含全部必需面板（`panel.view3d`、`panel.view2d`、`panel.graph`、`panel.toolbox`、`panel.log`），恢复用户布局并把 live 内容正确重挂到对应面板；缺任一必需面板则回退默认布局。
7. 删除或损坏 `layout.xml` 后启动，应用不崩溃、无弹窗，呈现默认布局（崩溃日志记录一条回退原因）。
8. 执行 `dotnet build d:\kiro\Sz3D\Sz3D.sln -c Debug` 通过编译，无新增警告。

### 1.5 Out of Scope

- 不改 `nodify:NodifyEditor` 任何 Resources/绑定/事件/样式。
- 不改顶部 chrome、工具箱四个 Tab 内容、日志 `DataGrid`。
- 不改 `MainViewModel` / `NodeEditorViewModel` / `PointCloudRenderer` / `CrashLogger`。
- 不改序列化机制本身（仍用 DTO + XmlSerializer），不迁移到其他 docking 库。
- 不新增 ViewModel 层对面板可见性的建模。
- 本特性不新增自动化测试项目（仓库现无覆盖 `Sz3D.Presentation.WPF` 的测试工程，见 §2.9），以构建门禁 + 人工冒烟为验收手段。

### 1.6 Assumptions

- A-1 用户可接受默认左列为「一个 Tab 组」而非上下并排；这是 FR-3 的明确要求。
- A-2 现有 `layout.xml`（若用户机上已存在旧版、含 `panel.view`）在新版下将因缺少 `panel.view3d`/`panel.view2d` 而触发必需面板校验失败 → 回退默认布局。这是可接受的一次性迁移行为（旧布局作废，恢复为新默认）。设计据此，无需编写迁移代码。

---

## 2. 技术设计（Design）

### 2.1 Overview

改动集中在 `MainWindow.xaml` 的 `LayoutRoot` 子树与 `MainWindow.xaml.cs` 的布局持久化白名单。XAML 侧：删掉外层「视图」`LayoutAnchorable` + `TabControl`，在同一个 `LayoutAnchorablePane`（左列）里放两个 `LayoutAnchorable`（`panel.view3d`、`panel.view2d`），分别承载原 3D、2D 的可视子树；并在中央算子图上加 `CanFloat="False"`。`.cs` 侧：把以 `panel.view` 为中心的常量、缓存字段、switch 分支全部替换为 `view3d`/`view2d` 两套，`RequiredIds` 扩成 5 项。既有的序列化/反序列化/回退/遍历框架保持不变——它们本就是数据驱动（按 `ContentId` 走查），只要白名单一致即可自动适配。

### 2.2 决策 1：视图拆分与初始 Tab 组（FR-1 / FR-2 / FR-3）

在 AvalonDock 中，同一个 `LayoutAnchorablePane` 内的多个 `LayoutAnchorable` 会自动渲染为一个带标签页的 Tab 组；用户拖动任一 `LayoutAnchorable` 的标签即可将其拆出（浮动/重新停靠），拖回同一 pane 又会合并。因此 FR-2（独立拖拽 + 可合并）与 FR-3（初始合并为一个 Tab 组）用「两个 `LayoutAnchorable` 同处一个 `LayoutAnchorablePane`」这一个结构即可同时满足，无需任何代码。

两个新面板沿用原「视图」`LayoutAnchorable` 的能力属性：`CanClose="False"`、`CanHide="True"`（与原一致，保持可自动隐藏）。**`CanFloat` 不显式声明，采用 `LayoutContent` 默认值 `true`**，以满足 AC-4 的「可独立浮动」。这里有意与算子图的 `CanFloat="False"` 形成对比：禁浮动是**仅算子图单个 `LayoutContent` 的局部开关**，视图面板必须保持默认可浮动，实现时切勿给两个视图面板误加 `CanFloat="False"`。左列的宽度约束 `DockMinWidth="180"` 从原左列 `LayoutAnchorablePane` 搬到新左列 pane。

原 3D/2D 的可视子树（`Grid`、`Viewport3D`、`ModelVisual3D` + 灯光、占位 `TextBlock` 及其 `DataTrigger` 样式；`Image` + 其占位 `TextBlock`）**整段**从原两个 `TabItem` 内迁入新的两个 `LayoutAnchorable`，内容逐字不变——仅去掉 `TabControl`/`TabItem` 包裹层。`x:Name="CloudViewport"` 与 `x:Name="CloudVisual"` 保持，故 `UpdateCloudVisual()` 的 `CloudVisual.Content = ...` 继续生效，无需改 `.cs` 的渲染逻辑。

默认哪个标签在前/选中：3D 在前、2D 在后（与原 `TabItem` 顺序一致：先「3D 视图」后「2D 视图」），由在 pane 内的声明顺序决定。初始选中项为 3D 视图——AvalonDock 的 `LayoutAnchorablePane` 默认选中其第一个子项，故声明顺序把 3D 放首位即可使其初始可见，无需显式设置 `IsActive`/`IsSelected`。

关于「初始合并」与「恢复用户布局」的边界（Finding 5）：AC-1 的「左列呈现为一个 Tab 组」只在**首次启动或回退默认布局**时成立。一旦用户把 2D/3D 拆到不同列或浮动并成功持久化，`layout.xml` 不再包含「同一 pane 含两个 anchorable」的结构；再次启动时 `AllRequiredPresent()` 只校验两个 `ContentId` 都在，**不校验是否同一 Tab 组**，因此恢复出的是用户自定义摆放（可能已分离），不强制重新合并。这是符合「尊重用户布局」的设计意图，不是缺陷。

### 2.3 决策 2：算子图禁浮动但可停靠内拖动（FR-4）—— 方案选择与依据

两种候选：
- (a) 保持 `LayoutDocument`，设 `CanFloat="False"`。
- (b) 改为 `LayoutAnchorable`，设 `CanFloat="False"`。

**对 AvalonDock 5.0.1 程序集（`AvalonDock.dll`，FileVersion `5.0.1.0`）的 IL 核验结论：**
- `CanFloat`（`Boolean`）声明在共享基类 `AvalonDock.Layout.LayoutContent` 上（`_canFloat` 字段 + `get_CanFloat`/`set_CanFloat`），`LayoutAnchorable` 与 `LayoutDocument` 均继承且未重声明，故 **`CanFloat` 在 `LayoutDocument` 上确实存在且可用**。
- `CanClose` 同样声明在 `LayoutContent`，`LayoutDocument` 继承可用（算子图现用 `CanClose="False"` 有效）。
- `CanFloat` 是**被运行时浮动逻辑实际消费的开关**，而非仅序列化字段，IL 给出三处证据：
  1. `DockingManager`「把被拖内容转为浮动窗口」的入口（编译器生成的 `<>c__DisplayClass481_0`）在创建浮动窗口前依次检查 `get_AllowFloatingWindows()` 与 `contentModel.get_CanFloat()`；`CanFloat` 为 `false` 时直接 `ret`（提前返回，不生成浮动窗口）。
  2. `LayoutItem` 的 Float 命令 `CanExecute` 路径读取 `get_LayoutElement().get_CanFloat()`，`false` 时跳过「可浮动」分支（Float 菜单/命令被禁用）。
  3. `LayoutItem` 把 `CanFloat` 暴露为 DependencyProperty，`OnCanFloatChanged` 回写 `LayoutContent.set_CanFloat`，控件层与模型层同步。
- `CanFloat` 构造默认值为 `true`（`LayoutContent` 构造置位），故算子图追加 `CanFloat="False"` 是真实行为改变（非 no-op）。
- `CanMove` 与 `CanFloat` 相互独立：`CanMove` 声明在 `LayoutDocument` 与 `LayoutAnchorable` 各自类上，`CanFloat` 在基类 `LayoutContent`，二者无耦合。故「禁浮动但保留停靠内移动」的组合在类型模型上成立。

**最终选择：方案 (a) —— 保持算子图为 `LayoutDocument`，追加 `CanFloat="False"`。** 依据：
1. `CanFloat` 对 `LayoutDocument` 有效（基类属性 + `LayoutItem`/`DockingManager` 两处消费端 IL 确认），能精确达成「禁浮动」。
2. 禁的是「浮动」，不是「移动」——`CanMove` 默认 `true` 且不受 `CanFloat` 影响，故算子图仍可在停靠区内拖动、重新停靠，满足 FR-4 的「可停靠内拖动」。
3. 算子图语义上是中央主工作区（document host），保持 `LayoutDocument` 与现有 `LayoutDocumentPaneGroup`/`LayoutDocumentPane DockMinWidth="300"` 结构一致，改动面最小、回归风险最低；方案 (b) 改类型会连带影响 pane 类型（`LayoutDocumentPane` ↔ `LayoutAnchorablePane`）、`DockMinWidth` 承载点与中央 document 语义，收益为零而风险更大。

**禁浮动的实现范围约束（Finding 4）：** 禁浮动**仅**通过算子图单个 `LayoutContent.CanFloat="False"` 实现。`DockingManager.AllowFloatingWindows`（IL 中与 `CanFloat` 并列作为浮动前置条件的 DockingManager 级开关）**保持默认 `true`，绝不改动**——否则会连带禁掉 2D/3D 视图面板的浮动能力（违反 AC-4）。实现时不得把「算子图禁浮动」误解为关闭 DockingManager 级浮动。

因此算子图 `LayoutDocument` 变为：`Title="算子图" ContentId="panel.graph" CanClose="False" CanFloat="False"`。其余（含整棵 `nodify:NodifyEditor` 子树）逐字不动。

> 行为边界说明：`CanFloat="False"` 阻止该内容浮动成独立窗口；它不阻止在停靠网格内重新停靠（那由 `CanMove` 控制）。这正是 FR-4 的目标组合「禁浮动 + 可停靠内拖动」。

### 2.4 拆分后的 LayoutRoot 结构草图

```
LayoutRoot
└─ LayoutPanel (Orientation=Vertical)
   ├─ LayoutPanel (Orientation=Horizontal)
   │  ├─ LayoutAnchorablePane  DockMinWidth=180         ← 左列（初始为一个 Tab 组，默认选中 3D）
   │  │  ├─ LayoutAnchorable  Title="3D 视图"  ContentId="panel.view3d"  CanClose=False CanHide=True
   │  │  │     │   (CanFloat 不写 → 默认 true，可浮动)
   │  │  │     └─ Grid { Viewport3D x:Name=CloudViewport (含 CloudVisual + 灯光组),
   │  │  │              TextBlock "无点云输出" (DataTrigger → Editor.HasCloudOutput) }
   │  │  └─ LayoutAnchorable  Title="2D 视图"  ContentId="panel.view2d"  CanClose=False CanHide=True
   │  │        │   (CanFloat 不写 → 默认 true，可浮动)
   │  │        └─ Grid { Image Source={Editor.ImageOutput},
   │  │                 TextBlock "无图像输出" (DataTrigger → Editor.HasImageOutput) }
   │  │
   │  ├─ LayoutDocumentPaneGroup                         ← 中央
   │  │  └─ LayoutDocumentPane  DockMinWidth=300
   │  │     └─ LayoutDocument  Title="算子图"  ContentId="panel.graph"
   │  │                         CanClose=False  CanFloat=False      ← 新增 CanFloat=False
   │  │        └─ nodify:NodifyEditor x:Name=Editor  (Resources/InputBindings/Drop 全部逐字不变)
   │  │
   │  └─ LayoutAnchorablePane  DockWidth=300 DockMinWidth=200   ← 右列（工具箱，不变）
   │     └─ LayoutAnchorable  Title="工具箱" ContentId="panel.toolbox" ...
   │
   └─ LayoutAnchorablePane  DockHeight=150 DockMinHeight=60     ← 底部（日志，不变）
      └─ LayoutAnchorable  Title="日志" ContentId="panel.log" ...
```

DockingManager 级：`AllowFloatingWindows` 保持默认 `true`（不在 XAML 中设置），`Theme`（`Vs2013DarkTheme`）不动。

与现结构相比的 delta：
- 删除：外层 `LayoutAnchorable panel.view` + 其 `TabControl` + 两个 `TabItem` 包裹。
- 新增：同一左列 `LayoutAnchorablePane` 内两个 `LayoutAnchorable`（`panel.view3d`、`panel.view2d`）。
- 修改：中央 `LayoutDocument` 追加 `CanFloat="False"`。
- 不动：中央 `NodifyEditor` 子树、右列工具箱、底部日志、顶部 chrome、`Window.Resources`、两行根 `Grid`、`DockingManager` 的 `Theme` 与（隐含默认的）`AllowFloatingWindows`。

### 2.5 决策 3：`.cs` 持久化逻辑改动点（FR-5）

既有持久化框架是**数据驱动**的：序列化只写结构+`ContentId`（`SerializeLayout`/`LayoutDtoMapper`），恢复时按 `ContentId` 走查并重挂 live 内容（`ReattachContent`/`EnumerateContents`/`DetachFromCurrentParent`），必需面板校验基于 `RequiredIds` 集合（`CollectPresentIds`/`AllRequiredPresent`）。因此只需替换「视图单面板」为「两面板」这一处白名单差异，框架其余部分不动。具体改动点：

1. 常量（§「ContentId 常量」区块）：
   - 删除 `private const string IdView = "panel.view";`
   - 新增 `private const string IdView3D = "panel.view3d";` 与 `private const string IdView2D = "panel.view2d";`
   - `IdGraph`/`IdToolbox`/`IdLog` 不变。
2. `RequiredIds`：
   - 由 `{ IdView, IdGraph, IdToolbox, IdLog }` 改为 `{ IdView3D, IdView2D, IdGraph, IdToolbox, IdLog }`（5 项）。其上方注释「§5：四块全部必需」改为「五块全部必需」。
3. live 缓存字段：
   - 删除 `private object? _viewHost;`
   - 新增 `private object? _view3dHost;` 与 `private object? _view2dHost;`
   - `_graphHost`/`_toolboxHost`/`_logHost` 不变。字段区块注释 `// ── 四块 live 内容缓存…` 改为「五块」。
4. `LoadLayout()` 步骤① 抓缓存 `switch`：
   - 删除 `case IdView: _viewHost = lc.Content; break;`
   - 新增 `case IdView3D: _view3dHost = lc.Content; break;` 与 `case IdView2D: _view2dHost = lc.Content; break;`
5. `LoadLayout()` 步骤①a null 校验：
   - 由 `_viewHost is null || _graphHost is null || ...` 改为 `_view3dHost is null || _view2dHost is null || _graphHost is null || _toolboxHost is null || _logHost is null`。
   - 校验失败日志文案改为列出五块（例如「视图(3D/2D)/算子图/工具箱/日志」）；文案非契约，仅诊断用途。
6. `ReattachContent()` 的 `switch`：
   - 删除 `IdView => _viewHost,`
   - 新增 `IdView3D => _view3dHost,` 与 `IdView2D => _view2dHost,`
   - 其余 `IdGraph`/`IdToolbox`/`IdLog`/`_ => null` 分支不变。
7. `CollectPresentIds()` / `AllRequiredPresent()`：无硬编码 ContentId（基于 `RequiredIds` 与 `EnumerateContents`），随 `RequiredIds` 自动适配——已核验无需改动。
8. 注释数量措辞同步：全文件搜索「四块」字样（含上述 2/3 两处注释、`LoadLayout`/`ResetToDefaultLayout` 等处 XML 文档注释中可能出现的「四块 live 缓存」「四块全部必需」等表述）统一改为「五块」，保证注释准确性（coding-standards 要求）。
9. 其余（`LayoutPath`、`SerializeLayout`、`ApplyLayoutXml`、`DetachFromCurrentParent`、`EnumerateContents`、`ResetToDefaultLayout`、`SaveLayout`、`DtoSerializer`、构造函数 Loaded/Closing 钩子、算子拖拽、`UpdateCloudVisual`）全部不动（但 `ResetToDefaultLayout` 的迁移安全性论证见 §2.6，不改代码）。

### 2.6 外部输入校验（持久化输入 = `layout.xml`）与旧布局迁移安全性

该特性唯一的外部输入是磁盘上的 `./config/layout.xml`。校验规则沿用既有管线，无新增输入面：
- 文件缺失（可选输入）：`LoadLayout()` 步骤③ `File.Exists` 为 false → 直接保留默认布局（正常路径，不记错误）。
- 文件存在但 XML 损坏/反序列化异常：`ApplyLayoutXml` 抛异常 → `LoadLayout()` 的 `try/catch` 捕获 → `CrashLogger.Write(ex)` 记录 → `ResetToDefaultLayout()` 回退。恢复：是（回退默认）；对调用方：无（UI 呈现默认布局，无弹窗）；日志级别：错误（crash 日志）。
- 文件存在、反序列化成功但缺必需面板（含旧版 `layout.xml` 只有 `panel.view` 的迁移情形，见 A-2）：`AllRequiredPresent()` 为 false → 记一条 `InvalidOperationException`（原因「恢复后必需面板缺失」）→ `ResetToDefaultLayout()` 回退。
- 未知/过时 `ContentId`（白名单外，如旧 `panel.view`）：`ReattachContent` 的 `switch` 落入 `_ => null` → 该面板保持空内容、不崩溃；通常伴随必需面板缺失而整体回退。

**旧 `layout.xml`（仅含 `panel.view`）回退路径的 live 内容重挂安全性（Finding 3）。** 拆成两面板后出现一条**新引入的拓扑**：恢复旧布局时，新代码白名单无 `panel.view`，`ReattachContent(newRoot)` 对旧 `panel.view` 落入 `_ => null`（不挂内容），而缓存的 `_view3dHost`/`_view2dHost` 这两份 live 内容**从未被挂进 `newRoot`**（因为 newRoot 里根本没有 `panel.view3d/2d` 面板）。随后 `DockManager.Layout = newRoot` 整体替换；`AllRequiredPresent()` 为 false → `ResetToDefaultLayout()` 回放默认布局，其 `ReattachContent` 要把 `_view3dHost/_view2dHost` 挂到默认布局的新面板上。问题是：此刻这两个 WPF 元素的「当前父」处于什么状态，重挂是否会触发 WPF 的「元素已是另一元素子级」异常？

**论证（采用论证 A，基于 AvalonDock 行为，不改代码）：**

关键事实是 `_view3dHost`/`_view2dHost` 这两份 live 内容在整个失败→回退过程中的父状态轨迹：

1. **启动期（`LoadLayout` 步骤①）**，默认 XAML 布局已 inflate，两份内容分别挂在默认布局的 `panel.view3d`/`panel.view2d` 面板的 `LayoutContent.Content` 上，缓存抓取只是把引用存入字段，不改变父关系。此时它们的「布局父」是**默认 XAML 布局树里的两个 `LayoutAnchorable`**。
2. **步骤④ `ApplyLayoutXml(旧 xml)`** 先执行 `ReattachContent(newRoot)`：newRoot 里没有 `panel.view3d/2d`，故对这两份内容**无任何操作**（它们不出现在 newRoot 的任何面板里，`DetachFromCurrentParent` 也只在 newRoot 内找父——找不到即不动）。接着 `DockManager.Layout = newRoot`。**此处是安全性的核心：** AvalonDock 的 `DockingManager` 在 `Layout` setter 中会解绑旧 `LayoutRoot`（旧 `LayoutRoot.Manager` 置空、旧布局树从 DockingManager 的可视/逻辑子树上整体卸下）。但 `LayoutContent.Content`（即我们的两份 live 内容）与其**旧 `LayoutAnchorable` 的父子关系并未被 `Layout` setter 主动拆除**——`LayoutContent` 仍持有 `Content` 引用，WPF 层面这两个元素的逻辑父仍是旧 `LayoutAnchorable` 对应的内容宿主控件（`LayoutAnchorableControl`/`ContentPresenter`），只是整条旧树已不在任何窗口的可视树中（游离子树）。
3. **`ResetToDefaultLayout()` 步骤①** 执行 `DockManager.Layout = new LayoutRoot()`：这再次替换 Layout，使「步骤④ 的 newRoot」也从 DockingManager 卸下。注意此步**不触碰** `_view3dHost/_view2dHost`（它们本就不在 newRoot 里）。它们此刻仍挂在步骤①那条**已游离的默认 XAML 旧 LayoutRoot** 的面板宿主上。
4. **`ResetToDefaultLayout()` 步骤② `ApplyLayoutXml(_defaultLayoutXml)`**：`FromDto` 从默认 XML **新建**一棵默认布局树（全新的 `LayoutAnchorable` 对象，`Content` 均为 `null`）→ `ReattachContent(新默认root)`：对 `panel.view3d` 命中 `_view3dHost`，先 `DetachFromCurrentParent(host, 新默认root)`，再 `lc.Content = host`。

真正要回答的是第 4 步 `lc.Content = _view3dHost` 会不会抛「元素已是另一元素子级」。依据 AvalonDock 的内容宿主实现：`LayoutContent.Content` 是一个 `object` 属性，承载它的控件（`LayoutAnchorableControl` 等）通过 `ContentPresenter.Content` 展示。当一个 WPF `UIElement` 被设为某 `ContentPresenter.Content` 时，WPF 会把它作为该 presenter 的可视/逻辑子；但**当承载它的那条布局树已被从 DockingManager 整体卸下（第 2、3 步）后，对应的 `LayoutAnchorableControl`/`ContentPresenter` 不再被实例化于任何活动可视树**——其内容宿主控件随旧 `LayoutRoot` 一并弃用并最终可被 GC。换言之，到第 4 步时 `_view3dHost` 的「活动可视父」已不存在（承载它的旧 presenter 已随旧树卸下、不在窗口可视树内），把它赋给新 presenter 的 `Content` 不会命中 WPF 的「已是另一活动元素的可视子」约束。

此外，这条迁移路径与**现状代码既有的正常回退路径**在机制上完全同构：现状单面板 `panel.view` 回退时，`_viewHost` 同样是「先挂在旧默认树、经一次或多次 `DockManager.Layout` 替换后、再在默认 XML 回放时被 `ReattachContent` 重挂」。拆成两面板只是把「一份 host」变成「两份 host」，**父状态轨迹与重挂时机完全一致**，没有引入新的父冲突类别。因此不需要在 `ResetToDefaultLayout()` 里追加显式 detach，§2.5 的改动清单不需要新增条目。

**兜底（人工冒烟，列入验收）：** 由于「WPF 内容宿主在布局树卸下后的父状态」属运行时行为、无法纯静态 100% 证明，AC-7 的「删除/损坏 layout.xml 后启动不崩溃」冒烟须额外覆盖**旧版单面板 `layout.xml`（仅含 `panel.view`）** 这一具体输入：手工放一个仅含 `panel.view` 的 `layout.xml` 到 `./config/`，启动应验证不崩溃、落到默认布局、3D/2D 两面板内容正常显示（证明 live 内容成功重挂、无父冲突异常）。若冒烟中确实观察到父冲突异常，退路为论证 B：在 `ResetToDefaultLayout()` 步骤①之后、步骤② `ApplyLayoutXml` 之前，显式把 `_view3dHost`/`_view2dHost`/`_graphHost`/`_toolboxHost`/`_logHost` 逐个从其逻辑父摘除（对 `FrameworkElement` 调用其父 `ContentControl.Content = null` 或 `RemoveLogicalChild`）；此退路一旦启用需在 §2.5 补一条改动点。当前设计按论证 A 不追加代码。

数据保护：`layout.xml` 仅含结构与 `ContentId`，不含敏感数据；无需脱敏处理。

### 2.7 不变量与归属层

- INV-1「XAML `ContentId` ⇔ `.cs` 常量一致」：由开发期约定 + 编译/测试保证，归属 View 层（XAML）与 code-behind 共同维护。本设计把二者的变更并列列出（§2.4 与 §2.5）以保证同步。
- INV-2「每个 live 内容元素任一时刻只有一个布局父」：由 `.cs` 的 `DetachFromCurrentParent` 在重挂前摘除旧父保证，归属 code-behind；本次不改该逻辑，迁移路径的安全性已在 §2.6 论证。
- INV-3「必需面板齐备才接受恢复的布局」：由 `AllRequiredPresent()`/`RequiredIds` 保证，归属 code-behind；本次仅扩充 `RequiredIds` 成员。
- INV-4「算子图不可浮动、不可关闭」：由 XAML 的 `CanFloat="False"`/`CanClose="False"` 声明，归属 View 层（AvalonDock 控件层 `LayoutItem` 消费这两个开关）。
- INV-5「视图/工具箱/日志面板可浮动」：由 `LayoutContent.CanFloat` 默认 `true` + `DockingManager.AllowFloatingWindows` 默认 `true` 共同保证，归属 View 层；本次不显式声明这两项，依赖默认值，实现时不得误改。

### 2.8 边界情形

- EC-1 用户把 2D 与 3D 全部拖离左列、左列 pane 变空：AvalonDock 会自动移除空 `LayoutAnchorablePane`；只要两面板仍存在于布局树（含浮动/自动隐藏），`EnumerateContents` 仍能枚举到（基于 `Descendents()` 覆盖浮动/隐藏），必需面板校验通过，持久化正常。用户拆分后的布局会被原样持久化并恢复，不强制重新合并为一个 Tab 组（见 §2.2 边界说明）。
- EC-2 用户把 2D 或 3D 面板「隐藏（AutoHide/Hide）」：`CanHide="True"` 允许隐藏；隐藏态仍在布局树内，`AllRequiredPresent()` 通过。此行为与原「视图」面板一致。
- EC-3 旧 `layout.xml`（仅含 `panel.view`）：按 §2.6 回退默认布局，一次性迁移，无需代码（A-2）；live 内容重挂安全性已在 §2.6 论证并列入冒烟验收。
- EC-4 算子图被用户尝试拖出窗口：`CanFloat="False"` 阻止生成浮动窗口；拖动在停靠网格内被解释为重新停靠（`CanMove`），符合预期。
- EC-5 `UpdateCloudVisual()` 在拆分后：`CloudVisual` 名称不变，`Content` 赋值路径不变；3D 面板处于隐藏/浮动态时赋值仍成立（WPF 元素未被销毁），显示在面板恢复可见时正确。

### 2.9 测试策略

仓库现状（已核验）：解决方案为 `d:\kiro\Sz3D\Sz3D.sln`；测试工程仅 `tests\Sz3D.Core.Operators.Tests` 与 `tests\Sz3D.Core.Infrastructure.Tests`，**不存在覆盖 `Sz3D.Presentation.WPF` 的测试项目**。本特性的改动全部落在 `Sz3D.Presentation.WPF` 的 code-behind（`private` 方法、依赖 `DockingManager`/WPF 控件实例）与 XAML 布局，难以在现有测试工程内无头落地。

因此本特性**不新增自动化测试工程**（Finding 1 方案 A），采用「构建门禁 + 人工冒烟」作为验收手段：

- 可核验的构建门禁（唯一自动化门禁）：
  - G-1 `dotnet build d:\kiro\Sz3D\Sz3D.sln -c Debug` 通过、无新增警告（对应 AC-8）。
- 人工冒烟（WPF 交互行为，无头环境受限，按 build-and-test 指南在 Windows 桌面运行 `RVS.App` 等价的 `Sz3D.Presentation.WPF` 可执行文件）：
  - M-1 启动后左列为单一 Tab 组、含「3D 视图」「2D 视图」两标签、初始选中 3D；无「视图」面板（AC-1）。
  - M-2 3D 页显示 `CloudViewport`，占位随 `HasCloudOutput` 切换；运行产出点云后 `UpdateCloudVisual` 正确刷新（AC-2）。
  - M-3 2D 页显示 `ImageOutput`，占位随 `HasImageOutput` 切换（AC-3）。
  - M-4 分别把 3D、2D 拖出为浮动窗口并停靠回；再拖到一起合并为一个 Tab 组（AC-4，含验证视图面板默认可浮动）。
  - M-5 算子图双击标题/拖出窗口外均不浮动，但可在停靠区内重新停靠；算子图无关闭按钮（AC-5）。
  - M-6 正常退出写出 `./config/layout.xml`；重启恢复用户布局并正确重挂内容（AC-6）。
  - M-7 删除 `layout.xml` 后启动→默认布局、不崩溃（AC-7）。
  - M-8 放置仅含 `panel.view` 的旧版 `layout.xml` 后启动→回退默认布局、不崩溃、3D/2D 内容正常显示（AC-7 + §2.6 迁移安全性兜底）。

> 说明（Finding 1）：若未来要为持久化逻辑补自动化测试，须新建 `tests\Sz3D.Presentation.WPF.Tests`（`net10.0-windows`，xUnit），并经 `[InternalsVisibleTo]` 把 `RequiredIds`/`SerializeLayout`/`ApplyLayoutXml`/`CollectPresentIds` 由 `private` 调整为 `internal`；其中 `RequiredIds` 可纯反射校验（无需 DockingManager），而 `ApplyLayoutXml`/`AllRequiredPresent` 为实例方法、依赖 `MainWindow`/`DockingManager` 实例，需在 STA 线程构造或把纯逻辑抽成可测静态单元。此为后续独立工作项，不在本特性范围内。

### 2.10 实现清单（供实现步骤直接执行）

XAML（`MainWindow.xaml`，仅改 `LayoutRoot` 左列与中央 document 两处）：
1. 把左列 `LayoutAnchorablePane`（现 `DockMinWidth="180"`）内的单个「视图」`LayoutAnchorable` + `TabControl` 替换为两个 `LayoutAnchorable`（同一 pane 内，3D 在前）：
   - `Title="3D 视图" ContentId="panel.view3d" CanClose="False" CanHide="True"`（不写 `CanFloat`，默认 `true`），内容 = 原「3D 视图」`TabItem` 的 `Grid`（含 `Viewport3D`/`CloudVisual`/灯光/占位 + `DataTrigger`）。
   - `Title="2D 视图" ContentId="panel.view2d" CanClose="False" CanHide="True"`（不写 `CanFloat`，默认 `true`），内容 = 原「2D 视图」`TabItem` 的 `Grid`（含 `Image`/占位 + `DataTrigger`）。
   - 左列 pane 的 `DockMinWidth="180"` 保留在 `LayoutAnchorablePane` 上。
2. 中央 `LayoutDocument` 追加属性 `CanFloat="False"`（与现有 `CanClose="False"` 并列）。
3. 不在任何位置设置 `DockingManager.AllowFloatingWindows`（保持默认 `true`）。

code-behind（`MainWindow.xaml.cs`，按 §2.5 的 1–8 点）：常量、`RequiredIds`、缓存字段、`LoadLayout` 抓缓存 switch、①a null 校验、`ReattachContent` switch，以及全文件「四块」→「五块」注释措辞同步。

### 2.11 风险

- R-1（低）`CanFloat="False"` 在 5.0.1 对 `LayoutDocument` 的运行时效果已由 IL（基类属性 + `DockingManager`/`LayoutItem` 两处消费端）强证据支撑，但「拖出窗口是否 100% 不浮动」为交互行为，需人工冒烟最终确认（M-5）。若发现仍可浮动，退路是方案 (b)（改 `LayoutAnchorable` + `CanFloat="False"`），结构 delta 已在 §2.3 评估。
- R-2（低）旧 `layout.xml` 迁移导致用户一次性丢失自定义布局——已按 A-2 接受，回退默认是既有健壮行为；迁移时的 live 内容重挂安全性已在 §2.6 论证并以 M-8 冒烟兜底。

---

## 附：评审意见处理（针对 design-review.json / design-review.md，CHANGES_REQUESTED）

- **Finding 1（MEDIUM，测试策略引用不存在的 `RVS.Tests`）—— 已采纳方案 A。** §1.5 Out of Scope 增加「本特性不新增自动化测试项目」；§2.9 整段重写：删除 `RVS.Tests` 引用，明确仓库现状（仅 `Sz3D.Core.*.Tests`、无 WPF 层测试工程），改为「构建门禁 G-1 + 人工冒烟 M-1..M-8」。保留一段「若未来要补自动化测试」的方案 B 说明（新建 `Sz3D.Presentation.WPF.Tests` + `InternalsVisibleTo`），但明确标注不在本特性范围。
- **Finding 2（MEDIUM，构建命令/解决方案名与实仓不符）—— 已修正。** 全文件以 `Sz3D.sln` 替换 `RVS.sln`。AC-8 改为确定命令 `dotnet build d:\kiro\Sz3D\Sz3D.sln -c Debug`（无新增警告）；§2.9 构建门禁 G-1 同步。
- **Finding 3（MEDIUM，旧 layout.xml 迁移回退时 live 内容重挂安全性未论证）—— 已采纳论证 A 并在 §2.6 新增完整论证。** 逐步追踪 `_view3dHost`/`_view2dHost` 在「抓缓存→ApplyLayoutXml(旧 xml)→DockManager.Layout 替换→ResetToDefaultLayout→默认 XML 回放重挂」全过程的父状态轨迹，依据 AvalonDock `DockingManager.Layout` setter 卸下旧树的行为说明重挂时内容的旧活动可视父已随旧树卸下、不触发 WPF「已是另一元素子级」约束，并论证该轨迹与现状单面板回退路径同构、未引入新父冲突类别；同时增补 M-8 冒烟（仅含 `panel.view` 的旧 xml）作兜底，并写明若冒烟暴露冲突则启用论证 B（显式 detach）及其对 §2.5 的影响。不改代码。
- **Finding 4（MEDIUM，视图面板 CanFloat 未显式对齐、未排除误改 DockingManager 级开关）—— 已补充。** §2.2 明确两新面板「不写 `CanFloat`、采用默认 `true`」并提示不得误加 `False`；§2.3 新增「禁浮动实现范围约束」：仅算子图单个 `LayoutContent.CanFloat="False"`，`DockingManager.AllowFloatingWindows` 保持默认 `true` 不改；新增 INV-5；§2.4 结构草图与 delta、§2.10 实现清单第 3 条同步标注；AC-4 补「两面板 `CanFloat` 默认 true」、M-4 补「验证视图面板默认可浮动」。
- **Finding 5（MEDIUM，AC-1 初始 Tab 组与 AC-6 恢复用户布局语义冲突）—— 已澄清。** AC-1 限定为「首次启动或回退默认布局时」左列为一个 Tab 组，并括注用户拆分持久化后恢复自定义摆放；§2.2 增补一段「初始合并与恢复用户布局边界」说明；EC-1 补「不强制重新合并」。
- **Finding 6（NIT，注释/文案仍称「四块」）—— 已采纳。** §2.5 新增第 8 点「注释数量措辞同步」，要求全文件「四块」→「五块」（含字段区块注释、`RequiredIds` 注释、①a 文案等）；FR-5 末尾与 §2.10 code-behind 清单同步要求。
- **Finding 7（NIT，默认选中标签未明确）—— 已采纳。** FR-3 补「默认选中 3D 视图标签」；AC-1 明确「初始可见（选中）项为 3D 视图」；§2.2 说明依赖 `LayoutAnchorablePane` 默认选中首个子项（3D 声明在前），无需显式 `IsActive`/`IsSelected`。
