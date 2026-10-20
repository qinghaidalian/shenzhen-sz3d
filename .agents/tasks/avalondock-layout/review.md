# AvalonDock 布局重构：2D/3D 面板拆分 + 算子图禁浮动

把原「视图」单面板里的 3D/2D 两个 TabItem 拆成同一 `LayoutAnchorablePane` 内的两个独立 `LayoutAnchorable`（初始合并为一个 Tab 组、默认选中 3D），让它们各自可浮动/停靠/再合并；同时给中央算子图 `LayoutDocument` 加 `CanFloat="False"` 禁止飞出成独立窗口但保留停靠区内拖动。`.cs` 侧把布局持久化白名单由单一 `panel.view` 扩成 `panel.view3d`/`panel.view2d` 两套，并在回退默认布局时对五块 live host 显式摘除逻辑父以消除跨树父冲突。本次为评审循环第二轮：第一轮唯一 blocking（AvalonDock 包引用未进提交、提交不自包含）已在 commit `6031ffb` 修复。

Watch for: (1) csproj 新增两条 AvalonDock 包引用，字面越出「仅两文件」约束——但这是第一轮 blocking 的既定修复，且因真实基线 `ab974c1` 根本无 AvalonDock，本特性客观上无法在不动 csproj 的前提下编译（confirmed，非阻断，见下）。(2) 评审基线 `ab974c1` 是纯 `Grid`+`TabControl` 版本，从无 `panel.view` 中间态，故逐条判据按「最终态」核对而非「删除 diff」核对（confirmed，非问题）。

**Verdict**: APPROVED

## High-level view

左列拆分已就位：同一 `LayoutAnchorablePane DockMinWidth="180"` 内两个独立 `LayoutAnchorable`，3D 声明在前并显式 `IsSelected="True" IsActive="True"`（强制要求①），两面板都不写 `CanFloat`（默认 true，可浮动），`CanClose="False" CanHide="True"`。3D/2D 的可视子树（Viewport3D/CloudVisual/灯光组/占位 + DataTrigger；Image/占位 + DataTrigger；均 `#111214`）逐字迁入，`x:Name` 各仅一次。

算子图禁浮动的实现范围精确收敛在中央 `LayoutDocument` 单个 `CanFloat="False"` 上，`CanClose="False"` 与 `ContentId="panel.graph"` 未变，整棵 `NodifyEditor` 子树未动；全文件无任何 `DockingManager.AllowFloatingWindows` 设置，故视图面板浮动能力不被连带禁掉（强制要求的范围约束成立）。

code-behind 白名单与最终态一致：常量为 `IdView3D`/`IdView2D`（无 `IdView` 残留），`RequiredIds` 五项，缓存字段 `_view3dHost`/`_view2dHost`（无 `_viewHost`），抓缓存 switch、①a null 校验、`ReattachContent` switch 三处均覆盖两个新 ContentId，全文件无 `panel.view`/`四块` 残留。

强制要求②已落实：`ResetToDefaultLayout()` 在 `DockManager.Layout = new LayoutRoot();` 之后、`ApplyLayoutXml(_defaultLayoutXml);` 之前，对五块 live host 做 `FrameworkElement.Parent is ContentControl ⇒ Content=null` 的显式摘除，带注释说明用途，`using System.Windows`/`System.Windows.Controls` 均已就位。

csproj 的两条 AvalonDock 包引用（精确固定 5.0.1）字面越出两文件约束，但这是第一轮唯一 blocking 的既定修复，且真实基线无 AvalonDock 导致不动 csproj 就无法编译自包含——判定为非阻断，详见下。

构建与运行时证据齐备：verification.md 记录了 `dotnet build Sz3D.sln -c Debug` 通过（唯一警告 CS9113 为未触及项目的既存警告，非新增）、删 layout.xml 后默认布局启动不崩溃、仅含 `panel.view` 旧版 layout.xml 迁移回退启动不崩溃且无 WPF 父冲突（仅记一条预期回退原因）。按指示未重跑这些命令。

<details>
<summary>Issues (1)</summary>

1. **csproj 越出两文件约束（非阻断）** — `Sz3D.Presentation.WPF.csproj` 新增两条 AvalonDock 包引用（`Dirkster.AvalonDock` 5.0.1、`Dirkster.AvalonDock.Themes.VS2013` 5.0.1）。字面上违反「仅改两文件」，但这是第一轮 blocking 的既定修复，且真实基线无 AvalonDock 导致不动 csproj 就无法编译；第一轮已将其交编排者裁定。无需进一步动作，除非编排者要求把 AvalonDock 依赖改由集中包管理（Directory.Build.props/Directory.Packages.props）承载。

</details>

<details>
<summary>Details</summary>

### 评审基线与「删除 diff」判据的解释

