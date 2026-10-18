# Implementation Plan — 节点分组块 (Phase 1 最小可用)

## 背景与方法决策

用户核心诉求：在 Nodify 画布上实现图漾 RVS 式的"分组块"——一个带标题、可移动、可缩放的矩形块，渲染在节点**后面**，节点可以被拖进/拖出分组块（成员关系按几何包含判定），移动分组块时其成员节点一起平移。

### 关键探查结论（已验证，非假设）

- **Nodify 7.3.0 确实内置 `GroupingNode` 控件**（`d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\bin\Debug\net10.0-windows\Nodify.dll` 的程序集元数据中存在 `GroupingNode`、`GroupingMovementMode`、`CanResize`、`HeaderBrush`、`MovementMode`、`ActualSize`、`ResizeCompletedCommand` 等符号；官方 `docs/Nodes-Overview.md` 的 "2. The GroupingNode control" 一节有完整说明）。
- **`GroupingNode` 放在编辑器的 `ItemsSource` 层**（与节点同一个集合），被 `ItemContainer` 包裹，位置来自 `ItemContainer.Location`（与现有 `ItemContainerStyle` 的 `Location` 绑定完全一致）。
- **Z 序自动置后**：`GroupingNode` 静态构造函数里 `Panel.ZIndexProperty.OverrideMetadata(typeof(GroupingNode), new FrameworkPropertyMetadata(-1, ...))`，并把该 ZIndex 同步到其 `ItemContainer`。无需额外处理即可渲染在普通节点后面。
- **"移动分组块平移成员"是内置能力**：拖拽 `GroupingNode` 的 Header 时，它调用 `Editor.SelectArea(groupBounds)` 选中区域内的容器并整体移动（`MovementMode.Group`，默认值）；按住 Shift（`SwitchMovementMode`）临时切换为 `Self`（只移动自己）。因此"拖拽分组块平移内部节点"**不需要我们自己写几何平移逻辑**。
- **成员关系在 Nodify 中是隐式/几何的**：没有持久的 parent-child 结构，拖拽时靠 `SelectArea` 的矩形包含判定。这正好契合 Phase 1 "drag-complete 时按几何包含重算成员"的要求。
- **已有的右键单击删除模式**：`MainWindow.xaml.cs` 的 `OnNodeRightButtonDown`（`PreviewMouseRightButtonDown` → `node.DeleteCommand.Execute(null)` → `e.Handled = true`），在 `MainWindow.xaml` 中由 `<nodify:Node ... PreviewMouseRightButtonDown="OnNodeRightButtonDown"/>` 挂接。**此模式复用于分组，但不得改动节点版本。**
- `RelayCommand<T>` 定义于 `Commands\GraphCommands.cs`；`ViewModelBase.Set/OnPropertyChanged` 提供属性变更；`Result`/`Result<T>` 在 `Sz3D.Core.Infrastructure.Common`。
- `NodeViewModel.Location` 是 `System.Windows.Point`，setter 回写 `Model.X/Y`。`GroupViewModel` 同样可用 `System.Windows.Point`/`Size`（符合约定，核心 `GroupModel` 则不依赖 WPF）。
- 基线：`dotnet build Sz3D.sln` 绿；`Sz3D.Core.Infrastructure.Tests` 6 个测试全通过。

### 决策：采用内置 `GroupingNode`（而非 Border 装饰器回退）

理由：GroupingNode 一次性满足"置后渲染（ZIndex=-1）＋可缩放（CanResize）＋拖 Header 整体平移成员（SelectArea+Group 模式）"三项核心需求，交互手感与图漾 RVS 一致，且是库官方、MVVM 友好的控件。Border 装饰器回退需要手写缩放 Thumb、Z 序、以及拖拽时的成员平移，重复造轮子且交互更差。**唯一需要我们手写的只有"持久成员关系按几何包含重算"**（供模型/DTO 往返与查询），这是纯几何逻辑，放核心库单元测试。

