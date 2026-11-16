#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ModConfigEnhance.Overview;

// 1.1.0（MCE，概览页第 7 章）：照抄 IGMM Overview/OverviewPage.Toolbar.cs 的布局部分后裁剪。
// 工具行（Q10，MCE 不联网）：刷新、隐藏已停用、铅笔、分组管理、语言、主题，放不下的收进「更多」（同 IGMM：只有溢出时才出现）；
// 「日志」照 IGMM 固定在右端（ToolbarLayout.Arrange 的 helpWidth，不收进「更多」）。
// 第 8b 章：铅笔（"edit"，编辑模式，Q5 只管拖动）与分组管理（"groups"，IGMM 同位置同排序值是「管理中心」）照 IGMM 的分组「编辑工具」；
// 分组管理两页都显示（同 IGMM）；铅笔只在模组列表页显示（MCE 的铅笔只管列表拖动，快捷键页没有可拖的东西；IGMM 两页都显示）。
// 裁掉：方案选择 / 保存配置 / 变更检查（方案）、标签筛选、来源与更新、导入、模组商店、管理中心、使用说明、自动检查提示。
internal sealed partial class OverviewPage
{
    private Button _more = null!;
    private readonly List<TextMeshProUGUI> _toolSeparators = new();
    private readonly List<ToolbarTool> _toolbarTools = new();
    private readonly HashSet<string> _hiddenTools = new();
    private RectTransform? _toolbarPopup;
    private string _toolbarLayoutKey = "";
    private GameObject? _activeToolHintTarget;
    private Func<string>? _activeToolHintText;
    // 1.1.0（MCE，第 11 章审查）：三参数（繁体照抄 IGMM 词条表）；MCE 的 Loc.T 没有 IGMM 的内联译文层，两参数版繁体会显示简体。
    private static string T(string en, string zh, string tw) => Loc.T(en, zh, tw);
    private void ClearToolHint()
    {
        _activeToolHintTarget = null; _activeToolHintText = null;_list?.HideExternalHint();
    }
    private void RefreshActiveToolHint()
    {
        if (_activeToolHintText == null) return;
        if (_activeToolHintTarget == null || !_activeToolHintTarget.activeInHierarchy) { ClearToolHint(); _list?.HideExternalHint(); return; }
        _list?.RefreshExternalHint(_activeToolHintTarget,_activeToolHintText);
    }

