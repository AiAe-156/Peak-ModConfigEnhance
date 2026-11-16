#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ModConfigEnhance.Overview;

/// <summary>
/// 1.1.0：快捷键设置页（从游戏内模组管理器 OverviewHotkeyView.cs 搬来并精简）。
/// 与管理器的不同（用户 2026-11-15 拍板）：
///   · 没有配置方案：去掉「方案预设」列、「预设调整」「保存快捷键」按钮；录完、恢复默认、清空都立即写进模组配置。
///   · 分组（概览页第 8a 章加回，照 IGMM）：与模组列表共用 overview.json 的分组与分组折叠（collapsedGroups），
///     分组标题可折叠，双击标题或标题上按 Y / 右键 = 分组菜单（管理分组 / 编辑分组）；组内模组仍按显示名排序。
///     没有任何分组时与之前完全一样（不画「未分组」标题）。模组行的折叠仍记在 hotkeys.json。
///   · 手柄（上线前修正，两页统一）：LB / RB 归外壳（工具行 / RT + LB / RB 切页签）；RT + ←→ 翻页，RT + L3 全部展开 / 收起。
///   · 手柄组合键可自由组合：先按住任意几个键，再按最后一个键（见 <see cref="GamepadCombos"/>）。
///   · 短说明直接双击（手柄按 A）编辑，没有编辑模式。
/// 由页面外壳 <see cref="OverviewPage"/> 在「快捷键设置」页签里构造，外壳直接调 Root / IsRecording / TryCloseLayer / OnOpened / OnClosed / FocusForGamepad
/// （模组清单、状态行、齿轮都由外壳提供；工具行 ↑ 由 <see cref="ToolbarAbove"/> 指回页签）。
/// </summary>
internal sealed class OverviewHotkeyView : IDisposable
{
    private readonly Func<IReadOnlyList<OverviewMod>> _mods;
    private readonly Action<string> _status;
    private readonly Action<OverviewMod>? _openSettings;
    private readonly Func<OverviewMod, bool>? _canOpenSettings;
    private readonly RectTransform _root, _tools, _header, _content, _viewport, _hint;
    private readonly TextMeshProUGUI _hintText;
    private readonly TMP_InputField _search;
    private readonly TextMeshProUGUI _placeholder;
    private readonly ScrollRect _scroll;
    private readonly OverviewListPump _pump;
    private readonly OverviewHotkeyCapture _capture;
    private readonly Dictionary<string, GameObject> _targets = new(StringComparer.Ordinal);
    /// <summary>实测修正（2026-11-16）：列表里每一行可聚焦的格子（按渲染顺序），手柄导航按行 / 列显式连接，不再用 Unity 自动导航。</summary>
    private readonly List<List<Selectable>> _grid = new();
    private float? _stickyX; // 连续上下移动时记住的列位置（经过只有箭头的标题行也回到原来那一栏）
    private bool _gridMoving;
    private string _selectedId = "", _query = "";
    private int _filter, _revision = -1;
    private float _width;
    private bool _opened, _disposed, _rendering, _dirty, _loaded;
    private bool _hintFollowsPointer;
    private RectTransform? _hintAnchor;
    public GameObject Root => _root.gameObject;
    /// <summary>The control above the tool row (the page's tab / back button), for ↑ from the tool row.</summary>
    internal Func<Selectable, Selectable?>? ToolbarAbove;
    public OverviewHotkeyService Service { get; } = new();
    public bool IsRecording => _capture.IsCapturing || _comboRecording;
    private static HotkeyPageState State => HotkeyPageState.Current;

    // ── 分组（概览页第 8a 章，照 IGMM）────────────────────────────────
    /// <summary>外壳的仓库（<c>OverviewPage.Repository</c>，与模组列表共用分组与折叠）。外壳构造本页后立即接上；没接时按不分组显示。</summary>
    internal Func<OverviewRepository?>? GroupRepository { get; set; }
    /// <summary>分组菜单「管理分组」：外壳打开分组编辑窗口。</summary>
    public Action? ManageGroups { get; set; }
    /// <summary>分组菜单「编辑分组」与双击分组标题：外壳打开分组编辑窗口。</summary>
    public Action<OverviewGroup?>? EditGroup { get; set; }
    private bool _groupsWarned;

    private OverviewRepository? Groups()
    {
        try
        {
            return GroupRepository?.Invoke();
        }
        catch (Exception ex)
        {
            if (!_groupsWarned) { _groupsWarned = true; Plugin.Log.LogWarning("[快捷键] 读取分组失败，按不分组显示：" + ex.Message); }
            return null;
        }
    }
    private void SaveGroups(OverviewRepository repo)
    {
        try { repo.Save(); }
        catch (Exception ex) { _status(Loc.T("Could not save the group fold state: ", "分组折叠状态保存失败：", "分組折疊狀態儲存失敗：") + ex.Message); }
    }
    private void ToggleGroup(string id)
    {
        var repo = Groups(); if (repo == null) return;
        if (!repo.State.collapsedGroups.Remove(id)) repo.State.collapsedGroups.Add(id);
        SaveGroups(repo); _selectedId = "group:" + id; Render();
        if (_targets.TryGetValue(_selectedId, out var header)) EventSystem.current?.SetSelectedGameObject(header);
    }
    private void OpenGroupEditor(OverviewGroup? group, bool manage)
    {
        // 1.1.0（MCE，第 11 章）：编辑窗口由外壳打开（同 IGMM 走列表的 ManageGroups）；关窗后手柄焦点回本页（见 Tick）。
        _refocusAfterDialog = true;
        if (manage) ManageGroups?.Invoke(); else EditGroup?.Invoke(group);
    }
    private void GroupMenu(OverviewGroup? group, RectTransform anchor)
    {
        var items = new List<(string Label, Action Action, bool Enabled)> {
            (Loc.T("Manage groups", "管理分组", "管理分組"), () => OpenGroupEditor(null, true), true) };
        if (group != null) items.Add((Loc.T("Edit group", "编辑分组", "編輯分組"), () => OpenGroupEditor(group, false), true));
        Popup(anchor, items);
    }

    public OverviewHotkeyView(RectTransform parent, Func<IReadOnlyList<OverviewMod>> mods, Action<string> status,
        Action<OverviewMod>? openSettings = null, Func<OverviewMod, bool>? canOpenSettings = null)
    {
        _mods = mods; _status = status; _openSettings = openSettings; _canOpenSettings = canOpenSettings;
        _root = OverviewUi.Rect("OverviewHotkeys", parent); OverviewUi.Fill(_root);
        _tools = OverviewUi.Rect("Tools", _root); _header = OverviewUi.Panel("Header", _root, ModConfigLook.Surface(new Color(.12f, .13f, .15f, .92f)));
        _search = OverviewUi.Input(_tools, ""); _search.characterLimit = 256;
        _search.textComponent.fontSize = 16;
        _placeholder = OverviewUi.Text(_search.transform, "", 15); OverviewUi.Fill(_placeholder.rectTransform, 4); _placeholder.color = OverviewUi.Muted; _search.placeholder = _placeholder;
        _search.onValueChanged.AddListener(v => { _query = v; Render(); });
        var host = OverviewUi.Rect("Scroll", _root); OverviewUi.Fill(host); host.offsetMax = new Vector2(0, -73);
        _viewport = OverviewUi.Panel("Viewport", host, Color.clear); OverviewUi.Fill(_viewport); _viewport.offsetMax = new Vector2(-15, 0); _viewport.gameObject.AddComponent<RectMask2D>();
        _content = OverviewUi.Rect("Rows", _viewport); _content.anchorMin = new Vector2(0, 1); _content.anchorMax = Vector2.one; _content.pivot = new Vector2(0, 1);
        _scroll = host.gameObject.AddComponent<ScrollRect>(); _scroll.viewport = _viewport; _scroll.content = _content; _scroll.horizontal = false; _scroll.scrollSensitivity = 40; _scroll.movementType = ScrollRect.MovementType.Clamped;
        var bar = OverviewUi.Panel("Scrollbar", host, ModConfigLook.Surface(new Color(.1f, .1f, .1f, .5f))); bar.anchorMin = new Vector2(1, 0); bar.anchorMax = Vector2.one; bar.offsetMin = new Vector2(-12, 0); bar.offsetMax = Vector2.zero;
        var handle = OverviewUi.Panel("Handle", bar, OverviewUi.Muted); OverviewUi.Fill(handle);
        var scrollbar = bar.gameObject.AddComponent<Scrollbar>(); scrollbar.handleRect = handle; scrollbar.targetGraphic = handle.GetComponent<Image>(); scrollbar.direction = Scrollbar.Direction.BottomToTop; _scroll.verticalScrollbar = scrollbar;
        _hint = OverviewUi.Panel("HotkeyHint", _root, ModConfigLook.Surface(new Color(.08f, .08f, .1f, .98f))); _hint.GetComponent<Image>().raycastTarget = false;
        _hintText = OverviewUi.Text(_hint, "", 17); OverviewUi.Fill(_hintText.rectTransform, 10); _hintText.textWrappingMode = TextWrappingModes.Normal; _hint.gameObject.SetActive(false);
        Service.Changed += OnChanged;
        _capture = _root.gameObject.AddComponent<OverviewHotkeyCapture>();
        _pump = _root.gameObject.AddComponent<OverviewListPump>(); _pump.Tick = Tick;
    }

    private void OnChanged() => _dirty = true;
    public void Reload() { Service.Reload(_mods()); _loaded = true; _dirty = false; Render(); }
    public void OnOpened()
    {
        _opened = true;
        if (!_loaded) Reload(); else if (_dirty) { _dirty = false; Render(); }
        if (_targets.TryGetValue(_selectedId, out var target) && target.activeInHierarchy) EventSystem.current?.SetSelectedGameObject(target);
    }
    public void OnClosed()
    {
        if (_comboRecording) EndComboRecording();
        GamepadIntent.Release(_root.gameObject);
        var selected = EventSystem.current?.currentSelectedGameObject;
        if (selected != null) { var pair = _targets.FirstOrDefault(p => p.Value == selected); if (pair.Key != null) _selectedId = pair.Key; }
        _opened = false; _capture.CancelCapture(); ClosePopup(); _hint.gameObject.SetActive(false);
    }
    public bool TryCloseLayer()
    {
        if (_capture.CancelCapture()) { _status(Loc.T("Recording cancelled.", "已取消按键录制。", "已取消按鍵錄製。")); return true; }
        if (_comboRecording) return true; // B is a recordable button; hold B to finish (TickComboRecording)
        return false; // 分组编辑窗口归外壳（OverviewPage.TryCloseLayer 先问它）
    }