### 架构影响：异构 ItemsSource

现状 `NodifyEditor.ItemsSource="{Binding Nodes}"` 绑定到同质的 `ObservableCollection<NodeViewModel>`，并有一个 `DataTemplate DataType="NodeViewModel"` 和一个 `ItemContainerStyle`（`Location`/`IsSelected` 双向绑定）。加入分组后，items 层需要**同时容纳节点与分组**。采用方案：引入一个 `CanvasItemViewModel` 抽象基类（含 `Point Location`、`bool IsSelected`、`ICommand DeleteCommand`），`NodeViewModel` 与新 `GroupViewModel` 都继承它；`NodifyEditor.ItemsSource` 改绑一个异构集合 `ObservableCollection<CanvasItemViewModel> Items`（节点与分组都加入其中），`ItemContainerStyle` 的 `Location`/`IsSelected` 绑定对两者都成立，再用两个 `DataTemplate`（`NodeViewModel`→`nodify:Node`，`GroupViewModel`→`nodify:GroupingNode`）按类型分流。

> needs-verification-at-implementation：异构 `ItemsSource` + 基于 `DataType` 的 `DataTemplate` 分流在本工程的 Nodify 7.3.0 下的实际渲染/选中/拖拽手感；GroupingNode 的 Header 拖拽平移成员、Shift 切换、resize 的视觉效果——这些属于运行时交互，需在实现期跑起应用肉眼确认（本步骤只做静态验证：build 绿 + 核心单测通过）。

---

## 实施步骤（按依赖排序，每步结束后代码可编译）

- [ ] 1. 新建核心 `GroupModel`（无 WPF 依赖）。
      记录 `Guid Id`、`string Title`、`double X/Y`（左上角画布坐标）、`double Width/Height`、`string Color`（header 颜色，形如 "#0E639C"）。镜像 `NodeModel` 风格（`Id`/只读 init、可变几何字段）。公共类型/成员加 XML 文档注释。
      文件：`d:\kiro\Sz3D\src\Sz3D.Core.Infrastructure\Graph\GroupModel.cs`
      验证：`dotnet build d:\kiro\Sz3D\src\Sz3D.Core.Infrastructure\Sz3D.Core.Infrastructure.csproj` 成功。

- [ ] 2. 在 `GraphModel` 中加入 `ObservableCollection<GroupModel> Groups`，并提供 `AddGroup(GroupModel)`→`Result`、`RemoveGroup(Guid)`→`Result`（仅删分组块本身，**不删成员节点**），镜像现有 `AddNode`/`RemoveNode` 的签名与 `Result` 返回风格（`ArgumentNullException.ThrowIfNull`，找不到时 `Result.Fail`）。
      文件：`d:\kiro\Sz3D\src\Sz3D.Core.Infrastructure\Graph\GraphModel.cs`
      验证：`dotnet build .../Sz3D.Core.Infrastructure.csproj` 成功。

- [ ] 3. 新建纯几何/成员判定帮助类 `GroupMembership`（无 WPF 依赖，供单元测试）。
      提供静态方法：`bool Contains(GroupModel group, double nodeX, double nodeY)`（节点左上角点是否落在分组矩形 `[X, X+Width] × [Y, Y+Height]` 内，边界含/不含在 XML 注释中写明——建议闭区间包含边界）；`IReadOnlyList<NodeModel> MembersOf(GroupModel group, IEnumerable<NodeModel> nodes)`；`GroupModel? GroupContaining(IEnumerable<GroupModel> groups, double nodeX, double nodeY)`（多个重叠时返回面积最小者，判定规则在注释中说明）。用集合表达式/模式匹配/`var`。
      文件：`d:\kiro\Sz3D\src\Sz3D.Core.Infrastructure\Graph\GroupMembership.cs`
      验证：`dotnet build .../Sz3D.Core.Infrastructure.csproj` 成功。

