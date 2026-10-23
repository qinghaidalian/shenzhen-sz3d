# Implementation Plan — 算子列表面板过滤区 UI 微调

## 背景与依据

本计划基于对以下两个文件的实际阅读：

- `src/Sz3D.Presentation.WPF/MainWindow.xaml`（算子列表面板约在 496–523 行）
- `src/Sz3D.Presentation.WPF/Themes/Dark.xaml`（画刷资源 7–16 行、TextBox 样式 109–116 行、MenuItem 模板 126–244 行、ContextMenu 253–281 行、TabItem 模板 287–328 行）

**当前过滤区 XAML（MainWindow.xaml）：**

```xml
<DockPanel DockPanel.Dock="Top" Margin="0 0 0 6">
    <Button DockPanel.Dock="Right" Content="更新" Margin="6 0 0 0"/>
    <ComboBox DockPanel.Dock="Right" Width="120" Margin="6 0 0 0"
              ItemsSource="{Binding CategoryOptions}"
              SelectedItem="{Binding SelectedCategory}"
              DisplayMemberPath="Display"
              Foreground="{StaticResource PrimaryForeground}"
              Background="{StaticResource ControlBackground}"
              BorderBrush="{StaticResource BorderColor}"/>
    <TextBox Text="{Binding SearchKeyword, UpdateSourceTrigger=PropertyChanged}"
             Foreground="{StaticResource PrimaryForeground}"/>
</DockPanel>
```

**已确认的事实（无需再假设）：**

- Dark.xaml 中**没有任何 ComboBox 样式** → 当前 ComboBox 使用 WPF 默认浅色模板，仅设 Foreground/Background 无法覆盖 ToggleButton 底色与 Popup 白底（改动 2 的根因）。
- 所有需要的画刷均已定义：`PrimaryForeground` #D4D4D4、`MutedForeground` #9A9A9A、`ControlBackground` #2D2D30、`ControlHover` #3E3E42、`BorderColor` #3F3F46、`AccentColor` #007ACC、`PanelBackground` #252526。
- 现有 TextBox 隐式样式会作用于查找框；水印必须用**局部样式**（只作用于该查找框）实现，避免改动全局 TextBox 的 Background/CaretBrush。
- MenuItem 的高亮交互写法：`IsHighlighted=True` → `Bd.Background={AccentColor}` + `Foreground=#FFFFFF`；TabItem 选中写法：`IsSelected=True` → `Bd.Background={AccentColor}` + `Foreground=#FFFFFF`，悬停用 `ControlHover`。新 ComboBoxItem 照搬此风格。
- 解决方案文件为 `Sz3D.sln`（注意：steering 规则文字写的是 `RVS.sln`，仓库实际文件是 `Sz3D.sln`，以实际为准）。

## 设计决策

1. **水印实现方式**：采用「覆盖式 TextBlock + Visibility 触发器」而非 VisualBrush。在查找框外包一层 `Grid`，Grid 内放查找 TextBox 和一个水印 `TextBlock Text="过滤"`（`MutedForeground`，`IsHitTestVisible=False`，左内边距对齐文本）。水印 TextBlock 默认可见，通过 `DataTrigger` 绑定查找框 `Text.IsEmpty`（或 `SearchKeyword` 为空）控制：非空时折叠。理由：此法最稳、无需改全局 TextBox 样式，也不依赖 `TextBox.Text` 的 `""` 特判之外的机制；且天然满足「有输入后隐藏」。「获得焦点后输入才隐藏」= 只要有文本就隐藏，符合需求（空焦点仍显示水印，属工业软件常见行为）。局部水印样式/结构直接写在 MainWindow.xaml 过滤区内，不污染 Dark.xaml 的全局 TextBox。

2. **ComboBox 深色模板**：在 Dark.xaml 新增 `<Style TargetType="ComboBox">`（含自定义 `ControlTemplate`）与 `<Style TargetType="ComboBoxItem">`（含自定义 `ControlTemplate`）两套隐式样式，放在 TextBox 样式之后、Menu 样式之前（约 116 行后），保持「输入控件」归类集中。ComboBox 模板结构：根 `Grid` → `ToggleButton`（深色底 `ControlBackground`、边框 `BorderColor`、右侧箭头 `Path` 用 `PrimaryForeground`，`IsChecked` 绑定 `IsDropDownOpen`）+ `ContentPresenter`（显示选中项文本）+ `Popup x:Name="PART_Popup"`（内含深色 `Border` 背景 `ControlBackground` + 边框 `BorderColor`，再包 `ScrollViewer` + `ItemsPresenter`）。ComboBoxItem 模板：`Border x:Name="Bd"` 深色透明底 + `ContentPresenter`，触发器 `IsMouseOver`/`IsHighlighted`/`IsSelected` → `AccentColor` 背景 + 白字（照搬 MenuItem/TabItem 写法）。理由：只有自定义 ControlTemplate 才能彻底替换默认浅色的 ToggleButton 与 Popup 白底；用隐式样式使面板内 ComboBox 自动生效，无需改 MainWindow.xaml 的 ComboBox 引用额外 Key。

