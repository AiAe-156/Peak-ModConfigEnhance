#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModConfigEnhance;

/// <summary>
/// 与 TimeTheme（昼夜主题，com.github.MiiMii1205.TimeTheme 0.4.2）的软对接，全部走反射，不引用它的 DLL。
///
/// TimeTheme 在 <c>RunManager.StartRun</c> 时把 GUIManager 下所有 Graphic 收进一张表，昼夜切换时按
/// 「当前颜色的 RGB == 表里的白天色」逐个换成夜间色（保留 alpha）。ModConfig 的 ESC 页面是第一次按 ESC 才
/// 构建的，晚于 StartRun，整页都不在那张表里 —— 所以夜间模式下返回按钮、模组列表全是白天色；
/// 选项标签之所以是紫的，是因为它们克隆自被 TimeTheme 就地改过色的 SettingsCell 预制体。
///
/// 这里做三件事：
///   ① 页面建好后让 TimeTheme 重新收表（<c>RefreshGraphicLists</c> 是它的公开静态方法），之后的昼夜切换就能覆盖到这页；
///   ② 建页、重建分区行、打开页面时，按它自己的配色表把「此刻应有的颜色」直接刷上去
///     （选表、跳过规则都照抄它 <c>UpdateGraphics</c> 的主循环，只是不做 0.25 秒渐变）；
///   ③ 暴露 IsDark / DarkTabText，给页签行的文字着色用（对应它给原版 SettingsTABSButton 打的那个补丁）。
/// TimeTheme 不在场时所有方法都是空操作。
/// </summary>
internal static class TimeThemeBridge
{
    internal const string Guid = "com.github.MiiMii1205.TimeTheme";
    private const string PluginTypeName = "TimeTheme.TimeThemePlugin";

    /// <summary>TimeTheme 主循环里「按钮类」图片（这些 sprite 或这个名字）走 m_dayNightButtonColors，其余走 m_dayNightColors。</summary>
    private static readonly HashSet<string> ButtonSprites = new HashSet<string>(StringComparer.Ordinal)
    {
        "UI_Banner", "UI_Blur_DoubleArrow", "UI_Blur_Arrow", "DottedLine",
    };

    private const string ButtonImageName = "BadgeSash";

    /// <summary>它对这三个文本不做处理。</summary>
    private static readonly HashSet<string> SkippedTexts = new HashSet<string>(StringComparer.Ordinal)
    {
        "HeroText", "HeroTimeOfDay", "HeroDay",
    };

    /// <summary>它的主循环对名叫 Background / BG 的对象跳过第 20 组（主表里那一组是页签底色 0.179,0.125,0.090 → 黑），两张表同样处理。</summary>
    private const int TabBackgroundPairIndex = 20;

    private static readonly Color FallbackDarkTabText = new Color(0.7843137f, 0.4980392f, 83f / 85f, 1f);

    /// <summary>它 m_dayNightOptionsColors[4] 的夜间色：给 Selectable 的 ColorBlock.selectedColor 用的那一格（白天是 0.96 灰白）。</summary>
    private static readonly Color FallbackDarkSelectedFill = new Color(0.7450981f, 0.4627451f, 0.9372549f, 1f);
    private const int SelectedFillPairIndex = 4;

    private static bool _resolved;
    private static PropertyInfo? _isDarkTheme;
    private static PropertyInfo? _darkTabText;
    private static MethodInfo? _refreshLists;
    private static (Color Day, Color Night)[] _mainPairs = Array.Empty<(Color, Color)>();
    private static (Color Day, Color Night)[] _buttonPairs = Array.Empty<(Color, Color)>();
    private static (Color Day, Color Night)[] _optionPairs = Array.Empty<(Color, Color)>();
    private static Color? _darkTabTextCache;
    private static int _darkCacheFrame = -1;
    private static bool _darkCache;
    private static bool _refreshFailedLogged;

    internal static bool Available
    {
        get
        {
            Resolve();
            return _isDarkTheme != null;
        }
    }

