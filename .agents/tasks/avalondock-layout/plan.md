# Implementation Plan — AvalonDock 布局重构（RVS 风格面板拆分）

范围：仅修改两个文件
- `d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\MainWindow.xaml`
- `d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\MainWindow.xaml.cs`

权威依据：design.md（尤其 §2.4/§2.5/§2.6/§2.10）+ design-review.md。
两个 MEDIUM 强制解法已由编排者拍板，必须落实：
- ① Finding 2：3D 面板显式 `IsSelected="True" IsActive="True"`（不依赖未证实的 pane 默认选首项）。
- ② Finding 1：`ResetToDefaultLayout()` 步骤①之后、②之前，对五块 live host 显式摘除逻辑父（论证 B 主动 detach）。

注意：ContentId 为稳定英文 Id，绝不本地化；Title 可中文。私有字段用下划线 camelCase，常量 PascalCase，公共 API 保留 XML 文档注释。改动顺序先 XAML（面板身份就位），后 code-behind（白名单对齐），每步完成后代码可编译。本特性为单一特性、仅两个文件，不拆分 FEAT，不重构后续工作流步骤。

---

- [ ] 1. 【XAML｜左列拆分】把 `MainWindow.xaml` 中左列 `LayoutAnchorablePane DockMinWidth="180"` 内的单个「视图」`LayoutAnchorable`（`ContentId="panel.view"`）及其内部 `TabControl` + 两个 `TabItem` 整段删除，替换为同一 pane 内的两个独立 `LayoutAnchorable`（3D 声明在前、2D 在后，以呈现为一个初始 Tab 组、默认选中 3D）：
      - 3D：`Title="3D 视图" ContentId="panel.view3d" CanClose="False" CanHide="True" IsSelected="True" IsActive="True"`（**强制解法①**，显式初始选中）。**不写 `CanFloat`**（默认 `true`）。内容 = 原「3D 视图」`TabItem` 的整个 `Grid`：`Background="#111214"`、`Viewport3D x:Name="CloudViewport"`（含 `PerspectiveCamera`、`ModelVisual3D x:Name="CloudVisual"`、灯光 `Model3DGroup`：`AmbientLight #888888` + `DirectionalLight #FFFFFF Direction="-1 -1 -1"`）、「无点云输出」`TextBlock` 及其绑定 `Editor.HasCloudOutput` 的 `DataTrigger` 样式——全部逐字保留。
      - 2D：`Title="2D 视图" ContentId="panel.view2d" CanClose="False" CanHide="True"`。**不写 `CanFloat`**（默认 `true`）、不写 `IsSelected/IsActive`。内容 = 原「2D 视图」`TabItem` 的整个 `Grid`：`Background="#111214"`、`Image Source="{Binding Editor.ImageOutput}" Stretch="Uniform"`、「无图像输出」`TextBlock` 及其绑定 `Editor.HasImageOutput` 的 `DataTrigger` 样式——全部逐字保留。
      - `DockMinWidth="180"` 保留在左列 `LayoutAnchorablePane` 上。移除原 `TabControl Background="#111214"` 包裹层（两面板各自的 `Grid` 已带 `Background="#111214"`）。
      - 不触碰该 pane 外的任何结构。
      Files: `d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\MainWindow.xaml`
      Verify: 随第 3 步统一构建；此时文件应无 XAML 语法错误、`x:Name="CloudViewport"`/`x:Name="CloudVisual"` 各仅出现一次。

- [ ] 2. 【XAML｜算子图禁浮动】在中央 `LayoutDocument`（`Title="算子图" ContentId="panel.graph" CanClose="False"`）上追加属性 `CanFloat="False"`，与 `CanClose="False"` 并列。`ContentId` 保持 `panel.graph` 不变，内部整棵 `nodify:NodifyEditor x:Name="Editor"`（Resources/InputBindings/`Drop="OnDropOperator"`/全部绑定）逐字不动。**不要**在 `DockingManager` 或任何位置设置 `AllowFloatingWindows`（保持默认 `true`，否则会连带禁掉视图面板浮动，违反 AC-4）。顶部 chrome、`Window.Resources`、右列工具箱、底部日志、`DockingManager` 的 `Theme` 全部不动。
      Files: `d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\MainWindow.xaml`
      Verify: 随第 3 步统一构建；确认 XAML 内无 `AllowFloatingWindows` 字样，`LayoutDocument` 同时含 `CanClose="False"` 与 `CanFloat="False"`。

