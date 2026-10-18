# 实现计划：拖拽分组块时带动内部成员节点一起移动

## 背景与缺陷

在 Nodify 7.3.0 画布上拖拽分组块（`GroupingNode`）时，块内成员节点不随之移动。目标：整块区域（分组 + 其几何范围内的成员节点）一起平移，像图漾 RVS 一样，并把成员新坐标持久化回 `NodeModel.X/Y`。

本次为**定向行为修复**，不重构，保持与现有分组功能内聚。

---

## Nodify 7.3.0 API 调查结论（已核实源码，非假设）

核实对象：
- 已安装程序集 XML：`C:\Users\aisha\.nuget\packages\nodify\7.3.0\lib\net10.0-windows7.0\Nodify.xml`
- 源码（tag v7.3.0）：`Nodify/Nodes/GroupingNode.cs`

确认的 API：
- 枚举 **`Nodify.GroupingMovementMode`**，成员 **`Group`**（移动时带动内容）与 **`Self`**（只移动自身，不带内容）。
- 依赖属性 **`GroupingNode.MovementMode`**（类型 `GroupingMovementMode`），**默认值为 `Group`**（源码 `MovementModeProperty` 用 `GroupMovementBoxed = GroupingMovementMode.Group` 注册）。
- 内置 `Group` 模式的工作机制：`GroupingNode.OnHeaderMouseDown` 在拖拽 header 时，用 `Container.Location + RenderSize` 组成 `groupBounds`，调用 **`NodifyEditor.SelectArea(groupBounds, append, fit:true)`** 把矩形内的 `ItemContainer` 全部选中，随后由编辑器的多选拖拽把整组一起移动；每个被移动的 `ItemContainer.Location` 通过 `ItemContainerStyle` 的 `Location` 双向绑定回写到各自 VM。
- `ItemContainer.Location` 为可双向绑定（分组自身当前能持久化移动即证明该路径成立）。
- 编辑器级拖拽命令 **`NodifyEditor.ItemsDragStartedCommand`** 与 **`NodifyEditor.ItemsDragCompletedCommand`**：在 `SelectedContainers` 的拖拽开始/结束时触发——作为“拖拽开始/结束”的可靠钩子。

### 选择 A 还是 B —— 选 **B**（在视图模型中按 delta 平移成员），理由：

- 内置 `Group` 模式（A）依赖**运行时命中测试 + 区域选择**：它在 header 按下瞬间按 `groupBounds` 选中容器。在当前这个**异构数据绑定编辑器**（`Items` 同时含节点与分组）里，这条路径对“哪些容器被选中、selection 的时序、`DesiredSizeForSelection` 对边界的影响”高度敏感，正是本缺陷现状（分组能动、成员不动）所暴露的不可靠点，且**无法用无 UI 的单测覆盖**。
- 路径 B 把“按 delta 平移成员坐标”做成**无 WPF 依赖的纯函数**（给定旧/新分组原点与成员集，算出成员新坐标），放在 `Sz3D.Core.Infrastructure`，可用 xUnit 稳定覆盖；成员集在**拖拽开始瞬间**用现有 `GroupMembership` 几何捕获快照（而非持续重算），避免边缘节点被中途甩出。满足需求 1/6 的持久化与可测试性要求。
- 为避免内置 `Group` 选择式移动与 B 的 VM 平移**双重移动**冲突，本方案把 `GroupingNode.MovementMode` 显式设为 **`Self`**：分组 header 拖拽只移动分组自身容器，成员平移完全由 VM 驱动。

> 注：`RecomputeMembership` 里“分组移动时平移成员由 GroupingNode 内置负责，此处不重复实现”的注释是**错误假设**，本计划将其纠正。

---

## 设计概述

