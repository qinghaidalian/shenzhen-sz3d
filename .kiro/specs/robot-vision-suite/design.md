# Design Document

## Overview

本平台是一套基于 **WPF / .NET 10 / C#** 的工业级 3D 机器视觉开发环境，采用可视化节点编程范式，参照图漾科技 RobotVisionSuite 架构设计。平台核心由五个子系统构成：节点编辑器（基于 Nodify）、插件化节点库（DLL 动态加载）、执行引擎（拓扑调度）、结果可视化（DirectX 视口，HelixToolkit.SharpDX）、项目管理（JSON 9前

---

## Architecture

### 整体分层架构

```
┌─────────────────────────────────────────────────────────────────┐
│                        WPF 表现层（View）                         │
│  MainWindow · NodeEditorView · VisualizerPanel · PropertyPanel  │
│  LogPanel · NodeLibraryPanel · StatusBar · ThemeManager         │
└──────────────────────────────┬──────────────────────────────────┘
                               │ MVVM (Commands / Bindings)
┌──────────────────────────────▼──────────────────────────────────┐
│                     ViewModel 层                                  │
│  GraphViewModel · NodeViewModel · ConnectionViewModel            │
│  NodeLibraryViewModel · VisualizerViewModel · ProjectViewModel  │
└──────────────────────────────┬──────────────────────────────────┘
                               │ Services / Domain
┌──────────────────────────────▼──────────────────────────────────┐
│                        核心引擎层                                  │
│  ExecutionEngine · GraphModel · PluginLoader · ProjectManager   │
│  UndoRedoManager · TypeRegistry · CalibrationStore              │
└──────────────────────────────┬──────────────────────────────────┘
                               │ Plugin Contract (INodePlugin)
┌──────────────────────────────▼──────────────────────────────────┐
│                      插件层（NodePlugin DLL）                      │
│  CameraSDKPlugin · ImageProcessingPlugin · RobotInterfacePlugin │
│  CalibrationPlugin · CommunicationPlugin · DeepLearningPlugin   │
└──────────────────────────────┬──────────────────────────────────┘
                               │ Native / Managed Interop
┌──────────────────────────────▼──────────────────────────────────┐
│                       外部依赖层                                   │
│  Nodify · HelixToolkit.SharpDX · AvalonDock · gRPC · ONNX Runtime │
│  Percipio CamSDK · ABB/KUKA Robot SDKs · NModbus                │
└─────────────────────────────────────────────────────────────────┘
```

### 关键设计原则

1. **插件隔离**：每个 NodePlugin 加载在独立的 `AssemblyLoadContext`，避免版本冲突，支持热重载。
2. **数据流不可变**：节点执行时 Output Port 数据封装为不可变 `PortValue` 对象，通过引用传递给下游，避免深拷贝开销。
3. **UI/执行线程分离**：执行引擎在后台 `Task` 线程运行，所有 UI 更新通过 `Dispatcher.InvokeAsync` 路由到 UI 线程。
4. **Command 模式 Undo/Redo**：所有画布操作封装为 `ICommand`（不同于 `IRelayCommand`），推入 `UndoRedoManager` 环形缓冲栈。

---

## Components and Interfaces

### 1. 节点编辑器子系统

#### 1.1 GraphModel（领域模型）

```csharp
// 核心图模型 — 纯 C# 数据结构，不依赖 WPF
public class GraphModel
{
    public ObservableCollection<NodeModel> Nodes { get; }
    public ObservableCollection<ConnectionModel> Connections { get; }

    public Result AddNode(NodeModel node) { ... }
    public Result RemoveNode(Guid nodeId) { ... }
    public Result<ConnectionModel> Connect(PortId output, PortId input) { ... }
    public bool WouldCreateCycle(PortId from, PortId to) { ... }
    public IReadOnlyList<NodeModel> TopologicalOrder() { ... }
}
```

#### 1.2 PortTypeRegistry（类型兼容性检查）

```csharp
public class PortTypeRegistry
{
    // 注册可兼容的类型对，例如 Image → object
    public void RegisterCompatibility(Type from, Type to);
    public bool AreCompatible(Type outputType, Type inputType);
}
```

兼容性规则：精确类型匹配 + 继承链检查 + 显式注册的隐式转换。

#### 1.3 UndoRedoManager

```csharp
public class UndoRedoManager
{
    private readonly CircularBuffer<IGraphCommand> _undoStack; // 容量 100
    private readonly Stack<IGraphCommand> _redoStack;

    public void Execute(IGraphCommand command);  // 执行并推入撤销栈
    public void Undo();
    public void Redo();
}

public interface IGraphCommand
{
    void Execute();
    void Undo();
    string Description { get; }
}
```

内置命令类型：`AddNodeCommand`、`RemoveNodeCommand`、`AddConnectionCommand`、`RemoveConnectionCommand`、`MoveNodeCommand`、`GroupNodesCommand`、`BatchCommand`（组合多个命令为一步）。

