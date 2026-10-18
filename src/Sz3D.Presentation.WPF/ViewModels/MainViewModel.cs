using System.Collections.ObjectModel;
using System.Windows.Input;
using Sz3D.Presentation.WPF.Commands;
using Sz3D.Presentation.WPF.Services;

namespace Sz3D.Presentation.WPF.ViewModels;

/// <summary>日志视图中的一行。</summary>
public sealed record LogEntry(string Timestamp, string Channel, string Level, string Message);

/// <summary>主窗口 DataContext。</summary>
public sealed class MainViewModel : ViewModelBase
{
    public NodeEditorViewModel Editor { get; } = new();
    public IReadOnlyList<OperatorGroup> OperatorGroups => NodeLibrary.Groups;

    public ObservableCollection<LogEntry> Logs { get; } =
    [
        new("2026-10-17 14:44:23.646028", "rvs_kernel", "info", "--Plugin 信息"),
        new("2026-10-17 14:44:23.646034", "rvs_kernel", "info", "名称=rvs_algorithms，版本=1.0.0，算子数量=6"),
        new("2026-10-17 14:44:23.646041", "rvs_kernel", "info", "名称=rvs_cameras，版本=1.0.0，算子数量=1"),
    ];

    private string _statusText = "已停止";
    public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

    public ICommand NewCommand     { get; }
    public ICommand LoadCommand    { get; }
    public ICommand SaveCommand    { get; }
    public ICommand SaveAsCommand  { get; }
    public ICommand RunCommand     { get; }
    public ICommand StopCommand    { get; }
    public ICommand RunOnceCommand { get; }

    public MainViewModel()
    {
        NewCommand     = new RelayCommand<object>(_ => { /* TODO */ });
        LoadCommand    = new RelayCommand<object>(_ => { /* TODO */ });
        SaveCommand    = new RelayCommand<object>(_ => { /* TODO */ });
        SaveAsCommand  = new RelayCommand<object>(_ => { /* TODO */ });
        RunCommand     = new RelayCommand<object>(async _ => await RunGraphAsync());
        StopCommand    = new RelayCommand<object>(_ => StatusText = "已停止");
        RunOnceCommand = new RelayCommand<object>(async _ => await RunGraphAsync());
    }

    private async Task RunGraphAsync()
    {
        StatusText = "运行中";
        try
        {
            await Editor.ExecuteAsync((level, msg) =>
                Logs.Add(new LogEntry(Now(), "rvs_kernel", level, msg)));
        }
        catch (Exception ex)
        {
            Logs.Add(new LogEntry(Now(), "rvs_kernel", "error", ex.Message));
        }
        StatusText = "已停止";
    }

    private static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffffff");
}
