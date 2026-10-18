namespace Sz3D.Core.Infrastructure.Graph;

/// <summary>画布操作命令接口。</summary>
public interface IGraphCommand
{
    void Execute();
    void Undo();
    string Description { get; }
}

/// <summary>
/// 撤销/重做管理器，使用环形缓冲实现固定容量历史。
/// 默认最大 100 步，最多 200 步。
/// </summary>
public sealed class UndoRedoManager
{
    private const int DefaultCapacity = 100;
    private readonly LinkedList<IGraphCommand> _undoStack = new();
    private readonly Stack<IGraphCommand>      _redoStack = new();
    private readonly int _maxSteps;

    public UndoRedoManager(int maxSteps = DefaultCapacity)
    {
        _maxSteps = Math.Clamp(maxSteps, 50, 200);
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>执行命令并推入撤销栈，同时清空重做栈。</summary>
    public void Execute(IGraphCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Execute();
        _undoStack.AddLast(command);
        if (_undoStack.Count > _maxSteps) _undoStack.RemoveFirst();
        _redoStack.Clear();
    }

    public void Undo()
    {
        if (!CanUndo) return;
        var cmd = _undoStack.Last!.Value;
        _undoStack.RemoveLast();
        cmd.Undo();
        _redoStack.Push(cmd);
    }

    public void Redo()
    {
        if (!CanRedo) return;
        var cmd = _redoStack.Pop();
        cmd.Execute();
        _undoStack.AddLast(cmd);
    }
}

/// <summary>将多个命令组合为单一撤销步骤。</summary>
public sealed class BatchCommand(IReadOnlyList<IGraphCommand> commands, string description) : IGraphCommand
{
    public string Description => description;
    public void Execute() { foreach (var c in commands) c.Execute(); }
    public void Undo()    { foreach (var c in commands.Reverse()) c.Undo(); }
}