#### 1.4 Canvas ViewModel（Nodify 集成）

```csharp
public class GraphViewModel : ViewModel
{
    // Nodify 绑定集合
    public ObservableCollection<NodeViewModel> Nodes { get; }
    public ObservableCollection<ConnectionViewModel> Connections { get; }
    public ObservableCollection<NodeViewModel> SelectedNodes { get; }

    // Nodify PendingConnection 支持
    public PendingConnectionViewModel PendingConnection { get; }

    public ICommand StartConnectionCommand { get; }   // 从 Output Port 开始拖拽
    public ICommand FinishConnectionCommand { get; }  // 到 Input Port 完成连接
    public ICommand DeleteSelectedCommand { get; }
    public ICommand GroupSelectedCommand { get; }
}
```

缩放约束由 Nodify `NodifyEditor.MinZoom = 0.1` / `MaxZoom = 10.0` 直接设置，无需额外逻辑。

---

### 2. 插件化架构子系统

#### 2.1 插件接口契约

```csharp
// RVS.PluginContract 程序集（独立 NuGet 包）
public interface INodePlugin
{
    string PluginId { get; }       // 唯一标识，GUID 格式
    string PluginName { get; }
    string Version { get; }        // SemVer
    IReadOnlyList<NodeTypeDescriptor> NodeTypes { get; }
}

public abstract class NodeBase
{
    public abstract NodeMetadata Metadata { get; }
    public abstract IReadOnlyList<PortDescriptor> InputPorts { get; }
    public abstract IReadOnlyList<PortDescriptor> OutputPorts { get; }

    // 主执行入口
    public abstract Task ExecuteAsync(
        INodeExecutionContext context,
        CancellationToken cancellationToken);

    // 参数验证（可选覆盖）
    public virtual ValidationResult Validate() => ValidationResult.Success;
}

public interface INodeExecutionContext
{
    T GetInput<T>(string portName);
    void SetOutput<T>(string portName, T value);
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message);
    IProgress<NodeProgress> Progress { get; }
}
```

#### 2.2 PluginLoader

```csharp
public class PluginLoader
{
    private readonly string _pluginsDirectory;
    private readonly Dictionary<string, PluginLoadContext> _loadedContexts = new();

    public event EventHandler<PluginLoadedEventArgs> PluginLoaded;
    public event EventHandler<PluginLoadFailedEventArgs> PluginLoadFailed;

    public async Task ScanAndLoadAsync()
    {
        var dlls = Directory.EnumerateFiles(_pluginsDirectory, "*.dll");
        foreach (var dll in dlls)
        {
            await TryLoadPluginAsync(dll);
        }
    }

    private async Task TryLoadPluginAsync(string dllPath)
    {
        try
        {
            var ctx = new PluginLoadContext(dllPath);
            var assembly = ctx.LoadFromAssemblyPath(dllPath);
            // 反射查找实现 INodePlugin 的类型并实例化
            var plugins = DiscoverPlugins(assembly);
            foreach (var plugin in plugins)
            {
                _loadedContexts[plugin.PluginId] = ctx;
                PluginLoaded?.Invoke(this, new(plugin));
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load plugin: {dllPath}", ex);
            PluginLoadFailed?.Invoke(this, new(dllPath, ex));
        }
    }
}
```

`PluginLoadContext` 继承 `AssemblyLoadContext`（`isCollectible: true`），以便热重载时可以卸载旧版本。

#### 2.3 NodeLibrary 搜索过滤

```csharp
public class NodeLibraryViewModel : ViewModel
{
    private string _searchText = "";

    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged();
            RefreshFilter();
        }
    }

    public ObservableCollection<NodeCategoryGroup> FilteredCategories { get; }

    private void RefreshFilter()
    {
        // O(n) 遍历，通过名称/描述 Contains(SearchText, OrdinalIgnoreCase) 过滤
    }
}
```

---

### 3. 执行引擎子系统

#### 3.1 ExecutionEngine 核心流程

```csharp
public class ExecutionEngine
{
    public async Task RunAsync(GraphModel graph, CancellationToken cancellationToken)
    {
        var order = graph.TopologicalOrder();   // Kahn 算法
        int total = order.Count;
        int completed = 0;

        foreach (var node in order)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await ReportProgress(node, completed, total);

            try
            {
                var context = BuildContext(node);
                // 从上游连线填充 Input Ports
                PropagateInputs(node, graph);
                await node.ExecuteAsync(context, cancellationToken);
                // 执行完成后广播输出数据
                PropagateOutputs(node, graph);
                completed++;
                await RecordTiming(node);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                await MarkNodeError(node, ex);
                throw new GraphExecutionException(node, ex);
            }
        }

        await ReportComplete(total, stopwatch.Elapsed);
    }
}
```

#### 3.2 拓扑排序（Kahn 算法）

