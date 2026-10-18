# 构建与测试指南

## 构建项目

```powershell
# 还原依赖
dotnet restore RVS.sln

# 构建整个解决方案
dotnet build RVS.sln

# 构建 Release 版本
dotnet build RVS.sln -c Release
```

## 运行应用

> **注意**：不要用 `dotnet run` 启动 WPF 应用，请在 Visual Studio / Rider 中直接运行，或：

```powershell
dotnet build RVS.App -c Debug
# 然后直接运行生成的 .exe
.\RVS.App\bin\Debug\net10.0-windows\RVS.App.exe
```

## 运行测试

```powershell
# 运行所有测试（单次，不进入 watch 模式）
dotnet test RVS.Tests --no-build

# 只运行单元测试（排除集成测试）
dotnet test RVS.Tests --filter "Category!=Integration"

# 只运行属性测试（FsCheck）
dotnet test RVS.Tests --filter "Category=Property"

# 只运行某个测试类
dotnet test RVS.Tests --filter "FullyQualifiedName~GraphModelTests"

# 运行测试并生成覆盖率报告
dotnet test RVS.Tests --collect:"XPlat Code Coverage"
```

## 性能基准测试

```powershell
# 运行 BenchmarkDotNet 基准（必须用 Release 配置）
dotnet run --project RVS.Benchmarks -c Release
```

## 插件开发与热重载

1. 新建类库项目，引用 `RVS.PluginContract` NuGet 包
2. 实现 `INodePlugin` 和若干 `NodeBase` 子类
3. 构建后将 DLL 复制到 `./plugins/` 目录
4. 在运行中的 RVS 应用中点击"重新扫描插件"，或：

```powershell
# 构建插件并复制到插件目录（开发期快捷脚本）
dotnet build MyPlugin -c Debug -o .\RVS.App\bin\Debug\net10.0-windows\plugins\
```

## 常用开发命令

```powershell
# 添加 NuGet 包到指定项目
dotnet add RVS.Core package <PackageName> --version <Version>

# 查看解决方案结构
dotnet sln RVS.sln list

# 清理构建产物
dotnet clean RVS.sln

# 格式化代码（需 dotnet-format 工具）
dotnet format RVS.sln
```

## 代码质量检查

```powershell
# 静态分析（Roslyn Analyzers 随构建自动运行）
dotnet build RVS.sln /p:TreatWarningsAsErrors=true

# 检查是否有过时 API
dotnet build RVS.sln /p:EnableNETAnalyzers=true
```

## 常见问题

- **WPF 控件在测试中不可用**：UI 相关测试使用 WPF TestHost，或通过 `Dispatcher.Invoke` 包装
- **HelixToolkit.SharpDX 需要 DirectX 11**：确保开发机支持 DX11，VM/远程桌面环境可能需要软件光栅化
- **AssemblyLoadContext 卸载**：插件 DLL 卸载前须确保所有引用该程序集的对象已被 GC，否则卸载会静默失败
