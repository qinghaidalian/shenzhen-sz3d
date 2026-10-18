# 分组拖拽带动成员节点一起平移（Nodify 7.3.0）

本次改动让在 Nodify 画布上拖拽分组块（`GroupingNode`）时，拖拽开始瞬间几何落在分组矩形内的成员节点随之按同一 delta 一起平移，并把新坐标回写持久化，像图漾 RVS 那样整块移动。采用「方案 B」：把分组内置移动模式显式设为 `Self`（关闭内置选择式带动），成员随动完全由视图模型驱动——拖拽开始时用 `GroupMembership.MembersOf` 快照成员集与各自起始坐标，拖拽过程中监听分组 `Location` 变化，用纯函数 `GroupTranslation.Translate` 以「起始坐标 + 累计 delta」计算成员新位置。核心平移逻辑下沉到无 WPF 依赖的 `Sz3D.Core.Infrastructure`，可被 xUnit 覆盖。

Watch for:
- （possible）`OnItemsDragStarted` 用 `Groups.Where(g => g.IsSelected)` 识别被拖分组，依赖「拖 header 前该分组 `IsSelected` 已为 true」。Nodify 文档明确 `ItemsDragStartedCommand` 针对 `SelectedContainers` 触发、`IsSelected` 与 VM 双向绑定，逻辑自洽；但本环境无法无头运行 WPF 验证实际交互时序，需按验证说明在 VS/Rider 手动确认。
- （confirmed）`GroupTranslation.ApplyDelta` 在生产路径上未被调用——VM 的 `TranslateMembers` 只用 `Translate`。`ApplyDelta` 及其 4 个测试属死代码/多余覆盖，非阻断，建议后续清理或在 VM 中改用它。
- （confirmed）VM 协调层（快照、拖拽命令、`TranslateMembers`）无无 UI 缝隙，未被单测覆盖，仅靠构建 + 手动验证，这与改动性质相符但覆盖面有限。

**Verdict**: APPROVED

## High-level view

拖拽带动的实现路径清晰且自洽：XAML 把 `GroupingNode.MovementMode` 设为 `Self` 关闭内置带动，`NodifyEditor` 绑定 `ItemsDragStartedCommand`/`ItemsDragCompletedCommand` 两个钩子；拖拽开始捕获成员快照，分组 `Location` 变化时按同一 delta 平移快照成员，拖拽结束清快照并 `RecomputeMembership`。成员集在开始瞬间快照、且以「起始坐标 + 累计 delta」计算，满足「边缘节点不中途被甩出」「无浮点漂移/双重累加」的要求。

持久化回写一致：成员平移通过设置 `NodeViewModel.Location`，经基类 `CanvasItemViewModel.OnLocationChanged` 回写 `NodeModel.X/Y`，与既有节点拖动回写路径完全一致；分组自身仍走 `GroupViewModel.OnLocationChanged` 回写 `GroupModel.X/Y`。

Nodify 7.3.0 的 API 依据真实可查：`GroupingMovementMode.Group/Self`、`GroupingNode.MovementMode`、`NodifyEditor.ItemsDragStartedCommand/ItemsDragCompletedCommand` 均在已安装程序集的 `Nodify.xml` 中确认存在，验证说明对机制的描述与文档一致。

约束未被破坏：核心 `GroupTranslation`/`GroupMembership`/`GroupModel`/`GraphModel` 无任何 `System.Windows` 依赖；ViewModel 未引用 `System.Windows.Controls` 类型；未重新引入单击右键立即删除或 `PreviewMouseRightButtonDown`（删除仍走 ContextMenu）；`CanResize`、ZIndex=-1 分组置于节点之后、右键「删除分组」只删块留成员均保留未动。

唯一实质瑕疵是 `ApplyDelta` 在生产路径未被使用，属多余代码，非阻断。真正只能靠手动确认的是拖拽交互时序（无头 WPF 不可测），验证说明已对此给出明确的手动验证清单。

<details>
<summary>Issues (3)</summary>