```csharp
public IReadOnlyList<NodeModel> TopologicalOrder()
{
    // Kahn's algorithm on adjacency list
    var inDegree = Nodes.ToDictionary(n => n.Id, _ => 0);
    foreach (var conn in Connections)
        inDegree[conn.TargetNodeId]++;

    var queue = new Queue<NodeModel>(Nodes.Where(n => inDegree[n.Id] == 0));
    var result = new List<NodeModel>();

    while (queue.Count > 0)
    {
        var node = queue.Dequeue();
        result.Add(node);
        foreach (var downstream in GetDownstream(node))
        {
            if (--inDegree[downstream.Id] == 0)
                queue.Enqueue(downstream);
        }
    }

    if (result.Count != Nodes.Count)
        throw new CyclicGraphException(); // 理论上不会触发，因为建图时已防环
    return result;
}
```

#### 3.3 单步执行模式

```csharp
public class StepExecutionController
{
    private readonly SemaphoreSlim _stepGate = new(0, 1);
    private IReadOnlyList<NodeModel> _order;
    private int _currentIndex = 0;

    public void StepNext() => _stepGate.Release();

    public async Task RunStepModeAsync(GraphModel graph, CancellationToken ct)
    {
        _order = graph.TopologicalOrder();
        while (_currentIndex < _order.Count)
        {
            await _stepGate.WaitAsync(ct);  // 等待用户触发"下一步"
            var node = _order[_currentIndex++];
            await ExecuteNodeAsync(node, ct);
        }
    }
}
```

#### 3.4 Thread 节点并行执行

RVS 区分普通节点（占用主调度线程）和 Thread 节点（在独立线程并行运行），平台同样实现此机制：

```csharp
// NodeBase 中的节点类型标记
public enum NodeExecutionMode { Sequential, Parallel }

public abstract class NodeBase
{
    // Thread 节点覆写此属性返回 Parallel
    public virtual NodeExecutionMode ExecutionMode => NodeExecutionMode.Sequential;
    // ...
}

public class ExecutionEngine
{
    public async Task RunAsync(GraphModel graph, CancellationToken ct)
    {
        var order = graph.TopologicalOrder();
        // 按 Wave 分组：同一 Wave 内的并行节点同时启动
        var waves = GroupIntoWaves(order, graph);

        foreach (var wave in waves)
        {
            ct.ThrowIfCancellationRequested();
            var parallelNodes = wave.Where(n => n.ExecutionMode == NodeExecutionMode.Parallel);
            var sequentialNodes = wave.Where(n => n.ExecutionMode == NodeExecutionMode.Sequential);

            // 顺序节点先依次执行
            foreach (var node in sequentialNodes)
                await ExecuteNodeAsync(node, ct);

            // 并行节点并发启动，等待全部完成
            if (parallelNodes.Any())
                await Task.WhenAll(parallelNodes.Select(n => ExecuteNodeAsync(n, ct)));
        }
    }

    private IEnumerable<IGrouping<int, NodeModel>> GroupIntoWaves(
        IReadOnlyList<NodeModel> order, GraphModel graph)
    {
        // 同一 Wave：入度为 0 且前驱均已完成的节点集合
        // 实现：BFS 分层，同层节点为同一 Wave
        var depth = ComputeNodeDepths(order, graph);
        return order.GroupBy(n => depth[n.Id]).OrderBy(g => g.Key);
    }
}
```

**执行规则（与 RVS ThreadNode 语义一致）：**
- 同一 Wave 内多个 Parallel 节点同时运行，全部完成后才推进下一 Wave
- Parallel 节点与 Sequential 节点同 Wave 时，Sequential 先执行完毕，再启动 Parallel
- 多个 Sequential 节点同 Wave 时，按创建顺序依次执行

---

### 4. 结果可视化子系统

#### 4.1 VisualizerPanel 架构

```csharp
// 可视化视图工厂模式
public interface IVisualizerFactory
{
    bool CanVisualize(Type dataType);
    IVisualizerControl CreateControl();
}

// 注册表（启动时由插件系统填充）
public class VisualizerRegistry
{
    private readonly List<IVisualizerFactory> _factories = new();
    public void Register(IVisualizerFactory factory) => _factories.Add(factory);
    public IVisualizerControl? CreateFor(Type dataType)
        => _factories.FirstOrDefault(f => f.CanVisualize(dataType))?.CreateControl();
}
```

内置可视化工厂：
| 数据类型 | 控件类型 |
|---|---|
| `string` | `TextBlock`（滚动文本） |
| `int` / `double` / `float` | 数值标签 |
| `Mat` / `Bitmap` | `Image` 控件（WriteableBitmap） |
| `PointCloud` | `Viewport3DX`（HelixToolkit.SharpDX） |
| `Pose` | `Viewport3DX`（坐标轴 Gizmo） |

#### 4.2 HelixToolkit.SharpDX 视口

