# Implementation Plan: Robot Vision Suite (RVS)

## Overview

基于 WPF / .NET 10 / C# 实现工业级 3D 机器视觉开发平台。实现分为八个阶段：项目脚手架与插件契约、核心领域模型（图、端口、撤销重做）、插件加载系统与节点库、可视化节点编辑器（Nodify 集成）、执行引擎、结果可视化（HelixToolkit.SharpDX）、项目管理与持久化、UI 布局与主题本地化，最后是内置节点库实现。每个阶段均以增量方式构建，以"检查点"任务验证已集成的功能。

---

## Tasks

- [ ] 1. 项目脚手架与插件契约程序集
  - [ ] 1.1 创建解决方案结构与核心项目
    - 创建 `RVS.sln`，添加以下项目：`RVS.App`（WPF，.NET 10）、`RVS.Core`（类库）、`RVS.PluginContract`（类库，独立 NuGet 边界）、`RVS.Tests`（xUnit 测试项目）
    - 在 `Directory.Build.props` 中统一配置 `<TargetFramework>net10.0-windows</TargetFramework>`、可为空引用类型、隐式 using
    - 添加 NuGet 包：`Nodify`、`HelixToolkit.SharpDX.Wpf`、`AvalonDock`（Xceed）、`xunit`、`Moq`、`FsCheck.Xunit`、`BenchmarkDotNet`、`Microsoft.ML.OnnxRuntime`、`NModbus`
    - _Requirements: 2.4_

  - [ ] 1.2 定义插件契约接口（`RVS.PluginContract`）
    - 实现 `INodePlugin`、`NodeBase`（抽象类）、`INodeExecutionContext`、`NodeMetadata`、`PortDescriptor`、`NodeTypeDescriptor` 等接口与数据结构
    - 定义 `ValidationResult`、`NodeProgress` 辅助类型
    - 在 `NodeBase` 中实现 `[NodeType]` 特性（`Category`、`Name`、`Description`、`Version`）
    - _Requirements: 2.4_

  - [ ]* 1.3 为插件契约编写单元测试
    - 测试 `NodeBase` 子类可正确被反射发现
    - 测试 `INodeExecutionContext` Mock 可正确传递输入/输出值
    - _Requirements: 2.4_

- [ ] 2. 核心领域模型
  - [ ] 2.1 实现 `GraphModel`、`NodeModel`、`ConnectionModel`、`PortId`
    - 实现 `GraphModel.AddNode`、`RemoveNode`（级联删除关联连接）、`Connect`、`WouldCreateCycle`（DFS 环路检测）、`TopologicalOrder`（Kahn 算法）
    - `RemoveNode` 必须删除所有引用该节点端口的 `ConnectionModel`，不留悬空连接
    - `Connect` 返回 `Result<ConnectionModel>`，失败时携带拒绝原因（类型不兼容 / 会形成环路）
    - _Requirements: 1.1, 1.3, 1.4, 1.5, 1.10_

  - [ ]* 2.2 为 `GraphModel` 编写属性测试（Property 2、3、4、8）
    - **Property 2: 类型兼容连接建立** — 兼容类型端口连接必须成功且图中恰好新增一条连接
    - **Validates: Requirements 1.3**
    - **Property 3: 类型不兼容连接拒绝** — 不兼容类型端口连接必须被拒绝且连接数不变
    - **Validates: Requirements 1.4**
    - **Property 4: 循环连接拒绝** — 会形成环路的连接必须被拒绝且连接数不变
    - **Validates: Requirements 1.5**
    - **Property 8: 节点删除级联清理** — 删除含连接的节点后图中不存在悬空连接
    - **Validates: Requirements 1.10**

  - [ ] 2.3 实现 `PortTypeRegistry`（类型兼容性检查）
    - 支持精确类型匹配、继承链检查、`RegisterCompatibility` 显式隐式转换注册
    - 实现单例或 DI 注入，供 `GraphModel.Connect` 调用
    - _Requirements: 1.3, 1.4_

  - [ ]* 2.4 为 `PortTypeRegistry` 编写属性测试
    - 任意注册的兼容对 `(A, B)` 均能通过 `AreCompatible` 检测；未注册且无继承关系的对均返回 `false`
    - _Requirements: 1.3, 1.4_

  - [ ] 2.5 实现 `UndoRedoManager` 与内置命令
    - 实现环形缓冲栈（容量 100）的 `UndoRedoManager`（`Execute`、`Undo`、`Redo`）
    - 实现 `IGraphCommand` 及以下命令类：`AddNodeCommand`、`RemoveNodeCommand`、`AddConnectionCommand`、`RemoveConnectionCommand`、`MoveNodeCommand`、`GroupNodesCommand`、`BatchCommand`
    - _Requirements: 1.9_

  - [ ]* 2.6 为 `UndoRedoManager` 编写属性测试（Property 7）
    - **Property 7: 撤销重做往返一致性** — 任意操作序列全部撤销后图恢复到操作前的完全一致状态
    - **Validates: Requirements 1.9**

