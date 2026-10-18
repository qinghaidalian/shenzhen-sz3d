using System.Windows;
using Sz3D.Core.Infrastructure.Execution;

namespace Sz3D.Presentation.WPF;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            CrashLogger.Write(args.Exception);
            MessageBox.Show("程序遇到错误，已保存崩溃日志", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            CrashLogger.Write((Exception)args.ExceptionObject);

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            // TODO: 接入日志服务
            args.SetObserved();
        };
    }
}