    private void Tick()
    {
        if (!_opened || _disposed) return;
        if (_hint.gameObject.activeSelf && _hintFollowsPointer) PositionHint();
        // The mod-default column follows the input device (keyboard default / controller default).
        if (_padDefaults != GamepadSupport.IsGamepad && !_capture.IsCapturing && !_comboRecording) { _padDefaults = GamepadSupport.IsGamepad; _dirty = true; }
        if (_comboRecording)
        {
            // 第 5 章（二次审查）：录制期间手柄被 GamepadLock 整支锁住；这里每帧抛异常的话「长按 B 1 秒」永远跑不到，
            // 只用手柄的玩家会被卡死。出错就立刻结束录制、解锁。
            try { TickComboRecording(); }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[快捷键] 手柄录制出错，已停止并解锁：" + ex);
                EndComboRecording();
                _status(Loc.T("Recording stopped because of an error; see the log.", "录制出错，已停止并解锁手柄，详见日志。", "錄製出錯，已停止並解鎖手把，詳見日誌。"));
            }
        }
        bool layerOpen = OverviewUi.HasModal || WindowPopover.AnyOpen || OverviewGroupEditor.AnyOpen; // 分组编辑窗口开着时背后不响应手势
        if (_refocusAfterDialog && !OverviewUi.HasModal && !OverviewGroupEditor.AnyOpen) { _refocusAfterDialog = false; if (GamepadSupport.IsGamepad) FocusForGamepad(); }
        if (!_comboRecording && GamepadSupport.IsGamepad && !layerOpen && !_capture.IsCapturing && !_search.isFocused) ListGestures();
        if (!_rendering && (_dirty || Math.Abs(_root.rect.width - _width) > 2 || _revision != OverviewLanguageBridge.Revision)) { _dirty = false; Render(); }
        // The right stick only scrolls; the game also binds it to UI navigation, so navigation pauses while it moves.
        bool free = !layerOpen && !_capture.IsCapturing && !_comboRecording && !_search.isFocused;
        GamepadIntent.HoldNavigation(_root.gameObject, free && GamepadIntent.WantsHold);
        if (free && GamepadSupport.IsGamepad && Gamepad.current is Gamepad pad && EventSystem.current is EventSystem system
            && (system.currentSelectedGameObject == null || !system.currentSelectedGameObject.activeInHierarchy)
            && (pad.dpad.up.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame || pad.buttonSouth.wasPressedThisFrame || GamepadIntent.PressedDirection() != MoveDirection.None))
            FocusForGamepad(); // focus lost (mouse, back from settings): the next press puts it back
        if (free && GamepadSupport.IsGamepad && Gamepad.current?.rightStickButton.wasPressedThisFrame == true && _search.gameObject.activeInHierarchy)
            EventSystem.current?.SetSelectedGameObject(_search.gameObject);
        if (free && GamepadIntent.ScrollY != 0)
        { _scroll.StopMovement(); SetPixel(_content.anchoredPosition.y - GamepadIntent.ScrollY * GamepadIntent.ScrollSpeed(600) * Time.unscaledDeltaTime); }
    }

    private void SetPixel(float value) => _content.anchoredPosition = new Vector2(0, Mathf.Clamp(value, 0, Mathf.Max(0, _content.rect.height - _viewport.rect.height)));
    private static void Clear(Transform parent) { for (int i = parent.childCount - 1; i >= 0; i--) { var go = parent.GetChild(i).gameObject; go.SetActive(false); UnityEngine.Object.Destroy(go); } }

    private void Render()
    {
        if (!_loaded || _disposed || _rendering) return; _rendering = true;
        try
        {
            var selected = EventSystem.current?.currentSelectedGameObject;
            bool ownFocus = selected != null && selected.transform.IsChildOf(_content);
            if (ownFocus) { var old = _targets.FirstOrDefault(p => p.Value == selected); if (old.Key != null) _selectedId = old.Key; EventSystem.current?.SetSelectedGameObject(null); }
            float pixel = Math.Max(0, _content.anchoredPosition.y); _scroll.StopMovement();
            _hint.gameObject.SetActive(false); ClosePopup(); Clear(_content); Clear(_header);
            // Keep the search field alive while typing; tool buttons are rebuilt without replacing that input.
            for (int i = _tools.childCount - 1; i >= 0; i--) if (_tools.GetChild(i) != _search.transform) { var go = _tools.GetChild(i).gameObject; go.SetActive(false); UnityEngine.Object.Destroy(go); }
            _targets.Clear(); _grid.Clear(); _width = _root.rect.width; float width = Mathf.Max(1, _width - 15); _revision = OverviewLanguageBridge.Revision;
            OverviewUi.Place(_tools, 0, 0, width, 34); OverviewUi.Place(_header, 0, 37, width, 32);
            float searchWidth = Mathf.Clamp(width * .32f, 220, 420); OverviewUi.Place((RectTransform)_search.transform, 0, 0, searchWidth, 32);
            GamepadSupport.MarkSelectable(_search.gameObject, null, null);
            _placeholder.text = Loc.T("Search name, mod, description or key", "搜索名称、模组、说明或按键", "搜尋名稱、模組、說明或按鍵");
            Button filter = null!;
            filter = OverviewUi.Button(_tools, FilterLabel(), () => Popup((RectTransform)filter.transform, new[] {
                (Loc.T("All", "全部", "全部"), (Action)(() => SetFilter(0)), true),
                (Loc.T("Conflicts", "重复", "重複"), (Action)(() => SetFilter(1)), true),
                (Loc.T("Unbound", "未绑定", "未綁定"), (Action)(() => SetFilter(2)), true) }), 96, 30, role: UiActionRole.Edit);
            OverviewUi.Place((RectTransform)filter.transform, searchWidth + 6, 1, 96, 30);
            float toolX = searchWidth + 108;
            Button more = null!;
            more = OverviewUi.Button(_tools, Loc.T("More", "更多", "更多"), () => Popup((RectTransform)more.transform, new[] {
                (Loc.T("Restore all duplicate warnings", "恢复全部冲突提醒", "恢復全部衝突提醒"), (Action)(() => { if (Service.ClearIgnored(out var error)) { _status(Loc.T("All duplicate warnings restored.", "已恢复全部冲突提醒。", "已恢復全部衝突提醒。")); Render(); } else _status(error); }), State.ignoredHotkeyConflicts.Count > 0),
                (Loc.T("Reload keybindings", "重新读取快捷键", "重新讀取快捷鍵"), (Action)(() => { Reload(); _status(Loc.T("Keybindings reloaded.", "已重新读取快捷键。", "已重新讀取快捷鍵。")); }), true) }), 76, 30, role: UiActionRole.Neutral);
            OverviewUi.Place((RectTransform)more.transform, toolX, 1, 76, 30); toolX += 82;
            var groupRepo = Groups();
            var tree = new GroupTree(groupRepo?.State.groups ?? new List<OverviewGroup>());
            bool grouped = groupRepo != null && tree.Flat.Count > 0;
            bool anyFolded = Service.Entries.Select(e => e.Mod.Id).Distinct().Any(m => State.collapsedHotkeyMods.Contains(m))
                || grouped && groupRepo!.State.groups.Any(g => groupRepo.State.collapsedGroups.Contains(g.id));
            // 版面修正（用户 2026-11-15）：原来是对齐手柄栏的孤立 ▼ 图标，改成工具行里「更多」旁的文字按钮。
            string foldText = anyFolded ? Loc.T("Expand all", "展开全部", "展開全部") : Loc.T("Collapse all", "收起全部", "收起全部");
            float foldWidth = Mathf.Max(76, Measure(foldText, 15) + 28);
            var foldAll = OverviewUi.Button(_tools, foldText, () => ToggleAllFolds(), foldWidth, 30, role: UiActionRole.Neutral);
            OverviewUi.Place((RectTransform)foldAll.transform, toolX, 1, foldWidth, 30); toolX += foldWidth + 6;
            Hint(foldAll.gameObject, (grouped
                    ? anyFolded ? Loc.T("Expand all groups and mods", "展开全部分组与模组", "展開全部分組與模組") : Loc.T("Collapse all groups and mods", "收起全部分组与模组", "收起全部分組與模組")
                    : anyFolded ? Loc.T("Expand all mods", "展开全部模组", "展開全部模組") : Loc.T("Collapse all mods", "收起全部模组", "收起全部模組"))
                + "\n" + Loc.T("Controller: hold RT and press L3.", "手柄：按住 RT 再按 L3。", "手把：按住 RT 再按 L3。"));
            float xName = 48, conflictWidth = Mathf.Clamp(width * .08f, 32, 80);
            // 版面修正（用户 2026-11-15）：名称栏按最长的配置项名分配（原来固定 ≤220，长名被缩到很小还截断），
            // 最多占三成宽；短说明栏拿剩下的。
            float nameColumn = Mathf.Clamp(Service.Entries.Select(e => Measure(e.Name, 16) + 14).DefaultIfEmpty(0)
                .Append(Measure(Loc.T("Setting", "配置项名", "設定項名"), 16) + 14).Max(), 120, Mathf.Max(120, width * .3f));
            // Budget the three binding columns together, leaving a real description column on narrow pages.
            float keyLimit = Mathf.Max(52, Mathf.Min(150, (width - xName - nameColumn - conflictWidth - Mathf.Min(140, width * .2f)) / 3));
            float KeyWidth(IEnumerable<string> labels) => labels.Select(s => Measure(s, 15) + 16).DefaultIfEmpty(74).Max();
            float keyWidth = Mathf.Min(keyLimit, Mathf.Max(74, Mathf.Max(
                KeyWidth(Service.Entries.Select(e => e.CurrentLabel).Concat(Service.Entries.Select(e => e.DefaultLabel))),
                new[] { Loc.T("Keyboard", "键盘按键", "鍵盤按鍵"), Loc.T("Controller", "手柄按键", "手把按鍵"), Loc.T("Mod default", "模组默认", "模組預設") }.Max(s => Measure(s, 16)) + 12)));
            float xDesc = xName + nameColumn, xCurrent = width - keyWidth * 3 - conflictWidth, xPad = xCurrent + keyWidth, xConflict = xPad + keyWidth, xModDefault = xConflict + conflictWidth;
            Label(_header, Loc.T("Setting", "配置项名", "設定項名"), xName, 1, xDesc - xName - 6, 30);
            Label(_header, Loc.T("Description", "短说明", "短說明"), xDesc, 1, xCurrent - xDesc - 6, 30);
            var currentTitle = Label(_header, Loc.T("Keyboard", "键盘按键", "鍵盤按鍵"), xCurrent, 1, keyWidth - 4, 30); currentTitle.alignment = TextAlignmentOptions.Midline;
            var padTitle = Label(_header, Loc.T("Controller", "手柄按键", "手把按鍵"), xPad, 1, keyWidth - 4, 30, PadAccent); padTitle.alignment = TextAlignmentOptions.Midline;
            var conflictTitle = Label(_header, Loc.T("Conflict", "冲突", "衝突"), xConflict, 1, xModDefault - xConflict - 2, 30); conflictTitle.alignment = TextAlignmentOptions.Midline;
            var defaultTitle = Label(_header, Loc.T("Mod default", "模组默认", "模組預設"), xModDefault, 1, keyWidth - 4, 30, OverviewUi.Green); defaultTitle.alignment = TextAlignmentOptions.Midline;
            foreach (var columnTitle in new[] { currentTitle, padTitle, conflictTitle, defaultTitle })
            {
                columnTitle.enableAutoSizing = true; columnTitle.fontSizeMin = 12; columnTitle.fontSizeMax = 16;
                Hint(columnTitle.gameObject, columnTitle.text);
            }
            Hint(conflictTitle.gameObject, conflictTitle.text + "\n" + Loc.T("The same key is also used by another mod or the game. The arrow points at the clashing column; click for details.",
                "同一个键也被别的模组或游戏占用。箭头指向冲突的那一栏，点击查看详情。", "同一個鍵也被別的模組或遊戲佔用。箭頭指向衝突的那一欄，點擊查看詳情。"));
            Hint(padTitle.gameObject, padTitle.text + "\n" + Loc.T("Controller keys for mods that only read the keyboard are pressed by this mod while you play (not in menus).",
                "只认键盘的模组，局内由本模组替它按下键盘键（菜单里不生效）。", "只認鍵盤的模組，局內由本模組替它按下鍵盤鍵（選單裡不生效）。"));

            // 第 8a 章：先按分组树顺序（未分组在最后），组内仍按外壳给的显示名顺序。
            string GroupOfMod(OverviewMod m) { if (!grouped) return ""; string g = groupRepo!.GroupOf(m.Id); return tree.Contains(g) ? g : ""; }
            var order = _mods().Select((m, i) => (m.Id, i)).ToDictionary(p => p.Id, p => p.i);
            // 实测修正（2026-11-16）：子分组排在本组直属模组前面（GroupTree.RowKey，与模组列表、设置页侧栏一致）。
            var visible = Service.Entries.Where(Matches).OrderBy(e => tree.RowKey(GroupOfMod(e.Mod))).ThenBy(e => order.TryGetValue(e.Mod.Id, out int i) ? i : int.MaxValue).ThenBy(e => e.Section).ThenBy(e => e.Key).ToList();
            bool foldApplies = _filter == 0 && string.IsNullOrWhiteSpace(_query);
            float y = 0;
            var emitted = new HashSet<string>(StringComparer.Ordinal);
            // = IGMM Header()：分组标题（色条按层级、箭头折叠、名称 + 浅色描述）；第 8a 章加双击 / Y / 右键分组菜单。
            void Header(string id)
            {
                if (!emitted.Add(id)) return;
                var group = tree.Get(id); Color color = ColorUtility.TryParseHtmlString(group?.color ?? "", out var c) ? c : OverviewUi.Muted;
                int depth = Math.Max(1, tree.Depth(id)); float indent = (depth - 1) * OverviewListView.GroupIndent;
                bool folded = foldApplies && groupRepo!.State.collapsedGroups.Contains(id);
                float height = depth <= 1 ? 34 : 30;
                var row = OverviewUi.Panel("Group_" + id, _content, Color.Lerp(ModConfigLook.Surface(new Color(.1f, .11f, .12f, .95f)), new Color(color.r, color.g, color.b, .95f), depth <= 1 ? .14f : .09f)); OverviewUi.Place(row, 0, y, width, height);
                float railX = 0; int railLevel = 0;
                foreach (var level in group == null ? new List<OverviewGroup>() : tree.Chain(id))
                {
                    var rail = OverviewUi.Rail("GroupRail", row, ColorUtility.TryParseHtmlString(level.color, out var lc) ? lc : OverviewUi.Muted);
                    float rw = GroupRailShape.Width(OverviewListView.RailWidth, railLevel); var (ry, rh) = GroupRailShape.Vertical(0, height, railLevel);
                    OverviewUi.Place(rail, railX, ry, rw, rh); railX += rw + OverviewListView.RailGap; railLevel++;
                }
                Button arrow = null!; _grid.Add(new List<Selectable>());
                arrow = OverviewUi.IconButton(row, folded ? "triangle-right" : "triangle-down", () => ToggleGroup(id), 22, 26); OverviewUi.Place((RectTransform)arrow.transform, 6 + indent, depth <= 1 ? 3 : 2, 22, 26); Track(arrow, "group:" + id);
                arrow.gameObject.AddComponent<OverviewHotkeyTextAction>().ContextRequested = () => GroupMenu(group, (RectTransform)arrow.transform);
                string title = group?.name ?? Loc.T("Ungrouped", "未分组", "未分組"); float size = depth <= 1 ? 20 : depth == 2 ? 18 : 17; float nameWidth = Mathf.Min(width * .42f, Measure(title, size) + 10);
                var titleText = Label(row, title, 32 + indent, 0, nameWidth, depth <= 1 ? 32 : 28, color, size); OverviewUi.ReadableName(titleText, nameWidth); titleText.richText = false;
                Hint(titleText.gameObject, (group == null ? title : tree.Path(id)) + "\n" + Loc.T("Click the arrow to fold. Double-click, right-click or Y: group menu.", "点箭头折叠；双击、右键或按 Y：分组菜单。", "點箭頭折疊；雙擊、右鍵或按 Y：分組選單。"));
                titleText.raycastTarget = true; // 第 8a 章：IGMM 这里没开射线，双击收不到
                var titleAction = titleText.gameObject.AddComponent<OverviewHotkeyTextAction>();
                titleAction.DoubleClicked = () => { if (group != null) OpenGroupEditor(group, false); else OpenGroupEditor(null, true); };
                titleAction.ContextRequested = () => GroupMenu(group, (RectTransform)arrow.transform);
                Label(row, group?.description ?? "", 36 + indent + nameWidth, 0, Math.Max(0, width - nameWidth - indent - 42), depth <= 1 ? 32 : 28, OverviewUi.Muted, 14); y += depth <= 1 ? 36 : 32;
            }
            foreach (var grouping in visible.GroupBy(e => GroupOfMod(e.Mod)))
            {
            string groupId = grouping.Key;
            float modX = 0; Color? modRailColor = null;
            if (grouped)
            {
                // Every ancestor header first, so a subgroup is never shown without its parent; a header under a collapsed ancestor folds away.
                if (foldApplies && groupId.Length > 0 && tree.HeaderHidden(groupId, groupRepo!.State.collapsedGroups)) continue;
                foreach (var ancestor in tree.Ancestors(groupId)) Header(ancestor.id);
                Header(groupId);
                if (foldApplies && groupRepo!.State.collapsedGroups.Contains(groupId)) continue;
                modRailColor = ColorUtility.TryParseHtmlString(tree.Get(groupId)?.color ?? "", out var groupColor) ? groupColor : OverviewUi.Muted;
                modX = 14 + (Math.Max(1, tree.Depth(groupId)) - 1) * OverviewListView.GroupIndent;
            }
            float modWidth = width - modX;
            foreach (var modEntries in grouping.GroupBy(e => e.Mod.Id))
            {
                var mod = modEntries.First().Mod; bool collapsed = foldApplies && State.collapsedHotkeyMods.Contains(mod.Id);
                var modRow = OverviewUi.Panel("Mod_" + mod.Id, _content, ModConfigLook.Surface(new Color(.11f, .13f, .15f, .75f))); OverviewUi.Place(modRow, modX, y, modWidth, 32);
                _grid.Add(new List<Selectable>());
                var modArrow = OverviewUi.IconButton(modRow, collapsed ? "triangle-right" : "triangle-down", () => ToggleMod(mod.Id), 20, 24); OverviewUi.Place((RectTransform)modArrow.transform, 4, 4, 20, 24); Track(modArrow, "mod:" + mod.Id);
                if (modRailColor is Color railColor) { var modRail = OverviewUi.Rail("GroupRail", modRow, railColor); OverviewUi.Place(modRail, 0, 0, 2, 32); }
                var name = Label(modRow, OverviewLanguageBridge.DisplayNameWithOriginal(mod), 28, 0, modWidth - 64, 30, OverviewUi.Ivory, 18); name.richText = true; OverviewUi.ReadableName(name, modWidth - 64);
                Hint(name.gameObject, mod.RawName + (mod.PluginVersion.Length > 0 ? "  v" + mod.PluginVersion : "") + "\n" + mod.Guid);
                if (_openSettings != null)
                {
                    bool canOpen = _canOpenSettings?.Invoke(mod) ?? true;
                    var gear = OverviewUi.IconButton(modRow, "gear", () => _openSettings(mod), 28, 28); OverviewUi.Place((RectTransform)gear.transform, modWidth - 34, 2, 28, 28);
                    gear.interactable = canOpen;
                    if (canOpen) Track(gear, "settings:" + mod.Id);
                    else foreach (var icon in gear.GetComponentsInChildren<OverviewIconGraphic>()) icon.color = OverviewUi.Muted;
                    // 第 5 章：齿轮跳转本身不可用（版本 / 结构不兼容）时说明原因，不再一律写成「没有 ModConfig 设置项」。
                    string gearUnavailable = canOpen ? "" : OverviewSettingsNavigator.Unavailable;
                    Hint(gear.gameObject, canOpen ? Loc.T("Open mod settings", "打开模组设置", "開啟模組設定")
                        : gearUnavailable.Length > 0 ? gearUnavailable : Loc.T("No ModConfig settings.", "没有 ModConfig 设置项。", "沒有 ModConfig 設定項。"));
                }
                y += 34;
                if (collapsed) continue;
                foreach (var entry in modEntries)
                {
                    const float rowIndent = 28; _grid.Add(new List<Selectable>());
                    var item = OverviewUi.Panel("Key_" + entry.Id, _content, ModConfigLook.Surface(new Color(.1f, .11f, .13f, .45f))); OverviewUi.Place(item, rowIndent, y, width - rowIndent, 34);
                    var label = Label(item, entry.Name, xName - rowIndent, 2, xDesc - xName - 6, 30, OverviewUi.Ivory, 16); Hint(label.gameObject, entry.Section + " → " + entry.Key);
                    var desc = TextButton(item, entry.ShortDescription, () => { if (GamepadSupport.IsGamepad) EditDescription(entry); }, xDesc - rowIndent, 2, xCurrent - xDesc - 6, 30, OverviewUi.Muted); Track(desc, entry.Id + ":description");
                    Hint(desc.gameObject, entry.ShortDescription + "\n" + Loc.T("Double-click (controller: A) to write a short description in the current language.", "双击（手柄按 A）可为当前语言写一句短说明。", "雙擊（手把按 A）可為目前語言寫一句短說明。"));
                    desc.gameObject.AddComponent<OverviewHotkeyTextAction>().DoubleClicked = () => EditDescription(entry);
                    bool native = GamepadCombos.NativeGamepad(entry.Config);
                    // Keyboard column: the keyboard key (empty for controller-only settings).
                    var current = KeyButton(item, native ? "" : entry.CurrentLabel, () => Record(entry), xCurrent - rowIndent, 3, keyWidth - 4, OverviewUi.Ivory);
                    KeyChipsOn(current, native ? "" : RawKey(entry));
                    Track(current, entry.Id + ":current");
                    Hint(current.gameObject, (native ? Loc.T("This setting is a controller binding; see the controller column.", "这是手柄按键设置，见手柄列。", "這是手把按鍵設定，見手把欄。") : entry.CurrentLabel)
                        + "\n" + Loc.T("Click to record a new key with keyboard and mouse; it is written at once. Right-click for more.", "点击用键鼠录制新按键，录完立即写入；右键更多操作。", "點擊用鍵鼠錄製新按鍵，錄完立即寫入；右鍵更多操作。"));
                    // Controller column: the mod's own controller key, or the combo standing in for its keyboard key.
                    var combo = GamepadCombos.For(entry.Id);
                    var pad = KeyButton(item, "", () => RecordCombo(entry), xPad - rowIndent, 3, keyWidth - 4, OverviewUi.Ivory);
                    StylePad(pad);
                    if (combo != null) KeyChipsOn(pad, combo);
                    else KeyChipsOn(pad, native ? RawKey(entry) : "");
                    Track(pad, entry.Id + ":pad");
                    Hint(pad.gameObject, ComboHint(entry));
                    pad.gameObject.AddComponent<OverviewHotkeyTextAction>().ContextRequested = () => KeyMenu(entry, (RectTransform)pad.transform, KeyColumn.Pad);
                    current.gameObject.AddComponent<OverviewHotkeyTextAction>().ContextRequested = () => KeyMenu(entry, (RectTransform)current.transform, KeyColumn.Keyboard);

                    bool padConflict = native ? entry.Conflicts.Count > 0 : entry.Conflicts.Any(c => c.Combo), keyConflict = !native && entry.Conflicts.Any(c => !c.Combo);
                    string conflictText = padConflict && keyConflict ? Loc.T("← Duplicate →", "← 重复键 →", "← 重複鍵 →") : padConflict ? Loc.T("← Duplicate", "← 重复键", "← 重複鍵") : Loc.T("Duplicate →", "重复键 →", "重複鍵 →");
                    if (entry.Conflicts.Count > 0)
                    {
                        Button warning = null!;
                        warning = TextButton(item, conflictText, () => OpenConflicts(entry, (RectTransform)warning.transform), xConflict - rowIndent, 2, xModDefault - xConflict - 2, 30, new Color(.95f, .65f, .35f));
                        Track(warning, entry.Id + ":conflict");
                        warning.gameObject.AddComponent<OverviewHotkeyTextAction>().ContextRequested = () => KeyMenu(entry, (RectTransform)warning.transform);
                        Hint(warning.gameObject, string.Join("\n", entry.Conflicts.Select(k => k.IsGame
                            ? Loc.T("Game: ", "游戏：", "遊戲：") + k.Key + " → " + k.Label
                            : OverviewLanguageBridge.GetDisplayName(k.Mod) + " → " + k.Section + " / " + k.Key + " → " + k.Label)));
                    }
                    bool showPad = _padDefaults;
                    string defaultRaw = native ? Convert.ToString(entry.Config.DefaultValue) ?? "" : "";
                    Button? original = null;
                    original = KeyButton(item, showPad ? "" : entry.DefaultLabel, () =>
                    {
                        // 第 5 章（用户 2026-11-15）：手柄态这一格 = 移除录的组合键，先弹小菜单确认，免得 A 误触直接删掉。
                        if (showPad && !native) { if (original != null) ConfirmRemoveCombo(entry, (RectTransform)original.transform); }
                        else WriteNow(entry, () => Service.RestoreDefault(entry, out var error) ? null : error);
                    }, xModDefault - rowIndent, 3, keyWidth - 4, OverviewUi.Green);
                    Track(original, entry.Id + ":default");
                    if (showPad) { StylePad(original); KeyChipsOn(original, native && !OverviewHotkeyAdapter.IsNone(defaultRaw) ? defaultRaw : ""); }
                    else KeyChipsOn(original, RawOf(entry.Config.DefaultValue));
                    Hint(original.gameObject, (showPad && !native
                        ? Loc.T("This mod has no controller default. Click to remove the combo recorded here (asks first).", "该模组没有手柄默认键。点击移除这里录的组合键（会先确认）。", "該模組沒有手把預設鍵。點擊移除這裡錄的組合鍵（會先確認）。")
                        : entry.DefaultLabel + "\n" + Loc.T("Click to restore the mod's own default; it is written at once.", "点击恢复模组自带的默认键，立即写入。", "點擊恢復模組自帶的預設鍵，立即寫入。")));
                    y += 36;
                }
            }
            }
            if (visible.Count == 0) { Label(_content, Loc.T("No matching keybindings", "没有符合条件的快捷键", "沒有符合條件的快捷鍵"), 20, 12, width - 40, 35, OverviewUi.Muted); y = 60; }
            _content.sizeDelta = new Vector2(0, y); SetPixel(pixel);
            if (ownFocus) { if (_targets.TryGetValue(_selectedId, out var target)) EventSystem.current?.SetSelectedGameObject(target); else EventSystem.current?.SetSelectedGameObject(_targets.Values.FirstOrDefault()); }
            LinkToolbarNavigation();
            LinkGrid();
            // Every focusable cell keeps itself in view.
            foreach (var selectable in _content.GetComponentsInChildren<Selectable>(false))
                if (selectable.GetComponent<ScrollFollow>() == null) { var follow = selectable.gameObject.AddComponent<ScrollFollow>(); var rect = (RectTransform)selectable.transform; follow.Selected = () => EnsureVisible(rect); }
        }
        finally { _rendering = false; }
    }

    private bool Matches(OverviewHotkeyEntry e)
    {
        if (_filter == 1 && e.Conflicts.Count == 0 || _filter == 2 && !e.IsUnbound) return false;
        if (string.IsNullOrWhiteSpace(_query)) return true;
        var combo = GamepadCombos.For(e.Id);
        string text = e.Name + "\n" + e.Key + "\n" + e.Section + "\n" + OverviewLanguageBridge.GetDisplayName(e.Mod) + "\n" + e.Mod.RawName + "\n" + e.ShortDescription
            + "\n" + e.CurrentLabel + "\n" + e.DefaultLabel + (combo != null ? "\n" + GamepadCombos.Label(combo) : "");
        return text.IndexOf(_query, StringComparison.OrdinalIgnoreCase) >= 0;
    }
    private string FilterLabel() => _filter == 1 ? Loc.T("Conflicts", "重复", "重複") : _filter == 2 ? Loc.T("Unbound", "未绑定", "未綁定") : Loc.T("All", "全部", "全部");
    private void SetFilter(int filter) { _filter = filter; Render(); }
    private float Measure(string s, float size) => _hintText.GetPreferredValues(s).x * size / Math.Max(1, _hintText.fontSize);
    private TextMeshProUGUI Label(Transform parent, string text, float x, float y, float width, float height, Color? color = null, float size = 16)
    {
        var label = OverviewUi.Text(parent, text, size); label.alignment = TextAlignmentOptions.MidlineLeft; label.color = color ?? OverviewUi.Ivory;
        label.enableAutoSizing = true; label.fontSizeMin = Mathf.Min(size, Mathf.Clamp(height * .4f, 8, 12)); label.fontSizeMax = size; label.overflowMode = TextOverflowModes.Ellipsis;
        OverviewUi.Place(label.rectTransform, x, y, Math.Max(1, width), height); return label;
    }
    private Button TextButton(Transform parent, string text, Action action, float x, float y, float width, float height, Color color)
    {
        var rect = OverviewUi.Rect("TextAction", parent); OverviewUi.Place(rect, x, y, width, height);
        var image = rect.gameObject.AddComponent<Image>(); image.color = Color.clear;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(() => action());
        Label(rect, text, 0, 0, width, height, color); return button;
    }
    private Button KeyButton(Transform parent, string label, Action action, float x, float y, float width, Color color)
    {
        var button = OverviewUi.Button(parent, label, action, Math.Max(20, width), 28, role: UiActionRole.Neutral); OverviewUi.Place((RectTransform)button.transform, x, y, Math.Max(20, width), 28);
        foreach (var text in button.GetComponentsInChildren<TMP_Text>())
        {
            text.color = color; text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
            text.enableAutoSizing = true; text.fontSizeMin = 10; text.fontSizeMax = 15; text.fontSize = 15; text.margin = new Vector4(KeyCaptionInset, 0, KeyCaptionInset, 0);
        }
        return button;
    }
    /// <summary>The ribbon's notches take about 7% of the width at each end; long key names stay inside.</summary>
    private const float KeyCaptionInset = 14f;

    /// <summary>The tool row (search, filter, more, fold-all) is one explicit left/right line and ↓ always goes into
    /// the list; ↑ goes to the page's tab row.</summary>
    private void LinkToolbarNavigation()
    {
        var tools = _tools.GetComponentsInChildren<Selectable>(false).Where(s => s.transform.parent == _tools && s.IsActive()).OrderBy(s => ((RectTransform)s.transform).anchoredPosition.x).ToList();
        Selectable? first = _targets.TryGetValue(_selectedId, out var remembered) && remembered != null && remembered.activeInHierarchy ? remembered.GetComponent<Selectable>() : null;
        first ??= _targets.Values.Select(go => go != null ? go.GetComponent<Selectable>() : null).FirstOrDefault(s => s != null && s.IsActive());
        for (int i = 0; i < tools.Count; i++)
            tools[i].navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = i > 0 ? tools[i - 1] : null, selectOnRight = i + 1 < tools.Count ? tools[i + 1] : null, selectOnDown = first, selectOnUp = ToolbarAbove?.Invoke(tools[i]) };
    }

    /// <summary>
    /// 实测修正（2026-11-16，用户：分组箭头上按右跳到工具行的「全部」；一直按下焦点会消失、回不来）：
    /// 列表格子原来用 Unity 自动导航，会在整个画面里找「那个方向最近的」——同一行没有别的格子时跳到工具行，
    /// 到底再按下会跳到概览页以外（游戏菜单背后还开着的按钮），之后本页的「焦点丢失时补回」也认不出来。
    /// 现在逐行显式连接：左右只在同一行里走（行尾不动）；上下走到相邻一行里离当前栏最近的格子，
    /// 连续上下时记住起始栏位（经过只有箭头的标题行再回到原来那一栏）；第一行再上 = 工具行（离得最近的那个），最后一行再下 = 不动。
    /// </summary>
    private void LinkGrid()
    {
        // 不按 IsActive 过滤：缓存预渲染时页面还没激活（EnterSteps），那时 IsActive 全是 false。
        var rows = _grid.Select(row => row.Where(s => s != null && s.gameObject.activeSelf && s.IsInteractable()).OrderBy(CenterX).ToList()).Where(row => row.Count > 0).ToList();
        var tools = _tools.GetComponentsInChildren<Selectable>(true).Where(s => s.transform.parent == _tools && s.gameObject.activeSelf).ToList();
        for (int r = 0; r < rows.Count; r++)
            for (int i = 0; i < rows[r].Count; i++)
            {
                var cell = rows[r][i]; int rowIndex = r;
                cell.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnLeft = i > 0 ? rows[r][i - 1] : null, selectOnRight = i + 1 < rows[r].Count ? rows[r][i + 1] : null };
                var mover = cell.GetComponent<HotkeyGridMove>(); if (mover == null) mover = cell.gameObject.AddComponent<HotkeyGridMove>();
                mover.Move = direction =>
                {
                    if (direction != MoveDirection.Up && direction != MoveDirection.Down) return false;
                    float x = _stickyX ?? CenterX(cell);
                    int next = rowIndex + (direction == MoveDirection.Down ? 1 : -1);
                    Selectable? target = next < 0 ? tools.Where(t => t != null && t.IsActive()).OrderBy(t => Math.Abs(CenterX(t) - x)).FirstOrDefault()
                        : next < rows.Count ? rows[next].Where(t => t != null).OrderBy(t => Math.Abs(CenterX(t) - x)).FirstOrDefault() : null;
                    if (target == null) return true; // 最后一行：留在原地
                    if (next >= 0) { _stickyX = x; _gridMoving = true; }
                    try { EventSystem.current?.SetSelectedGameObject(target.gameObject); }
                    finally { _gridMoving = false; }
                    return true;
                };
            }
    }
    private static float CenterX(Selectable s) { var rect = (RectTransform)s.transform; return rect.TransformPoint(rect.rect.center).x; }

    /// <summary>Gamepad entry into this tab: the remembered binding, else the first one.</summary>
    public void FocusForGamepad()
    {
        if (!GamepadSupport.IsGamepad) return;
        var target = _targets.TryGetValue(_selectedId, out var remembered) && remembered != null && remembered.activeInHierarchy ? remembered : _targets.Values.FirstOrDefault(go => go != null && go.activeInHierarchy);
        if (target != null) EventSystem.current?.SetSelectedGameObject(target);
    }
    private void Track(Button button, string id)
    {
        _targets[id] = button.gameObject;
        if (_rendering && _grid.Count > 0) _grid[^1].Add(button);
        button.gameObject.AddComponent<OverviewHotkeyFocus>().Selected = () => { _selectedId = id; if (!_gridMoving) _stickyX = null; EnsureVisible((RectTransform)button.transform); };
        GamepadSupport.MarkSelectable(button.gameObject, null, null);
    }
    private void EnsureVisible(RectTransform rect)
    {
        if (!_opened || _rendering) return;
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        float top = -_content.InverseTransformPoint(corners[1]).y, bottom = -_content.InverseTransformPoint(corners[0]).y, pixel = _content.anchoredPosition.y;
        if (top < pixel) SetPixel(top); else if (bottom > pixel + _viewport.rect.height) SetPixel(bottom - _viewport.rect.height);
    }
    private void Hint(GameObject target, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var anchor = (RectTransform)target.transform;
        var hover = target.AddComponent<OverviewHover>(); hover.Enter = () => ShowHint(text, anchor, true); hover.Exit = () => _hint.gameObject.SetActive(false);
        var focus = target.AddComponent<OverviewHotkeyFocus>(); focus.Selected = () => ShowHint(text, anchor, false); focus.Deselected = () => _hint.gameObject.SetActive(false);
        if (target.GetComponent<Graphic>() != null) target.GetComponent<Graphic>().raycastTarget = true;
    }
    private void ShowHint(string text, RectTransform anchor, bool followsPointer)
    {
        if (!_opened) return;
        _hintText.richText = text.Contains("<color=#"); _hintText.text = text; _hintAnchor = anchor; _hintFollowsPointer = followsPointer;
        PositionHint(); _hint.gameObject.SetActive(true); _hint.SetAsLastSibling();
    }
    private void PositionHint()
    {
        float width = Math.Min(650, Math.Max(180, _root.rect.width - 30));
        float height = Math.Min(_root.rect.height * .45f, Math.Max(50, _hintText.GetPreferredValues(_hintText.text, width - 20, 0).y + 20));
        Vector2 point;
        if (_hintFollowsPointer)
        {
            var canvas = _root.GetComponentInParent<Canvas>()?.rootCanvas; var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, mouse, camera, out point)) return;
        }
        else { if (_hintAnchor == null) return; var corners = new Vector3[4]; _hintAnchor.GetWorldCorners(corners); point = _root.InverseTransformPoint(corners[0]); }
        float x = point.x - _root.rect.xMin + 16, y = _root.rect.yMax - point.y + 18;
        if (y + height > _root.rect.height - 5) y = _root.rect.yMax - point.y - height - 12;
        OverviewUi.Place(_hint, Mathf.Clamp(x, 5, Math.Max(5, _root.rect.width - width - 5)), Mathf.Clamp(y, 5, Math.Max(5, _root.rect.height - height - 5)), width, height);
    }

    /// <summary>Recording, restoring and clearing write the mod's setting at once (no draft step).</summary>
    private void WriteNow(OverviewHotkeyEntry entry, Func<string?> operation, string suffix = "")
    {
        string? error = operation();
        if (!string.IsNullOrEmpty(error)) { _status(error + suffix); return; }
        _status(Loc.F("Saved: {0} / {1} = {2}.", "已保存：{0} / {1} = {2}。", "已儲存：{0} / {1} = {2}。", OverviewLanguageBridge.GetDisplayName(entry.Mod), entry.Name, entry.CurrentLabel) + suffix);
        Render();
    }

    private bool _comboRecording, _padDefaults;
    private OverviewHotkeyEntry? _comboEntry;

    /// <summary>Draws key chips on a key button (keyboard label or controller path).</summary>
    private void KeyChipsOn(Button button, string label)
    {
        var text = button.GetComponentInChildren<TMP_Text>(true); if (text == null) return;
        var host = (RectTransform)text.transform.parent; text.text = "";
        var keys = new List<(string, bool)>();
        foreach (var part in label.Split('+').Select(x => x.Trim()).Where(x => x.Length > 0)) keys.Add(KeyChips.Describe(part));
        var chipHost = OverviewUi.Rect("KeyChips", host); OverviewUi.Fill(chipHost);
        // An empty cell is a muted "Not set" chip; the cell keeps the click target and the focus ring.
        if (keys.Count == 0) KeyChips.Build(chipHost, new List<(string, bool)> { (Loc.T("Not set", "未设置", "未設定"), false) }, text.font, 24, centre: true, fontSize: 14, muted: true);
        else KeyChips.Build(chipHost, keys, text.font, 24, dimFirst: false, centre: true, fontSize: 15);
        Frameless(button);
    }
    private void KeyChipsOn(Button button, GamepadCombos.Item combo)
    {
        var text = button.GetComponentInChildren<TMP_Text>(true); if (text == null) return;
        var host = (RectTransform)text.transform.parent; text.text = "";
        var keys = GamepadCombos.ButtonsOf(combo).Select(b => (GamepadCombos.ButtonName(b), true)).ToList();
        var chipHost = OverviewUi.Rect("KeyChips", host); OverviewUi.Fill(chipHost);
        KeyChips.Build(chipHost, keys, text.font, 24, dimFirst: combo.held.Count > 0, centre: true, fontSize: 15);
        Frameless(button);
    }
    /// <summary>A cell showing key chips drops the ribbon frame behind them; the cell stays clickable and focusable.</summary>
    private static void Frameless(Button button)
    {
        var style = button.GetComponent<UiActionStyle>(); if (style != null) { style.enabled = false; UnityEngine.Object.Destroy(style); }
        foreach (var image in button.GetComponentsInChildren<Image>(true)) if (image.gameObject.name != "MCE_KeyHit") image.enabled = false;
        if (button.transform.Find("MCE_KeyHit") == null)
        {
            var hit = OverviewUi.Rect("MCE_KeyHit", button.transform); OverviewUi.Fill(hit); hit.SetAsFirstSibling();
            var img = hit.gameObject.AddComponent<Image>(); img.color = Color.clear; img.raycastTarget = true; button.targetGraphic = img;
        }
        button.transition = Selectable.Transition.None;
    }
    /// <summary>The stored value as text ("&lt;Gamepad&gt;/buttonSouth", "LeftControl + F5", "South"), for key chips.</summary>
    private static string RawKey(OverviewHotkeyEntry entry) => RawOf(entry.Config.BoxedValue);
    private static string RawOf(object? value)
    {
        if (value is BepInEx.Configuration.KeyboardShortcut shortcut) return shortcut.MainKey == KeyCode.None ? "" : string.Join(" + ", shortcut.Modifiers.Select(m => m.ToString()).Append(shortcut.MainKey.ToString()));
        string text = Convert.ToString(value) ?? "";
        return OverviewHotkeyAdapter.IsNone(text) ? "" : text;
    }
    /// <summary>Controller-only colour: a teal tint, distinct from the neutral key buttons and the action roles.</summary>
    internal static readonly Color PadAccent = new(.55f, .82f, .86f, 1f);
    private static readonly Color PadFace = new(.13f, .27f, .33f, 1f);
    private static void StylePad(Button button)
    {
        var peak = button.GetComponent<PEAKLib.UI.Elements.PeakMenuButton>();
        if (peak != null && peak.Panel != null) peak.Panel.color = PadFace;
        foreach (var text in button.GetComponentsInChildren<TMP_Text>(true)) text.color = PadAccent;
        foreach (var icon in button.GetComponentsInChildren<OverviewIconGraphic>(true)) icon.color = PadAccent;
        var style = button.GetComponent<UiActionStyle>(); if (style != null) UnityEngine.Object.Destroy(style); // keep our colour, not the role colour
    }

    private void ToggleMod(string id)
    {
        if (!State.collapsedHotkeyMods.Remove(id)) State.collapsedHotkeyMods.Add(id);
        SaveState(); _selectedId = "mod:" + id; Render();
        if (_targets.TryGetValue(_selectedId, out var header)) EventSystem.current?.SetSelectedGameObject(header);
    }
    /// <summary>Expand everything if anything is folded, otherwise fold everything (groups and mod rows; 第 8a 章加回分组，同 IGMM).</summary>
    private void ToggleAllFolds(bool? expand = null)
    {
        var repo = Groups();
        var groups = repo?.State.groups.Select(g => g.id).ToList() ?? new List<string>();
        var mods = Service.Entries.Select(e => e.Mod.Id).Distinct().ToList();
        bool open = expand ?? (mods.Any(State.collapsedHotkeyMods.Contains) || repo != null && groups.Any(repo.State.collapsedGroups.Contains));
        State.collapsedHotkeyMods.Clear();
        if (!open) State.collapsedHotkeyMods.AddRange(mods);
        SaveState();
        if (repo != null && groups.Count > 0)
        {
            repo.State.collapsedGroups.Clear();
            if (!open) repo.State.collapsedGroups.AddRange(groups);
            SaveGroups(repo);
        }
        Render();
        _status(groups.Count > 0
            ? open ? Loc.T("All groups expanded.", "已展开全部分组。", "已展開全部分組。") : Loc.T("All groups collapsed.", "已收起全部分组。", "已收起全部分組。")
            : open ? Loc.T("All mods expanded.", "已展开全部模组。", "已展開全部模組。") : Loc.T("All mods collapsed.", "已收起全部模组。", "已收起全部模組。"));
    }
    private void SaveState()
    {
        try { HotkeyPageState.Save(); }
        catch (Exception ex) { Plugin.Log.LogWarning("[快捷键] 保存 hotkeys.json 失败：" + ex.Message); }
    }

    /// <summary>
    /// 手柄手势（上线前修正，用户 2026-11-16 两页统一）：↑↓ 走行（导航本身）；LB / RB 归外壳（工具行 / RT + LB / RB 切页签，
    /// OverviewPage.Gamepad.cs），本页不再读。按住 RT：←→ 翻页，L3 全部展开 / 收起（只留这一种按法）；不按 RT 时 L3 收起 / 展开
    /// 焦点所在的模组或分组。去掉了与 LB / RB、RT + ↑↓ 重复的「跳模组」和 LT + LB / RB 的全部展开 / 收起。
    /// </summary>
    private void ListGestures()
    {
        var pad = Gamepad.current; if (pad == null) return;
        if (_gestureFrame == Time.frameCount) return; _gestureFrame = Time.frameCount;
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (GamepadIntent.Layer)
        {
            switch (GamepadIntent.PressedDirection())
            {
                case MoveDirection.Left: PageBy(-1); break;
                case MoveDirection.Right: PageBy(1); break;
            }
            if (pad.leftStickButton.wasPressedThisFrame) ToggleAllFoldsGuarded();
            return;
        }
        if (pad.leftStickButton.wasPressedThisFrame && selected != null)
        {
            // 第 8a 章：焦点在分组标题上时 L3 折叠该分组；其余同前（折叠焦点所在的模组）。
            string key = _targets.FirstOrDefault(p => p.Value == selected).Key ?? "";
            if (key.StartsWith("group:", StringComparison.Ordinal)) { ToggleGroup(key.Substring(6)); return; }
            string id = ModOf(selected); if (id.Length > 0) ToggleMod(id);
        }
    }
    /// <summary>The mod id of a focused control (its mod header, or the mod its row belongs to).</summary>
    private string ModOf(GameObject selected)
    {
        var pair = _targets.FirstOrDefault(p => p.Value == selected); string key = pair.Key ?? "";
        if (key.StartsWith("mod:", StringComparison.Ordinal)) return key.Substring(4);
        if (key.StartsWith("settings:", StringComparison.Ordinal)) return key.Substring(9);
        int colon = key.IndexOf(':'); if (colon <= 0) return "";
        string entryId = key.Substring(0, colon);
        return Service.Entries.FirstOrDefault(e => e.Id == entryId)?.Mod.Id ?? "";
    }
    private int _gestureFrame = -1;
    private float _lastFoldAllAt = -1;
    private bool _foldingAll;
    /// <summary>
    /// 上线前修正（用户 2026-11-16 回报「RT + L3 按一下游戏卡死」）：全部展开 / 收起的防重入与防连发。
    /// 同一次按键只做一次（0.3 秒内不再做；正在做时不再进）；展开全部要一次建出所有快捷键行，模组多时这一帧很重，
    /// 超过 0.5 秒就在日志里记下耗时与行数，供下次实测判断是不是这里卡住。
    /// </summary>
    private void ToggleAllFoldsGuarded()
    {
        if (_foldingAll || _rendering || !OverviewPadSteps.Cooled(Time.unscaledTime, _lastFoldAllAt, .3f)) return;
        _foldingAll = true; _lastFoldAllAt = Time.unscaledTime;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try { ToggleAllFolds(); }
        finally
        {
            _foldingAll = false;
            if (watch.ElapsedMilliseconds > 500)
                Plugin.Log.LogWarning("[快捷键] 全部展开 / 收起用了 " + watch.ElapsedMilliseconds + " ms（" + _targets.Count + " 个可选格）。");
        }
    }
    private void PageBy(int step)
    {
        float view = _viewport.rect.height; SetPixel(_content.anchoredPosition.y + step * view * .9f);
        var corners = new Vector3[4]; _viewport.GetWorldCorners(corners); float top = corners[1].y, bottom = corners[0].y;
        var first = _targets.Values.Where(g => g != null && g.activeInHierarchy && g.transform.position.y < top && g.transform.position.y > bottom).OrderByDescending(g => g.transform.position.y).FirstOrDefault();
        if (first != null) EventSystem.current?.SetSelectedGameObject(first);
    }

    private string ComboHint(OverviewHotkeyEntry entry)
    {
        var combo = GamepadCombos.For(entry.Id);
        string head = combo != null
            ? Loc.F("Controller hotkey: {0}", "已分配的手柄快捷键：{0}", "已分配的手把快捷鍵：{0}", GamepadCombos.Label(combo))
            : GamepadCombos.NativeGamepad(entry.Config)
                ? Loc.T("This setting is the mod's own controller key.", "这是模组自己的手柄按键设置。", "這是模組自己的手把按鍵設定。")
                : Loc.T("No controller hotkey yet.", "尚未分配手柄快捷键。", "尚未分配手把快捷鍵。");
        return head + "\n\n"
            + Loc.T("Press A to record. Hold any buttons first, then press the last one, to record a combo (up to 4 buttons).",
                "按 A 开始录制。先按住任意几个键、再按最后一个键，就录成组合键（最多 4 个键）。",
                "按 A 開始錄製。先按住任意幾個鍵、再按最後一個鍵，就錄成組合鍵（最多 4 個鍵）。") + "\n"
            + Loc.T("* The buttons held first still reach the game; only the last button is hidden from it. Steam Input's action sets or chords can do this without the mod.",
                "* 先按住的键游戏仍会读到，只有最后一个键对游戏屏蔽。也可以改用 Steam 输入自带的动作集或组合键。",
                "* 先按住的鍵遊戲仍會讀到，只有最後一個鍵對遊戲屏蔽。也可以改用 Steam 輸入內建的動作集或組合鍵。");
    }

    /// <summary>A on the controller cell: record a button or a combo (hold B alone for 1 s to finish).</summary>
    private void RecordCombo(OverviewHotkeyEntry entry)
    {
        if (!GamepadSupport.IsGamepad) { _status(Loc.T("Controller keys are set with a controller.", "手柄快捷键需使用手柄设置。", "手把快捷鍵需使用手把設定。")); return; }
        ClosePopup(); _comboRecording = true; _comboEntry = entry; _bDownAt = -1; _take.Clear();
        GamepadIntent.ReleaseAll(); var system = EventSystem.current; _navigationBefore = system == null || system.sendNavigationEvents; if (system != null) system.sendNavigationEvents = false;
        GamepadLock.Begin(_root.gameObject); ComboRecordingActive = true; // the A that started recording is already down: it never joins the take
        _status(Loc.T("Recording (controller locked): press a button, or hold buttons first and then press the last one for a combo. Hold B for 1 second to exit.",
            "录制中（手柄已锁定）：按下一个键；或先按住若干键再按最后一个键，录成组合键。长按 B 1 秒退出。",
            "錄製中（手把已鎖定）：按下一個鍵；或先按住若干鍵再按最後一個鍵，錄成組合鍵。長按 B 1 秒退出。"));
        HoldRing.Show(_root, 0f);
    }
    private void TickComboRecording()
    {
        var pad = Gamepad.current; var entry = _comboEntry;
        if (entry == null) return;
        // Keyboard Esc still cancels (the controller is locked and cannot reach the page).
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { OverviewHotkeyCapture.MarkConsumed(); EndComboRecording(); _status(Loc.T("Recording cancelled.", "已取消录制。", "已取消錄製。")); return; }
        if (pad == null || !GamepadSupport.IsGamepad) { EndComboRecording(); _status(Loc.T("Recording stopped: the controller is no longer the input device.", "录制已停止：当前输入设备已不是手柄。", "錄製已停止：目前輸入裝置已不是手把。")); return; }
        if (OverviewUi.HasModal) { EndComboRecording(); _status(Loc.T("Recording stopped: a dialog opened.", "录制已停止：有对话框弹出。", "錄製已停止：有對話框彈出。")); return; }
        bool native = GamepadCombos.NativeGamepad(entry.Config);
        // While recording, GamepadLock hides every controller event from the game, other mods and this page; only the raw
        // buttons are read here. Every button that goes down joins this take; when all are up again it is judged whole.
        _pressBuffer.Clear(); GamepadLock.DrainPresses(_pressBuffer);
        foreach (var name in _pressBuffer)
            if (!_take.Contains(name)) { _take.Add(name); if (name == "buttonEast" && _take.Count == 1) _bDownAt = Time.unscaledTime; }
        bool anyHeld = false;
        foreach (var name in _take) if (GamepadLock.Pressed(name)) anyHeld = true;
        // Hold B alone for 1 s = leave the lock (the ring shows it). B together with any other button never cancels.
        bool bAlone = _take.Count == 1 && _take[0] == "buttonEast" && GamepadLock.Pressed("buttonEast") && _bDownAt >= 0;
        HoldRing.Show(_root, bAlone ? Mathf.Clamp01((Time.unscaledTime - _bDownAt) / 1f) : -1f);
        if (bAlone && Time.unscaledTime - _bDownAt >= 1f) { _take.Clear(); _bDownAt = -1; EndComboRecording(); _status(Loc.T("Recording finished.", "录制已结束。", "錄製已結束。")); return; }
        if (_take.Count == 0 || anyHeld) return;
        var take = _take.Where(GamepadCombos.Buttons.Contains).ToList(); _take.Clear(); _bDownAt = -1;
        if (take.Count == 0) return;
        if (take.Count > GamepadCombos.MaxButtons)
        {
            _status(Loc.F("A combo has at most {0} buttons. Not recorded; recording continues. Hold B for 1 second to exit.", "组合键最多 {0} 个键，本次未录入，录制继续。长按 B 1 秒退出。", "組合鍵最多 {0} 個鍵，本次未錄入，錄製繼續。長按 B 1 秒退出。", GamepadCombos.MaxButtons));
            return;
        }
        // Press order: everything pressed before the last button is "held first".
        string button = take[take.Count - 1];
        var held = take.Take(take.Count - 1).ToList();
        if (native && held.Count == 0)
        {
            // A single button for a controller-native setting goes into the mod's own setting (written at once).
            object? value = GamepadCombos.NativeValue(entry.Config, button);
            if (value == null) { _status(Loc.T("This mod does not accept that button. Recording continues; hold B for 1 second to finish.", "该模组不接受这个键。录制继续，长按 B 1 秒结束。", "該模組不接受這個鍵。錄製繼續，長按 B 1 秒結束。")); return; }
            GamepadCombos.Clear(entry.Id);
            WriteNow(entry, () => Service.TrySet(entry, value, out var error) ? null : error, StillRecording); return;
        }
        string? moved = GamepadCombos.Set(entry.Id, entry.Guid, entry.Section, entry.Key, held, button);
        Service.RefreshConflicts();
        var other = moved == null ? null : Service.Entries.FirstOrDefault(e => e.Id == moved);
        var item = GamepadCombos.For(entry.Id);
        _status(Loc.F("{0} now presses this hotkey.", "{0} 现在会按下这个快捷键。", "{0} 現在會按下這個快捷鍵。", item != null ? GamepadCombos.Label(item) : GamepadCombos.ButtonName(button))
            + (other != null ? " " + Loc.F("(moved from {0})", "（原来分给了 {0}）", "（原來分給了 {0}）", OverviewLanguageBridge.GetDisplayName(other.Mod) + " / " + other.Name) : "")
            // 第 5 章（二次审查）：这个快捷键现在没有键盘键时，组合键按了也不会注入任何键，先说清楚。
            + (entry.IsUnbound ? " " + Loc.T("This keybinding has no keyboard key yet, so the combo does nothing until you set one.", "这个快捷键目前没有键盘键，设好键盘键之前组合键不会生效。", "這個快捷鍵目前沒有鍵盤鍵，設好鍵盤鍵之前組合鍵不會生效。") : "")
            + ComboSaveNote() + StillRecording);
        Render();
    }
    /// <summary>第 5 章（二次审查）：gamepad-combos.json 没写成功时补一句（本局已生效，下次启动会丢）。</summary>
    private static string ComboSaveNote() => GamepadCombos.LastSaveFailed
        ? " " + Loc.T("(Could not save gamepad-combos.json; this change is lost after a restart.)", "（未能保存到 gamepad-combos.json，重启后会丢失。）", "（未能儲存到 gamepad-combos.json，重新啟動後會遺失。）")
        : "";
    private float _bDownAt = -1;
    /// <summary>Appended to every message while still recording.</summary>
    private static string StillRecording => " " + Loc.T("Still recording: press another button to replace it, hold B for 1 second to finish.", "仍在录制：再按其他键可覆盖，长按 B 1 秒结束。", "仍在錄製：再按其他鍵可覆蓋，長按 B 1 秒結束。");
    /// <summary>Buttons pressed in the current take (in press order); judged when all are released.</summary>
    private readonly List<string> _take = new(), _pressBuffer = new();
    /// <summary>Any controller recording in progress (read by row actions such as Y = key menu).</summary>
    internal static bool ComboRecordingActive;
    private void EndComboRecording()
    {
        _comboRecording = false; _comboEntry = null; _take.Clear(); GamepadLock.End(); ComboRecordingActive = false; HoldRing.Hide(_root);
        var system = EventSystem.current; if (system != null) system.sendNavigationEvents = _navigationBefore;
    }
    private bool _navigationBefore = true;
    private void Record(OverviewHotkeyEntry entry)
    {
        if (GamepadSupport.IsGamepad) { _status(Loc.T("Keyboard keys are set with keyboard and mouse.", "键盘快捷键需使用键鼠设置。", "鍵盤快捷鍵需使用鍵鼠設定。")); return; }
        ClosePopup();
        _capture.Begin(entry, value => WriteNow(entry, () => Service.TrySet(entry, value, out var error) ? null : error), () => _status(Loc.T("Recording cancelled.", "已取消按键录制。", "已取消按鍵錄製。")));
        _status(Loc.T("Press a key or shortcut; Escape cancels.", "请按下按键或组合键，ESC 取消。", "請按下按鍵或組合鍵，ESC 取消。"));
    }
    /// <summary>第 5 章（二次审查）：对话框关掉后焦点停在已销毁的按钮上，手柄要多按一下才回来；关掉时直接放回原格。</summary>
    private bool _refocusAfterDialog;
    private void EditDescription(OverviewHotkeyEntry entry)
    {
        _refocusAfterDialog = true;
        OverviewUi.AskValidated(Loc.T("Keybinding description", "快捷键短说明", "快捷鍵短說明"),
            new[] { (Loc.T("Description (blank shows the setting's own description)", "短说明（留空显示配置项说明）", "短說明（留空顯示設定項說明）"), entry.CustomDescription) }, null,
            values => { if (!Service.SetDescription(entry, values[0], out var error)) throw new InvalidOperationException(error); Render(); });
    }
    private void OpenConflicts(OverviewHotkeyEntry entry, RectTransform anchor)
    {
        var conflicts = entry.Conflicts.ToList();
        if (conflicts.Count == 1 && !conflicts[0].IsGame) { JumpTo(conflicts[0].EntryId); return; }
        Popup(anchor, conflicts.Select(c => c.IsGame
            ? (Loc.T("Game: ", "游戏：", "遊戲：") + c.Key + " · " + c.Label, (Action)(() => _status(Loc.T("This key is also a game control; pressing it triggers both.", "这个键也是游戏自己的操作，按下时两边都会触发。", "這個鍵也是遊戲自己的操作，按下時兩邊都會觸發。"))), true)
            : (OverviewLanguageBridge.GetDisplayName(c.Mod) + " / " + c.Section + " / " + OverviewLanguageBridge.Translate(c.Key) + " · " + c.Label, (Action)(() => JumpTo(c.EntryId)), true)).ToList());
    }
    private void JumpTo(string entryId)
    {
        var entry = Service.Entries.FirstOrDefault(e => e.Id == entryId);
        if (entry == null) { _status(Loc.T("That binding is no longer available. Refresh the page.", "目标快捷键已不可用，请刷新页面。", "目標快捷鍵已不可用，請重新整理頁面。")); return; }
        _query = ""; _search.SetTextWithoutNotify(""); _filter = 0;
        State.collapsedHotkeyMods.Remove(entry.Mod.Id);
        // 第 8a 章（同 IGMM）：目标所在分组及其上级都展开
        var repo = Groups();
        if (repo != null)
        {
            string groupId = repo.GroupOf(entry.Mod.Id);
            bool changed = false;
            foreach (var level in new GroupTree(repo.State.groups).Chain(groupId)) changed |= repo.State.collapsedGroups.Remove(level.id);
            if (changed) SaveGroups(repo);
        }
        _selectedId = entryId + ":current"; SaveState(); Render(); Canvas.ForceUpdateCanvases();
        if (_targets.TryGetValue(_selectedId, out var target))
        {
            EnsureVisible((RectTransform)target.transform); EventSystem.current?.SetSelectedGameObject(target);
            var row = target.transform.parent; var image = row != null ? row.GetComponent<Image>() : null;
            if (image != null) { var flash = image.gameObject.GetComponent<OverviewHotkeyJumpFlash>() ?? image.gameObject.AddComponent<OverviewHotkeyJumpFlash>(); flash.Begin(image); }
        }
        _status(Loc.T("Jumped to: ", "已跳转到：", "已跳轉到：") + OverviewLanguageBridge.GetDisplayName(entry.Mod) + " → " + entry.Name);
    }
    /// <summary>右键 / Y 菜单是从哪一栏打开的：只改那一栏（实测修正 2026-11-16）。</summary>
    private enum KeyColumn { Keyboard, Pad, Any }
    /// <summary>
    /// 实测修正（2026-11-16，用户：在手柄栏点「重设 / 清空」改的却是键盘键）：菜单按打开它的那一栏动作——
    /// 键盘栏只改键盘键（不再带「移除手柄组合键」）；手柄栏：模组自己的手柄键设置（native）改它本身，
    /// 只认键盘的模组则只动本模组录的手柄组合键（模组没有手柄默认，重设 = 清空组合键，所以只给一项「清空」）。
    /// 冲突栏两边都可能涉及，保持原来的全部选项。
    /// </summary>
    private void KeyMenu(OverviewHotkeyEntry entry, RectTransform anchor, KeyColumn column = KeyColumn.Any)
    {
        bool native = GamepadCombos.NativeGamepad(entry.Config);
        bool hasCombo = GamepadCombos.For(entry.Id) != null;
        void RemoveCombo() { GamepadCombos.Clear(entry.Id); Service.RefreshConflicts(); _status(Loc.T("Controller combo removed.", "已移除手柄组合键。", "已移除手把組合鍵。") + ComboSaveNote()); Render(); }
        var items = new List<(string Label, Action Action, bool Enabled)>();
        if (column == KeyColumn.Pad && !native)
        {
            items.Add((Loc.T("Clear this controller combo", "清空这个手柄组合键", "清空這個手把組合鍵"), RemoveCombo, hasCombo));
            items.Add((Loc.T("Edit short description", "编辑短说明", "編輯短說明"), () => EditDescription(entry), true));
            Popup(anchor, items, new[] { UiActionRole.Danger });
            return;
        }
        // 键盘栏遇到手柄键设置（native）：那一格是空的，不能借它改手柄键。
        bool editsEntry = !(column == KeyColumn.Keyboard && native);
        // Every item writes at once like recording does (the keybindings page has no draft step).
        items.Add((Loc.T("Reset to the mod's default key", "重设为模组默认的快捷键", "重設為模組預設的快捷鍵"), () => WriteNow(entry, () => Service.RestoreDefault(entry, out var error) ? null : error), editsEntry));
        items.Add((Loc.T("Clear this key", "清空这个按键", "清空這個按鍵"), () => WriteNow(entry, () => Service.Clear(entry, out var error) ? null : error), editsEntry && entry.CanClear && !entry.IsUnbound));
        foreach (var conflict in entry.Conflicts.Where(k => !k.IsGame && !k.Combo).ToList())
        {
            var c = conflict;
            items.Add((Loc.T("Ignore conflict ←→ ", "忽略冲突 ←→ ", "忽略衝突 ←→ ") + OverviewLanguageBridge.GetDisplayName(c.Mod) + " / " + c.Key, () =>
            {
                if (Service.IgnoreConflict(entry, c.EntryId, out var error)) { _status(Loc.T("Conflict ignored.", "已忽略这条冲突。", "已忽略這條衝突。")); Render(); }
                else _status(error);
            }, true));
        }
        if (entry.IgnoredCount > 0) items.Add((Loc.T("Restore ignored warnings", "恢复已忽略的冲突提醒", "恢復已忽略的衝突提醒"), () => { if (Service.RestoreIgnoredFor(entry, out var error)) Render(); else _status(error); }, true));
        if (column == KeyColumn.Any && hasCombo) items.Add((Loc.T("Remove controller combo", "移除手柄组合键", "移除手把組合鍵"), RemoveCombo, true));
        items.Add((Loc.T("Edit short description", "编辑短说明", "編輯短說明"), () => EditDescription(entry), true));
        Popup(anchor, items, new[] { UiActionRole.Commit, UiActionRole.Danger });
    }
    private void Popup(RectTransform anchor, IReadOnlyList<(string Label, Action Action, bool Enabled)> items, UiActionRole[]? roles = null)
    {
        if (_capture.IsCapturing || _comboRecording) return; // no menu while recording
        ClosePopup();
        var entries = items.Select((item, index) => new UiMenuEntry { Label = item.Label, Enabled = item.Enabled,
            Role = roles != null && index < roles.Length ? roles[index] : UiActionRole.Edit, Invoke = item.Action }).ToArray();
        OverviewUi.CompactDropdown(anchor, "", entries, _root);
    }
    /// <summary>
    /// 第 5 章（用户 2026-11-15：要二次确认的小弹框）：贴着「模组默认」格弹出「移除 / 取消」两项，焦点落在「取消」，
    /// B 或点空白关闭 = 不删。没有录组合键时只提示，不弹。
    /// </summary>
    private void ConfirmRemoveCombo(OverviewHotkeyEntry entry, RectTransform anchor)
    {
        if (_capture.IsCapturing || _comboRecording) return;
        var combo = GamepadCombos.For(entry.Id);
        if (combo == null) { _status(Loc.T("No controller combo is recorded for this keybinding.", "这个快捷键没有录手柄组合键。", "這個快捷鍵沒有錄手把組合鍵。")); return; }
        ClosePopup();
        string title = Loc.F("Remove controller combo {0}?", "移除手柄组合键 {0}？", "移除手把組合鍵 {0}？", GamepadCombos.Label(combo));
        OverviewUi.CompactDropdown(anchor, title, new[]
        {
            // 放第一项：紧凑菜单没有勾选项时焦点落在第一项，A 连按也只是取消。
            new UiMenuEntry { Label = Loc.T("Cancel", "取消", "取消"), Role = UiActionRole.Neutral, Invoke = () => { } },
            new UiMenuEntry { Label = Loc.T("Remove", "移除", "移除"), Role = UiActionRole.Danger, Invoke = () =>
            {
                GamepadCombos.Clear(entry.Id); Service.RefreshConflicts();
                _status(Loc.T("Controller combo removed.", "已移除手柄组合键。", "已移除手把組合鍵。") + ComboSaveNote()); Render();
            } },
        }, _root);
    }
    private void ClosePopup() { _root.GetComponent<WindowPopover>()?.Close(); }
    public void Dispose()
    {
        if (_disposed) return;
        OnClosed(); _disposed = true; Service.Changed -= OnChanged; Service.Dispose(); _pump.Tick = null; UnityEngine.Object.Destroy(_root.gameObject);
    }
}

