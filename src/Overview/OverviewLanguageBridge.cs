#nullable enable
using System;
using System.Collections.Generic;

namespace ModConfigEnhance.Overview;

/// <summary>1.1.0（MCE）：模组名称 / 简介的显示译名与改写，照抄游戏内模组管理器同名类（取代 MCE 旧的精简版）。
/// 译名、简介都写当前语言文件（Loc.WriteExternalTranslations）；裁掉别名与简介覆盖（整合包作者功能）。</summary>
internal static class OverviewLanguageBridge
{
    internal static int Revision => Loc.TranslationRevision;
    internal static string Selection => Plugin.LanguageFile.Value ?? "";
    internal static List<Loc.LanguageFile> LanguageOptions => Loc.Scan();
    internal static string CurrentDisplayName => Selection.Length == 0 ? Loc.Get("LANG_FOLLOW") : Loc.SelectedLanguageGroup?.DisplayOrStem ?? Selection;
    internal static bool Select(string selection, out string error)
    {
        string previous = Selection; error = "";
        Plugin.LanguageFile.Value = selection ?? "";
        var result = Loc.Apply(selection ?? "", notify: false, refresh: true);
        if (!result.Success) { Plugin.LanguageFile.Value = previous; Loc.InvalidateEditingTranslations(); error = result.Message; return false; }
        return true;
    }
    internal static string GetDisplayName(OverviewMod mod)
    {
        if (string.Equals(mod.Guid, Plugin.PluginGuid, StringComparison.OrdinalIgnoreCase))
            return Plugin.PluginName; // 1.1.0（MCE）：自身显示 MCE 的名字（IGMM 这里是「游戏内模组管理器」三语名）
        string raw = string.IsNullOrEmpty(mod.RawName) ? mod.Name : mod.RawName;
        // 1.1.0（MCE）：IGMM「显示原文」时这里直接返回插件原名 raw；MCE 改由 GetName 返回 ModConfig 行名（FixNaming 后），
        // 与设置页侧栏（Loc.TranslateModName）和快捷键页改用扫描结果前一致。IGMM 这里还先看别名 Rule.alias（整合包作者功能），MCE 不要。
        string settingName = string.IsNullOrEmpty(mod.SettingDisplayName) ? Plugin.FixNaming(raw) : mod.SettingDisplayName;
        return GetName(raw, settingName);
    }
    internal static string DisplayNameWithOriginal(OverviewMod mod)
    {
        string primary = GetDisplayName(mod), original = string.IsNullOrWhiteSpace(mod.RawName) ? mod.Name : mod.RawName;
        string Safe(string text) => text.Replace("<", "＜").Replace(">", "＞");
        return Safe(primary) + (ModNameLayout.Equivalent(primary, original) ? "" : " <size=70%><color=#777E78>（" + Safe(original) + "）</color></size>");
    }
    internal static string GetName(string rawName, string settingName)
    {
        if (Loc.ShowingOriginal) return settingName;
        var entries = Loc.SelectedLanguageGroup?.Entries;
        if (entries != null)
        {
            if (entries.TryGetValue(settingName.Trim(), out string? translated) && !string.IsNullOrWhiteSpace(translated)) return translated;
            if (entries.TryGetValue(rawName.Trim(), out translated) && !string.IsNullOrWhiteSpace(translated)) return translated;
        }
        return Loc.TranslateExternal(settingName);
    }
    // 1.1.0（MCE）：IGMM 先看简介覆盖 Rule.descriptionOverridden（整合包作者功能）；MCE 的简介改写只走语言文件（SetDescription）。
    internal static string GetDescription(OverviewMod mod) => GetDescription(mod.Guid, string.IsNullOrEmpty(mod.RawDescription) ? mod.Description : mod.RawDescription);
    private static string DescriptionKey(string guid) => "ModConfigEnhance.Description." + guid;
    internal static string GetDescription(string guid, string original)
    {
        if (!string.IsNullOrWhiteSpace(original)) return Translate(original);
        string key = DescriptionKey(guid);
        if (!string.IsNullOrWhiteSpace(guid) && !Loc.ShowingOriginal && Loc.SelectedLanguageGroup?.Entries.TryGetValue(key, out string? value) == true && !string.IsNullOrEmpty(value) && value != key) return value;
        return "";
    }
    internal static string GetSettingName(string settingName)
    {
        foreach (var plugin in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
            if (Plugin.FixNaming(plugin.Metadata.Name) == settingName) return GetName(plugin.Metadata.Name, settingName);
        return Translate(settingName);
    }
    internal static string Translate(string original) => Loc.TranslateExternal(original);
    internal static bool SetName(OverviewMod mod, string value, out string error)
    {
        string raw = string.IsNullOrEmpty(mod.RawName) ? mod.Name : mod.RawName;
        string settingName = string.IsNullOrEmpty(mod.SettingDisplayName) ? Plugin.FixNaming(raw) : mod.SettingDisplayName;
        return Loc.WriteExternalTranslations(new[] { raw, settingName }, value, out error);
    }
    internal static bool SetDescription(OverviewMod mod, string value, out string error)
    {
        string raw = string.IsNullOrEmpty(mod.RawDescription) ? mod.Description : mod.RawDescription;
        if (string.IsNullOrWhiteSpace(raw))
        {
            if (string.IsNullOrWhiteSpace(mod.Guid)) { error = Loc.T("The mod identity is unavailable.", "无法确认该模组身份。", "無法確認該模組身分。"); return false; }
            return Loc.WriteExternalTranslations(new[] { DescriptionKey(mod.Guid) }, value, out error);
        }
        return Loc.WriteExternalTranslations(new[] { raw }, value, out error);
    }
}

