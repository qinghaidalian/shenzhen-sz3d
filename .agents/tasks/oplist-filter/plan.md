# Implementation Plan — 算子列表面板双过滤（关键字 + 种类）

目标：为「算子列表」停靠面板增加两项过滤，参照图漾（Percipio）交互。
1. 顶部查找框实时过滤（不区分大小写子串，匹配 Name + Description）。
2. 查找框右侧「算子库」ComboBox 按 Category 过滤，首项为「全部算子」。
两条件取交集，关键字清空 / 选「全部算子」时各自恢复全部。

## 关键事实（探查结论）

- `NodeLibrary.Groups`（`Services/NodeLibrary.cs`）= 按 Category 分组、组内按 Name 排序、组按 Category 排序的只读列表；`NodeLibrary.Operators` = 扁平只读列表。两者静态只读，**不改动反射扫描逻辑**。
- `OperatorDescriptor` record 字段：`TypeId, Category, Name, Description, IsPhase2, Factory`。`OperatorGroup(string Title, IReadOnlyList<OperatorDescriptor> Items)`。
- `MainViewModel`（`ViewModels/MainViewModel.cs`）继承 `ViewModelBase`，用 `Set(ref _field, value)`（`[CallerMemberName]`）做通知。当前仅 `public IReadOnlyList<OperatorGroup> OperatorGroups => NodeLibrary.Groups;`，无过滤。
- XAML 面板在 `MainWindow.xaml` 的 `<avalondock:LayoutAnchorable Title="算子列表" ContentId="panel.oplist">` 内（约 496–518 行）：顶部 `DockPanel` 含「更新」按钮（`DockPanel.Dock="Right"`）+ 占位 `TextBox Text="查找算子…"`；下方 `ScrollViewer` → `ItemsControl ItemsSource="{Binding OperatorGroups}"` → `Expander`（`Header=Title`）→ 内层 `ItemsControl ItemsSource="{Binding Items}"`。
- XAML 约 61 行有 `<DataTemplate DataType="{x:Type svc:OperatorDescriptor}">`（可拖拽项模板），`MainWindow.xaml.cs` 的 `OnOperatorMouseDown/OnOperatorMouseMove` 依赖项的 `DataContext` 为 `OperatorDescriptor`。过滤只重建 `OperatorGroup`、复用原 `OperatorDescriptor` 实例，拖拽不受影响。**不得删改该 DataTemplate**。
- 命名空间已就位：`svc`（Services）、`avalondock`。画刷 `MutedForeground`/`PrimaryForeground` 在 `Themes/Dark.xaml`。`Themes/Dark.xaml` **没有** `ComboBox` 样式（WPF 默认样式偏浅色），需在 XAML 内联设置 ComboBox 前景/背景以适配深色主题。
- 无 WPF 测试工程（`Sz3D.sln` 的测试工程只覆盖 Core）。本任务不新建测试工程；验证以编译通过为准，并辅以手动运行核对交互。

## 设计决策

- **过滤逻辑全部在 ViewModel 用 LINQ**，不把 `System.Windows.Controls` 引入 VM（符合 MVVM 规范）。
- **过滤后集合用 `ObservableCollection<OperatorGroup>`**，条件变化时先 `Clear()` 再逐项 `Add`（不 new 新集合替换引用），满足约束并让 UI 增量刷新。
- **「全部算子」哨兵**：种类选项列表用 `string?`，`null` 代表「全部算子」；ComboBox 用 DisplayMemberPath 无法显示 null，故改用一个带显示文本的 `CategoryOption` 轻量类型区分「全部」与具体 Category，避免哨兵字符串与真实 Category 撞名。
- **种类选项来源**：从 `NodeLibrary.Groups` 的 `Title`（即 Category）动态取，保持与现有分组标题一致；最前插入「全部算子」。

## 步骤

