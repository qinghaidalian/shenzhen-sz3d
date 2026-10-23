using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Sz3D.Core.Infrastructure.Contracts;
using Sz3D.Core.Infrastructure.Graph;
using Sz3D.Core.Infrastructure.Models;
using Sz3D.Presentation.WPF.Commands;
using Sz3D.Presentation.WPF.Services;

namespace Sz3D.Presentation.WPF.ViewModels;

/// <summary>Nodify 连接点——包装节点的输入/输出端口。</summary>
public sealed class ConnectorViewModel : ViewModelBase
{
    public ConnectorViewModel(string title, PortDirection direction, NodeViewModel node)
    {
        Title     = title;
        Direction = direction;
        Node      = node;
    }

    public string Title { get; }
    public PortDirection Direction { get; }
    public NodeViewModel Node { get; }
    public PortId PortId => new(Node.Model.Id, Title);

    private bool _isConnected;
    public bool IsConnected { get => _isConnected; set => Set(ref _isConnected, value); }

    private Point _anchor;
    public Point Anchor { get => _anchor; set => Set(ref _anchor, value); }
}

/// <summary>Nodify 画布上的一个节点——包装 <see cref="NodeModel"/>。</summary>
public sealed class NodeViewModel : CanvasItemViewModel
{
    public NodeViewModel(NodeModel model)
    {
        Model    = model;
        Location = new Point(model.X, model.Y);
    }

    public NodeModel Model { get; }
    public string Title => Model.DisplayName;

    /// <summary>位置变更时回写模型的画布坐标。</summary>
    protected override void OnLocationChanged(Point value)
    {
        Model.X = value.X;
        Model.Y = value.Y;
    }

    public ObservableCollection<ConnectorViewModel> Input  { get; } = [];
    public ObservableCollection<ConnectorViewModel> Output { get; } = [];
    public ObservableCollection<ParameterViewModel> Parameters { get; } = [];
}

/// <summary>Nodify 连线——连接一个输出连接点与一个输入连接点。</summary>
public sealed class ConnectionViewModel : ViewModelBase
{
    public ConnectionViewModel(ConnectorViewModel output, ConnectorViewModel input)
    {
        Output = output;
        Input  = input;
    }

    public ConnectorViewModel Output { get; }
    public ConnectorViewModel Input  { get; }
}

/// <summary>拖拽中的未完成连线。</summary>
public sealed class PendingConnectionViewModel : ViewModelBase
{
    private ConnectorViewModel? _source;
    public ConnectorViewModel? Source { get => _source; set => Set(ref _source, value); }

    private ConnectorViewModel? _target;
    public ConnectorViewModel? Target { get => _target; set => Set(ref _target, value); }

    private bool _isVisible;
    public bool IsVisible { get => _isVisible; set => Set(ref _isVisible, value); }

    private Point _targetLocation;
    public Point TargetLocation { get => _targetLocation; set => Set(ref _targetLocation, value); }
}

/// <summary>算子图画布控制器——桥接 <see cref="GraphModel"/> 与 Nodify 视图模型。</summary>
public sealed class NodeEditorViewModel : ViewModelBase
{
    private readonly GraphModel _graph = new();

    public ObservableCollection<NodeViewModel>       Nodes            { get; } = [];
    public ObservableCollection<ConnectionViewModel> Connections      { get; } = [];
    public PendingConnectionViewModel                PendingConnection { get; } = new();

    /// <summary>画布的异构项集合（节点 + 分组），作为 NodifyEditor 的 ItemsSource。</summary>
    public ObservableCollection<CanvasItemViewModel> Items  { get; } = [];

    /// <summary>画布上的分组集合。</summary>
    public ObservableCollection<GroupViewModel>      Groups { get; } = [];

    public ICommand StartConnectionCommand  { get; }
    public ICommand CreateConnectionCommand { get; }
    public ICommand DeleteSelectedCommand   { get; }

