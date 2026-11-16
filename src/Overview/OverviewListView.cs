#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ModConfigEnhance.Overview;

// 1.1.0（MCE，概览页第 7 章）：照抄 IGMM Overview/OverviewListView.cs 后裁剪（每处留「1.1.0（MCE）」注释）。
// 裁掉：更新检查 / 标签同步（ThunderstoreUpdateService，MCE 不联网）、来源标签与来源菜单、整合包冲突建议、
// 待保存 / 待重启角标（ProfilePending*）、变动账本小圆点（ChangeLedger）、「加入方案」+ 钮、编辑模式的更新许可列、启停草稿。
// 模组清单由外壳持有（OverviewPage.Mods，与快捷键页共用），本类 Reload 只取外壳的结果，不自己扫描。
// 分组编辑器（OverviewGroupEditor，第 8a 章）不在本类里建：ManageGroups / EditGroup / NewGroupIn 走外壳给的回调（第 8b 章已接：OverviewPage.OpenGroupEditor）。
// 启用勾只读（MCE 不启停任何模组）；双击名称 / 简介直接改译名 / 简介（OverviewModInfoDialog），不需要编辑模式。
internal sealed partial class OverviewListView : IDisposable
{
    private readonly OverviewRepository _repo;
    private readonly Func<IReadOnlyList<OverviewMod>> _source;
    private readonly Action<string> _status;
    private readonly Action _headersChanged;
    private readonly RectTransform _root, _header, _viewport, _content, _tools;
    private readonly ScrollRect _scroll;
    private readonly OverviewCascade _cascade;
    private readonly OverviewListPump _pump;
    private readonly List<OverviewMod> _mods = new();
    private readonly HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);
    private readonly RectTransform _hint;
    private readonly TextMeshProUGUI _hintText;
    private readonly HintLru<string,float> _hintMetrics=new(64,StringComparer.Ordinal);
    private readonly Dictionary<OverviewMod,int> _displayNumbers=new();
    private readonly Camera? _eventCamera;
    private Vector2 _lastHintPointer=new(float.NaN,float.NaN);
    private Vector2 _lastHintRootSize;
    private int _hintMetricRevision=-1;
    private TMP_FontAsset? _hintMetricFont;
    private float _hintMetricWidth=-1,_hintMetricSize=-1;
    private readonly Action<OverviewMod?>? _openSettings;
    private readonly OverviewDependencyHints _dependencyHints;
    private int _languageRevision = -1;
    private string _shownHint = "";
    private readonly HoverDwell _hintDwell=new();
    private readonly DeferredHint _factoryHint=new();
    private float _warmAfter, _warmFirstAt;
    private int _warmSettledFrames;
    private int _warmRow;
    private bool _warming, _arrowWarmed;
    private Vector2 _warmPointer;
    private int _warmInventory=-1,_warmContent=-1,_warmLanguage=-1;
    private void WarmVisibleHints()
    {
        if(_warming || !_opened || _residentPending || _hint.gameObject.activeSelf || OverviewUi.HasModal || _building || _dragged!=null || _scroll.velocity.sqrMagnitude>1 || _cascade.IsAnimating)return;
        var pointer=PointerPosition;
        if(pointer!=_warmPointer) { _warmPointer=pointer;_warmAfter=Time.unscaledTime+.25f;return; }
        if(Time.unscaledTime<_warmFirstAt || Time.unscaledTime<_warmAfter || Time.unscaledDeltaTime>.025f || _infrastructureLoading)return;
        if(++_warmSettledFrames<30)return;
        // 1.1.0（MCE）：没有来源识别，依赖图只来自本地扫描，不再比对 SourceUi.DisplayDependencyGraph。
        if(_warmInventory!=OverviewPageCache.Inventory || _warmContent!=OverviewPageCache.Content || _warmLanguage!=Loc.TranslationRevision)
        {
            _warmRow=0;_warmInventory=OverviewPageCache.Inventory;_warmContent=OverviewPageCache.Content;
            _warmLanguage=Loc.TranslationRevision;
        }
        _warmAfter=Time.unscaledTime+.05f;
        _warming=true;
        try
        {
            if(!_arrowWarmed) { _arrowWarmed=true;OverviewDependencyHints.TreeArrow();return; }
            if(_dependencyHints.HasWarmWork) { _dependencyHints.TickPrewarm();return; }
            // At most one live row's content is prepared in a frame. Cold native work
            // cannot be interrupted; diagnostics measure the real cost rather than promise a hard budget.
            if(_virtualRows.Count==0)return;
            for(;_warmRow<_virtualRows.Count;)
            {
                var row=_virtualRows[_warmRow++];
                if(row.View==null || !row.View.gameObject.activeInHierarchy)continue;
                if(_dependencyHints.Prewarm(row.View))break;
            }
        }
        finally { _warming=false; }
    }
    // 1.1.0（MCE）：没有方案草稿，去掉待保存 / 待重启角标的状态投影（ProfilePendingState / MarkerState）。
    private float _hintHeight, _hintWidth=-1, _hintFontSize=-1;
    private int _hintLanguage=-1;
    private TMP_FontAsset? _hintFont;
    private string _measuredHint="";
    private readonly RectTransform _dragGhost, _dropLine, _dropHighlight;
    private readonly TextMeshProUGUI _dragCaption;
    private readonly Dictionary<RectTransform, (string Group, OverviewMod? Before)> _dropAreas = new();
    private List<OverviewMod> _visible = new();
    private OverviewMod? _dragged;
    private int _anchor = -1;
    // 1.1.0（MCE）：_authorMode = 工具行铅笔开关（第 8b 章），按 Q5 只管拖动排序 / 换组与拖动分组标题挂接；
    // 改名、改简介随时双击，右键 / Y 菜单的「移动到分组」与分组菜单不受它限制。
    // 更新检查的状态（_checking / _round / 进度 / 整合包最新版）全部去掉。
    private bool _authorMode, _disposed, _building;
    private bool _opened;
    // 1.1.0（MCE，第 11 章审查）：三参数（繁体照抄 IGMM 词条表）；MCE 的 Loc.T 没有 IGMM 的内联译文层，两参数版繁体会显示简体。
    private static string T(string en, string zh, string tw) => Loc.T(en, zh, tw);
    private float _lastWidth;
    private const float RowHeight = 38;
    private const float GroupHeight = 32, GroupGapBefore = 4, GroupGapAfter = 2;
    public GameObject Root => _root.gameObject;
    public IReadOnlyList<OverviewMod> Mods => _mods;
    // 1.1.0（MCE）：没有标签筛选（不联网）。IGMM 的 RefreshTagFilter 改名为「按当前数据重画」：外壳换语言 / 改名后调用（退出关联视图）；
    // 快捷键页改了分组折叠后切回列表时 keepRelated = true（只重画）。
    internal void Redraw(bool keepRelated = false) { if (!keepRelated) _relatedMods = null; Render(); }
    internal float ScrollPosition => _scroll.verticalNormalizedPosition;
    internal void RestoreScrollPosition(float position) { _scroll.verticalNormalizedPosition = position; _pump.Enqueue(() => _scroll.verticalNormalizedPosition = position); }
    public bool IsEditingText
    {
        get
        {
            if (_root.GetComponentsInChildren<TMP_InputField>(false).Any(i => i.isFocused)) return true;
            var eventSystem = EventSystem.current; var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            var input = selected != null ? selected.GetComponent<TMP_InputField>() : null;
            return input != null && input.isFocused;
        }
    }

    /// <param name="source">1.1.0（MCE）：外壳的共用模组清单（OverviewPage.Mods）；Reload 只从这里取，不自己扫描。</param>
    public OverviewListView(Transform parent, OverviewRepository repo, Func<IReadOnlyList<OverviewMod>> source, Action<string> status, Action headersChanged, Action<OverviewMod?>? openSettings = null)
    {
        _repo = repo; _source = source; _status = status; _headersChanged = headersChanged; _openSettings = openSettings;
        var canvas=parent.GetComponentInParent<Canvas>()?.rootCanvas;
        _eventCamera=canvas!=null && canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
        _root = OverviewUi.Rect("OverviewModList", parent); OverviewUi.Fill(_root);
        _tools = OverviewUi.Rect("ListTools", _root);
        _header = OverviewUi.Panel("FixedHeader", _root, ModConfigLook.Surface(new Color(0.12f, 0.13f, 0.15f, .92f)));
        RectTransform scrollRoot = OverviewUi.Rect("ModScroll", _root);
        scrollRoot.anchorMin = Vector2.zero; scrollRoot.anchorMax = Vector2.one;
        scrollRoot.offsetMin = new Vector2(0, 0); scrollRoot.offsetMax = new Vector2(0, -72);
        _scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        _viewport = OverviewUi.Panel("Viewport", scrollRoot, ModConfigLook.Surface(new Color(0, 0, 0, 0.01f)));
        OverviewUi.Fill(_viewport); _viewport.offsetMax = new Vector2(-15, 0);
        _viewport.gameObject.AddComponent<RectMask2D>();
        _content = OverviewUi.Rect("Rows", _viewport);
        _content.anchorMin = new Vector2(0, 1); _content.anchorMax = new Vector2(1, 1); _content.pivot = new Vector2(0, 1);
        _content.anchoredPosition = Vector2.zero;
        _scroll.viewport = _viewport; _scroll.content = _content; _scroll.horizontal = false; _scroll.vertical = true;
        _scroll.movementType = ScrollRect.MovementType.Clamped; _scroll.scrollSensitivity = 40f;
        RectTransform bar = OverviewUi.Panel("Scrollbar", scrollRoot, ModConfigLook.Surface(new Color(.1f, .1f, .1f, .5f)));
        bar.anchorMin = new Vector2(1, 0); bar.anchorMax = Vector2.one; bar.pivot = new Vector2(1, 1); bar.offsetMin = new Vector2(-12, 0); bar.offsetMax = Vector2.zero;
        RectTransform handle = OverviewUi.Panel("Handle", bar, OverviewUi.Muted); OverviewUi.Fill(handle);
        var scrollbar = bar.gameObject.AddComponent<Scrollbar>(); scrollbar.handleRect = handle; scrollbar.targetGraphic = handle.GetComponent<Image>(); scrollbar.direction = Scrollbar.Direction.BottomToTop;
        _scroll.verticalScrollbar = scrollbar;
        _cascade = _root.gameObject.AddComponent<OverviewCascade>();
        _hint = OverviewUi.Panel("FullTextHint", parent, ModConfigLook.Surface(new Color(.08f, .08f, .1f, .98f)));
        HintCanvas.Attach(_hint,false);
        _hintText = OverviewUi.Text(_hint, "", 20); OverviewUi.Fill(_hintText.rectTransform, 12);
        _hintText.textWrappingMode = TextWrappingModes.Normal; _hintText.raycastTarget = false;
        _hint.GetComponent<Image>().raycastTarget = false; _hint.gameObject.SetActive(false);
        _dragGhost = OverviewUi.Panel("DragPreview", _root, ModConfigLook.Surface(new Color(.12f, .26f, .31f, .92f))); _dragGhost.GetComponent<Image>().raycastTarget = false;
        _dragCaption = OverviewUi.Text(_dragGhost, "", 20); OverviewUi.Fill(_dragCaption.rectTransform, 8); _dragGhost.gameObject.SetActive(false);
        _dropLine = OverviewUi.Panel("DropLine", _content, OverviewUi.Blue); _dropLine.GetComponent<Image>().raycastTarget = false; _dropLine.gameObject.SetActive(false);
        // 实测修正（2026-11-16）：拖分组标题落在另一个标题中间（= 成为它的子分组）时，整个标题罩一层半透明蓝色；落在上 / 下缘用蓝线。
        _dropHighlight = OverviewUi.Panel("DropHighlight", _content, new Color(OverviewUi.Blue.r, OverviewUi.Blue.g, OverviewUi.Blue.b, .30f)); _dropHighlight.GetComponent<Image>().raycastTarget = false; _dropHighlight.gameObject.SetActive(false);
        _dependencyHints = new OverviewDependencyHints((RectTransform)parent, repo, () => _mods, GetDisplayNumber, HideExternalHint);
        _pump = parent.gameObject.AddComponent<OverviewListPump>();
        // 1.1.0（MCE）：分组编辑器归第 8a 章（OverviewGroupEditor.Show 弹窗），本类不持有；由 GroupManagementRequested / NewGroupRequested 回调打开。
        _pump.Tick = () => {
            // The host may remain alive with another UI page selected. Drain completions only; no hidden-page ticks.
            if (!ListVisible) return;
            if(OverviewUi.HasModal)HideAllHints();
            else if(_factoryHint.Take(Time.unscaledTime,target=>target is GameObject go && go!=null && go.activeInHierarchy) is string generated) ShowHint(generated);
            else if(_hintDwell.Take(Time.unscaledTime) is string ready) ShowHint(ready);
            if (_hint.gameObject.activeSelf) PositionHint();
            if (_chipCard != null && _chipCard.gameObject.activeSelf) { if (_chipCardSource == null || !_chipCardSource.activeInHierarchy || OverviewUi.HasModal) HideChipCard(); else PositionChipCard(); }
            GamepadTick();
            // 1.1.0（MCE）：去掉来源显示上下文轮询、更新检查进度与共享更新缓存（不联网）。
            if (_opened) RefreshInfrastructure();
            FlushOverviewSearch();
            FlushInfrastructureRedraw();
            if (_opened) RefreshVirtualRows();
            UpdateFooterHint(); // 实测修正：列表下方空白处的操作提示（OverviewListView.FooterHint.cs）
            WarmVisibleHints();
            float width = Width;
            EnsureFixedChrome();
            if (ListVisible && !_building && (Math.Abs(width - _lastWidth) > 2 || _languageRevision != OverviewLanguageBridge.Revision)) Render();
        };
        EnsureFixedChrome();
        ShowInitialReadState();
        // 构造期不读数据：页面进入时 OnPageEnter 会统一加载一次，否则首次打开要扫描并建行两遍。
    }
    private float _fixedChromeWidth=-1, _fixedGoalWidth=60;
    private int _fixedChromeLanguage=-1;
    private bool _fixedChromeAuthor;
    private TextMeshProUGUI? _initialReadState;
    // 1.1.0（MCE）：启用勾只读——MCE 不启停任何模组；说明改成「只显示状态」，并提示双击可改名 / 简介。
    private const string EnablementNote = "Read only: shows whether each mod is enabled and loaded. ModConfig Enhance never enables, disables or removes mods. Double-click a name or description to edit it.";
    private string EnablementHint => Loc.T(EnablementNote, "只读：显示各模组是否启用、是否已加载；ModConfig Enhance 不会启停或删除任何模组。双击名称或简介可修改。", "唯讀：顯示各模組是否啟用、是否已載入；ModConfig Enhance 不會啟停或刪除任何模組。雙擊名稱或簡介可修改。");
    private void ShowInitialReadState()
    {
        if(_virtualRows.Count>0)return;
        if(_initialReadState==null)_initialReadState=Label(_content, Loc.T("Reading mods and components…", "正在读取模组与组件……", "正在讀取模組與組件……"),20,15,Width-50,45,24);
        _content.sizeDelta=new Vector2(0,70);
    }
    private void EnsureFixedChrome()
    {
        float width=Width;
        int language=OverviewLanguageBridge.Revision;
        if(width==_fixedChromeWidth && _authorMode==_fixedChromeAuthor && language==_fixedChromeLanguage)return;
        _fixedChromeWidth=width;_fixedChromeAuthor=_authorMode;_fixedChromeLanguage=language;
        Clear(_tools);Clear(_header);
        _tools.gameObject.SetActive(true);OverviewUi.Place(_tools,0,0,width,32);
        BuildOverviewSearch(width);
        string enablementNote=EnablementHint;
        // 1.1.3 (user): one button to expand / collapse every group. 上线前修正（用户 2026-11-16）：手柄只留 RT + L3 一种按法。
        _foldAllButton=OverviewUi.IconButton(_tools,"triangle-down",()=>SetAllFolded(null),30,30);
        OverviewUi.Place((RectTransform)_foldAllButton.transform,SearchWidth+14,1,30,30);
        AddHint(_foldAllButton.gameObject,Loc.T("Expand or collapse all groups. Controller: hold RT and press L3.","展开或收起全部分组。手柄：按住 RT 再按 L3。","展開或收起全部分組。手把：按住 RT 再按 L3。"));
        RefreshFoldAllIcon();
        var note=Label(_tools,enablementNote,SearchWidth+52,0,Mathf.Max(40,width-SearchWidth-252),32,15);note.color=OverviewUi.Muted;AddHint(note.gameObject,enablementNote);
        OverviewUi.Place(_header,0,33,width,39);
        string goalHeading = Loc.T("Enabled", "启用", "啟用");
        _fixedGoalWidth=Mathf.Max(60,Measure(goalHeading,18)+16);
        const float indexWidth = 48, versionWidth = 110;
        float goalWidth = _fixedGoalWidth;
        const float stateWidth = 0; // loading state is encoded by the enable checkbox border
        float xEnable = 8, xIndex = xEnable + goalWidth, xName = xIndex + indexWidth;
        bool compact = width < 1000;
        float authorWidth = compact ? 0 : Mathf.Clamp(width * .12f, 90, 180);
        float iconWidth = 43;
        // 1.1.0（MCE）：没有编辑模式的「更新检查许可」列，图标列恒为 3 个（设置 / 主页 / 文件夹）。
        float nameWidth = Mathf.Max(80, Mathf.Min(ModNameLayout.ColumnWidth(width), width - xName - stateWidth - iconWidth * 3 - versionWidth - authorWidth - (compact ? 20 : 85)));
        float xState = xName + nameWidth, xSettings = xState + stateWidth, xHome = xSettings + iconWidth, homeWidth = 43;
        float xFolder = xHome + homeWidth, folderWidth = 43;
        float xCheck = xFolder + folderWidth, xVersion = xCheck;
        float xAuthor = xVersion + versionWidth, xDescription = xAuthor + authorWidth;
        _columnStarts = new[] { xName, xSettings, xHome, xFolder, xCheck, xVersion, xAuthor, xDescription };
        var goalHeader = Label(_header, goalHeading, xEnable, 4, goalWidth, 30, 18); goalHeader.alignment = TextAlignmentOptions.Midline;
        AddHint(goalHeader.gameObject, enablementNote); Label(_header, "#", xIndex, 4, indexWidth, 30, 20);
        var settingsHeader = OverviewUi.IconButton(_header, "gear", () => _openSettings?.Invoke(null), 30, 32); OverviewUi.Place((RectTransform)settingsHeader.transform, xSettings + 2, 2, 30, 32); AddHint(settingsHeader.gameObject, T("Mod settings column. Click to open the general mod settings page.", "模组设置列，点击跳转到模组总设置页", "模組設置列，點擊跳轉到模組總設置頁"));
        var homeHeader = OverviewUi.IconButton(_header, "home", OpenEnhanceHomepage, 30, 32); Tint(homeHeader, OverviewUi.Ivory); OverviewUi.Place((RectTransform)homeHeader.transform, xHome + (homeWidth - 9 - 30) / 2, 2, 30, 32); AddHint(homeHeader.gameObject, T("Mod homepage column. Click to open the ModConfig Enhance store page.", "模组主页列，点击打开 ModConfig Enhance 的商店页", "模組主頁欄，點擊開啟 ModConfig Enhance 的商店頁")); Label(_header, T("Name", "名称", "名稱"), xName, 4, nameWidth, 30, 21);
        var folderHeader = OverviewUi.IconButton(_header, "folder", () => OpenFolder(BepInEx.Paths.PluginPath), 30, 32); Tint(folderHeader, OverviewUi.Ivory); OverviewUi.Place((RectTransform)folderHeader.transform, xFolder + (folderWidth - 9 - 30) / 2, 2, 30, 32); AddHint(folderHeader.gameObject, T("Mod folder column. Click to open the plugins folder.", "模组文件夹列，点击打开 plugins 文件夹", "模組文件夾列，點擊打開 plugins 文件夾"));
        Label(_header, T("Version", "版本", "版本"), xVersion, 4, versionWidth, 30, 21);
        if (!compact) { Label(_header, T("Author", "作者", "作者"), xAuthor, 4, authorWidth, 30, 21); Label(_header, T("Description", "说明", "說明"), xDescription, 4, Mathf.Max(50, width - xDescription - 20), 30, 21); }
        float[] edges = new[] { xEnable, xIndex, xName, xState, xSettings, xHome, xFolder, xVersion, xAuthor, xDescription, width - 15 }.Distinct().ToArray();
        for (int c = 0; c < edges.Length - 1; c++) { var line = OverviewUi.Panel("ColumnLine", _header, ModConfigLook.Surface(new Color(.77f, .76f, .66f, .8f))); line.GetComponent<Image>().raycastTarget = false; OverviewUi.Place(line, edges[c], 37, Mathf.Max(2, edges[c + 1] - edges[c] - 9), 1.5f); }
    }
    // 1.1.0（MCE）：整合包冲突建议（PackConflict*）、共享更新缓存、更新检查（CheckUpdates / RunChecks）、
    // 商店标签同步（SyncCategories）与更新显示签名（UpdateDisplayKey）全部去掉：MCE 不联网、没有整合包。
    private float Width => Mathf.Max(700, _root.rect.width);
    /// <summary>铅笔开关（第 8b 章，外壳工具行调用）：按 Q5 只管拖动排序 / 换组。</summary>
    public void SetAuthorMode(bool value) { _authorMode = value; Render(); }
    public void SetHideDisabled(bool value) { _repo.State.hideDisabled = value; SaveUi(); Render(); }
    /// <summary>
    /// 1.1.0（MCE）：从外壳的共用清单取模组（外壳负责扫描：OverviewPage.RescanMods），本类不扫描。
    /// IGMM 这里会 new OverviewCatalogue().Scan(_repo) 并套用缓存的商店标签。
    /// </summary>
    public void Reload()
    {
        try
        {
            SharedRelationshipSnapshot.InvalidateDisplay();
            _mods.Clear(); _mods.AddRange(_source()); _dependencyHints.Reload();
            RefreshInfrastructure(true);
            _selected.RemoveWhere(id => !_mods.Any(m => m.Id == id));
            // The component result commits this generation once. Keep existing rows until then.
            _anchor = -1; _status(Loc.T("Reading components…", "正在读取组件……", "正在讀取組件……")); _headersChanged();
        }
        catch (Exception ex) { _status(T("Could not read mods: ", "读取模组失败：", "讀取模組失敗：") + ex.Message); }
    }
    public void OnOpened(bool automaticCheck = true)
    {
        if (_disposed) return; EnsureFixedChrome(); _opened = true; _cascade.Resume(); _warmSettledFrames=0;_warmFirstAt=Time.unscaledTime+3; RefreshVirtualRows();
        RefreshInfrastructure();
        // A completed resident generation is not animated/loaded again on re-entry.
        if(_residentPending && !_cascade.HasSequence)PlayAppearance();
    }
    internal void OnTabHidden() { GamepadIntent.Release(_root.gameObject); _layerFocus=null; _opened=false; _cascade.PauseForExit(); HideAllHints(); EndDragFeedback(); }
    public void OnClosed() { GamepadIntent.Release(_root.gameObject); _layerFocus=null; _cascade.PauseForExit(); _opened = false; HideAllHints(); EndDragFeedback(); }
    /// <summary>由概览页提供：当前向 ModConfig 提供了设置项的插件 GUID。空集合表示未知，不置灰。</summary>
    internal Func<HashSet<string>>? SettingsOwners;
    internal void PlayAppearance() => _cascade.Play(_scroll, AppearanceRows);
    private IEnumerable<OverviewCascade.Row> AppearanceRows()
    {
        foreach (var row in _virtualRows)
            if (row.View != null && row.Layers == null) yield return new OverviewCascade.Row(row.View);
    }

    /// <summary>1.1.0（MCE，第 8b 章）：保存成功后通知外壳（快捷键页的分组 / 折叠与列表共用 overview.json，下次显示时重读）；
    /// 返回是否成功，失败时状态行已写原因。</summary>
    private bool Save()
    {
        try { _repo.Save(); _headersChanged(); return true; } catch (Exception ex) { _status(T("Save failed: ", "保存失败：", "儲存失敗：") + ex.Message); return false; }
    }
    /// <summary>1.1.0（MCE）：精简仓库没有 SaveUiState（IGMM 只写界面状态），折叠 / 隐藏已停用同样走 Save（overview.json 只有这一份）。</summary>
    private void SaveUi() => Save();
    private static void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        { GameObject child = parent.GetChild(i).gameObject; child.SetActive(false); UnityEngine.Object.Destroy(child); }
    }
    private void Render()
    {
        // All structural requests (including resize/filter/language) coalesce while scanning.
        if (_disposed || _infrastructureLoading || !_cascade.BeforeRebuild()) return;
        _infrastructureRedraw = false; _building = true;
        var oldFocus=CaptureRowFocus();
        float oldPixelOffset = Mathf.Max(0, _content.anchoredPosition.y);
        bool cleared = false;
        try
        {
            RefreshRowData(); InvalidateGroupTree();
            float width = Width; _lastWidth = width; _languageRevision = OverviewLanguageBridge.Revision;
            _scroll.StopMovement();
            var eventSystem = EventSystem.current; var oldSelection = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (oldSelection != null && oldSelection.transform.IsChildOf(_content)) eventSystem!.SetSelectedGameObject(null);
            HideAllHints(); _dependencyHints.InvalidateDisplay(); _warmRow=0;_warmAfter=Time.unscaledTime+.5f; EndDragFeedback(); _dropAreas.Clear();
            // Keep the owned insertion marker out of the row-destruction pass.
            string rowsSignature=RowsSignature(width);
            bool reuseRows=_reuseRows && rowsSignature==_rowsSignature;
            if(reuseRows)ParkBuiltModRows(); else DestroyParkedRows();
            _rowsSignature=rowsSignature;
            ResetVirtualRows(); _dropLine.SetParent(_root, false); _dropHighlight.SetParent(_root, false); Clear(_content); _dropLine.SetParent(_content, false); _dropHighlight.SetParent(_content, false);
            cleared = true;
            EnsureFixedChrome();
            // 1.1.0（MCE）：没有整合包冲突建议、标签筛选与来源筛选；过滤只剩关联视图、搜索与「隐藏已停用」。
            HashSet<string> configurable = SettingsOwners?.Invoke() ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var layoutRows = _mods.Where(m => MatchesRelated(m) && MatchesOverviewSearch(m) && (!_repo.State.hideDisabled || m.TargetEnabled || m.Loaded || _repo.PlannedEnabled(m))).OrderBy(m => GroupSortKey(m.Rule.groupId)).ThenBy(m => m.Rule.order).ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
            _visible = layoutRows.Where(m => _relatedMods != null || _searchQuery.Length > 0 || !RowHiddenByCollapse(m.Rule.groupId)).ToList();
            _displayNumbers.Clear();for(int i=0;i<_visible.Count;i++)_displayNumbers[_visible[i]]=i+1;
            const float indexWidth = 48, versionWidth = 110;
            float goalWidth = _fixedGoalWidth;
            const float stateWidth = 0; // loading state is encoded by the enable checkbox border
            float xEnable = 8, xIndex = xEnable + goalWidth, xName = xIndex + indexWidth;
            bool compact = width < 1000;
            float authorWidth = compact ? 0 : Mathf.Clamp(width * .12f, 90, 180);
            float iconWidth = 43;
            float nameWidth = Mathf.Max(80, Mathf.Min(ModNameLayout.ColumnWidth(width), width - xName - stateWidth - iconWidth * 3 - versionWidth - authorWidth - (compact ? 20 : 85)));
            float xState = xName + nameWidth, xSettings = xState + stateWidth, xHome = xSettings + iconWidth, homeWidth = 43;
            float xFolder = xHome + homeWidth, folderWidth = 43;
            float xCheck = xFolder + folderWidth, xVersion = xCheck;
            float xAuthor = xVersion + versionWidth, xDescription = xAuthor + authorWidth;
        _columnStarts = new[] { xName, xSettings, xHome, xFolder, xCheck, xVersion, xAuthor, xDescription };
            float y = BuildInfrastructure(0, width, xEnable, goalWidth, xName, nameWidth, xSettings, xHome, xFolder, xVersion, versionWidth, xAuthor, authorWidth, xDescription, compact); int index = 0;
            var headers = HeaderOrder(layoutRows); int nextHeader = 0;
            bool collapseApplies = _relatedMods == null && _searchQuery.Length == 0;
            void EmitHeader(string id)
            {
                // A header under a collapsed ancestor is folded away with its parent.
                if (collapseApplies && Groups.HeaderHidden(id, _repo.State.collapsedGroups)) return;
                int depth = Math.Max(1, Groups.Depth(id));
                y += y > 0 ? GroupGapBefore : 2; BuildGroupHeader(id, y, width, layoutRows); y += HeaderHeight(depth) + GroupGapAfter;
            }
            void EmitHeadersThrough(string id)
            {
                string key = Groups.Contains(id) ? id : "";
                // 实测修正（2026-11-16）：子分组排在直属模组前面，所以一个分组的第一个直属模组之前要先出完整棵子树的标题
                // （没有成员的子分组标题也在这里出，不会落到直属模组后面）。标题按树序排，子树在 headers 里是连续的一段。
                int at = -1;
                if (key.Length == 0) at = headers.IndexOf(key, nextHeader);
                else { var subtree = Groups.Subtree(key); for (int i = nextHeader; i < headers.Count; i++) if (subtree.Contains(headers[i])) at = i; else if (at >= 0) break; }
                if (at < 0) return;
                for (; nextHeader <= at; nextHeader++) EmitHeader(headers[nextHeader]);
            }
            foreach (OverviewMod mod in layoutRows)
            {
                string groupId = mod.Rule.groupId;
                EmitHeadersThrough(groupId);
                if (collapseApplies && RowHiddenByCollapse(groupId)) continue;
                int capturedIndex=index++;
                float rowY=y;
                System.Collections.IEnumerator BuildModLayers(RectTransform row)
                {
                _dropAreas[row] = (groupId, mod);
                var enablement=row.gameObject.AddComponent<OverviewEnablementBinding>();
                enablement.Mod=mod;enablement.Target=()=>_repo.PlannedEnabled(mod);
                DrawRails(row, groupId, RowHeight);
                var interaction = row.GetComponent<OverviewRowInteraction>() ?? row.gameObject.AddComponent<OverviewRowInteraction>();
                interaction.DragStateChanged=()=>_warmAfter=Time.unscaledTime+.25f;
                interaction.NativeScroll = _scroll; interaction.ForwardScroll = () => !_authorMode;
                interaction.Clicked = data => Select(mod, VisibleIndex(mod), data);
                interaction.DragStarted = () => { if (_authorMode) BeginDragFeedback(mod); };
                interaction.DragMoved = MoveDragFeedback;
                interaction.Dropped = () => { if (_draggedGroup == null) MoveSelected(groupId, mod); }; // 拖分组标题时松在模组行上不动（实测修正 2026-11-16）
                interaction.DragFinished = EndDragFeedback;
                bool ownMod = string.Equals(mod.Guid, Plugin.PluginGuid, StringComparison.OrdinalIgnoreCase);
                Label(row, (capturedIndex+1).ToString(), xIndex, 4, indexWidth, 32, 20).name = "RowIndex";
                // 1.1.0（MCE）：没有来源标签（商店 / 本地 / 待定）、变动账本、待保存 / 待重启角标、更新提示与冲突角标；
                // 名称后的角标只剩嵌套与形态（资源包 / 散放 DLL），槽位仍按 StatusChip 固定位置，右边界与 IGMM 关掉来源标签时一致。
                float laneRight = xState - 26;
                bool nestedChip=mod.Outer!=null;
                // 2.3.4: one shape chip after the pending ones: resource pack (no assembly) or loose DLL in plugins/.
                bool shapeChip=mod.ResourcePack||mod.LooseFile;
                // Slots are fixed (StatusChip.*Slot), so the lane must reach the leftmost slot in use.
                int laneChips=shapeChip?StatusChip.ShapeSlot+1:nestedChip?StatusChip.NestedSlot+1:0;
                float updateSpace=StatusChip.Reserve(laneChips);
                float titleWidth = Mathf.Max(20, laneRight - updateSpace - xName);
                // A single TMP line lays out the main name first, then as much of the
                // smaller gray original as fits. Do not reserve two estimated boxes.
                var title = Label(row, OverviewLanguageBridge.DisplayNameWithOriginal(mod), xName, 4, titleWidth, 34, 19);
                OverviewUi.ReadableName(title, titleWidth);
                title.richText = true;
                title.margin = Vector4.zero;
                OverviewUi.PlaceClipped(title, xName, 4, titleWidth, 34);
                BindDependencyHint(title.gameObject, mod);
                BindGamepadName(title.gameObject, mod);
                if (ownMod) OverviewRowActions.HighlightSelf(title);
                // 1.1.0（MCE，11-15 决定）：双击名称直接改译名（IGMM 双击是弹行菜单）；单击仍是选中。
                var titleClick = title.gameObject.AddComponent<OverviewTextClick>(); titleClick.Clicked = data => { if (data.clickCount >= 2 && data.button == PointerEventData.InputButton.Left) EditName(mod); else if (data.button == PointerEventData.InputButton.Right) ShowModActions(mod, data); else Select(mod, VisibleIndex(mod), data); };
                var versionText = Label(row, UpdateVersionCaption(mod), xVersion, 4, versionWidth - 7, 34, 20); versionText.color = OverviewUi.Ivory;
                if (mod.PreparedVersion.Length > 0) versionText.color = new Color(.88f, .72f, .43f);
                string versionHint = UpdateVersionHint(mod);
                // 1.1.0（MCE）：版本列不带更新提示（不联网）；单击写状态行，右键 / 双击看版本详情。
                var versionClick = versionText.gameObject.AddComponent<OverviewTextClick>(); versionClick.Clicked = data => { if (data.clickCount >= 2 || data.button == PointerEventData.InputButton.Right) ShowVersion(mod); else _status(SingleLine(versionHint)); };
                AddHint(versionText.gameObject, versionHint);
                string displayAuthor = mod.Author;
                var author = Label(row, displayAuthor, xAuthor, 4, Mathf.Max(1, authorWidth - 8), 34, 18); author.gameObject.SetActive(!compact); AddHint(author.gameObject, string.IsNullOrWhiteSpace(mod.Author) ? T("The local manifest does not declare an author and no project team is known.", "本地清单没有声明作者，也未找到项目所属团队。", "本地清單沒有聲明作者，也未找到項目所屬團隊。") : mod.Author + (mod.AuthorSource == "homepage" ? T("\nShown from the project homepage's owner/team.", "\n按项目主页所属作者／团队显示。", "\n按項目主頁所屬作者／團隊顯示。") : ""));
                string translatedDescription = OverviewLanguageBridge.GetDescription(mod);
                var description = Label(row, translatedDescription, xDescription, 4, Mathf.Max(40, width - xDescription - 28), 34, 16); description.color = OverviewUi.Muted; description.gameObject.SetActive(!compact);
                AddHint(description.gameObject, translatedDescription);
                BindGamepadDescription(description.gameObject, mod);
                // 1.1.0（MCE）：双击简介直接编辑简介。
                var descClick = description.gameObject.AddComponent<OverviewTextClick>(); descClick.Clicked = data => { if (data.clickCount >= 2 && data.button == PointerEventData.InputButton.Left) EditDescription(mod); else if (data.button == PointerEventData.InputButton.Right) ShowModActions(mod, data); else Select(mod, VisibleIndex(mod), data); };
                yield return null;
                // 1.1.0（MCE）：启用勾只读（不可点），只显示当前状态；颜色 / 悬停说明见 ModEnablementPresentation。
                var enabled = OverviewUi.Check(row, _repo.PlannedEnabled(mod), _ => { }); enabled.interactable = false;
                OverviewUi.Place((RectTransform)enabled.transform, xEnable + (goalWidth - 24) / 2, (row.rect.height - 24) / 2, 24, 24);
                enablement.Checkbox=enabled;
                yield return null;
                // 没有可显示设置项（或插件没加载）就灰掉齿轮，省得点进去只看到一句错误。
                bool configurableMod = configurable.Contains(mod.Guid) || mod.PluginGuids.Any(configurable.Contains);
                bool canOpenSettings = mod.Loaded && configurableMod;
                OverviewRowActions.Settings(row, xSettings + 2, (row.rect.height - OverviewRowActions.Size) / 2, () => _openSettings?.Invoke(mod), canOpenSettings,
                    canOpenSettings ? T("Click to open this mod's settings page.", "点击跳转到该模组的模组设置页", "點擊跳轉到該模組的模組設置頁")
                        : !mod.Loaded ? T("The mod is not loaded; its settings page is unavailable.", "该模组当前未加载，没有可打开的设置页", "該模組當前未加載，沒有可打開的設置頁")
                        : T("This mod does not expose any settings to ModConfig.", "该模组未向 ModConfig 提供可显示的设置项", "該模組未向 ModConfig 提供可顯示的設置項"), AddHint);
                yield return null;
                // 1.1.0（MCE）：主页钮只开项目主页 / 商店页（http/https），没有来源菜单与「已确认来源」样式。
                // 实测修正：颜色按网址种类（Thunderstore 青绿 / GitHub 浅灰 / 其余暗灰），见 OverviewRowActions.Home。
                bool hasHome = HomeUri(mod) != null;
                OverviewRowActions.Home(row, xHome + (homeWidth - 9 - OverviewRowActions.Size) / 2, (row.rect.height - OverviewRowActions.Size) / 2,
                    () => Home(mod), hasHome ? mod.HomeUrl : null, hasHome ? T("Click to open this mod's homepage:\n", "点击打开该模组的主页：\n", "點一下開啟這個模組的首頁：\n") + mod.HomeUrl : T("This mod does not declare a homepage.", "该模组没有声明主页。", "這個模組沒有提供首頁。"), AddHint);
                yield return null;
                string? modFolder = ModFolder(mod);
                Button? folder = null;
                folder = OverviewRowActions.Folder(row, xFolder + (folderWidth - 9 - OverviewRowActions.Size) / 2, (row.rect.height - OverviewRowActions.Size) / 2,
                    () => { if (folder != null) FolderShortcuts.Open(mod, (RectTransform)folder.transform, _status); },
                    ownMod || modFolder != null, ownMod ? Loc.T("Folder shortcuts", "目录快捷方式", "資料夾捷徑")
                        : modFolder != null ? T("Click to open this mod's folder:\n", "点击打开该模组所在的文件夹：\n", "點擊打開該模組所在的文件夾：\n") + modFolder
                        : T("This mod's folder was not found.", "找不到该模组所在的文件夹。", "找不到該模組所在的文件夾。"), AddHint);
                yield return null;
                // Put status dots in the strip above the text. Attaching them to the
                // title reserved 46 units of right margin even with no pending state.
                var nameMarkers = OverviewUi.Rect("NameMarkers", row);
                OverviewUi.Place(nameMarkers, xName, 0, Mathf.Max(1, laneRight - xName), 32);
                // 实测修正：去掉「已停用行把主页图标染蓝」（IGMM 照抄来的，用来标来源已确认；MCE 没有来源确认，只会让亮暗跟启停走）。
                if (nestedChip)
                {
                    var nested = StatusChip.Create(nameMarkers, StatusChip.NestedSlot, StatusChip.Nested, raycast: true);
                    AddHint(nested.gameObject, T("Amber: nested inside ", "琥珀色：内嵌于「", "琥珀色：內嵌於「") + OverviewLanguageBridge.GetDisplayName(mod.Outer!) + T(". Nested groups are recommended instead of nesting mod folders.", "」。建议用嵌套分组代替文件夹嵌套。", "」。建議用巢狀分組代替資料夾巢狀。"));
                }
                if (shapeChip)
                {
                    var shape = StatusChip.Create(nameMarkers, StatusChip.ShapeSlot, mod.ResourcePack ? StatusChip.ResourcePack : StatusChip.LooseFile, raycast: true);
                    AddHint(shape.gameObject, mod.ResourcePack
                        ? T("Teal: resource pack — a package with no DLL (bundles, textures or data used by other mods).", "青色：资源包——没有 DLL 的包（给其他模组用的资源、贴图或数据）。", "青色：資源包——沒有 DLL 的套件（給其他模組用的資源、貼圖或資料）。")
                        : T("Pink: loose DLL directly in plugins/.", "粉色：直接放在 plugins 根下的单个 DLL。", "粉色：直接放在 plugins 根目錄下的單個 DLL。"));
                }
                BuildEnablementStatus(enabled, mod);
                enablement.Capture();enablement.Refresh();
                }
                AddLayeredRow("mod:"+mod.Id, "Mod_"+mod.Id, rowY, RowHeight-2, width-15, BuildModLayers,mod);
                var added = _virtualRows[_virtualRows.Count - 1];
                if (added.View != null) RebindReusedModRow(added.View, mod, groupId);
                y += RowHeight;
            }
            for (; nextHeader < headers.Count; nextHeader++) EmitHeader(headers[nextHeader]);
            if (layoutRows.Count == 0) { Label(_content, T("No mods to display.", "没有可显示的模组。", "沒有可顯示的模組。"), 20, y + 15, width - 50, 45, 24); y += 65; }
            if (reuseRows) RefreshSelectionColors();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError("[概览] 列表刷新失败：" + ex);
            _status(Loc.T("The mod list could not finish refreshing. Please refresh to retry: ", "模组列表刷新未完成，请点击刷新重试：", "模組清單重新整理未完成，請點擊重新整理重試：") + ex.Message);
        }
        finally
        {
            try
            {
                if (cleared)
                {
                    // Count only live rows: Destroy is deferred, and an interrupted
                    // build must not leave the previous profile's scroll bounds behind.
                    float height = Mathf.Max(60,_virtualHeight);
                    foreach (RectTransform child in _content)
                        if (child != _dropLine && child != _dropHighlight && child.gameObject.activeSelf)
                            height = Mathf.Max(height, -child.anchoredPosition.y + child.rect.height + 8);
                    _content.sizeDelta = new Vector2(0, height);
                    Vector2 preserved = _content.anchoredPosition; preserved.y = Mathf.Clamp(oldPixelOffset, 0, Mathf.Max(0, _content.rect.height - _viewport.rect.height)); _content.anchoredPosition = preserved;
                    _scroll.StopMovement();
                }
            }
            finally { _building = false; }
            RefreshVirtualRows();
            if (_overviewSearch == null || !_overviewSearch.isFocused) RestoreRowFocus(oldFocus);
            if (cleared && _searchRefreshPending && _searchQuery == _searchInput.Trim() && Time.unscaledTime >= _searchAfter) { _searchRefreshPending = false; _scroll.verticalNormalizedPosition = 1; }
            if (cleared && _opened) PlayAppearance();
        }
    }
    private TextMeshProUGUI Label(Transform parent, string text, float x, float y, float width, float height, float size)
    {
        var label = OverviewUi.Text(parent, text, size);
        float lineHeight = height;
        OverviewUi.Place(label.rectTransform, x, y, width, lineHeight);
        label.overflowMode = parent == _header || parent.name.StartsWith("Group_", StringComparison.Ordinal) ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.alignment = TextAlignmentOptions.MidlineLeft; label.raycastTarget = true; return label;
    }
    private float Measure(string text, float size) => _hintText.GetPreferredValues(text, 1000, 40).x * size / Mathf.Max(1, _hintText.fontSize);
    private static string EscapeRich(string value) => value.Replace("<", "＜").Replace(">", "＞");
    private static void Tint(Button button, Color color) { foreach (var text in button.GetComponentsInChildren<TextMeshProUGUI>(true)) text.color = color; foreach (var icon in button.GetComponentsInChildren<OverviewIconGraphic>(true)) icon.color = color; }
    private void AddHint(GameObject target, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var hover = target.GetComponent<OverviewHover>() ?? target.AddComponent<OverviewHover>();
        hover.Enter = () => { _hintAnchor=null;_factoryHint.Cancel();_warmAfter=Time.unscaledTime+.25f;_hintDwell.Enter(text,Time.unscaledTime); _hint.gameObject.SetActive(false); };
        hover.Exit = HideExternalHint;
        var focus=target.GetComponent<OverviewHotkeyFocus>() ?? target.AddComponent<OverviewHotkeyFocus>();
        focus.Selected+=()=> { _factoryHint.Cancel();_hintDwell.Cancel();ShowFocusHint(text,target); };
        focus.Deselected+=HideExternalHint;
    }
    private void AddHint(GameObject target,Func<string> factory)
    {
        var hover=target.GetComponent<OverviewHover>()??target.AddComponent<OverviewHover>();
        hover.Enter=()=> { _hintAnchor=null;QueueExternalHint(target,factory); };hover.Exit=HideExternalHint;
        var focus=target.GetComponent<OverviewHotkeyFocus>()??target.AddComponent<OverviewHotkeyFocus>();
        focus.Selected+=()=> { _hintAnchor=(RectTransform)target.transform;QueueExternalHint(target,factory,true); };focus.Deselected+=HideExternalHint;
    }
    private void ShowHint(string text)
    {
        if (_disposed || OverviewUi.HasModal) return;
        _dependencyHints.HideNow();_hintDwell.Cancel();
        _shownHint = text; _hintText.richText = true; _hintText.text = text;
        _lastHintPointer=new(float.NaN,float.NaN);PositionHint(); _hint.SetAsLastSibling(); _hint.gameObject.SetActive(true);
    }
    // 1.1.0（MCE）：分组编辑器是独立弹窗（第 8a 章），B 关弹窗走外壳的 OverviewUi.TryCloseTopModal。
    internal bool TryCloseLayer() => _dependencyHints.TryCloseLayer();
    internal void HideAllHints() { _factoryHint.Cancel();_hintDwell.Cancel(); _hint.gameObject.SetActive(false); _hintAnchor=null; _dependencyHints.HideNow(); HideChipCard(); }
    internal void BindDependencyHint(GameObject target, OverviewMod mod)
    {
        _dependencyHints.Bind(target, mod);
    }
    internal int? GetDisplayNumber(OverviewMod mod) => _displayNumbers.TryGetValue(mod,out int number)?number:null;
    internal void QueueExternalHint(object target, Func<string> factory, bool immediate=false)
    {
        if(_disposed || !_opened || OverviewUi.HasModal) { HideExternalHint();return; }
        _hintDwell.Cancel();
        if(immediate) ShowHint(_factoryHint.Focus(target,factory,Time.unscaledTime));
        else _factoryHint.Enter(target,factory,Time.unscaledTime);
    }
    internal void RefreshExternalHint(object target,Func<string> factory)
    {
        if(_disposed || !_opened || OverviewUi.HasModal) { HideExternalHint();return; }
        if(_factoryHint.Refresh(target,factory) is string ready)ShowHint(ready);
    }
    internal void ShowExternalHint(string text)
    {
        if(_disposed || OverviewUi.HasModal || (_hint.gameObject.activeSelf && _shownHint==text))return;
        // 1.1.0: on a gamepad, hints raised by the focused cell (pending markers on the enable box) sit under that cell.
        if(GamepadSupport.IsGamepad && EventSystem.current?.currentSelectedGameObject is GameObject focused && focused.transform.IsChildOf(_content))
        { _factoryHint.Cancel();_hintDwell.Cancel();ShowFocusHint(text,focused);return; }
        _hintAnchor=null;_factoryHint.Cancel();_hintDwell.Enter(text,Time.unscaledTime);
    }
    internal void HideExternalHint() { _factoryHint.Cancel();_hintDwell.Cancel(); _hint.gameObject.SetActive(false); _hintAnchor=null; }
    private void PositionHint()
    {
        float width = Mathf.Min(700, Width - 30);
        if(_measuredHint!=_shownHint || _hintWidth!=width || _hintFont!=_hintText.font || _hintLanguage!=Loc.TranslationRevision || _hintFontSize!=_hintText.fontSize)
        {
            if(_hintMetricRevision!=Loc.TranslationRevision || _hintMetricFont!=_hintText.font || _hintMetricWidth!=width || _hintMetricSize!=_hintText.fontSize)
            { _hintMetrics.Clear();_hintMetricRevision=Loc.TranslationRevision;_hintMetricFont=_hintText.font;_hintMetricWidth=width;_hintMetricSize=_hintText.fontSize; }
            if(!_hintMetrics.TryGet(_shownHint,out _hintHeight))
            {
                _hintHeight=Mathf.Clamp(_hintText.GetPreferredValues(_shownHint,width-24,0).y+24,45,300);
                _hintMetrics.Set(_shownHint,_hintHeight);
            }
            _hint.sizeDelta=new Vector2(width,_hintHeight);
            _lastHintPointer=new(float.NaN,float.NaN);
            _measuredHint=_shownHint;_hintWidth=width;_hintFont=_hintText.font;_hintLanguage=Loc.TranslationRevision;_hintFontSize=_hintText.fontSize;
        }
        float height=_hintHeight;
        if(HintFollowsFocus)
        {
            var at=FocusPoint(_hintAnchor!,out float anchorHeight);
            float fx=at.x+8, fy=at.y+6;
            if(fy+height>_root.rect.height-5) fy=at.y-anchorHeight-height-6;
            fx=Mathf.Clamp(fx,5,Mathf.Max(5,Width-width-20)); fy=Mathf.Clamp(fy,5,Mathf.Max(5,_root.rect.height-height-5));
            HintCanvas.Position(_hint,fx,fy); _lastHintPointer=new(float.NaN,float.NaN); return;
        }
        var screen=PointerPosition;var rootSize=_root.rect.size;
        if(screen==_lastHintPointer && rootSize==_lastHintRootSize)return;
        _lastHintPointer=screen;_lastHintRootSize=rootSize;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, EventCamera, out Vector2 pointer)) return;
        float x = pointer.x - _root.rect.xMin + 18;
        float pointerY = _root.rect.yMax - pointer.y;
        float y = pointerY + 22;
        if (y + height > _root.rect.height - 5) y = pointerY - height - 18;
        x = Mathf.Clamp(x, 5, Mathf.Max(5, Width - width - 20)); y = Mathf.Clamp(y, 5, Mathf.Max(5, _root.rect.height - height - 5));
        HintCanvas.Position(_hint, x, y);
    }
    private int GroupOrder(string id) => GroupSortKey(id);
    private Color GroupColor(string id) => string.IsNullOrEmpty(id) ? OverviewUi.Muted : ColorUtility.TryParseHtmlString(_repo.State.groups.FirstOrDefault(g => g.id == id)?.color ?? "", out Color color) ? color : OverviewUi.Blue;
    private void BuildGroupHeader(string groupId, float y, float width, IReadOnlyList<OverviewMod> layoutRows)
    {
        OverviewGroup? group = Groups.Get(groupId);
        int depth = Math.Max(1, Groups.Depth(groupId));
        float height = HeaderHeight(depth), font = HeaderFont(depth);
        Color groupTint = GroupColor(groupId);
        int count = group == null ? layoutRows.Count(m => !Groups.Contains(m.Rule.groupId)) : Groups.SubtreeCount(groupId, layoutRows, m => m.Rule.groupId);
        AddVirtualRow("group:"+groupId, "Group_"+groupId, y, height, width-15, row => {
        row.GetComponent<Image>().color=Color.Lerp(ModConfigLook.Surface(new Color(.1f,.11f,.12f,.95f)), new Color(groupTint.r,groupTint.g,groupTint.b,.95f), depth <= 1 ? .14f : .09f);
        _dropAreas[row] = (groupId, null);
        float rails = DrawRails(row, groupId, height);
        float indent = (depth - 1) * GroupIndent, left = Mathf.Max(rails + 4, indent + 7);
        bool collapsed = _repo.State.collapsedGroups.Contains(groupId); // 1.1.0（MCE）：没有标签筛选
        var arrow = OverviewUi.IconButton(row, collapsed ? "triangle-right" : "triangle-down", () => { if (!_repo.State.collapsedGroups.Remove(groupId)) _repo.State.collapsedGroups.Add(groupId); SaveUi(); _selected.Clear(); _anchor = -1; RenderFold(); }, 22, 26); OverviewUi.Place((RectTransform)arrow.transform, left, (height - 26) / 2, 22, 26);
        string groupName = group?.name ?? T("Ungrouped", "未分组", "未分組");
        float captionX = left + 28, titleWidth = Mathf.Min(width * .45f, Measure(groupName, font) + 12);
        var caption = Label(row, EscapeRich(groupName), captionX, 0, titleWidth, height, font); caption.fontStyle = FontStyles.Normal; caption.color = groupTint; caption.characterSpacing = depth <= 1 ? 1.5f : 1f; OverviewUi.ReadableName(caption, titleWidth); OverviewUi.PlaceClipped(caption,captionX,0,titleWidth,height); AddHint(caption.gameObject, group == null ? groupName : Groups.Path(groupId)); OverviewEditableFrame.Set(caption.rectTransform, _authorMode);
        var counter = Label(row, count.ToString(), captionX + titleWidth + 6, 0, 60, height, font - 4); counter.color = OverviewUi.Muted; OverviewUi.PlaceClipped(counter, captionX + titleWidth + 6, 0, 60, height);
        float descriptionX = captionX + titleWidth + 6 + Mathf.Min(60, Measure(count.ToString(), font - 4) + 14);
        if (!string.IsNullOrWhiteSpace(group?.description)) { var description = Label(row, group!.description, descriptionX, 0, Mathf.Max(0, width - descriptionX - 21), height, 17); description.color = OverviewUi.Muted; OverviewUi.PlaceClipped(description,descriptionX,0,Mathf.Max(0,width-descriptionX-21),height); AddHint(description.gameObject, group.description); }
        var target = row.gameObject.AddComponent<OverviewRowInteraction>();
        target.NativeScroll = _scroll; target.ForwardScroll = () => !_authorMode || group == null;
        // A group heading dropped here: on the top / bottom quarter it goes before / after this group (same level),
        // in the middle it becomes this group's subgroup (实测修正 2026-11-16, see DropGroupOn); dropped mods join this group.
        target.Dropped = () => { if (_draggedGroup != null) { var dragged = _draggedGroup; var zone = GroupDropZoneFor(groupId); EndGroupDrag(); if (group != null && dragged.id != groupId) DropGroupOn(dragged.id, groupId, zone); } else MoveSelected(groupId, null); };
        if (group != null) { target.DragStarted = () => BeginGroupDrag(group); target.DragMoved = MoveDragFeedback; target.DragFinished = EndGroupDrag; }
        // 1.1.0（MCE，概览页第 8b 章）：照 IGMM 接回：右键 = 分组菜单，双击 = 编辑分组（「未分组」标题双击 = 分组管理）。
        // 拖动标题挂接只在铅笔开着（_authorMode，Q5）时生效，否则拖动 = 滚动。
        Action<PointerEventData> clicked = data => {
            if (data.button == PointerEventData.InputButton.Right) { ShowGroupMenu(group, data); return; }
            if (data.clickCount >= 2) { if (group != null) EditGroup(group); else ManageGroups(); } };
        target.Clicked = clicked;
        caption.gameObject.AddComponent<OverviewTextClick>().Clicked = clicked;
        });
    }
    private void Select(OverviewMod mod, int index, PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left) return;
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift), ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (shift && _anchor >= 0 && _anchor < _visible.Count)
        { if (!ctrl) _selected.Clear(); for (int i = Math.Min(index, _anchor); i <= Math.Max(index, _anchor); i++) _selected.Add(_visible[i].Id); }
        else if (ctrl) { if (!_selected.Add(mod.Id)) _selected.Remove(mod.Id); _anchor = index; }
        else { _selected.Clear(); _selected.Add(mod.Id); _anchor = index; }
        _status(T("Selected ", "已选 ", "已選 ") + _selected.Count + T(" mods", " 个模组", " 個模組") + (_authorMode ? T("; drag to reorder or change group.", "；可拖动排序或移动分组。", "；可拖動排序或移動分組。") : T("; double-click a name or description to edit it.", "；双击名称或简介可修改。", "；雙擊名稱或簡介可修改。")));
        // Do not destroy the click target before the UI can detect the second click.
        RefreshSelectionColors();
    }
    private Vector2 PointerPosition => Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
    private Camera? EventCamera => _eventCamera;
    private void BeginDragFeedback(OverviewMod mod)
    {
        if (!_selected.Contains(mod.Id)) { _selected.Clear(); _selected.Add(mod.Id); RefreshSelectionColors(); }
        _dragged = mod; HideAllHints();
        _dragCaption.text = OverviewLanguageBridge.GetDisplayName(mod) + (_selected.Count > 1 ? " + " + (_selected.Count - 1) : "");
        _dragGhost.gameObject.SetActive(true); _dragGhost.SetAsLastSibling(); _status(T("Drag to the blue insertion line, or to a group heading.", "拖到蓝色插入线或分组标题处松开。", "拖到藍色插入線或分組標題處松開。"));
    }
    private void MoveDragFeedback(PointerEventData data)
    {
        if (!_authorMode || _dragged == null && _draggedGroup == null) return;
        Camera? camera = EventCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, data.position, camera, out var point))
            OverviewUi.Place(_dragGhost, Mathf.Clamp(point.x - _root.rect.xMin + 15, 0, Mathf.Max(0, Width - 300)), Mathf.Clamp(_root.rect.yMax - point.y + 18, 0, Mathf.Max(0, _root.rect.height - 40)), 300, 40);
        _dropLine.gameObject.SetActive(false); _dropHighlight.gameObject.SetActive(false);
        if (_draggedGroup != null) GroupDragFeedback(data, camera);
        else foreach (var area in _dropAreas)
        {
            if (!area.Key.gameObject.activeInHierarchy || !RectTransformUtility.RectangleContainsScreenPoint(area.Key, data.position, camera)) continue;
            if (area.Value.Before != null && _selected.Contains(area.Value.Before.Id)) break;
            float top = -area.Key.anchoredPosition.y;
            OverviewUi.Place(_dropLine, 4, top, Mathf.Max(30, _viewport.rect.width - 8), 3); _dropLine.SetAsLastSibling(); _dropLine.gameObject.SetActive(true);
            break;
        }
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, data.position, camera, out var viewportPoint))
        {
            if (viewportPoint.y > _viewport.rect.yMax - 25) _scroll.verticalNormalizedPosition = Mathf.Clamp01(_scroll.verticalNormalizedPosition + .018f);
            else if (viewportPoint.y < _viewport.rect.yMin + 25) _scroll.verticalNormalizedPosition = Mathf.Clamp01(_scroll.verticalNormalizedPosition - .018f);
            RefreshVirtualRows();
        }
    }
    private void EndDragFeedback() { _warmAfter=Time.unscaledTime+.25f;_dragged = null; _dragGhost.gameObject.SetActive(false); _dropLine.gameObject.SetActive(false); _dropHighlight.gameObject.SetActive(false); }
    private void RefreshSelectionColors()
    {
        foreach (Transform child in _content)
        {
            if (!child.name.StartsWith("Mod_", StringComparison.Ordinal)) continue;
            string id = child.name.Substring(4); var image = child.GetComponent<Image>();
            if (image != null) image.color = _selected.Contains(id) ? ModConfigLook.Surface(new Color(.18f, .34f, .43f, .72f)) : ModConfigLook.Surface(new Color(.1f, .11f, .13f, .4f));
        }
    }
    private List<OverviewMod> Selected(OverviewMod? fallback = null)
    {
        var result = _mods.Where(m => _selected.Contains(m.Id)).ToList(); if (result.Count == 0 && fallback != null) result.Add(fallback); return result;
    }
    // 1.1.0（MCE）：去掉 Toggle（启停草稿）——MCE 不启停任何模组，勾选框只读。
    // 1.1.0（MCE）：IGMM 的「更改模组信息」走管理中心（不搬）；这里双击 / 行菜单直接开小对话框改译名或简介，
    // 写入当前语言文件（Q6，OverviewLanguageBridge.SetName / SetDescription），成功后 Loc.TranslationRevision 变化，
    // 列表、快捷键页、设置页一起刷新。版本标签（Rule.versionLabel）是整合包作者功能，不搬。
    private void EditName(OverviewMod mod) { HideAllHints(); OverviewModInfoDialog.EditName(mod, _status, AfterInfoEdited); }
    private void EditDescription(OverviewMod mod) { HideAllHints(); OverviewModInfoDialog.EditDescription(mod, _status, AfterInfoEdited); }
    private void AfterInfoEdited() { _relatedMods = null; Render(); _headersChanged(); }
    /// <summary>1.1.0（MCE）：主页只认 http/https（IGMM 这里是来源菜单 SourceUi.Menu）。</summary>
    private static Uri? HomeUri(OverviewMod mod)
        => Uri.TryCreate(mod.HomeUrl ?? "", UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) ? uri : null;
    private void Home(OverviewMod mod)
    {
        var uri = HomeUri(mod);
        if (uri == null) { _status(T("This mod does not declare a homepage.", "该模组没有声明主页。", "該模組沒有宣告主頁。")); return; }
        Application.OpenURL(uri.AbsoluteUri); _status(T("Opened: ", "已打开：", "已打開：") + uri.AbsoluteUri);
    }
    /// <summary>The package folder (manifest root); loose DLLs in plugins\ fall back to the folder that holds them.</summary>
    internal static string? ModFolder(OverviewMod mod)
    {
        try
        {
            if (!string.IsNullOrEmpty(mod.PackageRoot) && System.IO.Directory.Exists(mod.PackageRoot)) return mod.PackageRoot;
            string? folder = string.IsNullOrEmpty(mod.Location) ? null : System.IO.Path.GetDirectoryName(mod.Location);
            return folder != null && System.IO.Directory.Exists(folder) ? folder : null;
        }
        catch { return null; }
    }
    private void OpenFolder(string path) => OpenFolder(path, _status);
    /// <summary>1.1.0（MCE）：上架规则不许 Process.Start，IGMM 的 explorer.exe 一律改 Application.OpenURL（file:// 地址）。</summary>
    internal static void OpenFolder(string path, Action<string> status)
    {
        try
        {
            if (!System.IO.Directory.Exists(path)) { status(T("Folder not found: ", "文件夹不存在：", "文件夾不存在：") + path); return; }
            Application.OpenURL(new Uri(path).AbsoluteUri);
            status(T("Opened: ", "已打开：", "已打開：") + path);
        }
        catch (Exception ex) { status(T("Could not open the folder: ", "无法打开文件夹：", "無法打開文件夾：") + ex.Message); }
    }
    /// <summary>
    /// 1.1.0（MCE）：表头主页钮开 ModConfig Enhance 自己的商店页：本地文件夹认得出包身份（Team-Name）时拼雷霆商店页链接
    /// （只拼链接，不联网），否则用 manifest 里的主页，再不行用 GitHub 项目页。
    /// </summary>
    private void OpenEnhanceHomepage()
    {
        var own = _mods.FirstOrDefault(mod => string.Equals(mod.Guid, Plugin.PluginGuid, StringComparison.OrdinalIgnoreCase));
        string store = own != null ? OverviewCatalogue.StorePageUrl(own.PackageNamespace, own.PackageName) : "";
        string url = store.Length > 0 ? store : own != null && HomeUri(own) != null ? HomeUri(own)!.AbsoluteUri : "https://github.com/AiAe-156/Peak-ModConfigEnhance";
        Application.OpenURL(url); _status(T("Opened: ", "已打开：", "已打開：") + url);
    }
    /// <summary>1.1.0（MCE）：只看本地版本信息（IGMM 的菜单是更新到最新版 / 恢复整合包版本 / 更新说明 / 打开主页手动下载）。</summary>
    private void ShowVersion(OverviewMod mod)
    {
        HideAllHints();
        OverviewUi.ShowText(OverviewLanguageBridge.GetDisplayName(mod) + " · " + UpdateVersionCaption(mod), UpdateVersionHint(mod));
    }
    private static string UpdateVersionHint(OverviewMod mod)
    {
        var lines = new List<string>();
        void Add(string text) { if (text.Trim().Length > 0) lines.Add(text.TrimStart('\n')); }
        // 1.1.0（MCE）：没有商店最新版、检查结果与整合包指定版本。
        Add(Loc.F("Installed package: {0}\nPlugin version: {1}", "已安装包版本：{0}\n插件版本：{1}", "已安裝套件版本：{0}\n外掛版本：{1}",
                mod.PackageVersion.Length > 0 ? mod.PackageVersion : Loc.T("Unknown version", "版本未知", "版本未知"),
                mod.PluginVersion.Length > 0 ? mod.PluginVersion : Loc.T("Unknown version", "版本未知", "版本未知")));
        if (mod.PreparedVersion.Length > 0) Add(Loc.F("\nA newer copy is waiting in the same folder: {0}. It loads after the game restarts.",
            "\n同一文件夹里有更新的副本：{0}，重启游戏后加载。", "\n同一資料夾裡有更新的副本：{0}，重新啟動遊戲後載入。", mod.PreparedVersion));
        // 2.4.4: lower-version manifest.json copies inside this package folder; the version above already ignores them.
        if (mod.StaleManifests.Count > 0) Add("\n" + Loc.T("Old manifest.json copies (ignored for the version):", "旧说明文件残留（不参与版本判断）：", "舊說明檔案殘留（不參與版本判斷）：")
            + "\n" + string.Join("\n", mod.StaleManifests));
        return string.Join("\n", lines);
    }
    private static string UpdateVersionCaption(OverviewMod mod) => mod.DisplayVersion;
    // 1.1.0（MCE）：来源标签槽宽、更新角标、更新许可（EditPermissions / SetGlobalAllowed）去掉。
    // 1.1.0（MCE）：分组编辑窗口由外壳打开（OverviewPage.OpenGroupEditor），构造列表后立即接上这两个回调；
    // IGMM 没接回调时退回列表自己持有的编辑器，MCE 不需要这条退路。显式初值免 CS0649。
    internal Action<OverviewGroup?>? GroupManagementRequested = null;
    /// <summary>Opens the group editor on a new group under <paramref name="parentId"/> (empty = top level).</summary>
    internal Action<string>? NewGroupRequested = null;
    internal void ManageGroups() => GroupManagementRequested?.Invoke(null);
    internal void NewGroupIn(string parentId) => NewGroupRequested?.Invoke(parentId);
    internal void EditGroup(OverviewGroup? group) => GroupManagementRequested?.Invoke(group);
    /// <summary>
    /// 行菜单「移动到分组」（1.1.0（MCE，第 8b 章），Q5：不受铅笔开关限制）。IGMM 的 AssignGroup 要手输分组 ID / 名称且没有入口；
    /// 这里改成从分组树里选（缩进 = 层级）。点的行不在已选里时只移动这一行；在已选里时移动全部已选（Ctrl / Shift 多选）。
    /// </summary>
    private void AssignGroup(OverviewMod mod)
    {
        HideAllHints();
        if (!_selected.Contains(mod.Id)) { _selected.Clear(); _selected.Add(mod.Id); _anchor = _visible.IndexOf(mod); RefreshSelectionColors(); }
        var options = new List<(string Label, string Value)> { (T("Ungrouped", "未分组", "未分組"), "") };
        foreach (var group in Groups.Flat) options.Add((new string(' ', (Groups.Depth(group.id) - 1) * 2) + Groups.Path(group.id), group.id));
        string current = Groups.Contains(mod.Rule.groupId ?? "") ? mod.Rule.groupId! : "";
        string title = _selected.Count > 1 ? Loc.F("Move {0} selected mods", "移动所选的 {0} 个模组", "移動所選的 {0} 個模組", _selected.Count) : Loc.T("Move to group", "移动到分组", "移動到分組");
        OverviewUi.Choose(title, options, current, value => MoveSelected(value ?? "", null, true));
    }
    private void MoveSelected(string groupId, OverviewMod? before, bool explicitAction = false)
    {
        // 1.1.0（MCE，第 8b 章）：菜单里的明确操作（explicitAction）不受铅笔限制（Q5）；拖动仍要铅笔开着。
        if (!explicitAction && (!_authorMode || _dragged == null)) return;
        var moving = Selected(); if (moving.Count == 0 || before != null && moving.Contains(before)) return;
        var target = _mods.Where(m => m.Rule.groupId == groupId && !moving.Contains(m)).OrderBy(m => m.Rule.order).ToList();
        int index = before == null ? target.Count : target.IndexOf(before); if (index < 0) index = target.Count;
        target.InsertRange(index, moving.OrderBy(m => _visible.IndexOf(m)));
        for (int i = 0; i < target.Count; i++) { target[i].Rule.groupId = groupId; target[i].Rule.order = i; }
        bool saved = Save(); _dragged = null; _pump.Enqueue(Render); if (saved) _status(T("Groups and display order saved.", "已保存模组分组和显示顺序。", "已保存模組分組和顯示順序。"));
    }
    public void Dispose()
    {
        // 1.1.0（MCE）：没有 HTTP 客户端、更新检查任务与本类持有的分组编辑器要释放。
        if (_disposed) return; HideAllHints();_componentProtectionRefresh.Clear(); DestroyParkedRows(); ResetVirtualRows(); _disposed = true; _pump.Tick = null; _dependencyHints.Dispose();
        UnityEngine.Object.Destroy(_root.gameObject);
    }
}

