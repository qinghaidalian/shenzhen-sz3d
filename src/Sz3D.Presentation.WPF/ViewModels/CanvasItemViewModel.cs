using System.Windows;
using System.Windows.Input;

namespace Sz3D.Presentation.WPF.ViewModels;

/// <summary>
/// 画布项的抽象基类——节点与分组共存于 Nodify 的异构 <c>ItemsSource</c> 时的公共契约。
/// 暴露位置、选中状态与删除命令，供 <c>ItemContainerStyle</c> 统一绑定。
/// </summary>
public abstract class CanvasItemViewModel : ViewModelBase
{
    private Point _location;

    /// <summary>画布坐标系中的位置（左上角）。</summary>
    public virtual Point Location
    {
        get => _location;
        set
        {
            if (Set(ref _location, value))
                OnLocationChanged(value);
        }
    }

    private bool _isSelected;

    /// <summary>画布项是否被选中。</summary>
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

    /// <summary>删除当前画布项（由画布控制器注入）。</summary>
    public ICommand DeleteCommand { get; internal set; } = null!;

    /// <summary>位置变更后的受保护钩子，派生类可据此回写各自的模型坐标。</summary>
    /// <param name="value">新的画布坐标。</param>
    protected virtual void OnLocationChanged(Point value) { }
}