- [ ] 3. 检查点 — 确保所有测试通过，如有问题请告知用户。

- [ ] 4. 插件加载系统与节点库
  - [ ] 4.1 实现 `PluginLoader`（`AssemblyLoadContext` + 热重载）
    - 使用 `PluginLoadContext : AssemblyLoadContext`（`isCollectible: true`）隔离每个插件
    - 实现 `ScanAndLoadAsync`：枚举 `./plugins/*.dll`，反射发现 `INodePlugin` 实现，实例化后触发 `PluginLoaded` 事件
    - 单个 DLL 加载失败时记录错误日志并触发 `PluginLoadFailed` 事件，继续加载其余插件
    - 实现"重新扫描插件"方法，支持无需重启刷新节点库
    - _Requirements: 2.1, 2.3, 2.6_

  - [ ]* 4.2 为 `PluginLoader` 编写属性测试（Property 9、10）
    - **Property 9: 插件加载完整注册** — 含 N 个节点类型的有效插件 DLL 加载后 NodeLibrary 中必须包含全部 N 个类型
    - **Validates: Requirements 2.2**
    - **Property 10: 插件加载错误隔离** — 有效与无效 DLL 混合时，有效插件全部加载成功，每个无效插件恰好产生一条错误日志，平台达到可交互状态
    - **Validates: Requirements 2.3**

  - [ ] 4.3 实现 `NodeLibraryViewModel`（分类显示 + 实时搜索过滤）
    - 按类别（相机、图像处理、机器人、标定、通讯、深度学习、自定义）分组显示节点类型
    - `SearchText` setter 触发 `RefreshFilter`，O(n) 遍历按名称/描述 `Contains(keyword, OrdinalIgnoreCase)` 过滤
    - _Requirements: 2.2, 2.5_

  - [ ]* 4.4 为 `NodeLibraryViewModel` 编写属性测试（Property 11）
    - **Property 11: 节点库搜索过滤正确性** — 任意非空关键词过滤结果中每条记录均包含该关键词，且无漏网的匹配项
    - **Validates: Requirements 2.5**

