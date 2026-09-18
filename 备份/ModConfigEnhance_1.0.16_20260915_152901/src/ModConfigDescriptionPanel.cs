#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.Configuration;
using PEAKLib.ModConfig;
using PEAKLib.ModConfig.Components;
using PEAKLib.UI;
using PEAKLib.UI.Elements;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zorro.Settings;

namespace ModConfigEnhance;

/// <summary>
/// 模组设置页右侧顶部的「选项说明」面板：显示鼠标悬停（或手柄焦点）所在选项的 cfg 描述，
/// 也就是配置文件里每项上方 <c>##</c> 后面那段文字，外加一行「默认 / 范围 / 可选」。
///
/// ModConfig 把 ConfigEntry 原样带进了每个单元格（输入控件 Setup 时登记的 Setting 实现了
/// IBepInExProperty，ConfigBase 就是 BepInEx 的 ConfigEntryBase），所以七种类型都走同一条路反查，
/// 不用逐类型打补丁，也不改它任何数据。
///
/// 版面：固定高；宽度与底图都从**实际生成的第一个单元格**实时量取（2.16.0）—— 左右边与单元格对齐，
/// sprite / 材质 / 颜色照抄它的底图，圆角与褶皱质感一致，昼夜变色也跟着它走。预制体的宽度不可信（2.16.0 首版
/// 拿它算出来超出屏幕），单元格生成前先用整宽纯色顶着。
/// 描述装不下时可滚轮滚动，右侧一条细滚动条，底边用 RectMask2D 的软裁剪做渐隐。
/// 装了 TimeTheme 时底色随昼夜；没装就保持页签同款棕色。
/// KeyCode 型选项不列「可选」：一百多个键名没意义，屏幕上是按键图标。
/// </summary>
internal static class ModConfigDescriptionPanel
{
    /// <summary>2.15.0 是 88（描述两行 + 元信息一行）；2.16.0 翻倍到 176，能放五六行说明。</summary>
    internal const float PanelHeight = 176f;
    private const float PanelTop = 8f;
    private const float Padding = 8f;
    private const float PaddingLeft = 14f;
    private const float DescriptionFontSize = 20f;
    private const float MetaFontSize = 16f;
    private const int FadeSoftness = 26;
    private const float ScrollbarWidth = 6f;

    private static readonly Color MetaColor = new Color(0.75f, 0.75f, 0.75f, 1f);
    private static readonly Color WarningColor = new Color(1f, 0.55f, 0.2f, 1f);