```csharp
// WPF XAML 中直接使用，无需 HwndHost
// <hx:Viewport3DX Camera="{Binding Camera}" EnableSwapChainRendering="True">
//     <hx:PointGeometryModel3D Geometry="{Binding PointCloudGeometry}" Size="2"/>
// </hx:Viewport3DX>

public class PointCloudVisualizerViewModel : ViewModelBase
{
    private PointGeometry3D? _geometry;
    public PointGeometry3D? PointCloudGeometry
    {
        get => _geometry;
        set { _geometry = value; OnPropertyChanged(); }
    }

    public void LoadPointCloud(PointCloud cloud)
    {
        var geo = new PointGeometry3D();
        geo.Positions = new Vector3Collection(
            Enumerable.Range(0, cloud.Count)
                .Select(i => new Vector3(cloud.X[i], cloud.Y[i], cloud.Z[i])));

        // LOD：超过 500 万点时进行降采样
        if (cloud.Count > 5_000_000)
            geo = ApplyVoxelDownsample(geo, targetCount: 3_000_000);

        PointCloudGeometry = geo;
    }
}
```

**LOD 策略**：当点数超过 500 万时，进行体素降采样（Voxel Grid Downsampling），目标点数 300 万，体素尺寸动态计算以维持 30 FPS。HelixToolkit.SharpDX 原生基于 DirectX 11，无需 `HwndHost` 桥接，与 WPF 视觉树直接集成，渲染结果通过 `D3DImage` 共享至 WPF。

#### 4.3 Pose Gizmo 渲染

每个 Pose（位置 + 姿态）在视口中使用 HelixToolkit 的 `LineGeometryModel3D` 绘制三条彩色坐标轴，无需手写着色器：
- X 轴：`Color = Colors.Red`
- Y 轴：`Color = Colors.Green`
- Z 轴：`Color = Colors.Blue`

轴长 = `scene_bounding_box.diagonal * 0.1`，随场景缩放自适应。三轴起点均为 Pose 的世界坐标位置，终点由姿态旋转矩阵将各轴单位向量变换后乘以轴长得到。

#### 4.5 Python 脚本节点（子进程 + 共享内存）

PythonScriptNode 采用**子进程 + 共享内存**方案：Python 解释器运行在独立子进程中，大型数据（图像、点云）通过 `System.IO.MemoryMappedFiles` 共享内存块传递，控制信令（调用参数、端口元数据、执行状态）通过 `AnonymousPipeServerStream` 传递 JSON 消息。

```csharp
public class PythonScriptNode : NodeBase
{
    [NodeParameter] public string ConfigJsonPath { get; set; } = "";

    private Process? _pythonProcess;
    private MemoryMappedFile? _sharedMem;
    private AnonymousPipeServerStream? _controlPipe;

    public override async Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        // 1. 将输入数据写入共享内存块
        var inputData = SerializeInputsToSharedMemory(ctx, _sharedMem!);

        // 2. 通过控制管道发送执行指令（JSON 格式）
        var command = new { action = "execute", config = ConfigJsonPath, sharedMemKey = inputData.Key };
        await _controlPipe!.WriteJsonAsync(command, ct);

        // 3. 等待 Python 子进程完成并返回结果元数据
        var result = await _controlPipe.ReadJsonAsync<PythonResult>(ct);
        if (result.Status != "ok")
            throw new NodeExecutionException(result.ErrorMessage);

        // 4. 从共享内存读取输出数据
        ReadOutputsFromSharedMemory(ctx, _sharedMem!, result);
    }

    protected override void OnFirstRun()
    {
        _sharedMem = MemoryMappedFile.CreateNew($"rvs_py_{Guid.NewGuid():N}", 512 * 1024 * 1024); // 512MB
        _controlPipe = new AnonymousPipeServerStream(PipeDirection.InOut);
        _pythonProcess = StartPythonWorker(ConfigJsonPath, _controlPipe.GetClientHandleAsString());
    }
}
```

**Python 侧 Worker 模板（由"Python 配置生成器"自动生成）：**
```python
# rvs_worker.py — 由平台自动生成，用户只需实现 process() 函数
import mmap, json, sys, struct

class RVSPyClass:
    def __init__(self): pass
    def inputData(self, inputs): pass   # 平台填充，不修改
    def outputData(self): return []     # 平台填充，不修改
    def process(self):
        # TODO: 用户在此编写处理逻辑
        pass
```

**Python 配置生成器面板** 以图形化方式定义端口与参数并生成 JSON 配置文件和同名 Python 模板：
```json
{
  "name": "ResizeImage",
  "inputs":  [{"name": "input_image", "type": "Image"}],
  "outputs": [{"name": "output_image", "type": "Image"}],
  "params":  [{"name": "width", "type": "Int", "default": 640, "min": 1, "max": 4096},
              {"name": "height","type": "Int", "default": 480, "min": 1, "max": 4096}]
}
```

