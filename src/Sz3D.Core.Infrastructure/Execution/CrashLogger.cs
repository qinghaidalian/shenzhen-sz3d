namespace Sz3D.Core.Infrastructure.Execution;

public static class CrashLogger
{
    private static readonly string LogDir =
        Path.Combine(AppContext.BaseDirectory, "logs");

    public static void Write(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            var path = Path.Combine(LogDir,
                $"crash_{DateTime.Now:yyyy-MM-dd_HHmmss}.log");
            var content = $"""
                Timestamp : {DateTime.Now:O}
                .NET      : {Environment.Version}
                OS        : {Environment.OSVersion}

                {ex}
                """;
            File.WriteAllText(path, content);
        }
        catch { /* 崩溃日志写入失败时静默处理 */ }
    }
}