    private sealed class ToolbarTool
    {
        internal string Id = "", Label = "", GroupName = "", Reason = "";
        internal int Group, Order;
        internal float Width;
        internal Button Button = null!;
        internal UiActionRole Role;
        internal bool Checked;
    }
    private void LayoutToolbar(float width)
    {
        string layoutKey = width + ":" + _tab + ":" + _author + ":" + _languageRevision + ":" + _repo.State.hideDisabled;
        if (layoutKey != _toolbarLayoutKey && _toolbarPopup != null && _toolbarPopup.gameObject.activeInHierarchy) OverviewUi.CloseModal();
        _toolbarLayoutKey = layoutKey;
        float available = Mathf.Max(0, width - 40);
        _toolbarTools.Clear(); bool list = _tab == PageTab.Mods;
        void Tool(string id, Button button, string label, int group, string groupName, int order, float minimum, UiActionRole role, bool shown = true, bool check = false)
        {
            button.gameObject.SetActive(shown); if (!shown) return;
            var caption = button.GetComponentInChildren<TextMeshProUGUI>(true); if (caption != null && id != "edit" && id != "theme") caption.text = label;
            float measured = Mathf.Max(minimum, id == "edit" || id == "theme" || caption == null ? minimum : caption.GetPreferredValues(label).x + 24);
            UiActionStyle.Set(button, role);
            _toolbarTools.Add(new ToolbarTool { Id = id, Button = button, Label = label, Group = group, GroupName = groupName, Width = measured, Order = order, Role = role, Checked = check });
        }
        string display = T("Display", "显示筛选", "顯示篩選"), editing = T("Editing", "编辑工具", "編輯工具"), preferences = T("Preferences", "偏好", "偏好");
        Tool("hide", _hide, T("Hide disabled", "隐藏已停用", "隱藏已停用"), 1, display, 5, 112, _repo.State.hideDisabled ? UiActionRole.Edit : UiActionRole.Neutral, list, _repo.State.hideDisabled);
        Tool("refresh", _refresh, T("Refresh", "刷新", "重新整理"), 1, display, 2, 76, UiActionRole.Neutral);
        // 第 8b 章：照 IGMM 的「编辑工具」组（分组号 3、排序值同 IGMM：铅笔 6、管理中心 int.MaxValue - 1 → 最后才收进「更多」）。
        Tool("edit", _edit, T("Edit mode", "编辑模式", "編輯模式"), 3, editing, 6, 32, _author ? UiActionRole.Edit : UiActionRole.Neutral, list, _author);
        Tool("groups", _groups, Loc.T("Group management", "分组管理", "分組管理"), 3, editing, int.MaxValue - 1, 105, UiActionRole.Edit);
        Tool("language", _language, T("Language", "语言", "語言"), 4, preferences, 4, 76, UiActionRole.Edit);
        Tool("theme", _theme, Loc.T("Colour theme", "配色主题", "配色主題"), 4, preferences, 3, 32, UiActionRole.Neutral);
        var moreCaption = _more.GetComponentInChildren<TextMeshProUGUI>(true); moreCaption.text = T("More", "更多", "更多") + " " + OverviewUi.Caret;
        float moreWidth = Mathf.Max(76, moreCaption.GetPreferredValues(moreCaption.text).x + 24);
        // 1.1.0（MCE，第 8b 章）：右端固定区照 IGMM，只有「日志」（「使用说明」不搬）。
        var logCaption = _logs.GetComponentInChildren<TextMeshProUGUI>(true); logCaption.text = Loc.T("Logs", "日志", "日誌");
        float logWidth = Mathf.Max(70, logCaption.GetPreferredValues(logCaption.text).x + 24);
        UiActionStyle.Set(_logs, UiActionRole.Overview);
        var result = ToolbarLayout.Arrange(available, logWidth, moreWidth, _toolbarTools.Select(i => new ToolbarLayout.Item { Id = i.Id, Group = i.Group, Width = i.Width, CollapseOrder = i.Order }).ToList());
        var selected = EventSystem.current?.currentSelectedGameObject;
        bool movedFocus = selected != null && _toolbarTools.Any(i => result.Hidden.Contains(i.Id) && (selected == i.Button.gameObject || selected.transform.IsChildOf(i.Button.transform)));
        _hiddenTools.Clear(); _hiddenTools.UnionWith(result.Hidden);
        foreach (var tool in _toolbarTools)
        {
            bool visible = result.Visible.TryGetValue(tool.Id, out var slot); tool.Button.gameObject.SetActive(visible);
            if (visible) OverviewUi.Place((RectTransform)tool.Button.transform, slot.X, 0, slot.Width, 34);
        }
        _more.gameObject.SetActive(result.Hidden.Count > 0); if (_more.gameObject.activeSelf) OverviewUi.Place((RectTransform)_more.transform, result.MoreX, 0, moreWidth, 34);
        OverviewUi.Place((RectTransform)_logs.transform, result.HelpX, 0, logWidth, 34);
        for (int i = 0; i < result.Separators.Count; i++)
        {
            if (i == _toolSeparators.Count) { var separator = OverviewUi.Text(_controls, "|", 22); separator.color = ModConfigLook.Surface(new Color(.72f, .68f, .56f, .48f)); separator.alignment = TextAlignmentOptions.Midline; _toolSeparators.Add(separator); }
            var text = _toolSeparators[i]; text.gameObject.SetActive(true); OverviewUi.PlaceClipped(text, result.Separators[i], 0, 8, 34);
        }
        for (int i = result.Separators.Count; i < _toolSeparators.Count; i++) _toolSeparators[i].gameObject.SetActive(false);
        _edit.GetComponentInChildren<OverviewIconGraphic>().color = OverviewUi.Ivory; UiActionStyle.Select(_hide, _repo.State.hideDisabled);
        var navigation = _controls.GetComponentsInChildren<Selectable>(false).Where(s => s.IsActive() && s.IsInteractable()).OrderBy(s => s.transform.position.x).ToList();
        var bodyTargets = _body.GetComponentsInChildren<Selectable>(false).Where(s => s.IsActive() && s.IsInteractable()).ToList();
        var pageTabs = new[] { _back, _listTab.GetComponent<Button>(), _hotkeyTab.GetComponent<Button>(), _settingsTab.GetComponent<Button>() }.Where(s => s.IsActive() && s.IsInteractable()).ToList();
        for (int i = 0; i < navigation.Count; i++)
        {
            var control = navigation[i];
            var up = pageTabs.OrderBy(s => Mathf.Abs(s.transform.position.x - control.transform.position.x)).FirstOrDefault();
            var down = bodyTargets.OrderByDescending(s => s.transform.position.y).ThenBy(s => Mathf.Abs(s.transform.position.x - control.transform.position.x)).FirstOrDefault();
            control.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = navigation[(i + navigation.Count - 1) % navigation.Count], selectOnRight = navigation[(i + 1) % navigation.Count], selectOnUp = up, selectOnDown = down };
            GamepadSupport.MarkSelectable(control.gameObject, null, (RectTransform)control.transform);
        }
        movedFocus |= selected != null && selected.transform.IsChildOf(_controls) && !selected.activeInHierarchy;
        if (movedFocus) EventSystem.current?.SetSelectedGameObject(_more.gameObject.activeSelf ? _more.gameObject : _refresh.gameObject);
        RefreshActiveToolHint();
    }
    /// <summary>上线前修正（用户 2026-11-16）：手柄 LB / RB 在第二行工具行的按钮间跳的顺序 = 屏幕上从左到右（含「更多」「日志」）。</summary>
    private List<Selectable> ToolbarStops() => _controls.GetComponentsInChildren<Selectable>(false)
        .Where(s => s.IsActive() && s.IsInteractable()).OrderBy(s => s.transform.position.x).ToList();
    private RectTransform ToolAnchor(Button button) => (RectTransform)(button.gameObject.activeInHierarchy ? button.transform : _more.transform);
    private void ShowToolbarMore()
    {
        var entries = new List<UiMenuEntry>();
        foreach (var group in _toolbarTools.Where(t => _hiddenTools.Contains(t.Id)).GroupBy(t => t.Group))
        {
            entries.Add(new UiMenuEntry { Header = true, Label = group.First().GroupName });
            foreach (var tool in group)
            {
                entries.Add(new UiMenuEntry { Label = tool.Label, Role = tool.Role, Enabled = tool.Button.interactable, Checked = tool.Checked, DisabledReason = tool.Reason, CanInvoke = () => tool.Button.interactable, Invoke = () => tool.Button.onClick.Invoke() });
            }
        }
        _toolbarPopup = OverviewUi.Dropdown((RectTransform)_more.transform, T("More", "更多", "更多"), entries, 390);
    }
}