- [ ] 5. 可视化节点编辑器（Nodify 集成）
  - [ ] 5.1 实现 `GraphViewModel`（Nodify MVVM 绑定）
    - 实现 `NodeViewModel`、`ConnectionViewModel`、`PendingConnectionViewModel`
    - 绑定 `NodifyEditor.MinZoom = 0.1`、`MaxZoom = 10.0` 约束画布缩放范围
    - 实现 `StartConnectionCommand`（从 Output Port 开始拖拽）和 `FinishConnectionCommand`（到 Input Port 完成连接），调用 `GraphModel.Connect` 并在类型不兼容时触发端口红色高亮反馈
    - 实现 `DeleteSelectedCommand`（Delete 键），通过 `RemoveNodeCommand` 写入 Undo 栈
    - 实现 `GroupSelectedCommand`，通过 `GroupNodesCommand` 封装所选节点为 Group
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.8, 1.10_

  - [ ]* 5.2 为画布操作编写属性测试（Property 1、5、6）
    - **Property 1: 节点实例化完整性** — NodeLibrary 中任意节点类型拖入画布后恰好产生一个该类型的节点实例
    - **Validates: Requirements 1.2**
    - **Property 5: 成组操作节点计数不变性** — 选中 N 个节点成组后画布顶层节点数减少恰好 N−1
    - **Validates: Requirements 1.6**
    - **Property 6: 画布缩放范围约束** — 任意缩放操作序列后画布 Scale 始终在 [0.1, 10.0] 闭区间内
    - **Validates: Requirements 1.8**

  - [ ] 5.3 实现节点编辑器 WPF View（`NodeEditorView.xaml`）
    - 使用 `NodifyEditor` 控件绑定 `GraphViewModel`
    - 实现从 `NodeLibraryPanel` 拖拽节点到 Canvas 的拖放逻辑（`DragDrop`），在拖放位置实例化节点
    - 实现 Group 节点双击进入子 Canvas 视图
    - 绑定 Ctrl+Z / Ctrl+Y 快捷键到 `UndoRedoManager.Undo` / `Redo`
    - 实现不兼容连接的端口红色高亮动画及"不允许循环连接"提示 `ToolTip`
    - _Requirements: 1.1, 1.2, 1.7, 1.9_

- [ ] 6. 检查点 — 确保所有测试通过，如有问题请告知用户。

- [ ] 7. 执行引擎
  - [ ] 7.1 实现 `ExecutionEngine`（拓扑调度 + 数据流传播）
    - 调用 `graph.TopologicalOrder()` 获取执行序列，依次调用每个节点的 `ExecuteAsync`
    - 实现 `PropagateInputs`：执行前从上游 `PortValue` 填充当前节点 Input Ports
    - 实现 `PropagateOutputs`：执行后将当前节点 Output Ports 的 `PortValue` 传播至下游节点
    - 节点执行异常时：停止执行、在 UI 将该节点标记红色边框、写入含节点名称/错误类型/消息的日志条目，抛出 `GraphExecutionException`
    - 实现 `CancellationToken` 协作停止（检查点在每个节点 `ExecuteAsync` 调用前）
    - _Requirements: 4.1, 4.2, 4.3, 4.5_

  - [ ]* 7.2 为 `ExecutionEngine` 编写属性测试（Property 13、14、15、16）
    - **Property 13: 拓扑排序依赖顺序** — 任意有效 DAG 的执行序列中，每条连接边的源节点均出现在目标节点之前
    - **Validates: Requirements 4.1**
    - **Property 14: 数据流传播正确性** — 任意图拓扑中，节点 A 执行完成后，所有通过连接与 A 相连的下游节点 B 的对应 Input Port 必须收到 A 产生的精确 `PortValue`
    - **Validates: Requirements 4.2**
    - **Property 15: 执行错误隔离与上报** — 任意节点抛出异常时，引擎必须停止执行、恰好将该节点标记为错误状态、写入包含节点名称和异常消息的日志条目
    - **Validates: Requirements 4.3**
    - **Property 16: 停止操作协作终止** — 停止请求发出后，引擎不得对尚未开始的节点调用 `ExecuteAsync`
    - **Validates: Requirements 4.5**

  - [ ] 7.3 实现执行状态 UI 反馈（状态栏 + 节点高亮）
    - 执行期间状态栏显示当前执行节点名称及进度（已完成/总数）
    - Canvas 上当前执行节点以高亮颜色标记（绑定 `NodeViewModel.ExecutionState`）
    - 执行完成时状态栏显示"执行完成"及总耗时（ms 精度）
    - _Requirements: 4.4, 4.7_

  - [ ]* 7.4 为执行耗时记录编写属性测试（Property 17）
    - **Property 17: 执行耗时记录完整性** — 任意成功完成的图执行，日志面板中每个已执行节点均有对应的耗时记录，无遗漏
    - **Validates: Requirements 4.7**

  - [ ] 7.5 实现单步执行模式（`StepExecutionController`）
    - 使用 `SemaphoreSlim` 实现"下一步"门控，每次用户触发仅执行拓扑序中的下一个节点
    - _Requirements: 4.6_