    /// <summary>新建一个分组块到画布。</summary>
    public ICommand CreateGroupCommand { get; }

    /// <summary>编辑器开始拖拽选中容器时触发——用于捕获分组成员快照。</summary>
    public ICommand ItemsDragStartedCommand { get; }

    /// <summary>编辑器结束拖拽选中容器时触发——用于清理快照并刷新归属映射。</summary>
    public ICommand ItemsDragCompletedCommand { get; }

    private NodeViewModel? _selectedNode;
    public NodeViewModel? SelectedNode { get => _selectedNode; private set => Set(ref _selectedNode, value); }

    private BitmapImage? _imageOutput;
    public BitmapImage? ImageOutput { get => _imageOutput; private set => Set(ref _imageOutput, value); }

    private PointCloud? _cloudOutput;
    public PointCloud? CloudOutput { get => _cloudOutput; private set => Set(ref _cloudOutput, value); }

    public bool HasImageOutput => ImageOutput is not null;
    public bool HasCloudOutput => CloudOutput is not null;

    public NodeEditorViewModel()
    {
        // Nodify 在拖出连接点时已通过 OneWayToSource 回写 PendingConnection.Source
        StartConnectionCommand  = new RelayCommand<ConnectorViewModel>(_ => { });
        CreateConnectionCommand = new RelayCommand<ConnectorViewModel>(OnCreateConnection);
        DeleteSelectedCommand   = new RelayCommand<object>(_ => DeleteSelected());
        CreateGroupCommand      = new RelayCommand<object>(_ => CreateGroup());
        ItemsDragStartedCommand   = new RelayCommand<object>(_ => OnItemsDragStarted());
        ItemsDragCompletedCommand = new RelayCommand<object>(_ => OnItemsDragCompleted());
    }

    /// <summary>一个被拖拽成员节点在拖拽开始瞬间的坐标快照。</summary>
    private readonly record struct MemberSnapshot(NodeViewModel Node, double StartX, double StartY);

    // 分组 Id → 拖拽开始时该分组几何范围内的成员节点及其起始坐标快照。
    private readonly Dictionary<Guid, List<MemberSnapshot>> _dragMembers = [];

    /// <summary>
    /// 拖拽开始：对每个被选中的分组记录起点，并用 <see cref="GroupMembership"/> 几何捕获
    /// 当前落在分组矩形内的成员节点快照（含起始坐标），后续按 delta 平移以此快照为基准。
    /// </summary>
    private void OnItemsDragStarted()
    {
        _dragMembers.Clear();
        foreach (var gvm in Groups.Where(g => g.IsSelected))
        {
            gvm.BeginDrag();
            var members = GroupMembership.MembersOf(gvm.Model, Nodes.Select(n => n.Model));
            var snapshots = Nodes
                .Where(n => members.Contains(n.Model))
                .Select(n => new MemberSnapshot(n, n.Model.X, n.Model.Y))
                .ToList();
            _dragMembers[gvm.Model.Id] = snapshots;
        }
    }

    /// <summary>拖拽结束：清空所有分组的拖拽起点与成员快照，并刷新归属映射。</summary>
    private void OnItemsDragCompleted()
    {
        foreach (var gvm in Groups.Where(g => g.DragOrigin is not null))
            gvm.EndDrag();
        _dragMembers.Clear();
        RecomputeMembership();
    }

    /// <summary>
    /// 当某个分组在拖拽过程中位置变化时，按分组相对起点的累计 delta，
    /// 把快照中的每个成员节点平移到「成员起始坐标 + 累计 delta」。
    /// 以起始坐标为基准（而非逐帧增量）避免浮点漂移与双重累加。
    /// </summary>
    /// <param name="gvm">正在被拖拽的分组视图模型。</param>
    private void TranslateMembers(GroupViewModel gvm)
    {
        if (gvm.DragOrigin is not { } origin) return;
        if (!_dragMembers.TryGetValue(gvm.Model.Id, out var snapshots)) return;

        var dx = gvm.Location.X - origin.X;
        var dy = gvm.Location.Y - origin.Y;
        foreach (var (node, startX, startY) in snapshots)
        {
            var (x, y) = GroupTranslation.Translate(startX, startY, dx, dy);
            node.Location = new Point(x, y);
        }
    }

