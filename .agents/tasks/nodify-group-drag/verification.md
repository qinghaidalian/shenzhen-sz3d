# 验证说明：拖拽分组块时带动内部成员节点一起移动

## 采用的 Nodify 7.3.0 机制及理由

采用 **方案 B：视图模型按 delta 平移成员**，并将分组块的内置移动模式显式设为 `Self`。

### 在 7.3.0 程序集中确认的实际 API
核实对象：`C:\Users\aisha\.nuget\packages\nodify\7.3.0\lib\net10.0-windows7.0\Nodify.xml`。确认存在：
- `T:Nodify.GroupingMovementMode`，成员 `F:Nodify.GroupingMovementMode.Group`、`F:Nodify.GroupingMovementMode.Self`
- `P:Nodify.GroupingNode.MovementMode`
- `P:Nodify.NodifyEditor.ItemsDragStartedCommand`、`P:Nodify.NodifyEditor.ItemsDragCompletedCommand`

### 理由
- 内置 `Group` 模式依赖 header 按下瞬间的命中测试 + 区域选择，在本「节点 + 分组」异构 `ItemsSource` 编辑器中对选择时序与边界高度敏感，正是现状缺陷（分组能动、成员不动）的来源，且**无法用无 UI 的单测覆盖**。
- 方案 B 把「按 delta 平移成员坐标」下沉为无 WPF 依赖的纯函数 `GroupTranslation`（置于 `Sz3D.Core.Infrastructure`），可被 xUnit 稳定覆盖。
- 为避免内置选择式带动与 VM 平移**双重移动**冲突，`GroupingNode.MovementMode` 显式设为 `Self`：header 拖拽只移动分组自身容器，成员随动完全由 VM 驱动。
- 成员集在**拖拽开始瞬间**用 `GroupMembership.MembersOf` 几何捕获快照（含每个成员的起始坐标），拖拽过程中成员新坐标 = 起始坐标 + 分组相对起点的累计 delta，避免边缘节点中途被甩出，也避免逐帧增量导致的浮点漂移/双重累加。

## 改动清单
- 新增 `src/Sz3D.Core.Infrastructure/Graph/GroupTranslation.cs`：纯几何平移逻辑（`Translate` 纯函数、`ApplyDelta` 就地平移成员模型），无 WPF 依赖。
- 新增 `tests/Sz3D.Core.Infrastructure.Tests/GroupTranslationTests.cs`：6 个 xUnit 测试。
- `src/Sz3D.Presentation.WPF/ViewModels/GroupViewModel.cs`：新增 `DragOrigin`/`BeginDrag()`/`EndDrag()` 拖拽会话状态。
- `src/Sz3D.Presentation.WPF/ViewModels/NodeEditorViewModel.cs`：新增 `ItemsDragStartedCommand`/`ItemsDragCompletedCommand`、成员快照字典、`TranslateMembers`，在 `CreateGroup` 中订阅分组 `Location` 变化驱动随动；修正 `RecomputeMembership` 上的错误注释。
- `src/Sz3D.Presentation.WPF/MainWindow.xaml`：`GroupingNode` 设 `MovementMode="Self"`，`NodifyEditor` 绑定两个拖拽命令。

## 由构建/测试验证（自动，已执行）
- 命令：`dotnet build d:\kiro\Sz3D\Sz3D.sln -c Debug`
  - 结果：**生成成功，0 错误**。唯一警告为既有的 `StepExecutionController.cs(6,61) CS9113`（与本次改动无关，非新增）。
- 命令：`dotnet test d:\kiro\Sz3D\tests\Sz3D.Core.Infrastructure.Tests --no-build`
  - 结果：**总计 23，成功 23，失败 0**（原有测试不回归 + 新增 `GroupTranslationTests` 的 6 个全绿）。

纯平移逻辑（`GroupTranslation.Translate`/`ApplyDelta`）由单测覆盖。VM 协调层（`TranslateMembers`、拖拽命令、快照）内嵌 Nodify 控件交互与 `System.Windows.Point`，无合适的无 UI 缝隙做纯单测，故依赖「构建 + 评审 + 下方手动验证」。

## 需用户在 Visual Studio / Rider 手动确认（本环境为 WPF，不能 `dotnet run` 无头启动）
在 VS/Rider 直接运行 `Sz3D.Presentation.WPF`（或运行生成的 `.exe`）后：
1. 「新建分组」创建分组块，拖动若干节点使其落入分组矩形内。
2. 拖动分组 header 平移整块 —— 确认**块内节点随分组一起移动**，相对位置不变。
3. 拖动结束后再次操作/保存，确认成员新位置**已持久化**（未回弹）。
4. 回归确认：
   - 分组**缩放**（`CanResize`）仍可用。
   - **新建分组**仍可用。
   - 右键分组 →「删除分组」仍只删块、保留成员节点；右键节点 →「删除节点」仍可用（未退回单击右键立即删除）。
   - 分组仍**渲染在节点之后**（ZIndex=-1，被节点遮挡在下层）。
   - 落在分组**外**的节点不随分组移动。