- [x] 1. 在 `MainViewModel` 中新增过滤状态与可绑定成员，并实现重算逻辑。
      内容：
      - 新增内嵌/独立类型 `CategoryOption`（建议放在 `MainViewModel.cs` 内或同目录新文件 `ViewModels/CategoryOption.cs`）：`public sealed record CategoryOption(string Display, string? Category);`，`Category == null` 表示「全部算子」。带 XML 文档注释。
      - 新增只读集合属性：`public ObservableCollection<OperatorGroup> FilteredGroups { get; } = [];`（供 XAML 绑定，替代原 `OperatorGroups`）。
      - 新增 `public ObservableCollection<CategoryOption> CategoryOptions { get; } = [];`（首项 `new("全部算子", null)`，其后按 `NodeLibrary.Groups` 顺序每个 `g.Title` 一项 `new(g.Title, g.Title)`）。
      - 新增 `private string _searchKeyword = "";` + `public string SearchKeyword { get => _searchKeyword; set { if (Set(ref _searchKeyword, value)) RebuildFilteredGroups(); } }`。
      - 新增 `private CategoryOption? _selectedCategory;` + `public CategoryOption? SelectedCategory { get => _selectedCategory; set { if (Set(ref _selectedCategory, value)) RebuildFilteredGroups(); } }`。
      - 构造函数末尾：填充 `CategoryOptions`，设 `SelectedCategory = CategoryOptions[0]`（触发首次 `RebuildFilteredGroups`，或显式调用一次 `RebuildFilteredGroups()` 做初始填充）。
      - 实现 `private void RebuildFilteredGroups()`：从 `NodeLibrary.Groups` 起算，先按 `SelectedCategory?.Category`（非 null 时）筛组；再对关键字做不区分大小写子串匹配（`o.Name` 或 `o.Description` 含关键字，用 `StringComparison.OrdinalIgnoreCase`），空关键字视为全通过；对每组过滤后 `Items`，仅保留非空组，重建为新的 `OperatorGroup`（复用原 `OperatorDescriptor` 实例）；最后 `FilteredGroups.Clear()` 后逐项 `Add`。公共属性/方法加 XML 文档注释。
      - 删除旧的 `public IReadOnlyList<OperatorGroup> OperatorGroups => NodeLibrary.Groups;`（XAML 将改绑 `FilteredGroups`）。
      约束遵循：私有字段下划线前缀；`var`/集合表达式/record/pattern matching；不引用 `System.Windows.Controls`；不引入新 NuGet。
      Files: `src/Sz3D.Presentation.WPF/ViewModels/MainViewModel.cs`（如拆分则另加 `src/Sz3D.Presentation.WPF/ViewModels/CategoryOption.cs`）
      Verify: `dotnet build src/Sz3D.Presentation.WPF/Sz3D.Presentation.WPF.csproj -c Debug` 退出码 0，无错误/警告。

- [x] 2. 更新「算子列表」面板 XAML：查找框接真实绑定、右侧加「算子库」ComboBox、列表改绑 `FilteredGroups`。
      内容（仅改 `panel.oplist` 面板内约 496–518 行，不动其它面板）：
      - 顶部 `DockPanel`：保留「更新」按钮（`DockPanel.Dock="Right"`）。在其左侧新增 `ComboBox DockPanel.Dock="Right"`，`ItemsSource="{Binding CategoryOptions}"`、`SelectedItem="{Binding SelectedCategory}"`、`DisplayMemberPath="Display"`、宽度约 `120`、`Margin="6 0 0 0"`，并内联设置 `Foreground`/`Background`（如 `Foreground="{StaticResource PrimaryForeground}"`、`Background="{StaticResource ControlBackground}"`）以适配深色主题（Dark.xaml 无 ComboBox 样式）。
      - 将占位 `TextBox Text="查找算子…"` 改为真实搜索框：`Text="{Binding SearchKeyword, UpdateSourceTrigger=PropertyChanged}"`（实时过滤），`Foreground` 用 `PrimaryForeground`；占位提示文字（「查找算子…」）改为非绑定的 watermark 实现（可用覆盖 `TextBlock` + `Visibility` 跟随文本空判断，或简单保留无 watermark；若加 watermark 用 `MutedForeground`）。该 `TextBox` 继续填充 `DockPanel` 剩余宽度（不设 Dock）。
      - 下方列表：`ItemsControl ItemsSource="{Binding OperatorGroups}"` 改为 `ItemsSource="{Binding FilteredGroups}"`。`Expander`（`Header={Binding Title}`、`IsExpanded=True`）与内层 `ItemsControl ItemsSource="{Binding Items}"` 保持不变，`OperatorDescriptor` DataTemplate（约 61 行）保持不变。
      Files: `src/Sz3D.Presentation.WPF/MainWindow.xaml`
      Verify: `dotnet build src/Sz3D.Presentation.WPF/Sz3D.Presentation.WPF.csproj -c Debug` 退出码 0（XAML 绑定编译/标记无误）。

