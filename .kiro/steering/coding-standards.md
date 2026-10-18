# 编码规范（C# / WPF / .NET 10）

## 命名约定

- **类、接口、枚举、属性、方法**：PascalCase（`GraphModel`、`INodePlugin`）
- **私有字段**：下划线前缀 camelCase（`_undoStack`、`_pluginsDirectory`）
- **局部变量、参数**：camelCase（`nodeId`、`cancellationToken`）
- **接口名**：以 `I` 开头（`INodePlugin`、`IVisualizerFactory`）
- **抽象基类**：以 `Base` 结尾（`NodeBase`）
- **事件**：动词过去时 PascalCase（`PluginLoaded`、`PluginLoadFailed`）
- **常量**：PascalCase（`MaxUndoSteps`），不用全大写下划线

## 代码风格

- 使用 C# 最新语法：record、primary constructor、pattern matching、collection expressions
- 优先使用 `var`，类型明显时必须用 `var`
- 所有异步方法名称以 `Async` 结尾，返回 `Task` 或 `Task<T>`
- 不要使用 `async void`（事件处理器除外）
- 使用 `CancellationToken` 参数传递取消信号，参数名为 `cancellationToken`
- 优先用 `ArgumentNullException.ThrowIfNull` 替代手动 null 检查

## 注释要求

- **公共 API**（接口、公共类、公共方法）必须有 XML 文档注释（`<summary>`、`<param>`、`<returns>`）
- 复杂算法（如 Kahn 拓扑排序、LOD 降采样）需注释说明算法思路
- 不要注释显而易见的代码（`// 加一` 这类注释禁止）
- TODO 注释格式：`// TODO(功能区): 描述`

## MVVM 规范

- ViewModel 不直接引用 View（不引用任何 `System.Windows.Controls` 类型）
- ViewModel 通过 `ICommand` 暴露操作，不在 code-behind 写业务逻辑
- 属性变更通知必须调用 `OnPropertyChanged()`，使用 `[CallerMemberName]` 特性
- 集合使用 `ObservableCollection<T>`，避免直接替换集合引用

## 错误处理

- 业务操作返回 `Result<T>` 而非抛异常（连接类型不兼容、环路检测等）
- 只在真正异常情况（无法恢复的状态）才抛异常
- 捕获异常时必须记录日志，不得 `catch (Exception) { }`（空吞异常）
- 插件加载失败不中断启动流程，记录错误继续

## 文件组织

- 一个文件一个类，文件名与类名一致
- 项目结构按功能分层：`Models/`、`ViewModels/`、`Views/`、`Services/`、`Commands/`
- 测试文件放在 `RVS.Tests` 项目，目录结构镜像源码结构
