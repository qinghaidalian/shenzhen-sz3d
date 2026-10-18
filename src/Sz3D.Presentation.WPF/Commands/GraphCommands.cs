using System.Windows.Input;
using Sz3D.Core.Infrastructure.Graph;

namespace Sz3D.Presentation.WPF.Commands;

/// <summary>通用委托命令。</summary>
public sealed class RelayCommand<T>(Action<T?> execute, Predicate<T?>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add    => System.Windows.Input.CommandManager.RequerySuggested += value;
        remove => System.Windows.Input.CommandManager.RequerySuggested -= value;
    }
    public bool CanExecute(object? p) => canExecute?.Invoke((T?)p) ?? true;
    public void Execute(object? p)    => execute((T?)p);
}

// ── 画布命令实现 ──────────────────────────────────────────────

public sealed class RemoveNodeCommand(GraphModel graph, Guid nodeId) : IGraphCommand
{
    private NodeModel? _removed;
    public string Description => "删除节点";

    public void Execute()
    {
        _removed = graph.Nodes.FirstOrDefault(n => n.Id == nodeId);
        graph.RemoveNode(nodeId);
    }
    public void Undo()
    {
        if (_removed is not null) graph.AddNode(_removed);
    }
}

public sealed class AddNodeCommand(GraphModel graph, NodeModel node) : IGraphCommand
{
    public string Description => "添加节点";
    public void Execute() => graph.AddNode(node);
    public void Undo()    => graph.RemoveNode(node.Id);
}

public sealed class MoveNodeCommand(NodeModel node, double oldX, double oldY,
                                     double newX, double newY) : IGraphCommand
{
    public string Description => "移动节点";
    public void Execute() { node.X = newX; node.Y = newY; }
    public void Undo()    { node.X = oldX; node.Y = oldY; }
}
