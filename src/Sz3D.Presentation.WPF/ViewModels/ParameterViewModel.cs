using System.Globalization;
using System.Reflection;
using Sz3D.Core.Infrastructure;

namespace Sz3D.Presentation.WPF.ViewModels;

/// <summary>属性面板中的一个可编辑参数（节点上带 [NodeParameter] 的属性）。</summary>
public sealed class ParameterViewModel : ViewModelBase
{
    private readonly NodeBase _target;
    private readonly PropertyInfo _prop;

    public ParameterViewModel(NodeBase target, PropertyInfo prop)
    {
        _target = target;
        _prop   = prop;
    }

    public string Name => _prop.Name;
    public bool IsBool => _prop.PropertyType == typeof(bool);

    /// <summary>文本型参数（int/double/float/string 等）。</summary>
    public string Text
    {
        get => _prop.GetValue(_target)?.ToString() ?? "";
        set
        {
            try
            {
                var converted = Convert.ChangeType(value, _prop.PropertyType, CultureInfo.InvariantCulture);
                _prop.SetValue(_target, converted);
            }
            catch { /* 忽略非法输入，回显原值 */ }
            OnPropertyChanged();
        }
    }

    /// <summary>布尔型参数。</summary>
    public bool BoolValue
    {
        get => (bool)(_prop.GetValue(_target) ?? false);
        set { _prop.SetValue(_target, value); OnPropertyChanged(); }
    }
}
