using Sz3D.Core.Infrastructure.Graph;
using Sz3D.Core.Infrastructure.Contracts;

namespace Sz3D.Core.Infrastructure.Execution;

/// <summary>节点执行异常，携带出错节点信息。</summary>
public sealed class GraphExecutionException(NodeModel node, Exception inner)
    : Exception($"Node [{node.DisplayName}] execution failed: {inner.Message}", inner)
{
    public NodeModel FailedNode { get; } = node;
}

/// <summary>
/// 执行引擎——按 Wave 分组调度节点，支持顺序节点和并行 Thread 节点。
/// </summary>
public sealed class ExecutionEngine
{
    private readonly PortDataStore _dataStore = new();

    public event EventHandler<NodeExecutionProgressEventArgs>? ProgressChanged;
    public event EventHandler<NodeTimingEventArgs>?            NodeCompleted;

    public async Task RunAsync(GraphModel graph, CancellationToken ct)
    {
        _dataStore.Clear();
        var order = graph.TopologicalOrder();
        var waves = GroupIntoWaves(order, graph);
        int total = order.Count, completed = 0;

        foreach (var wave in waves)
        {
            ct.ThrowIfCancellationRequested();

            var sequential = wave.Where(n => n.Instance?.ExecutionMode == NodeExecutionMode.Sequential).ToList();
            var parallel   = wave.Where(n => n.Instance?.ExecutionMode == NodeExecutionMode.Parallel  ).ToList();

            foreach (var node in sequential)
            {
                await ExecuteNodeAsync(node, graph, ct);
                OnProgress(node, ++completed, total);
            }

            if (parallel.Count > 0)
            {
                await Task.WhenAll(parallel.Select(async n =>
                {
                    await ExecuteNodeAsync(n, graph, ct);
                    OnProgress(n, Interlocked.Increment(ref completed), total);
                }));
            }
        }

        ProgressChanged?.Invoke(this, new(null, completed, total, true));
    }

    private async Task ExecuteNodeAsync(NodeModel node, GraphModel graph, CancellationToken ct)
    {
        if (node.Instance is null) return;

        node.ExecutionState = NodeExecutionState.Running;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            PropagateInputs(node, graph);
            var ctx = new NodeExecutionContext(node, _dataStore);
            await node.Instance.ExecuteAsync(ctx, ct);
            PropagateOutputs(node, graph);
            node.ExecutionState = NodeExecutionState.Completed;
            sw.Stop();
            NodeCompleted?.Invoke(this, new(node, sw.Elapsed));
        }
        catch (OperationCanceledException) { node.ExecutionState = NodeExecutionState.Idle; throw; }
        catch (Exception ex)
        {
            node.ExecutionState = NodeExecutionState.Error;
            throw new GraphExecutionException(node, ex);
        }
    }

    private void PropagateInputs(NodeModel node, GraphModel graph)
    {
        foreach (var conn in graph.Connections.Where(c => c.TargetNodeId == node.Id))
            _dataStore.Transfer(conn.OutputPort, conn.InputPort);
    }

    private void PropagateOutputs(NodeModel node, GraphModel graph)
    {
        // 输出数据已写入 _dataStore，下游节点执行前由 PropagateInputs 读取
    }

    private void OnProgress(NodeModel node, int done, int total) =>
        ProgressChanged?.Invoke(this, new(node, done, total, false));

    // ── Wave 分组（BFS 分层）────────────────────────────────

    private static List<List<NodeModel>> GroupIntoWaves(
        IReadOnlyList<NodeModel> order, GraphModel graph)
    {
        var depth = new Dictionary<Guid, int>();
        foreach (var node in order)
        {
            var maxPredDepth = graph.Connections
                .Where(c => c.TargetNodeId == node.Id)
                .Select(c => depth.GetValueOrDefault(c.SourceNodeId, 0))
                .DefaultIfEmpty(-1).Max();
            depth[node.Id] = maxPredDepth + 1;
        }
        return order.GroupBy(n => depth[n.Id])
                    .OrderBy(g => g.Key)
                    .Select(g => g.ToList())
                    .ToList();
    }
}

// ── 辅助事件参数 ─────────────────────────────────────────────

public sealed record NodeExecutionProgressEventArgs(
    NodeModel? CurrentNode, int Completed, int Total, bool IsFinished);

public sealed record NodeTimingEventArgs(NodeModel Node, TimeSpan Elapsed);

// ── 端口数据存储 ──────────────────────────────────────────────

internal sealed class PortDataStore
{
    private readonly Dictionary<PortId, object?> _store = [];

    public void Set(PortId port, object? value) => _store[port] = value;
    public object? Get(PortId port) => _store.GetValueOrDefault(port);
    public void Transfer(PortId from, PortId to) => _store[to] = Get(from);
    public void Clear() => _store.Clear();
}

// ── NodeExecutionContext 实现 ─────────────────────────────────

internal sealed class NodeExecutionContext(NodeModel node, PortDataStore store)
    : INodeExecutionContext
{
    public IProgress<NodeProgress> Progress { get; } =
        new Progress<NodeProgress>(_ => { }); // TODO: 接入 UI 进度

    public T GetInput<T>(string portName) =>
        (T)(store.Get(new PortId(node.Id, portName)) ?? throw new InvalidOperationException($"Input '{portName}' is null."));

    public void SetOutput<T>(string portName, T value) =>
        store.Set(new PortId(node.Id, portName), value);

    public void LogInfo   (string msg) => Console.WriteLine($"[INFO]  [{node.DisplayName}] {msg}");
    public void LogWarning(string msg) => Console.WriteLine($"[WARN]  [{node.DisplayName}] {msg}");
    public void LogError  (string msg) => Console.WriteLine($"[ERROR] [{node.DisplayName}] {msg}");
}