- [ ] 8. 结果可视化子系统
  - [ ] 8.1 实现 `VisualizerRegistry` 与内置可视化工厂
    - 实现 `IVisualizerFactory` 接口和 `VisualizerRegistry`（工厂注册表）
    - 注册内置工厂：`string` → `TextBlock`（滚动）、`int`/`double`/`float` → 数值标签、`Mat`/`Bitmap` → `Image`（WriteableBitmap）
    - _Requirements: 5.1_

  - [ ]* 8.2 为 `VisualizerRegistry` 编写属性测试（Property 18）
    - **Property 18: 可视化类型覆盖** — 对 `{string, int, double, float, Mat, Bitmap, PointCloud, Pose}` 集合中任意类型调用 `VisualizerRegistry.CreateFor(dataType)` 必须返回非空控件且不抛异常
    - **Validates: Requirements 5.1**

  - [ ] 8.3 实现 HelixToolkit.SharpDX 点云视口（`PointCloudVisualizerViewModel`）
    - 在 XAML 中使用 `<hx:Viewport3DX>` + `<hx:PointGeometryModel3D>` 绑定 `PointCloudGeometry`
    - `LoadPointCloud` 方法：将 SOA 布局的 `PointCloud` 转换为 `PointGeometry3D.Positions`（`Vector3Collection`）
    - 超过 500 万点时自动执行体素降采样 LOD（目标 300 万点，体素尺寸动态计算以维持 30 FPS）
    - 支持鼠标左键旋转、右键平移、滚轮缩放交互（HelixToolkit 原生支持）
    - _Requirements: 5.3, 5.4_

  - [ ] 8.4 实现 Pose Gizmo 渲染
    - 使用 `LineGeometryModel3D` 绘制三色坐标轴：X 轴红色、Y 轴绿色、Z 轴蓝色
    - 轴长 = 场景包围盒对角线长度 × 0.1，随场景缩放自适应
    - 三轴起点为 Pose 世界坐标位置，终点由姿态旋转矩阵变换各轴单位向量后乘以轴长
    - _Requirements: 5.5_

  - [ ]* 8.5 为 Pose Gizmo 编写属性测试（Property 19）
    - **Property 19: 位姿 Gizmo 轴颜色正确性** — 任意有效 `Pose` 渲染后 Gizmo 必须恰好包含三条轴线，X 轴为红色、Y 轴为绿色、Z 轴为蓝色
    - **Validates: Requirements 5.5**

  - [ ] 8.6 实现 `VisualizerPanel`（多标签页 + 双击端口触发 + 自动刷新）
    - 支持多标签页同时显示；用户双击 Output Port 时在 VisualizerPanel 中打开对应可视化视图
    - 节点执行完成时自动刷新当前已打开的该节点相关可视化视图
    - _Requirements: 5.2, 5.6, 5.7_

  - [ ]* 8.7 为可视化自动刷新编写属性测试（Property 20）
    - **Property 20: 节点执行后可视化自动刷新** — 任意节点执行完成且 VisualizerPanel 中有已打开的该节点可视化标签页时，每个打开的可视化视图必须显示本次最新执行数据，而非上次执行的陈旧数据
    - **Validates: Requirements 5.7**

- [ ] 9. 检查点 — 确保所有测试通过，如有问题请告知用户。