1. **ApplyDelta 死代码** — `GroupTranslation.ApplyDelta` 在生产路径未被调用（VM 只用 `Translate`），其 4 个测试覆盖的是未使用的分支。建议 `TranslateMembers` 改用 `ApplyDelta`，或删除 `ApplyDelta` 及对应测试以免误导。非阻断。
2. **拖拽选中时序依赖手动验证** — `OnItemsDragStarted` 依赖被拖分组在命令触发时 `IsSelected==true`。文档层面自洽，但 WPF 无法无头验证实际交互时序，须按 verification.md 在 VS/Rider 手动确认「拖 header 时成员随动且相对位置不变、结束后不回弹」。非阻断。
3. **VM 协调层无单测覆盖** — 快照、两个拖拽命令、`TranslateMembers` 内嵌 Nodify 交互与 `System.Windows.Point`，无合适无 UI 缝隙，仅靠构建 + 手动验证。与改动性质相符，记录为已知覆盖缺口。非阻断。

</details>

<details>
<summary>Details</summary>

### 拖拽开始的成员快照：捕获时机与基准正确

`OnItemsDragStarted` 先清空 `_dragMembers`，再对每个 `IsSelected` 的分组调用 `BeginDrag()` 记录起点，并用 `GroupMembership.MembersOf(gvm.Model, Nodes.Select(n => n.Model))` 对**当前**几何算出成员集，映射回 `NodeViewModel` 后连同各自 `(StartX, StartY)` 存入 `MemberSnapshot`。成员集在拖拽开始瞬间一次性捕获，过程中不再重算，满足「拖拽开始时位于分组矩形内的成员才随动、边缘节点不会中途被甩出」的要求。

`TranslateMembers` 以 `dx/dy = gvm.Location - DragOrigin` 为累计 delta，成员新坐标 = `Translate(startX, startY, dx, dy)` = 起始坐标 + 累计 delta——每帧基于拖拽起始坐标而非上一帧坐标，规避浮点漂移与双重累加。

### Self 模式 + VM 驱动：避免双重移动

```
拖 header
  │  MovementMode=Self → 内置带动关闭，只移动分组容器自身
  ▼
GroupingNode.Location 变 → ItemContainer.Location(TwoWay) 回写 GroupViewModel.Location
  │
  ├─► OnLocationChanged → GroupModel.X/Y        （分组自身持久化）
  └─► PropertyChanged(Location) → TranslateMembers → 成员 Location = start+delta
                                                        │
                                                        └─► NodeModel.X/Y （成员持久化）
```

把 `MovementMode` 显式设为 `Self` 关闭了 Nodify 内置的区域选择式带动，成员随动唯一来源是 VM 的 `TranslateMembers`，不存在内置带动与 VM 平移叠加的双重移动。即便用户事先多选了「分组 + 若干节点」再拖 header，被选中且在组内的节点会被内置容器拖动移动一次 delta，又被 `TranslateMembers` 设为 start+delta——两者结果同值，不产生偏移。

### IsSelected 识别被拖分组：唯一需要实机确认的点

`OnItemsDragStarted` 把被拖对象识别为 `Groups.Where(g => g.IsSelected)`。`ItemContainerStyle` 把 `IsSelected` 双向绑定到 VM，`Nodify.xml` 对 `ItemsDragStartedCommand` 的描述为「a drag operation starts for the `SelectedContainers`」——命令针对已选中的容器触发，Nodify 在按下容器时会先选中再开始拖拽，故命令触发时被拖分组的 VM `IsSelected` 应已为 true。链路在文档层面自洽，但 WPF 不能无头运行，交互时序只能由 verification.md 的手动步骤在 VS/Rider 确认。证据支持「likely 正确」，不构成阻断。

### ApplyDelta 未进入生产路径

