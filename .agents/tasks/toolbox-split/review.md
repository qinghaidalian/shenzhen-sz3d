# Splitting the right-side 工具箱 into four dockable panels

The single 工具箱 `LayoutAnchorable` (one `TabControl` with four `TabItem`s) is replaced by four independent `LayoutAnchorable`s (`panel.props`/`panel.optree`/`panel.oplist`/`panel.debug`) sharing one `LayoutAnchorablePane`, so AvalonDock renders them as a native tab group while letting each be hidden via its X and re-shown individually from the 视图 menu — the 图漾-style behavior the user asked for. The layout-persistence bookkeeping in `MainWindow.xaml.cs` grows from five live panels to eight, and the 视图 menu gains checkable items wired to the existing `TogglePanel`/`FindAnchorable`/`IsPanelVisible` helpers. A supporting checkmark visual was added to the `MenuItem` template in `Dark.xaml`.

Watch for: nothing blocking. The persistence invariant is fully closed across all five bookkeeping sites (confirmed). The `Dark.xaml` change is not an unrelated bundle — it is the rendering mechanism for the new checkable menu items (confirmed).

**Verdict**: APPROVED

## High-level view

The persistence invariant is the highest-risk surface and it is intact. Every one of the four new ContentIds that carries live XAML content is both cached (the `LoadLayout` step-① switch plus the step-①a null assertion) and reattached (the `ReattachContent` switch), `RequiredIds` lists exactly the eight panels that must exist in the tree, the `ResetToDefaultLayout` detach loop covers all eight hosts, and the removed `IdToolbox`/`_toolboxHost` leave no dangling references anywhere in source.

All four anchorables use `CanClose="False" CanHide="True"`, matching the 2D/3D anchorables. That is what lets a hidden panel stay in the layout tree so `Show()` from the menu can restore it, and what lets `AllRequiredPresent()` count a hidden panel as present rather than forcing a fallback to default layout.

The four former `TabItem` bodies moved verbatim, bindings intact — 属性面板's `Editor.SelectedNode` driven `StackPanel`/`ItemsControl` with the `ParameterViewModel` template, and 算子列表's `OperatorGroups` list. 属性面板 is declared first with `IsSelected`/`IsActive` so the group opens on a live tab.

The 视图 menu additions follow the existing `MenuPanelView2D`/`View3D` pattern. `OnViewMenuOpened` syncs each checkable item to actual visibility, pins 算子图 checked-and-disabled, and the 窗口布局一~四 placeholders are preserved. No ViewModel was touched; the handlers live entirely in code-behind.

The `Dark.xaml` edit adds a checkmark `Path` to the `MenuItem` control template so checkable items render a tick — it is required for the new menu items to show their checked state, not a drive-by change.

<details>
<summary>Issues (0)</summary>

No blocking or non-blocking findings. The change is coherent and the persistence invariant is fully closed.

</details>

<details>
<summary>Details</summary>

### Persistence invariant closed at all five sites

The risk the task flags is a ContentId that carries live content but is missing from one of the bookkeeping sites, which would silently blank a panel or force a wrong fallback to default layout. Tracing the four new IDs through the diff:

```
                  cache-grab   null-assert   ReattachContent   RequiredIds   reset detach loop
panel.props          ✓             ✓               ✓               ✓                ✓
panel.optree         ✓             ✓               ✓               ✓                ✓
panel.oplist         ✓             ✓               ✓               ✓                ✓
panel.debug          ✓             ✓               ✓               ✓                ✓
```

The `LoadLayout` step-① switch grabs `_propsHost`/`_opTreeHost`/`_opListHost`/`_debugHost`; the step-①a assertion ORs all eight hosts for null and bails to default layout if any is missing; `ReattachContent`'s `lc.ContentId switch` maps the four IDs to the four hosts; `RequiredIds` is the eight-element array `{ IdView3D, IdView2D, IdGraph, IdProps, IdOpTree, IdOpList, IdDebug, IdLog }`; and the `ResetToDefaultLayout` detach loop iterates all eight hosts. `IdToolbox`/`_toolboxHost` are gone, and a source-wide grep for `IdToolbox|_toolboxHost|panel\.toolbox|ToolboxTab` returns no matches.

