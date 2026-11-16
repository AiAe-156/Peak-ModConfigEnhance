#nullable enable
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
namespace ModConfigEnhance.Overview;

/// <summary>
/// 1.1.0（MCE，快捷键页搬迁第 4 章）：照抄 IGMM Overview/OverviewPage.Gamepad.cs 后裁剪。
/// 留下：View 显示 / 隐藏按键提示条（只在手柄态显示，内容随焦点与 RT 层变化）、弹窗开着时右摇杆滚弹窗、切页签后焦点进内容。
/// 裁掉：LB / RB 切页签（TabStep / StepPageTab；用户 2026-11-15：LB / RB 在快捷键页 = 上 / 下一个模组，页签用十字键上到页签行选）、
/// 管理中心 / 更新队列的切页、介绍页签。
/// 提示文字只写各页签实际可用的键（快捷键页见 <see cref="OverviewHotkeyView"/> 的 ListGestures / TickComboRecording；
/// 模组列表见 OverviewListView.Gamepad）。概览页第 7 章：加「模组列表」页签的焦点与提示，提示按页签切换。
/// 上线前修正（用户 2026-11-16，两页统一）：↑↓ 选模组（各页自己的导航）；LB / RB 在第二行工具行的按钮间跳；
/// RT + LB / RB 在「模组列表 → 快捷键设置 → 模组设置」三个页签按钮间移焦点（实测修正：不直接切页，按 A 才切换）；两件都在本文件统一处理，
/// 两个页签不再各自读 LB / RB。其余：RT + ←→ 翻页、RT + L3 全部展开 / 收起（各页自己处理）；LT + LB / RB 全部展开 / 收起已去掉。
/// </summary>
internal sealed partial class OverviewPage
{
    private RectTransform? _promptBar;
    private TextMeshProUGUI? _promptText;
    private string _promptKey = "";
    private static bool _promptsHidden;

    private void GamepadPageTick()
    {
        bool gamepad = GamepadSupport.IsGamepad && !TypingGuard.Active;
        UpdatePromptBar(gamepad);
        DriveLayerScroll(gamepad);
        if (!gamepad) { ReleaseMoveRepeat(); return; }
        var pad = Gamepad.current; if (pad == null) { ReleaseMoveRepeat(); return; }
        ShoulderTick(pad);
        MoveRepeatTick(pad);
        if (GamepadIntent.Layer) return;
        if (pad.selectButton.wasPressedThisFrame && !pad.rightTrigger.isPressed && !IsRecording) { _promptsHidden = !_promptsHidden; _promptKey = ""; }
    }