1. 新增**纯逻辑**（无 WPF）：`Sz3D.Core.Infrastructure.Graph.GroupTranslation`，提供按 delta 计算成员新坐标的纯方法 + 直接把平移应用到 `NodeModel` 集合的方法。
2. `GroupViewModel` 暴露“拖拽起点”状态并在 `Location` 变化时产出 delta，但**不自行引用其它 VM**；由 `NodeEditorViewModel` 作为协调者持有节点集合并执行平移。
3. `NodeEditorViewModel` 新增两个命令绑定到编辑器的 `ItemsDragStartedCommand`/`ItemsDragCompletedCommand`：
   - 拖拽开始：对每个被拖动的 `GroupViewModel`，用 `GroupMembership.MembersOf` 对**当前**节点几何快照成员集，并记录分组起始原点。
   - 拖拽过程：`GroupViewModel.Location` 变更 → 计算 delta → 用 `GroupTranslation` 把 delta 施加到快照成员的 `NodeViewModel.Location`（经其 `OnLocationChanged` 回写 `NodeModel.X/Y`）。
   - 拖拽结束：清空快照，调用 `RecomputeMembership` 刷新归属映射。
4. XAML：`GroupingNode` 模板设 `MovementMode="Self"`；`NodifyEditor` 绑定两个拖拽命令。

保持不变：节点/分组右键 ContextMenu 删除、Dark.xaml MenuItem 模板、`ImageProcessingNodes.cs` 的 12 个 2D 算子、`GroupModel/GraphModel/GroupMembership` 的无 WPF 约束、ZIndex=-1 分组渲染在节点之后（Nodify 内置，不动）。

---

## 分步实现清单

- [ ] 1. 新增纯逻辑类型 `GroupTranslation`（无 WPF 依赖）。
      提供两个方法：`static (double X, double Y) Translate(double x, double y, double dx, double dy)`（给定点与 delta 返回新坐标，纯函数）；`static void ApplyDelta(IEnumerable<NodeModel> members, double dx, double dy)`（对每个成员的 `X/Y` 施加 delta，就地回写模型）。方法含 XML 文档注释，用集合表达式/模式匹配。
      Files: `d:\kiro\Sz3D\src\Sz3D.Core.Infrastructure\Graph\GroupTranslation.cs`
      Verify: `dotnet build d:\kiro\Sz3D\Sz3D.sln -c Debug` 成功（新文件编译通过）。

- [ ] 2. 为 `GroupTranslation` 新增 xUnit 单元测试。
      覆盖：正向/负向 delta 的 `Translate` 返回值；`ApplyDelta` 把每个成员 `X/Y` 按同一 delta 平移；空成员集为无操作（不抛异常）；delta 为 0 时坐标不变。镜像现有 `GroupModelTests` 风格（`[Fact]`、`Assert`、中文注释分节）。
      Files: `d:\kiro\Sz3D\tests\Sz3D.Core.Infrastructure.Tests\GroupTranslationTests.cs`
      Verify: `dotnet test d:\kiro\Sz3D\tests\Sz3D.Core.Infrastructure.Tests` —— 全绿，新测试通过（原 16 个测试不回归）。

- [ ] 3. 在 `GroupViewModel` 增加“拖拽起点原点”字段与读取。
      新增 `internal` 可空字段 `_dragOrigin`（`Point?`，`_camelCase`），以及 `internal` 方法 `BeginDrag()`（记录当前 `Location` 为起点）和 `EndDrag()`（清空起点），以及 `internal Point? DragOrigin => _dragOrigin;`。不引用任何 `System.Windows.Controls` 类型（`System.Windows.Point` 允许）。公共/内部成员加 XML 文档注释。
      Files: `d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\ViewModels\GroupViewModel.cs`
      Verify: `dotnet build d:\kiro\Sz3D\Sz3D.sln -c Debug` 成功。

