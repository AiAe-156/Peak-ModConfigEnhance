#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModConfigEnhance.Overview;

// 1.1.0（MCE，概览页第 7 章）：照抄 IGMM 后裁剪：基础组件区只读。
// 去掉：来源显示上下文（SourceUi.PollDisplayContext / DisplayDependencyContext，依赖图缓存改 OverviewDependencyGraphCache）、
// 卸载保护锁（⊘，DependencyDiagnostic）、组件别名（Rule.alias）、更新角标（AvailableUpdateDot）、来源菜单（ComponentSourceMenu）、
// 标签筛选。组件名点击打开只读详情（ComponentDetailsView，没有启停 / 卸载）；主页钮在认得出「Team-Name」时开商店页（只拼链接）。
// 依赖图的「已确认文件」传空：MCE 没有来源确认，等同 IGMM 来源未确认时（第 6 章 OverviewDependencies 同一取舍）。
internal sealed partial class OverviewListView
{
    private IReadOnlyList<InfrastructureOverviewModel> _infrastructure = Array.Empty<InfrastructureOverviewModel>();
    private bool _infrastructureCollapsed, _infrastructureLoading, _infrastructureRedraw;
    private string _infrastructureError = "", _searchQuery = "";
    private int _infrastructureRevision = -1;

    private ModConfigEnhance.Updating.ComponentDependencyGraph? _infrastructureGraph;
    private readonly List<Action> _componentProtectionRefresh = new();
    private int _infrastructureGeneration;
    private void RefreshInfrastructure(bool force = false)
    {
        if (!force && !ListVisible) return;
        int revision = OverviewPageCache.Inventory;
        if (!force && revision == _infrastructureRevision) return;
        _infrastructureRevision = revision;
        int generation = ++_infrastructureGeneration;
        _infrastructureLoading = true; _infrastructureRedraw = false; _infrastructureGraph = null;
        OverviewDependencyGraphCache.Invalidate();
        foreach(var refresh in _componentProtectionRefresh)refresh();
        // Keep the displayed snapshot while scanning; publish both sections in one Render.
        string root = BepInEx.Paths.BepInExRootPath;
        var graphconfirmed = Array.Empty<ModConfigEnhance.Updating.ComponentDependencyCollector.ConfirmedFile>();
        _ = Task.Run(() => {
            var rows = InfrastructureOverviewModel.Create(InfrastructurePackages.Scan(root));
            _pump.Enqueue(() => {
                if (_disposed || generation != _infrastructureGeneration) return;
                if (revision != OverviewPageCache.Inventory) { RefreshInfrastructure(true); return; }
                _infrastructure = rows; _infrastructureLoading = false; _infrastructureError = "";
                _infrastructureRedraw = true;
                _status(Loc.T("Reading dependency graph…", "正在读取依赖关系……", "正在讀取相依關係……"));
            });
            return InfrastructureDependencyCollector.Collect(root, graphconfirmed);
        }).ContinueWith(task => {
            _pump.Enqueue(() => {
                if (_disposed || generation != _infrastructureGeneration) return;
                if (revision != OverviewPageCache.Inventory) { RefreshInfrastructure(true); return; }
                // Metadata owns the one structural publication. Graph completion never rebuilds rows.
                bool metadataFailed = _infrastructureLoading;
                _infrastructureLoading = false;
                if (task.IsFaulted || task.IsCanceled)
                {
                    _infrastructureError = task.Exception?.GetBaseException().Message ?? "Component scan cancelled";
                    if(metadataFailed) _infrastructureRedraw = true;
                }
                else {
                    _infrastructureGraph = task.Result; _infrastructureError = "";
                    OverviewDependencyGraphCache.Publish(task.Result, revision, _infrastructure);
                }
                _dependencyHints.HideNow(); _dependencyHints.InvalidateDisplay();
                foreach(var refresh in _componentProtectionRefresh)refresh();
                _status(_infrastructureError.Length == 0 ? "" : Loc.T("Could not read components: ", "组件读取失败：", "組件讀取失敗：") + _infrastructureError);
            });
        }, TaskScheduler.Default);
    }

