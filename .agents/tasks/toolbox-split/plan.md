# Implementation Plan — 工具箱四 Tab 拆分为独立可停靠面板

## 背景与目标

把右列「工具箱」面板内部的 **一个 `TabControl` + 四个 `TabItem`** 拆成 **四个独立的 `LayoutAnchorable`**（放在同一个 `LayoutAnchorablePane` 内，原生渲染为一个 Tab 组）。每个面板都可用自带的 X 按钮关闭（`CanHide="True"` → 实为隐藏，`IsHidden=true`，保留在布局树中），并能在「视图」菜单里逐个重新勾选显示——完全复刻现有 2D/3D 面板的行为。

## 探查结论（已读代码确认）

- `MainWindow.xaml`：右列为 `LayoutAnchorablePane DockWidth="300" DockMinWidth="240"` > 单个 `LayoutAnchorable Title="工具箱" ContentId="panel.toolbox" CanClose="False" CanHide="True"` > 一个 `TabControl Style="{StaticResource ToolboxTabControlStyle}" MinWidth="236"`，含四个 `TabItem`：属性面板（真绑定 `Editor.SelectedNode`/`Parameters`/`ParameterViewModel`）、算子树状图（占位 `TextBlock`）、算子列表（真绑定 `OperatorGroups`/`Items`，含「更新」按钮与「查找算子…」`TextBox`）、调试面板（占位 `TextBlock`）。
- `ToolboxTabControlStyle`/`ToolboxTabItemStyle`/`ToolboxTabStripPanel` 三个资源只在 `MainWindow.xaml` 的 `Window.Resources` 里定义，且只被这一个 `TabControl` 引用（全工程 `.xaml`/`.cs` 搜索确认，`Dark.xaml` 无引用）。拆分后这三个资源与 `TabControl`/`TabItem` 一并消失，成为死资源。
- `MainWindow.xaml.cs`：持久化白名单当前为 5 块（`IdView3D`/`IdView2D`/`IdGraph`/`IdToolbox`/`IdLog`），每块都 **既缓存（`LoadLayout` 步骤① switch + ①a null 断言）又重挂（`ReattachContent` switch）又列入 `RequiredIds`**，并在 `ResetToDefaultLayout` 的 detach 循环里出现。`CollectPresentIds()`/`AllRequiredPresent()` 无硬编码 ContentId，随 `RequiredIds` 自动适配。
- 面板切换用 `FindAnchorable(contentId)` / `IsPanelVisible(contentId)` / `TogglePanel(contentId)`，与具体面板无关，可直接复用。
- 「视图」菜单 `OnViewMenuOpened` 同步 `MenuPanelView3D`/`View2D` 的 `IsChecked`；`MenuPanelGraph` 恒勾选且禁用。
- 测试工程无任何对 `MainWindow`/`TogglePanel`/`RequiredIds` 的引用。本功能为 WPF 原地改动，验证手段 = 编译通过 + 手工布局保存/恢复回归（无自动化 UI 测试）。

## 关键风险推理（持久化白名单对齐）

拆分后承载 live XAML 内容的面板从 5 个变为 **8 个**：`panel.view3d`、`panel.view2d`、`panel.graph`、`panel.props`、`panel.optree`、`panel.oplist`、`panel.debug`、`panel.log`。

