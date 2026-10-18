# 技术栈说明

## 项目信息

- **项目名称**：Robot Vision Suite（RVS）
- **目标平台**：Windows（WPF 桌面应用）
- **框架**：.NET 10，目标框架 `net10.0-windows`
- **语言**：C# 13

## 核心依赖库

| 库 | 用途 | 说明 |
|---|---|---|
| **Nodify** | 节点编辑器画布 | WPF 专用，MVVM 友好，`NodifyEditor.MinZoom=0.1`/`MaxZoom=10.0` |
| **HelixToolkit.SharpDX.Wpf** | 3D 点云/Pose 渲染 | DirectX 11，原生 WPF 集成，无需 HwndHost；用 `Viewport3DX` + `PointGeometryModel3D` |
| **AvalonDock** (Xceed) | 停靠面板布局 | 布局序列化至 `./config/layout.xml` |
| **Microsoft.ML.OnnxRuntime** | 深度学习推理 | 运行于独立 AiService 进程，`InferenceSession` 在服务端懒加载 |
| **Grpc.AspNetCore** | AI 推理后端服务 | `Sz3D.Backend.AiService` 进程暴露 gRPC 接口（LoadModel / Infer / …） |
| **Grpc.Net.Client** · **Google.Protobuf** · **Grpc.Tools** | gRPC 客户端 | `DeepLearningPlugin` 节点通过 gRPC stub 调用 AiService，proto 生成客户端代码 |
| **NModbus** | Modbus TCP 通讯 | 工业通讯节点 |
| **System.Text.Json** | 项目文件序列化 | .rvsp 文件格式（JSON），不用 Newtonsoft.Json |

## 测试框架

| 库 | 用途 |
|---|---|
| **xUnit** | 单元测试 + 集成测试主框架 |
| **Moq** | Mock 外部依赖（相机 SDK、机器人协议等） |
| **FsCheck.Xunit** | 属性测试（PBT），每条属性 ≥100 次随机迭代 |
| **BenchmarkDotNet** | 性能基准测试（启动时间、Canvas 帧率、点云渲染帧率） |

## 关键架构决策

1. **插件隔离**：每个 NodePlugin DLL 加载在独立 `AssemblyLoadContext`（`isCollectible: true`），支持热重载
2. **不可变数据流**：节点执行时输出封装为不可变 `PortValue`，引用传递给下游，避免深拷贝
3. **UI/执行线程分离**：执行引擎在后台 `Task` 运行，UI 更新通过 `Dispatcher.InvokeAsync` 路由
4. **点云 LOD**：超过 500 万点时启用体素降采样（目标 300 万点），维持 ≥30 FPS
5. **插件契约程序集**：`RVS.PluginContract` 独立发布为 NuGet 包，第三方节点开发者只依赖此包
6. **AI 推理服务隔离**：深度学习推理运行在独立 gRPC 后端进程（`Sz3D.Backend.AiService`），模型与 WPF 主进程隔离，支持 GPU 推理与未来对接 Python 训练服务

## 项目文件格式

- 扩展名：`.rvsp`
- 格式：JSON（`System.Text.Json`）
- 包含：Graph 拓扑、节点参数、Canvas 布局信息
- `NodeDto.TypeId` 格式：`"{PluginId}:{NodeTypeName}"`

## 目录约定

```
./plugins/          # NodePlugin DLL 存放目录（可配置）
./autosave/         # 自动保存文件目录
./logs/             # 崩溃日志目录，格式 crash_<yyyy-MM-dd_HHmmss>.log
./config/           # 布局配置、语言设置等
```
