#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ModConfigEnhance.Updating;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ModConfigEnhance.Overview;

// 1.1.0（MCE，概览页第 7 章）：照抄 IGMM 后裁成只读详情（去掉启停 ComponentToggle、卸载、打开商店窗口、组件改名），见下方各处注释。
/// <summary>A bounded component page with normal actions and an expandable relationship tree.</summary>
internal static class ComponentDetailsView
{
    /// <summary>1.1.0（MCE）：从 IGMM SourceUi.Rows 抄来（SourceUi 是来源工具，不搬）：带滚动的行容器。</summary>
    private static RectTransform Rows(RectTransform panel, float top, float height, out ScrollRect scroll)
    {
        PublicationNavigation.Invalidate(panel);
        var host = OverviewUi.Rect("Scroll", panel); OverviewUi.Place(host, 20, top, panel.rect.width - 40, height);
        host.gameObject.AddComponent<RectMask2D>(); var background = host.gameObject.AddComponent<Image>(); background.color = ModConfigLook.Surface(new Color(0, 0, 0, .08f));
        scroll = host.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40;
        var content = OverviewUi.Rect("Rows", host); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(0, 1); content.sizeDelta = Vector2.zero;
        scroll.viewport = host; scroll.content = content; return content;
    }
    internal static void Open(OverviewRepository repo, IReadOnlyList<OverviewMod> mods,
        InfrastructureOverviewModel? owner, string key, ComponentDependencyGraph? rawGraph,
        IReadOnlyList<InfrastructureOverviewModel> components, Action? changed = null)
    {
        var graph=SharedRelationshipSnapshot.Get(mods,components,rawGraph);
        components=SharedRelationshipSnapshot.Components(graph);
        string requestedKey=key;
        key=SharedRelationshipSnapshot.Canonical(graph,key);
        var rootMod=SharedRelationshipSnapshot.Metadata(graph,key)?.Mod;
        var panel = OverviewUi.CreateModalPanel("MCE_ComponentDetails", 940, 680); if (panel == null) return;
        var labels = new ComponentRelationshipLabels(graph, mods, components);
        string name = owner?.Name ?? labels?.Name(key) ?? key; // 1.1.0（MCE）：没有组件别名（Rule.alias）
        var heading = OverviewUi.Text(panel, name, 22);
        OverviewUi.PlaceClipped(heading, 22, 10, panel.rect.width - 44, 32);
        var content = Rows(panel, 50, Mathf.Max(28, panel.rect.height - 110), out var scroll);
        float width = panel.rect.width - 40;
        var body = OverviewUi.Rect("ComponentBody", content);
        Button? folderButton = null;
        bool reverse = graph.Edges.All(e=>!e.From.Equals(key,StringComparison.OrdinalIgnoreCase))
            &&graph.Edges.Any(e=>e.To.Equals(key,StringComparison.OrdinalIgnoreCase));
        var collapsed = new HashSet<string>(StringComparer.Ordinal);
        var allRows = Array.Empty<ComponentRelationshipTree.Row>();
        bool defaultExpansion = true;
        void Clear()
        {
            for (int i = body.childCount - 1; i >= 0; i--) {
                var child = body.GetChild(i).gameObject; child.SetActive(false); UnityEngine.Object.Destroy(child);
            }
        }
        TextMeshProUGUI Text(string text, float x, float y, float w, float height, float size = 17, bool wrap = false, bool rich = false)
        {
            var label = OverviewUi.Text(body, text, size);
            label.richText = rich;
            label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            // TMP suppresses the entire first line in Ellipsis mode when its actual font
            // metrics exceed the box. Localized fallback fonts can be taller than the base font.
            float textWidth = Mathf.Max(20, w);
            float measuredHeight = Mathf.Ceil(label.GetPreferredValues(text, textWidth, 0).y) + 4;
            OverviewUi.Place(label.rectTransform, x, y, textWidth, Mathf.Max(height, measuredHeight));
            return label;
        }
        Button Action(string title, Action clicked, float x, float y, float w, bool enabled = true, UiActionRole role = UiActionRole.Neutral)
        {
            var button = OverviewUi.Button(body, title, clicked, w, 32, role);
            button.gameObject.name = "ComponentAction_" + title;
            OverviewUi.Place((RectTransform)button.transform, x, y, w, 32); button.interactable = enabled;
            var caption = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (caption != null) { caption.alignment = TextAlignmentOptions.Midline; caption.overflowMode = TextOverflowModes.Ellipsis; }
            return button;
        }
        void OpenLocations()
        {
            if (owner == null || folderButton == null) return;
            FolderShortcuts.Open(owner,(RectTransform)folderButton.transform,_=>{});
        }
        void Render(bool resetScroll = false)
        {
            var selected = EventSystem.current?.currentSelectedGameObject;
            string? focus = selected != null && selected.transform.IsChildOf(body) ? selected.name : null;
            float position = scroll.verticalNormalizedPosition; Clear();
            float y = 0;
            string version = owner?.Version ?? labels?.Version(key) ?? "";
            var metadata = Text((owner?.TypeTitle ?? Loc.T("Component details", "组件详情", "組件詳情")) + " · " + version,
                6, y, width - 12, 24, 15);
            metadata.color = OverviewUi.Muted; y += metadata.rectTransform.rect.height + 4;
            bool identified = owner?.IdentityConfirmed ?? ShopInstalled.Key(key);
            if (owner != null)
            {
                // 1.1.0（MCE，概览页第 7 章）：只读详情。去掉「模组商店」「卸载（安全预览）」「改译名」（组件别名）「启用 / 停用」
                // （ComponentToggle 会改名文件）；只留「商店网页」（只拼链接，Application.OpenURL）与「打开文件夹」。
                int columns = width >= 720 ? 4 : 2;
                float buttonWidth = Mathf.Min(185, (width - (columns - 1) * 10 - 12) / columns);
                Button PlaceAction(int i, string title, Action clicked, bool enabled = true, UiActionRole role = UiActionRole.Neutral)
                    => Action(title, clicked, 6 + (i % columns) * (buttonWidth + 10), y + (i / columns) * 36, buttonWidth, enabled, role);
                int at = key.IndexOf('-');
                string store = identified && at > 0 ? OverviewCatalogue.StorePageUrl(key.Substring(0, at), key.Substring(at + 1)) : "";
                PlaceAction(0, Loc.T("Store page", "商店网页", "商店網頁"), () => { if (store.Length > 0) Application.OpenURL(store); }, store.Length > 0);
                folderButton = PlaceAction(1, Loc.T("Open folder", "打开文件夹", "開啟資料夾"), OpenLocations);
                y += 36 + 2;
            }
            string status = !identified ? Loc.T("Package identity is unconfirmed; no store page.",
                "包身份尚未确认，没有商店页。", "套件身分尚未確認，沒有商店頁。")
                : "";
            if (status.Length > 0) {
                var statusText = Text(status, 6, y, width - 12, 24, 14, true);
                statusText.color = OverviewUi.Muted; y += statusText.rectTransform.rect.height + 4;
            }
            float tabWidth = Mathf.Min(195, (width - 22) / 2);
            void SwitchDirection(bool incoming)
            {
                reverse = incoming; collapsed.Clear(); defaultExpansion = true;
                Render(true);
            }
            var dependencies = Action(Loc.T("Dependencies", "依赖的模组", "依賴的模組"), () => SwitchDirection(false), 6, y, tabWidth);
            var dependants = Action(Loc.T("Installed dependants", "依赖它的已安装模组", "依賴它的已安裝模組"), () => SwitchDirection(true), 16 + tabWidth, y, tabWidth);
            UiActionStyle.Select(dependencies, !reverse); UiActionStyle.Select(dependants, reverse);
            bool inlineActions = width >= 720;
            float bulkX = inlineActions ? 26 + tabWidth * 2 : 6;
            float bulkY = inlineActions ? y : y + 36;
            Action(Loc.T("Expand all", "全部展开", "全部展開"), () => { collapsed.Clear(); defaultExpansion = false; Render(); }, bulkX, bulkY, 105);
            Action(Loc.T("Collapse all", "全部折叠", "全部摺疊"), () => { defaultExpansion = false; foreach (var row in allRows.Where(r => r.HasChildren)) collapsed.Add(row.Path); Render(); }, bulkX + 115, bulkY, 105);
            y += inlineActions ? 36 : 72;
            if (graph == null || labels == null) {
                var reading = Text(Loc.T("Reading relationships…", "正在读取依赖关系……", "正在讀取依賴關係……"), 10, y, width - 20, 30);
                y += reading.rectTransform.rect.height + 6;
            }
            else
            {
                allRows = ComponentRelationshipTree.Build(graph, key, reverse, 16, 2048, labels.Canonical).ToArray();
                if (defaultExpansion)
                {
                    foreach (var row in allRows.Where(r => r.Depth >= 2 && r.HasChildren)) collapsed.Add(row.Path);
                    defaultExpansion = false;
                }
                var hidden = new HashSet<string>(StringComparer.Ordinal);
                if (allRows.Length == 0) {
                    var empty = Text(Loc.T("No relationships found.", "暂无依赖关系。", "暫無依賴關係。"), 10, y, width - 20, 30);
                    y += empty.rectTransform.rect.height + 6;
                }
                foreach (var row in allRows)
                {
                    if (hidden.Contains(row.Parent)) { hidden.Add(row.Path); continue; }
                    bool closed = collapsed.Contains(row.Path);
                    if (closed) hidden.Add(row.Path);
                    float indent = Mathf.Min(row.Depth - 1, Mathf.Max(0, Mathf.Min(8, (width - 400) / 14))) * 14;
                    var background = OverviewUi.Panel("Relationship", body, ModConfigLook.Surface(new Color(.08f, .11f, .12f, .50f)));
                    float stateWidth = Mathf.Clamp(width * .19f, 100, 165);
                    float nameWidth = Mathf.Max(60, width - 58 - indent - stateWidth);
                    var rowTitle = Text("", 34 + indent, y + 2, nameWidth, 24, 16, rich: true);
                    string fitted = DependencyRowText.FitName(labels.Name(row.Id), labels.Original(row.Id), labels.Version(row.Id),
                        nameWidth - 20, value => rowTitle.GetPreferredValues(value).x);
                    rowTitle.text = DependencyDepthStyle.Wrap(OverviewDependencyHints.TreeArrow() + " " + fitted, row.Depth - 1);
                    float titleHeight = Mathf.Max(24, Mathf.Ceil(rowTitle.GetPreferredValues(rowTitle.text, nameWidth, 0).y) + 2);
                    OverviewUi.Place(rowTitle.rectTransform, 34 + indent, y + 2, nameWidth, titleHeight);
                    var rowState = Text(labels.State(row), width - stateWidth - 12, y + 2, stateWidth, 24, 13);
                    rowState.color = OverviewUi.Muted;
                    float rowHeight = Mathf.Max(titleHeight, rowState.rectTransform.rect.height) + 4;
                    rowTitle.alignment = rowState.alignment = TextAlignmentOptions.MidlineLeft;
                    OverviewUi.Place(rowTitle.rectTransform, 34 + indent, y + 2, nameWidth, rowHeight - 4);
                    OverviewUi.Place(rowState.rectTransform, width - stateWidth - 12, y + 2, stateWidth, rowHeight - 4);
                    OverviewUi.Place(background, 6, y, width - 12, rowHeight);
                    if (row.HasChildren && !row.Truncated)
                    {
                        var toggle = OverviewUi.IconButton(body, closed ? "triangle-right" : "triangle-down", () => {
                            if (!collapsed.Add(row.Path)) collapsed.Remove(row.Path); Render();
                        }, 18, 20);
                        if (ColorUtility.TryParseHtmlString(DependencyDepthStyle.Ink(row.Depth - 1), out var depthInk))
                            foreach (var glyph in toggle.GetComponentsInChildren<OverviewIconGraphic>(true)) glyph.color = depthInk;
                        toggle.gameObject.name = "Relationship_" + row.Path;
                        OverviewUi.Place((RectTransform)toggle.transform, 10 + indent, y + (rowHeight - 20) / 2, 18, 20);
                        GamepadSupport.MarkSelectable(toggle.gameObject, scroll, (RectTransform)toggle.transform);
                    }
                    y += rowHeight + 1;
                }
                if (allRows.Any(r => r.Truncated)) {
                    var limit = Text(Loc.T("Relationship limit reached.", "其余关系已省略。", "其餘關係已省略。"), 10, y, width - 20, 28, 15);
                    y += limit.rectTransform.rect.height + 4;
                }
            }
            y += 4;
            var note = Text(Loc.T("These relationships describe installed files. Uninstall still requires a fresh safety check.",
                "此处显示已安装文件的关系；卸载前会重新核查。", "此處顯示已安裝檔案的關係；解除安裝前會重新核查。"), 10, y, width - 20, 22, 12, true);
            note.color = OverviewUi.Muted;
            y += note.rectTransform.rect.height + 4;
            OverviewUi.Place(body, 0, 0, width, y); content.sizeDelta = new Vector2(0, y);
            Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = resetScroll ? 1 : position;
            if (focus != null) {
                var replacement = body.GetComponentsInChildren<Selectable>(false).FirstOrDefault(s => s.gameObject.name == focus && s.IsInteractable());
                if (replacement != null) EventSystem.current?.SetSelectedGameObject(replacement.gameObject);
            }
            PublicationNavigation.Attach(panel);
        }
        // First and second levels are visible; deeper branches start collapsed.
        Render(true);
        var close = OverviewUi.Button(panel, Loc.T("Close", "关闭", "關閉"), OverviewUi.CloseModal, 115, 34);
        OverviewUi.Bottom((RectTransform)close.transform, panel.rect.width - 137, 16);
        int language = Loc.TranslationRevision;
        string context = OverviewDependencyGraphCache.Context; // 1.1.0（MCE）：依赖图缓存不在 SourceUi
        panel.gameObject.AddComponent<ComponentDetailsRefresh>().Tick = () => {
            if (context != OverviewDependencyGraphCache.Context) { OverviewUi.CloseModal(); return; }
            var current = SharedRelationshipSnapshot.Get(mods,Array.Empty<InfrastructureOverviewModel>(),OverviewDependencyGraphCache.Graph);
            if (ReferenceEquals(current, graph) && language == Loc.TranslationRevision) return;
            bool changedGraph = !ReferenceEquals(current, graph);
            graph = current; language = Loc.TranslationRevision;
            key=rootMod!=null ? SharedRelationshipSnapshot.RootId(rootMod,graph) : SharedRelationshipSnapshot.Canonical(graph,requestedKey);
            components=SharedRelationshipSnapshot.Components(graph);
            labels = new ComponentRelationshipLabels(graph, mods, components);
            if (changedGraph) { collapsed.Clear(); defaultExpansion = true; }
            heading.text = owner?.Name ?? labels?.Name(key) ?? key;
            Render();
        };
        PublicationNavigation.Attach(panel);
    }
}

internal sealed class ComponentDetailsRefresh : MonoBehaviour
{
    internal Action? Tick;
    private void Update() => Tick?.Invoke();
    private void OnDestroy() => Tick = null;
}