// 1.1.0（MCE）：OverviewListPump / OverviewTextClick / OverviewHover 已在 OverviewInteraction.cs（快捷键页搬迁时抄出），这里不重复定义。
internal sealed class OverviewRowInteraction : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    internal Action<PointerEventData>? Clicked;
    internal Action? DragStarted, DragFinished, Dropped;
    internal Action<PointerEventData>? DragMoved;
    internal bool IsDragging;
    internal Action? DragStateChanged;
    internal ScrollRect? NativeScroll;
    internal Func<bool>? ForwardScroll;
    public void OnPointerClick(PointerEventData data) => Clicked?.Invoke(data);
    public void OnBeginDrag(PointerEventData data) { IsDragging=true; DragStateChanged?.Invoke(); if (ForwardScroll?.Invoke() == true) NativeScroll?.OnBeginDrag(data); else DragStarted?.Invoke(); }
    public void OnDrag(PointerEventData data) { if (ForwardScroll?.Invoke() == true) NativeScroll?.OnDrag(data); else DragMoved?.Invoke(data); }
    public void OnEndDrag(PointerEventData data) { IsDragging=false; DragStateChanged?.Invoke(); if (ForwardScroll?.Invoke() == true) NativeScroll?.OnEndDrag(data); else DragFinished?.Invoke(); }
    public void OnDrop(PointerEventData data) => Dropped?.Invoke();
    private void OnDisable()=>IsDragging=false;
}







