# 算子列表面板过滤区 UI 微调（查找标签 + 水印 + 深色下拉框，删更新按钮）

本次改动在上一轮「双过滤」基础上做三项 UI 调整：查找框左侧加「查找：」标签、查找框加「过滤」空值水印、删除顶部「更新」按钮，并为 `ComboBox`/`ComboBoxItem` 新增完整深色 `ControlTemplate`（替换 WPF 默认浅色模板）。改动只落在 `MainWindow.xaml` 的 `panel.oplist` 过滤区与 `Themes/Dark.xaml` 两处，未触碰 ViewModel、`NodeLibrary`、拖拽模板与其它面板。水印用局部样式实现，没有改动全局 `TextBox` 隐式样式；下拉框样式照搬 `MenuItem`/`TabItem` 的 `AccentColor` + 白字高亮风格，与主题一致。

Watch for: 水印语义为「只要框内有文本就隐藏」而非严格「聚焦即隐藏」——plan 明示这是贴合图漾示例的刻意取舍，非阻塞（confirmed）；深色下拉模板为纯 XAML 改动、无自动化 UI 断言，依赖构建通过 + 目视核对（confirmed）。

**Verdict**: APPROVED

## High-level view

查找区布局改为 `DockPanel`：「查找：」标签停左、`ComboBox` 停右、查找框 Grid 作为最后一个无 Dock 子元素填充中间剩余空间，所有元素垂直居中，「更新」按钮整行删除。标签用 `PrimaryForeground` 浅字。

水印用覆盖式 `TextBlock Text="过滤"`（`MutedForeground`、`IsHitTestVisible=False`）叠在查找 `TextBox` 上，默认 `Collapsed`，仅当 `OpListSearchBox.Text.IsEmpty==True` 时 `Visible`——空值显示、输入即隐藏，绑定方向正确。水印样式是写在该 `TextBlock` 内的局部 `Style`，不触碰 Dark.xaml 全局 `TextBox` 的 `Background`/`CaretBrush`（二者在 diff 之外保持原样）。

关键改动是 Dark.xaml 新增的两套隐式样式，均带自定义 `ControlTemplate`：`ComboBox` 把主体底色、`ToggleButton` 箭头、`Popup` 弹层 `Border` 全部改成 `ControlBackground` 深底 + `PrimaryForeground` 浅字 + `BorderColor` 边框；`ComboBoxItem` 深色透明底浅字，悬停/高亮/选中统一 `AccentColor` #007ACC 配白字，与 `MenuItem`/`TabItem` 写法一致。模板内无 `SystemColors`、无默认白底残留（已全文件搜索确认）。因是隐式样式，面板内 ComboBox 自动接管，MainWindow.xaml 的 ComboBox 得以移除冗余的内联 Foreground/Background/BorderBrush。

<details>
<summary>Issues (0)</summary>

无阻塞问题，无需整改项。

</details>

<details>
<summary>Details</summary>

## 改动 1：查找标签 + 空值水印

「查找：」`TextBlock` 以 `DockPanel.Dock="Left"`、`VerticalAlignment="Center"`、`Foreground="{StaticResource PrimaryForeground}"` 停于最左，满足「过滤框前加『查找：』」且浅字可读（confirmed）。

水印的实现满足全部关键点：`TextBlock Text="过滤"` 用 `MutedForeground` #9A9A9A（即图漾示例里那种灰字可见、非白底白字），`IsHitTestVisible="False"` 让点击穿透到下层 `TextBox`。可见性由局部 `Style` 控制：

```xml
<Setter Property="Visibility" Value="Collapsed"/>
<DataTrigger Binding="{Binding Text.IsEmpty, ElementName=OpListSearchBox}" Value="True">
    <Setter Property="Visibility" Value="Visible"/>
</DataTrigger>
```

默认 `Collapsed`、空值时 `Visible`——绑定方向正确，空框显示水印、输入后隐藏（confirmed）。这个 `Style` 内联在水印 `TextBlock` 上、`TargetType="TextBlock"` 且无 Key，作用域仅此元素，不影响全局；查找 `TextBox` 本身只设了 `Foreground`，未覆写 `Background` 或 `CaretBrush`，所以 Dark.xaml 第 110–116 行的全局 `TextBox` 样式（含 `CaretBrush="{StaticResource PrimaryForeground}"`）在本次 diff 中完全未动，深色外观与光标颜色得以保留（confirmed，已逐行比对全局 TextBox 样式块未进入 diff）。

水印语义上是「有文本即隐藏」，空框即便聚焦仍显示提示——plan 将其列为贴合图漾示例的刻意取舍，非缺陷。

## 改动 2（关键）：ComboBox / ComboBoxItem 深色模板

两套样式均为带自定义 `ControlTemplate` 的隐式样式，位置在 TextBox 样式之后、菜单样式之前，归类集中。