- **四个新面板全部携带 live 内容**：属性面板、算子列表显然有真实绑定子树；算子树状图、调试面板虽是占位 `TextBlock`，但同样是 XAML 建好的 UI 子树——反序列化会整体替换 `DockManager.Layout`，任何未被 `ReattachContent` 重挂的 `ContentId` 其 `.Content` 都会变空白。因此 **四个新 ContentId 必须同时满足：① `LoadLayout` 缓存、①a null 断言、`ReattachContent` 重挂、`ResetToDefaultLayout` detach 循环、`RequiredIds`** 五处全覆盖，缺一处就会在恢复后白屏或误判回退。
- **`RequiredIds` 正确性**：隐藏（`IsHidden=true`）的 anchorable 仍在布局树中（`Descendents()` 枚举得到），故 `CollectPresentIds()` 仍能收集到它 → 用户关掉某个工具箱面板后保存再启动，`AllRequiredPresent()` 不会误报缺失、不会回退默认。所以四个新面板列入 `RequiredIds` 是安全且必要的（它们必须始终存在于树中，才能被菜单 `Show()` 复位）。
- `IdToolbox`/`_toolboxHost` 彻底移除（不再有 `panel.toolbox` 面板），五处引用处同步删除。

## 实施步骤

- [ ] 1. 【XAML｜右列面板替换】在 `MainWindow.xaml` 中，把右列那个 `LayoutAnchorablePane`（保留 `DockWidth="300" DockMinWidth="240"`）内的「单个 `LayoutAnchorable Title="工具箱"` + `TabControl` + 四个 `TabItem`」整体替换为 **四个并列的 `LayoutAnchorable`**，逐字搬入各自原 `TabItem` 的内层内容：
      - `Title="属性面板" ContentId="panel.props" CanClose="False" CanHide="True" IsSelected="True" IsActive="True"` —— 内容 = 原「属性面板」`TabItem` 的 `<Grid>…</Grid>`（含占位 `TextBlock` 的 `DataTrigger` 到 `Editor.SelectedNode`、`ScrollViewer`>`StackPanel DataContext="{Binding Editor.SelectedNode}"`>`ItemsControl ItemsSource="{Binding Parameters}"` 的完整 `ParameterViewModel` 模板，全部逐字保留）。
      - `Title="算子树状图" ContentId="panel.optree" CanClose="False" CanHide="True"` —— 内容 = 原占位 `<TextBlock Text="算子树（占位）" .../>`。
      - `Title="算子列表" ContentId="panel.oplist" CanClose="False" CanHide="True"` —— 内容 = 原「算子列表」`TabItem` 的 `<DockPanel Margin="6">…</DockPanel>`（含「更新」`Button`、「查找算子…」`TextBox`、`ScrollViewer`>`ItemsControl ItemsSource="{Binding OperatorGroups}"`>`Expander`>`ItemsControl ItemsSource="{Binding Items}"`，逐字保留）。
      - `Title="调试面板" ContentId="panel.debug" CanClose="False" CanHide="True"` —— 内容 = 原占位 `<TextBlock Text="调试面板（占位）" .../>`。
      四块放在同一个 `LayoutAnchorablePane` 内即原生呈现为一个 Tab 组（与左列 3D/2D 同一机制）；移除包裹它们的 `TabControl`/`TabItem` 以及 `MinWidth="236"`。注意 `ContentId` 字符串与第 2 步常量严格一一对应。
      Files: `d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\MainWindow.xaml`
      Verify: 随第 6 步统一 `dotnet build d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\Sz3D.Presentation.WPF.csproj -c Debug` 通过（此步单独会因 XAML 内容已变、而 code-behind 仍含 `IdToolbox` 而编译不过，与 2–6 连做）；构建后目视确认四面板启动时合并为一个 Tab 组、默认选中「属性面板」。

- [ ] 2. 【code-behind｜ContentId 常量】在 `MainWindow.xaml.cs` 常量区，删除 `private const string IdToolbox = "panel.toolbox";`，新增四条：`IdProps = "panel.props"`、`IdOpTree = "panel.optree"`、`IdOpList = "panel.oplist"`、`IdDebug = "panel.debug"`（PascalCase，字符串与第 1 步 XAML 完全一致）。
      Files: `d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\MainWindow.xaml.cs`
      Verify: 随第 6 步统一构建。