    private int _shoulderFrame = -1;
    /// <summary>
    /// LB / RB（两页通用）。弹窗、分组编辑窗口、下拉层、录键、打字时不响应；同一帧只处理一次。
    /// 按住 RT：焦点在「模组列表 / 快捷键设置 / 模组设置」三个页签按钮间移动（<see cref="OverviewPadSteps.TabFocusStep"/>，
    /// 实测修正 2026-11-16：不直接切页，按 A 才切换）；
    /// 不按 RT：焦点在第二行工具行的按钮间跳（<see cref="OverviewPadSteps.ToolbarStep"/>）。
    /// </summary>
    private void ShoulderTick(Gamepad pad)
    {
        if (_shoulderFrame == Time.frameCount) return; _shoulderFrame = Time.frameCount;
        bool left = pad.leftShoulder.wasPressedThisFrame, right = pad.rightShoulder.wasPressedThisFrame;
        if (left == right) return; // 都没按，或同一帧两个都按（意图不明）
        if (IsRecording || OverviewUi.HasModal || OverviewGroupEditor.AnyOpen || WindowPopover.AnyOpen || !_bound) return;
        int step = right ? 1 : -1;
        if (GamepadIntent.Layer)
        {
            // 实测修正（用户 2026-11-16）：RT + LB / RB 只把焦点移到三个页签按钮之一，按 A 才切换（按钮自己的 onClick）。
            var tabs = new[] { _listTab, _hotkeyTab, _settingsTab };
            var focusedObject = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            int focused = focusedObject == null ? -1 : System.Array.FindIndex(tabs, t => t != null && (t.gameObject == focusedObject || focusedObject.transform.IsChildOf(t)));
            int active = _tab == PageTab.Mods ? OverviewPadSteps.ModsTab : OverviewPadSteps.HotkeysTab;
            int next = OverviewPadSteps.TabFocusStep(focused, active, step);
            if (next < 0 || tabs[next] == null || !tabs[next].gameObject.activeInHierarchy) return;
            EventSystem.current?.SetSelectedGameObject(tabs[next].gameObject);
            return;
        }
        var stops = ToolbarStops();
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        int at = selected == null ? -1 : stops.FindIndex(s => s.gameObject == selected || selected.transform.IsChildOf(s.transform));
        int target = OverviewPadSteps.ToolbarStep(at, step, stops.Count);
        if (target >= 0) EventSystem.current?.SetSelectedGameObject(stops[target].gameObject);
    }
    // ── 方向键连发 ─────────────────────────────────────────────
    // 实测修正（用户 2026-11-16）：十字键 / 左摇杆按住时焦点不会连续移动（两个页签都是）。这一段代码里查不到关掉引擎连发的地方
    // （UI 输入模块的 moveRepeatDelay / Rate 没人改，页签的 OnMove 也只走一步），原因没能在桌面上定位到哪一处；
    // 所以连发改由外壳自己做，不再依赖引擎：按下那一下仍由引擎走（各页原有的 OnMove 逻辑不变），按住 0.15 秒后
    // 暂停引擎的方向导航（免得引擎的连发若恢复了会一次走两步），0.4 秒起每 0.08 秒对当前焦点发一次与引擎相同的 Move 事件；
    // 松开、换方向、按 RT、推右摇杆、录键时立即交还。导航已被别处暂停（录键、键鼠录制）时不连发。
    private const float MoveRepeatDelay = .4f, MoveRepeatInterval = .08f, MoveRepeatTakeover = .15f;
    private MoveDirection _repeatDirection = MoveDirection.None;
    private float _repeatSince;
    private int _repeatFired;
    private bool _repeatHolding;
    private void MoveRepeatTick(Gamepad pad)
    {
        var direction = HeldDirection(pad);
        var system = EventSystem.current;
        if (direction == MoveDirection.None || system == null || IsRecording || GamepadIntent.Layer || GamepadIntent.RightStickHeld) { ReleaseMoveRepeat(); return; }
        if (direction != _repeatDirection)
        {
            ReleaseMoveRepeat(); // 新按下或换方向：这一下交给引擎走
            _repeatDirection = direction; _repeatSince = Time.unscaledTime; _repeatFired = 0;
            return;
        }
        float held = Time.unscaledTime - _repeatSince;
        if (!_repeatHolding)
        {
            if (held < MoveRepeatTakeover || !system.sendNavigationEvents) return; // 导航被别处暂停：不接管
            GamepadIntent.HoldNavigation(_frame, true); _repeatHolding = true;
        }
        int due = OverviewPadSteps.RepeatCount(held, MoveRepeatDelay, MoveRepeatInterval);
        if (_repeatFired >= due) return;
        _repeatFired = due; // 一帧最多走一步；掉帧时不补发
        var selected = system.currentSelectedGameObject;
        if (selected == null || !selected.activeInHierarchy) return;
        var data = new AxisEventData(system) { moveDir = direction, moveVector = MoveVector(direction) };
        ExecuteEvents.Execute(selected, data, ExecuteEvents.moveHandler);
    }
    /// <summary>页面被关掉（切到设置页、离开菜单、销毁）时一定交还方向导航，免得停在暂停状态。</summary>
    private void OnDisable() => ReleaseMoveRepeat();
    private void ReleaseMoveRepeat()
    {
        if (_repeatHolding && _frame != null) GamepadIntent.HoldNavigation(_frame, false);
        _repeatHolding = false; _repeatDirection = MoveDirection.None;
    }
    /// <summary>十字键优先；否则左摇杆推过一半时取主轴方向。</summary>
    private static MoveDirection HeldDirection(Gamepad pad)
    {
        if (pad.dpad.up.isPressed) return MoveDirection.Up;
        if (pad.dpad.down.isPressed) return MoveDirection.Down;
        if (pad.dpad.left.isPressed) return MoveDirection.Left;
        if (pad.dpad.right.isPressed) return MoveDirection.Right;
        var stick = pad.leftStick.ReadValue();
        if (stick.sqrMagnitude < .5f * .5f) return MoveDirection.None;
        return Mathf.Abs(stick.x) > Mathf.Abs(stick.y) ? (stick.x > 0 ? MoveDirection.Right : MoveDirection.Left) : (stick.y > 0 ? MoveDirection.Up : MoveDirection.Down);
    }
    private static Vector2 MoveVector(MoveDirection direction) => direction switch
    {
        MoveDirection.Up => Vector2.up, MoveDirection.Down => Vector2.down, MoveDirection.Left => Vector2.left, MoveDirection.Right => Vector2.right, _ => Vector2.zero
    };
    /// <summary>弹窗开着时，右摇杆滚动弹窗内容（页签内容自己处理自己的滚动）。</summary>
    private void DriveLayerScroll(bool gamepad)
    {
        Transform? layer = OverviewUi.TopLayer != null ? OverviewUi.TopLayer.transform : null;
        if (gamepad && layer != null) GamepadIntent.ScrollWithin(layer);
        GamepadIntent.HoldNavigation(this, gamepad && layer != null && GamepadIntent.RightStickHeld);
    }
    /// <summary>After a tab switch the focus goes into the new page's content, not the tab strip; with no focusable content it stays on the tab.</summary>
    private void FocusTabContent()
    {
        if (!GamepadSupport.IsGamepad) return;
        if (_tab == PageTab.Mods) _list?.FocusListForGamepad();
        else if (_tab == PageTab.Hotkeys) _hotkeys?.FocusForGamepad();
        var system = EventSystem.current;
        if (system != null && (system.currentSelectedGameObject == null || !system.currentSelectedGameObject.activeInHierarchy))
            system.SetSelectedGameObject(TabRect(_tab).gameObject);
    }

