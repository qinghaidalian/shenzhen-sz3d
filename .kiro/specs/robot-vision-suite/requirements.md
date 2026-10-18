# Requirements Document

## Introduction

本项目旨在参照图漾科技 RobotVisionSuite（RVS）系统，基于 WPF / .NET 10 / C# 技术栈，开发一个功能一致的全新 3D 机器视觉开发平台。平台采用可视化节点编程范式，用户可通过拖拽连接节点的方式，快速搭建 3D 视觉项目流程，涵盖相机采集、图像处理、机器人接口、视觉标定、通讯以及深度学习推理等完整工业视觉开发场景。节点以 DLL 插件形式动态加载，核心引擎与节点实现分离，支持未来第三方节点包扩展。3D 点云等大数据量结果通过基于 HelixToolkit.SharpDX 的 DirectX 视口进行高性能渲染。

## Glossary

- **Platform**：本 3D 机器视觉开发平台（WPF 桌面应用程序）
- **Node**：节点，封装特定视觉算子或功能的可视化编程单元，含输入/输出端口
- **Port**：节点上的数据端口，分为输入端口（Input Port）与输出端口（Output Port）
- **Connection**：连线，连接两个节点端口的有向边，表示数据流向
- **Graph**：节点图，由节点与连线构成的有向无环图，代表一套完整的项目处理流程
- **Group**：节点组，将多个节点封装为一个复合节点以供嵌套复用
- **NodePlugin**：节点插件，以 .NET DLL 形式提供的节点实现包，由插件加载器动态载入
- **PluginLoader**：插件加载器，Platform 核心引擎中负责扫描、加载、注册 NodePlugin 的子系统
- **NodeLibrary**：节点库面板，列出所有可用节点类型以供拖入画布
- **Canvas**：画布，节点编辑器的可视化工作区，基于 Nodify 控件实现
- **Viewport**：3D 视口，基于 HelixToolkit.SharpDX 的 DirectX 11 渲染窗口，原生集成至 WPF 视觉树
- **PointCloud**：点云，包含三维坐标（及可选颜色/法向量）的大规模三维数据集
- **Pose**：位姿，包含位置（X/Y/Z）与姿态（Roll/Pitch/Yaw 或四元数）的空间变换数据
- **Project**：项目，包含 Graph 定义、参数配置及相关资源的持久化工作单元
- **ExecutionEngine**：执行引擎，负责解析 Graph 拓扑、调度节点按依赖顺序执行的核心子系统
- **CameraSDKNode**：相机 SDK 节点，封装图漾 3D 相机 SDK 调用的节点类型
- **ImageProcessingNode**：图像处理节点，封装 2D/3D 图像处理算法的节点类型
- **RobotInterfaceNode**：机器人接口节点，封装工业机器人通讯与控制接口的节点类型
- **CalibrationNode**：标定节点，封装视觉标定与手眼标定算法的节点类型
- **CommunicationNode**：通讯节点，封装 Modbus/TCP、OPC-UA 等工业通讯协议的节点类型
- **DeepLearningNode**：深度学习推理节点，通过 gRPC 调用独立后端服务（AiService）执行 ONNX 模型推理的节点类型
- **AiService**：AI 推理后端服务（`Sz3D.Backend.AiService`），独立进程，通过 gRPC 暴露模型加载、推理、卸载与健康检查接口，模型以 ONNX Runtime 运行在服务端
- **VisualizerPanel**：可视化面板，展示节点运算结果（图像、点云、位姿、字符串等）的 UI 区域
- **CameraSDKWrapper**：相机 SDK 封装层，通过 P/Invoke 或 C++/CLI 桥接图漾 camport3 C API，向上层提供 .NET 接口
- **SimulatedCameraNode**：模拟相机节点，在无真实硬件时以预录点云帧文件替代真实采集，用于开发与测试
- **PythonScriptNode**：Python 脚本算子节点，允许用户通过 JSON 配置文件定义端口、用 Python 脚本实现处理逻辑，由平台嵌入 Python 运行时执行
- **TriggerNode**：触发器节点，产生执行触发信号以启动节点图的执行流程
- **LoadNode**：加载节点，从文件系统读取图像、点云等数据并输出至 Output Port
- **FileIONode**：文件 I/O 节点，提供通用文件读写能力（图像、点云、CSV、文本等格式）

