#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;

namespace ModConfigEnhance;

/// <summary>
/// 翻译自管（从游戏内模组管理器搬回）：各模组的名称、分区、选项名、说明、下拉选项由本模组按语言文件显示，
/// 不再交给 XUnity AutoTranslator。这里放 <c>Loc.Editing.cs</c> / <c>Loc.Regex.cs</c> / <c>MceConfigLabels.cs</c>
/// 用到、而简化版 MCE 原先没有的几个小工具；语言目录结构（内建文件只建不覆、同组可分包）保持原样。
/// </summary>
internal static partial class Loc
{
    /// <summary>键以 <c>MCE_</c> 开头 → 去掉前缀、转大写返回 true。</summary>
    internal static bool TryStripTermPrefix(string key, out string id)
    {
        if (key.StartsWith(KeyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            id = key.Substring(KeyPrefix.Length).ToUpperInvariant();
            return true;
        }

        id = key;
        return false;
    }

    private static string PickTerm(Term term, string code) =>
        code == "zh-cn" ? term.SimplifiedChinese : code == "zh-tw" ? term.TraditionalChinese : term.English;

    /// <summary>三语句子 + 占位符；译文的占位符与英文不一致或格式化失败时回落英文。</summary>
    internal static string F(string english, string simplifiedChinese, string traditionalChinese, params object[] args)
    {
        try
        {
            string translated = T(english, simplifiedChinese, traditionalChinese);
            if (!FormatArguments(english).SetEquals(FormatArguments(translated))) throw new FormatException("Translation placeholders differ from the original.");
            return string.Format(CultureInfo.CurrentCulture, translated, args);
        }
        catch (FormatException)
        {
            try { return string.Format(CultureInfo.CurrentCulture, english, args); }
            catch (FormatException) { return english; }
        }
    }

    private static readonly Regex FormatArgument = new(@"(?<!\{)\{(\d+)(?:[^{}]*)\}(?!\})", RegexOptions.Compiled);
    private static HashSet<string> FormatArguments(string text) => new(FormatArgument.Matches(text).Cast<Match>().Select(m => m.Groups[1].Value), StringComparer.Ordinal);

    /// <summary>查词典用的原文：只统一换行并去首尾空白（完整版在这里还有旧键别名，简化版没有）。</summary>
    internal static string EnglishDisplay(string original) => original.Replace("\r\n", "\n").Replace('\r', '\n').Trim();

    /// <summary>本模组自己的 cfg 选项名 / 说明：语言文件里没写时用内置三语。</summary>
    internal static bool TryConfigText(string original, out string translated)
    {
        string canonical = EnglishDisplay(original);
        foreach (Term term in ConfigTerms)
        {
            if (term.Key.Trim() == canonical || term.English.Trim() == canonical)
            {
                translated = T(term.English, term.SimplifiedChinese, term.TraditionalChinese);
                return true;
            }
        }

        translated = original;
        return false;
    }

    /// <summary>
    /// 模组名（ModConfig 列表里的行名）的显示译文：语言文件里按行名或插件原名写的整句优先，其次走通用入口。
    /// 「显示原文」开着时返回原名。只改显示，行的身份（置顶、导出勾选）仍按原名。
    /// </summary>
    internal static string TranslateModName(string settingName)
    {
        if (string.IsNullOrEmpty(settingName) || ShowingOriginal) return settingName ?? "";
        Dictionary<string, string>? entries = SelectedLanguageGroup?.Entries;
        if (entries != null)
        {
            if (entries.TryGetValue(settingName.Trim(), out string? translated) && !string.IsNullOrWhiteSpace(translated)) return translated;
            string raw = RawPluginName(settingName);
            if (raw.Length > 0 && entries.TryGetValue(raw, out translated) && !string.IsNullOrWhiteSpace(translated)) return translated;
        }

        return TranslateExternal(settingName);
    }

    /// <summary>行名（FixNaming 后）→ 插件原名。插件列表启动后不变，只建一次，免得每次重画都对每个插件跑正则。</summary>
    private static Dictionary<string, string>? _rawPluginNames;

    private static string RawPluginName(string settingName)
    {
        if (_rawPluginNames == null)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (PluginInfo plugin in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
            {
                string raw = (plugin.Metadata?.Name ?? "").Trim();
                if (raw.Length == 0) continue;
                string fixedName = Plugin.FixNaming(raw);
                if (!map.ContainsKey(fixedName)) map[fixedName] = raw;
            }

            _rawPluginNames = map;
        }

        return _rawPluginNames.TryGetValue(settingName, out string? found) ? found : "";
    }

    /// <summary>补漏翻用：本模组自己的 cfg 文字在中文界面下有内置译文，也算已翻（与实际显示一致）。</summary>
    internal static bool HasBuiltInConfigText(string original) => UiLanguage != UiLang.En && TryConfigText(original, out _);

    /// <summary>路径在本模组的语言目录里（概览里改译名只写这里的文件）。</summary>
    internal static bool IsPersonalLanguage(string path) =>
        Path.GetFullPath(path).StartsWith(Path.GetFullPath(LanguageDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 已删除的界面词条与旧 cfg 开关名：旧版写出的语言文件里这些行说的是 XUnity 联动，读取时跳过。
    /// 改了含义的词条换成 <c>_V2</c> 新键，旧键列在这里。
    /// </summary>
    internal static readonly HashSet<string> RetiredTerms = new HashSet<string>(StringComparer.Ordinal)
    {
        "NOTICE_NO_XUNITY_CHECK", "NOTICE_NO_XUNITY", "NOTICE_XUNITY_SWITCHED", "NOTICE_XUNITY_SYNCED", "NOTICE_XUNITY_NO_SWITCH",
        "NOTICE_SHOWING_ORIGINAL", "NOTICE_RELOADED", "TIP_UNTRANSLATED", "TIP_TOGGLE_ORIG", "TIP_RELOAD",
        "NOTICE_LANG_APPLIED", "NOTICE_LANG_MISSING", // 1.1.0：没有调用方，已从词条表删除
        "Switch XUnity language with the language file", "Disable XUnity machine translation",
    };

    internal static bool RetiredTerm(string key) => RetiredTerms.Contains(key);
}

/// <summary>
/// 旧版本模组写进 XUnity 词典目录的镜像 <c>ModConfigEnhance_&lt;码&gt;.txt</c>（文件头有本模组的生成标记）只做一次性搬走：
/// 留着的话「显示原文」时 XUnity 会按旧镜像把原文再翻回去。只动带本模组标记的文件，XUnity 的其他文件与设置一概不碰。
/// </summary>
internal static class LegacyXUnityMirrors
{
    private const string Marker = ".mce-xunity-mirrors-moved";

    internal static void MoveAway()
    {
        string language = Loc.LanguageDirectory;
        try
        {
            if (File.Exists(Path.Combine(language, Marker))) return;
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backup = Path.Combine(language, "_xunity-mirror-backup_" + stamp);
            int moved = 0;
            foreach (string root in new[] { Path.Combine(Paths.ConfigPath, "Translation"), Path.Combine(Paths.BepInExRootPath, "Translation") })
            {
                if (!Directory.Exists(root)) continue;
                foreach (string mirror in Directory.GetFiles(root, "ModConfigEnhance_*.txt", SearchOption.AllDirectories))
                {
                    string first = File.ReadLines(mirror, Encoding.UTF8).FirstOrDefault() ?? "";
                    if (!first.StartsWith("// Generated by ModConfig Enhance", StringComparison.Ordinal)) continue;
                    string relative = mirror.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    // 两个根目录各放一个子目录，相对路径相同时不会互相覆盖
                    string target = Path.Combine(backup, root.StartsWith(Paths.ConfigPath, StringComparison.OrdinalIgnoreCase) ? "config" : "BepInEx", relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    if (File.Exists(target)) File.Delete(target);
                    File.Move(mirror, target);
                    moved++;
                }
            }

            Directory.CreateDirectory(language);
            File.WriteAllText(Path.Combine(language, Marker), stamp + "\n", new UTF8Encoding(false));
            if (moved > 0) Plugin.Log.LogInfo($"[语言] 翻译改由本模组自己显示：以前写给 XUnity 的 {moved} 个镜像词典已移到 {backup}");
        }
        catch (Exception ex)
        {
            // 不写标记：下次启动再试；已移动的文件都在备份目录里
            Plugin.Log.LogWarning("[语言] 搬走旧的 XUnity 镜像词典未完成，下次启动重试：" + ex.Message);
        }
    }
}