    private void FlushInfrastructureRedraw()
    {
        if (!_infrastructureRedraw || _infrastructureLoading || !_opened || _building || IsEditingText || _dragged != null || _scroll.velocity.sqrMagnitude > 1) return;
        // Render captures/restores row focus; a selected row must not starve publication.
        Render(); // Only an accepted Render consumes the pending publication.
    }
    private void BindDependencyCard(GameObject target, InfrastructureOverviewModel component)
    {
        _dependencyHints.Bind(target, component, () => _infrastructureGraph, () => _infrastructure,
            () => _hint.gameObject.SetActive(false));
    }
    internal static string ComponentRuleId(InfrastructureOverviewModel package) => "component:" + package.Key;
    /// <summary>1.1.0（MCE）：认得出「Team-Name」的组件给雷霆商店页链接（只拼链接，不联网）；认不出返回空。</summary>
    private static string ComponentStorePage(InfrastructureOverviewModel package)
    {
        if (!package.IdentityConfirmed) return "";
        int split = package.Key.IndexOf('-');
        return split < 1 ? "" : OverviewCatalogue.StorePageUrl(package.Key.Substring(0, split), package.Key.Substring(split + 1));
    }
    private float BuildInfrastructure(float y, float width, float xEnable, float goalWidth, float xName, float nameWidth,
        float xSettings, float xHome, float xFolder, float xVersion, float versionWidth, float xAuthor, float authorWidth, float xDescription, bool compact)
    {
        _infrastructureRedraw = false;
        _componentProtectionRefresh.Clear();
        var rows = _infrastructure.Where(p => p.Matches(_searchQuery) && (_relatedMods == null || _relatedPackages.Contains(p.Key))).ToArray();
        if (_relatedMods != null && rows.Length == 0) return y;
        y += 2;
        bool collapsed = _infrastructureCollapsed && _searchQuery.Length == 0 && _relatedMods == null;
        AddVirtualRow("infrastructure-group:", "InfrastructureGroup", y, GroupHeight, width-15, header => {
        header.GetComponent<Image>().color=ModConfigLook.Surface(new Color(.12f,.18f,.21f,.95f));
        var arrow = OverviewUi.IconButton(header, collapsed ? "triangle-right" : "triangle-down", () => {
            _infrastructureCollapsed = !_infrastructureCollapsed; RenderFold();
        }, 22, 26);
        OverviewUi.Place((RectTransform)arrow.transform, 7, 4, 22, 26);
        var title = Label(header, InfrastructureOverviewModel.GroupTitle + " · " + Loc.F("Components: {0} · Locations: {1}", "组件 {0} 个 · 安装位置 {1} 处", "組件 {0} 個 · 安裝位置 {1} 處", rows.Length, rows.Sum(p => p.LocationCount)), 35, 0, width - 65, GroupHeight, 21);
        AddHint(title.gameObject, Loc.T("Packages are grouped across Core and Patcher. Hover for a summary; click a name for the full tree.",
            "同一资源包合并显示 Core 和 Patcher 位置。悬停看摘要，点击名称看完整依赖树。", "同一資源包合併顯示 Core 和 Patcher 位置。懸停看摘要，點擊名稱看完整依賴樹。"));
        });
        y += GroupHeight + GroupGapAfter;
        if (collapsed) return y;
        int index = 0;
        foreach (var package in rows)
        {
            const float height = RowHeight;
            int capturedIndex=index++;
            System.Collections.IEnumerator BuildComponentLayers(RectTransform row)
            {
            // Deliberately no checkbox, editable frame, drag target or OverviewMod identity.
            // 1.1.0（MCE）：只显示组件实际状态，不能启停（IGMM 在详情里改名启停）。
            var placeholder = Label(row, package.Enabled ? "✓" : "○", xEnable, 4, goalWidth, 32, 20); placeholder.alignment = TextAlignmentOptions.Midline;
            placeholder.color = package.Enabled ? OverviewUi.Green : OverviewUi.Muted;
            AddHint(placeholder.gameObject, (package.Enabled ? Loc.T("Enabled", "已启用", "已啟用") : Loc.T("Disabled", "已停用", "已停用"))
                + " · " + Loc.T("Read only. Click the name for its dependency tree.", "只读；点名称看依赖树。", "唯讀；點名稱看依賴樹。"));
            void Menu() => ComponentDetailsView.Open(_repo, _mods, package, package.Key, OverviewDependencyGraphCache.Graph, _infrastructure);
            float titleWidth = Mathf.Max(20, nameWidth - 24);
            var name = Label(row, EscapeRich(package.Name), xName, 4, titleWidth, 34, 19); name.richText = true;
            OverviewUi.PlaceClipped(name, xName, 4, titleWidth, 34);
            BindDependencyCard(name.gameObject, package); name.gameObject.AddComponent<OverviewTextClick>().Clicked = _ => Menu();
            var version = Label(row, EscapeRich(package.Version.Length > 0 ? package.Version : Loc.T("Unknown version", "版本未知", "版本未知")), xVersion, 4,
                versionWidth - 7, 34, 20);
            string versionHint=EscapeRich(package.TypeTitle+" · "+package.Version)
                // 2.4.4: list ignored lower-version manifest.json copies.
                +(package.StaleManifests.Count > 0 ? "\n"+Loc.T("Old manifest.json copies (ignored for the version):", "旧说明文件残留（不参与版本判断）：", "舊說明檔案殘留（不參與版本判斷）：")
                    +"\n"+string.Join("\n", package.StaleManifests.Select(EscapeRich)) : "");
            AddHint(version.gameObject, versionHint);
            version.gameObject.AddComponent<OverviewTextClick>().Clicked = _ => Menu();
            var author = Label(row, EscapeRich(package.Author), xAuthor, 4, Mathf.Max(1, authorWidth - 8), 34, 18); author.gameObject.SetActive(!compact); AddHint(author.gameObject, EscapeRich(package.Author));
            string description = package.TypeTitle + (!package.IdentityConfirmed ? " · " + Loc.T("Identity unconfirmed", "身份未确认", "身分未確認")
                : package.Description.Length > 0 ? " · " + InfrastructureOverviewModel.ShortText(package.Description) : "");
            var desc = Label(row, EscapeRich(description), xDescription, 4, Mathf.Max(40, width - xDescription - 28), 34, 16); desc.color = OverviewUi.Muted; desc.gameObject.SetActive(!compact); AddHint(desc.gameObject, EscapeRich(description));
            yield return null;
            OverviewRowActions.Settings(row, xSettings + 2, (row.rect.height - OverviewRowActions.Size) / 2, () => { }, false,
                Loc.T("No ModConfig settings.", "没有 ModConfig 设置项。", "沒有 ModConfig 設定項。"), AddHint);
            yield return null;
            string store = ComponentStorePage(package);
            OverviewRowActions.Home(row, xHome + 2, (row.rect.height - OverviewRowActions.Size) / 2,
                () => { if (store.Length > 0) { Application.OpenURL(store); _status(Loc.T("Opened: ", "已打开：", "已開啟：") + store); } }, store.Length > 0 ? store : null,
                store.Length > 0 ? Loc.T("Click to open its store page:\n", "点击打开它的商店页：\n", "點一下開啟它的商店頁面：\n") + store
                    : Loc.T("Package identity unconfirmed; no store page.", "包身份未确认，没有商店页。", "套件身分未確認，沒有商店頁面。"), AddHint); // 实测修正：颜色按网址种类
            yield return null;
            Button? folder = null;
            folder = OverviewRowActions.Folder(row, xFolder + 2, (row.rect.height - OverviewRowActions.Size) / 2, () => {
                if (folder != null) FolderShortcuts.Open(package, (RectTransform)folder.transform, _status);
            }, package.LocationCount > 0, EscapeRich(string.Join("\n", package.Locations.Select(l => l.Area + " · " + l.Path))), AddHint);
            }
            AddLayeredRow("component:"+package.Key,"Infrastructure_"+capturedIndex,y,height-2,width-15,BuildComponentLayers);
            y += height;
        }
        if (rows.Length == 0)
        {
            string message = _infrastructureLoading ? Loc.T("Reading components…", "正在读取组件……", "正在讀取組件……")
                : _infrastructureError.Length > 0 ? Loc.T("Could not read components: ", "组件读取失败：", "組件讀取失敗：") + _infrastructureError
                : Loc.T("No matching components.", "没有匹配的组件。", "沒有符合的組件。");
            var empty = Label(_content, message, 12, y, width - 40, 36, 17); AddHint(empty.gameObject, EscapeRich(message)); y += 38;
        }
        return y;
    }
}