---

## Requirements

### Requirement 1: 可视化节点编辑器

**User Story:** 作为视觉工程师，我希望通过拖拽连接节点来构建视觉处理流程，以便快速搭建和验证 3D 视觉方案而无需编写代码。

#### Acceptance Criteria

1. THE Platform SHALL 提供基于 Nodify 控件的 Canvas，供用户在其中创建、移动、删除和连接节点。
2. WHEN 用户从 NodeLibrary 将节点类型拖拽至 Canvas，THE Platform SHALL 在拖放目标位置实例化对应类型的 Node，并在 Canvas 上显示该节点。
3. WHEN 用户从一个 Node 的 Output Port 拖拽至另一个 Node 的 Input Port，THE Platform SHALL 在两端口之间创建 Connection，前提是两端口数据类型兼容。
4. IF 用户尝试将 Output Port 连接到数据类型不兼容的 Input Port，THEN THE Platform SHALL 拒绝建立 Connection，并以视觉反馈（端口高亮变红）提示用户类型不匹配。
5. IF 用户尝试在 Graph 中创建形成环路的 Connection，THEN THE Platform SHALL 拒绝该 Connection 并向用户显示"不允许循环连接"的提示信息。
6. WHEN 用户选中一个或多个 Node 并触发"成组"操作，THE Platform SHALL 将所选 Node 封装为 Group，并在 Canvas 上以单一复合节点图标表示该 Group。
7. WHEN 用户双击 Group 节点，THE Platform SHALL 展开进入该 Group 内部的子 Canvas 视图供编辑。
8. THE Canvas SHALL 支持通过鼠标滚轮进行缩放（缩放比例范围 10% ~ 1000%），并支持通过鼠标中键拖拽进行平移。
9. THE Platform SHALL 支持对 Canvas 上所有操作（节点增删、连线、移动、成组）的撤销（Ctrl+Z）与重做（Ctrl+Y），最大历史步数不少于 50 步。
10. WHEN 用户选中 Node 并按 Delete 键，THE Platform SHALL 删除该 Node 及与之相连的所有 Connection。

---

### Requirement 2: 节点库与插件化架构

**User Story:** 作为平台开发者，我希望节点以 DLL 插件形式动态加载，以便核心引擎与节点实现解耦，支持未来扩展第三方节点包。

#### Acceptance Criteria

1. THE PluginLoader SHALL 在 Platform 启动时扫描指定插件目录（默认为 `./plugins/`）下的所有 .dll 文件，并自动加载实现了节点插件接口的程序集。
2. WHEN PluginLoader 成功加载一个 NodePlugin，THE Platform SHALL 将该插件中包含的所有 Node 类型注册到 NodeLibrary，并在节点库面板中按插件定义的分类显示。
3. IF PluginLoader 加载某 .dll 文件时发生异常（文件损坏、接口不兼容等），THEN THE Platform SHALL 记录错误日志并跳过该 .dll，继续加载其余插件，不中断 Platform 启动流程。
4. THE NodePlugin 接口 SHALL 定义以下契约：节点元数据（名称、类别、描述、版本）、输入/输出端口定义、以及同步/异步执行方法，使第三方开发者可在不修改 Platform 核心代码的情况下实现新节点。
5. THE NodeLibrary 面板 SHALL 按节点类别（相机、图像处理、机器人、标定、通讯、深度学习、自定义）将节点分组显示，并提供文本搜索框，WHEN 用户输入搜索关键词，THE NodeLibrary SHALL 实时过滤并仅显示名称或描述中包含该关键词的节点类型。
6. WHERE 用户安装了新的 NodePlugin DLL，THE Platform SHALL 提供"重新扫描插件"功能，WHEN 用户触发该功能，THE PluginLoader SHALL 重新扫描插件目录并更新 NodeLibrary，无需重启 Platform。

---

### Requirement 3: 内置节点库