`RequiredIds` membership is correct for the hidden-panel case: a hidden (`IsHidden=true`) anchorable remains in the tree and is still enumerated by `Descendents()`, so `CollectPresentIds()` still collects it. Listing the four new panels in `RequiredIds` is therefore both safe (a user hiding a toolbox panel won't trip `AllRequiredPresent()` into a default-layout fallback) and necessary (they must stay in the tree for the menu's `Show()` to restore them). `CollectPresentIds()`/`AllRequiredPresent()` carry no hardcoded ContentIds — they derive entirely from `RequiredIds`, so they adapt automatically.

### CanHide semantics match the 2D/3D anchorables

All four new anchorables declare `CanClose="False" CanHide="True"`, identical to the `panel.view2d`/`panel.view3d` anchorables in the same file. This is the specific pairing that makes the X button hide (not destroy) the panel, keeping it in the layout tree so `LayoutAnchorable.Show()` from the menu brings it back. `CanClose="True"` would have removed it from the tree and broken re-show.

### 视图 menu and 算子图 handling

The six checkable items (`MenuPanelView3D`/`View2D`/`Props`/`OpTree`/`OpList`/`Debug` plus `MenuPanelGraph`) follow the established pattern, and `OnViewMenuOpened` syncs each `IsChecked` to `IsPanelVisible(...)` on submenu open. 算子图 is handled as a central document that cannot be hidden: `OnViewMenuOpened` pins it `IsChecked = true; IsEnabled = false`, and `OnTogglePanelGraph` only re-asserts the check so a click can't remove the document from the layout. The 窗口布局一~四 items are preserved as plain placeholders.

`FindAnchorable` searches `Descendents().OfType<LayoutAnchorable>()`, which reaches hidden panels, so `IsPanelVisible` reports the true state and `TogglePanel` can flip between `Hide()`/`Show()` correctly.

### Dark.xaml checkmark is part of this change, not a bundle

The task description listed only `MainWindow.xaml`/`.cs`, so the `Dark.xaml` edit warrants a look. It adds a `Path x:Name="CheckMark"` at `Grid.Column="0"` of the `MenuItem` control template, shown by an `IsChecked=True` trigger (and turned white under the `IsChecked + IsHighlighted` MultiTrigger). Without it, the new checkable menu items would toggle state invisibly — the user would get no visual feedback that a panel is shown or hidden. It shares column 0 with the `Icon` ContentPresenter, but checkable menu items set no Icon, so there is no overlap. This is the rendering half of the 图漾-style menu and belongs with the change.

### Verification evidence

`verification.md` records a clean `dotnet build` of the WPF project (0 warnings, 0 errors), which compiles the XAML and so validates the moved markup. The runtime smoke test shows the process alive and responding with no new crash logs, then stopped cleanly. The author is explicit that click-level UI behavior (tab grouping, per-panel hide, menu re-show, restart-without-fallback, missing-layout default) was not automated and needs manual confirmation — an honest and appropriate scope statement for an in-place WPF change with no UI test harness. The evidence is adequate for the build/runtime claims; the un-automatable UI checks are correctly surfaced rather than overclaimed.

</details>

<details>
<summary>File map</summary>

- `src/Sz3D.Presentation.WPF/MainWindow.xaml` — 工具箱 `TabControl` replaced by four `LayoutAnchorable`s in one pane; 视图 menu gains checkable items + placeholders; dead ToolboxTab* resources removed.
- `src/Sz3D.Presentation.WPF/MainWindow.xaml.cs` — persistence bookkeeping extended from five to eight panels; `FindAnchorable`/`IsPanelVisible`/`TogglePanel`/`OnViewMenuOpened` and six toggle handlers added; `IdToolbox`/`_toolboxHost` removed.
- `src/Sz3D.Presentation.WPF/Themes/Dark.xaml` — checkmark `Path` + triggers added to the `MenuItem` template to render checkable-item state.
- `.agents/tasks/toolbox-split/verification.md` — build and runtime smoke-test evidence.

Full diff: `git diff 70b6a9d~1 70b6a9d`

</details>
