#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using HarmonyLib;
using PEAKLib.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zorro.UI;

namespace ModConfigEnhance.Overview;

// 1.1.0（MCE，快捷键页搬迁第 3 章）：照抄 IGMM Overview/OverviewController.cs 后裁剪。
// 留下：游戏设置菜单入口（ModConfig「模组设置」下方）、分阶段打开与 OpenDriver（设置页表头跳快捷键用）、页面外壳。
// 裁掉：暂停菜单左上角入口、整合包名称 / 版本、OverviewOptions（MCE 不加这组选项，入口固定显示）、
// 主菜单背景预建与商店元数据预取（第一次点击才建页面）、方案仓库 / 管理中心 / 整合包 / 更新 / 工具条 / 作者模式。
// 概览页第 6 章：外壳持有精简仓库 OverviewRepository 与扫描出的模组清单（OverviewPage.Mods），快捷键页从中取已加载的插件；
// 入口第一次出现时后台预读模组元数据（OverviewCatalogue.Prefetch，本地只读）。
// 概览页第 7 章：外壳加「模组列表」页签（默认页签）与共用工具行；页面外壳见下方 OverviewPage。

internal static class OverviewController
{
    private static readonly List<OverviewEntry> Entries = new();
    private static MenuAPI.BuilderDelegate? _settingsBuilder;
    private static bool _running;
    internal static void Register(Harmony harmony)
    {
        _running = true;
        _settingsBuilder ??= BuildSettings; MenuAPI.AddToSettingsMenus(_settingsBuilder);
        TypingGuard.TryApply(harmony); // 概览页的输入框也要打字保护；树形侧栏关掉时这里是唯一的挂载点（重复调用无副作用）
        // 最后输入设备跟踪 + 输入框「A 才打字」闸门：原来只在树形侧栏挂上时装，关掉侧栏后概览页会把滑鼠当手柄（键盘栏录不了）。
        GamepadSupport.EnsureInputTracking(harmony);
        OverviewSettingsNavigator.Register(harmony);
    }
    private static void BuildSettings(Transform parent)
    {
        // PEAKLib.UI 只在 MainMenuSettingsPage.Start 调设置菜单构建器；暂停菜单里的「设置」也是这个页面，所以局内同样有入口。
        var page = parent.GetComponent<MainMenuSettingsPage>();
        if (!_running || page == null || parent.Find("MCE_OverviewSettingsEntry") != null) return;
        try
        {
            Entries.RemoveAll(entry => entry == null); // 换场景后旧菜单连同入口一起销毁
            ModConfigPaperStyle.Resolve(parent);
            var root = OverviewUi.Rect("MCE_OverviewSettingsEntry", parent);
            root.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var entry = root.gameObject.AddComponent<OverviewEntry>();
            entry.Initialize(page); Entries.Add(entry);
        }
        catch (Exception ex) { Plugin.Log.LogError("[模组概览] 创建设置入口失败：" + ex); }
    }
    /// <summary>
    /// 2.4.1：模组设置页表头「跳转到快捷键设置页」。找与设置页同一个菜单（同一 UIPageHandler）的概览入口，
    /// 打开概览并切到快捷键设置页签；概览未建好时走入口的分帧打开，建好后再切页签。
    /// </summary>
    internal static void OpenHotkeys(UIPage? from)
    {
        if (from == null) return;
        var handler = from.GetComponentInParent<UIPageHandler>(true);
        foreach (var entry in Entries)
            if (entry != null && entry.BelongsTo(handler)) { entry.OpenOnHotkeys(from); return; }
        Plugin.Log.LogWarning("[模组概览] 跳转快捷键设置页：找不到当前菜单的概览入口。");
    }
}

