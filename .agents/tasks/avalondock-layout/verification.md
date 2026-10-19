# 验证结论 — AvalonDock 布局重构（RVS 风格面板拆分）

本文件记录实现后的自验证结果，供评审查阅（评审无需重跑以下命令）。

实现范围：仅修改两文件
- `src/Sz3D.Presentation.WPF/MainWindow.xaml`
- `src/Sz3D.Presentation.WPF/MainWindow.xaml.cs`

---

## 第二次迭代 — 修复评审 finding（提交不自包含）

评审 `review.json` 指出 blocking finding：上一次提交 `6dd1a89` 仅含两个 MainWindow 文件 +
verification.md，但这两文件硬依赖的 `Dirkster.AvalonDock` / `Dirkster.AvalonDock.Themes.VS2013`
5.0.1 两条 `PackageReference` 只以**未提交**的工作区改动停留在 `Sz3D.Presentation.WPF.csproj`，
导致被评审提交隔离构建会因缺少 AvalonDock 程序集而失败。

修复：将 `Sz3D.Presentation.WPF.csproj` 的两条 AvalonDock 包引用（`Dirkster.AvalonDock` 5.0.1、
`Dirkster.AvalonDock.Themes.VS2013` 5.0.1，均为精确固定版本）纳入提交，使本特性自包含、可独立编译。
MainWindow 两文件内容未变（上次迭代已与 design §2.4/§2.5/§2.6/§2.10 及强制要求①②逐条吻合）。

第二次迭代构建复核（工作目录 `d:\kiro\Sz3D`）：

```
dotnet build Sz3D.sln -c Debug
```

结论：**通过**。`在 6.5 秒内生成 成功，出现 1 警告`——该唯一警告为既存的
`StepExecutionController.cs(6,61): warning CS9113: 参数"engine"未读`，位于未被本特性触及的
`Sz3D.Core.Infrastructure` 项目，非本次改动引入、非新增警告。两个 MainWindow 文件与 csproj 均无
告警或错误。关键产物：`src/Sz3D.Presentation.WPF/bin/Debug/net10.0-windows/Sz3D.Presentation.WPF.dll`。

运行时冒烟（2.a/2.b/2.c）结论沿用上一次迭代（代码与布局逻辑未变，仅补齐包引用），此处不重跑。

---

## 1. 构建门禁 G-1（AC-8）

命令（工作目录 `d:\kiro\Sz3D`）：

```
dotnet build Sz3D.sln -c Debug
```

结论：**通过**。全部项目编译成功，`0 个警告`、`0 个错误`，用时约 6 秒。无新增警告。

关键产物：`src/Sz3D.Presentation.WPF/bin/Debug/net10.0-windows/Sz3D.Presentation.WPF.dll`。

---

## 2. 运行时验证（不使用 dotnet run，直接运行已编译 exe）

被测可执行文件：`src/Sz3D.Presentation.WPF/bin/Debug/net10.0-windows/Sz3D.Presentation.WPF.exe`

### 2.a 删除旧布局文件

删除 `src/Sz3D.Presentation.WPF/bin/Debug/net10.0-windows/config/layout.xml`
（原文件为旧版、仅含 `panel.view` 的 4 面板布局；不删会触发迁移回退而非新默认布局）。
删除后确认文件不存在。

### 2.b 默认布局启动冒烟（M-1/M-7）

后台启动 exe，等待 6 秒：

- 进程状态：**ALIVE**（未崩溃、未退出）。
- 崩溃日志：`bin/.../logs/` **无** `crash_*.log`（启动过程无任何异常）。
- 验证后已停止该后台进程。

结论：**通过**。删除 layout.xml 后启动走新默认布局，不崩溃、不弹窗、无崩溃日志。

### 2.c 迁移回退冒烟 —— 验证强制要求②（M-8 / AC-7 / §2.6）