- [ ] 3. 【code-behind｜RequiredIds + live 缓存字段】
      - 把 `RequiredIds` 由 `{ IdView3D, IdView2D, IdGraph, IdToolbox, IdLog }` 改为 `{ IdView3D, IdView2D, IdGraph, IdProps, IdOpTree, IdOpList, IdDebug, IdLog }`（8 项），并把其上方注释「§5：五块全部必需」改为「八块全部必需」。
      - live 缓存字段：删除 `private object? _toolboxHost;`，新增 `private object? _propsHost;`、`private object? _opTreeHost;`、`private object? _opListHost;`、`private object? _debugHost;`（下划线 camelCase）；字段区块注释「── 五块 live 内容缓存 …」改为「── 八块 live 内容缓存 …」。
      Files: `d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\MainWindow.xaml.cs`
      Verify: 随第 6 步统一构建。

- [ ] 4. 【code-behind｜LoadLayout 抓缓存 switch + ①a null 断言】
      - 步骤① 抓缓存 `switch`：删除 `case IdToolbox: _toolboxHost = lc.Content; break;`，新增 `case IdProps: _propsHost = lc.Content; break;`、`case IdOpTree: _opTreeHost = lc.Content; break;`、`case IdOpList: _opListHost = lc.Content; break;`、`case IdDebug: _debugHost = lc.Content; break;`。
      - 步骤①a null 校验：由 `… || _toolboxHost is null || …` 改为检查八块：`_view3dHost is null || _view2dHost is null || _graphHost is null || _propsHost is null || _opTreeHost is null || _opListHost is null || _debugHost is null || _logHost is null`；诊断日志文案相应更新为列出八块（文案仅诊断用途，非契约）。
      Files: `d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\MainWindow.xaml.cs`
      Verify: 随第 6 步统一构建。

- [ ] 5. 【code-behind｜ReattachContent switch + ResetToDefaultLayout detach 循环】
      - `ReattachContent()` 的 `lc.ContentId switch`：删除 `IdToolbox => _toolboxHost,`，新增 `IdProps => _propsHost,`、`IdOpTree => _opTreeHost,`、`IdOpList => _opListHost,`、`IdDebug => _debugHost,`（`IdView3D`/`IdView2D`/`IdGraph`/`IdLog`/`_ => null` 分支不变）。
      - `ResetToDefaultLayout()` 内的 detach 循环数组 `new[] { _view3dHost, _view2dHost, _graphHost, _toolboxHost, _logHost }` 改为 `new[] { _view3dHost, _view2dHost, _graphHost, _propsHost, _opTreeHost, _opListHost, _debugHost, _logHost }`。
      - `CollectPresentIds()` / `AllRequiredPresent()` 无硬编码 ContentId，随 `RequiredIds` 自动适配，无需改动（已核验）。
      Files: `d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\MainWindow.xaml.cs`
      Verify: 随第 6 步统一构建。

- [ ] 6. 【视图菜单 + Click 处理器】
      - XAML：在「视图」`MenuItem` 中，把现有 `MenuPanelView3D`/`MenuPanelView2D` 之后（或紧随 `MenuPanelGraph` 组内，保持 2D/3D/算子图 不变），新增四个可勾选菜单项：`x:Name="MenuPanelProps" Header="属性面板" IsCheckable="True" Click="OnTogglePanelProps"`、`MenuPanelOpTree Header="算子树状图" Click="OnTogglePanelOpTree"`、`MenuPanelOpList Header="算子列表" Click="OnTogglePanelOpList"`、`MenuPanelDebug Header="调试面板" Click="OnTogglePanelDebug"`（完全复刻 `MenuPanelView2D` 写法）。保留「窗口布局一~四」占位与 `Separator`，`MenuPanelGraph` 的禁用+恒勾选不动。
      - code-behind `OnViewMenuOpened`：在同步 3D/2D 勾选之后，追加 `MenuPanelProps.IsChecked = IsPanelVisible(IdProps);`、`MenuPanelOpTree.IsChecked = IsPanelVisible(IdOpTree);`、`MenuPanelOpList.IsChecked = IsPanelVisible(IdOpList);`、`MenuPanelDebug.IsChecked = IsPanelVisible(IdDebug);`。
      - code-behind 新增四个处理器（复刻 `OnTogglePanelView2D` 的单行表达式体）：`OnTogglePanelProps => TogglePanel(IdProps)`、`OnTogglePanelOpTree => TogglePanel(IdOpTree)`、`OnTogglePanelOpList => TogglePanel(IdOpList)`、`OnTogglePanelDebug => TogglePanel(IdDebug)`。`TogglePanel`/`FindAnchorable`/`IsPanelVisible` 复用不变。方法均 PascalCase；表达式体处理器与现有 `OnTogglePanelView2D` 风格一致，无需额外 XML 文档注释（与现有同类处理器保持一致）。
      Files: `d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\MainWindow.xaml`、`d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\MainWindow.xaml.cs`
      Verify: `dotnet build d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\Sz3D.Presentation.WPF.csproj -c Debug` 通过，无新增警告。

