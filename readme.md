# Sz3D — 3D Machine Vision Development Platform

参照图漾科技 RobotVisionSuite（RVS）开发的工业级 3D 机器视觉可视化节点编程平台。

## 技术栈
- **框架**: .NET 9（目标升级至 .NET 10）/ WPF / C# 13
- **节点编辑器**: Nodify
- **3D 渲染**: HelixToolkit.SharpDX（DirectX 11）
- **停靠布局**: AvalonDock
- **深度学习推理**: Microsoft.ML.OnnxRuntime（ONNX 格式）

## 项目结构

```
src/
  Sz3D.Core.Infrastructure    # 插件契约、图模型、执行引擎、数据模型
  Sz3D.Core.Operators         # 基础算子（Trigger/Load/File/Conditional）
  Sz3D.Plugins.Algorithms     # 图像处理/标定/通讯算子插件
  Sz3D.Plugins.Cameras        # 图漾相机 SDK 封装插件
  Sz3D.Plugins.Robots         # 机器人接口插件（Modbus TCP / ABB / KUKA）
  Sz3D.Backend.AiService      # ONNX 推理插件
  Sz3D.Presentation.WPF       # WPF 主程序（UI / ViewModel / Theme）
tests/
  Sz3D.Core.Infrastructure.Tests
  Sz3D.Core.Operators.Tests
```

## 快速开始

```powershell
dotnet restore Sz3D.sln
dotnet build   Sz3D.sln
dotnet test    Sz3D.sln
```
