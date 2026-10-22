# 验证记录 — 工具箱四面板拆分

## 改动摘要

把右列单个「工具箱」`LayoutAnchorable`（`ContentId="panel.toolbox"`，内含一个 `TabControl` + 四个 `TabItem`）拆成 **四个并列的独立 `LayoutAnchorable`**，放在同一个 `LayoutAnchorablePane`（保留 `DockWidth="300" DockMinWidth="240"`）内，原生渲染为一个 Tab 组：

| ContentId | Title | 内容（逐字搬入） | 初始态 |
|---|---|---|---|
| `panel.props` | 属性面板 | 原「属性面板」`TabItem` 的 `<Grid>`（含 `Editor.SelectedNode` 占位 `DataTrigger`、`StackPanel DataContext="{Binding Editor.SelectedNode}"` + `ItemsControl ItemsSource="{Binding Parameters}"` 的 `ParameterViewModel` 模板） | `IsSelected="True" IsActive="True"` |
| `panel.optree` | 算子树状图 | 原占位 `TextBlock`「算子树（占位）」 | — |
| `panel.oplist` | 算子列表 | 原「算子列表」`TabItem` 的 `<DockPanel>`（含「更新」`Button`、「查找算子…」`TextBox`、`ItemsControl ItemsSource="{Binding OperatorGroups}"` > `Expander` > `ItemsControl ItemsSource="{Binding Items}"`） | — |
| `panel.debug` | 调试面板 | 原占位 `TextBlock`「调试面板（占位）」 | — |

四块均 `CanClose="False" CanHide="True"`（与同文件 2D/3D anchorable 一致，X 触发隐藏、保留在布局树中）。

## 持久化白名单（最高风险区）对齐推理

承载 live XAML 内容的面板由 5 块变为 8 块：`panel.view3d`、`panel.view2d`、`panel.graph`、`panel.props`、`panel.optree`、`panel.oplist`、`panel.debug`、`panel.log`。四个新 `ContentId` 在以下五处全部覆盖，缺一处就会在布局恢复后白屏或误判回退：

1. **`LoadLayout` 步骤① 抓缓存 `switch`**：新增 `IdProps`/`IdOpTree`/`IdOpList`/`IdDebug` 四个 `case`，删除 `IdToolbox`。
2. **`LoadLayout` 步骤①a null 断言**：八块全部参与 null 校验（任一为 null 即放弃持久化、保留默认布局）。
3. **`ReattachContent` 的 `lc.ContentId switch`**：新增四个映射到 `_propsHost`/`_opTreeHost`/`_opListHost`/`_debugHost`，删除 `IdToolbox => _toolboxHost`。
4. **`ResetToDefaultLayout` detach 循环数组**：扩为八块 host。
5. **`RequiredIds`**：改为 8 项 `{ IdView3D, IdView2D, IdGraph, IdProps, IdOpTree, IdOpList, IdDebug, IdLog }`。

`RequiredIds` 正确性：隐藏（`IsHidden=true`）的 anchorable 仍在布局树中（`Descendents()` 枚举得到），`CollectPresentIds()` 仍能收集到它 → 用户关掉某个工具箱面板后保存再启动，`AllRequiredPresent()` 不会误报缺失、不会回退默认。因此四个新面板列入 `RequiredIds` 既安全又必要（它们必须始终存在于树中，才能被菜单 `Show()` 复位）。`CollectPresentIds()`/`AllRequiredPresent()` 无硬编码 `ContentId`，随 `RequiredIds` 自动适配，无需改动。

`IdToolbox` 常量与 `_toolboxHost` 字段已彻底移除（全工程 grep 确认无残留引用）。

## 视图菜单

在 2D/3D 菜单项之后新增四个可勾选 `MenuItem`（`x:Name` 为 `MenuPanelProps`/`MenuPanelOpTree`/`MenuPanelOpList`/`MenuPanelDebug`），完全复刻 `MenuPanelView2D` 写法。`OnViewMenuOpened` 中追加四行 `IsChecked = IsPanelVisible(...)` 同步。新增四个单行表达式体 `Click` 处理器调用 `TogglePanel(...)`。`TogglePanel`/`FindAnchorable`/`IsPanelVisible` 复用不变。`算子图` 恒勾选+禁用、`窗口布局一~四` 占位、2D/3D/日志 行为均未改动。

## 死资源清理

`ToolboxTabControlStyle`/`ToolboxTabItemStyle`/`ToolboxTabStripPanel` 三个样式资源及其上方注释块已从 `MainWindow.xaml` 的 `Window.Resources` 删除。删除前后 grep `ToolboxTab`：源码（`.xaml`/`.cs`）已 0 处引用（仅剩 `.agents/tasks/**` 文档中的历史提及，非代码引用）。其它仍被引用的资源（`OperatorDescriptor` 的 `DataTemplate`、节点样式等）未动。

## 构建与运行验证（实际执行结果）

- **清理运行实例**：`Get-Process Sz3D.Presentation.WPF | Stop-Process -Force`（执行，无异常）。
- **构建**：`dotnet build src\Sz3D.Presentation.WPF\Sz3D.Presentation.WPF.csproj -c Debug`
  - 结果：**已成功生成，0 个警告，0 个错误**（用时约 4s）。本次未出现任何 CS9113 警告（计划中提到的 `StepExecutionController.cs` 未使用 `engine` 参数的 CS9113 为既有、与本改动无关、可接受）。
  - XAML 在构建期编译，故构建成功即验证 XAML 有效。
- **运行冒烟**：启动 `bin\Debug\net10.0-windows\Sz3D.Presentation.WPF.exe` 为后台进程，等待 3s：
  - `ALIVE=True`（进程存活）
  - `RESPONDING=True`（消息循环响应，窗口已正常显示）
  - `NEW_CRASH_LOGS=0`（`./logs/crash_*.log` 无新增崩溃日志）
  - 随后已 `Stop-Process -Force` 停止该进程（`REMAINING_PROCESSES=0`），不锁定 exe。
- **临时文件**：冒烟测试中写入 `%TEMP%` 的临时结果文件已删除。

## 仍需用户手工确认的 UI 行为（无法通过程序验证点击）

本次为 WPF 原地改动，无自动化 UI 测试；以下需用户在运行中的应用里目视/点击确认：

1. 四面板启动时合并为**一个 Tab 组**，默认选中「属性面板」，内容/绑定与拆分前一致。
2. 每个面板的 X 可**独立关闭**（隐藏），且横向拉伸时布局表现正常。
3. 「视图」菜单可**逐个重新勾选**复位显示，勾选态与实际可见性同步。
4. 关掉某个工具箱面板后关闭应用、重启——布局恢复且**不回退默认**（`AllRequiredPresent` 对隐藏面板仍判为存在）。
5. 删除 `./config/layout.xml` 重启不崩溃、呈现默认布局。
6. 2D/3D/算子图/日志 行为不变。