---

### 5. 项目管理子系统

#### 5.1 数据模型与 JSON 格式

```csharp
public record ProjectFile
{
    public string FormatVersion { get; init; } = "1.0";
    public string ProjectName { get; init; } = "";
    public GraphDto Graph { get; init; } = new();
    public CanvasLayoutDto Layout { get; init; } = new();
    public Dictionary<string, JsonElement> NodeParameters { get; init; } = new();
}

public record GraphDto
{
    public List<NodeDto> Nodes { get; init; } = new();
    public List<ConnectionDto> Connections { get; init; } = new();
}

public record NodeDto
{
    public Guid Id { get; init; }
    public string TypeId { get; init; } = "";   // "{PluginId}:{NodeTypeName}"
    public string DisplayName { get; init; } = "";
    public double X { get; init; }
    public double Y { get; init; }
}
```

#### 5.2 缺失插件处理

```csharp
public class ProjectLoader
{
    public async Task<LoadResult> LoadAsync(string rvspPath)
    {
        var dto = JsonSerializer.Deserialize<ProjectFile>(await File.ReadAllTextAsync(rvspPath));
        var missingPlugins = FindMissingPlugins(dto.Graph.Nodes);

        if (missingPlugins.Any())
        {
            // 缺失节点替换为 PlaceholderNode（灰色虚框）
            return new LoadResult(
                RestoreWithPlaceholders(dto, missingPlugins),
                missingPlugins);
        }

        return new LoadResult(RestoreGraph(dto), Array.Empty<string>());
    }
}
```

#### 5.3 自动保存

```csharp
public class AutoSaveService
{
    private readonly ProjectManager _manager;

    public void OnGraphExecutionCompleted()
    {
        var path = $"./autosave/autosave_{_manager.CurrentProject.Name}.rvsp";
        _manager.SaveTo(path);
    }

    public bool HasRecoveryFile(string projectName)
        => File.Exists($"./autosave/autosave_{projectName}.rvsp");
}
```

---

### 6. UI 布局与主题

#### 6.1 AvalonDock 停靠布局

主窗口使用 **AvalonDock**（或 Xceed.Wpf.AvalonDock）实现可停靠多面板布局：

```xml
<avalonDock:DockingManager x:Name="DockManager"
                            Layout="{Binding LayoutRoot}">
    <avalonDock:LayoutRoot>
        <avalonDock:LayoutPanel Orientation="Horizontal">
            <avalonDock:LayoutAnchorablePane Width="220">
                <!-- NodeLibraryPanel -->
            </avalonDock:LayoutAnchorablePane>
            <avalonDock:LayoutDocumentPane>
                <!-- Canvas（主编辑区）-->
            </avalonDock:LayoutDocumentPane>
            <avalonDock:LayoutAnchorablePane Width="280">
                <!-- PropertyPanel -->
            </avalonDock:LayoutAnchorablePane>
        </avalonDock:LayoutPanel>
        <!-- 底部 LogPanel -->
    </avalonDock:LayoutRoot>
</avalonDock:DockingManager>
```

布局序列化：AvalonDock 原生支持 XML 布局序列化，保存至 `./config/layout.xml`。

#### 6.2 主题系统

```csharp
public class ThemeManager
{
    private const string LightThemeUri = "/RVS;component/Themes/Light.xaml";
    private const string DarkThemeUri  = "/RVS;component/Themes/Dark.xaml";

    public void ApplyTheme(AppTheme theme)
    {
        var uri = theme == AppTheme.Dark ? DarkThemeUri : LightThemeUri;
        Application.Current.Resources.MergedDictionaries[0] =
            new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) };
    }
}
```

#### 6.3 本地化（i18n）

使用 .NET `ResourceManager`（`resx` 文件）：
- `Strings.zh-CN.resx`（默认）
- `Strings.en-US.resx`

语言切换时调用 `Thread.CurrentThread.CurrentUICulture` 并触发全局 `PropertyChanged`（通过绑定的静态 `LocalizationExtension`）。

---

### 7. 内置节点库

#### 7.1 节点接口实现模式

所有内置节点均继承 `NodeBase`。深度学习推理节点采用**独立 gRPC 后端服务**方案：`DeepLearningPlugin` 中的节点是 gRPC 客户端，模型加载与 ONNX 推理在独立进程 `Sz3D.Backend.AiService` 中完成，与 WPF 主进程隔离。以下给出其典型实现示意：