- [ ] 10. 项目管理与持久化
  - [ ] 10.1 实现数据传输对象（DTO）与 JSON 序列化
    - 实现 `ProjectFile`、`GraphDto`、`NodeDto`、`ConnectionDto`、`CanvasLayoutDto` record 类型
    - 使用 `System.Text.Json` 实现 `.rvsp` 文件的序列化与反序列化
    - `NodeDto.TypeId` 格式为 `"{PluginId}:{NodeTypeName}"`
    - _Requirements: 6.1, 6.2_

  - [ ] 10.2 实现 `ProjectLoader`（含缺失插件占位节点处理）
    - 反序列化 `.rvsp` 文件，检测引用了但当前未加载的插件 ID
    - 缺失节点替换为 `PlaceholderNode`（灰色虚框，标注缺失插件名称），向用户显示缺失插件列表
    - 对于未缺失节点，正常还原 Graph 拓扑、节点参数、Canvas 布局
    - _Requirements: 6.2, 6.3_

  - [ ]* 10.3 为项目序列化编写属性测试（Property 21、22）
    - **Property 21: 项目序列化往返一致性** — 任意项目状态序列化后反序列化，必须产生相同图拓扑（相同节点 ID、类型、连接）、相同节点参数值、Canvas 位置误差在 0.01 像素以内
    - **Validates: Requirements 6.1, 6.2**
    - **Property 22: 缺失插件占位节点行为** — 引用未加载插件的 `.rvsp` 文件加载时，每个缺失节点类型恰好产生一个 `PlaceholderNode`，向用户显示缺失插件 ID 列表，非缺失节点的图拓扑保持不变
    - **Validates: Requirements 6.3**

  - [ ] 10.4 实现最近项目列表与模板功能
    - 最近项目列表持久化至配置文件，维护最多 10 条记录（逆序），无重复
    - 内置不少于 3 个项目模板（"基础点云采集"、"手眼标定"、"缺陷检测"），从模板新建项目时加载预置 Graph 结构
    - 未保存修改关闭时弹出确认对话框（保存并关闭 / 不保存关闭 / 取消）
    - _Requirements: 6.4, 6.5, 6.6_

  - [ ]* 10.5 为最近项目列表编写属性测试（Property 23、24）
    - **Property 23: 最近项目列表维护** — 顺序打开 ≥10 个不同项目后，列表恰好包含最近 10 个项目，按逆序排列，无重复
    - **Validates: Requirements 6.4**
    - **Property 24: 模板项目非空结构** — 任意内置模板通过"从模板新建"加载后，项目包含至少一个节点，且图是有效 DAG
    - **Validates: Requirements 6.6**

  - [ ] 10.6 实现 `AutoSaveService`（自动保存与崩溃恢复）
    - Graph 执行成功完成后自动保存至 `./autosave/autosave_<project_name>.rvsp`
    - Platform 启动时检测自动保存文件，提示用户是否恢复
    - _Requirements: 8.5_

  - [ ]* 10.7 为自动保存编写属性测试（Property 26）
    - **Property 26: 自动保存恢复往返** — 任意 Graph 执行成功完成时，自动保存文件必须存在且可反序列化为包含等效图与节点参数的项目
    - **Validates: Requirements 8.5**

- [ ] 11. 检查点 — 确保所有测试通过，如有问题请告知用户。

- [ ] 12. 全局异常处理与崩溃日志
  - [ ] 12.1 实现全局异常捕获与崩溃日志（`CrashLogger`）
    - 在 `App.xaml.cs` 中注册 `DispatcherUnhandledException`、`AppDomain.CurrentDomain.UnhandledException`、`TaskScheduler.UnobservedTaskException`
    - `CrashLogger.Write` 将时间戳、.NET 版本、OS 版本、完整堆栈跟踪写入 `./logs/crash_<yyyy-MM-dd_HHmmss>.log`
    - 向用户显示"程序遇到错误，已保存崩溃日志"提示，不直接崩溃退出
    - _Requirements: 8.4_

  - [ ]* 12.2 为崩溃日志编写属性测试（Property 25）
    - **Property 25: 崩溃日志文件生成** — 任意未处理异常到达全局处理器时，必须在 `./logs/crash_<timestamp>.log` 创建崩溃日志文件并包含完整异常堆栈跟踪
    - **Validates: Requirements 8.4**