- [ ] 7. 【死资源清理】确认第 1 步后 `ToolboxTabControlStyle`/`ToolboxTabItemStyle`/`ToolboxTabStripPanel` 三个资源已无任何引用（已全工程搜索：仅 `MainWindow.xaml` 内定义+原 `TabControl` 使用；`Dark.xaml` 与其余文件无引用）。从 `MainWindow.xaml` 的 `Window.Resources` 中删除这三个资源定义及其上方「══ 工具箱 Tab：图漾风格 ══」注释块。**不要**删除仍被引用的其它资源（`OperatorDescriptor` 的 `DataTemplate`、节点样式等）。删除前再次用 grep 确认 `ToolboxTab` 无残留引用（应只剩 0 处）。
      Files: `d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\MainWindow.xaml`
      Verify: `dotnet build d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\Sz3D.Presentation.WPF.csproj -c Debug` 通过；`grep ToolboxTab` 结果为空；构建后目视确认四面板 Tab 组外观正常（AvalonDock VS2013Dark 原生 Tab 样式，不再依赖旧 ToolboxTab 样式）。

- [ ] 8. 【整体构建 + 手工回归】
      - `dotnet build d:\kiro\Sz3D\Sz3D.sln -c Debug` 整解决方案通过，无新增警告。
      - 手工回归（无自动化 UI 测试，按本仓既有验证路径）：启动 `Sz3D.Presentation.WPF`，确认：① 四面板启动合并为一个 Tab 组、默认选中「属性面板」，内容/绑定与拆分前一致；② 各面板 X 可独立关闭（隐藏）；③「视图」菜单逐个重新勾选可复位显示，勾选态与实际可见性同步；④ 关闭某个工具箱面板后关闭应用、重启——布局恢复且 **不回退默认**（`AllRequiredPresent` 对隐藏面板仍判为存在）；⑤ 删除 `./config/layout.xml` 重启不崩溃、呈现默认布局；⑥ 2D/3D/算子图/日志 行为不变。
      Files: （无改动，验证步）
      Verify: `dotnet build d:\kiro\Sz3D\Sz3D.sln -c Debug` 通过；上述手工回归全部符合预期。

## 约束与注意

- 不改动 2D/3D/算子图/日志 四个面板的行为；不触碰任何 ViewModel；四个原 Tab 的内层内容与绑定逐字保留。
- 四个新面板一律 `CanClose="False" CanHide="True"`（与 2D/3D 一致），X 触发 `Hide()`（保留在树中、`IsHidden=true`），菜单 `Show()` 可复位——这是 `FindAnchorable`/`TogglePanel`/持久化赖以工作的前提；**不得**用 `CanClose="True"`。
- 中文 XML 文档/行内注释风格与文件保持一致；注释中出现的「五块」计数同步改为「八块」以免误导。
