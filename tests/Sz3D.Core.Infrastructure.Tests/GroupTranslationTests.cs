using Sz3D.Core.Infrastructure.Graph;
using Xunit;

namespace Sz3D.Core.Infrastructure.Tests;

public sealed class GroupTranslationTests
{
    // ── GroupTranslation.Translate ───────────────────────────

    [Fact]
    public void Translate_PositiveDelta_ReturnsShiftedPoint()
    {
        var (x, y) = GroupTranslation.Translate(10, 20, 5, 7);
        Assert.Equal(15, x);
        Assert.Equal(27, y);
    }

    [Fact]
    public void Translate_NegativeDelta_ReturnsShiftedPoint()
    {
        var (x, y) = GroupTranslation.Translate(10, 20, -4, -8);
        Assert.Equal(6, x);
        Assert.Equal(12, y);
    }

    [Fact]
    public void Translate_ZeroDelta_ReturnsSamePoint()
    {
        var (x, y) = GroupTranslation.Translate(10, 20, 0, 0);
        Assert.Equal(10, x);
        Assert.Equal(20, y);
    }

    // ── GroupTranslation.ApplyDelta ──────────────────────────

    [Fact]
    public void ApplyDelta_ShiftsAllMembersBySameDelta()
    {
        var a = new NodeModel { X = 10, Y = 10 };
        var b = new NodeModel { X = 50, Y = 80 };

        GroupTranslation.ApplyDelta([a, b], 100, -20);

        Assert.Equal(110, a.X);
        Assert.Equal(-10, a.Y);
        Assert.Equal(150, b.X);
        Assert.Equal(60, b.Y);
    }

    [Fact]
    public void ApplyDelta_EmptyMembers_IsNoOp()
    {
        var ex = Record.Exception(() => GroupTranslation.ApplyDelta([], 5, 5));
        Assert.Null(ex);
    }

    [Fact]
    public void ApplyDelta_ZeroDelta_LeavesCoordinatesUnchanged()
    {
        var node = new NodeModel { X = 42, Y = 99 };
        GroupTranslation.ApplyDelta([node], 0, 0);
        Assert.Equal(42, node.X);
        Assert.Equal(99, node.Y);
    }

    [Fact]
    public void ApplyDelta_PreservesRelativePositions()
    {
        var a = new NodeModel { X = 0, Y = 0 };
        var b = new NodeModel { X = 30, Y = 40 };

        GroupTranslation.ApplyDelta([a, b], 12, 34);

        // 平移后两成员相对距离不变
        Assert.Equal(30, b.X - a.X);
        Assert.Equal(40, b.Y - a.Y);
    }
}