```csharp
[NodeType(Category = "深度学习", Name = "AI 推理（gRPC）")]
public class AiInferenceNode : NodeBase
{
    [NodeParameter] public string ModelId       { get; set; } = "";
    [NodeParameter] public string ModelPath     { get; set; } = "";  // 服务端可访问路径
    [NodeParameter] public string ServerAddress { get; set; } = "http://localhost:50051";

    public override async Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        var channel = GrpcChannel.ForAddress(ServerAddress);
        var client  = new InferenceService.InferenceServiceClient(channel);

        // 确保模型已在服务端加载（幂等）
        await client.LoadModelAsync(new LoadModelRequest
            { ModelId = ModelId, ModelPath = ModelPath }, cancellationToken: ct);

        var image = ctx.GetInput<byte[]>("Image");
        var resp  = await client.InferAsync(new InferRequest
            { ModelId = ModelId, ImageData = ByteString.CopyFrom(image) }, cancellationToken: ct);

        ctx.SetOutput("Detections", resp.Detections);
    }
}
```

#### 7.2 节点类别与典型节点清单

| 类别 | 典型节点 |
|---|---|
| 相机 SDK（TyCamera） | Percipio3DCamera（枚举/连接/采集）、SimulatedCamera（预录帧回放）|
| 图像处理 2D（2D） | GaussianBlur、MedianFilter、CannyEdge、Dilate、Erode |
| 图像处理 3D（3D） | PassThroughFilter、StatisticalOutlierRemoval、NormalEstimation |
| ICP 点云配准（ICP） | IcpRegistration（点云模板匹配与精配准）|
| 机器人接口（Robot） | ABBEgmRobot、KukaRsiRobot、ModbusTcpRobot |
| 标定（Calibration） | CameraIntrinsicCalibration、EyeInHandCalibration、EyeToHandCalibration |
| 通讯（Communication） | TcpClientNode、TcpServerNode、ModbusTcpNode、OpcUaClientNode |
| 深度学习（AI） | AiInferenceNode（gRPC 调用 AiService） |
| Python 脚本（Python） | PythonScriptNode（子进程 + 共享内存，JSON 端口配置）|
| 基础（Basic） | TriggerNode、LoadNode（图像/点云加载）、SinkNode（数据汇聚）、ConditionalNode（条件分支）、CounterNode（计数器）|
| 文件操作（File） | SaveImageNode、SavePointCloudNode、ReadCsvNode、WriteCsvNode、ReadTextNode |
| 运动规划（Motion Planning，Phase 2） | JointInterpolationNode、LinearTrajectoryNode |
| 拆垛与表面（Box Unstack / Surface，Phase 2） | BoxUnstackNode、SurfaceFitNode、SurfaceDefectNode |

#### 7.3 Phase 2 节点预留接口

Phase 2 节点（运动规划、拆垛、表面处理）在 Phase 1 中以**禁用占位**方式存在于节点库，不执行任何逻辑：

```csharp
// Phase 2 占位节点基类
public abstract class Phase2PlaceholderNode : NodeBase
{
    public override Task ExecuteAsync(INodeExecutionContext ctx, CancellationToken ct)
    {
        ctx.LogWarning($"{Metadata.Name} 是 Phase 2 功能，当前版本暂不可用。");
        return Task.CompletedTask;
    }
}

// 运动规划占位
[NodeType(Category = "运动规划", Name = "关节插补", IsPhase2 = true)]
public class JointInterpolationNode : Phase2PlaceholderNode { }

// 拆垛占位
[NodeType(Category = "拆垛", Name = "箱体检测", IsPhase2 = true)]
public class BoxUnstackNode : Phase2PlaceholderNode { }
```

`IsPhase2 = true` 的节点在 NodeLibrary 面板中以灰色禁用样式渲染，悬停时显示 Tooltip "Phase 2 功能，敬请期待"，可拖入画布但执行时仅输出警告日志。

---

## Data Models

### PortValue（执行时数据载体）

```csharp
// 不可变值对象，包装任意数据
public sealed class PortValue
{
    public Type DataType { get; }
    public object? Data { get; }
    public static PortValue<T> Create<T>(T data) => new(data);
}
```

### PointCloud

```csharp
public class PointCloud
{
    public float[] X { get; }   // SOA 布局以优化 SIMD
    public float[] Y { get; }
    public float[] Z { get; }
    public byte[]? R { get; }   // 可选颜色
    public byte[]? G { get; }
    public byte[]? B { get; }
    public float[]? NX { get; } // 可选法向量
    public float[]? NY { get; }
    public float[]? NZ { get; }
    public int Count => X.Length;
}
```

### Pose

```csharp
public record struct Pose(
    double X, double Y, double Z,         // 位置（毫米）
    double Roll, double Pitch, double Yaw // 欧拉角（度），或可选四元数
);
```

### CalibrationResult

```csharp
public record CalibrationResult
{
    public double[,] IntrinsicMatrix { get; init; }    // 3×3
    public double[] DistortionCoeffs { get; init; }    // 5 或 8 元素
    public double[,] HandEyeMatrix { get; init; }      // 4×4 齐次变换
    public double ReprojectionError { get; init; }
}
```

---

## Error Handling

### 异常层次