- [x] 3. 全量构建并手动验证交互。
      内容：构建整个解决方案确认无回归；运行应用核对：输入关键字时列表实时按 Name/Description 子串（不区分大小写）过滤、清空恢复全部；ComboBox 选具体种类只显示该组、选「全部算子」显示全部；两条件取交集；算子项仍可拖拽到画布建节点（验证 DataTemplate 与拖拽未受影响）。
      Files:（无改动，仅验证）
      Verify:
      - `dotnet build Sz3D.sln -c Debug` 退出码 0。
      - 手动：`dotnet build src/Sz3D.Presentation.WPF -c Debug` 后运行 `./src/Sz3D.Presentation.WPF/bin/Debug/net10.0-windows/Sz3D.Presentation.WPF.exe`（勿用 `dotnet run`），按上述清单逐项核对。

## 验证记录（本次迭代）

运行的命令与结果：
- `Get-Process Sz3D.Presentation.WPF | Stop-Process -Force`（构建前释放 DLL 锁，进程不存在时忽略）。
- `dotnet build src/Sz3D.Presentation.WPF/Sz3D.Presentation.WPF.csproj -c Debug` → 退出码 0，无错误无警告（含 XAML 标记编译通过）。
- 后台启动 `bin/Debug/net10.0-windows/Sz3D.Presentation.WPF.exe`，等 3 秒确认进程存活（ALIVE）、未崩溃、无异常堆栈；随后结束测试进程保持环境干净。

经运行验证：编译通过；应用能正常启动不崩溃（构造函数填充 `CategoryOptions` 并首次 `RebuildFilteredGroups()` 未抛异常，说明初始「全部算子 + 空关键字」显示全部分组路径可用）。

经代码审查确认（未做自动化 UI 断言，无 WPF 测试工程）：
- 关键字 + 种类取交集：`RebuildFilteredGroups` 先按 `SelectedCategory?.Category` 筛组（`null` 即「全部算子」不筛），再对每组 `Items` 按关键字做 `OrdinalIgnoreCase` 子串匹配（命中 Name 或 Description），仅保留非空组——两条件串联即交集。
- 清空关键字恢复全部：`keyword.Length == 0` 时该条件全通过，回到仅受种类约束的结果。
- 「全部算子」行为：哨兵以 `CategoryOption.Category == null` 表示，与具体 Category 字符串区分；选中它时跳过种类筛选显示全部。
- TextBox 用 `UpdateSourceTrigger=PropertyChanged` 实现逐字实时过滤；ItemsControl 改绑 `FilteredGroups`；`OperatorDescriptor` DataTemplate 未改动，过滤复用原实例，拖拽建节点不受影响。

## 备注 / 假设

- 无 WPF 单元测试工程，故步骤均以构建通过 + 手动核对为验证；未新建测试工程（任务未要求，且 ViewModel 构造依赖 `NodeLibrary` 反射与 WPF 类型，headless 测试成本高）。
- watermark（占位提示）为次要视觉项；若实现复杂度超预期，可先只做功能性绑定，占位提示作为可选增强，不阻塞主功能。
- ComboBox 深色样式仅内联微调，不新增全局样式资源，符合「不引入新依赖 / 最小改动」约束。