- [ ] 13. UI 布局、主题与本地化
  - [ ] 13.1 实现 AvalonDock 主窗口停靠布局
    - 在 `MainWindow.xaml` 中配置 `DockingManager`，包含：顶部菜单栏与工具栏、左侧 `NodeLibraryPanel`（宽 220）、中央 `Canvas` 文档区、右侧属性面板（宽 280）、底部 `LogPanel`
    - AvalonDock XML 布局序列化保存至 `./config/layout.xml`，启动时恢复
    - 属性面板绑定选中节点的可编辑参数列表，参数修改后立即同步到节点实例
    - _Requirements: 7.1, 7.2, 7.3_

  - [ ] 13.2 实现日志面板（按级别过滤）
    - 底部日志面板按时间戳记录 INFO / WARN / ERROR 级别日志
    - 提供按日志级别过滤的下拉选择器
    - _Requirements: 7.4_

  - [ ] 13.3 实现主题系统（`ThemeManager`）与双主题 ResourceDictionary
    - 创建 `Themes/Light.xaml` 与 `Themes/Dark.xaml` 资源字典，覆盖所有 UI 组件配色
    - `ThemeManager.ApplyTheme` 替换 `Application.Current.Resources.MergedDictionaries[0]`，即时刷新，无需重启
    - _Requirements: 7.5_

  - [ ] 13.4 实现本地化系统（中文 / 英文，`ResourceManager`）
    - 创建 `Strings.zh-CN.resx`（默认）与 `Strings.en-US.resx`
    - 语言切换通过修改 `Thread.CurrentThread.CurrentUICulture` 并触发全局 `PropertyChanged` 即时更新所有 UI 文本，无需重启
    - _Requirements: 7.6_

- [ ] 14. 内置节点库实现
  - [ ] 14.1 实现相机 SDK 节点（`CameraSDKPlugin`）
    - 实现 `Percipio3DCameraNode`：设备枚举、连接、参数配置（曝光、增益）、彩色/深度图像帧采集，输出至 Output Port
    - _Requirements: 3.1_

  - [ ] 14.2 实现图像处理节点（`ImageProcessingPlugin`）
    - 实现 2D 处理节点：`GaussianBlurNode`、`MedianFilterNode`、`CannyEdgeNode`、`DilateNode`、`ErodeNode`
    - 实现 3D 点云处理节点：`PassThroughFilterNode`、`StatisticalOutlierRemovalNode`、`IcpNode`、`NormalEstimationNode`
    - _Requirements: 3.2_

  - [ ] 14.3 实现机器人接口节点（`RobotInterfacePlugin`）
    - 实现 `ABBEgmRobotNode`、`KukaRsiRobotNode`、`ModbusTcpRobotNode`，支持向机器人发送运动指令或读取关节状态
    - _Requirements: 3.3_

  - [ ] 14.4 实现标定节点（`CalibrationPlugin`）
    - 实现 `CameraIntrinsicCalibrationNode`（棋盘格法）、`EyeInHandCalibrationNode`、`EyeToHandCalibrationNode`
    - 标定完成后输出 `CalibrationResult`（内参矩阵、畸变系数、手眼变换矩阵）至 Output Port，并支持持久化保存至文件
    - _Requirements: 3.4_

  - [ ]* 14.5 为 `CalibrationResult` 编写属性测试（Property 12）
    - **Property 12: 标定结果序列化往返** — 任意有效 `CalibrationResult` 序列化到文件再反序列化，必须在浮点精度容差 1e-9 内与原始值相等
    - **Validates: Requirements 3.4**

  - [ ] 14.6 实现通讯节点（`CommunicationPlugin`）
    - 实现 `TcpClientNode`、`TcpServerNode`（TCP/IP 客户端与服务端）
    - 实现 `ModbusTcpNode`（NModbus 库）、`OpcUaClientNode`
    - 建立连接、发送/接收数据，将接收数据输出至 Output Port
    - _Requirements: 3.5_

  - [ ] 14.7 实现深度学习推理（gRPC 后端服务）
    - 实现 gRPC 后端服务 `Sz3D.Backend.AiService`（独立进程，Kestrel 监听 localhost），暴露 LoadModel / Infer / InferStream / UnloadModel / HealthCheck 接口（由 `protos/inference.proto` 定义）
    - 实现 gRPC 客户端节点 `AiInferenceNode`（`DeepLearningPlugin`）：确保模型加载、图像预处理、通过 gRPC stub 调用 AiService 推理、解析结果输出至 Output Port
    - _Requirements: 3.7_