`GroupTranslation.ApplyDelta`（就地平移 `NodeModel` 集合）在 VM 中无调用点——`TranslateMembers` 只用 `Translate` 逐个计算并经 `Location` setter 回写（这是必要的，因为要触发 VM 的持久化钩子与属性通知，直接改 `NodeModel.X/Y` 不会刷新 `NodeViewModel.Location`）。因此 `ApplyDelta` 及覆盖它的 4 个测试目前是多余的。建议二选一：在 `TranslateMembers` 内改用它，或删除以免给读者「成员平移走 ApplyDelta」的错误印象。非阻断。

### 回归面：均未触碰

- 删除交互：节点与分组仍各自用 ContextMenu（「删除节点」/「删除分组」）走 `DeleteCommand`，未见 `PreviewMouseRightButtonDown`，未退回单击右键立即删除。
- `DeleteGroup` 仍只 `RemoveGroup` + 从 `Groups`/`Items` 移除并 `RecomputeMembership`，不删成员节点。
- `CanResize="True"`、`ActualSize` 双向绑定、`HeaderBrush` 保留；ZIndex=-1 由 Nodify 内置、未改，分组仍渲染在节点之后。
- 核心约束：`grep` 确认 `Sz3D.Core.Infrastructure` 下无 `System.Windows`/`PresentationFramework` 依赖；`Presentation.WPF/ViewModels` 下无 `System.Windows.Controls` 引用（`System.Windows.Point`/`Size` 属允许）。
- 未触碰 Dark.xaml 的 MenuItem 模板与 12 个「图像处理 2D」算子（本次 diff 不含相关文件）。

### 测试覆盖

新增 `GroupTranslationTests` 6 个 `[Fact]`：`Translate` 的正/负/零 delta，`ApplyDelta` 的多成员同 delta、空集无操作、零 delta 不变、保持相对位置。注意其中 4 个 `ApplyDelta` 测试覆盖的是生产路径未使用的方法（见上）。

Not tested（已知缺口，与改动性质相符）：
- VM 协调层——成员快照捕获、`ItemsDragStarted/Completed` 命令、`TranslateMembers` 的 delta 计算与 `Location` 回写——因内嵌 Nodify 控件交互与 `System.Windows.Point`，无无 UI 缝隙，未被单测覆盖。
- `IsSelected` 识别被拖分组的交互时序，仅手动验证。

### 关于证据与构建

验证说明声明 `dotnet build Sz3D.sln -c Debug` 成功（仅既有、与本次无关的 `CS9113` 警告），`dotnet test Sz3D.Core.Infrastructure.Tests` 23/23 通过。按本步职责不重复整套构建/测试。所有引用的源文件已逐一读取，内容与 plan/verification 描述一致，未发现与声明相悖的证据，无需额外抽查。

</details>

<details>
<summary>文件清单</summary>

- `src/Sz3D.Core.Infrastructure/Graph/GroupTranslation.cs`（新增）：纯几何平移 `Translate`（纯函数）+ `ApplyDelta`（就地平移成员模型），无 WPF 依赖。
- `tests/Sz3D.Core.Infrastructure.Tests/GroupTranslationTests.cs`（新增）：6 个 xUnit 测试。
- `src/Sz3D.Presentation.WPF/ViewModels/GroupViewModel.cs`：新增 `_dragOrigin`/`DragOrigin`/`BeginDrag()`/`EndDrag()` 拖拽会话状态。
- `src/Sz3D.Presentation.WPF/ViewModels/NodeEditorViewModel.cs`：新增 `MemberSnapshot`、`_dragMembers`、`ItemsDragStartedCommand`/`ItemsDragCompletedCommand`、`OnItemsDragStarted`/`OnItemsDragCompleted`/`TranslateMembers`；在 `CreateGroup` 订阅分组 `Location` 变化；修正 `RecomputeMembership` 注释。
- `src/Sz3D.Presentation.WPF/MainWindow.xaml`：`GroupingNode` 加 `MovementMode="Self"`，`NodifyEditor` 绑定两个拖拽命令。

无 git 仓库，无法生成 `git diff`；本评审基于对上述源文件与 plan.md/verification.md 的直接阅读，并与已安装 Nodify 7.3.0 的 `Nodify.xml` 交叉核对。

</details>