**User Story:** 作为视觉工程师，我希望平台内置覆盖完整工业视觉场景的节点库，以便开箱即用地搭建 3D 视觉项目。

#### Acceptance Criteria

1. THE Platform SHALL 内置 CameraSDKNode 类型；THE CameraSDKNode SHALL 通过 CameraSDKWrapper（P/Invoke 封装图漾 camport3 C API）完成设备枚举、连接、参数配置（曝光、增益等）及彩色/深度图像帧采集，并将采集结果输出至 Output Port；WHEN 该节点执行时 SDK 调用超时超过 10 秒，THE CameraSDKNode SHALL 抛出超时异常并在日志面板记录错误详情。
2. THE Platform SHALL 内置 ImageProcessingNode 类型集合，涵盖但不限于：2D 滤波（高斯、中值）、边缘检测（Canny）、形态学操作（膨胀、腐蚀）、点云滤波（直通滤波、统计离群值滤除）、点云配准（ICP）、法向量估计。
3. THE Platform SHALL 内置 PythonScriptNode 类型，支持用户通过 JSON 配置文件定义算子的输入/输出端口与参数，并编写对应的 Python 脚本文件实现处理逻辑；WHEN PythonScriptNode 执行时，THE PythonScriptNode SHALL 通过嵌入式 Python 运行时（Python.NET 或子进程调用）载入对应 Python 脚本，按 JSON 配置传入输入数据并获取输出数据，输出至 Output Port；THE Platform SHALL 提供"Python 配置生成器"面板，供用户以图形化方式添加端口与参数定义并生成 JSON 与 Python 模板文件；IF Python 运行时不可用或脚本执行异常，THEN THE PythonScriptNode SHALL 在日志面板记录详细错误信息并将节点标记为错误状态。
4. THE Platform SHALL 内置 RobotInterfaceNode 类型，WHEN 该节点执行时，THE RobotInterfaceNode SHALL 通过所配置的机器人通讯协议（如 ABB EGMD、KUKA RSI、通用 Modbus/TCP）向目标机器人发送运动指令或读取关节状态。
5. THE Platform SHALL 内置 CalibrationNode 类型，涵盖：相机内参标定（棋盘格法）、相机-机器人手眼标定（Eye-in-Hand 与 Eye-to-Hand 两种模式），WHEN CalibrationNode 执行完成，THE CalibrationNode SHALL 将标定结果（内参矩阵、畸变系数、手眼变换矩阵）输出至 Output Port 并支持持久化保存至文件。
6. THE Platform SHALL 内置 CommunicationNode 类型，支持以下协议：TCP/IP（客户端与服务端）、Modbus TCP、OPC-UA 客户端，WHEN CommunicationNode 执行时，THE CommunicationNode SHALL 按配置建立连接、发送/接收数据，并将接收数据输出至 Output Port。
7. THE Platform SHALL 内置 DeepLearningNode 类型，支持加载 ONNX 格式模型文件（文件大小上限 2 GB）；THE DeepLearningNode SHALL 通过 gRPC 调用独立后端推理服务 Sz3D.Backend.AiService（独立进程，Kestrel 监听 localhost），模型加载与 ONNX 推理均在服务端进程内完成，与 WPF 主进程隔离，支持未来扩展 GPU 推理或对接 Python 训练服务；WHEN DeepLearningNode 执行推理时，THE DeepLearningNode SHALL 将输入图像经预处理（缩放至模型输入尺寸、像素值归一化至 [0,1]）后通过 gRPC 发送至 AiService，获取推理结果并输出至 Output Port；本节点仅支持模型推理，不包含模型训练功能；IF 用户需要训练模型，THE Platform SHALL 在节点属性面板中注明"请使用外部训练工具（如 PyTorch/PaddlePaddle）导出 ONNX 模型后导入"。
8. IF 图漾相机 SDK 不可用（设备未连接或 DLL 缺失），THEN THE Platform SHALL 内置 SimulatedCameraNode，支持用户指定本地预录点云帧文件（.pcd 或 .bin 格式）作为数据源；WHEN SimulatedCameraNode 执行时，THE SimulatedCameraNode SHALL 循环读取指定目录下的预录帧并将其输出至 Output Port，使整个 Graph 流程在无真实硬件时仍可完整联调。
9. THE RobotInterfaceNode 对 ABB EGM 和 KUKA RSI 协议的支持 SHALL 以 Modbus TCP 作为基础验证协议优先实现；ABB EGM 与 KUKA RSI 的完整实现 SHALL 依赖对应品牌机器人控制器的授权选件，THE Platform SHALL 在节点属性面板中注明各协议的硬件依赖要求，并在无授权环境下以友好提示替代崩溃。

