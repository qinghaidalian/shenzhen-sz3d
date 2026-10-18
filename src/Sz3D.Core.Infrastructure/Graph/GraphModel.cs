using System.Collections.ObjectModel;
using Sz3D.Core.Infrastructure.Common;

namespace Sz3D.Core.Infrastructure.Graph;

/// <summary>端口唯一标识符。</summary>
public readonly record struct PortId(Guid NodeId, string PortName);

/// <summary>画布上的节点实例。</summary>
public sealed class NodeModel
{
    public Guid   Id          { get; init; } = Guid.NewGuid();
    public string TypeId      { get; init; } = "";   // "{PluginId}:{TypeName}"
    public string DisplayName { get; set;  } = "";
    public double X           { get; set;  }
    public double Y           { get; set;  }

    /// <summary>当前执行状态，供 Canvas 高亮绑定。</summary>
    public NodeExecutionState ExecutionState { get; set; } = NodeExecutionState.Idle;

    public NodeBase? Instance { get; set; }
}

public enum NodeExecutionState { Idle, Running, Completed, Error }

/// <summary>两个端口之间的有向连接。</summary>
public sealed class ConnectionModel
{
    public Guid   Id           { get; init; } = Guid.NewGuid();
    public PortId OutputPort   { get; init; }
    public PortId InputPort    { get; init; }
    public Guid   SourceNodeId => OutputPort.NodeId;
    public Guid   TargetNodeId => InputPort.NodeId;
}

/// <summary>
/// 核心图模型——纯 C# 数据结构，不依赖 WPF。
/// 维护节点集合与连接集合，提供防环检测与拓扑排序。
/// </summary>
public sealed class GraphModel
{
    public ObservableCollection<NodeModel>      Nodes       { get; } = [];
    public ObservableCollection<ConnectionModel> Connections { get; } = [];

    /// <summary>画布上的分组矩形集合。</summary>
    public ObservableCollection<GroupModel> Groups { get; } = [];

    // ── 节点操作 ─────────────────────────────────────────────

    public Result AddNode(NodeModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        Nodes.Add(node);
        return Result.Ok();
    }

    public Result RemoveNode(Guid nodeId)
    {
        var node = Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null) return Result.Fail($"Node {nodeId} not found.");

        // 级联删除所有关联连接
        var toRemove = Connections
            .Where(c => c.SourceNodeId == nodeId || c.TargetNodeId == nodeId)
            .ToList();
        foreach (var c in toRemove) Connections.Remove(c);

        Nodes.Remove(node);
        return Result.Ok();
    }

    // ── 分组操作 ─────────────────────────────────────────────

    /// <summary>向图中添加一个分组。</summary>
    /// <param name="group">待添加的分组。</param>
    /// <returns>添加成功的 <see cref="Result"/>。</returns>
    public Result AddGroup(GroupModel group)
    {
        ArgumentNullException.ThrowIfNull(group);
        Groups.Add(group);
        return Result.Ok();
    }

    /// <summary>
    /// 按 Id 移除分组。仅从 <see cref="Groups"/> 中删除分组本身，
    /// 不触碰 <see cref="Nodes"/> 与 <see cref="Connections"/>（分组内节点保留）。
    /// </summary>
    /// <param name="groupId">待移除分组的标识符。</param>
    /// <returns>找到并移除返回成功；未找到返回失败的 <see cref="Result"/>。</returns>
    public Result RemoveGroup(Guid groupId)
    {
        var group = Groups.FirstOrDefault(g => g.Id == groupId);
        if (group is null) return Result.Fail($"Group {groupId} not found.");

        Groups.Remove(group);
        return Result.Ok();
    }

    // ── 连接操作 ─────────────────────────────────────────────

    public Result<ConnectionModel> Connect(PortId output, PortId input)
    {
        if (WouldCreateCycle(output, input))
            return Result<ConnectionModel>.Fail("不允许循环连接");

        // 替换已存在的同 Input Port 连接（每个 Input Port 只允许一条连接）
        var existing = Connections.FirstOrDefault(c => c.InputPort == input);
        if (existing is not null) Connections.Remove(existing);

        var conn = new ConnectionModel { OutputPort = output, InputPort = input };
        Connections.Add(conn);
        return Result<ConnectionModel>.Ok(conn);
    }

    public Result RemoveConnection(Guid connectionId)
    {
        var conn = Connections.FirstOrDefault(c => c.Id == connectionId);
        if (conn is null) return Result.Fail($"Connection {connectionId} not found.");
        Connections.Remove(conn);
        return Result.Ok();
    }

    // ── 环路检测（DFS）────────────────────────────────────────

    public bool WouldCreateCycle(PortId from, PortId to)
    {
        // 若从 to.NodeId 出发能到达 from.NodeId，则加边会成环
        return IsReachable(to.NodeId, from.NodeId, []);
    }

    private bool IsReachable(Guid start, Guid target, HashSet<Guid> visited)
    {
        if (start == target) return true;
        if (!visited.Add(start)) return false;
        foreach (var conn in Connections.Where(c => c.SourceNodeId == start))
            if (IsReachable(conn.TargetNodeId, target, visited)) return true;
        return false;
    }

    // ── 拓扑排序（Kahn 算法）─────────────────────────────────

    public IReadOnlyList<NodeModel> TopologicalOrder()
    {
        var inDegree = Nodes.ToDictionary(n => n.Id, _ => 0);
        foreach (var conn in Connections)
            inDegree[conn.TargetNodeId]++;

        var queue  = new Queue<NodeModel>(Nodes.Where(n => inDegree[n.Id] == 0));
        var result = new List<NodeModel>();

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            result.Add(node);
            foreach (var conn in Connections.Where(c => c.SourceNodeId == node.Id))
            {
                if (--inDegree[conn.TargetNodeId] == 0)
                    queue.Enqueue(Nodes.First(n => n.Id == conn.TargetNodeId));
            }
        }

        if (result.Count != Nodes.Count)
            throw new InvalidOperationException("Graph contains a cycle — topological sort failed.");

        return result;
    }

    // ── 辅助查询 ──────────────────────────────────────────────

    public IEnumerable<NodeModel> GetDownstream(NodeModel node) =>
        Connections.Where(c => c.SourceNodeId == node.Id)
                   .Select(c => Nodes.First(n => n.Id == c.TargetNodeId));
}