`ComboBox` 模板根为 `Grid`，三层叠放：
- `ToggleButton`（覆盖整控件、`Focusable=False`、`ClickMode=Press`、`IsChecked` 双向绑 `IsDropDownOpen`），自身模板是深色 `Border`（`Background`/`BorderBrush`/`BorderThickness` 走 TemplateBinding，最终取 `ControlBackground` 深底 + `BorderColor` 边框）内含右侧向下箭头 `Path`（`Fill="{StaticResource PrimaryForeground}"`），满足「主体背景、箭头均深色」。
- `ContentPresenter`（选中项文本）`TextElement.Foreground="{StaticResource PrimaryForeground}"` 浅字、左对齐、`IsHitTestVisible=False` 让点击穿透到 ToggleButton。
- `Popup x:Name="PART_Popup"` 内 `Border` 用 `Background="{StaticResource ControlBackground}"` 深底 + `BorderColor` 边框（非系统默认白底），内包 `ScrollViewer` + `ItemsPresenter`。弹层深色这一点正是用户第 5 条「算子库下拉」与第 6 条「字体看得清」的核心诉求所在（confirmed）。

`ComboBoxItem` 模板为 `Border x:Name="Bd"` + `ContentPresenter`，默认透明深底浅字；`IsMouseOver`/`IsHighlighted`/`IsSelected` 三个触发器统一把 `Bd.Background` 设为 `AccentColor` #007ACC、`Foreground` 设 `#FFFFFF`，与 `MenuItem` 的 `IsHighlighted`、`TabItem` 的 `IsSelected` 写法完全一致（已比对 Dark.xaml 第 346、438 行）。

全文件搜索 `SystemColors`/`SystemColorBrush` 无任何匹配，模板未残留系统画刷或默认白底；所有引用的 `PrimaryForeground`/`ControlBackground`/`BorderColor`/`AccentColor` 均在 Dark.xaml 第 8–14 行定义，不会抛 StaticResource 解析异常（confirmed）。审查标准明确要求「只设属性而无自定义模板为不合格」——本实现有完整的 ControlTemplate，达标。

## 改动 3：删「更新」按钮 + 布局

diff 中 `<Button ... Content="更新" .../>` 整行被删，面板顶部不再有该按钮（confirmed）。布局用 `DockPanel`：标签 Dock=Left、ComboBox Dock=Right（保留 `Width="120"`、`Margin="6 0 0 0"`、新增 `VerticalAlignment="Center"`），查找框 `Grid` 为唯一无 Dock 子元素，按 DockPanel 规则填充中间剩余空间，正好实现「ComboBox 靠右、标签+查找框占左侧剩余、垂直居中」。ComboBox 上原先的 `Foreground`/`Background`/`BorderBrush` 内联设置已移除，交由新的隐式深色样式统一接管，减少重复且保证风格一致（confirmed）。

## 约束核对

逐行比对 diff（`git diff 24bdd7b 5ed8a7e`）与磁盘实际文件：改动仅触及 `MainWindow.xaml` 的 `panel.oplist` 过滤区段与 `Themes/Dark.xaml` 两处，外加 plan.md 文档更新。未改 `MainViewModel`/`NodeLibrary`（不在 diff 内）；`ItemsControl ItemsSource="{Binding FilteredGroups}"`、下方 `Expander`/内层 `ItemsControl ItemsSource="{Binding Items}"` 与算子拖拽 `DataTemplate` 均保持原样；其它面板未动；无新增 NuGet 依赖（diff 不含 csproj/包引用变更）（confirmed）。

## 验证证据

plan.md「验证记录（第一次迭代）」记录：构建前 `Stop-Process` 释放 DLL 锁；`dotnet build src\Sz3D.Presentation.WPF\...csproj -c Debug` → Exit Code 0、7.1s、无错误无新增警告（含 XAML 标记编译）；后台启动 exe 存活 3s、未崩溃、无异常堆栈，验证后结束进程保持环境干净。证据具体、与 diff 自洽——纯 XAML 改动、所有 StaticResource 已核实存在、模板结构完整且无系统画刷残留，无可指明的疑点，按指示不复跑构建/启动套件。

## 未测试项

无 WPF UI 自动化测试工程，水印可见性切换、下拉弹层深色渲染、悬停/选中高亮、布局填充这几项以构建通过 + 目视核对为准，无断言覆盖。给定仓库现状（测试工程只覆盖 Core），这是既有约束，不作为本次阻塞项。

</details>

<details>
<summary>File map</summary>

- `src/Sz3D.Presentation.WPF/MainWindow.xaml` — `panel.oplist` 过滤区：删「更新」按钮；加 Dock=Left 的「查找：」标签；查找框包进 Grid 并叠加「过滤」空值水印（局部 Style + `Text.IsEmpty` DataTrigger）；ComboBox 加 `VerticalAlignment=Center` 并移除冗余内联画刷。
- `src/Sz3D.Presentation.WPF/Themes/Dark.xaml` — 新增深色 `ComboBox` + `ComboBoxItem` 隐式样式，各带自定义 ControlTemplate（深底浅字、深色 Popup 弹层、AccentColor + 白字高亮）。
- `.agents/tasks/oplist-filter/plan.md` — 更新实现计划与验证记录。

完整 diff：`git -C d:\kiro\Sz3D diff 24bdd7b 5ed8a7e`
</details>