---

### Requirement 4: 执行引擎

**User Story:** 作为视觉工程师，我希望平台能自动按正确顺序执行节点图中的所有节点，以便得到完整的处理结果。

#### Acceptance Criteria

1. WHEN 用户触发"运行"操作，THE ExecutionEngine SHALL 对当前 Graph 执行拓扑排序，确定节点执行顺序，并按该顺序依次调用每个 Node 的执行方法。
2. WHEN 一个 Node 执行完成，THE ExecutionEngine SHALL 将该 Node 的 Output Port 数据传递至所有通过 Connection 连接的下游 Node 的对应 Input Port，然后再执行下游 Node。
3. IF 某 Node 执行时抛出异常，THEN THE ExecutionEngine SHALL 停止当前 Graph 执行，在 UI 中将该 Node 标记为错误状态（红色边框），并在日志面板中显示包含节点名称、错误类型和错误消息的详细错误信息。
4. WHILE Graph 正在执行，THE Platform SHALL 在状态栏显示当前正在执行的节点名称及整体执行进度（已完成节点数 / 总节点数），并以高亮颜色标记 Canvas 上正在执行的 Node。
5. THE ExecutionEngine SHALL 支持用户在执行过程中随时触发"停止"操作，WHEN 用户触发停止，THE ExecutionEngine SHALL 在当前节点执行完成后终止执行，不强制中断正在执行的节点内部逻辑。
6. THE ExecutionEngine SHALL 支持单步执行模式，WHEN 用户在单步模式下触发"下一步"操作，THE ExecutionEngine SHALL 仅执行拓扑顺序中的下一个节点，并等待下一次用户触发。
7. WHEN Graph 执行完成（无错误），THE ExecutionEngine SHALL 在状态栏显示"执行完成"及总耗时（毫秒精度），并记录每个节点的单独执行耗时至日志面板。

---

### Requirement 5: 结果可视化

**User Story:** 作为视觉工程师，我希望实时查看每个节点的运算结果，以便快速调试和验证算法效果。

#### Acceptance Criteria

1. THE VisualizerPanel SHALL 支持以下数据类型的可视化展示：字符串（文本显示）、整数/浮点数（数值显示）、2D 图像（图像预览控件）、PointCloud（3D 视口渲染）、Pose（3D 视口中的坐标轴可视化）。
2. WHEN 用户在 Canvas 上双击某个 Node 的 Output Port，THE Platform SHALL 在 VisualizerPanel 中打开对应的可视化视图，并实时显示最近一次执行后该端口的数据。
3. THE Viewport SHALL 基于 HelixToolkit.SharpDX 实现 DirectX 11 渲染，支持渲染不少于 500 万点的 PointCloud，WHILE PointCloud 数据量超过 500 万点时，THE Viewport SHALL 自动启用体素降采样 LOD（细节层次）以维持交互帧率不低于 30 FPS。
4. WHILE Viewport 显示 PointCloud 时，THE Platform SHALL 支持用户通过鼠标左键旋转、鼠标右键平移、鼠标滚轮缩放对点云进行三维交互浏览。
5. WHEN 用户在 Viewport 中显示 Pose 数据，THE Viewport SHALL 以三色坐标轴（X 轴红色、Y 轴绿色、Z 轴蓝色）可视化位姿，轴长与场景尺度自适应。
6. THE VisualizerPanel SHALL 支持同时显示多个可视化标签页，WHEN 用户切换标签页，THE Platform SHALL 立即显示对应端口的可视化内容。
7. WHEN Graph 中某节点执行完成，THE VisualizerPanel SHALL 自动刷新当前已打开的该节点相关可视化视图。