3. **删除「更新」按钮并调整布局**：删除 `<Button .../>`。ComboBox 保留 `DockPanel.Dock="Right"`；「查找：」TextBlock 以 `DockPanel.Dock="Left"` 停靠；查找框（含水印 Grid）为 DockPanel 最后一个无 Dock 的子元素，自动填充左侧剩余空间。所有元素 `VerticalAlignment="Center"`。理由：DockPanel 中最后一个非 Dock 子元素填充剩余区域，正好满足「输入框占左侧剩余空间、ComboBox 靠右」。

## 实现步骤

- [ ] 1. 在 Dark.xaml 新增深色 ComboBox + ComboBoxItem 隐式样式。
      在 `src/Sz3D.Presentation.WPF/Themes/Dark.xaml` 的 TextBox 样式（约 116 行 `</Style>`）之后、`<!-- ── 菜单 / 右键菜单 ── -->` 之前，插入两段隐式样式：
      (a) `<Style TargetType="ComboBoxItem">`：`Foreground={PrimaryForeground}`、`Background=Transparent`、自定义 `ControlTemplate`（`Border x:Name="Bd"` + `ContentPresenter`，`Margin` 用 `Padding` 的 TemplateBinding，`TextElement.Foreground={TemplateBinding Foreground}`），触发器：`IsMouseOver=True` 与 `IsHighlighted=True` 与 `IsSelected=True` 均 `Bd.Background={AccentColor}` + `Foreground=#FFFFFF`（照搬 MenuItem `IsHighlighted` / TabItem `IsSelected` 风格）。
      (b) `<Style TargetType="ComboBox">`：`Background={ControlBackground}`、`Foreground={PrimaryForeground}`、`BorderBrush={BorderColor}`、`BorderThickness=1`、`Padding=4 2`、`MinHeight=22`、自定义 `ControlTemplate`：
        - 根 `Grid`，含列定义（`*` + `Auto` 给箭头按钮）。
        - `ToggleButton` 置于整个 Grid（`Grid.ColumnSpan=2`），`Focusable=False`、`ClickMode=Press`、`IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}"`，其自身模板为深色 `Border`（`Background={TemplateBinding Background}`、`BorderBrush={TemplateBinding BorderBrush}`、`BorderThickness={TemplateBinding BorderThickness}`）内放右侧向下箭头 `Path`（`Data="M 0 0 L 4 4 L 8 0 Z"`、`Fill={PrimaryForeground}`、右对齐、`Margin=0 0 6 0`）。
        - `ContentPresenter x:Name="ContentSite"`：`Content={TemplateBinding SelectionBoxItem}`、`ContentTemplate={TemplateBinding SelectionBoxItemTemplate}`、左对齐、垂直居中、`Margin={TemplateBinding Padding}`、`IsHitTestVisible=False`，`TextElement.Foreground={PrimaryForeground}`（保证选中项文字浅色）。
        - `Popup x:Name="PART_Popup"`：`AllowsTransparency=True`、`Placement=Bottom`、`IsOpen="{TemplateBinding IsDropDownOpen}"`、`Focusable=False`，内含深色 `Border`（`Background={ControlBackground}`、`BorderBrush={BorderColor}`、`BorderThickness=1`、`MinWidth={TemplateBinding ActualWidth}`、`MaxHeight={TemplateBinding MaxDropDownHeight}`），内再包 `ScrollViewer` + `ItemsPresenter`。
        - 可选触发器：`IsMouseOver=True` 时边框转 `AccentColor` 以增强可见性（非必需）。
      Files: `src/Sz3D.Presentation.WPF/Themes/Dark.xaml`
      Verify: `dotnet build Sz3D.sln -c Debug`（在 `d:\kiro\Sz3D`）构建成功、无 XAML 解析错误、无新增警告。