- [ ] 4. 为分组添加 DTO 持久化管线（仅 DTO/模型，不接 .rvsp 存取）。
      在 `ProjectDtos.cs` 新增 `GroupDto`（`Guid Id`、`string Title`、`double X`、`double Y`、`double Width`、`double Height`、`string Color`，`sealed record`，与 `NodeDto` 同风格），并给 `GraphDto` 增加 `List<GroupDto> Groups { get; init; } = []`。保持 `System.Text.Json`，不引 Newtonsoft。
      文件：`d:\kiro\Sz3D\src\Sz3D.Core.Infrastructure\Models\ProjectDtos.cs`
      验证：`dotnet build .../Sz3D.Core.Infrastructure.csproj` 成功。

- [ ] 5. 为核心几何/成员逻辑与 Groups 集合添加 xUnit 单元测试。
      覆盖：`Contains` 命中/未命中/边界；`MembersOf` 多节点筛选；`GroupContaining` 无/单/多重叠（返回最小面积）；`GraphModel.AddGroup` 增计数、`RemoveGroup` 删分组但**保留成员节点**、删不存在的分组返回 `Result.Fail`。镜像 `GraphModelTests.cs` 的 `[Fact]`/`Assert` 风格，放新文件。
      文件：`d:\kiro\Sz3D\tests\Sz3D.Core.Infrastructure.Tests\GroupModelTests.cs`（可选再加 `GroupMembershipTests.cs`）
      验证：`dotnet test d:\kiro\Sz3D\tests\Sz3D.Core.Infrastructure.Tests\Sz3D.Core.Infrastructure.Tests.csproj` —— 新增测试全部通过，原有 6 个不回归。

- [ ] 6. 引入画布项抽象基类 `CanvasItemViewModel`，并让 `NodeViewModel` 继承它。
      `CanvasItemViewModel : ViewModelBase` 暴露 `Point Location`（virtual/带 `OnLocationChanged` 钩子）、`bool IsSelected`、`ICommand DeleteCommand`。重构 `NodeViewModel` 继承该基类：把现有 `Location`（含回写 `Model.X/Y`）、`IsSelected`、`DeleteCommand` 上提或覆写；**保持对外行为与现有绑定不变**。ViewModel 不得引用 `System.Windows.Controls`。
      文件：新增 `d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\ViewModels\CanvasItemViewModel.cs`；修改 `NodeEditorViewModel.cs`（`NodeViewModel`）。
      验证：`dotnet build d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\Sz3D.Presentation.WPF.csproj` 成功。

- [ ] 7. 新建 `GroupViewModel : CanvasItemViewModel`，包装 `GroupModel`。
      暴露 `GroupModel Model`、`string Title`、`Point Location`（setter 回写 `Model.X/Y`）、`Size Size`（回写 `Model.Width/Height`，供 `GroupingNode.ActualSize` 双向绑定）、`string Color`（header 画刷来源）。使用 `System.Windows.Point`/`Size`（允许）。公共成员加 XML 注释。
      文件：`d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\ViewModels\GroupViewModel.cs`
      验证：`dotnet build .../Sz3D.Presentation.WPF.csproj` 成功。