    // ── 按键提示条 ─────────────────────────────────────────────

    private bool _promptShown;
    private GameObject? _promptSelected;
    private bool _promptLayer, _promptModal, _promptRecording;
    private PageTab _promptTab;
    private int _promptRevision = -1;
    private void UpdatePromptBar(bool gamepad)
    {
        bool show = gamepad && !_promptsHidden && _frame != null;
        if (show != _promptShown) { _promptShown = show; RefreshLayout(); } // status line height follows the strip
        if (!show) { if (_promptBar != null) _promptBar.gameObject.SetActive(false); _promptKey = ""; return; }
        if (_promptBar == null)
        {
            _promptBar = OverviewUi.Rect("GamepadPrompts", _frame!);
            _promptText = OverviewUi.Text(_promptBar, "", 16); OverviewUi.Fill(_promptText.rectTransform);
            _promptText.alignment = TextAlignmentOptions.MidlineRight; _promptText.richText = true; _promptText.color = OverviewUi.Ivory;
            _promptText.textWrappingMode = TextWrappingModes.NoWrap; _promptText.overflowMode = TextOverflowModes.Ellipsis; _promptText.raycastTarget = false;
            _promptText.parseCtrlCharacters = false;
        }
        _promptBar.gameObject.SetActive(true);
        OverviewUi.Place(_promptBar, 20, _frame!.rect.height - 30, _frame.rect.width - 40, 26);
        _promptBar.SetAsLastSibling();
        // Rebuild the text only when something it depends on changed (no per-frame string work).
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        bool layer = GamepadIntent.Layer, modal = OverviewUi.HasModal || OverviewGroupEditor.AnyOpen, recording = IsRecording;
        if (_promptKey.Length > 0 && selected == _promptSelected && layer == _promptLayer && modal == _promptModal && recording == _promptRecording
            && _tab == _promptTab && _promptRevision == Loc.TranslationRevision) return;
        _promptSelected = selected; _promptLayer = layer; _promptModal = modal; _promptRecording = recording; _promptTab = _tab; _promptRevision = Loc.TranslationRevision;
        string key = PromptKey();
        if (key == _promptKey) return;
        _promptKey = key;
        _promptText!.text = PromptText(key);
    }
    private string PromptKey()
    {
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        string where = IsRecording ? "recording" : OverviewUi.HasModal || OverviewGroupEditor.AnyOpen ? "modal" : _tab.ToString();
        string kind = selected == null ? "" : selected.GetComponent<TMP_InputField>() != null ? "input" : selected.GetComponent<UnityEngine.UI.Toggle>() != null ? "toggle"
            : selected.GetComponent<OverviewGamepadSubmit>() != null ? "name" : "button"; // 第 7 章：模组列表的名称格 / 简介格 A = 菜单
        return where + "|" + kind + "|" + GamepadIntent.Layer + "|" + Loc.TranslationRevision + "|" + (Gamepad.current?.displayName ?? "");
    }
    private string PromptText(string key)
    {
        var parts = key.Split('|');
        string where = parts[0], kind = parts[1];
        var items = new List<string>();
        void Add(string control, string label) => items.Add(Glyph(control) + " " + label);
        // 1.1.0（MCE）：手柄录制中整个手柄被锁定，只有长按 B 1 秒能退出。
        if (where == "recording")
        {
            Add("buttonEast", Loc.T("Hold 1 s to finish recording", "长按 1 秒结束录制", "長按 1 秒結束錄製"));
            return string.Join("　　", items);
        }
        bool mods = where == PageTab.Mods.ToString();
        bool listLike = mods || where == PageTab.Hotkeys.ToString();
        bool layer = parts[2] == "True" && listLike;
        // 上线前修正（用户 2026-11-16）：两页统一——RT + LB / RB 选页签、RT + ←→ 翻页、RT + L3 全部展开 / 收起；
        // 模组列表另有 RT + ↑↓ 上 / 下一个分组、RT + Y 分组菜单（快捷键页的 RT + ↑↓ 跳模组已去掉）。
        if (layer)
        {
            string rt = Glyph("rightTrigger") + "+";
            // 实测修正（2026-11-16）：只移焦点到页签按钮，按 A 才切换。
            items.Add(rt + Glyph("leftShoulder") + Glyph("rightShoulder") + " " + Loc.T("Choose tab (A to open)", "选择页签（A 切换）", "選擇頁籤（A 切換）"));
            if (mods) items.Add(rt + Glyph("dpad") + " " + Loc.T("Previous / next group", "上 / 下一个分组", "上 / 下一個分組"));
            items.Add(rt + Glyph("left") + Glyph("right") + " " + Loc.T("Page", "翻页", "翻頁"));
            if (mods) items.Add(rt + Glyph("buttonNorth") + " " + Loc.T("Group menu", "分组菜单", "分組選單"));
            items.Add(rt + Glyph("leftStickPress") + " " + (mods ? Loc.T("Fold / unfold all groups", "折叠 / 展开全部分组", "摺疊 / 展開全部分組")
                : Loc.T("Expand / collapse all", "全部展开 / 收起", "全部展開 / 收起")));
            return string.Join("　　", items);
        }
        string confirm = kind == "name" ? Loc.T("Menu", "菜单", "選單") : kind == "toggle" ? Loc.T("Tick", "勾选", "勾選") : kind == "input" ? Loc.T("Type", "输入", "輸入") : Loc.T("Select", "确认", "確認");
        Add("buttonSouth", confirm);
        if (listLike) Add("buttonNorth", Loc.T("More", "更多", "更多"));
        // 1.1.0（MCE，用户 2026-11-15）：B 先逐层关菜单 / 取消录键，什么都没得关时快速连按两次才离开页面。
        Add("buttonEast", listLike ? Loc.T("Back (twice quickly to leave)", "返回（快速按两次离开）", "返回（快速按兩次離開）") : Loc.T("Back", "返回", "返回"));
        if (listLike) items.Add(Glyph("leftShoulder") + Glyph("rightShoulder") + " " + Loc.T("Toolbar", "工具栏", "工具列"));
        if (listLike) Add("rightTrigger", Loc.T("Hold for more", "按住看更多", "按住看更多"));
        if (listLike) Add("leftStickPress", mods ? Loc.T("Fold group", "折叠分组", "摺疊分組") : Loc.T("Fold mod", "收起 / 展开模组", "收起 / 展開模組"));
        Add("select", Loc.T("Hide these hints", "隐藏按键提示", "隱藏按鍵提示"));
        return string.Join("　　", items);
    }
    private string Glyph(string control)
    {
        if (control == "dpad") return Glyph("up") + Glyph("down");
        string glyph = GamepadIntent.Glyph(control, out var sprites);
        if (sprites != null && _promptText != null) { _promptText.spriteAsset = sprites; return glyph; }
        return "<b>" + glyph + "</b>";
    }
}