internal sealed class OverviewEntry : MonoBehaviour
{
    private UIPage? _parent;
    private OverviewPage? _page;
    private IEnumerator? _shell;
    private bool _opening;
    private int _stage;
    private int _builtEpoch = -1;
    private void EnsureShell()
    {
        // 2.4.0: colours are baked in when the page is built; a page built under the other theme is rebuilt.
        if (_page != null && !_opening && _builtEpoch != ModConfigLook.Epoch && !_page.gameObject.activeSelf)
        { (_shell as IDisposable)?.Dispose(); _shell = null; _page.DisposeResources(); UnityEngine.Object.Destroy(_page.gameObject); _page = null; }
        if (_page != null || _parent == null) return;
        _builtEpoch = ModConfigLook.Epoch;
        var rect = OverviewUi.Rect("MCE_OverviewPage", _parent.transform.parent); OverviewUi.Fill(rect);
        rect.gameObject.SetActive(false);
        _page = rect.gameObject.AddComponent<OverviewPage>(); _shell = _page.BuildShell(_parent);
    }
    /// <summary>推进当前阶段的步骤；返回 true 表示这一阶段还没走完（下一帧继续）。失败时已收尾、打开被取消。</summary>
    private bool PumpShell()
    {
        try
        {
            if (_shell == null) return false;
            // Advance cheap shell steps together; only yield after actual work used the budget.
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int steps = 0; steps < 8 && _shell != null; steps++)
            {
                if (!_shell.MoveNext()) { (_shell as IDisposable)?.Dispose(); _shell = null; break; }
                if ((System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d / System.Diagnostics.Stopwatch.Frequency >= 4) break;
            }
        }
        catch (Exception error) { FailOpen(error); return false; }
        return _shell != null;
    }
    private void FailOpen(Exception error)
    {
        Plugin.Log.LogError("[概览] 页面准备失败：" + error);
        (_shell as IDisposable)?.Dispose(); _shell = null;
        if (_page != null)
        {
            // 切页之后才出错时，概览页已是当前页：先切回入口所在的设置页再销毁，免得菜单停在一个被销毁的页面上。
            try
            {
                var handler = _parent != null ? _parent.GetComponentInParent<UIPageHandler>(true) : null;
                if (handler != null && _parent != null && handler.currentPage == _page)
                    handler.TransistionToPage(_parent, new SetActivePageTransistion());
            }
            catch (Exception back) { Plugin.Log.LogError("[概览] 打开失败后退回设置页也失败：" + back); }
            _page.DisposeResources(); UnityEngine.Object.Destroy(_page.gameObject); _page = null;
        }
        _opening = false; _driven = false; _openHotkeys = false; EntryNavLink.ReturnFocus = false;
    }
    /// <summary>2.5.4（二次审查）：打开中途用户离开——不再推进，也不在下次回来时突然切页；跳快捷键的意图一并作废。</summary>
    private void CancelOpen() { _opening = false; _driven = false; _openHotkeys = false; EntryNavLink.ReturnFocus = false; }
    private Button? _button;
    private RectTransform? _settingsReference;
    private TextMeshProUGUI? _settingsReferenceText, _settingsCaption;
    private float _settingsReferenceRetryAt;
    private SettingsEntryLayout? _settingsLayout;
    internal void Initialize(UIPage parent)
    {
        _parent = parent;
        _button = OverviewUi.Button(transform, Caption, OpenFromEntry, 180, 43, role: UiActionRole.Overview);
        var caption = _button.GetComponentInChildren<TextMeshProUGUI>(true);
        _settingsCaption = caption;
        MatchNativeRibbon(_button, caption);
        // 1.1.4 (user): the entry sits away from the menu column's automatic navigation; link it explicitly.
        _button.gameObject.AddComponent<EntryNavLink>();
        GamepadSupport.MarkSelectable(_button.gameObject, null, null);
        caption.enableAutoSizing = true; caption.fontSizeMin = 13; caption.fontSizeMax = 18;
        PositionSettingsEntry();
    }
    private static string Caption => Loc.T("Mod overview", "模组概览", "模組概覽");
    /// <summary>青绿缎带：与 ModConfig 的「模组设置」蓝、「返回」红同一档饱和度与明度，两套主题都不变（它挨着原生按钮）。</summary>
    internal static readonly Color EntryRibbon = new Color32(0x2a, 0x80, 0x6e, 255);
    /// <summary>
    /// 2.4.7（用户：「模组概览」字体与上面的「返回」「模组设置」不一样、颜色不搭）：入口挨着游戏原生缎带，
    /// 不用 MCE 自己的正文字体与功能色，改回按钮模板原样的字体、材质、字色与阴影，底色用固定青绿。
    /// </summary>
    private static void MatchNativeRibbon(Button button, TextMeshProUGUI caption)
    {
        var style = button.GetComponent<UiActionStyle>(); if (style != null) UnityEngine.Object.DestroyImmediate(style);
        var peak = button.GetComponent<PEAKLib.UI.Elements.PeakMenuButton>(); if (peak != null) peak.SetColor(EntryRibbon);
        var weight = caption.GetComponent<MceTextWeightBinding>(); if (weight != null) UnityEngine.Object.DestroyImmediate(weight);
        var template = PEAKLib.UI.Elements.Templates.ButtonTemplate;
        var source = template != null ? template.transform.Find("Text")?.GetComponent<TextMeshProUGUI>() : null;
        if (source == null) return;
        caption.font = source.font; caption.fontSharedMaterial = source.fontSharedMaterial;
        caption.fontStyle = source.fontStyle; caption.fontWeight = source.fontWeight; caption.characterSpacing = source.characterSpacing;
        caption.color = source.color;
        var shadows = caption.GetComponents<Shadow>(); var sourceShadows = source.GetComponents<Shadow>();
        for (int i = 0; i < shadows.Length && i < sourceShadows.Length; i++) shadows[i].enabled = sourceShadows[i].enabled;
    }
    private bool _openHotkeys;
    internal bool BelongsTo(UIPageHandler? handler) => _parent != null && handler != null && _parent.GetComponentInParent<UIPageHandler>(true) == handler;
    /// <summary>
    /// 2.5.1：从模组设置页跳过来时，入口所在的「设置」页是隐藏的，入口自己的 Update 不跑，分帧打开停在第一步（点了没反应）。
    /// 这时由当前可见页面上的 <see cref="OpenDriver"/> 代为推进，打开完成后自毁。
    /// </summary>
    internal void OpenOnHotkeys(UIPage from)
    {
        _openHotkeys = true; _returnFrom = from; Open();
        if (_opening && (_parent == null || !_parent.gameObject.activeInHierarchy))
        {
            _driven = true;
            // Unity 判空：同一帧里刚 Destroy 的旧 OpenDriver 仍会被 GetComponent 取到，?? 不会换新的。
            var driver = from.gameObject.GetComponent<OpenDriver>();
            if (driver == null) driver = from.gameObject.AddComponent<OpenDriver>();
            driver.Entry = this;
        }
    }
    private bool _driven;
    internal bool Opening => _opening;
    internal void PumpOpen() { if (_opening) StepOpen(); }
    internal sealed class OpenDriver : MonoBehaviour
    {
        internal OverviewEntry? Entry;
        private void Update()
        {
            if (Entry == null || !Entry.Opening) { if (Entry != null) Entry._driven = false; Destroy(this); return; }
            Entry.PumpOpen();
        }
        // 2.5.4（二次审查）：挂它的设置页被隐藏 = 用户在打开完成前离开了 → 取消这次打开（已切到概览时 Opening 已为 false，不受影响）
        private void OnDisable()
        {
            if (Entry != null && Entry.Opening) Entry.CancelOpen();
            Entry = null;
            Destroy(this);
        }
    }
    /// <summary>
    /// 第 5 章（用户 2026-11-15）：从模组设置页表头「跳转到快捷键设置页」进来时记下那一页，离开概览（连按两次 B /
    /// 返回钮）回到它；从游戏设置菜单的「模组概览」入口进来仍回游戏设置页。
    /// </summary>
    private UIPage? _returnFrom;
    private void OpenFromEntry() { if (!_opening) _returnFrom = null; Open(); }
    private void Open()
    {
        if (_parent == null) return;
        if (_opening) return;
        EntryNavLink.ReturnFocus = true;
        try { EnsureShell(); } catch (Exception error) { FailOpen(error); return; }
        _opening = true; _stage = 0;
        StepOpen();
    }
    // A stage boundary is the only place the frame can be given back: shell, content binding, first-entry
    // preparation and the page transition stay separate stages of the pump.
    private void StepOpen()
    {
        if (_parent == null || _page == null) { CancelOpen(); return; }
        if (_stage == 0)
        {
            if (PumpShell() || !_opening) return;
            _stage = 1;
        }
        if (_stage == 1)
        {
            if (!_page.IsBound)
            {
                _shell ??= _page.BindSteps();
                if (PumpShell() || !_opening) return;
            }
            _stage = 2;
        }
        if (_stage == 2)
        {
            _shell ??= _page.EnterSteps();
            if (PumpShell() || !_opening) return;
            _stage = 3;
        }
        FinishOpen();
    }
    private void FinishOpen()
    {
        if (_parent == null || _page == null) { CancelOpen(); return; }
        // 2.5.5（用户实机：跳快捷键只到概览）：切页会隐藏当前的设置页，触发 OpenDriver.OnDisable；
        // 若此时仍算「打开中」，它会取消打开并清掉跳快捷键的意图。所以先把状态收尾、记下意图，再切页。
        bool hotkeys = _openHotkeys;
        var returnTo = hotkeys ? _returnFrom : null;
        _openHotkeys = false; _opening = false; _driven = false; _returnFrom = null;
        try
        {
            _page.SetReturnPage(returnTo);
            var handler = _parent.GetComponentInParent<UIPageHandler>(true);
            if (handler != null) handler.TransistionToPage(_page, new SetActivePageTransistion());
            if (hotkeys) _page.ShowHotkeysTab();
        }
        catch (Exception error)
        {
            FailOpen(error);
        }
    }
    private void Update()
    {
        if (_opening)
        {
            // 入口所在页被隐藏：用户离开了就取消；由设置页跳转代推进（OpenDriver）的不取消
            if (_parent == null || !_parent.gameObject.activeInHierarchy && !_driven) { CancelOpen(); return; }
            if (_parent.gameObject.activeInHierarchy) StepOpen();
            return;
        }
        // 1.1.0（MCE，概览页第 6 章）：照 IGMM 在入口可见后预读一次模组元数据（后台线程，Cecil 只读 DLL 与 manifest，不写文件）。
        // IGMM 在主菜单停 3 秒后做；MCE 的入口只在设置菜单里，所以入口第一次出现就做，每局一次，首次打开概览时扫描就只剩查缓存。
        if (!_metadataWarmed) { _metadataWarmed = true; OverviewCatalogue.Prefetch(); }
        PositionSettingsEntry();
    }
    private static bool _metadataWarmed;
    private void PositionSettingsEntry()
    {
        if (_parent == null || _button == null) return;
        // Only discover a missing/replaced direct child. Hovering never scans the page tree.
        if (_settingsReference == null || _settingsReference.parent != _parent.transform)
        {
            _settingsReference = null; _settingsReferenceText = null; _settingsLayout = null;
            if (Time.unscaledTime >= _settingsReferenceRetryAt)
            {
                _settingsReferenceRetryAt = Time.unscaledTime + .25f;
                _settingsReference = _parent.transform.Find("UI_MainMenuButton_MOD SETTINGS") as RectTransform;
                if (_settingsReference != null)
                    _settingsReferenceText = _settingsReference.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }
        var reference = _settingsReference;
        // 1.1.0（MCE）：没有 OverviewOptions.Enabled；只要 ModConfig 的「模组设置」按钮在，入口就显示在它下方。
        bool visible = reference != null;
        if (_button.gameObject.activeSelf != visible) _button.gameObject.SetActive(visible);
        if (reference == null || _settingsCaption == null) return;
        // localScale/rotation and TMP's animated/current auto-size are deliberately excluded.
        // 2.4.7：字号与上面的「模组设置」一致（原先 0.85 倍、上限 22，看着比原生小一号）
        float fontMax = _settingsReferenceText == null ? 22 : Mathf.Min(30,
            _settingsReferenceText.enableAutoSizing ? _settingsReferenceText.fontSizeMax : _settingsReferenceText.fontSize);
        var layout = new SettingsEntryLayout(reference, fontMax, Loc.TranslationRevision);
        MatchReferenceStitches(reference);
        if (_settingsLayout.HasValue && _settingsLayout.Value.Equals(layout)) return;
        _settingsLayout = layout;
        var rect = (RectTransform)transform;
        rect.anchorMin = layout.AnchorMin; rect.anchorMax = layout.AnchorMax; rect.pivot = layout.Pivot;
        rect.sizeDelta = layout.Size;
        rect.anchoredPosition = layout.Position - new Vector2(0, layout.Height + 10);
        OverviewUi.Fill((RectTransform)_button.transform);
        _settingsCaption.fontSizeMax = fontMax;
        _settingsCaption.fontSizeMin = Mathf.Min(13, fontMax);
        // 1.1.0（MCE）：没有 Loc.BindInline；语言变了（布局键含 TranslationRevision）在这里换字。
        _settingsCaption.text = Caption;
    }
    /// <summary>
    /// 1.1.0（MCE，实测修正）：入口缎带的两条虚线照抄上面「模组设置」按钮的虚线（锚点、轴心、位置、尺寸）。
    /// 原因：入口先按 180×43 建（OverviewUi.Button 里 FitBorders 按 43/67 把虚线往中间缩），随后 PositionSettingsEntry
    /// 用 Fill 把按钮改成拉满父物体的拉伸锚点，实际高度变成「模组设置」的高度，可 sizeDelta.y 成了 0，
    /// FitBorders（读 sizeDelta.y）此后直接返回，虚线一直停在 43 高时的内缩位置，离上下边都偏远。
    /// 入口与「模组设置」同父物体、同锚点、同尺寸，直接抄它的虚线最准；只在值不同时写，每帧调用也不脏布局。
    /// 找不到参照的虚线（不是 PeakMenuButton、也没有 Border 子物体）时不动，保留原状。
    /// </summary>
    private void MatchReferenceStitches(RectTransform reference)
    {
        if (_button == null) return;
        var peak = _button.GetComponent<PEAKLib.UI.Elements.PeakMenuButton>();
        if (peak == null) return;
        RectTransform? top = null, bottom = null;
        var native = reference.GetComponent<PEAKLib.UI.Elements.PeakMenuButton>();
        if (native != null && native.BorderTop != null && native.BorderBottom != null)
        { top = native.BorderTop.rectTransform; bottom = native.BorderBottom.rectTransform; }
        else
        {
            // 与 ModConfigSidebarWidgets.ResolveBorderTemplate 同一套找法：名为 Border 的子物体 + 它的下一个兄弟。
            var border = reference.Find("Border");
            if (border != null && border.GetSiblingIndex() + 1 < reference.childCount)
            { top = border as RectTransform; bottom = reference.GetChild(border.GetSiblingIndex() + 1) as RectTransform; }
        }
        if (top == null || bottom == null) return;
        if (peak.BorderTop != null) Copy(top, peak.BorderTop.rectTransform);
        if (peak.BorderBottom != null) Copy(bottom, peak.BorderBottom.rectTransform);
        static void Copy(RectTransform from, RectTransform to)
        {
            if (to.anchorMin != from.anchorMin) to.anchorMin = from.anchorMin;
            if (to.anchorMax != from.anchorMax) to.anchorMax = from.anchorMax;
            if (to.pivot != from.pivot) to.pivot = from.pivot;
            if (to.sizeDelta != from.sizeDelta) to.sizeDelta = from.sizeDelta;
            if (to.anchoredPosition != from.anchoredPosition) to.anchoredPosition = from.anchoredPosition;
        }
    }
    private void OnDestroy() { (_shell as IDisposable)?.Dispose(); _shell = null; if (_page != null) { _page.DisposeResources(); UnityEngine.Object.Destroy(_page.gameObject); } }
}

/// <summary>
/// 1.1.0（MCE）：独立页面外壳（照抄 IGMM OverviewPage 的 BuildShell / GetParentPage / Back / SwitchTab 后裁剪）：
/// 背景、边框、纸面、返回钮、页签行、共用工具行、内容区、状态行。
/// 概览页第 7 章：页签「模组列表」（<see cref="OverviewListView"/>，默认页签，放在「快捷键设置」左边，同 IGMM 顺序）+「快捷键设置」
/// （<see cref="OverviewHotkeyView"/>）；两页共用工具行（OverviewPage.Toolbar.cs）与模组清单（<see cref="Mods"/>）。
/// 手柄底部提示栏在 OverviewPage.Gamepad.cs。离开顺序：录键锁定 → 文本框 → 关弹层 → 快速连按两次 B。
/// 上线前修正（用户 2026-11-16）：第三个页签「模组设置」（不是本页的内容，按下直接跳到 ModConfig 的模组设置页，见 <see cref="OpenSettingsTab"/>）；
/// 手柄两页统一：LB / RB 在第二行工具行的按钮间跳，RT + LB / RB 在「模组列表 → 快捷键设置 → 模组设置」页签按钮间移焦点、A 切换（OverviewPage.Gamepad.cs）。
/// </summary>
internal sealed partial class OverviewPage : UIPage, IHaveParentPage
{
    private UIPage _parent = null!;
    internal enum PageTab { Mods, Hotkeys }
    private PageTab _tab = PageTab.Mods;
    private RectTransform _frame = null!, _body = null!, _listTab = null!, _hotkeyTab = null!, _settingsTab = null!, _controls = null!;
    private TextMeshProUGUI _status = null!;
    private Button _back = null!, _refresh = null!, _hide = null!, _language = null!, _theme = null!;
    // 第 8b 章：铅笔（编辑模式，Q5 只管拖动）、分组管理（IGMM 同位置是「管理中心」）、日志（固定在右端，同 IGMM）。
    private Button _edit = null!, _groups = null!, _logs = null!;
    private bool _author;
    private OverviewGroupEditor? _groupEditor;
    private OverviewListView? _list;
    private OverviewHotkeyView? _hotkeys;
    private float _width = -1;
    private int _languageRevision = -1;
    private bool _entryPrepared;
    private bool _returnFromSettings;
    private float _settingsScrollPosition = 1;
    private GameObject? _selectionBeforeSettings;
    internal bool ShellReady { get; private set; }
    private bool _bound, _bindingStarted;
    internal bool IsBound => _bound;
    /// <summary>录键锁定中（快捷键页的键鼠录制或手柄录制）：B / 返回钮都不离开页面。</summary>
    internal bool IsRecording => _hotkeys?.IsRecording == true;
    internal IEnumerator BuildShell(UIPage parent)
    {
        _parent = parent;
        var background = OverviewUi.Panel("Backdrop", transform, ModConfigLook.Surface(new Color(.05f, .07f, .07f, .84f))); OverviewUi.Fill(background);
        var border = OverviewUi.Panel("Frame", transform, OverviewUi.Gold); OverviewUi.Fill(border, 14);
        _frame = OverviewUi.Panel("Paper", border, OverviewUi.Brown); OverviewUi.Fill(_frame, 2);
        _back = OverviewUi.Button(_frame, Loc.T("Back", "返回", "返回"), Back, 85, 35, role: UiActionRole.Neutral); OverviewUi.Place((RectTransform)_back.transform, 14, 15, 85, 35);
        _listTab = Tab(Loc.T("Mod list", "模组列表", "模組列表"), () => { SwitchTab(PageTab.Mods); FocusTabContent(); });
        _hotkeyTab = Tab(Loc.T("Keybindings", "快捷键设置", "快捷鍵設定"), () => { SwitchTab(PageTab.Hotkeys); FocusTabContent(); });
        _settingsTab = Tab(SettingsTabLabel, OpenSettingsTab); // 上线前修正：样式同页签，按下跳到 ModConfig 的模组设置页
        _controls = OverviewUi.Rect("CommonTools", _frame);
        yield return null;
        // 第 7 章：共用工具行（照 IGMM BuildShell 的顺序建，布局在 LayoutToolbar）。
        _refresh = OverviewUi.Button(_controls, Loc.T("Refresh", "刷新", "重新整理"), RefreshData, 90, 34, role: UiActionRole.Neutral);
        _edit = OverviewUi.IconButton(_controls, "pencil", ToggleAuthor); // 第 8b 章
        _hide = OverviewUi.Button(_controls, Loc.T("Hide disabled", "隐藏已停用", "隱藏已停用"), () => { if (_repo == null) return; _list?.SetHideDisabled(!_repo.State.hideDisabled); RefreshLayout(); });
        yield return null;
        _language = OverviewUi.Button(_controls, Loc.T("Language", "语言", "語言"), PickLanguage, 95, 34, role: UiActionRole.Edit);
        // 第 8b 章：IGMM 这里是「管理中心」（分组页嵌在里面）；MCE 不搬管理中心，直接开分组编辑窗口（OverviewGroupEditor.Show）。
        _groups = OverviewUi.Button(_controls, Loc.T("Group management", "分组管理", "分組管理"), () => OpenGroupEditor(null), 115, 34, role: UiActionRole.Edit);
        yield return null;
        _logs = OverviewUi.Button(_controls, Loc.T("Logs", "日志", "日誌"), () => { if (_repo != null) LogWindow.Open(_repo, _mods); }, 76, 34, role: UiActionRole.Edit); // 第 8b 章（第 10 章的日志窗口）
        _theme = OverviewUi.IconButton(_controls, "theme", ToggleTheme); OverviewUi.UseSpriteIcon(_theme, ModConfigSidebarWidgets.ThemeIconSprite());
        _more = OverviewUi.Button(_controls, Loc.T("More", "更多", "更多"), ShowToolbarMore, 76, 34, role: UiActionRole.Neutral);
        yield return null;
        _body = OverviewUi.Rect("Content", _frame); OverviewUi.Fill(_body); _body.offsetMin = new Vector2(20, 64); _body.offsetMax = new Vector2(-20, -BodyTop);
        _status = OverviewUi.Text(_frame, "", 17); _status.color = OverviewUi.Muted;
        _status.textWrappingMode = TextWrappingModes.Normal; _status.overflowMode = TextOverflowModes.Ellipsis;
        _status.enableAutoSizing = true; _status.fontSizeMin = 12; _status.fontSizeMax = 17;
        _status.richText = false; _status.raycastTarget = true;
        AttachToolHint(_status.gameObject, () => _fullStatus);
        yield return null;
        ShellReady = true;
    }
    /// <summary>
    /// 第 7 章：内容区顶边。IGMM 是 140（工具行下面还有一行方案状态）；MCE 没有方案，工具行（y 68，高 34）下留 8 即可。
    /// </summary>
    private const float BodyTop = 110;
    /// <summary>2.4.1：工具行主题钮。切换后概览要按新主题重建：先回到上一层再重新打开（见 OverviewEntry 的 Epoch 判断）。</summary>
    private void ToggleTheme()
    {
        ModConfigSidebarWidgets.ToggleTheme();
        SetStatus(ModConfigLook.IsDark ? Loc.T("Purple theme. Reopen the overview to apply it here.", "已切到紫色主题，重新打开概览后此页生效。", "已切到紫色主題，重新開啟概覽後此頁生效。")
            : Loc.T("Blue theme. Reopen the overview to apply it here.", "已切到蓝色主题，重新打开概览后此页生效。", "已切到藍色主題，重新開啟概覽後此頁生效。"));
    }
    internal IEnumerator BindSteps()
    {
        if (_bound) yield break;
        if (!ShellReady) throw new InvalidOperationException("Overview shell is not ready.");
        _bindingStarted = true;
        // 1.1.0（概览页第 6 章）：仓库（overview.json）与模组清单由外壳持有，各页签共用（IGMM 快捷键页也是 () => _list.Mods）。
        // 扫描放在分阶段打开的「绑定内容」这一步，各占一帧，避免和建界面挤在同一帧。
        _repo = new OverviewRepository();
        yield return null;
        RescanMods();
        yield return null;
        // 第 7 章：模组列表（构造期不读数据，进页时 PrepareCachedPage 统一 Reload 一次，同 IGMM）。
        _list = new OverviewListView(_body, _repo, () => _mods, SetStatus, OnListChanged, OpenModSettings);
        _list.SettingsOwners = () => SettingsOwnersNow();
        // 第 8b 章：列表的分组标题菜单 / 双击、「未分组」双击都开外壳的分组编辑窗口（IGMM 开管理中心的分组页）。
        _list.GroupManagementRequested = group => OpenGroupEditor(group);
        _list.NewGroupRequested = parent => OpenGroupEditor(null, parent);
        _list.SetAuthorMode(_author);
        _list.Root.SetActive(_tab == PageTab.Mods);
        yield return null;
        _hotkeys = CreateHotkeysContent(_body);
        // 第 8b 章（第 8a 章留的接点）：快捷键页与列表共用外壳的仓库与分组编辑窗口（IGMM：_hotkeys.ManageGroups = () => _list.ManageGroups()）。
        _hotkeys.GroupRepository = () => _repo;
        _hotkeys.ManageGroups = () => OpenGroupEditor(null);
        _hotkeys.EditGroup = group => OpenGroupEditor(group);
        _hotkeys.Root.SetActive(_tab == PageTab.Hotkeys);
        var hideTip = _hide.gameObject.AddComponent<OverviewHover>();
        hideTip.Enter = () => _list?.ShowExternalHint((_repo.State.hideDisabled ? Loc.T("On · ", "已开启 · ", "已開啟 · ") : Loc.T("Off · ", "已关闭 · ", "已關閉 · ")) + Loc.T("Hide disabled mods; loaded mods remain visible.", "隐藏已停用的模组；本次已加载的模组继续显示。", "隱藏已停用的模組；本次已載入的模組繼續顯示。"));
        hideTip.Exit = () => _list?.HideExternalHint();
        AttachToolHint(_refresh.gameObject, () => Loc.T("Refresh installed mods and configuration values.", "重新读取已安装模组和配置值。", "重新讀取已安裝模組和設定值。"));
        AttachToolHint(_language.gameObject, () => Loc.T("Choose the language file for mod names and descriptions.", "选择模组名称与简介用的语言文件。", "選擇模組名稱與簡介用的語言檔案。"));
        AttachToolHint(_theme.gameObject, () => Loc.T("Colour theme", "配色主题", "配色主題"));
        // 第 8b 章：铅笔照 IGMM 用 OverviewHover（显示开 / 关），文字改成 MCE 实际管的事（Q5：只管拖动）。
        var editTip = _edit.gameObject.AddComponent<OverviewHover>();
        editTip.Enter = () => _list?.ShowExternalHint((_author ? Loc.T("On · ", "已开启 · ", "已開啟 · ") : Loc.T("Off · ", "已关闭 · ", "已關閉 · "))
            + Loc.T("Edit mode: drag mods to reorder them or move them between groups, and drag a group heading onto another to nest it. Names and descriptions can be edited by double-clicking at any time.",
                "编辑模式：拖动模组排序或换组，把分组标题拖到另一个分组标题上可挂到它下面。名称和简介随时双击即可修改。",
                "編輯模式：拖動模組排序或換組，把分組標題拖到另一個分組標題上可掛到它下面。名稱和簡介隨時雙擊即可修改。"));
        editTip.Exit = () => _list?.HideExternalHint();
        AttachToolHint(_groups.gameObject, () => Loc.T("Create, rename, colour, nest and delete display groups. Only overview.json is changed; no mod files are moved.",
            "新建、改名、设置颜色、嵌套或删除显示用分组。只改 overview.json，不移动任何模组文件。", "新增、改名、設定顏色、巢狀或刪除顯示用分組。只改 overview.json，不移動任何模組檔案。"));
        yield return null;
        RefreshLayout();
        gameObject.AddComponent<TypingGuard.Driver>(); _bound = true;
    }