- [ ] 8. 在 `NodeEditorViewModel` 中引入异构 `Items` 集合与分组命令、成员重算。
      - 新增 `ObservableCollection<CanvasItemViewModel> Items`，节点与分组都加入它（`AddOperator` 里把节点同时加入 `Items`；保留原 `Nodes` 供执行/连线逻辑使用，或让 `Nodes` 成为按类型过滤的视图——择一，注释说明选择理由）。
      - 新增 `ObservableCollection<GroupViewModel> Groups`。
      - 新增 `ICommand CreateGroupCommand`（`RelayCommand<object>`），在视图中心或 (0,0) 以默认尺寸（如 300×200）新建 `GroupModel`+`GroupViewModel`，`_graph.AddGroup(...)`，加入 `Items`/`Groups`；`DeleteGroupCommand`/`DeleteGroup(GroupViewModel)`：`_graph.RemoveGroup(id)`、从 `Items`/`Groups` 移除，**不动成员节点**，`GroupViewModel.DeleteCommand` 注入该命令（镜像 `AddOperator` 里给 `NodeViewModel.DeleteCommand` 注入的写法）。
      - 新增 `RecomputeMembership()`（或 `OnNodeDragCompleted`）：拖拽完成后用 `GroupMembership` 重算每个节点归属（Phase 1 可仅更新内部映射/用于后续持久化；"移动分组平移成员"由 GroupingNode 内置处理，不在此重复实现）。命名明确供单测的几何函数仍是第 3 步的 `GroupMembership`。
      文件：`d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\ViewModels\NodeEditorViewModel.cs`
      验证：`dotnet build .../Sz3D.Presentation.WPF.csproj` 成功。

- [ ] 9. XAML 渲染：编辑器改绑异构 `Items`，加入 `GroupingNode` 模板与分组选中/位置样式。
      - `MainWindow.xaml` 中 `nodify:NodifyEditor` 的 `ItemsSource` 从 `{Binding Nodes}` 改为 `{Binding Items}`。
      - 保留现有 `NodeViewModel` 的 `DataTemplate`（含 `OnNodeRightButtonDown`，**不改**）。
      - 新增 `DataTemplate DataType="{x:Type vm:GroupViewModel}"` → `<nodify:GroupingNode Header="{Binding Title}" ActualSize="{Binding Size, Mode=TwoWay}" HeaderBrush="..." CanResize="True" PreviewMouseRightButtonDown="OnGroupRightButtonDown"/>`（颜色用主题画刷或绑定 `Color`）。
      - `ItemContainerStyle` 的 `Location`/`IsSelected` 对两种 VM 均适用（都继承 `CanvasItemViewModel`），无需拆分；GroupingNode 的 ZIndex=-1 自动置后，节点在上。
      - 新建分组入口：在顶部工具栏（`MainWindow.xaml` 的底部 DockPanel 按钮区，"删除选中"附近）加一个 `Button Content="新建分组" Command="{Binding Editor.CreateGroupCommand}"`。
      文件：`d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\MainWindow.xaml`
      验证：`dotnet build .../Sz3D.Presentation.WPF.csproj` 成功（XAML 编译通过）。

- [ ] 10. 右键单击删除分组：复用节点删除模式新增 `OnGroupRightButtonDown`。
      在 `MainWindow.xaml.cs` 新增 `OnGroupRightButtonDown`，与 `OnNodeRightButtonDown` 同构：`sender is FrameworkElement { DataContext: GroupViewModel g }` → `g.DeleteCommand.Execute(null)` → `e.Handled = true`。**不得改动 `OnNodeRightButtonDown`。** 第 9 步的 GroupingNode 模板已挂 `PreviewMouseRightButtonDown="OnGroupRightButtonDown"`。
      文件：`d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\MainWindow.xaml.cs`
      验证：`dotnet build d:\kiro\Sz3D\Sz3D.sln` 成功；随后 `dotnet test .../Sz3D.Core.Infrastructure.Tests...` 全绿（整体集成验证）。

## 不在范围内（不要做）
嵌套分组、按成员自动调整大小、折叠/展开、完整 .rvsp 存取接线（仅 DTO）。

## 约束 / 不要动
- 不改本次会话已修的两处：`Dark.xaml` 的 MenuItem 模板（菜单字体可读性）、节点右键单击删除（`OnNodeRightButtonDown` + `MainWindow.xaml` 里 `nodify:Node` 的 `PreviewMouseRightButtonDown`）。
- 核心 `GroupModel`/`GroupMembership` 不得依赖 WPF/`System.Windows.Controls`。
- 直接在仓库工作，不用 worktree。