    private static void Resolve()
    {
        if (_resolved)
        {
            return;
        }

        _resolved = true;
        if (!Chainloader.PluginInfos.ContainsKey(Guid))
        {
            return;
        }

        try
        {
            Type? type = AccessTools.TypeByName(PluginTypeName);
            if (type == null)
            {
                Plugin.Log.LogWarning("[面板布局] TimeTheme 在场但找不到 TimeThemePlugin 类型，夜间跟色跳过。");
                return;
            }

            _isDarkTheme = AccessTools.Property(type, "IsDarkTheme");
            _darkTabText = AccessTools.Property(type, "DarkTabText");
            _refreshLists = AccessTools.Method(type, "RefreshGraphicLists", Type.EmptyTypes);
            _mainPairs = ReadTable(type, "m_dayNightColors");
            _buttonPairs = ReadTable(type, "m_dayNightButtonColors");
            _optionPairs = ReadTable(type, "m_dayNightOptionsColors");

            if (_isDarkTheme == null || _mainPairs.Length == 0 || _buttonPairs.Length == 0)
            {
                Plugin.Log.LogWarning(
                    $"[面板布局] TimeTheme 的结构变了（IsDarkTheme={_isDarkTheme != null}, " +
                    $"配色 {_mainPairs.Length}+{_buttonPairs.Length} 组），夜间跟色跳过。");
                _isDarkTheme = null;
                return;
            }

            Plugin.Log.LogInfo(
                $"[面板布局] 已对接 TimeTheme {Chainloader.PluginInfos[Guid].Metadata.Version}" +
                $"（配色 {_mainPairs.Length}+{_buttonPairs.Length} 组）。");
        }
        catch (Exception ex)
        {
            _isDarkTheme = null;
            Plugin.Log.LogWarning($"[面板布局] 对接 TimeTheme 失败，夜间跟色跳过: {ex.Message}");
        }
    }

    private static (Color, Color)[] ReadTable(Type type, string field)
    {
        List<(Color, Color)> pairs = new List<(Color, Color)>();
        if (AccessTools.Field(type, field)?.GetValue(null) is Tuple<Color, Color>[] table)
        {
            foreach (Tuple<Color, Color> pair in table)
            {
                pairs.Add((pair.Item1, pair.Item2));
            }
        }

        return pairs.ToArray();
    }

    /// <summary>
    /// 当前是否夜间主题。每帧最多真正读一次反射，其余走缓存（页签行每帧都问）。
    /// 只在游戏场景里算数：TimeTheme 的 m_isDay 回到主菜单也不复位，而主菜单本身它不着色，
    /// 夜里退回主菜单再开模组设置，页面不该单独变成夜间色。
    /// </summary>
    internal static bool IsDark
    {
        get
        {
            Resolve();
            if (_isDarkTheme == null || GUIManager.instance == null)
            {
                return false;
            }

            int frame = Time.frameCount;
            if (frame == _darkCacheFrame)
            {
                return _darkCache;
            }

            _darkCacheFrame = frame;
            try
            {
                _darkCache = _isDarkTheme.GetValue(null) is bool dark && dark;
            }
            catch
            {
                _darkCache = false;
            }

            return _darkCache;
        }
    }

    /// <summary>TimeTheme 给未选中页签文字用的夜间色（它自己对原版页签就是这么涂的）。</summary>
    internal static Color DarkTabText
    {
        get
        {
            if (_darkTabTextCache.HasValue)
            {
                return _darkTabTextCache.Value;
            }

            Resolve();
            try
            {
                if (_darkTabText?.GetValue(null) is Color color)
                {
                    _darkTabTextCache = color;
                    return color;
                }
            }
            catch
            {
            }

            return FallbackDarkTabText;
        }
    }