    // ── 1.1.0（概览页第 6 章）：数据底座 ───────────────────────────────
    private OverviewRepository _repo = null!;
    private List<OverviewMod> _mods = new();
    private int _modsVersion;
    /// <summary>个人状态（分组、折叠、隐藏已停用），第 7、8 章的列表 / 分组用。</summary>
    internal OverviewRepository Repository => _repo;
    /// <summary>全部模组（<see cref="OverviewCatalogue.Scan"/> 的结果：含未加载、被停用、资源包、嵌套、重复副本）。</summary>
    internal IReadOnlyList<OverviewMod> Mods => _mods;
    /// <summary>重新扫描 plugins/（只读）。失败时退回「本次已加载的插件」，保证快捷键页仍可用，并写状态行。「刷新」也走这里。</summary>
    internal void RescanMods()
    {
        try { _mods = new OverviewCatalogue().Scan(_repo); }
        catch (Exception ex)
        {
            Plugin.Log.LogError("[概览] 扫描模组清单失败，改为只列本次已加载的插件：" + ex);
            _mods = LoadedOnly();
            SetStatus(Loc.T("Could not read the mod folder; only loaded mods are listed. See the log.", "读取模组文件夹失败，只列出本次已加载的模组，详见日志。", "讀取模組資料夾失敗，只列出本次已載入的模組，詳見日誌。"));
        }
        _modsVersion++;
    }
    /// <summary>扫描失败时的退路：只取 Chainloader 里本次已加载的插件（等同 1.1.0 快捷键页原来的 OverviewMod.LoadedPlugins）。</summary>
    private List<OverviewMod> LoadedOnly()
    {
        var rows = new List<OverviewMod>();
        foreach (var plugin in Chainloader.PluginInfos.Values)
        {
            if (plugin?.Metadata == null) continue;
            string raw = plugin.Metadata.Name ?? plugin.Metadata.GUID;
            rows.Add(new OverviewMod
            {
                Id = plugin.Metadata.GUID, Guid = plugin.Metadata.GUID, Name = raw, RawName = raw, SettingDisplayName = Plugin.FixNaming(raw),
                PluginVersion = plugin.Metadata.Version?.ToString() ?? "", Location = plugin.Location ?? "", Loaded = true,
                PluginGuids = { plugin.Metadata.GUID }, Rule = _repo.GetRule(plugin.Metadata.GUID),
            });
        }
        return rows;
    }
    /// <summary>
    /// 快捷键页的模组清单：从共用清单里只取本次已加载、实例在跑的插件（不含本模组自己），按显示名排序。
    /// 与改用扫描结果前（OverviewMod.LoadedPlugins）同一集合、同一顺序；清单重扫或语言变了（显示名排序会变）才重算。
    /// </summary>
    private List<OverviewMod> HotkeyMods(ref List<OverviewMod>? cache, ref int version, ref int revision)
    {
        if (cache != null && version == _modsVersion && revision == Loc.TranslationRevision) return cache;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        cache = _mods.Where(m => m.Loaded && m.Guid != Plugin.PluginGuid && Chainloader.PluginInfos.TryGetValue(m.Guid, out var info) && info?.Instance != null && seen.Add(m.Guid))
            .OrderBy(m => OverviewLanguageBridge.GetDisplayName(m), StringComparer.CurrentCultureIgnoreCase).ToList();
        version = _modsVersion; revision = Loc.TranslationRevision;
        return cache;
    }
    /// <summary>
    /// 第 4 章：建快捷键页。模组清单取共用清单里已加载的插件（<see cref="HotkeyMods"/>）；状态行、齿轮（打开该模组的设置页）由外壳提供。
    /// 第 7 章：快捷键页工具行按 ↑ 去共用工具行里水平最近的按钮（同 IGMM），没有时回页签。
    /// IGMM 还给快捷键页传依赖悬停卡与序号回调（_list.BindDependencyHint / GetDisplayNumber）；MCE 的快捷键页沿用 1.1.0 已定的版面，
    /// 不加这两项（第 11 章定，见规划进度）。
    /// </summary>
    private OverviewHotkeyView CreateHotkeysContent(RectTransform body)
    {
        List<OverviewMod>? mods = null; int modsVersion = -1, modsRevision = -1;
        IReadOnlyList<OverviewMod> Mods() => HotkeyMods(ref mods, ref modsVersion, ref modsRevision);
        // 齿轮只看这一行自己的 GUID：快捷键页每行是一个插件，同包兄弟插件有设置项不算（与改用扫描结果前一致）。
        bool CanOpenOwnSettings(OverviewMod mod) => SettingsOwnersNow().Contains(mod.Guid);
        return new OverviewHotkeyView(body, Mods, SetStatus, OpenModSettings, CanOpenOwnSettings) { ToolbarAbove = ToolbarAbove };
    }
    private Selectable ToolbarAbove(Selectable from)
        => _controls.GetComponentsInChildren<Selectable>(false).Where(s => s.IsActive() && s.IsInteractable()).OrderBy(s => Mathf.Abs(s.transform.position.x - from.transform.position.x)).FirstOrDefault() ?? TabAbove(from);
    private RectTransform Tab(string label, Action click)
    {
        var rect = OverviewUi.Rect("Tab", _frame); var shape = rect.gameObject.AddComponent<OverviewTabGraphic>(); shape.color = ModConfigLook.Surface(new Color(.29f, .25f, .17f));
        shape.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = shape; button.onClick.AddListener(() => click());
        var text = OverviewUi.Text(rect, label, 20); OverviewUi.Fill(text.rectTransform); text.margin = new Vector4(6, 0, 6, 0); text.alignment = TextAlignmentOptions.Midline;
        text.enableAutoSizing = true; text.fontSizeMax = 20; text.fontSizeMin = 12; text.overflowMode = TextOverflowModes.Overflow; text.raycastTarget = false;
        GamepadSupport.MarkSelectable(rect.gameObject, null, null); // 实测修正（2026-11-16）：RT+LB/RB 在页签间移焦点时要看得到焦点框
        return rect;
    }
    private RectTransform TabRect(PageTab tab) => tab == PageTab.Mods ? _listTab : _hotkeyTab;
    /// <summary>当前页签（工具行之上）。</summary>
    internal Selectable TabAbove(Selectable from) => TabRect(_tab).GetComponent<Button>();
    // First entry's styling, run while the page is still inactive. OnPageEnter skips what already ran.
    internal IEnumerator EnterSteps()
    {
        ModConfigPaperStyle.Resolve(_parent.transform);
        yield return null;
        OverviewPaperPanel.Refresh(transform);
        yield return null;
        PrepareCachedPage();
        _entryPrepared = true;
    }