    /// <summary>从算子目录在指定位置创建节点。</summary>
    public NodeViewModel AddOperator(OperatorDescriptor op, Point location)
    {
        var node = new NodeModel
        {
            TypeId      = op.TypeId,
            DisplayName = op.Name,
            X           = location.X,
            Y           = location.Y,
            Instance    = op.Factory(),
        };
        return AddNodeInternal(op, node);
    }

    /// <summary>
    /// 用预置的 <see cref="NodeModel"/>（已含 Id/坐标/实例）接入画布。
    /// 供项目加载时按持久化的 Id 重建节点，确保连线能按 Id 正确还原。
    /// </summary>
    private NodeViewModel AddNodeInternal(OperatorDescriptor op, NodeModel node)
    {
        _graph.AddNode(node);

        var vm = new NodeViewModel(node);
        vm.DeleteCommand = new RelayCommand<object>(_ => DeleteNode(vm));
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NodeViewModel.IsSelected))
                RefreshSelectedNode();
        };

        foreach (var p in node.Instance!.InputPorts)
            vm.Input.Add(new ConnectorViewModel(p.Name, PortDirection.Input, vm));
        foreach (var p in node.Instance!.OutputPorts)
            vm.Output.Add(new ConnectorViewModel(p.Name, PortDirection.Output, vm));

        // 反射出 [NodeParameter] 属性，供属性面板编辑
        foreach (var prop in node.Instance.GetType().GetProperties()
                     .Where(p => p.GetCustomAttribute<NodeParameterAttribute>() is not null))
            vm.Parameters.Add(new ParameterViewModel(node.Instance, prop));

        Nodes.Add(vm);
        // Nodes 仍是节点的权威集合（供执行/连线逻辑使用）；Items 是渲染用的异构集合。
        Items.Add(vm);
        return vm;
    }

    /// <summary>新建一个默认分组块并加入画布。</summary>
    public GroupViewModel CreateGroup()
    {
        var model = new GroupModel
        {
            Title  = "分组",
            X      = 0,
            Y      = 0,
            Width  = 300,
            Height = 200,
            Color  = "#0E639C",
        };
        _graph.AddGroup(model);

        var gvm = new GroupViewModel(model);
        gvm.DeleteCommand = new RelayCommand<object>(_ => DeleteGroup(gvm));
        // 分组拖拽过程中其 Location 变化 → 带动快照成员节点一起平移
        gvm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GroupViewModel.Location))
                TranslateMembers(gvm);
        };
        Groups.Add(gvm);
        Items.Add(gvm);
        RecomputeMembership();
        return gvm;
    }

    /// <summary>删除指定分组（仅移除分组块，保留其覆盖范围内的成员节点）。</summary>
    public void DeleteGroup(GroupViewModel gvm)
    {
        _graph.RemoveGroup(gvm.Model.Id);
        Groups.Remove(gvm);
        Items.Remove(gvm);
        RecomputeMembership();
    }

    /// <summary>
    /// 重算节点到分组的归属映射（按左上角坐标落点，取面积最小的包含分组）。
    /// 维护内部映射供持久化与后续查询；分组拖拽时成员的随动平移由
    /// <see cref="TranslateMembers"/> 基于拖拽开始时的成员快照驱动，不在此重算。
    /// </summary>
    private readonly Dictionary<Guid, Guid> _membership = [];

    private void RecomputeMembership()
    {
        _membership.Clear();
        foreach (var n in Nodes)
        {
            var group = GroupMembership.GroupContaining(_graph.Groups, n.Model.X, n.Model.Y);
            if (group is not null)
                _membership[n.Model.Id] = group.Id;
        }
    }

    /// <summary>连接两个端口（from 的 outPort 输出 → to 的 inPort 输入）。</summary>
    public bool Connect(NodeViewModel from, string outPort, NodeViewModel to, string inPort)
    {
        var output = from.Output.FirstOrDefault(c => c.Title == outPort);
        var input  = to.Input.FirstOrDefault(c => c.Title == inPort);
        return output is not null && input is not null && TryCreateConnection(output, input);
    }

    private void OnCreateConnection(ConnectorViewModel? target)
    {
        var source = PendingConnection.Source;
        var tgt    = target ?? PendingConnection.Target;
        PendingConnection.IsVisible = false;

        if (source is null || tgt is null || ReferenceEquals(source, tgt)) return;

        // 归一化方向：输出 → 输入
        if (source.Direction == PortDirection.Output && tgt.Direction == PortDirection.Input)
            TryCreateConnection(source, tgt);
        else if (source.Direction == PortDirection.Input && tgt.Direction == PortDirection.Output)
            TryCreateConnection(tgt, source);
    }

    private bool TryCreateConnection(ConnectorViewModel output, ConnectorViewModel input)
    {
        var result = _graph.Connect(output.PortId, input.PortId);
        if (!result.IsSuccess) return false;

        Connections.Add(new ConnectionViewModel(output, input));
        RefreshConnectionStates();
        return true;
    }

    private void DeleteSelected()
    {
        foreach (var vm in Nodes.Where(n => n.IsSelected).ToList())
            DeleteNode(vm);
    }

    /// <summary>删除指定节点及其关联连线。</summary>
    public void DeleteNode(NodeViewModel vm)
    {
        _graph.RemoveNode(vm.Model.Id);
        Nodes.Remove(vm);
        Items.Remove(vm);
        foreach (var c in Connections
                     .Where(c => ReferenceEquals(c.Output.Node, vm) || ReferenceEquals(c.Input.Node, vm))
                     .ToList())
            Connections.Remove(c);

        RefreshConnectionStates();
        RefreshSelectedNode();
    }

    private void RefreshConnectionStates()
    {
        foreach (var node in Nodes)
            foreach (var port in node.Input.Concat(node.Output))
                port.IsConnected = Connections.Any(c => ReferenceEquals(c.Output, port) || ReferenceEquals(c.Input, port));
    }

    private void RefreshSelectedNode() =>
        SelectedNode = Nodes.FirstOrDefault(n => n.IsSelected);

    // ── 项目文件（.rvsp）持久化：运行时图 ↔ DTO ──────────────────

    /// <summary>
    /// 把当前画布（节点、连线、分组、各节点参数、画布视图状态）快照为可序列化的
    /// <see cref="ProjectFile"/> DTO，供 <see cref="ProjectSerializer"/> 写入 JSON。
    /// </summary>
    /// <param name="projectName">项目名（通常取文件名，无扩展名）。</param>
    public ProjectFile ToProjectFile(string projectName)
    {
        var nodeDtos = Nodes.Select(vm => new NodeDto
        {
            Id          = vm.Model.Id,
            TypeId      = vm.Model.TypeId,
            DisplayName = vm.Model.DisplayName,
            X           = vm.Model.X,
            Y           = vm.Model.Y,
            Parameters  = vm.Parameters.ToDictionary(p => p.Name, p => p.SerializedValue),
        }).ToList();

        var connDtos = _graph.Connections.Select(c => new ConnectionDto
        {
            Id           = c.Id,
            SourceNodeId = c.SourceNodeId,
            SourcePort   = c.OutputPort.PortName,
            TargetNodeId = c.TargetNodeId,
            TargetPort   = c.InputPort.PortName,
        }).ToList();

        var groupDtos = Groups.Select(g => new GroupDto
        {
            Id     = g.Model.Id,
            Title  = g.Model.Title,
            X      = g.Model.X,
            Y      = g.Model.Y,
            Width  = g.Model.Width,
            Height = g.Model.Height,
            Color  = g.Model.Color,
        }).ToList();

        return new ProjectFile
        {
            ProjectName = projectName,
            Graph = new GraphDto
            {
                Nodes       = nodeDtos,
                Connections = connDtos,
                Groups      = groupDtos,
            },
        };
    }

    /// <summary>
    /// 清空当前画布并从 <see cref="ProjectFile"/> DTO 重建整张算子图：
    /// 按持久化 Id 重建节点（经算子库工厂实例化并还原参数）、重连连线、重建分组。
    /// 未知 TypeId 的节点会被跳过并通过 <paramref name="warn"/> 上报，不中断加载。
    /// </summary>
    /// <param name="project">已反序列化的项目 DTO。</param>
    /// <param name="warn">加载过程中的告警回调（如遇未知节点类型）。</param>
    public void LoadProjectFile(ProjectFile project, Action<string>? warn = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        Clear();

        var catalog = NodeLibrary.Operators.ToDictionary(o => o.TypeId);

        // 1) 重建节点（保留持久化 Id/坐标，还原参数）
        foreach (var nd in project.Graph.Nodes)
        {
            if (!catalog.TryGetValue(nd.TypeId, out var op))
            {
                warn?.Invoke($"未知节点类型「{nd.TypeId}」，已跳过（相关连线一并忽略）。");
                continue;
            }

            var node = new NodeModel
            {
                Id          = nd.Id,
                TypeId      = nd.TypeId,
                DisplayName = string.IsNullOrEmpty(nd.DisplayName) ? op.Name : nd.DisplayName,
                X           = nd.X,
                Y           = nd.Y,
                Instance    = op.Factory(),
            };
            var vm = AddNodeInternal(op, node);

            // 还原参数值（统一走字符串通道）
            foreach (var pvm in vm.Parameters)
                if (nd.Parameters.TryGetValue(pvm.Name, out var raw))
                    pvm.SerializedValue = raw;
        }

        // 2) 重建连线（按 节点Id + 端口名 定位连接点）
        foreach (var cd in project.Graph.Connections)
        {
            var from = Nodes.FirstOrDefault(n => n.Model.Id == cd.SourceNodeId);
            var to   = Nodes.FirstOrDefault(n => n.Model.Id == cd.TargetNodeId);
            if (from is null || to is null) continue;  // 节点被跳过时忽略其连线
            Connect(from, cd.SourcePort, to, cd.TargetPort);
        }

        // 3) 重建分组
        foreach (var gd in project.Graph.Groups)
        {
            var model = new GroupModel
            {
                Id     = gd.Id,
                Title  = gd.Title,
                X      = gd.X,
                Y      = gd.Y,
                Width  = gd.Width,
                Height = gd.Height,
                Color  = string.IsNullOrEmpty(gd.Color) ? "#0E639C" : gd.Color,
            };
            _graph.AddGroup(model);

            var gvm = new GroupViewModel(model);
            gvm.DeleteCommand = new RelayCommand<object>(_ => DeleteGroup(gvm));
            gvm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(GroupViewModel.Location))
                    TranslateMembers(gvm);
            };
            Groups.Add(gvm);
            Items.Add(gvm);
        }

        RecomputeMembership();
        RefreshSelectedNode();
    }

    /// <summary>清空画布上的全部节点、连线、分组与派生状态（新建/加载前复位）。</summary>
    public void Clear()
    {
        // 从底层图模型移除所有节点（会级联删连线）与分组
        foreach (var n in Nodes.ToList())
            _graph.RemoveNode(n.Model.Id);
        foreach (var g in Groups.ToList())
            _graph.RemoveGroup(g.Model.Id);

        Nodes.Clear();
        Groups.Clear();
        Items.Clear();
        Connections.Clear();
        _membership.Clear();
        _dragMembers.Clear();

        ImageOutput = null;
        CloudOutput = null;
        OnPropertyChanged(nameof(HasImageOutput));
        OnPropertyChanged(nameof(HasCloudOutput));
        RefreshSelectedNode();
    }

    // ── 执行：按拓扑序运行整图，并把输出路由到 2D/3D 视口 ────────

    /// <summary>运行整张算子图；log 回调用于把节点日志写入日志视图。</summary>
    public async Task ExecuteAsync(Action<string, string> log)
    {
        var store = new Dictionary<PortId, object?>();

        foreach (var node in _graph.TopologicalOrder())
        {
            if (node.Instance is null) continue;
            node.ExecutionState = NodeExecutionState.Running;

            // 沿每条连线把上游输出传播到下游输入
            foreach (var conn in _graph.Connections.Where(c => c.TargetNodeId == node.Id))
                store[conn.InputPort] = store.GetValueOrDefault(conn.OutputPort);

            var ctx = new UiContext(node, store, log);
            try
            {
                await node.Instance.ExecuteAsync(ctx, CancellationToken.None);
                node.ExecutionState = NodeExecutionState.Completed;
            }
            catch (Exception ex)
            {
                node.ExecutionState = NodeExecutionState.Error;
                log("error", $"{node.DisplayName} 执行失败: {ex.Message}");
            }
        }

        ExtractOutputs(store);
    }

    private void ExtractOutputs(Dictionary<PortId, object?> store)
    {
        BitmapImage? img = null;
        PointCloud? cloud = null;

        // 显示管线「末端」结果：优先取未连向任何下游输入的输出端口（叶子输出），
        // 这样 2D/3D 视图呈现的是整条流程的最终产物，而非上游原始数据。
        // 若没有任何末端输出（极端情况），再回退到任意可用输出。
        var connectedOutputs = _graph.Connections.Select(c => c.OutputPort).ToHashSet();

        bool TryCollect(bool leafOnly)
        {
            var found = false;
            foreach (var node in _graph.Nodes)
            {
                if (node.Instance is null) continue;
                foreach (var port in node.Instance.OutputPorts)
                {
                    var portId = new PortId(node.Id, port.Name);
                    if (leafOnly && connectedOutputs.Contains(portId)) continue;

                    switch (store.GetValueOrDefault(portId))
                    {
                        case byte[] bytes when img is null:
                            img = ImageHelper.BytesToBitmap(bytes);
                            found = true;
                            break;
                        case PointCloud c when cloud is null:
                            cloud = c;
                            found = true;
                            break;
                    }
                }
            }
            return found;
        }

        // 第一轮只看末端输出；若两类结果都没拿到，再放宽到全部输出兜底。
        TryCollect(leafOnly: true);
        if (img is null && cloud is null)
            TryCollect(leafOnly: false);

        ImageOutput = img;
        CloudOutput = cloud;
        OnPropertyChanged(nameof(HasImageOutput));
        OnPropertyChanged(nameof(HasCloudOutput));
    }

    /// <summary>执行上下文——把节点的 SetOutput 数据存进本地数据存储供 UI 读取。</summary>
    private sealed class UiContext(NodeModel node, Dictionary<PortId, object?> store, Action<string, string> log)
        : INodeExecutionContext
    {
        public IProgress<NodeProgress> Progress { get; } = new Progress<NodeProgress>(_ => { });

        public T GetInput<T>(string portName) =>
            (T)(store.GetValueOrDefault(new PortId(node.Id, portName))
                ?? throw new InvalidOperationException($"输入端口 '{portName}' 没有数据。"));

        public void SetOutput<T>(string portName, T value) =>
            store[new PortId(node.Id, portName)] = value;

        public void LogInfo(string m)    => log("info", m);
        public void LogWarning(string m) => log("warning", m);
        public void LogError(string m)   => log("error", m);
    }
}