---

### Requirement 6: 项目管理

**User Story:** 作为视觉工程师，我希望能保存和加载项目文件，以便跨会话延续工作并与团队共享项目配置。

#### Acceptance Criteria

1. THE Platform SHALL 支持将当前 Project（包含 Graph 拓扑、节点参数、Canvas 布局信息）以 JSON 格式序列化并保存至用户指定的 .rvsp 文件。
2. WHEN 用户打开一个 .rvsp 文件，THE Platform SHALL 反序列化该文件，还原 Graph 拓扑、节点参数及 Canvas 布局，并在 Canvas 上完整呈现该项目流程。
3. IF 用户打开的 .rvsp 文件引用了当前 Platform 中未加载的 NodePlugin，THEN THE Platform SHALL 显示缺失插件列表，并允许用户在确认后以"占位节点"方式加载项目，缺失节点以灰色虚框显示，标注缺失插件名称。
4. THE Platform SHALL 支持"最近项目"功能，记录最近打开的不少于 10 个项目的文件路径，WHEN 用户从菜单访问"最近项目"，THE Platform SHALL 显示带文件路径提示的项目列表供快速打开。
5. WHEN 用户尝试关闭包含未保存修改的 Project，THE Platform SHALL 弹出确认对话框提示用户保存，用户可选择"保存并关闭"、"不保存关闭"或"取消"。
6. THE Platform SHALL 支持项目模板功能，内置不少于 3 个常用场景模板（如"基础点云采集"、"手眼标定"、"缺陷检测"），WHEN 用户从模板新建项目，THE Platform SHALL 加载对应模板的预置 Graph 结构。

---

### Requirement 7: 用户界面与工作区布局

**User Story:** 作为视觉工程师，我希望平台提供清晰、高效的工作区布局，以便我能专注于视觉流程的开发与调试。

#### Acceptance Criteria

1. THE Platform SHALL 提供可停靠（dockable）的主工作区布局，包含以下固定功能区域：顶部菜单栏与工具栏、左侧 NodeLibrary 面板、中央 Canvas 编辑区、右侧属性面板（Property Panel）、底部日志面板（Log Panel）、独立的 VisualizerPanel（可作为停靠面板或独立窗口）。
2. WHEN 用户选中 Canvas 上的某个 Node，THE Platform SHALL 在右侧属性面板中显示该节点的可编辑参数列表，用户修改参数后，THE Platform SHALL 立即将新参数值同步至节点实例。
3. THE Platform SHALL 支持用户调整各面板的尺寸与停靠位置，WHEN 用户调整布局后关闭 Platform，THE Platform SHALL 在下次启动时自动恢复上次保存的布局配置。
4. THE Platform SHALL 在底部日志面板按时间戳记录所有节点执行日志（INFO 级别）、警告（WARN 级别）与错误（ERROR 级别），并提供按日志级别过滤的下拉选择器。
5. THE Platform SHALL 提供浅色（Light）与深色（Dark）两套主题，WHEN 用户切换主题，THE Platform SHALL 即时刷新所有 UI 组件的配色，无需重启。
6. THE Platform SHALL 支持中文与英文两种界面语言，WHEN 用户切换语言，THE Platform SHALL 即时更新所有 UI 文本，无需重启。

---

### Requirement 8: 系统性能与稳定性

**User Story:** 作为视觉工程师，我希望平台在处理大型项目和大数据量时保持稳定且响应迅速，以便高效完成开发工作。

#### Acceptance Criteria