    /// <summary>
    /// 夜间模式下「选中行」的底色：取它 m_dayNightOptionsColors[4] 的夜间格，也就是它给 Selectable 的
    /// ColorBlock.selectedColor 用的夜间紫。白天那一格是 0.96 的灰白，与 ModConfig 选中底图的纯白同属一色，
    /// 夜间换成这格正好是「白底黑字」在它配色体系里的对应物。表读不到时退回同值常量。
    /// </summary>
    internal static Color DarkSelectedFill
    {
        get
        {
            Resolve();
            return Dim(_optionPairs.Length > SelectedFillPairIndex ? _optionPairs[SelectedFillPairIndex].Night : FallbackDarkSelectedFill);
        }
    }

    /// <summary>
    /// 给页签行的「Selected」底图上色：夜间涂 <see cref="DarkSelectedFill"/>，白天还原纯白（ModConfig 建行时的默认值）。
    /// 只比 RGB、保留原 alpha，颜色已对时不写。TimeTheme 不在场时直接返回 —— 底图从未被改过，无需还原，
    /// 也保证没装它时这里对界面零影响。
    /// </summary>
    internal static void TintSelectedGraphic(GameObject? selectedGraphic, bool dark)
    {
        if (selectedGraphic == null || !Available)
        {
            return;
        }

        try
        {
            Image? image = selectedGraphic.GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            Color target = dark ? DarkSelectedFill : Color.white;
            Color current = image.color;
            if (!SameRgb(current, target))
            {
                image.color = new Color(target.r, target.g, target.b, current.a);
            }
        }
        catch
        {
            // 底图对象在页面销毁途中可能已失效；这里只是着色，静默即可
        }
    }

    /// <summary>让 TimeTheme 重新收集 GUIManager 下的 Graphic。只在游戏场景（GUIManager 存在）里有意义。</summary>
    internal static void RefreshLists()
    {
        Resolve();
        if (_refreshLists == null || GUIManager.instance == null)
        {
            return;
        }

        try
        {
            _refreshLists.Invoke(null, null);
        }
        catch (Exception ex)
        {
            if (!_refreshFailedLogged)
            {
                _refreshFailedLogged = true;
                Plugin.Log.LogWarning($"[面板布局] 让 TimeTheme 重新收表失败（之后的昼夜切换可能覆盖不到模组设置页）: {ex.Message}");
            }
        }
    }

    internal static void ApplyCurrentTheme(Transform root)
    {
        ApplyTheme(root, IsDark);
    }

    /// <summary>
    /// 按 TimeTheme 的配色表把 root 下所有 Graphic 刷成目标主题该有的颜色：夜间把「白天色」换成「夜间色」，
    /// 白天反之；只比 RGB、保留各自 alpha。选表与跳过规则照抄它的主循环，因此不会多染它不染的东西
    /// （纯白只在输入框 / 下拉框的 ColorBlock 表里，主循环不用那张表，白色文字与白色选中底不会被改成紫色）。
    /// 已经是目标主题颜色的对象自然匹配不上，反复调用无副作用。
    /// </summary>
    internal static void ApplyTheme(Transform root, bool dark)
    {
        if (!Available || root == null)
        {
            return;
        }

        foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic == null)
            {
                continue;
            }

            string name = graphic.name;
            (Color Day, Color Night)[] table = _mainPairs;
            if (graphic is Image image &&
                ((image.sprite != null && ButtonSprites.Contains(image.sprite.name)) || name == ButtonImageName))
            {
                table = _buttonPairs;
            }
            else if (graphic is TextMeshProUGUI && SkippedTexts.Contains(name))
            {
                continue;
            }