```
GraphExecutionException
  ├── NodeExecutionException (含节点 Id、名称、内部异常)
  ├── CyclicGraphException
  └── TopologicalSortException

PluginException
  ├── PluginLoadException
  └── PluginInterfaceException

ProjectException
  ├── ProjectDeserializationException
  └── MissingPluginException (含缺失插件 Id 列表)
```

### 全局异常捕获

```csharp
// App.xaml.cs
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    DispatcherUnhandledException += (_, args) =>
    {
        CrashLogger.Write(args.Exception);
        MessageBox.Show("程序遇到错误，已保存崩溃日志");
        args.Handled = true;
    };
    AppDomain.CurrentDomain.UnhandledException += (_, args) =>
    {
        CrashLogger.Write((Exception)args.ExceptionObject);
    };
    TaskScheduler.UnobservedTaskException += (_, args) =>
    {
        Logger.Error("Unobserved task exception", args.Exception);
        args.SetObserved();
    };
}
```

崩溃日志路径：`./logs/crash_<yyyy-MM-dd_HHmmss>.log`，包含时间戳、.NET 版本、OS 版本、完整堆栈跟踪。

---

## Testing Strategy

### 双轨测试方法

本项目采用**单元/集成示例测试**与**基于属性的测试（PBT）**并行的双轨策略：

- **示例测试（xUnit + Moq）**：覆盖特定交互场景、边界条件、UI 行为（如主题切换、语言切换、停靠布局持久化）以及外部集成点（相机 SDK、机器人协议、网络通讯）。
- **属性测试（FsCheck via xUnit）**：验证跨输入空间的普适不变量（见下方 Correctness Properties），每条属性默认运行 ≥100 次随机迭代。

### 测试分层

| 层级 | 工具 | 覆盖范围 |
|---|---|---|
| 单元测试 | xUnit + Moq | `GraphModel`、`PortTypeRegistry`、`UndoRedoManager`、`PluginLoader`、序列化、`NodeLibraryViewModel` 搜索 |
| 属性测试 | FsCheck | 图操作不变量、类型兼容性、执行顺序、序列化往返（见 Correctness Properties）|
| 集成测试 | xUnit + Mock SDK | `ExecutionEngine` 全流程、插件热重载、自动保存恢复 |
| 性能测试 | BenchmarkDotNet | 启动时间、Canvas 帧率（200 节点/300 连线）、5M 点云渲染帧率 |
| UI 冒烟测试 | WPF TestHost | 主界面呈现、停靠布局、主题切换 |

### 单元测试原则

- 示例测试聚焦于**特定行为的具体演示**、**集成边界**和**错误条件**。
- 避免为 PBT 已覆盖的输入变体重复编写大量示例测试。
- 外部 SDK 调用（相机、机器人、通讯协议）均通过 Mock 接口隔离，不依赖真实硬件。

### 属性测试配置

- 最小迭代次数：每条属性 100 次（FsCheck 默认）。
- 每条属性测试通过 `[Property]` 特性标注，并注明对应的设计文档属性编号。
- 标签格式：`Feature: robot-vision-suite, Property {N}: {property_text}`

---

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: 节点实例化完整性

*For any* node type registered in the NodeLibrary, creating a node of that type on the canvas should produce exactly one node instance with the correct type identifier on the canvas.

**Validates: Requirements 1.2**

---

### Property 2: 类型兼容连接建立

*For any* two ports where the output port's data type is compatible with the input port's data type (per `PortTypeRegistry`), attempting to connect them must succeed and the resulting graph must contain exactly one new connection between those ports.

**Validates: Requirements 1.3**

---

### Property 3: 类型不兼容连接拒绝

*For any* two ports where the output port's data type is incompatible with the input port's data type, attempting to connect them must be rejected — the graph's connection count must remain unchanged.

**Validates: Requirements 1.4**

---

### Property 4: 循环连接拒绝

*For any* valid DAG and any proposed connection edge that would introduce a cycle, the connection must be rejected and the graph's connection count must remain unchanged.

**Validates: Requirements 1.5**

---

### Property 5: 成组操作节点计数不变性

*For any* non-empty selection of N nodes on the canvas, performing a group operation must reduce the canvas's top-level node count by exactly (N − 1), replacing the selected nodes with a single Group node.

**Validates: Requirements 1.6**

---

### Property 6: 画布缩放范围约束

*For any* sequence of zoom operations (scroll up or down by any amount), the resulting canvas scale value must always remain within the closed interval [0.1, 10.0].

**Validates: Requirements 1.8**

---

### Property 7: 撤销重做往返一致性

*For any* sequence of canvas operations (add node, delete node, add connection, move node, group), undoing all operations in reverse order must restore the graph to its exact state before the operations were applied.

**Validates: Requirements 1.9**

---

### Property 8: 节点删除级联清理

*For any* node in the graph that has connections, deleting that node must remove both the node itself and all connections that referenced that node — no dangling connections may remain.

**Validates: Requirements 1.10**

---

### Property 9: 插件加载完整注册