    internal static void Attach(ModSettingsMenu menu, RectTransform scrollArea)
    {
        // 选项列表让出面板的高度
        scrollArea.offsetMax = new Vector2(scrollArea.offsetMax.x, -(PanelTop + PanelHeight + Padding));

        GameObject root = new GameObject("MCE_DescPanel", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.SetParent(menu.transform, false);
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        // 先整宽纯色；第一个单元格出现后由 Panel.LateUpdate 对齐它的左右边、照抄它的底图
        rootRect.offsetMin = new Vector2(scrollArea.offsetMin.x, -(PanelTop + PanelHeight));
        rootRect.offsetMax = new Vector2(scrollArea.offsetMax.x, -PanelTop);
        Image background = root.GetComponent<Image>();
        background.color = ModConfigTreeLayoutPatch.TabBackground;
        background.raycastTarget = true; // 滚轮事件要落在 ScrollRect 自己的对象上
        ModConfigSidebarWidgets.ApplyRounded(background, 10f);

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        RectTransform viewportRect = (RectTransform)viewport.transform;
        viewportRect.SetParent(rootRect, false);
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(PaddingLeft, Padding);
        viewportRect.offsetMax = new Vector2(-(Padding + ScrollbarWidth + 6f), -Padding);

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform contentRect = (RectTransform)content.transform;
        contentRect.SetParent(viewportRect, false);
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;
        VerticalLayoutGroup group = content.GetComponent<VerticalLayoutGroup>();
        group.padding = new RectOffset(0, 0, 0, 0);
        group.spacing = 2f;
        group.childAlignment = TextAnchor.UpperLeft;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = true;
        group.childForceExpandHeight = false;
        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI title = MakeText(contentRect, "Title", MetaFontSize, MetaColor);
        TextMeshProUGUI description = MakeText(contentRect, "Description", DescriptionFontSize, Color.white);
        TextMeshProUGUI meta = MakeText(contentRect, "Meta", MetaFontSize, MetaColor);

        ScrollRect scroll = root.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        scroll.inertia = false;

        // 右侧细滚动条（装得下时自动隐藏）
        Scrollbar bar = ModConfigSidebarWidgets.MakeScrollbar(rootRect, scroll, 0f, ScrollbarWidth, Padding, Padding);
        RectTransform barRect = (RectTransform)bar.transform;
        barRect.anchorMin = new Vector2(1f, 0f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(1f, 1f);
        barRect.offsetMin = new Vector2(-(Padding + ScrollbarWidth), Padding);
        barRect.offsetMax = new Vector2(-Padding, -Padding);

        root.AddComponent<ModConfigSidebarWidgets.ThemeAsOption>(); // 白字夜里变紫

        Panel panel = root.AddComponent<Panel>();
        panel.Menu = menu;
        panel.Scroll = scroll;
        panel.Mask = viewport.GetComponent<RectMask2D>();
        panel.Viewport = viewportRect;
        panel.Content = contentRect;
        panel.Title = title;
        panel.Description = description;
        panel.Meta = meta;
        panel.Background = background;
        Current = panel;
    }

    /// <summary>最近一次构建的面板（主菜单与 ESC 各一份，谁在场谁是它）；导出功能借它显示结果。</summary>
    internal static Panel? Current { get; private set; }

    private static TextMeshProUGUI MakeText(RectTransform parent, string name, float fontSize, Color color)
    {
        PeakText peak = MenuAPI.CreateText("", name).ParentTo(parent);
        TextMeshProUGUI text = peak.TextMesh;
        text.enableAutoSizing = false;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Normal;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.richText = false;
        text.color = color;
        text.raycastTarget = false;
        text.text = "";
        return text;
    }

    internal sealed class Panel : MonoBehaviour
    {
        internal ModSettingsMenu? Menu;
        internal ScrollRect? Scroll;
        internal RectMask2D? Mask;
        internal RectTransform? Viewport;
        internal RectTransform? Content;
        internal TextMeshProUGUI? Title;
        internal TextMeshProUGUI? Description;
        internal TextMeshProUGUI? Meta;
        internal Image? Background;

        private SettingsUICell? _shownCell;
        private SettingsUICell? _referenceCell;
        private Image? _referenceImage;
        private bool _fitLogged;
        private bool _holdNotice;
        private SettingsUICell? _noticeHovered;
        private SettingsUICell? _noticeFocused;
        private ModdedTABSButton? _overviewMod;
        private ModdedTABSButton? _overviewSection;
        private bool _overviewInitialized;


        private void OnEnable()
        {
            Current = this;
            _overviewInitialized = false;
            _shownCell = null;
        }

        private void ShowOverview(ModdedTABSButton? mod, ModdedTABSButton? section)
        {
            if (mod == null)
            {
                ShowNotice(Loc.T("Select a mod to view its information.", "选择左侧模组查看信息。", "選擇左側模組查看資訊。"));
                return;
            }

            string name = mod.category ?? "";
            string version = "";
            string location = "";
            if (Menu?.settings != null)
            {
                foreach (IBepInExProperty item in Menu.settings)
                {
                    if (!string.Equals(item.GetCategory(), name, StringComparison.Ordinal)) continue;
                    version = item.Pluginfo?.Metadata?.Version?.ToString() ?? "";
                    location = item.Pluginfo?.Location ?? "";
                    break;
                }
            }

            string category = section?.category ?? Loc.T("All", "全部", "全部");
            string categoryText = Loc.T("Category: ", "当前子分类：", "目前子分類：") + category;
            string description = ReadModDescription(location);
            ShowNotice(description);
            // 介绍独占一个文本组件，和导出的原文完全一致，才能命中 XUnity 的精确词典。
            SetTexts((version.Length == 0 ? name : name + "  v" + version) + "\n" + categoryText, description, "");

            StyleTitle();
        }

        [Serializable]
        private sealed class ManifestInfo
        {
            public string description = "";
        }

        internal static string ReadModDescription(string pluginLocation, bool showFallback = true)
        {
            try
            {
                if (!string.IsNullOrEmpty(pluginLocation))
                {
                    string root = Path.GetFullPath(BepInEx.Paths.PluginPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    DirectoryInfo? directory = Directory.GetParent(Path.GetFullPath(pluginLocation));
                    // 从 DLL 所在目录向上找最近的包清单，不越出 plugins，也不借用其它模组的说明。
                    while (directory != null && directory.FullName.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        string manifest = Path.Combine(directory.FullName, "manifest.json");
                        if (File.Exists(manifest))
                        {
                            ManifestInfo? info = JsonUtility.FromJson<ManifestInfo>(File.ReadAllText(manifest, Encoding.UTF8));
                            if (!string.IsNullOrWhiteSpace(info?.description)) return info!.description.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
                            break;
                        }
                        directory = directory.Parent;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[说明面板] 读取模组 manifest.json 失败: {ex.Message}");
            }

            return showFallback ? Loc.T("No description provided in manifest.json.", "未提供 manifest.json 模组说明。", "未提供 manifest.json 模組說明。") : "";
        }

        private void StyleTitle()
        {
            if (Title != null)
            {
                Color target = TimeThemeBridge.IsDark ? TimeThemeBridge.DarkTabText : MetaColor;
                if (Title.color != target) Title.color = target;
            }
        }

        /// <summary>导出等功能借面板显示一条结果，下次悬停到别的选项时自然被替换。</summary>
        internal void ShowNotice(string text, bool warning = false)
        {

            StyleTitle();
            _shownCell = null;
            _noticeHovered = FindHoveredCell();
            _noticeFocused = FindFocusedCell();
            _holdNotice = true;
            SetNoticeColor(warning);
            SetTexts("", text, "");
        }

        private bool _warningShown;

        /// <summary>警告用橙色；恢复白色后再让 TimeTheme 刷一遍（夜里白字要变紫）。</summary>
        private void SetNoticeColor(bool warning)
        {
            if (Description == null || warning == _warningShown)
            {
                return;
            }

            _warningShown = warning;
            if (warning)
            {
                Description.color = WarningColor;
            }
            else
            {
                Description.color = Color.white;
                TimeThemeBridge.ApplyOptionsTheme(transform, TimeThemeBridge.IsDark);
            }
        }

        private void Update()
        {
            if (Menu == null || Description == null)
            {
                return;
            }

            ModdedTABSButton? selectedMod = Menu.ModTabs != null ? Menu.ModTabs.selectedButton : null;
            ModdedTABSButton? selectedSection = null;
            if (Menu.SectionTabController != null)
            {
                foreach (GameObject row in Menu.SectionTabController.Tabs)
                {
                    ModdedTABSButton? button = row != null ? row.GetComponent<ModdedTABSButton>() : null;
                    if (button != null && button.Selected) { selectedSection = button; break; }
                }
            }

            if (!_overviewInitialized || selectedMod != _overviewMod || selectedSection != _overviewSection)
            {
                _overviewInitialized = true;
                _overviewMod = selectedMod;
                _overviewSection = selectedSection;
                ShowOverview(selectedMod, selectedSection);
            }

            SettingsUICell? mouseCell = FindHoveredCell();
            SettingsUICell? focusedCell = FindFocusedCell();
            if (_holdNotice)
            {
                // 留下完整加载结果供阅读；旧手柄焦点不能在下一帧把它顶掉。
                bool newHover = mouseCell != null && mouseCell != _noticeHovered;
                bool newFocus = focusedCell != null && focusedCell != _noticeFocused;
                _noticeHovered = mouseCell;
                _noticeFocused = focusedCell;
                if (!newHover && !newFocus)
                {
                    return;
                }

                _holdNotice = false;
            }

            SettingsUICell? hovered = mouseCell ?? focusedCell;
            if (hovered == null || hovered == _shownCell)
            {
                return;
            }

            _shownCell = hovered;

            StyleTitle();
            SetNoticeColor(false);
            IBepInExProperty? property = ResolveProperty(hovered);
            if (property == null)
            {
                SetTexts("", "", "");
                return;
            }

            SetTexts(FormatHeader(property), FormatDescription(property.ConfigBase), FormatMeta(property.ConfigBase));
        }

        private void LateUpdate()
        {
            FitToCells();
            StyleTitle();
            if (Mask == null || Viewport == null || Content == null)
            {
                return;
            }

            // 只有装不下时才渐隐；装得下的短描述干净显示
            bool overflow = Content.rect.height > Viewport.rect.height + 0.5f;
            Vector2Int softness = overflow ? new Vector2Int(0, FadeSoftness) : Vector2Int.zero;
            if (Mask.softness != softness)
            {
                Mask.softness = softness;
            }
        }

        /// <summary>
        /// 对齐第一个活着的单元格：左右边取它在本面板父级坐标系里的位置；底图 sprite / 材质 / 类型照抄它最大的那张
        /// 背景 Image，颜色每帧同步（TimeTheme 会改它的颜色，跟着走就自动昼夜）。单元格换了才重新量。
        /// </summary>
        private void FitToCells()
        {
            if (Menu == null || Background == null || transform.parent is not RectTransform parent)
            {
                return;
            }

            SettingsUICell? cell = null;
            foreach (SettingsUICell candidate in Menu.m_spawnedCells)
            {
                if (candidate != null && candidate.gameObject.activeInHierarchy)
                {
                    cell = candidate;
                    break;
                }
            }

            if (cell == null)
            {
                return;
            }

            if (cell != _referenceCell)
            {
                _referenceCell = cell;
                _referenceImage = FindBackdrop(cell);
                if (_referenceImage != null && _referenceImage.sprite != null)
                {
                    ModConfigPaperStyle.CopyFrom(_referenceImage, Background);
                }

                // 单元格刚生成那一帧布局还没排（Canvas 在 LateUpdate 之后才重排），世界坐标还是预制体的旧位置；
                // 换了参考单元格就先把它所在的列表布局强制算一遍，量出来的才是最终位置，不会闪一帧
                if (Menu != null && Menu.Content is RectTransform content)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                }
            }

            try
            {
                Vector3[] corners = new Vector3[4];
                ((RectTransform)cell.transform).GetWorldCorners(corners);
                float left = parent.InverseTransformPoint(corners[0]).x - parent.rect.xMin;
                float right = parent.InverseTransformPoint(corners[2]).x - parent.rect.xMax;
                RectTransform rect = (RectTransform)transform;
                if (Mathf.Abs(rect.offsetMin.x - left) > 0.5f || Mathf.Abs(rect.offsetMax.x - right) > 0.5f)
                {
                    rect.offsetMin = new Vector2(left, rect.offsetMin.y);
                    rect.offsetMax = new Vector2(right, rect.offsetMax.y);
                }

                if (!_fitLogged)
                {
                    _fitLogged = true;
                    Plugin.Log.LogDebug($"[面板布局] 说明面板已对齐单元格：left {left:0.#} right {right:0.#}，底图 {(_referenceImage != null ? _referenceImage.name + "/" + (_referenceImage.sprite != null ? _referenceImage.sprite.name : "无 sprite") : "未找到")}");
                }
            }
            catch (Exception ex)
            {
                if (!_fitLogged)
                {
                    _fitLogged = true;
                    Plugin.Log.LogWarning($"[面板布局] 说明面板对齐单元格失败: {ex.Message}");
                }
            }

            if (_referenceImage != null && Background.color != _referenceImage.color)
            {
                Background.color = _referenceImage.color;
            }
        }

        /// <summary>单元格自己的底图：不在选项内容容器里、面积最大的那张 Image。</summary>
        private static Image? FindBackdrop(SettingsUICell cell)
        {
            Image? best = null;
            float bestArea = 0f;
            foreach (Image image in cell.GetComponentsInChildren<Image>(true))
            {
                if (cell.m_settingsContentParent != null && image.transform.IsChildOf(cell.m_settingsContentParent))
                {
                    continue;
                }

                Rect r = image.rectTransform.rect;
                float area = r.width * r.height;
                if (area > bestArea)
                {
                    bestArea = area;
                    best = image;
                }
            }

            return best;
        }

        private void SetTexts(string title, string description, string meta)
        {
            if (Title != null)
            {
                Title.text = title;
                Title.gameObject.SetActive(title.Length > 0);
            }

            if (Description != null)
            {
                Description.text = description;
                Description.gameObject.SetActive(description.Length > 0);
            }

            if (Meta != null)
            {
                Meta.text = meta;
                Meta.gameObject.SetActive(meta.Length > 0);
            }

            if (Scroll != null)
            {
                Canvas.ForceUpdateCanvases();
                Scroll.verticalNormalizedPosition = 1f;
            }
        }

        private SettingsUICell? FindHoveredCell()
        {
            if (Menu == null)
            {
                return null;
            }

            Camera? eventCamera = null;
            Canvas? canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                eventCamera = canvas.worldCamera;
            }

            Vector2 mouse = Input.mousePosition;
            foreach (SettingsUICell cell in Menu.m_spawnedCells)
            {
                if (cell == null || !cell.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)cell.transform, mouse, eventCamera))
                {
                    return cell;
                }
            }

            return null;
        }

        private static SettingsUICell? FindFocusedCell()
        {
            EventSystem? system = EventSystem.current;
            GameObject? selected = system != null ? system.currentSelectedGameObject : null;
            return selected != null ? selected.GetComponentInParent<SettingsUICell>() : null;
        }

        private static IBepInExProperty? ResolveProperty(SettingsUICell cell)
        {
            try
            {
                if (cell.m_settingsContentParent == null)
                {
                    return null;
                }

                SettingInputUICell? input = cell.m_settingsContentParent.GetComponentInChildren<SettingInputUICell>(true);
                Setting? setting = input != null ? input._listeningSetting : null;
                return setting as IBepInExProperty;
            }
            catch
            {
                return null;
            }
        }
    }

    // ── 文案 ────────────────────────────────────────────────────────

    /// <summary>标题行：模组显示名（GetCategory = 列表里的行名）+ 版本号；拿不到版本就只留名字。</summary>
    private static string FormatHeader(IBepInExProperty property)
    {
        string name;
        try
        {
            name = property.GetCategory() ?? "";
        }
        catch
        {
            name = "";
        }

        string version = "";
        try
        {
            version = property.Pluginfo?.Metadata?.Version?.ToString() ?? "";
        }
        catch
        {
        }

        return version.Length > 0 ? name + "  v" + version : name;
    }

    internal static string FormatDescription(ConfigEntryBase entry)
    {
        string? text = entry.Description?.Description;
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        return text!.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
    }

    internal static string FormatMeta(ConfigEntryBase entry)
    {
        StringBuilder sb = new StringBuilder();
        string? defaultText = FormatValue(entry.DefaultValue, entry.SettingType);
        if (defaultText != null)
        {
            sb.Append(Loc.Get("META_DEFAULT")).Append(": ").Append(defaultText);
        }

        AcceptableValueBase? acceptable = entry.Description?.AcceptableValues;
        if (acceptable != null)
        {
            Type type = acceptable.GetType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(AcceptableValueRange<>))
            {
                object? min = type.GetProperty("MinValue")?.GetValue(acceptable);
                object? max = type.GetProperty("MaxValue")?.GetValue(acceptable);
                Separator(sb).Append(Loc.Get("META_RANGE")).Append(": ")
                    .Append(FormatValue(min, entry.SettingType)).Append(" ~ ").Append(FormatValue(max, entry.SettingType));
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(AcceptableValueList<>))
            {
                if (type.GetProperty("AcceptableValues")?.GetValue(acceptable) is Array values && values.Length > 0)
                {
                    List<string> items = new List<string>();
                    foreach (object? value in values)
                    {
                        items.Add(FormatValue(value, entry.SettingType) ?? "");
                    }

                    Separator(sb).Append(Loc.Get("META_OPTIONS")).Append(": ").Append(string.Join(" / ", items));
                }
            }
        }
        else if (entry.SettingType.IsEnum && entry.SettingType != typeof(KeyCode))
        {
            Separator(sb).Append(Loc.Get("META_OPTIONS")).Append(": ").Append(string.Join(" / ", Enum.GetNames(entry.SettingType)));
        }

        return sb.ToString();
    }

    private static StringBuilder Separator(StringBuilder sb)
    {
        return sb.Length > 0 ? sb.Append("    ") : sb;
    }

    private static string? FormatValue(object? value, Type settingType)
    {
        if (value == null)
        {
            return null;
        }

        if (value is bool flag)
        {
            return flag ? Loc.Get("META_ON") : Loc.Get("META_OFF");
        }

        if (value is float f)
        {
            return f.ToString("0.###");
        }

        if (value is double d)
        {
            return d.ToString("0.###");
        }

        string text = value.ToString() ?? "";
        return text.Length == 0 && settingType == typeof(string) ? Loc.Get("META_EMPTY") : text;
    }
}
