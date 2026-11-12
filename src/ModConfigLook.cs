#nullable enable
using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ModConfigEnhance;

/// <summary>
/// 1.0.4：模组设置页的配色来源（移植自完整版 2.3.12）。PEAK 3.1 把设置菜单改成深蓝底、米色字、选中米底深蓝字
/// （<c>Peak.UI.SettingsMenu.SettingsColors</c>），而本模组左栏、页签行、说明面板一直写死 3.1 之前的棕色，
/// 右侧 ModConfig 的格子已经跟着游戏变蓝，两边就不一致了。
///
/// 默认跟随游戏：运行时反射读那三个颜色，读不到（3.1 之前的游戏）就用旧棕色。
/// 「经典棕色」开关打开时一律用旧棕色、白字 / 选中白底黑字。
/// TimeTheme 夜间跟色不变：它的配色表只认旧棕色，所以跟随蓝色时夜里这几处不会被它换成夜间色（与完整版相同的已知限制）。
/// </summary>
internal static class ModConfigLook
{
    private const string SettingsColorsTypeName = "Peak.UI.SettingsMenu.SettingsColors";

    /// <summary>3.1 之前的棕底（PeakHorizontalTabs 默认值，TimeTheme 的配色表认这个色）。</summary>
    internal static readonly Color ClassicBox = new Color(0.1792453f, 0.1253449f, 0.09046815f, 62f / 85f);

    /// <summary>游戏 3.1 SettingsColors 的同值常量（反射读不到时用）。</summary>
    private static readonly Color GameBox = new Color(0.07075472f, 0.1818232f, 0.2830189f, 62f / 85f);
    private static readonly Color GameText = new Color(0.874f, 0.857f, 0.762f, 1f);
    private static readonly Color GameTextSelected = new Color(0.071f, 0.182f, 0.283f, 1f);

    /// <summary>置顶竖线：青柠黄绿（完整版用户定 #C8F05A），在蓝 / 棕两种底上都醒目，不与分组色、选中色混淆。</summary>
    internal static readonly Color PinAccent = new Color(0.784f, 0.941f, 0.353f, 1f);

    private static bool _resolved;
    private static bool _gameHasColors;
    private static Color _box = ClassicBox, _text = Color.white, _textSelected = Color.black, _selectedFill = Color.white;

    /// <summary>用旧棕色（开关打开，或游戏没有 3.1 的配色）。</summary>
    internal static bool Classic
    {
        get
        {
            Resolve();
            return !_gameHasColors || (Plugin.ClassicBrownMenu != null && Plugin.ClassicBrownMenu.Value);
        }
    }

    /// <summary>自建行、页签行、说明面板的底色（保留各处自己的透明度时只取 RGB）。</summary>
    internal static Color Box { get { Resolve(); return Classic ? ClassicBox : _box; } }

    /// <summary>未选中页签文字。</summary>
    internal static Color TextDefault { get { Resolve(); return Classic ? Color.white : _text; } }

    /// <summary>选中页签文字。</summary>
    internal static Color TextSelected { get { Resolve(); return Classic ? Color.black : _textSelected; } }

    /// <summary>选中页签底图。</summary>
    internal static Color SelectedFill { get { Resolve(); return Classic ? Color.white : _selectedFill; } }

    /// <summary>启动日志里的一句话。</summary>
    internal static string State
    {
        get
        {
            Resolve();
            if (!_gameHasColors) return "游戏没有 3.1 的设置配色，使用经典棕色";
            return Plugin.ClassicBrownMenu != null && Plugin.ClassicBrownMenu.Value ? "经典棕色（开关已开）" : "跟随游戏设置菜单（深蓝）";
        }
    }

    private static void Resolve()
    {
        if (_resolved) return;
        _resolved = true;
        try
        {
            Type? type = AccessTools.TypeByName(SettingsColorsTypeName);
            if (type == null) return;
            _box = Read(type, "BoxColor") ?? GameBox;
            _text = Read(type, "TextDefaultColor") ?? GameText;
            _textSelected = Read(type, "TextSelectedColor") ?? GameTextSelected;
            _selectedFill = _text; // 原版选中页签：米色底图、深蓝字
            _gameHasColors = true;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning("[配色] 读取游戏设置配色失败，使用经典棕色：" + ex.Message);
        }
    }

    private static Color? Read(Type type, string name)
    {
        object? value = AccessTools.Field(type, name)?.GetValue(null) ?? AccessTools.Property(type, name)?.GetValue(null);
        return value is Color color ? color : (Color?)null;
    }
}