- [ ] 2. 改写 MainWindow.xaml 算子列表过滤区：删「更新」按钮、加「查找：」标签、查找框加水印、ComboBox 靠右。
      在 `src/Sz3D.Presentation.WPF/MainWindow.xaml` 把当前顶部过滤 `DockPanel`（约 501–513 行）替换为：
        - 删除 `<Button ... Content="更新" .../>` 整行。
        - 最左新增 `<TextBlock DockPanel.Dock="Left" Text="查找：" VerticalAlignment="Center" Margin="0 0 6 0" Foreground="{StaticResource PrimaryForeground}"/>`。
        - 保留 `<ComboBox DockPanel.Dock="Right" .../>`（绑定 `CategoryOptions`/`SelectedCategory`/`DisplayMemberPath="Display"` 不变；因已有隐式深色 ComboBox 样式，可移除控件上冗余的 Foreground/Background/BorderBrush，由样式统一接管；保留 `Width="120"` `Margin="6 0 0 0"` `VerticalAlignment="Center"`）。
        - 查找框改为：`<Grid>` 包裹，内放原 `TextBox`（`x:Name="OpListSearchBox"`、`Text="{Binding SearchKeyword, UpdateSourceTrigger=PropertyChanged}"`、`VerticalAlignment="Center"`、保留 `Foreground`）与水印 `<TextBlock Text="过滤" Foreground="{StaticResource MutedForeground}" IsHitTestVisible="False" VerticalAlignment="Center" Margin="6 0 0 0"/>`；水印用 `Style` + `DataTrigger` 绑定 `{Binding Text.IsEmpty, ElementName=OpListSearchBox}`：`False` 时 `Visibility=Collapsed`（默认 Visible）。该 `Grid` 为 DockPanel 最后一个无 `DockPanel.Dock` 的子元素，自动填充左侧剩余空间。
      不改动下方 `ScrollViewer`/`ItemsControl ItemsSource="{Binding FilteredGroups}"` 及算子拖拽 `DataTemplate`。不改 ViewModel。
      Files: `src/Sz3D.Presentation.WPF/MainWindow.xaml`
      Verify: `dotnet build Sz3D.sln -c Debug` 构建成功、无 XAML 错误。

- [ ] 3. 构建整解决方案并做静态检查，确认无回归。
      Files: 无（验证步骤）
      Verify: 在 `d:\kiro\Sz3D` 运行 `dotnet build Sz3D.sln -c Debug`；确认退出码 0、无 XAML 编译错误、无新增分析器警告。若机器支持，运行 `.\src\Sz3D.Presentation.WPF\bin\Debug\net10.0-windows\<App>.exe`（WPF 不用 `dotnet run`）目视确认：查找框左侧有「查找：」、空框显示灰色「过滤」水印且输入后消失；「算子库」下拉为深底浅字、下拉弹层深色、悬停/选中项蓝色高亮白字；「更新」按钮已消失，ComboBox 靠右、查找框占左侧剩余空间且垂直居中。

## 验证要点汇总

- 构建命令（真实）：`dotnet build Sz3D.sln -c Debug`（steering 文档写 `RVS.sln`，以仓库实际 `Sz3D.sln` 为准）。
- 本任务为纯 XAML 改动，无 C# 逻辑变更，故无单元测试覆盖；验证以构建通过 + 目视检查为准（WPF UI 行为无自动化测试基础设施用于此面板）。
- 回归边界：不得改动 `MainViewModel`、`NodeLibrary`、算子拖拽 `DataTemplate`、`FilteredGroups` 列表、其它面板，以及全局 `TextBox` 隐式样式的 Background/CaretBrush。

## 验证记录（第一次迭代）

- 结束残留进程：`Get-Process -Name Sz3D.Presentation.WPF | Stop-Process -Force`，等 0.6s。
- 构建：`dotnet build src\Sz3D.Presentation.WPF\Sz3D.Presentation.WPF.csproj -c Debug` → Exit Code 0，7.1s 成功，无错误、无新增警告。
- 启动：后台启动 `Sz3D.Presentation.WPF.exe`，等 3s，进程存活（pid 存在）、未崩溃、启动终端无异常堆栈；验证后已结束进程保持环境干净。
- XAML 层面复核：
  - 改动 1 —「查找：」TextBlock（Dock=Left）存在；水印 TextBlock Text="过滤" 默认 Collapsed，DataTrigger 绑定 `Text.IsEmpty`(ElementName=OpListSearchBox)=True 时 Visible（空值显示，绑定方向正确），用 MutedForeground、IsHitTestVisible=False，局部 Style 只作用于该框，未改全局 TextBox。
  - 改动 2 — Dark.xaml 新增 `ComboBox` + `ComboBoxItem` 深色隐式样式（TextBox 之后、菜单之前）。ToggleButton 背景 ControlBackground、边框 BorderColor、箭头 Path 用 PrimaryForeground；Popup 内 Border 背景 ControlBackground + 边框 BorderColor（非白底）；ComboBoxItem 透明深底浅字，悬停/高亮/选中用 AccentColor + 白字（照搬 MenuItem/TabItem）。模板无残留 SystemColors 或默认白底。
  - 改动 3 —「更新」按钮已删除；ComboBox 保留 Dock=Right 并移除冗余 Foreground/Background/BorderBrush（由隐式样式接管）；查找框 Grid 为最后一个无 Dock 子元素填充左侧剩余空间；元素均 VerticalAlignment=Center。

## 已知差距与假设

- 「获得焦点后输入才隐藏水印」被实现为「只要查找框有文本即隐藏，空文本（含空焦点）即显示」——这是工业软件水印的标准行为，若需严格「聚焦即隐藏」，可追加 `IsKeyboardFocusWithin` 触发器，但本计划不默认启用以贴合图漾示例（空焦点仍显示提示）。
- ComboBox `ToggleButton` 使用内联模板（嵌在 ComboBox ControlTemplate 内），不单独抽成资源，减少文件改动面。
