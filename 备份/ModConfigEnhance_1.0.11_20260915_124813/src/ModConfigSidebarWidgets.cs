#nullable enable
using System;
using System.Collections.Generic;
using PEAKLib.ModConfig.Components;
using PEAKLib.UI;
using PEAKLib.UI.Elements;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModConfigEnhance;

/// <summary>
/// 树形侧栏上自己画的小部件（2.16.0）：
///   · 复选框：深色圆角方块 + 米白描边，未勾描边半透明、勾上描边点亮并压一个天蓝 ✓（两根旋转的细条，不依赖字体）；
///     ✓ 的蓝（白天 #C2E4FF / 夜间 #238CE0）不在 TimeTheme 表里，由 <see cref="ThemeTint"/> 自己按昼夜切；
///   · 置顶的行左缘一条同色竖条；
///   · 列表右侧的竖向滚动条：细、圆角，深棕轨道 + 米白滑块，内容装得下时自动隐藏；
///   · 圆角来自运行时画的一张 32×32 圆角 sprite（九宫格），不依赖游戏资源；
///   · 列表上方的工具行：导出翻译 / 查漏翻 / 「仅导出已选」开关；
///   · 列表首行的表头：两列复选框的标题（导出 / 置顶）+ 右侧的「只显示快捷键选项」开关；
///   · 每个模组行左侧的两列复选框：导出列（只在「仅导出已选」勾上时显示）、置顶列（勾上的行排到列表最上面）。
/// 颜色全部取自 TimeTheme 配色表里已有的那几格（米白 ↔ 夜间紫、页签棕 ↔ 黑），
/// 于是 <see cref="TimeThemeBridge.ApplyTheme"/> 扫一遍就自动跟昼夜，不用另写夜间分支；没装 TimeTheme 就是原版米白。
/// 置顶集合、不导出集合、「仅导出已选」开关存在本模组 cfg 里（带 Hidden 标签，ModConfig 界面不显示），
/// 键是 ModConfig 给模组行的名字（它的 FixNaming 结果），与导出文件里的模组名一致。
/// </summary>
internal static class ModConfigSidebarWidgets
{
    internal const float CheckBoxSize = 22f;
    internal const float ColumnWidth = 30f;
    internal const float ToolbarHeight = 40f;
    internal const float ToolbarRowGap = 6f;
    internal const int ToolbarRows = 3;

    /// <summary>工具行 + 表头这一块的文字统一 13 号。</summary>
    private const float HeaderFontSize = 13f;

    /// <summary>复选框点击区比方块大一圈（28），在 30 宽的列里居中要往右挪 1。</summary>
    private const float ColumnInset = (ColumnWidth - (CheckBoxSize + 6f)) / 2f;
    private const float ToolbarGap = 6f;
    private const float ToolbarButtonWidth = 92f;

    /// <summary>TimeTheme 主表第 19 组的白天色（米白 #DFDAC2），夜间对应紫 (0.65,0.38,0.75)。</summary>
    internal static readonly Color Cream = new Color(0.8745098f, 0.854902f, 0.7607843f, 1f);
    private const float UncheckedBorderAlpha = 0.45f;

    /// <summary>✓ 与置顶竖条的颜色：白天低饱和天蓝，夜间深一档的蓝（用户定）。</summary>
    internal static readonly Color MarkDay = new Color(0.7607843f, 0.8941177f, 1f, 1f);
    internal static readonly Color MarkNight = new Color(0.1372549f, 0.5490196f, 0.8784314f, 1f);

    private const int SpriteSize = 32;
    private const int SpriteRadius = 10;
    private const int SpriteBorder = 12;

    private const string PinKey = "MCE_PIN";
    private const string ExportColumnKey = "MCE_EXPORT_COL";
    private const string ExportSelectedKey = "MCE_EXPORT_SELECTED";
    private const string KeysOnlyKey = "MCE_KEYS_ONLY";

    private static bool _borderLogged;

    private static Sprite? _roundedSprite;

    // ── 持久化 ──────────────────────────────────────────────────────

    internal static readonly HashSet<string> Pinned = new HashSet<string>(StringComparer.Ordinal);
    internal static readonly HashSet<string> ExcludedFromExport = new HashSet<string>(StringComparer.Ordinal);

    private const char Separator = '|';

    internal static bool ExportSelectedOnly
    {
        get => Plugin.ExportSelectedOnly != null && Plugin.ExportSelectedOnly.Value;
        set
        {
            if (Plugin.ExportSelectedOnly != null)
            {
                Plugin.ExportSelectedOnly.Value = value;
            }
        }
    }

    /// <summary>导出列当前是否可见：开关记得的状态 × 完整工具行在场（只留语言行或工具行全关时连列一起收，此时它唯一的开关也看不到）。</summary>
    internal static bool ExportColumnVisible =>
        ExportSelectedOnly && Plugin.EnableTranslationExport.Value && !Plugin.LanguageRowOnly.Value;

    internal static void LoadState()
    {
        Pinned.Clear();
        ExcludedFromExport.Clear();
        Split(Plugin.PinnedMods?.Value, Pinned);
        Split(Plugin.ExportExcludedMods?.Value, ExcludedFromExport);
    }