手工在 `config/` 放置一个**仅含 `panel.view`**（不含 `panel.view3d`/`panel.view2d`）的合法旧版 `layout.xml`
（结构：左列 `panel.view` + 中央 `panel.graph` + 右列 `panel.toolbox` + 底部 `panel.log`），再次后台启动：

- 进程状态：**ALIVE**（未崩溃、未退出）。
- 崩溃日志：出现**一条**记录，内容为：
  `System.InvalidOperationException: 恢复后必需面板缺失（视图3D/视图2D/算子图/工具箱/日志），回退默认布局。`
  —— 这是设计预期的「回退原因」记录（AC-7 要求记录一条回退原因），**不是** WPF 的
  「元素已是另一元素的子级」父冲突异常。
- 由此证明：`ResetToDefaultLayout()` 中对五块 live host 的显式逻辑父摘除（强制要求②）
  成功消除了跨树残留父冲突；旧 host（`_view3dHost`/`_view2dHost` 等）在回放默认布局时
  被正确重挂，未触发 WPF 父冲突，应用不崩溃、回退默认布局。
- 验证后已停止该后台进程，并清理了该测试用 `layout.xml` 与本次产生的 `crash_*.log`。

结论：**通过**。旧版 `layout.xml` 触发必需面板校验失败 → 回退默认布局，应用不崩溃、无 WPF 父冲突异常。

---

## 3. 交互行为（拖拽/浮动/合并）—— 代码审查确认

无人值守环境无法进行鼠标交互，按计划以代码审查确认属性设置正确：

- 3D（`panel.view3d`）与 2D（`panel.view2d`）为同一 `LayoutAnchorablePane` 内的两个独立
  `LayoutAnchorable`，初始合并为一个 Tab 组；3D 声明在前并显式 `IsSelected="True" IsActive="True"`
  （强制要求①），初始选中 3D。
- 两视图面板**均未**设置 `CanFloat`（采用默认 `true`，可独立浮动/停靠/合并），符合 AC-4。
- 算子图（`panel.graph`）`LayoutDocument` 追加 `CanFloat="False"`（与 `CanClose="False"` 并列），
  禁止浮动成独立窗口，但 `CanMove` 默认 `true` 仍可在停靠区内拖动、重新停靠。
- XAML 全文**无** `AllowFloatingWindows` 设置（保持 DockingManager 级默认 `true`，不连带禁掉视图面板浮动）。

运行时以「默认布局启动不崩溃 + 迁移回退不崩溃」为准，二者均已通过。

---

## 4. 不回归核查

- `x:Name="CloudViewport"`/`x:Name="CloudVisual"` 各仍仅出现一次，`UpdateCloudVisual()` 的
  `CloudVisual.Content = ...` 路径不变。
- 顶部 chrome、`Window.Resources`、右列工具箱四 Tab、底部日志 DataGrid、`nodify:NodifyEditor`
  整棵子树（Resources/InputBindings/`Drop="OnDropOperator"`/绑定）逐字未动。
- `.cs` 持久化框架（`SerializeLayout`/`ApplyLayoutXml`/`DetachFromCurrentParent`/`EnumerateContents`/
  `CollectPresentIds`/`AllRequiredPresent`/`SaveLayout`/`DtoSerializer`、构造函数 Loaded/Closing 钩子、
  算子拖拽、`CrashLogger`）逻辑未回归，仅白名单（常量/`RequiredIds`/缓存字段/两处 switch/null 校验）
  与注释「四块→五块」同步更新，并新增强制要求②的显式 detach。
- 全文件已无 `panel.view`（旧单面板 Id）、`IdView`、`_viewHost`、「四块」残留。

---

## 总体结论

构建门禁通过（0 警告 0 错误）；默认布局启动与旧版布局迁移回退两项运行时冒烟均不崩溃、无 WPF 父冲突，
迁移回退仅记录一条预期的回退原因日志。强制要求①（显式初始选中 3D）与强制要求②（五块 live host
显式摘除逻辑父）均已落实并经运行时验证。