- [ ] 4. 在 `NodeEditorViewModel` 增加拖拽协调逻辑与命令。
      新增字段 `private readonly Dictionary<Guid, List<NodeViewModel>> _dragMembers = [];`（分组 Id → 拖拽开始时快照的成员 VM）。新增两个 `ICommand`：`ItemsDragStartedCommand`、`ItemsDragCompletedCommand`（`RelayCommand<object>`，在构造函数赋值）。
      - 开始：遍历 `Groups` 中 `IsSelected` 的分组（分组 header 拖拽时其容器被选中），对每个分组调用 `gvm.BeginDrag()`，并用 `GroupMembership.MembersOf(gvm.Model, Nodes.Select(n => n.Model))` 的结果映射回 `NodeViewModel` 存入 `_dragMembers[gvm.Model.Id]`。
      - 过程：订阅每个 `GroupViewModel.PropertyChanged`（在 `CreateGroup` 创建时挂接），当 `Location` 变化且该分组有 `DragOrigin` 快照时，计算 `dx/dy = newLocation - DragOrigin`，对 `_dragMembers[id]` 中每个成员 VM 设置 `Location = GroupTranslation.Translate(memberStart.X, memberStart.Y, dx, dy)`。注意：成员平移须以**各成员拖拽开始时的坐标**为基准，故快照需同时记录成员起始坐标（见下方实现要点）。
      - 结束：对所有 `DragOrigin` 非空的分组调用 `EndDrag()`，清空 `_dragMembers`，调用 `RecomputeMembership()`。
      删除 `RecomputeMembership` 上那条错误注释（“分组移动时平移成员由 GroupingNode 内置负责”），改为正确描述。所有新公共/内部成员加 XML 文档注释。
      Files: `d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\ViewModels\NodeEditorViewModel.cs`
      Verify: `dotnet build d:\kiro\Sz3D\Sz3D.sln -c Debug` 成功。

  实现要点（务必遵循，保证正确与可测试）：
  - **成员快照记录起始坐标**：`_dragMembers` 的值建议用记录成员 VM + 其起始 `Point` 的结构（例如 `record struct MemberSnapshot(NodeViewModel Node, Point Start)`，或并存 `Dictionary<Guid,(NodeViewModel Node, double X, double Y)[]>`）。每次 `Location` 变化时成员新坐标 = `起始坐标 + 累计 delta`，而非在上一帧基础上增量，避免浮点漂移与双重累加。
  - **纯逻辑下沉**：成员新坐标的计算必须走 `GroupTranslation.Translate`（步骤 1），VM 只负责快照与赋值，确保核心平移逻辑被步骤 2 的单测覆盖。
  - 设置成员 `Location` 会触发其 `OnLocationChanged` 回写 `NodeModel.X/Y`（满足需求 1 持久化）。
  - 分组自身的移动仍由其 `Location` 的 `OnLocationChanged` 回写 `GroupModel.X/Y`，不改动。

- [ ] 5. 更新 `MainWindow.xaml`：`GroupingNode` 设 `MovementMode="Self"`，`NodifyEditor` 绑定拖拽命令。
      在 `GroupViewModel` 的 `DataTemplate` 里给 `nodify:GroupingNode` 添加 `MovementMode="Self"`（禁用内置选择式带动，改由 VM 驱动）；在 `nodify:NodifyEditor` 上添加 `ItemsDragStartedCommand="{Binding ItemsDragStartedCommand}"` 与 `ItemsDragCompletedCommand="{Binding ItemsDragCompletedCommand}"`。保留现有 `Header`/`ActualSize`/`CanResize`/`HeaderBrush`/`ContextMenu(删除分组)` 不变。
      Files: `d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\MainWindow.xaml`
      Verify: `dotnet build d:\kiro\Sz3D\Sz3D.sln -c Debug` 成功（XAML 编译无误）。

