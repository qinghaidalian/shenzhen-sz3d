using Sz3D.Core.Infrastructure.Graph;
using Xunit;

namespace Sz3D.Core.Infrastructure.Tests;

public sealed class GraphModelTests
{
    private static GraphModel MakeGraph() => new();

    [Fact]
    public void AddNode_IncreasesNodeCount()
    {
        var g    = MakeGraph();
        var node = new NodeModel { DisplayName = "Test" };
        g.AddNode(node);
        Assert.Single(g.Nodes);
    }

    [Fact]
    public void RemoveNode_DeletesConnections()
    {
        var g = MakeGraph();
        var a = new NodeModel { Id = Guid.NewGuid(), DisplayName = "A" };
        var b = new NodeModel { Id = Guid.NewGuid(), DisplayName = "B" };
        g.AddNode(a); g.AddNode(b);
        g.Connect(new(a.Id, "Out"), new(b.Id, "In"));
        Assert.Single(g.Connections);

        g.RemoveNode(a.Id);
        Assert.Empty(g.Connections);   // Property 8: 节点删除级联清理
    }

    [Fact]
    public void Connect_CycleIsRejected()
    {
        var g = MakeGraph();
        var a = new NodeModel { Id = Guid.NewGuid() };
        var b = new NodeModel { Id = Guid.NewGuid() };
        g.AddNode(a); g.AddNode(b);
        g.Connect(new(a.Id, "Out"), new(b.Id, "In"));

        // 反向连接会形成环
        var result = g.Connect(new(b.Id, "Out"), new(a.Id, "In"));
        Assert.False(result.IsSuccess);   // Property 4: 循环连接拒绝
    }

    [Fact]
    public void TopologicalOrder_RespectsEdgeDirection()
    {
        var g = MakeGraph();
        var a = new NodeModel { Id = Guid.NewGuid(), DisplayName = "A" };
        var b = new NodeModel { Id = Guid.NewGuid(), DisplayName = "B" };
        var c = new NodeModel { Id = Guid.NewGuid(), DisplayName = "C" };
        g.AddNode(a); g.AddNode(b); g.AddNode(c);
        g.Connect(new(a.Id, "Out"), new(b.Id, "In"));
        g.Connect(new(b.Id, "Out"), new(c.Id, "In"));

        var order = g.TopologicalOrder();
        Assert.Equal(a.Id, order[0].Id);  // Property 13: 拓扑排序依赖顺序
        Assert.Equal(b.Id, order[1].Id);
        Assert.Equal(c.Id, order[2].Id);
    }
}
