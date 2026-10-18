namespace Sz3D.Core.Infrastructure.Contracts;

/// <summary>节点执行期间由引擎注入，提供输入输出数据及日志访问。</summary>
public interface INodeExecutionContext
{
    T GetInput<T>(string portName);
    void SetOutput<T>(string portName, T value);
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message);
    IProgress<NodeProgress> Progress { get; }
}

/// <summary>节点执行进度报告。</summary>
public sealed record NodeProgress(int Current, int Total, string Message = "");