被评审 diff 的基线是仓库首提交 `ab974c1`，其 `MainWindow.xaml` 是纯 `Grid`+两个 `TabControl`（355 行、无 AvalonDock、无任何 `panel.*` ContentId），`MainWindow.xaml.cs` 仅 50 行、无任何布局持久化框架。换言之，design.md §1.1 所述的「现状：单个 `panel.view` + 内嵌 TabControl」这一中间态从未出现在已提交历史里——整套 AvalonDock 布局与持久化框架都是本特性两次提交（`6dd1a89` + `6031ffb`）一次性引入的。

因此判据 1/7/8 中以「删除 `panel.view` 单面板」「删 `IdView`」「`四块`→`五块`」形式表达的 delta，无法在 diff 里看到对应的删除行（因为那些符号在基线里本就不存在）。核对改为验证**最终态**是否等价于判据描述的目标结构，并额外确认不该存在的符号（`panel.view`/`IdView`/`_viewHost`/`四块`/`AllowFloatingWindows`）在最终文件里 0 残留。两类核对均通过。

### 左列拆分与初始 Tab 组（判据 1/2/3/6）

左列 `LayoutAnchorablePane DockMinWidth="180"` 内按顺序声明两个独立 `LayoutAnchorable`：先 3D（`ContentId="panel.view3d"`）后 2D（`ContentId="panel.view2d"`），同处一个 pane 即渲染为一个 Tab 组。两面板均 `CanClose="False" CanHide="True"`，均未出现 `CanFloat`（取默认 true）。grep 对 `CanFloat` 在 XAML 只命中三处：3D/2D 两段注释「CanFloat 默认 true」与算子图上唯一的 `CanFloat="False"`——确认视图面板未被误加 `CanFloat="False"`。

强制要求① 落实：3D 的 `LayoutAnchorable` 显式写了 `IsSelected="True" IsActive="True"`，不依赖 design §2.2 原先设想的「pane 默认选首项」。

3D 面板内容：`Grid Background="#111214"` → `Viewport3D x:Name="CloudViewport"`（`PerspectiveCamera` + `ModelVisual3D x:Name="CloudVisual"` + 灯光 `Model3DGroup`：`AmbientLight #888888`、`DirectionalLight #FFFFFF Direction="-1 -1 -1"`）+「无点云输出」`TextBlock`（`DataTrigger` 绑定 `Editor.HasCloudOutput`）。2D 面板内容：`Grid Background="#111214"` → `Image Source="{Binding Editor.ImageOutput}" Stretch="Uniform"` +「无图像输出」`TextBlock`（`DataTrigger` 绑定 `Editor.HasImageOutput`）。`CloudViewport`/`CloudVisual` 各仅出现一次，`UpdateCloudVisual()` 的 `CloudVisual.Content = ...` 赋值路径不变。

### 算子图禁浮动范围（判据 4/5）

中央 `LayoutDocumentPaneGroup` → `LayoutDocumentPane DockMinWidth="300"` → `LayoutDocument Title="算子图" ContentId="panel.graph" CanClose="False" CanFloat="False"`。`ContentId`、`CanClose` 未变，仅新增 `CanFloat="False"`。整棵 `nodify:NodifyEditor x:Name="Editor"`（Resources/两个 DataTemplate/两个连接点 Style/InputBindings/`Drop="OnDropOperator"`/全部绑定）逐字保留。

禁浮动范围精确：全文件无 `DockingManager.AllowFloatingWindows`（保持默认 true），故 2D/3D/工具箱/日志面板的浮动能力不被连带禁掉。`CanFloat` 与 `CanMove` 独立，算子图仍可停靠区内拖动——与 design §2.3 的 IL 核验结论一致。

### 持久化白名单对齐（判据 7）

常量区删了 `IdView`、加了 `IdView3D="panel.view3d"`/`IdView2D="panel.view2d"`，`IdGraph`/`IdToolbox`/`IdLog` 未动。`RequiredIds` 为 `{ IdView3D, IdView2D, IdGraph, IdToolbox, IdLog }`（5 项）。缓存字段删 `_viewHost`、加 `_view3dHost`/`_view2dHost`。三处走查——`LoadLayout` 抓缓存 `switch`、①a null 校验（`_view3dHost is null || _view2dHost is null || _graphHost is null || _toolboxHost is null || _logHost is null`）、`ReattachContent` 的 `lc.ContentId switch`——均覆盖两个新 ContentId，`_ => null` 分支保留。`CollectPresentIds()`/`AllRequiredPresent()` 无硬编码，随 `RequiredIds` 自动适配。字段/常量命名符合下划线 camelCase 与 PascalCase，公共行为方法保留 XML 文档注释，赋值处用 `var`。

### 强制要求②：回退时显式摘除逻辑父（判据 9）