1. WHEN Platform 启动（冷启动，包含加载不少于 5 个 NodePlugin），THE Platform SHALL 在 10 秒内完成启动并呈现可交互的主界面。
2. WHILE Canvas 上存在不超过 200 个 Node 及 300 条 Connection 时，THE Canvas SHALL 维持节点拖拽与连线操作的交互帧率不低于 60 FPS。
3. THE ExecutionEngine SHALL 在单个节点执行线程外维护独立的 UI 线程，确保 Graph 执行期间 Canvas 与 VisualizerPanel 的 UI 响应时间不超过 100ms。
4. IF Platform 运行过程中发生未处理的异常，THEN THE Platform SHALL 捕获该异常，将完整堆栈信息写入 `./logs/crash_<timestamp>.log` 文件，并向用户显示"程序遇到错误，已保存崩溃日志"的提示，不直接崩溃退出。
5. THE Platform SHALL 在每次 Graph 成功执行完成后，自动将当前 Project 状态保存至临时文件（`./autosave/autosave_<project_name>.rvsp`），WHEN Platform 在上次自动保存后异常退出并重新启动，THE Platform SHALL 检测到自动保存文件并提示用户是否恢复。

---

### Requirement 9: 基础流程控制节点与文件操作节点

**User Story:** 作为视觉工程师，我希望平台提供基础的触发、数据加载、文件读写及流程控制节点，以便构建完整的端到端视觉处理流程。

#### Acceptance Criteria

1. THE Platform SHALL 内置 TriggerNode 类型，WHEN 用户触发 TriggerNode（手动点击或外部信号），THE TriggerNode SHALL 向所有连接的下游节点发出执行触发信号，启动节点图执行流程。
2. THE Platform SHALL 内置 LoadNode 类型，支持从文件系统加载以下数据类型：2D 图像（PNG、JPG、BMP、TIFF）、点云（.pcd、.ply、.bin）；WHEN LoadNode 执行时，THE LoadNode SHALL 读取用户配置的文件路径，将数据输出至 Output Port；IF 文件不存在或格式不支持，THEN THE LoadNode SHALL 在日志面板记录错误并将节点标记为错误状态。
3. THE Platform SHALL 内置 FileIONode 类型集合，涵盖以下功能：保存图像至文件（支持 PNG/JPG/BMP）、保存点云至文件（支持 .pcd/.ply）、读写 CSV 文件（用于数值数据的批量导入导出）、读写文本文件；WHEN FileIONode 执行时，THE FileIONode SHALL 按配置完成文件读写操作，并将操作结果（成功/失败状态）输出至 Output Port。
4. THE Platform SHALL 内置流程控制节点，包括：条件分支节点（IF/ELSE，根据输入布尔值决定数据路由）、计数器节点（记录执行次数，可用于循环控制）；WHEN 条件分支节点执行时，THE Platform SHALL 根据输入条件值将数据路由至对应输出端口。

---

### Requirement 10: 机器人运动规划节点（可选，Phase 2）

**User Story:** 作为视觉工程师，我希望平台提供机器人运动规划节点，以便将视觉定位结果直接转化为机器人可执行的运动轨迹。

#### Acceptance Criteria

1. THE Platform MAY 内置 MotionPlanningNode 类型（标记为可选功能，Phase 2 实现），支持接收目标 Pose 输入，调用运动规划算法（关节插补、直线轨迹规划）输出机器人关节角度序列至 Output Port。
2. IF MotionPlanningNode 未实现或运动规划库不可用，THEN THE Platform SHALL 在节点属性面板中显示"此功能需要运动规划库支持，当前版本暂不可用"的提示，不影响其他节点的正常运行。

---

### Requirement 11: 拆垛与表面处理节点（可选，Phase 2）

**User Story:** 作为视觉工程师，我希望平台提供拆垛算法和多边形表面处理节点，以便快速实现工业拆垛和表面分析场景。

#### Acceptance Criteria

1. THE Platform MAY 内置 BoxUnstackNode 类型（标记为可选功能，Phase 2 实现），支持对点云输入进行箱体检测与位姿估算，输出最顶层箱体的位姿至 Output Port，用于自动拆垛引导。
2. THE Platform MAY 内置 SurfaceNode 类型集合（标记为可选功能，Phase 2 实现），涵盖多边形表面拟合、表面缺陷检测等功能。
3. IF 上述可选节点未实现，THEN THE Platform SHALL 在节点库中以灰色禁用状态显示这些节点类型，并提示"Phase 2 功能，敬请期待"，不影响 Phase 1 功能的正常使用。