    // ── 第 7 章：进页时按需重读（照抄 IGMM OverviewPage.Caching 后裁剪：没有方案草稿、整合包、来源记录）──
    private bool _listLoaded, _hotkeysStale = true;
    private int _inventoryVersion = -1, _contentVersion = -1, _listLanguage = -1;
    private void PrepareCachedPage()
    {
        if (_repo == null) return;
        OverviewPageCache.OnPageEnter();
        int inventory = OverviewPageCache.Inventory, content = OverviewPageCache.Content;
        bool changed = _repo.IsStale || _listLoaded && (!OverviewPageCache.Reliable || inventory != _inventoryVersion || content != _contentVersion);
        if (changed)
        {
            // 外部改了 plugins/ 或 overview.json（含另一份仓库实例保存过）：重读仓库、重扫清单。
            OverviewCatalogue.InvalidateCaches();
            _repo.Reload(); RescanMods(); _listLoaded = false; _hotkeysStale = true;
        }
        if (!_listLoaded) { _list?.Reload(); _listLoaded = true; _hotkeysStale = true; _listLanguage = Loc.TranslationRevision; }
        else if (_listLanguage != Loc.TranslationRevision) { _list?.Redraw(); _hotkeysStale = true; _listLanguage = Loc.TranslationRevision; }
        _inventoryVersion = inventory; _contentVersion = content;
        EnsureSelectedPage();
    }
    private void EnsureSelectedPage()
    {
        if (_tab == PageTab.Hotkeys && _hotkeysStale && _hotkeys != null)
        {
            try { _hotkeys.Reload(); _hotkeysStale = false; }
            catch (Exception ex) { Plugin.Log.LogError("[概览] 快捷键页载入失败：" + ex); }
        }
    }
    /// <summary>第 7 章「刷新」钮（照 IGMM RefreshData）：清元数据缓存、重读仓库、重扫清单，列表重建，快捷键页标记重读。</summary>
    private void RefreshData()
    {
        if (_repo == null) return;
        if (IsRecording) _hotkeys?.TryCloseLayer();
        OverviewCatalogue.InvalidateCaches(); OverviewPageCache.Invalidate();
        _repo.Reload(); RescanMods();
        _list?.Reload(); _listLoaded = true; _listLanguage = Loc.TranslationRevision; _hotkeysStale = true;
        EnsureSelectedPage();
        _inventoryVersion = OverviewPageCache.Inventory; _contentVersion = OverviewPageCache.Content;
        RefreshLayout();
    }
    /// <summary>列表改了显示相关的东西（隐藏已停用、改名、分组）：重排工具行，快捷键页下次显示时重读。</summary>
    private void OnListChanged() { RefreshLayout(); _hotkeysStale = true; if (_tab == PageTab.Hotkeys && !IsRecording) EnsureSelectedPage(); }

