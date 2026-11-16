#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;

namespace ModConfigEnhance;

/// <summary>
/// 1.1.0：快捷键页的短说明（照抄游戏内模组管理器 Loc.Hotkeys.cs）。键 = 前缀 + 稳定 ID（插件 GUID / 分区 / 键名的 SHA256），
/// 写进当前语言文件。本模组写 <c>ModConfigEnhance.HotkeyDescription.</c>；读取时管理器的 <c>IGMM.HotkeyDescription.</c> 也认，
/// 从它那边拷来的语言文件照样能用。
/// </summary>
internal static partial class Loc
{
    internal const string HotkeyPrefix = "ModConfigEnhance.HotkeyDescription.";
    private const string ManagerHotkeyPrefix = "IGMM.HotkeyDescription.";

    internal static string HotkeyStableId(string guid, string section, string key)
    {
        // Length-prefixed fields cannot collide when config names contain separators.
        string input = guid.Length + ":" + guid + section.Length + ":" + section + key.Length + ":" + key;
        using var digest = SHA256.Create();
        var result = new StringBuilder();
        foreach (byte b in digest.ComputeHash(Encoding.UTF8.GetBytes(input))) result.Append(b.ToString("x2"));
        return result.ToString();
    }

    internal static string HotkeyDescriptionKey(string guid, string section, string key) => HotkeyPrefix + HotkeyStableId(guid, section, key);
    /// <summary>1.1.0（第 5 章）：游戏内模组管理器前缀的同一条键。清空短说明时一起清，否则读取时会退回它的旧说明。</summary>
    internal static string ManagerHotkeyDescriptionKey(string guid, string section, string key) => ManagerHotkeyPrefix + HotkeyStableId(guid, section, key);

    internal static string GetHotkeyDescription(string guid, string section, string key)
    {
        if (ShowingOriginal) return "";
        var entries = SelectedLanguageGroup?.Entries;
        if (entries == null) return "";
        string stable = HotkeyStableId(guid, section, key);
        foreach (string id in new[] { HotkeyPrefix + stable, ManagerHotkeyPrefix + stable })
            if (entries.TryGetValue(id, out string? value) && !string.IsNullOrWhiteSpace(value) && value != id) return value;
        return "";
    }
}
