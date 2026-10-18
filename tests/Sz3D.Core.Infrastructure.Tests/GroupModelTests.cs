using Sz3D.Core.Infrastructure.Graph;
using Xunit;

namespace Sz3D.Core.Infrastructure.Tests;

public sealed class GroupModelTests
{
    private static GroupModel MakeGroup(double x, double y, double w, double h) =>
        new() { X = x, Y = y, Width = w, Height = h };

    // ── GroupMembership.Contains ─────────────────────────────

    [Fact]
    public void Contains_PointInside_ReturnsTrue()
    {
        var group = MakeGroup(0, 0, 100, 100);
        Assert.True(GroupMembership.Contains(group, 50, 50));
    }

    [Fact]
    public void Contains_PointOutside_ReturnsFalse()
    {
        var group = MakeGroup(0, 0, 100, 100);
        Assert.False(GroupMembership.Contains(group, 150, 50));
        Assert.False(GroupMembership.Contains(group, 50, -10));
    }

    [Fact]
    public void Contains_BoundaryPoints_AreIncluded()
    {
        var group = MakeGroup(0, 0, 100, 100);
        Assert.True(GroupMembership.Contains(group, 0, 50));     // 左边界
        Assert.True(GroupMembership.Contains(group, 100, 50));   // 右边界
        Assert.True(GroupMembership.Contains(group, 50, 0));     // 上边界
        Assert.True(GroupMembership.Contains(group, 50, 100));   // 下边界
    }

    // ── GroupMembership.MembersOf ────────────────────────────

    [Fact]
    public void MembersOf_FiltersNodesInsideRectangle()
    {
        var group = MakeGroup(0, 0, 100, 100);
        var inside1 = new NodeModel { X = 10, Y = 10 };
        var inside2 = new NodeModel { X = 90, Y = 90 };
        var outside = new NodeModel { X = 200, Y = 10 };

        var members = GroupMembership.MembersOf(group, [inside1, inside2, outside]);

        Assert.Equal(2, members.Count);
        Assert.Contains(inside1, members);
        Assert.Contains(inside2, members);
        Assert.DoesNotContain(outside, members);
    }

    // ── GroupMembership.GroupContaining ──────────────────────

    [Fact]
    public void GroupContaining_NoOverlap_ReturnsNull()
    {
        var group = MakeGroup(0, 0, 50, 50);
        Assert.Null(GroupMembership.GroupContaining([group], 500, 500));
    }

    [Fact]
    public void GroupContaining_SingleContainer_ReturnsThatGroup()
    {
        var group = MakeGroup(0, 0, 100, 100);
        Assert.Same(group, GroupMembership.GroupContaining([group], 50, 50));
    }

    [Fact]
    public void GroupContaining_MultipleOverlap_ReturnsSmallestArea()
    {
        var big   = MakeGroup(0, 0, 200, 200);   // 面积 40000
        var small = MakeGroup(0, 0, 100, 100);   // 面积 10000

        var result = GroupMembership.GroupContaining([big, small], 50, 50);

        Assert.Same(small, result);
    }

    // ── GraphModel.AddGroup / RemoveGroup ────────────────────

    [Fact]
    public void AddGroup_IncreasesGroupCount()
    {
        var g = new GraphModel();
        g.AddGroup(MakeGroup(0, 0, 100, 100));
        Assert.Single(g.Groups);
    }

    [Fact]
    public void RemoveGroup_KeepsMemberNodes()
    {
        var g     = new GraphModel();
        var group = MakeGroup(0, 0, 100, 100);
        var node  = new NodeModel { X = 50, Y = 50 };
        g.AddNode(node);
        g.AddGroup(group);

        var result = g.RemoveGroup(group.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(g.Groups);
        Assert.Single(g.Nodes);          // 分组内节点仍保留
        Assert.Contains(node, g.Nodes);
    }

    [Fact]
    public void RemoveGroup_UnknownId_ReturnsFailure()
    {
        var g      = new GraphModel();
        var result = g.RemoveGroup(Guid.NewGuid());
        Assert.False(result.IsSuccess);
    }
}