*For any* valid plugin DLL containing N declared node types, after successful loading via `PluginLoader`, the `NodeLibrary` must contain all N node types from that plugin.

**Validates: Requirements 2.2**

---

### Property 10: 插件加载错误隔离

*For any* mix of valid and invalid plugin DLLs in the plugins directory, the valid plugins must all be loaded successfully, each invalid plugin must produce exactly one error log entry, and the platform must reach a fully interactive state.

**Validates: Requirements 2.3**

---

### Property 11: 节点库搜索过滤正确性

*For any* non-empty search keyword and any state of the NodeLibrary, every node type returned in the filtered results must contain the keyword in its name or description (case-insensitive), and no matching node types may be absent from the results.

**Validates: Requirements 2.5**

---

### Property 12: 标定结果序列化往返

*For any* valid `CalibrationResult` value (intrinsic matrix, distortion coefficients, hand-eye transform), serializing it to a file and then deserializing it must produce a value equal to the original within floating-point precision tolerance (1e-9).

**Validates: Requirements 3.4**

---

### Property 13: 拓扑排序依赖顺序

*For any* valid DAG, the topological execution order produced by `ExecutionEngine` must satisfy: for every connection edge from node A to node B in the graph, node A appears before node B in the execution sequence.

**Validates: Requirements 4.1**

---

### Property 14: 数据流传播正确性

*For any* graph topology, after executing node A, every downstream node B connected to A via a connection must receive on its corresponding input port the exact `PortValue` that node A produced on the connected output port.

**Validates: Requirements 4.2**

---

### Property 15: 执行错误隔离与上报

*For any* node that throws any exception during execution, the execution engine must: (a) stop execution after that node, (b) mark exactly that node as error state, (c) write a log entry containing the node name and exception message.

**Validates: Requirements 4.3**

---

### Property 16: 停止操作协作终止

*For any* execution in progress, after stop is requested, the execution engine must not invoke `ExecuteAsync` on any node that has not yet started — at most the currently-executing node completes before termination.

**Validates: Requirements 4.5**

---

### Property 17: 执行耗时记录完整性

*For any* successfully completed graph execution, the log panel must contain a timing record for every node that was executed — no executed node may be missing a timing entry.

**Validates: Requirements 4.7**

---

### Property 18: 可视化类型覆盖

*For any* data value whose type is in the set {`string`, `int`, `double`, `float`, `Mat`, `Bitmap`, `PointCloud`, `Pose`}, calling `VisualizerRegistry.CreateFor(dataType)` must return a non-null visualizer control without throwing.

**Validates: Requirements 5.1**

---

### Property 19: 位姿 Gizmo 轴颜色正确性

*For any* valid `Pose` value rendered in the Viewport, the produced gizmo must contain exactly three axis lines where the X-axis color is red, the Y-axis color is green, and the Z-axis color is blue.

**Validates: Requirements 5.5**

---

### Property 20: 节点执行后可视化自动刷新

*For any* node that completes execution and has at least one open visualizer tab in `VisualizerPanel`, every open visualizer for that node must display data from the most recent execution (not a prior execution's stale data).

**Validates: Requirements 5.7**

---

### Property 21: 项目序列化往返一致性

*For any* project state (graph topology, node parameters, canvas layout), serializing it to a `.rvsp` file and then deserializing it must produce a project with an identical graph topology (same node IDs, types, connections), identical node parameter values, and canvas positions equal within 0.01 pixels.

**Validates: Requirements 6.1, 6.2**

---

### Property 22: 缺失插件占位节点行为

*For any* `.rvsp` project file that references one or more unloaded plugin IDs, loading the project must: (a) produce exactly one `PlaceholderNode` per missing node type, (b) display the list of missing plugin IDs to the user, (c) preserve the graph topology for all non-missing nodes.

**Validates: Requirements 6.3**

---

### Property 23: 最近项目列表维护

*For any* sequence of opening ≥10 distinct projects, the recent projects list must contain exactly the 10 most recently opened projects in reverse-chronological order, with no duplicates.

**Validates: Requirements 6.4**

---

### Property 24: 模板项目非空结构

*For any* built-in project template, loading it via "new from template" must produce a project containing at least one node in the graph (the template is not empty), and the graph must be a valid DAG.

**Validates: Requirements 6.6**

---

### Property 25: 崩溃日志文件生成

*For any* unhandled exception that reaches the global exception handler (`DispatcherUnhandledException` or `AppDomain.UnhandledException`), a crash log file must be created at `./logs/crash_<timestamp>.log` containing the full exception stack trace.

**Validates: Requirements 8.4**

---

### Property 26: 自动保存恢复往返

*For any* project state at the moment a `Graph` execution completes successfully, the auto-saved file at `./autosave/autosave_<project_name>.rvsp` must exist and must be deserializable to a project with an equivalent graph and node parameters.

**Validates: Requirements 8.5**