    private static void Split(string? raw, HashSet<string> into)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return;
        }

        foreach (string part in raw!.Split(Separator))
        {
            string name = part.Trim();
            if (name.Length > 0)
            {
                into.Add(name);
            }
        }
    }

    private static void SavePinned()
    {
        if (Plugin.PinnedMods != null)
        {
            Plugin.PinnedMods.Value = string.Join(Separator.ToString(), Pinned);
        }
    }

    private static void SaveExcluded()
    {
        if (Plugin.ExportExcludedMods != null)
        {
            Plugin.ExportExcludedMods.Value = string.Join(Separator.ToString(), ExcludedFromExport);
        }
    }

    /// <summary>导出时是否包含这个模组（按 ModConfig 的模组行名字）。</summary>
    internal static bool ShouldExport(string modName)
    {
        return !ExportSelectedOnly || !ExcludedFromExport.Contains(modName);
    }

    // ── 词条 ────────────────────────────────────────────────────────

    /// <summary>每次建页都重注册（语言表整表重载后词条会丢；MenuAPI 对已有键只是覆盖同值）。</summary>
    private static void EnsureTerms() => Loc.EnsureRegistered();

    // ── 圆角底图 ────────────────────────────────────────────────────

    /// <summary>
    /// 运行时画一张 32×32、圆角半径 10 的白色圆角矩形（边缘一像素抗锯齿），九宫格边距 12。
    /// 之前借搜索框 sprite 的路子拿到的是 null（输入框的 targetGraphic 不是那张底图），改为自绘，稳定且任意尺寸可用。
    /// </summary>
    internal static Sprite RoundedSprite
    {
        get
        {
            if (_roundedSprite == null)
            {
                _roundedSprite = BuildRoundedSprite();
            }

            return _roundedSprite;
        }
    }

    private static Sprite BuildRoundedSprite()
    {
        Texture2D texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "MCE_Rounded",
        };
        Color32[] pixels = new Color32[SpriteSize * SpriteSize];
        float r = SpriteRadius;
        for (int y = 0; y < SpriteSize; y++)
        {
            for (int x = 0; x < SpriteSize; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                // 到圆角矩形边界的有符号距离：角落按圆算，其余按直边算
                float cx = Mathf.Clamp(px, r, SpriteSize - r);
                float cy = Mathf.Clamp(py, r, SpriteSize - r);
                float d = r - Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                float alpha = Mathf.Clamp01(d + 0.5f);
                pixels[y * SpriteSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        UnityEngine.Object.DontDestroyOnLoad(texture);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, SpriteSize, SpriteSize), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(SpriteBorder, SpriteBorder, SpriteBorder, SpriteBorder));
        sprite.name = "MCE_Rounded";
        UnityEngine.Object.DontDestroyOnLoad(sprite);
        return sprite;
    }

    /// <summary>给 Image 套上圆角 sprite，圆角在屏幕上约 cornerPixels 像素（九宫格边距 12 ÷ 倍率）。</summary>
    internal static void ApplyRounded(Image image, float cornerPixels)
    {
        image.sprite = RoundedSprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = SpriteBorder / Mathf.Max(1f, cornerPixels);
    }

    /// <summary>paper=true 用原版纸面（带抖动），false 用自绘圆角（滚动条这种细条纸面糊不出形状）。</summary>
    private static Image MakeBox(string name, Transform parent, Color color, float cornerPixels, bool paper = true)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        if (paper)
        {
            ModConfigPaperStyle.Apply(image, cornerPixels);
        }
        else
        {
            ApplyRounded(image, cornerPixels);
        }

        return image;
    }

    /// <summary>不在 TimeTheme 表里的颜色自己按昼夜切（✓、置顶竖条）。IsDark 每帧有缓存，Update 很便宜。</summary>
    internal sealed class ThemeTint : MonoBehaviour
    {
        internal Graphic? Target;
        internal Color Day;
        internal Color Night;
        private bool? _dark;

        private void OnEnable()
        {
            _dark = null;
        }

        private void Update()
        {
            bool dark = TimeThemeBridge.IsDark;
            if (_dark == dark || Target == null)
            {
                return;
            }

            _dark = dark;
            Color target = dark ? Night : Day;
            Target.color = new Color(target.r, target.g, target.b, Target.color.a);
        }
    }

    internal static ThemeTint Tint(Graphic graphic, Color day, Color night)
    {
        ThemeTint tint = graphic.gameObject.AddComponent<ThemeTint>();
        tint.Target = graphic;
        tint.Day = day;
        tint.Night = night;
        graphic.color = new Color(day.r, day.g, day.b, graphic.color.a);
        return tint;
    }

    /// <summary>
    /// 自身模组行的行名色：白天低饱和橙（用户定的 #EFC95F）、夜间暖金（#C9A55B，压得住深底又与夜间紫字同区）；
    /// 选中态换深琥珀（#8A6207，白天白底、夜里紫底都压得住，不回黑）。
    /// <c>ModdedTABSButton.Update</c> 每帧把文字往黑 / 白 Lerp，所以本组件必须在 LateUpdate 里每帧断言颜色。
    /// 分区行也挂它（仅当自身模组被选中时，见 UpdateSectionTabsPostfix）。
    /// </summary>
    internal sealed class SelfRowTint : MonoBehaviour
    {
        internal static readonly Color Day = new Color(0.9372549f, 0.7882353f, 0.372549f);      // #EFC95F
        internal static readonly Color Night = new Color(0.7882353f, 0.6470588f, 0.3568628f);    // #C9A55B
        internal static readonly Color Selected = new Color(0.5411765f, 0.3843137f, 0.027451f);  // #8A6207

        internal TextMeshProUGUI? Text;
        internal ModdedTABSButton? Button;

        private void LateUpdate()
        {
            if (Text == null)
            {
                return;
            }

            bool selected = Button != null && Button.Selected;
            Color target = selected ? Selected : (TimeThemeBridge.IsDark ? Night : Day);
            if (Text.color != target)
            {
                Text.color = target;
            }
        }
    }

    // ── 复选框 ──────────────────────────────────────────────────────

    /// <summary>
    /// 结构：Button 根（透明、接点击）→ Border（米白圆角，做描边）→ Box（深色圆角，内缩 2）→ Mark（两根天蓝细条拼成 ✓）。
    /// 未勾：描边 45% 透明、无 ✓；勾上：描边不透明、✓ 显示。深色与米白都是 TimeTheme 表里的格，夜里自动变黑 / 紫；
    /// ✓ 的蓝由 ThemeTint 切。任何底色（含选中行的白底）上深色方块都看得见。
    /// </summary>
    internal sealed class CheckBox : MonoBehaviour
    {
        internal Image? Border;
        internal GameObject? Mark;
        internal Action<bool>? Changed;
        private bool _checked;

        internal bool Checked
        {
            get => _checked;
            set
            {
                _checked = value;
                Refresh();
            }
        }

        internal void SetSilently(bool value)
        {
            _checked = value;
            Refresh();
        }

        private void Refresh()
        {
            if (Border != null)
            {
                Color color = Border.color;
                color.a = _checked ? 1f : UncheckedBorderAlpha;
                Border.color = color;
            }

            if (Mark != null && Mark.activeSelf != _checked)
            {
                Mark.SetActive(_checked);
            }
        }

        internal void Toggle()
        {
            Checked = !Checked;
            Changed?.Invoke(Checked);
        }
    }

    internal static CheckBox MakeCheckBox(string name, Transform parent, Vector2 anchoredPosition, bool initial, Action<bool>? changed)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rect = (RectTransform)root.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(CheckBoxSize + 6f, CheckBoxSize + 6f); // 比方块大一圈，好点
        rect.anchoredPosition = anchoredPosition;

        Image hit = root.GetComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f); // 只接射线，不画
        hit.raycastTarget = true;

        Image border = MakeBox("Border", rect, Cream, 6f);
        RectTransform borderRect = border.rectTransform;
        borderRect.anchorMin = new Vector2(0.5f, 0.5f);
        borderRect.anchorMax = new Vector2(0.5f, 0.5f);
        borderRect.sizeDelta = new Vector2(CheckBoxSize, CheckBoxSize);
        borderRect.anchoredPosition = Vector2.zero;

        Image box = MakeBox("Box", borderRect, new Color(ModConfigTreeLayoutPatch.TabBackground.r, ModConfigTreeLayoutPatch.TabBackground.g, ModConfigTreeLayoutPatch.TabBackground.b, 0.92f), 4f);
        RectTransform boxRect = box.rectTransform;
        boxRect.anchorMin = Vector2.zero;
        boxRect.anchorMax = Vector2.one;
        boxRect.offsetMin = new Vector2(2f, 2f);
        boxRect.offsetMax = new Vector2(-2f, -2f);

        GameObject mark = new GameObject("Mark", typeof(RectTransform));
        RectTransform markRect = (RectTransform)mark.transform;
        markRect.SetParent(boxRect, false);
        markRect.anchorMin = Vector2.zero;
        markRect.anchorMax = Vector2.one;
        markRect.offsetMin = Vector2.zero;
        markRect.offsetMax = Vector2.zero;
        MakeBar(markRect, new Vector2(-3.2f, -1.2f), new Vector2(2.4f, 6f), 45f);
        MakeBar(markRect, new Vector2(1.4f, 0.4f), new Vector2(2.4f, 10.5f), -45f);

        Button button = root.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        button.targetGraphic = border;
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.selectedColor = Color.white;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        CheckBox checkBox = root.AddComponent<CheckBox>();
        checkBox.Border = border;
        checkBox.Mark = mark;
        checkBox.Changed = changed;
        checkBox.SetSilently(initial);
        button.onClick.AddListener(checkBox.Toggle);
        root.AddComponent<GamepadSupport.FocusHighlight>(); // 手柄焦点框（1.2x 描边色太弱，补一圈外扩描边）
        return checkBox;
    }

    /// <summary>✓ 的一根细条：天蓝、绕中心旋转、圆头。</summary>
    private static void MakeBar(RectTransform parent, Vector2 center, Vector2 size, float angle)
    {
        GameObject go = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = center;
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        ApplyRounded(image, 1.5f);
        Tint(image, MarkDay, MarkNight);
    }

    // ── 滚动条 ──────────────────────────────────────────────────────

    /// <summary>
    /// 立在列表右侧的竖向滚动条：轨道用页签棕（更淡）、滑块用米白，都是 TimeTheme 表里的色；两者都圆角。
    /// 挂到 ScrollRect.verticalScrollbar 上由它驱动；AutoHide 让内容装得下时整条隐藏。
    /// 滑块不叫「Handle」：TimeTheme 对这个名字有特殊处理。
    /// </summary>
    internal static Scrollbar MakeScrollbar(RectTransform page, ScrollRect scroll, float left, float width, float top, float bottom)
    {
        GameObject root = new GameObject("MCE_Scrollbar", typeof(RectTransform), typeof(Scrollbar));
        RectTransform rect = (RectTransform)root.transform;
        rect.SetParent(page, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(left + width, -top);

        Image track = MakeBox("MCE_ScrollTrack", rect, new Color(ModConfigTreeLayoutPatch.TabBackground.r, ModConfigTreeLayoutPatch.TabBackground.g, ModConfigTreeLayoutPatch.TabBackground.b, 0.4f), width / 2f, paper: false);
        RectTransform trackRect = track.rectTransform;
        trackRect.anchorMin = Vector2.zero;
        trackRect.anchorMax = Vector2.one;
        trackRect.offsetMin = Vector2.zero;
        trackRect.offsetMax = Vector2.zero;

        GameObject area = new GameObject("Sliding Area", typeof(RectTransform));
        RectTransform areaRect = (RectTransform)area.transform;
        areaRect.SetParent(rect, false);
        areaRect.anchorMin = Vector2.zero;
        areaRect.anchorMax = Vector2.one;
        areaRect.offsetMin = new Vector2(1f, 1f);
        areaRect.offsetMax = new Vector2(-1f, -1f);

        Image thumb = MakeBox("Thumb", areaRect, new Color(Cream.r, Cream.g, Cream.b, 0.85f), (width - 2f) / 2f, paper: false);
        thumb.raycastTarget = true;
        RectTransform thumbRect = thumb.rectTransform;
        thumbRect.anchorMin = Vector2.zero;
        thumbRect.anchorMax = Vector2.one;
        thumbRect.offsetMin = Vector2.zero;
        thumbRect.offsetMax = Vector2.zero;

        Scrollbar bar = root.GetComponent<Scrollbar>();
        bar.handleRect = thumbRect;
        bar.targetGraphic = thumb;
        bar.direction = Scrollbar.Direction.BottomToTop;
        bar.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.selectedColor = Color.white;
        colors.colorMultiplier = 1f;
        bar.colors = colors;

        // 手柄焦点不许落到滚动条上（行滚动由 FocusScrollIntoView 代劳；鼠标拖拽不受影响）
        bar.navigation = new Navigation { mode = Navigation.Mode.None };

        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return bar;
    }

    // ── 工具行（列表上方）───────────────────────────────────────────

    /// <summary>
    /// 三格等分 300 宽：导出翻译（蓝）、查漏翻（绿；没装 XUnity 灰）、「仅导出已选」开关（页签棕底 + 复选框）。
    /// 返回值是开关的复选框，勾选变化时由 <see cref="SidebarController"/> 显示/隐藏导出列。
    /// <paramref name="languageRowOnly"/> 时前两排不建，语言行直接落在第一排的位置（配合 SidebarTop 收拢，不留空白）。
    /// </summary>
    internal static CheckBox? MakeToolbar(RectTransform page, float left, float width, float top, bool languageRowOnly, Action<bool> exportSelectedChanged)
    {
        EnsureTerms();
        if (languageRowOnly)
        {
            MakeLanguageRow(page, left, top, width, ToolbarHeight, ToolbarGap);
            return null;
        }

        float x = left;
        ModConfigTranslationExport.MakeToolbarButton(page, "MCE_ExportTexts", false, x, top, ToolbarButtonWidth, ToolbarHeight);
        x += ToolbarButtonWidth + ToolbarGap;
        ModConfigTranslationExport.MakeToolbarButton(page, "MCE_ExportUntranslated", true, x, top, ToolbarButtonWidth, ToolbarHeight);
        x += ToolbarButtonWidth + ToolbarGap;

        float toggleWidth = left + width - x;
        GameObject row = new GameObject("MCE_ExportSelectedOnly", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rect = (RectTransform)row.transform;
        rect.SetParent(page, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -top);
        rect.sizeDelta = new Vector2(toggleWidth, ToolbarHeight);
        Image background = row.GetComponent<Image>();
        background.color = ModConfigTreeLayoutPatch.TabBackground;
        ModConfigPaperStyle.Apply(background, 8f);

        CheckBox box = MakeCheckBox("Check", rect, new Vector2(4f, 0f), ExportSelectedOnly, value =>
        {
            ExportSelectedOnly = value;
            exportSelectedChanged(value);
        });

        PeakText label = MenuAPI.CreateText("", "Label").ParentTo(rect);
        label.SetLocalizationIndex(ExportSelectedKey);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(CheckBoxSize + 10f, 0f);
        labelRect.offsetMax = new Vector2(-3f, 0f);
        StyleLabel(label.TextMesh, TextAlignmentOptions.MidlineLeft);

        row.GetComponent<Button>().transition = Selectable.Transition.None;
        row.GetComponent<Button>().onClick.AddListener(box.Toggle);
        row.AddComponent<ThemeAsOption>();
        GamepadSupport.MarkSelectable(row, null, null); // 不在滚动区：只要焦点框

        // 第二行：XUnity 的「显示原文 / 译文」「重载翻译」+「打开目录」
        ModConfigTranslationExport.MakeXUnityButtons(page, left, top + ToolbarHeight + ToolbarRowGap, width, ToolbarHeight, ToolbarGap);
        // 第三行：语言文件下拉 + 刷新
        MakeLanguageRow(page, left, top + (ToolbarHeight + ToolbarRowGap) * 2f, width, ToolbarHeight, ToolbarGap);
        return box;
    }

    // ── 语言行（第三行）────────────────────────────────────────────

    private static readonly Color RefreshColor = new Color(0.185f, 0.394f, 0.6226f);

    /// <summary>
    /// 左边是语言下拉（第一项「跟随游戏语言」，其后是 language 目录下的文件，显示名取文件首行 // display: 或文件名），
    /// 右边「刷新」重扫目录（新建的文件点一下就能选）。选中即应用、全页面实时刷新；出错在说明面板用警告色提示。
    /// </summary>
    private static void MakeLanguageRow(RectTransform page, float left, float top, float width, float height, float gap)
    {
        float buttonWidth = ToolbarButtonWidth;
        float dropdownWidth = width - buttonWidth - gap;

        // 直接实例化到 page 下时页面还没激活，PeakDropdown.Awake 不跑、RectTransform 为空（ModConfig 自己也是先挂活动节点再 ParentTo）
        PeakDropdown dropdown = MenuAPI.CreateDropdown("MCE_Language", null!).ParentTo(page);
        RectTransform cell = (RectTransform)dropdown.transform;
        cell.anchorMin = new Vector2(0f, 1f);
        cell.anchorMax = new Vector2(0f, 1f);
        cell.pivot = new Vector2(0f, 1f);
        cell.sizeDelta = new Vector2(dropdownWidth, height);
        cell.anchoredPosition = new Vector2(left, -top);
        dropdown.SetSize(new Vector2(dropdownWidth, height));
        dropdown.RectTransform.anchorMin = new Vector2(0f, 1f);
        dropdown.RectTransform.anchorMax = new Vector2(0f, 1f);
        dropdown.RectTransform.pivot = new Vector2(0f, 1f);
        dropdown.RectTransform.anchoredPosition = Vector2.zero;
        // 与 ModConfig 自己的筛选下拉同一套配色：深底、米白字；预制体默认的字色在深底上看不清
        dropdown.SetBackgroundColor(ModConfigTreeLayoutPatch.TabBackground)
            .SetLabelColor(Color.wheat)
            .SetArrowColor(Color.wheat);
        ModConfigPaperStyle.Apply(dropdown.Background, 8f);
        TMP_Text caption = dropdown.Dropdown.captionText;
        if (caption != null)
        {
            caption.enableAutoSizing = false;
            caption.fontSize = HeaderFontSize;
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            caption.overflowMode = TextOverflowModes.Ellipsis;
        }

        if (dropdown.Dropdown.itemText != null)
        {
            dropdown.Dropdown.itemText.fontSize = HeaderFontSize;
        }

        dropdown.gameObject.AddComponent<ThemeAsOption>();
        GamepadSupport.MarkSelectable(dropdown.Dropdown.gameObject, null, null); // 手柄焦点框
        LanguageDropdown controller = dropdown.gameObject.AddComponent<LanguageDropdown>();
        controller.Dropdown = dropdown;
        controller.Rescan(notify: false);
        dropdown.OnValueChanged(controller.OnPicked);

        ModConfigTranslationExport.MakeButton(page, "MCE_LanguageRefresh", "BTN_REFRESH", RefreshColor, left + dropdownWidth + gap, top, buttonWidth, height, () => controller.Rescan(notify: true));
    }

    /// <summary>下拉框的状态：索引 0 = 跟随游戏，其后按 <see cref="Loc.Scan"/> 的顺序。</summary>
    internal sealed class LanguageDropdown : MonoBehaviour
    {
        internal PeakDropdown? Dropdown;
        private readonly List<Loc.LanguageFile> _files = new List<Loc.LanguageFile>();
        private bool _updating;

        internal void Rescan(bool notify)
        {
            if (Dropdown == null || Dropdown.Dropdown == null)
            {
                return;
            }

            _files.Clear();
            _files.AddRange(Loc.Scan());
            List<string> options = new List<string> { Loc.Get("LANG_FOLLOW") };
            int selected = 0;
            string current = Plugin.LanguageFile.Value ?? "";
            string normalizedCurrent = Loc.NormalizeSelection(current);
            for (int i = 0; i < _files.Count; i++)
            {
                options.Add(_files[i].Display + " (" + _files[i].FileCount + ")");
                if (string.Equals(_files[i].Code, normalizedCurrent, StringComparison.OrdinalIgnoreCase))
                {
                    selected = i + 1;
                }
            }

            _updating = true;
            try
            {
                Dropdown.SetOptions(options);
                Dropdown.Dropdown.SetValueWithoutNotify(selected);
                Dropdown.Dropdown.RefreshShownValue();
            }
            finally
            {
                _updating = false;
            }

            if (notify)
            {
                ModConfigDescriptionPanel.Current?.ShowNotice(Loc.Format("NOTICE_REFRESHED", _files.Count));
            }

            if (selected == 0 && current.Length > 0)
            {
                // 记忆里的文件已不在：提示但不改记忆（用户可能只是暂时移走）
                ModConfigDescriptionPanel.Current?.ShowNotice(Loc.Format("NOTICE_LANG_ERROR", current + ".txt", Loc.Get("NOTICE_LANG_NOT_FOUND")), true);
            }
        }

        internal void OnPicked(int index)
        {
            if (_updating)
            {
                return;
            }

            string selection = index <= 0 || index > _files.Count ? Loc.FollowGame : _files[index - 1].Code;
            string previous = Plugin.LanguageFile.Value ?? "";
            Plugin.LanguageFile.Value = selection;
            Loc.ApplyResult result = Loc.Apply(selection, notify: true, refresh: true);
            if (!result.Success)
            {
                Plugin.LanguageFile.Value = previous;
            }

            Rescan(notify: false); // 「跟随游戏语言」这一项的文字本身也要换语言
        }

        /// <summary>页面重开时下拉文字按当前语言重刷（LANG_FOLLOW 是纯文本选项，不带 LocalizedText 组件）。</summary>
        private void OnEnable()
        {
            if (Dropdown != null && Dropdown.Dropdown != null && Dropdown.Dropdown.options.Count > 0)
            {
                Dropdown.Dropdown.options[0].text = Loc.Get("LANG_FOLLOW");
                Dropdown.Dropdown.RefreshShownValue();
            }
        }
    }

    /// <summary>
    /// 标记：这棵子树里的白色文字夜里要变紫。TimeTheme 主表不含纯白（白字不动），选项表第 0 组是「白 → 夜间紫」，
    /// 标题、搜索标签、工具行、表头、说明面板这些我们自己的（或 ModConfig 的固定）文字挂上它，主题跟随器多刷一遍选项表。
    /// </summary>
    internal sealed class ThemeAsOption : MonoBehaviour
    {
    }

    private static void StyleLabel(TextMeshProUGUI text, TextAlignmentOptions alignment)
    {
        text.enableAutoSizing = false;
        text.fontSize = HeaderFontSize;
        text.fontStyle = FontStyles.Normal;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
    }

    // ── 表头行 + 模组行的两列 ──────────────────────────────────────

    /// <summary>
    /// 挂在模组列表上，管两列复选框：建行、显示/隐藏导出列、置顶重排、把文字缩进让开列宽。
    /// 分区子行不带复选框。
    /// </summary>
    internal sealed class SidebarController : MonoBehaviour
    {
        internal ModSettingsMenu? Menu;
        internal PeakHorizontalTabs? ModTabs;
        internal RectTransform? ListContent;
        internal RectTransform? SectionRoot;
        internal ScrollRect? Scroll;
        internal GameObject? Header;
        internal TextMeshProUGUI? ExportCaption;
        internal TextMeshProUGUI? PinCaption;
        internal CheckBox? KeysOnlyBox;
        internal CheckBox? ExportSelectedBox;

        private readonly List<RowWidgets> _rows = new List<RowWidgets>();

        private sealed class RowWidgets
        {
            internal GameObject Row = null!;
            internal string Name = "";
            internal TextMeshProUGUI? Text;
            internal CheckBox Export = null!;
            internal CheckBox Pin = null!;
            internal GameObject Accent = null!;
        }

        internal float TextIndent => ModConfigTreeLayoutPatch.ModTextIndent + ColumnWidth + (ExportColumnVisible ? ColumnWidth : 0f);

        internal void Build()
        {
            if (ModTabs == null || ListContent == null)
            {
                return;
            }

            EnsureTerms();
            LoadState();
            BuildHeader();
            foreach (GameObject row in ModTabs.Tabs)
            {
                Decorate(row);
            }

            RefreshColumns();
            ApplyPinOrder();
        }

        private void BuildHeader()
        {
            if (Menu == null || ListContent == null)
            {
                return;
            }

            GameObject row = new GameObject("MCE_Header", typeof(RectTransform), typeof(LayoutElement));
            row.transform.SetParent(ListContent, false);
            row.transform.SetAsFirstSibling();
            GameObject background = new GameObject("Image", typeof(RectTransform), typeof(Image)).ParentTo(row.transform).ExpandToParent();
            background.GetComponent<Image>().color = ModConfigTreeLayoutPatch.TabBackground;
            background.GetComponent<Image>().raycastTarget = false;
            ModConfigTreeLayoutPatch.StyleRowFrame(row, ModConfigTreeLayoutPatch.ModRowHeight);
            ModConfigPaperStyle.Apply(background.GetComponent<Image>(), 8f);

            // 两列标题：压在下面复选框列的正上方
            ExportCaption = MakeCaption(row.transform, ExportColumnKey, ModConfigTreeLayoutPatch.ModTextIndent);
            PinCaption = MakeCaption(row.transform, PinKey, ModConfigTreeLayoutPatch.ModTextIndent + ColumnWidth);

            // 右侧：「只看按键」+ 复选框
            float boxLeft = ModConfigTreeLayoutPatch.SidebarWidth - 8f - CheckBoxSize - 6f;
            KeysOnlyBox = MakeCheckBox("KeysOnly", row.transform, new Vector2(boxLeft, 0f), false, _ => ToggleKeysOnly());
            PeakText label = MenuAPI.CreateText("", "Text (TMP)").ParentTo(row.transform);
            label.SetLocalizationIndex(KeysOnlyKey);
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(ModConfigTreeLayoutPatch.ModTextIndent + ColumnWidth * 2f + 4f, 0f);
            labelRect.offsetMax = new Vector2(-(ModConfigTreeLayoutPatch.SidebarWidth - boxLeft) - 4f, 0f);
            StyleLabel(label.TextMesh, TextAlignmentOptions.MidlineRight);

            // 只有「只显示快捷键选项」文字 + 复选框这一段能点，别压到左边的导出 / 置顶标题
            GameObject hit = new GameObject("KeysOnlyHit", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform hitRect = (RectTransform)hit.transform;
            hitRect.SetParent(row.transform, false);
            hitRect.anchorMin = Vector2.zero;
            hitRect.anchorMax = Vector2.one;
            hitRect.offsetMin = new Vector2(labelRect.offsetMin.x, 0f);
            hitRect.offsetMax = Vector2.zero;
            hit.GetComponent<Image>().color = Color.clear;
            hitRect.SetAsFirstSibling(); // 在文字与复选框下面
            Button button = hit.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => KeysOnlyBox?.Toggle());
            row.AddComponent<ThemeAsOption>();
            // 手柄：表头的命中区与复选框都能拿焦点，选中时把表头滚回视野
            GamepadSupport.MarkSelectable(hit, Scroll, (RectTransform)row.transform);
            if (KeysOnlyBox != null)
            {
                GamepadSupport.MarkSelectable(KeysOnlyBox.gameObject, Scroll, (RectTransform)row.transform);
            }
            Header = row;
        }

        private static TextMeshProUGUI MakeCaption(Transform parent, string key, float left)
        {
            PeakText caption = MenuAPI.CreateText("", "Caption").ParentTo(parent);
            caption.SetLocalizationIndex(key);
            RectTransform rect = caption.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = new Vector2(left - 4f, 0f);
            rect.offsetMax = new Vector2(left + ColumnWidth + 4f, 0f);
            TextMeshProUGUI text = caption.TextMesh;
            StyleLabel(text, TextAlignmentOptions.Center);
            return text;
        }

        /// <summary>「只看按键」：走 ModConfig 自己的筛选下拉（同 2.13.1 的 KeysOnlyToggle），复选框状态在 Update 里跟 FilterValue 走。</summary>
        private void ToggleKeysOnly()
        {
            if (Menu == null || Menu.FilterDropdown == null || Menu.FilterDropdown.Dropdown == null)
            {
                return;
            }

            int target = Menu.FilterValue == ModConfigTreeLayoutPatch.ControlsFilter ? ModConfigTreeLayoutPatch.AllFilter : ModConfigTreeLayoutPatch.ControlsFilter;
            if (Menu.FilterDropdown.Dropdown.value == target)
            {
                Menu.SetFilter(target);
            }
            else
            {
                Menu.FilterDropdown.Dropdown.value = target;
            }
        }

        private void Update()
        {
            if (KeysOnlyBox == null || Menu == null)
            {
                return;
            }

            bool active = Menu.FilterValue == ModConfigTreeLayoutPatch.ControlsFilter;
            if (KeysOnlyBox.Checked != active)
            {
                KeysOnlyBox.SetSilently(active);
            }
        }

        private void Decorate(GameObject row)
        {
            string name = row.name;
            bool isSelf = name == Plugin.SelfRowName;
            RowWidgets widgets = new RowWidgets
            {
                Row = row,
                Name = name,
                Text = row.GetComponentInChildren<TextMeshProUGUI>(true),
            };
            widgets.Export = MakeCheckBox("MCE_Export", row.transform, new Vector2(ModConfigTreeLayoutPatch.ModTextIndent + ColumnInset, 0f),
                !ExcludedFromExport.Contains(name), value =>
                {
                    if (value)
                    {
                        ExcludedFromExport.Remove(name);
                    }
                    else
                    {
                        ExcludedFromExport.Add(name);
                    }

                    SaveExcluded();
                });
            widgets.Pin = MakeCheckBox("MCE_Pin", row.transform, new Vector2(ModConfigTreeLayoutPatch.ModTextIndent + ColumnWidth + ColumnInset, 0f),
                Pinned.Contains(name) || isSelf, value =>
                {
                    if (value)
                    {
                        Pinned.Add(name);
                    }
                    else
                    {
                        Pinned.Remove(name);
                    }

                    SavePinned();
                    widgets.Accent.SetActive(value);
                    ApplyPinOrder();
                });
            if (isSelf)
            {
                // 自身行固定最顶，复选框没有意义；置顶竖条常亮，行名上色（LateUpdate 每帧断言，见 SelfRowTint）
                widgets.Pin.gameObject.SetActive(false);
                if (widgets.Text != null)
                {
                    SelfRowTint tint = widgets.Text.gameObject.AddComponent<SelfRowTint>();
                    tint.Text = widgets.Text;
                    tint.Button = row.GetComponent<ModdedTABSButton>();
                }
            }

            widgets.Accent = MakeAccent(row.transform, isSelf || Pinned.Contains(name));

            // 手柄：行与两列复选框都可聚焦；焦点框亮起 + 选中时把整行滚回视野（复选框对准整行而非小方块）；
            // A 键语义（选中 / 已选中再往分区·右栏钻）在 WireModRow
            GamepadSupport.MarkSelectable(row, Scroll, (RectTransform)row.transform);
            GamepadSupport.MarkSelectable(widgets.Export.gameObject, Scroll, (RectTransform)row.transform);
            GamepadSupport.MarkSelectable(widgets.Pin.gameObject, Scroll, (RectTransform)row.transform);
            GamepadSupport.WireModRow(row);
            _rows.Add(widgets);
        }

        /// <summary>置顶行左缘 4 宽的竖条，颜色与 ✓ 同（白天天蓝 / 夜间深蓝），一眼能认出哪些是钉住的。</summary>
        private static GameObject MakeAccent(Transform row, bool active)
        {
            GameObject go = new GameObject("MCE_PinAccent", typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(row, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = new Vector2(0f, 4f);
            rect.offsetMax = new Vector2(4f, -4f);
            Image image = go.GetComponent<Image>();
            image.raycastTarget = false;
            ApplyRounded(image, 2f);
            Tint(image, MarkDay, MarkNight);
            go.SetActive(active);
            return go;
        }

        /// <summary>导出列随「仅导出已选」显示/隐藏（只留语言行 / 工具行全关时强制收掉）；隐藏时置顶列左移、文字跟着左移，不留空档。</summary>
        internal void RefreshColumns()
        {
            bool showExport = ExportColumnVisible;
            float pinLeft = ModConfigTreeLayoutPatch.ModTextIndent + (showExport ? ColumnWidth : 0f) + ColumnInset;
            float indent = TextIndent;
            foreach (RowWidgets widgets in _rows)
            {
                if (widgets.Export.gameObject.activeSelf != showExport)
                {
                    widgets.Export.gameObject.SetActive(showExport);
                }

                ((RectTransform)widgets.Pin.transform).anchoredPosition = new Vector2(pinLeft, 0f);
                if (widgets.Text != null)
                {
                    Vector4 margin = widgets.Text.margin;
                    margin.x = indent;
                    widgets.Text.margin = margin;
                }
            }

            if (ExportCaption != null && ExportCaption.gameObject.activeSelf != showExport)
            {
                ExportCaption.gameObject.SetActive(showExport);
            }

            if (PinCaption != null)
            {
                // 表头里置顶标题跟着列走
                RectTransform rect = PinCaption.rectTransform;
                float left = ModConfigTreeLayoutPatch.ModTextIndent + (showExport ? ColumnWidth : 0f);
                rect.offsetMin = new Vector2(left - 4f, 0f);
                rect.offsetMax = new Vector2(left + ColumnWidth + 4f, 0f);
            }
        }

        /// <summary>
        /// 自身行永远钉在最顶（表头之下第一位），其后才是置顶行（按它们在 ModConfig 列表里的原顺序），
        /// 其余行保持原顺序跟在后面，分区容器再贴回当前选中行下面。只动 sibling 顺序，不碰 ModConfig 的 Tabs / buttons 列表。
        /// </summary>
        internal void ApplyPinOrder()
        {
            if (ModTabs == null || ListContent == null)
            {
                return;
            }

            int index = Header != null ? 1 : 0;
            foreach (GameObject row in ModTabs.Tabs)
            {
                if (row != null && row.name == Plugin.SelfRowName)
                {
                    row.transform.SetSiblingIndex(index++);
                }
            }

            foreach (GameObject row in ModTabs.Tabs)
            {
                if (row != null && row.name != Plugin.SelfRowName && Pinned.Contains(row.name))
                {
                    row.transform.SetSiblingIndex(index++);
                }
            }

            foreach (GameObject row in ModTabs.Tabs)
            {
                if (row != null && row.name != Plugin.SelfRowName && !Pinned.Contains(row.name))
                {
                    row.transform.SetSiblingIndex(index++);
                }
            }

            ModdedTABSButton? selected = Menu != null && Menu.ModTabs != null ? Menu.ModTabs.selectedButton : null;
            if (SectionRoot != null && selected != null && selected.transform.parent == SectionRoot.parent)
            {
                SectionRoot.SetAsLastSibling();
                SectionRoot.SetSiblingIndex(selected.transform.GetSiblingIndex() + 1);
            }

            LayoutRebuilder.MarkLayoutForRebuild(ListContent);
        }
    }

    // ── 按钮虚线位置 ────────────────────────────────────────────────

    /// <summary>
    /// PeakMenuButton 模板高 67 左右，上下两条虚线（BorderTop / BorderBottom）的位置是按模板高度定的；
    /// 把按钮压矮之后两条线没跟着缩，就挤到中间去了。按「新高 / 旧高」把锚点式定位的 y 等比缩回去；
    /// 上下拉伸式锚点的偏移本来就贴边，不动。第一次调用把实际锚点记进日志，便于对照实机。
    /// </summary>
    internal static void FitBorders(PeakMenuButton button, float oldHeight, float newHeight)
    {
        if (oldHeight <= 0f || newHeight <= 0f || Mathf.Approximately(oldHeight, newHeight))
        {
            return;
        }

        float ratio = newHeight / oldHeight;
        foreach (Image? border in new[] { button.BorderTop, button.BorderBottom })
        {
            if (border == null)
            {
                continue;
            }

            RectTransform rect = border.rectTransform;
            if (!_borderLogged)
            {
                Plugin.Log.LogDebug($"[面板布局] 按钮虚线 {border.name}: anchor {rect.anchorMin}~{rect.anchorMax} pos {rect.anchoredPosition} size {rect.sizeDelta}（模板高 {oldHeight}）");
            }

            if (Mathf.Approximately(rect.anchorMin.y, rect.anchorMax.y))
            {
                Vector2 position = rect.anchoredPosition;
                position.y *= ratio;
                rect.anchoredPosition = position;
            }
        }

        _borderLogged = true;
    }
}