- [ ] 15. 集成联调与最终检查点
  - [ ] 15.1 集成所有子系统并完成端到端联调
    - 验证从拖入节点 → 连线 → 运行 → 可视化结果的完整流程
    - 验证项目保存/加载往返（`.rvsp` 文件）
    - 验证插件热重载后节点库正确刷新
    - _Requirements: 1.1–1.10, 2.1–2.6, 4.1–4.7, 5.1–5.7, 6.1–6.6_

  - [ ]* 15.2 编写集成测试（`ExecutionEngine` 全流程 + 插件热重载 + 自动保存恢复）
    - 使用 Mock SDK 节点验证 `ExecutionEngine` 全流程（输入传播 → 执行 → 输出传播 → 结果可视化刷新）
    - 测试插件热重载：新 DLL 放入 `./plugins/` 并触发重扫后节点库包含新节点类型
    - 测试自动保存 → 模拟异常退出 → 重启恢复提示流程
    - _Requirements: 2.6, 4.1–4.7, 8.5_

- [ ] 16. 最终检查点 — 确保所有测试通过，如有问题请告知用户。

---

## Notes

- 标有 `*` 的子任务为可选属性测试/单元测试，可在 MVP 阶段跳过以加快交付
- 每个任务均引用具体需求条款以便追溯
- 属性测试使用 FsCheck via xUnit，每条属性标注对应设计文档属性编号（`[Property]` 特性）
- 外部 SDK 调用（相机、机器人、通讯协议）均通过 Mock 接口隔离，不依赖真实硬件
- HelixToolkit.SharpDX 的 `Viewport3DX` 原生基于 DirectX 11，与 WPF 视觉树直接集成，无需 `HwndHost` 桥接
- 检查点任务不包含代码，仅为验证节点，不纳入并行执行图

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3", "2.1", "2.3"] },
    { "id": 2, "tasks": ["2.2", "2.4", "2.5"] },
    { "id": 3, "tasks": ["2.6", "4.1"] },
    { "id": 4, "tasks": ["4.2", "4.3"] },
    { "id": 5, "tasks": ["4.4", "5.1"] },
    { "id": 6, "tasks": ["5.2", "5.3"] },
    { "id": 7, "tasks": ["7.1"] },
    { "id": 8, "tasks": ["7.2", "7.3"] },
    { "id": 9, "tasks": ["7.4", "7.5", "8.1"] },
    { "id": 10, "tasks": ["8.2", "8.3"] },
    { "id": 11, "tasks": ["8.4", "8.6"] },
    { "id": 12, "tasks": ["8.5", "8.7", "10.1"] },
    { "id": 13, "tasks": ["10.2"] },
    { "id": 14, "tasks": ["10.3", "10.4"] },
    { "id": 15, "tasks": ["10.5", "10.6"] },
    { "id": 16, "tasks": ["10.7", "12.1"] },
    { "id": 17, "tasks": ["12.2", "13.1"] },
    { "id": 18, "tasks": ["13.2", "13.3", "13.4"] },
    { "id": 19, "tasks": ["14.1", "14.2", "14.3", "14.6", "14.7"] },
    { "id": 20, "tasks": ["14.4"] },
    { "id": 21, "tasks": ["14.5", "15.1"] },
    { "id": 22, "tasks": ["15.2"] }
  ]
}
```
