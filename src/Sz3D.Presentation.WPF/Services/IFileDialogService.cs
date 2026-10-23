namespace Sz3D.Presentation.WPF.Services;

/// <summary>
/// 文件对话框服务——把打开/保存对话框从 ViewModel 中隔离，
/// 使 ViewModel 不直接依赖 WPF UI 类型（遵循 MVVM 规范）。
/// </summary>
public interface IFileDialogService
{
    /// <summary>
    /// 弹出「打开」对话框，让用户选择一个项目文件。
    /// </summary>
    /// <returns>选中文件的完整路径；用户取消时返回 <c>null</c>。</returns>
    string? OpenProjectFile();

    /// <summary>
    /// 弹出「另存为」对话框，让用户指定项目文件保存路径。
    /// </summary>
    /// <param name="suggestedFileName">建议的默认文件名（含扩展名）。</param>
    /// <returns>用户确认的完整路径；取消时返回 <c>null</c>。</returns>
    string? SaveProjectFileAs(string suggestedFileName);
}