- [ ] 3. 【code-behind｜常量与 RequiredIds】在 `MainWindow.xaml.cs` 的「ContentId 常量」区块删除 `private const string IdView = "panel.view";`，新增 `private const string IdView3D = "panel.view3d";` 与 `private const string IdView2D = "panel.view2d";`（`IdGraph`/`IdToolbox`/`IdLog` 不变）。把 `RequiredIds` 由 `{ IdView, IdGraph, IdToolbox, IdLog }` 改为 `{ IdView3D, IdView2D, IdGraph, IdToolbox, IdLog }`（5 项）。常量命名遵循 PascalCase，ContentId 字符串与第 1 步 XAML 严格一一对应。
      Files: `d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\MainWindow.xaml.cs`
      Verify: `dotnet build d:\kiro\Sz3D\Sz3D.sln -c Debug` 通过，无新增警告（对应 AC-8 / G-1）。

- [ ] 4. 【code-behind｜live 缓存字段】删除 `private object? _viewHost;`，新增 `private object? _view3dHost;` 与 `private object? _view2dHost;`（`_graphHost`/`_toolboxHost`/`_logHost` 不变）。字段命名遵循下划线 camelCase。
      Files: `d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\MainWindow.xaml.cs`
      Verify: 随第 5/6 步统一构建（此步单独会因 switch 引用旧字段暂不编译，与 5/6 连做）。

- [ ] 5. 【code-behind｜LoadLayout 抓缓存与 null 校验】在 `LoadLayout()` 步骤① 的 `switch (lc.ContentId)` 中删除 `case IdView: _viewHost = lc.Content; break;`，新增 `case IdView3D: _view3dHost = lc.Content; break;` 与 `case IdView2D: _view2dHost = lc.Content; break;`。步骤①a 的 null 校验改为检查五块：`_view3dHost is null || _view2dHost is null || _graphHost is null || _toolboxHost is null || _logHost is null`；诊断日志文案相应更新为列出五块（文案非契约，仅诊断用途）。
      Files: `d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\MainWindow.xaml.cs`
      Verify: 随第 6 步统一构建。

- [ ] 6. 【code-behind｜ReattachContent switch】在 `ReattachContent()` 的 `lc.ContentId switch` 中删除 `IdView => _viewHost,`，新增 `IdView3D => _view3dHost,` 与 `IdView2D => _view2dHost,`（`IdGraph`/`IdToolbox`/`IdLog`/`_ => null` 分支不变）。`CollectPresentIds()`/`AllRequiredPresent()` 无硬编码、随 `RequiredIds` 自动适配，不需改。
      Files: `d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\MainWindow.xaml.cs`
      Verify: `dotnet build d:\kiro\Sz3D\Sz3D.sln -c Debug` 通过，无新增警告（AC-8 / G-1）。

- [ ] 7. 【code-behind｜强制解法②：ResetToDefaultLayout 显式 detach】在 `ResetToDefaultLayout()` 的 `try` 块内，步骤①（`DockManager.Layout = new LayoutRoot();`）之后、步骤②（`ApplyLayoutXml(_defaultLayoutXml);`）之前，插入对五块 live host 的显式逻辑父摘除，防止把仍挂在已卸下旧树上的 WPF 元素重挂到新树时抛「元素已是另一元素的子级」异常。按编码规范加注释说明原因（消除跨树残留父冲突）。实现：
      ```csharp
      DockManager.Layout = new LayoutRoot();          // ① 卸下旧树
      // 显式摘除五块 live 内容残留的逻辑父，避免重挂到默认布局时触发 WPF 父冲突
      foreach (var host in new[] { _view3dHost, _view2dHost, _graphHost, _toolboxHost, _logHost })
      {
          if (host is FrameworkElement fe && fe.Parent is ContentControl cc)
              cc.Content = null;
      }
      ApplyLayoutXml(_defaultLayoutXml);               // ② 回放默认布局并重挂
      ```
      确保引入 `using System.Windows;`（`FrameworkElement`，已有）与 `using System.Windows.Controls;`（`ContentControl`，需新增该 using）。不改 `ResetToDefaultLayout()` 的其余逻辑（③ 校验、catch 记日志不递归回退）。
      Files: `d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\MainWindow.xaml.cs`
      Verify: `dotnet build d:\kiro\Sz3D\Sz3D.sln -c Debug` 通过，无新增警告（AC-8 / G-1）。

