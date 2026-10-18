using System.Windows;
using System.Windows.Input;
using Sz3D.Presentation.WPF.Services;
using Sz3D.Presentation.WPF.ViewModels;

namespace Sz3D.Presentation.WPF;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private Point _dragStart;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        _viewModel.Editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NodeEditorViewModel.CloudOutput))
                UpdateCloudVisual();
        };
    }

    private void UpdateCloudVisual()
    {
        var cloud = _viewModel.Editor.CloudOutput;
        CloudVisual.Content = cloud is not null && cloud.Count > 0
            ? PointCloudRenderer.BuildModel(cloud)
            : null;
    }

    // ── 算子列表 → 画布拖拽 ─────────────────────────────

    private void OnOperatorMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
    }

    private void OnOperatorMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (sender is FrameworkElement el && el.DataContext is OperatorDescriptor op)
            DragDrop.DoDragDrop(el, new DataObject(typeof(OperatorDescriptor), op), DragDropEffects.Copy);
    }

    private void OnDropOperator(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(OperatorDescriptor)) is OperatorDescriptor op)
        {
            var position = Editor.GetLocationInsideEditor(e);
            _viewModel.Editor.AddOperator(op, position);
        }
    }

}