- [ ] 6. 在 `CreateGroup` 中挂接 `GroupViewModel.PropertyChanged` 以驱动成员平移。
      在 `NodeEditorViewModel.CreateGroup()` 创建 `gvm` 后，订阅 `gvm.PropertyChanged`，在 `Location` 变化时执行步骤 4 的“过程”逻辑。确保 `DeleteGroup` 时正确清理（无悬挂订阅导致的内存泄漏——若用实例方法订阅，`Groups.Remove` 后 GC 可回收，无需手动退订；若有闭包捕获，于 `DeleteGroup` 退订）。
      Files: `d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\ViewModels\NodeEditorViewModel.cs`
      Verify: `dotnet build d:\kiro\Sz3D\Sz3D.sln -c Debug` 成功；`dotnet test d:\kiro\Sz3D\tests\Sz3D.Core.Infrastructure.Tests` 全绿。

- [ ] 7. 全量构建与测试回归。
      Files:（无新增，验证步骤）
      Verify:
      - `dotnet build d:\kiro\Sz3D\Sz3D.sln -c Debug` 成功、无新增警告为错误。
      - `dotnet test d:\kiro\Sz3D\tests\Sz3D.Core.Infrastructure.Tests` 全绿（原 16 个 + 新增 `GroupTranslationTests`）。
      - 若存在 `Sz3D.Core.Operators.Tests`：`dotnet test d:\kiro\Sz3D\tests\Sz3D.Core.Operators.Tests` 仍全绿。

---

## 测试清单（新增）

`d:\kiro\Sz3D\tests\Sz3D.Core.Infrastructure.Tests\GroupTranslationTests.cs`：
- `Translate_PositiveDelta_ReturnsShiftedPoint`
- `Translate_NegativeDelta_ReturnsShiftedPoint`
- `Translate_ZeroDelta_ReturnsSamePoint`
- `ApplyDelta_ShiftsAllMembersBySameDelta`
- `ApplyDelta_EmptyMembers_IsNoOp`
- `ApplyDelta_PreservesRelativePositions`（两个成员平移后相对距离不变）

---

## 验证分工

### 由构建/测试验证（自动）
- 编译通过：`dotnet build d:\kiro\Sz3D\Sz3D.sln -c Debug`。
- 纯平移逻辑正确性：`dotnet test d:\kiro\Sz3D\tests\Sz3D.Core.Infrastructure.Tests`（含新增 `GroupTranslationTests`，原 16 个测试不回归）。

### 需用户在 Visual Studio / Rider 手动交互确认（本环境为 WPF，不能 `dotnet run` 无头启动）
手动步骤（在 VS/Rider 直接运行 `RVS.App`/`Sz3D.Presentation.WPF`，或运行生成的 `.exe`）：
1. 新建一个分组块（“新建分组”），拖动节点使若干节点落入分组矩形内。
2. 拖动分组 header 平移整块 —— 确认**块内节点随分组一起移动**，相对位置保持不变。
3. 拖动结束后再次拖动画布/保存，确认成员新位置**已持久化**（移动后未回弹）。
4. 回归确认：
   - 分组**缩放**（`CanResize`）仍可用。
   - **新建分组**仍可用。
   - 右键分组 → **“删除分组”**仍只删块、保留成员节点；右键节点 → **“删除节点”**仍可用（未退回单击右键立即删除）。
   - 分组仍**渲染在节点之后**（ZIndex=-1，被节点遮挡在下层）。
   - 落在分组**外**的节点不随分组移动。

---

## 不改动项（防回归清单）

- 节点/分组删除：保持右键 ContextMenu（“删除节点”/“删除分组”），不得重新引入单次右键立即删除或 `PreviewMouseRightButtonDown`。
- `Dark.xaml` 的 `MenuItem` 模板。
- `Sz3D.Plugins.Algorithms\Image2D\ImageProcessingNodes.cs` 中的 12 个 2D 图像处理算子。
- `GroupModel` / `GraphModel` / `GroupMembership` 不得引入任何 WPF / `System.Windows.Controls` 依赖（`GroupTranslation` 同样保持无 WPF）。