- [ ] 8. 【code-behind｜注释措辞「四块」→「五块」】全文件搜索「四块」并替换为「五块」，共 5 处（以实际源码为准）：`RequiredIds` 上方注释（约行 27）、字段区块 `// ── 四块 live 内容缓存 …`（约行 36）、`LoadLayout` XML 文档注释「①抓取四块 live 缓存」（约行 130）、`LoadLayout` 步骤① 行内注释「抓取四块 live 缓存…四块 .Content 已建好」（约行 136，该行含两处「四块」，均替换）、`AllRequiredPresent` XML 文档注释「必需面板（四块全部）…」（约行 319）。仅改注释文案，不改任何代码逻辑（coding-standards 要求注释准确）。
      Files: `d:\kiro\Sz3D\src\Sz3D.Presentation.WPF\MainWindow.xaml.cs`
      Verify: `grep 四块` 在该文件应 0 命中；`dotnet build d:\kiro\Sz3D\Sz3D.sln -c Debug` 通过，无新增警告。

- [ ] 9. 【构建门禁 G-1（唯一自动化验收）】运行完整解决方案构建，确认编译通过且无新增警告（AC-8）。
      Files: 无（验证步骤）
      Verify: 在 `d:\kiro\Sz3D` 执行 `dotnet build d:\kiro\Sz3D\Sz3D.sln -c Debug`，退出码 0、无 error、无新增 warning。

---

## 运行时验证门禁（人工冒烟，Windows 桌面运行 `Sz3D.Presentation.WPF` 可执行文件）

构建门禁 G-1 是唯一自动化门禁；以下为 WPF 交互行为验收（无头环境受限，须桌面运行）。M-8 为 Finding 1 解法②的兜底冒烟，若异常则解法②已到位应可消除冲突：

- M-1（AC-1）启动后左列为单一 Tab 组，含「3D 视图」「2D 视图」两标签、**初始选中 3D**（验证强制解法①生效）；无标题为「视图」的面板。
- M-2（AC-2）3D 页显示 `CloudViewport`；`Editor.HasCloudOutput=false` 显示「无点云输出」占位、`true` 隐藏；运行产出点云后 `UpdateCloudVisual()` 刷新 `CloudVisual.Content`。
- M-3（AC-3）2D 页显示绑定 `Editor.ImageOutput` 的图像；`Editor.HasImageOutput=false` 显示「无图像输出」占位。
- M-4（AC-4）分别把 3D、2D 拖出为浮动窗口并停靠回；再拖到一起合并为一个 Tab 组（验证视图面板默认可浮动）。
- M-5（AC-5）算子图双击标题/拖出窗口外均**不浮动**成独立窗口；但可在停靠区内拖动、重新停靠；算子图无关闭按钮。
- M-6（AC-6）正常退出写出 `./config/layout.xml`；重启恢复用户布局并正确重挂内容。
- M-7（AC-7）删除 `layout.xml` 后启动 → 默认布局、不崩溃、不弹窗（崩溃日志记录一条回退原因）。
- M-8（AC-7 + §2.6 迁移兜底，blocking）放置仅含旧 `panel.view` 的 `layout.xml`（仓库 `src\Sz3D.Presentation.WPF\bin\Debug\net10.0-windows\config\layout.xml` 现存样本）后启动 → 必需面板校验失败 → 回退默认布局、不崩溃、3D/2D 两面板内容正常显示。若仍观察到「元素已是另一元素的子级」异常，说明解法②未落实到位，须复核第 7 步。

## 不回归清单（NFR-3）

保持完整、不改动：顶部 chrome（菜单/工具栏/状态/日志 DataGrid）、`OnOperatorMouseDown/Move/OnDropOperator` 算子拖拽、`UpdateCloudVisual`、`CrashLogger`、`SerializeLayout`/`ApplyLayoutXml`/`DetachFromCurrentParent`/`EnumerateContents`、`LayoutPath`/`SaveLayout`/`DtoSerializer`、构造函数 Loaded/Closing 钩子、`MainViewModel`/`NodeEditorViewModel`/`PointCloudRenderer`、`nodify:NodifyEditor` 子树、右列工具箱四 Tab、底部日志、`DockingManager.Theme`（Vs2013DarkTheme）与默认 `AllowFloatingWindows=true`。