`ResetToDefaultLayout()` 的 `try` 块内，次序为：`DockManager.Layout = new LayoutRoot();` → 对 `new[] { _view3dHost, _view2dHost, _graphHost, _toolboxHost, _logHost }` 逐个 `if (host is FrameworkElement fe && fe.Parent is ContentControl cc) cc.Content = null;` → `ApplyLayoutXml(_defaultLayoutXml);`。插入位置正确（①之后、②之前），覆盖全部五块 host，带注释说明「消除跨树残留父冲突，避免『元素已是另一元素的子级』异常」。文件顶部 `using System.Windows;`（`FrameworkElement`）与 `using System.Windows.Controls;`（`ContentControl`）均在。`ResetToDefaultLayout()` 的其余逻辑（`_defaultLayoutXml is null` 守卫、③ 回放后校验、catch 记日志不递归回退）完整。

### 不回归（判据 10）

顶部 chrome（菜单/工具栏/状态/日志 DataGrid）、`OnOperatorMouseDown/Move/OnDropOperator`、`UpdateCloudVisual`、`CrashLogger`、`SerializeLayout`/`ApplyLayoutXml`/`DetachFromCurrentParent`/`EnumerateContents`/`CollectPresentIds`/`AllRequiredPresent`/`SaveLayout`/`DtoSerializer`、构造函数 Loaded/Closing 钩子、右列工具箱四 Tab、底部日志、`DockingManager.Theme`（Vs2013DarkTheme）均未改。注释措辞已全量「四块→五块」（`RequiredIds` 上方、字段区块、`LoadLayout` 文档/行内、`AllRequiredPresent` 文档），grep `四块` 在两文件 0 命中。

### csproj 越界（判据：仅两文件约束）

diff 命中四个文件：`verification.md`（豁免）、`MainWindow.xaml`（允许）、`MainWindow.xaml.cs`（允许）、`Sz3D.Presentation.WPF.csproj`（+2 行：`Dirkster.AvalonDock` 5.0.1、`Dirkster.AvalonDock.Themes.VS2013` 5.0.1，均精确固定版本）。csproj 不在 verification.md/plan.md/layout.xml 豁免集内，字面构成越界。

但这是第一轮评审（review.json，CHANGES_REQUESTED）唯一 blocking finding 的既定修复：第一轮指出两 MainWindow 文件硬依赖 AvalonDock 命名空间与程序集，而这两条包引用当时仅以未提交工作区改动存在，导致提交 `6dd1a89` 不自包含、隔离构建会因缺程序集而失败；且基线 `ab974c1` 的 csproj 仅有 Nodify、无集中包管理，本特性客观上无法在「只改两文件」的前提下编译。第一轮已明确把「是否接受该 csproj 改动」交编排者裁定。本轮据此判定：把它重新作为 blocking 会使评审循环无法收敛（修与不修都 blocking），且该改动是最小、必要、版本固定的依赖补齐，无副作用。故记为非阻断 finding。

### 构建与运行时验证证据（判据 11）

verification.md 记录充分且按指示不重跑：

- 构建门禁 G-1：`dotnet build Sz3D.sln -c Debug` 通过；唯一警告为 `StepExecutionController.cs(6,61) CS9113 参数"engine"未读`，位于本特性未触及的 `Sz3D.Core.Infrastructure`，系既存、非新增。两 MainWindow 文件与 csproj 无告警。
- 默认布局冒烟（M-1/M-7）：删 `config/layout.xml` 后后台启动 exe，进程 ALIVE、无 `crash_*.log`。
- 迁移回退冒烟（M-8，强制要求② 兜底）：放置仅含 `panel.view` 的旧版 `layout.xml` 后启动，进程 ALIVE，崩溃日志仅一条预期的「恢复后必需面板缺失…回退默认布局」`InvalidOperationException`，**非** WPF 父冲突异常——证明五块 live host 显式摘除逻辑父生效。

</details>

<details>
<summary>File map</summary>

- `src/Sz3D.Presentation.WPF/MainWindow.xaml` — 左列拆成两个独立 `LayoutAnchorable`（3D 前含显式 `IsSelected/IsActive`、2D 后，均不写 CanFloat）；中央 `LayoutDocument` 加 `CanFloat="False"`。
- `src/Sz3D.Presentation.WPF/MainWindow.xaml.cs` — 常量/`RequiredIds`/缓存字段/两处 switch/①a null 校验切到 view3d+view2d 两套；`ResetToDefaultLayout()` 加五块 host 显式摘除逻辑父；注释「四块→五块」。
- `src/Sz3D.Presentation.WPF/Sz3D.Presentation.WPF.csproj` — 新增两条 AvalonDock 包引用（修复第一轮「提交不自包含」blocking；字面越出两文件约束，非阻断）。
- `.agents/tasks/avalondock-layout/verification.md` — 自验证记录（构建门禁 + 两项运行时冒烟），豁免文件。

完整 diff：`git -C d:\kiro\Sz3D diff ab974c1 HEAD`。

</details>