/// <summary>Keeps a focused cell scrolled into view (separate from OverviewHotkeyFocus, whose handlers get replaced).</summary>
/// <summary>快捷键页列表格子的上下导航（见 OverviewHotkeyView.LinkGrid）；左右仍走显式 Navigation。</summary>
internal sealed class HotkeyGridMove : MonoBehaviour, IMoveHandler
{
    internal Func<MoveDirection, bool>? Move;
    public void OnMove(AxisEventData data) { if (Move != null && Move(data.moveDir)) data.Use(); }
}
internal sealed class ScrollFollow : MonoBehaviour, ISelectHandler { internal Action? Selected; public void OnSelect(BaseEventData data) => Selected?.Invoke(); }
internal sealed class OverviewHotkeyFocus : MonoBehaviour, ISelectHandler, IDeselectHandler
{ internal Action? Selected, Deselected; public void OnSelect(BaseEventData data) => Selected?.Invoke(); public void OnDeselect(BaseEventData data) => Deselected?.Invoke(); }
internal sealed class OverviewHotkeyJumpFlash : MonoBehaviour
{
    private Image? _image; private Color _original; private float _until;
    internal void Begin(Image image) { if (_image == null) _original = image.color; _image = image; _until = Time.unscaledTime + 1.2f; image.color = ModConfigLook.Surface(new Color(.15f, .31f, .38f, .85f)); }
    private void Update() { if (_image == null || Time.unscaledTime < _until) return; _image.color = _original; UnityEngine.Object.Destroy(this); }
}
/// <summary>Double-click / right-click on a cell; Y on a focused cell opens its menu (controller).</summary>
internal sealed class OverviewHotkeyTextAction : MonoBehaviour, IPointerClickHandler
{
    internal Action? DoubleClicked, ContextRequested;
    public void OnPointerClick(PointerEventData data) { if (data.button == PointerEventData.InputButton.Right) ContextRequested?.Invoke(); else if (data.clickCount >= 2) DoubleClicked?.Invoke(); }
    private void Update() { if (OverviewHotkeyView.ComboRecordingActive || GamepadLock.Active) return; if (EventSystem.current?.currentSelectedGameObject == gameObject && Gamepad.current?.buttonNorth.wasPressedThisFrame == true) ContextRequested?.Invoke(); }
}