            Color current = graphic.color;
            for (int j = 0; j < table.Length; j++)
            {
                if (j == TabBackgroundPairIndex && (name == "Background" || name == "BG"))
                {
                    continue;
                }

                Color from = dark ? table[j].Day : table[j].Night;
                Color to = dark ? table[j].Night : table[j].Day;
                if (SameRgb(current, from))
                {
                    graphic.color = new Color(to.r, to.g, to.b, current.a);
                    break;
                }
            }
        }
    }

    // ── 选项控件（输入框 / 下拉框 / 滑块）的昼夜色 ──────────────────

    /// <summary>它给 TMP_InputField 用的 ColorBlock：五格索引照抄它 Awake 里的组装顺序。</summary>
    private const int OptionNormal = 0, OptionHighlighted = 2, OptionDropdownSelected = 3, OptionInputSelected = 4,
        OptionDisabled = 5, OptionHandle = 6, OptionPressed = 8;

    /// <summary>夜间底色（选中行、输入框、下拉框的紫）在它原表基础上压暗一档：原色偏亮，用户要求调暗。</summary>
    private const float NightDim = 0.82f;

    private static Color Dim(Color color)
    {
        return new Color(color.r * NightDim, color.g * NightDim, color.b * NightDim, color.a);
    }

    private static Color OptionColor(int index, bool dark)
    {
        if (index >= _optionPairs.Length)
        {
            return Color.white;
        }

        if (!dark)
        {
            return _optionPairs[index].Day;
        }

        // 只压底色格（普通 / 高亮 / 选中），禁用与按下保持原表
        Color night = _optionPairs[index].Night;
        return index == OptionNormal || index == OptionHighlighted || index == OptionInputSelected || index == OptionDropdownSelected ? Dim(night) : night;
    }

    private static ColorBlock BuildColors(bool dark, int selectedIndex)
    {
        ColorBlock block = ColorBlock.defaultColorBlock;
        block.normalColor = OptionColor(OptionNormal, dark);
        block.highlightedColor = OptionColor(OptionHighlighted, dark);
        block.pressedColor = OptionColor(OptionPressed, dark);
        block.selectedColor = OptionColor(selectedIndex, dark);
        block.disabledColor = OptionColor(OptionDisabled, dark);
        block.colorMultiplier = 1f;
        block.fadeDuration = 0.1f;
        return block;
    }

    /// <summary>
    /// 把 root 下的选项控件刷成目标主题：对应它 SwitchDayNight 收尾时对设置页内容做的那一段 ——
    /// 输入框 / 下拉框整块换 ColorBlock（Selectable 的着色不走 Graphic.color，主表那一遍碰不到），
    /// 滑块把手固定用第 6 组，其余 Graphic 按选项配色表换色。
    /// TimeTheme 只在 StartRun 时收一次原版设置页的控件，ModConfig 的单元格是之后才生成的，
    /// 它扫不到，所以夜里下拉框和搜索框一直是白的；这里补上。不在场时空操作。
    /// </summary>
    internal static void ApplyOptionsTheme(Transform root, bool dark)
    {
        if (!Available || root == null || _optionPairs.Length <= OptionPressed)
        {
            return;
        }

        foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic == null)
            {
                continue;
            }

            if (graphic.TryGetComponent(out TMP_InputField input))
            {
                input.colors = BuildColors(dark, OptionInputSelected);
                continue;
            }

            if (graphic.TryGetComponent(out TMP_Dropdown dropdown))
            {
                dropdown.colors = BuildColors(dark, OptionDropdownSelected);
                continue;
            }

            Color current = graphic.color;
            if (graphic.name == "Handle")
            {
                Color handle = OptionColor(OptionHandle, dark);
                if (!SameRgb(current, handle))
                {
                    graphic.color = new Color(handle.r, handle.g, handle.b, current.a);
                }

                continue;
            }

            for (int j = 0; j < _optionPairs.Length; j++)
            {
                Color from = dark ? _optionPairs[j].Day : _optionPairs[j].Night;
                Color to = dark ? _optionPairs[j].Night : _optionPairs[j].Day;
                if (SameRgb(current, from))
                {
                    graphic.color = new Color(to.r, to.g, to.b, current.a);
                    break;
                }
            }
        }
    }

    private static bool SameRgb(Color a, Color b)
    {
        return new Vector3(a.r, a.g, a.b) == new Vector3(b.r, b.g, b.b);
    }
}
