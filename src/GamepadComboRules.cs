#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace ModConfigEnhance;

/// <summary>
/// 1.1.0：手柄组合键的判定规则（纯逻辑，不碰 Unity / BepInEx），由 <see cref="GamepadCombos"/> 调用；
/// 夹具 <c>验证/快捷键页_20261115/combo-checks/</c> 直接链接本文件做单元检查。改规则时两边一起跑。
/// </summary>
internal static class GamepadComboRules
{
    internal static readonly string[] Buttons = { "buttonSouth", "buttonEast", "buttonWest", "buttonNorth", "leftShoulder", "rightShoulder",
        "leftTrigger", "rightTrigger", "dpad/up", "dpad/down", "dpad/left", "dpad/right", "leftStickPress", "rightStickPress", "start", "select" };
    /// <summary>一组最多几个键（含最后一个）。</summary>
    internal const int MaxButtons = 4;

    /// <summary>同一组键（先按住的键不分顺序）的比较键：一组键只能给一个快捷键。</summary>
    internal static string SetKey(IEnumerable<string> held, string button) =>
        string.Join("+", held.OrderBy(h => h, StringComparer.Ordinal)) + ">" + button;

    /// <summary>
    /// 读文件时整理「先按住」的键：游戏内模组管理器格式的单个 <paramref name="modifier"/> 并入（放最前），
    /// 去掉未知键、与最后一个键相同的键、重复键。
    /// </summary>
    internal static List<string> NormalizeHeld(List<string>? held, string? modifier, string button)
    {
        var list = held ?? new List<string>();
        if (!string.IsNullOrEmpty(modifier) && !list.Contains(modifier!)) list.Insert(0, modifier!);
        return list.Where(h => Buttons.Contains(h) && h != button).Distinct().ToList();
    }

    /// <summary>整理后的一组是否可用：有 id、最后一个键认识、总键数不超过 <see cref="MaxButtons"/>。</summary>
    internal static bool IsUsable(string? id, string? button, int heldCount) =>
        !string.IsNullOrEmpty(id) && button != null && Buttons.Contains(button) && heldCount + 1 <= MaxButtons;

    /// <summary>
    /// 这一组的「先按住」键全部按着，并且没有别的「先按住」键多按着（多按了说明玩家想要的是另一组，比如
    /// 「LT + A」不该同时触发单键「A」）。<paramref name="allHeld"/> = 所有组的「先按住」键并集。
    /// 不检查最后一个键本身（调用方已确认它按着）。
    /// </summary>
    internal static bool Satisfied(IReadOnlyCollection<string> held, string button, IEnumerable<string> allHeld, Func<string, bool> pressed)
    {
        foreach (string name in held) if (!pressed(name)) return false;
        foreach (string name in allHeld)
        {
            if (name == button || held.Contains(name)) continue;
            if (pressed(name)) return false;
        }
        return true;
    }

    /// <summary>
    /// 已满足的多键组，最后一个键是否要对游戏屏蔽：单键组不屏蔽；最后一个键在某个「先按住」键之前就按下了
    /// （先按 RT 攀爬、再按 LT）也不屏蔽，否则游戏会丢掉 RT。<paramref name="pressedAt"/> 为空（没记到按下帧）时照常屏蔽。
    /// </summary>
    internal static bool MaskLastButton(IReadOnlyCollection<string> held, int? pressedAt, Func<string, int?> heldSince)
    {
        if (held.Count == 0) return false;
        if (pressedAt is int since && held.Any(h => heldSince(h) is int down && since < down)) return false;
        return true;
    }
}
