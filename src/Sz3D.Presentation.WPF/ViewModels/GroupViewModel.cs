using System.Windows;
using Sz3D.Core.Infrastructure.Graph;

namespace Sz3D.Presentation.WPF.ViewModels;

/// <summary>Nodify 画布上的一个分组块——包装 <see cref="GroupModel"/>，用 <c>nodify:GroupingNode</c> 渲染。</summary>
public sealed class GroupViewModel : CanvasItemViewModel
{
    /// <summary>用给定的分组模型构造视图模型，并同步初始位置。</summary>
    /// <param name="model">被包装的分组模型。</param>
    public GroupViewModel(GroupModel model)
    {
        Model    = model;
        Location = new Point(model.X, model.Y);
    }

    /// <summary>被包装的分组模型。</summary>
    public GroupModel Model { get; }

    /// <summary>分组标题，setter 回写模型。</summary>
    public string Title
    {
        get => Model.Title;
        set
        {
            if (Model.Title == value) return;
            Model.Title = value;
            OnPropertyChanged();
        }
    }

    /// <summary>分组尺寸，供 <c>GroupingNode.ActualSize</c> 双向绑定，setter 回写模型宽高。</summary>
    public Size Size
    {
        get => new(Model.Width, Model.Height);
        set
        {
            if (Model.Width == value.Width && Model.Height == value.Height) return;
            Model.Width  = value.Width;
            Model.Height = value.Height;
            OnPropertyChanged();
        }
    }

    /// <summary>分组 header 颜色（十六进制字符串）。</summary>
    public string Color => Model.Color;

    /// <summary>位置变更时回写模型的画布坐标。</summary>
    protected override void OnLocationChanged(Point value)
    {
        Model.X = value.X;
        Model.Y = value.Y;
    }

    private Point? _dragOrigin;

    /// <summary>拖拽开始时记录的分组起始原点；非 <c>null</c> 表示正处于拖拽会话中。</summary>
    internal Point? DragOrigin => _dragOrigin;

    /// <summary>开始一次拖拽会话，把当前位置记录为起点，供成员平移计算 delta。</summary>
    internal void BeginDrag() => _dragOrigin = Location;

    /// <summary>结束当前拖拽会话，清空起点。</summary>
    internal void EndDrag() => _dragOrigin = null;
}
