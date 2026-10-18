using Sz3D.Core.Infrastructure.Graph;
using Xunit;

namespace Sz3D.Core.Infrastructure.Tests;

// 简单内联命令，测试专用
file sealed class AddNodeCmd(GraphModel g, NodeModel n) : IGraphCommand
{
    private bool _added;
    public string Description => "Add";
    public void Execute() { g.AddNode(n); _added = true; }
    public void Undo()    { if (_added) g.RemoveNode(n.Id); }
}

public sealed class UndoRedoManagerTests
{
    [Fact]
    public void Undo_RestoresState()
    {
        var g    = new GraphModel();
        var mgr  = new UndoRedoManager();
        var node = new NodeModel { DisplayName = "N1" };

        mgr.Execute(new AddNodeCmd(g, node));
        Assert.Single(g.Nodes);

        mgr.Undo();
        Assert.Empty(g.Nodes);
    }

    [Fact]
    public void Redo_ReappliesCommand()
    {
        var g    = new GraphModel();
        var mgr  = new UndoRedoManager();
        var node = new NodeModel { DisplayName = "N1" };

        mgr.Execute(new AddNodeCmd(g, node));
        mgr.Undo();
        mgr.Redo();
        Assert.Single(g.Nodes);
    }
}