    // ── 第 8b 章：分组编辑窗口、铅笔、智能分组的外壳接点 ─────────────────────
    /// <summary>
    /// 打开分组编辑窗口（第 8a 章 <see cref="OverviewGroupEditor"/> 的弹窗模式）。<paramref name="group"/> 为 null = 分组管理（新建表单，
    /// 窗口里可切到已有分组）；<paramref name="newParent"/> 非 null = 在该分组下新建子分组（空串 = 顶层）。
    /// 每次新建编辑器：它在构造时取模组清单，外壳重扫后清单是新对象。IGMM 这里开管理中心的分组页（OpenManagement(ManagementTab.Groups)）。
    /// </summary>
    private void OpenGroupEditor(OverviewGroup? group, string? newParent = null)
    {
        if (_repo == null) return;
        if (IsRecording) _hotkeys?.TryCloseLayer();
        ClearToolHint(); _list?.HideAllHints();
        _groupEditor?.Close();
        _groupEditorOpener = _tab == PageTab.Mods ? EventSystem.current?.currentSelectedGameObject : null; // 快捷键页自己还焦点
        _groupEditorReturn = _tab == PageTab.Mods;
        _groupEditor = new OverviewGroupEditor(_repo, _mods, SetStatus, OnGroupsSaved);
        if (newParent != null) { _groupEditor.NewGroupParent = newParent; group = null; }
        _groupEditor.Show(group);
    }
    private GameObject? _groupEditorOpener;
    private bool _groupEditorReturn;
    /// <summary>1.1.0（MCE，第 11 章）：分组编辑窗口关掉后，手柄焦点还给打开它的按钮；那个按钮已经不在（菜单项、重排后的行）时回到列表。</summary>
    private void ReturnFocusAfterGroupEditor()
    {
        if (!_groupEditorReturn || _groupEditor?.IsOpen == true || OverviewUi.HasModal) return;
        _groupEditorReturn = false; var opener = _groupEditorOpener; _groupEditorOpener = null;
        if (!GamepadSupport.IsGamepad || _tab != PageTab.Mods || EventSystem.current == null) return;
        if (opener != null && opener.activeInHierarchy) EventSystem.current.SetSelectedGameObject(opener);
        else _list?.FocusListForGamepad();
    }
    /// <summary>分组编辑窗口保存后（同 IGMM 的 <c>_list?.Reload(); RefreshHeader();</c>）：列表重读，快捷键页标记重读。</summary>
    private void OnGroupsSaved() { _list?.Reload(); OnListChanged(); }
    /// <summary>第 8a 章 OverviewPage.SmartGrouping 的分部方法：智能分组写盘后列表重读（IGMM <c>_list?.Reload()</c>）。</summary>
    partial void OnSmartGroupingApplied() => OnGroupsSaved();
    /// <summary>
    /// 第 8b 章：首次打开模组列表时提示智能分组（IGMM 在 OnPageEnter 末尾无条件调用；MCE 从设置页表头进快捷键页时不弹，
    /// 等切到模组列表再弹）。每个页面实例只检查一次（<c>_smartGroupingChecked</c>），已有分组或选过「不用了」不弹。
    /// </summary>
    private void OfferSmartGroupingOnList()
    {
        if (!_bound || _repo == null || _tab != PageTab.Mods || IsRecording || OverviewUi.HasModal) return;
        MaybeOfferSmartGrouping();
    }
    /// <summary>铅笔（照 IGMM ToggleAuthor 裁剪：没有作者模式 / 整合包 / 介绍页）。按 Q5 只管列表的拖动排序与换组。</summary>
    private void ToggleAuthor()
    {
        _author = !_author; _list?.SetAuthorMode(_author);
        SetStatus(_author ? Loc.T("Edit mode: drag mods to reorder them or move them between groups; drag a group heading onto another to nest it. Shift selects a range.",
                "编辑模式：拖动模组可排序或换组，把分组标题拖到另一个分组标题上可挂到它下面；Shift 可批量选择。",
                "編輯模式：拖動模組可排序或換組，把分組標題拖到另一個分組標題上可掛到它下面；Shift 可批量選取。") : "");
        RefreshLayout();
    }
    private void PickLanguage()
    {
        var options = new List<(string Label, string Value)> { (Loc.Get("LANG_FOLLOW"), "") };
        foreach (var file in OverviewLanguageBridge.LanguageOptions) options.Add((file.DisplayOrStem, file.Key));
        OverviewUi.Dropdown(ToolAnchor(_language), Loc.T("Language file", "选择语言文件", "選擇語言檔案"), options, OverviewLanguageBridge.Selection, value => {
            if (!OverviewLanguageBridge.Select(value, out string error)) { SetStatus(error); return; }
            _list?.Redraw(); _listLanguage = Loc.TranslationRevision; _hotkeysStale = true; EnsureSelectedPage(); RefreshLayout(); SetStatus(OverviewLanguageBridge.CurrentDisplayName);
        });
    }
    private void AttachToolHint(GameObject target, Func<string> text)
    {
        var hover = target.AddComponent<OverviewHover>();
        hover.Enter = () => { _activeToolHintTarget = target; _activeToolHintText = text; _list?.QueueExternalHint(target,text); };
        hover.Exit = () => { if (_activeToolHintTarget != target) return; ClearToolHint(); _list?.HideExternalHint(); };
        var focus = target.AddComponent<OverviewHotkeyFocus>();
        focus.Selected = () => { _activeToolHintTarget=target;_activeToolHintText=text;_list?.QueueExternalHint(target,text,true); }; focus.Deselected = hover.Exit;
    }
    public override void OnPageEnter()
    {
        base.OnPageEnter(); OverviewUi.ModalParent = transform;
        bool returning = _returnFromSettings; _returnFromSettings = false;
        if (_entryPrepared) _entryPrepared = false;
        else { ModConfigPaperStyle.Resolve(_parent.transform); OverviewPaperPanel.Refresh(transform); PrepareCachedPage(); }
        RefreshLayout();
        if (returning) _list?.RestoreScrollPosition(_settingsScrollPosition);
        if (_tab == PageTab.Mods) _list?.OnOpened(!returning);
        if (_tab == PageTab.Hotkeys) OpenHotkeysContent();
        RestoreFocus(returning);
        OfferSmartGroupingOnList(); // 第 8b 章（IGMM 同位置：OnPageEnter 末尾）
    }
    /// <summary>
    /// 第 5 章（二次审查）：内容载入（读所有模组的快捷键并重绘）出错时只写状态行，不让异常冒到切页 / 打开流程——
    /// 首次打开时那会被当成「打开失败」把已是当前页的概览销毁。
    /// </summary>
    private void OpenHotkeysContent()
    {
        if (_hotkeys == null) return;
        try { EnsureSelectedPage(); _hotkeys.OnOpened(); }
        catch (Exception ex)
        {
            Plugin.Log.LogError("[概览] 快捷键页载入失败：" + ex);
            SetStatus(Loc.T("The keybindings page failed to load; see the log.", "快捷键页载入失败，详见日志。", "快捷鍵頁載入失敗，詳見日誌。"));
        }
    }
    public override void OnPageExit()
    {
        GamepadIntent.Release(this); ReleaseMoveRepeat(); ClearToolHint(); _groupEditor?.Close(); _groupEditor = null; _groupEditorReturn = false; _groupEditorOpener = null; _hotkeys?.OnClosed(); _list?.OnClosed(); OverviewUi.ModalParent = null; OverviewUi.CloseModal(); TypingGuard.Active = false; base.OnPageExit();
    }
    public (UIPage, PageTransistion) GetParentPage()
    {
        // 1.1.4: recording a key is a lock; only a 1 s B hold leaves it (handled by the keybindings page).
        if (IsRecording) return (this, GamepadSupport.NoopTransistion.Shared);
        if (GamepadSupport.ConsumeTypingBack()) return (this, GamepadSupport.NoopTransistion.Shared); // B only left a text box
        if (TryCloseLayer()) return (this, GamepadSupport.NoopTransistion.Shared);
        if (!BackGuard.Allow()) return (this, GamepadSupport.NoopTransistion.Shared); // 1.1.5: B twice quickly to leave
        return (BackTarget, new SetActivePageTransistion());
    }
    /// <summary>第 5 章（用户 2026-11-15）：从模组设置页表头跳来时离开回到那一页；否则回游戏设置页（入口所在页）。</summary>
    private UIPage? _returnTo;
    internal void SetReturnPage(UIPage? page)
    {
        _returnTo = page;
        // 回模组设置页时不要让游戏设置页下次显示时把焦点拉到「模组概览」入口上。
        if (page != null) EntryNavLink.ReturnFocus = false;
    }
    private UIPage BackTarget => _returnTo != null && _returnTo.GetComponentInParent<UIPageHandler>(true) == _parent.GetComponentInParent<UIPageHandler>(true) ? _returnTo : _parent;
    /// <summary>齿轮跳到模组设置页之前调用（<see cref="OverviewSettingsNavigator"/>）：回来时把手柄焦点与列表滚动位置放回原处。</summary>
    internal void MarkReturnFromSettings()
    {
        _returnFromSettings = true; _settingsScrollPosition = _list?.ScrollPosition ?? 1;
        _selectionBeforeSettings = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
    }
    internal void ShowHotkeysTab() => SwitchTab(PageTab.Hotkeys);
    private static string SettingsTabLabel => Loc.T("Mod Settings", "模组设置", "模組設定");
    /// <summary>
    /// 上线前修正（用户 2026-11-16）：「模组设置」页签 = 设置页表头「跳转到快捷键设置页」（<see cref="OverviewController.OpenHotkeys"/>）的反向路径。
    /// 从那颗按钮进来的（离开时本来就回模组设置页）直接退回那一页，设置页保持原样；否则像齿轮一样打开模组设置页、不指定模组，
    /// 在设置页按 B 回到这里。录键锁定中不响应；失败时写状态行（同齿轮）。
    /// </summary>
    private void OpenSettingsTab()
    {
        if (IsRecording) return;
        ClearToolHint(); _list?.HideAllHints();
        // 实测修正（用户 2026-11-16）：两条路进设置页后，焦点都停在表头「跳转到快捷键设置页」（OverviewSettingsNavigator.FocusJumpHotkeys）。
        if (_returnTo != null && ReferenceEquals(BackTarget, _returnTo)) { var page = _returnTo; NavigateBack(); OverviewSettingsNavigator.FocusJumpHotkeys(page); return; }
        OpenModSettings(null);
    }
    /// <summary>打开某个模组的设置页（列表 / 快捷键行的齿轮）；null = 打开设置页、不指定模组。失败时写状态行。</summary>
    internal void OpenModSettings(OverviewMod? mod)
    {
        if (OverviewSettingsNavigator.Open(this, mod, out string error)) return;
        _returnFromSettings = false; _selectionBeforeSettings = null; SetStatus(error);
    }
    private HashSet<string>? _owners;
    private int _ownersFrame = -1;
    /// <summary>该模组当前是否向 ModConfig 提供了可显示的设置项（齿轮可用）。同一帧内复用一次查询，逐行调用也便宜。</summary>
    internal bool CanOpenSettings(OverviewMod mod)
    {
        var owners = SettingsOwnersNow();
        return owners.Contains(mod.Guid) || mod.PluginGuids.Exists(owners.Contains);
    }
    private HashSet<string> SettingsOwnersNow()
    {
        if (_owners == null || _ownersFrame != Time.frameCount) { _owners = OverviewSettingsNavigator.SettingsOwners(this); _ownersFrame = Time.frameCount; }
        return _owners;
    }
    private void Back()
    {
        if (IsRecording) return; // recording lock
        if (GamepadSupport.ConsumeTypingBack()) return; // B only left a text box
        if (TryCloseLayer()) return;
        if (!BackGuard.Allow()) return;
        NavigateBack();
    }
    private bool TryCloseLayer()
    {
        if (OverviewHotkeyCapture.ConsumedCancelFrame == Time.frameCount) return true;
        if (OverviewUi.TryCloseTopModal()) return true;
        // 第 8b 章：分组编辑窗口不是 OverviewUi 的公共弹窗（第 8a 章说明）；有未保存修改时先问，放弃确认框是 OverviewUi 的层，上一行先关它。
        if (_groupEditor?.TryCloseTop() == true) return true;
        if (_hotkeys?.TryCloseLayer() == true) return true;
        return _list?.TryCloseLayer() == true;
    }
    private void NavigateBack()
    {
        var target = BackTarget;
        var handler = _parent.GetComponentInParent<UIPageHandler>(); if (handler != null) handler.TransistionToPage(target, new SetActivePageTransistion());
    }
    private void SwitchTab(PageTab tab)
    {
        if (tab == _tab) return;
        if (_tab == PageTab.Mods) { _list?.OnTabHidden(); _listCollapsed = CollapsedKey(); }
        if (_tab == PageTab.Hotkeys) _hotkeys?.OnClosed();
        _list?.HideAllHints(); ClearToolHint();
        _tab = tab;
        if (_list != null) _list.Root.SetActive(tab == PageTab.Mods);
        if (_hotkeys != null) { _hotkeys.Root.SetActive(tab == PageTab.Hotkeys); if (tab == PageTab.Hotkeys) OpenHotkeysContent(); }
        RefreshLayout();
        ModConfigPaperStyle.Resolve(_parent.transform); OverviewPaperPanel.Refresh(transform);
        if (tab == PageTab.Mods)
        {
            _list?.OnOpened();
            // 第 11 章：快捷键页与列表共用分组折叠；在快捷键页折叠 / 展开过分组，切回来时列表当场重画（列表 OnOpened 不重画）。
            if (_listLoaded && CollapsedKey() != _listCollapsed) _list?.Redraw(keepRelated: true);
            OfferSmartGroupingOnList(); // 第 8b 章：从快捷键页进来的，第一次切到列表时提示
        }
    }
    private string _listCollapsed = "";
    private string CollapsedKey() => _repo == null ? "" : string.Join("\n", _repo.State.collapsedGroups.OrderBy(id => id, StringComparer.Ordinal));
    /// <summary>手柄进入页面：从设置页回来放回齿轮；焦点已在本页里就不动；否则进内容（内容为空时停在页签上）。</summary>
    private void RestoreFocus(bool returning)
    {
        var remembered = _selectionBeforeSettings; _selectionBeforeSettings = null;
        var system = EventSystem.current;
        if (!GamepadSupport.IsGamepad || system == null) return;
        if (returning && remembered != null && remembered.activeInHierarchy) { system.SetSelectedGameObject(remembered); return; }
        var current = system.currentSelectedGameObject;
        if (current != null && current.activeInHierarchy && current.transform.IsChildOf(transform)) return;
        FocusTabContent();
    }
    private void RefreshLayout()
    {
        if (_frame == null || _body == null || _status == null) return;
        _width = _frame.rect.width; _languageRevision = Loc.TranslationRevision;
        float width = Mathf.Max(_width, 380);
        _back.GetComponentInChildren<TextMeshProUGUI>(true).text = Loc.T("Back", "返回", "返回");
        _listTab.GetComponentInChildren<TextMeshProUGUI>(true).text = Loc.T("Mod list", "模组列表", "模組列表");
        _hotkeyTab.GetComponentInChildren<TextMeshProUGUI>(true).text = Loc.T("Keybindings", "快捷键设置", "快捷鍵設定");
        _settingsTab.GetComponentInChildren<TextMeshProUGUI>(true).text = SettingsTabLabel;
        const float tabsWidth = 390; // 所有页签的设计宽度之和（模组列表 120 + 快捷键设置 140 + 模组设置 130）；加页签时同步
        float tabX = 112;
        float tabScale = Mathf.Min(1, Mathf.Max(.25f, (width - 112 - 38) / tabsWidth));
        void PlaceTab(RectTransform tab, PageTab? kind, float tabWidth)
        {
            tabWidth *= tabScale;
            bool active = kind == _tab; OverviewUi.Place(tab, tabX, active ? 12 : 16, tabWidth, active ? 38 : 32); tabX += tabWidth - 1;
            tab.GetComponent<OverviewTabGraphic>().color = active ? ModConfigLook.Surface(new Color(.20f, .48f, .60f)) : UiActionStyle.ColorFor(UiActionRole.Neutral, ModConfigLook.IsDark);
            var caption = tab.GetComponentInChildren<TextMeshProUGUI>(true); caption.overflowMode = TextOverflowModes.Overflow;
        }
        PlaceTab(_listTab, PageTab.Mods, 120); PlaceTab(_hotkeyTab, PageTab.Hotkeys, 140);
        PlaceTab(_settingsTab, null, 130); // 「模组设置」不是本页的内容，永远画成未选中的页签
        OverviewUi.Place(_controls, 20, 68, width - 40, 36);
        if (_repo != null) LayoutToolbar(width);
        _body.offsetMin = new Vector2(20, 64); _body.offsetMax = new Vector2(-20, -BodyTop);
        OverviewUi.Place(_status.rectTransform, 20, _frame.rect.height - 58, width - 40, _promptShown ? 28 : 52); // 手柄提示栏占底部一行
    }
    private string _fullStatus = "";
    /// <summary>状态行（列表 / 快捷键页的 status 回调）。单行显示，过长省略；悬停看全文。</summary>
    internal void SetStatus(string text)
    {
        _fullStatus = text ?? "";
        if (_status != null) _status.text = OverviewListView.SingleLine(_fullStatus);
        if (_activeToolHintTarget == _status?.gameObject) RefreshActiveToolHint();
    }
    private void Update()
    {
        if (_frame == null) return;
        if (Math.Abs(_width - _frame.rect.width) > 1 || _languageRevision != Loc.TranslationRevision) RefreshLayout();
        GamepadPageTick(); // 提示栏 + 弹窗滚动（OverviewPage.Gamepad.cs）
        ReturnFocusAfterGroupEditor();
    }
    private bool _resourcesDisposed;
    internal void DisposeResources()
    {
        if (_resourcesDisposed) return; _resourcesDisposed = true;
        AnimatorRestGate.DetachTree(transform);
        if (!_bindingStarted) return;
        void Release(Action action) { try { action(); } catch (Exception error) { Plugin.Log.LogWarning("[概览] 清理：" + error.Message); } }
        OverviewSettingsNavigator.ClearOrigin(this);
        Release(() => _groupEditor?.Close()); Release(() => _hotkeys?.Dispose()); Release(() => _list?.Dispose());
        if (OverviewUi.ModalParent == transform) { OverviewUi.CloseModal(); OverviewUi.ModalParent = null; }
    }
    private void OnDestroy() => DisposeResources();
}
