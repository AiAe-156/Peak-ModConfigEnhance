#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using PEAKLib.UI;
using UnityEngine;

namespace ModConfigEnhance;

/// <summary>
/// 本模组的语言子系统。
///
/// 词条：每条一个稳定键（<c>MCE_*</c>，写进游戏语言表 <c>LocalizedText.mainTable</c>，与 PEAKLib 的 MenuAPI 同一条路），
/// 界面文字都挂 <c>LocalizedText</c> 组件或在需要时用 <see cref="Get"/> 取，切语言时游戏的 <c>RefreshAllText</c> 统一刷新。
///
/// 语言文件：<c>BepInEx\config\ModConfigEnhance\language\*.txt</c>，格式与 XUnity 词典相同（<c>键=译文</c>，// 注释，\n \= 转义）。
/// 两类行：<c>MCE_*</c> 键行是本模组界面词条；其余行是本模组 cfg 选项的英文原文 → 译文，会镜像给 XUnity（它负责翻设置列表里的文字）。
/// 文件名不以 <c>_</c> 开头的 .txt 才算语言文件；首行可写 <c>// display: 显示名</c>。说明在根目录 <c>readme.md</c>。
/// 语言文件就是「导出翻译」的产物：MCE_ 块 + 各模组选项文本，翻完改名放进来即可，非 MCE_ 行镜像给 XUnity。
///
/// 两种模式：
///   · 跟随游戏语言（默认）：en-us / zh-cn / zh-tw 三份内建文件各填进对应语言槽，其余语言回落英文；
///   · 指定文件：该文件的译文填满全部语言槽，游戏切语言不再影响本模组管的文字。
/// </summary>
internal static class Loc
{
    internal const string KeyPrefix = "MCE_";
    internal const string FollowGame = "";
    internal const string ReadmeFile = "readme.md";
    internal const string MirrorPrefix = "ModConfigEnhance_";

    private const string DisplayMarker = "// display:";

    internal static string RootDirectory => Path.Combine(Paths.ConfigPath, "ModConfigEnhance");
    internal static string LanguageDirectory => Path.Combine(RootDirectory, "language");
    internal static string ExportDirectory => Path.Combine(RootDirectory, "export");

    // ── 内建词条 ──────────────────────────────────────────────────

    internal readonly struct Term
    {
        public readonly string Key;
        public readonly string English;
        public readonly string SimplifiedChinese;
        public readonly string TraditionalChinese;

        public Term(string key, string english, string simplifiedChinese, string traditionalChinese)
        {
            Key = key;
            English = english;
            SimplifiedChinese = simplifiedChinese;
            TraditionalChinese = traditionalChinese;
        }
    }

    /// <summary>界面词条（键不含前缀）。英文是回落值，也是模板里等号右边的默认内容。</summary>
    internal static readonly Term[] Terms =
    {
        // ModConfig 自己的界面文字
        new("SEARCH", "Search", "搜索", "搜尋"),
        new("SEARCH_HERE", "Search here", "在此处搜索", "在此處搜尋"),
        new("MODS", "MODS", "模组", "模組"),
        new("SECTIONS", "SECTIONS", "类目", "類目"),
        new("DEFAULT", "DEFAULT", "默认", "預設"),
        new("CLEAR", "CLEAR", "清空", "清除"),
        new("SELECT_KEY", "SELECT A KEY", "请按下按键", "請按下按鍵"),
        new("BOUND_TO", "This key is bound to: ", "此键已绑定给：", "此鍵已綁定給："),
        new("FILTER_BOOLS", "Bools", "开关", "開關"),
        new("FILTER_STRINGS", "Strings", "文本", "文字"),
        new("FILTER_NUMBERS", "Numbers", "数值", "數值"),
        new("FILTER_ENUMS", "Enums", "下拉框", "下拉框"),
        new("FILTER_CONTROLS", "Controls", "按键", "按鍵"),
        new("DD_NOTHING", "Nothing", "无", "無"),
        new("DD_EVERYTHING", "Everything", "全部", "全部"),
        new("DD_MIXED", "Mixed...", "部分", "部分"),
        // 左栏表头 / 复选框
        new("PIN", "Pin", "置顶", "置頂"),
        new("EXPORT_COL", "Export", "导出", "導出"),
        new("EXPORT_SELECTED", "Selected only", "仅导出已选", "僅導出已選"),
        new("KEYS_ONLY", "Keybind options only", "只显示快捷键选项", "只顯示快捷鍵選項"),
        // 工具行按钮
        new("BTN_EXPORT", "Export texts", "导出翻译", "導出翻譯"),
        new("BTN_UNTRANSLATED", "Untranslated", "查漏翻", "查漏翻"),
        new("BTN_SHOW_ORIGINAL", "Show original", "显示原文", "顯示原文"),
        new("BTN_SHOW_TRANSLATED", "Show translated", "显示译文", "顯示譯文"),
        new("BTN_RELOAD", "Reload texts", "重载翻译", "重載翻譯"),
        new("BTN_OPEN_FOLDER", "Open folder", "打开目录", "打開目錄"),
        new("BTN_REFRESH", "Refresh", "刷新", "重新整理"),
        new("LANG_FOLLOW", "Follow game language", "跟随游戏语言", "跟隨遊戲語言"),
        // 说明面板
        new("META_DEFAULT", "Default", "默认", "預設"),
        new("META_RANGE", "Range", "范围", "範圍"),
        new("META_OPTIONS", "Options", "可选", "可選"),
        new("META_ON", "ON", "开", "開"),
        new("META_OFF", "OFF", "关", "關"),
        new("META_EMPTY", "(empty)", "（空）", "（空）"),
        // 提示（说明面板里显示；{0} {1} {2} 是占位符，译文里保留）
        new("NOTICE_NO_XUNITY_CHECK", "XUnity AutoTranslator is not installed, so the untranslated check is unavailable.", "没装 XUnity AutoTranslator，查漏翻不可用。", "沒裝 XUnity AutoTranslator，查漏翻不可用。"),
        new("NOTICE_NO_XUNITY", "XUnity AutoTranslator is not installed or its structure has changed.", "没装 XUnity AutoTranslator，或它的结构已变。", "沒裝 XUnity AutoTranslator，或它的結構已變。"),
        new("NOTICE_SHOWING_TRANSLATED", "Showing translations.", "已切回译文。", "已切回譯文。"),
        new("NOTICE_SHOWING_ORIGINAL", "Showing original texts (click again to restore; same as Alt+T).", "正在显示原文（再点一次切回译文；XUnity 快捷键 Alt+T 同效）。", "正在顯示原文（再點一次切回譯文；XUnity 快捷鍵 Alt+T 同效）。"),
        new("NOTICE_RELOADED", "Re-read the language file and the dictionaries from disk.", "已从磁盘重新读取语言文件与词典。", "已從磁碟重新讀取語言檔案與詞典。"),
        new("NOTICE_EXPORT_FAILED", "Export failed, see the log.", "导出失败，详见日志。", "導出失敗，詳見日誌。"),
        new("NOTICE_UNTRANSLATED", "{0} untranslated ({1} translated, {2} already CJK)", "未翻译 {0} 条（已翻 {1} 条，已是中文 {2} 条）", "未翻譯 {0} 條（已翻 {1} 條，已是中文 {2} 條）"),
        new("NOTICE_EXPORTED", "Exported {0} entries", "已导出 {0} 条", "已導出 {0} 條"),
        new("NOTICE_SELECTED_MODS", ", {0} selected mods", "，{0} 个已选模组", "，{0} 個已選模組"),
        new("NOTICE_OPEN_FOLDER_FAILED", "Could not open the folder: {0}", "打开目录失败：{0}", "打開目錄失敗：{0}"),
        new("NOTICE_LANG_APPLIED", "Language file applied: {0} ({1} entries).", "已应用语言文件：{0}（{1} 条）。", "已套用語言檔案：{0}（{1} 條）。"),
        new("NOTICE_LANG_FOLLOW", "Following the game language (built-in en-us / zh-cn / zh-tw).", "已改为跟随游戏语言（内建 en-us / zh-cn / zh-tw）。", "已改為跟隨遊戲語言（內建 en-us / zh-cn / zh-tw）。"),
        new("NOTICE_LANG_MISSING", "{0}: {1} entries missing, they fall back to English. See {2}.", "{0}：缺 {1} 条，缺的回落英文。参考 {2}。", "{0}：缺 {1} 條，缺的回落英文。參考 {2}。"),
        new("NOTICE_LANG_ERROR", "Language file error in {0}: {1}", "语言文件 {0} 有错：{1}", "語言檔案 {0} 有錯：{1}"),
        new("NOTICE_LANG_NO_ENTRIES", "no valid \"key=text\" line", "没有一行有效的「键=译文」", "沒有一行有效的「鍵=譯文」"),
        new("NOTICE_LANG_BAD_LINES", "lines without \"=\": {0}", "缺少等号的行：{0}", "缺少等號的行：{0}"),
        new("NOTICE_LANG_NOT_FOUND", "file not found (click Refresh)", "文件不存在（点刷新）", "檔案不存在（點重新整理）"),
        new("NOTICE_XUNITY_SWITCHED", "XUnity switched to \"{0}\" ({1} entries handed over); texts without a translation show the original.", "XUnity 已切到「{0}」（交给它 {1} 条）；词典里没有的文本显示原文。", "XUnity 已切到「{0}」（交給它 {1} 條）；詞典裡沒有的文字顯示原文。"),
        new("NOTICE_XUNITY_SYNCED", "XUnity dictionary \"{0}\" refreshed ({1} entries handed over).", "XUnity 词典「{0}」已刷新（交给它 {1} 条）。", "XUnity 詞典「{0}」已重新整理（交給它 {1} 條）。"),
        new("NOTICE_XUNITY_NO_SWITCH", "XUnity could not be switched at runtime (structure changed); it stays on \"{0}\". Only this mod's UI changed.", "无法在运行时切换 XUnity（结构不符），它仍是「{0}」；只改了本模组界面。", "無法在執行時切換 XUnity（結構不符），它仍是「{0}」；只改了本模組介面。"),
        new("NOTICE_REFRESHED", "Language folder rescanned: {0} language group(s).", "已重新扫描语言目录：{0} 个语言组。", "已重新掃描語言目錄：{0} 個語言組。"),
    };

    /// <summary>本模组 cfg 选项名 / 说明的译文：键是 cfg 里的英文原文，镜像给 XUnity 后由它翻设置列表。</summary>
    internal static readonly Term[] ConfigTerms =
    {
        new("Tree sidebar", "Tree sidebar", "树形侧栏", "樹形側欄"),
        new("Description panel", "Description panel", "选项说明面板", "選項說明面板"),
        new("UI localization", "UI localization", "界面本地化", "介面本地化"),
        new("Translation tools", "Translation tools", "翻译工具", "翻譯工具"),
        new("Language row only", "Language row only", "只留语言行", "只留語言行"),
        new("Switch XUnity language with the language file", "Switch XUnity language with the language file", "语言文件同时切换 XUnity 的语言", "語言檔案同時切換 XUnity 的語言"),
        new("Disable XUnity machine translation", "Disable XUnity machine translation", "禁用 XUnity 机翻端点", "禁用 XUnity 機翻端點"),
        new("1. Layout", "1. Layout", "1. 版面", "1. 版面"),
        new("2. Localization", "2. Localization", "2. 本地化", "2. 本地化"),
        new(Plugin.DescTreeSidebar, Plugin.DescTreeSidebar,
            "把 ModConfig 的模组设置页改成「左树右选项」：左栏是竖排的模组列表，选中的模组下面展开它的分区。只改版面，不碰 ModConfig 的数据。改开关需重启。",
            "把 ModConfig 的模組設定頁改成「左樹右選項」：左欄是直排的模組列表，選中的模組下面展開它的分區。只改版面，不碰 ModConfig 的資料。改開關需重啟。"),
        new(Plugin.DescDescriptionPanel, Plugin.DescDescriptionPanel,
            "右侧顶部加一块说明面板：鼠标悬停在哪个选项上，就显示它 cfg 里的说明，以及默认值 / 范围 / 可选项。需要树形侧栏。改开关需重启。",
            "右側頂部加一塊說明面板：滑鼠懸停在哪個選項上，就顯示它 cfg 裡的說明，以及預設值 / 範圍 / 可選項。需要樹形側欄。改開關需重啟。"),
        new(Plugin.DescUiLocalization, Plugin.DescUiLocalization,
            "翻译 ModConfig 自己的界面文字（搜索、默认、清空、筛选项……）与本模组的按钮、提示。文字来自 config\\ModConfigEnhance\\language\\ 下的语言文件，左栏第三行可选。改开关需重启。",
            "翻譯 ModConfig 自己的介面文字（搜尋、預設、清除、篩選項……）與本模組的按鈕、提示。文字來自 config\\ModConfigEnhance\\language\\ 下的語言檔案，左欄第三行可選。改開關需重啟。"),
        new(Plugin.DescTranslationTools, Plugin.DescTranslationTools,
            "左栏工具行：导出翻译 / 查漏翻 / 仅导出已选；显示原文 / 重载翻译 / 打开目录。导出文件写到 config\\ModConfigEnhance\\export\\。查漏翻、显示原文、重载翻译需要 XUnity AutoTranslator。改开关需重启。",
            "左欄工具列：導出翻譯 / 查漏翻 / 僅導出已選；顯示原文 / 重載翻譯 / 打開目錄。導出檔案寫到 config\\ModConfigEnhance\\export\\。查漏翻、顯示原文、重載翻譯需要 XUnity AutoTranslator。改開關需重啟。"),
        new(Plugin.DescLanguageRowOnly, Plugin.DescLanguageRowOnly,
            "模组列表上方的工具行只保留语言行（语言下拉 + 刷新）：「导出翻译 / 查漏翻 / 仅导出已选」和「显示原文 / 重载翻译 / 打开目录」两排收起，列表自动上移不留空白。「导出」列在此期间隐藏，勾选状态保留。「翻译工具」关着时本项无效。改开关需重启。",
            "模組列表上方的工具列只保留語言行（語言下拉 + 重新整理）：「導出翻譯 / 查漏翻 / 僅導出已選」和「顯示原文 / 重載翻譯 / 打開目錄」兩排收起，列表自動上移不留空白。「導出」欄在此期間隱藏，勾選狀態保留。「翻譯工具」關著時本項無效。改開關需重啟。"),
        new(Plugin.DescSwitchXUnity, Plugin.DescSwitchXUnity,
            "选中语言文件（或跟随游戏时游戏语言变了）后，立刻把 XUnity AutoTranslator 切到该语言：目标语言与词典目录变成 Translation\\<码>\\Text\\，文件里除 MCE_ 键外的译文写成那里的 ModConfigEnhance_<码>.txt，重载词典，并把 AutoTranslatorConfig.ini 的 Language= 改成该码供下次启动。词典里没有的文本还原成原文。关掉则完全不碰 XUnity。",
            "選中語言檔案（或跟隨遊戲時遊戲語言變了）後，立刻把 XUnity AutoTranslator 切到該語言：目標語言與詞典目錄變成 Translation\\<碼>\\Text\\，檔案裡除 MCE_ 鍵外的譯文寫成那裡的 ModConfigEnhance_<碼>.txt，重載詞典，並把 AutoTranslatorConfig.ini 的 Language= 改成該碼供下次啟動。詞典裡沒有的文字還原成原文。關掉則完全不碰 XUnity。"),
        new(Plugin.DescTameXUnity, Plugin.DescTameXUnity,
            "XUnity AutoTranslator 在场时，启动就把 AutoTranslatorConfig.ini 的 Endpoint / FallbackEndpoint 清空，并把当前选中的翻译端点摘掉——它的出厂默认 GoogleTranslateV2 会把每个界面文本丢去机翻（满屏怪翻译就是这么来的）。词典与镜像照常生效，机翻请求不再发出。确实要用机翻请关掉本项。",
            "XUnity AutoTranslator 在場時，啟動就把 AutoTranslatorConfig.ini 的 Endpoint / FallbackEndpoint 清空，並把當前選中的翻譯端點摘掉——它的出廠預設 GoogleTranslateV2 會把每個介面文字丟去機翻（滿屏怪翻譯就是這麼來的）。詞典與鏡像照樣生效，機翻請求不再發出。確實要用機翻請關掉本項。"),
    };

    // ── 取词 ──────────────────────────────────────────────────────

    private static bool _registered;

    /// <summary>按当前语言取词；语言表里没有就退回英文。</summary>
    internal static string Get(string key)
    {
        EnsureRegistered();
        string value = LocalizedText.GetText(KeyPrefix + key, printDebug: false);
        if (!string.IsNullOrEmpty(value))
        {
            return value;
        }

        foreach (Term term in Terms)
        {
            if (term.Key == key)
            {
                return term.English;
            }
        }

        return key;
    }

    internal static string Format(string key, params object[] args)
    {
        try
        {
            return string.Format(Get(key), args);
        }
        catch (FormatException)
        {
            // 译文把 {0} 写坏了：退回英文
            foreach (Term term in Terms)
            {
                if (term.Key == key)
                {
                    return string.Format(term.English, args);
                }
            }

            return key;
        }
    }

    internal static string Index(string key) => KeyPrefix + key;

    /// <summary>第一次用到时把词条按当前模式写进语言表（主菜单构建页面时才会走到，避开插件加载期）。</summary>
    internal static void EnsureRegistered()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        try
        {
            Apply(Plugin.LanguageFile.Value, notify: false, refresh: false);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[语言] 首次注册词条失败，退回内建英文: {ex}");
            RegisterBuiltIn();
        }
    }

    /// <summary>语言表被整表重载（ReloadAll）后词条会丢，语言切换回调里补回去。</summary>
    internal static void ReRegister()
    {
        _registered = false;
        EnsureRegistered();
    }

    private static void RegisterBuiltIn()
    {
        foreach (Term term in Terms)
        {
            MenuAPI.CreateLocalization(KeyPrefix + term.Key)
                .AddLocalization(term.English, LocalizedText.Language.English)
                .AddLocalization(term.SimplifiedChinese, LocalizedText.Language.SimplifiedChinese)
                .AddLocalization(term.TraditionalChinese, LocalizedText.Language.TraditionalChinese);
        }
    }

    // ── 语言文件 ──────────────────────────────────────────────────

    internal sealed class LanguageFile
    {
        internal string Path = "";
        internal string Stem = "";
        internal string Display = "";
        internal string Code = "";
        internal readonly List<string> Paths = new List<string>();
        /// <summary>键 → 译文；MCE_ 键已去前缀并大写，其余键原样。</summary>
        internal readonly Dictionary<string, string> Entries = new Dictionary<string, string>(StringComparer.Ordinal);
        internal readonly List<int> BadLines = new List<int>();

        internal string Name => System.IO.Path.GetFileName(Path);
        internal int FileCount => Paths.Count > 0 ? Paths.Count : 1;
    }

    /// <summary>目录下的语言组（只看当前目录，不含 _ 开头的模板）。同组内基础文件优先，其余按文件名排序。</summary>
    internal static List<LanguageFile> Scan()
    {
        Dictionary<string, LanguageFile> groups = new Dictionary<string, LanguageFile>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(LanguageDirectory))
        {
            return new List<LanguageFile>();
        }

        string[] files = Directory.GetFiles(LanguageDirectory, "*.txt");
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            if (name.StartsWith("_", StringComparison.Ordinal))
            {
                continue;
            }

            string stem = Path.GetFileNameWithoutExtension(file);
            string code = LanguageCodeFromStem(stem);
            if (!groups.TryGetValue(code, out LanguageFile? item))
            {
                item = new LanguageFile { Code = code, Stem = code, Display = code };
                groups.Add(code, item);
            }

            item.Paths.Add(file);
        }

        List<LanguageFile> result = new List<LanguageFile>(groups.Values);
        foreach (LanguageFile item in result)
        {
            item.Paths.Sort(CompareGroupFiles);
            item.Path = item.Paths[0];
            item.Display = ReadDisplayName(item.Path) ?? item.Code;
        }

        result.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Code, b.Code));
        return result;
    }

    private static int CompareGroupFiles(string left, string right)
    {
        string leftStem = Path.GetFileNameWithoutExtension(left);
        string rightStem = Path.GetFileNameWithoutExtension(right);
        bool leftBase = leftStem.IndexOf('_') < 0;
        bool rightBase = rightStem.IndexOf('_') < 0;
        if (leftBase != rightBase)
        {
            return leftBase ? -1 : 1;
        }

        return StringComparer.OrdinalIgnoreCase.Compare(Path.GetFileName(left), Path.GetFileName(right));
    }

    internal static string LanguageCodeFromStem(string stem)
    {
        int separator = stem.IndexOf('_');
        return NormalizeLanguageCode(separator < 0 ? stem : stem.Substring(0, separator));
    }

    private static string NormalizeLanguageCode(string value)
    {
        string code = value.Trim().Replace('_', '-').ToLowerInvariant();
        if (code == "zh")
        {
            return "zh-cn";
        }

        if (code == "en")
        {
            return "en-us";
        }

        return code;
    }

    internal static string NormalizeSelection(string selection)
    {
        return string.IsNullOrWhiteSpace(selection) ? FollowGame : LanguageCodeFromStem(selection);
    }

    private static string? ReadDisplayName(string file)
    {
        try
        {
            using StreamReader reader = new StreamReader(file, Encoding.UTF8);
            for (int i = 0; i < 5; i++)
            {
                string? line = reader.ReadLine();
                if (line == null)
                {
                    break;
                }

                if (line.StartsWith(DisplayMarker, StringComparison.OrdinalIgnoreCase))
                {
                    string value = line.Substring(DisplayMarker.Length).Trim();
                    return value.Length > 0 ? value : null;
                }
            }
        }
        catch
        {
        }

        return null;
    }

    internal static LanguageFile Parse(string file)
    {
        LanguageFile result = new LanguageFile { Path = file, Stem = Path.GetFileNameWithoutExtension(file) };
        result.Code = LanguageCodeFromStem(result.Stem);
        result.Display = ReadDisplayName(file) ?? result.Stem;
        Dictionary<string, int> sourceLines = new Dictionary<string, int>(StringComparer.Ordinal);
        int lineNo = 0;
        foreach (string raw in File.ReadLines(file, Encoding.UTF8))
        {
            lineNo++;
            string line = raw.TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal) || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            int eq = FindUnescapedEquals(line);
            if (eq <= 0)
            {
                result.BadLines.Add(lineNo);
                continue;
            }

            string key = Unescape(line.Substring(0, eq)).Trim();
            string value = Unescape(line.Substring(eq + 1));
            if (key.Length == 0 || string.IsNullOrWhiteSpace(value))
            {
                continue; // 等号右边留空 = 未翻，回落
            }

            if (key.StartsWith(KeyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                key = key.Substring(KeyPrefix.Length).ToUpperInvariant();
            }

            bool incomingEffective = IsEffectiveTranslation(key, value, result.Code);
            if (result.Entries.TryGetValue(key, out string? oldValue))
            {
                bool oldEffective = IsEffectiveTranslation(key, oldValue, result.Code);
                if ((!incomingEffective || IsMceEnglishPlaceholder(key, value)) && oldEffective &&
                    !string.Equals(oldValue, value, StringComparison.Ordinal))
                {
                    continue;
                }

                if (incomingEffective && oldEffective && !string.Equals(oldValue, value, StringComparison.Ordinal))
                {
                    Plugin.Log.LogWarning($"[语言] {Path.GetFileName(file)} 键冲突「{key}」：第 {sourceLines[key]} 行 → 第 {lineNo} 行，采用后者。");
                }
            }

            result.Entries[key] = value;
            sourceLines[key] = lineNo;
        }

        return result;
    }

    /// <summary>先完整解析并校验语言组，再按确定顺序合并；任何文件失败都不会产生外部状态变化。</summary>
    internal static LanguageFile ParseGroup(string selection)
    {
        string code = NormalizeSelection(selection);
        LanguageFile? group = null;
        foreach (LanguageFile candidate in Scan())
        {
            if (string.Equals(candidate.Code, code, StringComparison.OrdinalIgnoreCase))
            {
                group = candidate;
                break;
            }
        }

        if (group == null)
        {
            throw new FileNotFoundException(Get("NOTICE_LANG_NOT_FOUND"), code + ".txt");
        }

        LanguageFile merged = new LanguageFile
        {
            Code = group.Code,
            Stem = group.Code,
            Path = group.Path,
            Display = group.Display,
        };
        merged.Paths.AddRange(group.Paths);
        Dictionary<string, string> sources = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in group.Paths)
        {
            LanguageFile file = Parse(path);
            if (file.BadLines.Count > 0)
            {
                throw new InvalidDataException(Format("NOTICE_LANG_BAD_LINES", Path.GetFileName(path) + ": " + JoinLines(file.BadLines)));
            }

            foreach (KeyValuePair<string, string> pair in file.Entries)
            {
                bool incomingEffective = IsEffectiveTranslation(pair.Key, pair.Value, merged.Code);
                if (merged.Entries.TryGetValue(pair.Key, out string? oldValue))
                {
                    bool oldEffective = IsEffectiveTranslation(pair.Key, oldValue, merged.Code);
                    if ((!incomingEffective || IsMceEnglishPlaceholder(pair.Key, pair.Value)) && oldEffective &&
                        !string.Equals(oldValue, pair.Value, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (incomingEffective && oldEffective && !string.Equals(oldValue, pair.Value, StringComparison.Ordinal))
                    {
                        Plugin.Log.LogWarning($"[语言] {group.Code} 键冲突「{pair.Key}」：{sources[pair.Key]} → {Path.GetFileName(path)}，采用后者。");
                    }
                }

                merged.Entries[pair.Key] = pair.Value;
                sources[pair.Key] = Path.GetFileName(path);
            }
        }

        if (merged.Entries.Count == 0)
        {
            throw new InvalidDataException(Get("NOTICE_LANG_NO_ENTRIES"));
        }

        return merged;
    }

    private static bool IsEffectiveTranslation(string key, string value, string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!IsTermKey(key))
        {
            return !string.Equals(key, value, StringComparison.Ordinal);
        }

        foreach (Term term in Terms)
        {
            if (term.Key == key)
            {
                return !string.Equals(term.English, value, StringComparison.Ordinal) ||
                       string.Equals(NormalizeLanguageCode(languageCode ?? ""), "en-us", StringComparison.Ordinal);
            }
        }

        return true;
    }

    private static bool IsMceEnglishPlaceholder(string key, string value)
    {
        foreach (Term term in Terms)
        {
            if (term.Key == key)
            {
                return string.Equals(term.English, value, StringComparison.Ordinal);
            }
        }

        return false;
    }

    internal static HashSet<string> CurrentTranslatedTermKeys()
    {
        HashSet<string> result = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            string selection = Plugin.LanguageFile != null ? Plugin.LanguageFile.Value ?? "" : "";
            LanguageFile? group = ParseOptionalGroup(string.IsNullOrEmpty(selection) ? FollowStem() : selection);
            if (group != null)
            {
                foreach (KeyValuePair<string, string> pair in group.Entries)
                {
                    if (IsTermKey(pair.Key) && IsEffectiveTranslation(pair.Key, pair.Value, group.Code))
                    {
                        result.Add(pair.Key);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[语言] 查漏翻读取当前语言组失败，MCE 词条按未翻处理: {ex.Message}");
        }

        return result;
    }

    internal static int FindUnescapedEquals(string line)
    {
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '=' && (i == 0 || line[i - 1] != '\\'))
            {
                return i;
            }
        }

        return -1;
    }

    internal static string Escape(string text)
    {
        return text.Replace("\r", "").Replace("\n", "\\n").Replace("=", "\\=");
    }

    internal static string Unescape(string text)
    {
        return text.Replace("\\=", "=").Replace("\\n", "\n");
    }

    // ── 应用 ──────────────────────────────────────────────────────

    /// <summary>说明面板要显示的结果：正常一行，出错用警告色。</summary>
    internal sealed class ApplyResult
    {
        internal string Message = "";
        internal bool Warning;
        internal string Mirror = "";
        internal bool Success;
    }

    /// <summary>
    /// 按 <paramref name="selection"/>（空 = 跟随游戏语言，否则是文件名不含扩展名）把词条写进语言表，
    /// 需要时刷新全部 LocalizedText 组件并镜像给 XUnity。
    /// </summary>
    internal static ApplyResult Apply(string selection, bool notify, bool refresh)
    {
        _registered = true;
        ApplyResult result = new ApplyResult();
        bool appliedSuccessfully = false;
        try
        {
            LanguageFile? preparedGroup;
            if (string.IsNullOrEmpty(selection))
            {
                preparedGroup = ApplyFollowGame();
                result.Message = Get("NOTICE_LANG_FOLLOW");
            }
            else
            {
                LanguageFile file = ParseGroup(selection);
                preparedGroup = file;
                int applied = ApplyFile(file);
                result.Message = Format("NOTICE_LANG_APPLIED", file.Display + " ×" + file.FileCount, applied);
                int missing = Terms.Length - applied;
                if (missing > 0)
                {
                    result.Warning = true;
                    result.Message += "\n" + Format("NOTICE_LANG_MISSING", file.Code, missing, "en-us.txt");
                }
            }

            result.Mirror = SyncXUnity(selection, preparedGroup);
            WriteReadme();
            if (result.Mirror.Length > 0)
            {
                result.Message += "\n" + result.Mirror;
            }

            appliedSuccessfully = true;
            result.Success = true;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[语言] 应用「{selection}」失败，保留原状态: {ex}");
            result.Warning = true;
            result.Message = Format("NOTICE_LANG_ERROR", selection, ex.Message);
        }
        finally
        {
            if (refresh && appliedSuccessfully)
            {
                _refreshing = true;
                try
                {
                    RefreshOwnTexts();
                }
                finally
                {
                    _refreshing = false;
                }
            }
        }

        if (notify)
        {
            ModConfigDescriptionPanel.Current?.ShowNotice(result.Message, result.Warning);
        }

        Plugin.Log.LogInfo($"[语言] {result.Message.Replace("\n", " | ")}");
        return result;
    }

    private static bool _refreshing;

    /// <summary>
    /// 只刷本模组的 LocalizedText（索引 MCE_ 开头）。不能用 <c>LocalizedText.RefreshAllText()</c>：
    /// ModConfig 的选项单元格是原版设置单元格的克隆，文字上还挂着索引为空的 LocalizedText，全局刷一遍会把它们都变成「LOC: 」。
    /// </summary>
    private static void RefreshOwnTexts()
    {
        LocalizedText[] all = UnityEngine.Object.FindObjectsByType<LocalizedText>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (LocalizedText text in all)
        {
            if (text != null && !string.IsNullOrEmpty(text.index) && text.index.StartsWith(KeyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                text.RefreshText();
            }
        }

        ModConfigLocalizationPatch.RefreshStaticTexts();
    }

    /// <summary>Apply 自己触发的 RefreshAllText 会回调 OnLanguageChanged，别在那里再 Apply 一次。</summary>
    internal static bool IsRefreshing => _refreshing;

    private static LanguageFile? ApplyFollowGame()
    {
        string code = FollowStem();
        // 只严格校验当前游戏语言组；其它语言组的坏分包不能阻止当前语言生效。
        LanguageFile? current = ParseOptionalGroup(code);
        RegisterBuiltIn();
        OverlayBuiltInGroup(current, LocalizedText.CURRENT_LANGUAGE);
        return current;
    }

    private static LanguageFile? ParseOptionalGroup(string code)
    {
        foreach (LanguageFile group in Scan())
        {
            if (string.Equals(group.Code, code, StringComparison.OrdinalIgnoreCase))
            {
                return ParseGroup(code);
            }
        }

        return null;
    }

    private static void OverlayBuiltInGroup(LanguageFile? file, LocalizedText.Language language)
    {
        if (file == null)
        {
            return;
        }

        foreach (Term term in Terms)
        {
            if (file.Entries.TryGetValue(term.Key, out string? value) && IsEffectiveTranslation(term.Key, value, file.Code))
            {
                MenuAPI.CreateLocalization(KeyPrefix + term.Key).AddLocalization(value, language);
            }
        }
    }

    /// <summary>文件里的译文填满全部语言槽；文件没有的键填英文。返回命中的词条数。</summary>
    private static int ApplyFile(LanguageFile file)
    {
        int applied = 0;
        foreach (Term term in Terms)
        {
            bool hasTranslation = file.Entries.TryGetValue(term.Key, out string? translated) &&
                                  IsEffectiveTranslation(term.Key, translated, file.Code);
            string value = hasTranslation ? translated! : term.English;
            if (hasTranslation)
            {
                applied++;
            }

            List<string> row = new List<string>();
            foreach (LocalizedText.Language _ in Enum.GetValues(typeof(LocalizedText.Language)))
            {
                row.Add(value);
            }

            LocalizedText.mainTable[(KeyPrefix + term.Key).ToUpperInvariant()] = row;
        }

        return applied;
    }

    private static string JoinLines(List<int> lines)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < lines.Count && i < 8; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append(lines[i]);
        }

        if (lines.Count > 8)
        {
            sb.Append(" …");
        }

        return sb.ToString();
    }

    // ── 让 XUnity 跟着切语言 ──────────────────────────────────────

    /// <summary>
    /// 选中语言文件（或跟随游戏时游戏语言变了）后：把 XUnity 的目标语言切到该文件的语言码，词典目录随之变成
    /// <c>Translation\&lt;码&gt;\Text\</c>；文件里非 MCE_ 的行写成那个目录下的 <c>ModConfigEnhance_&lt;码&gt;.txt</c>（其他语言目录里的旧镜像先删）；
    /// 重载词典——词典里没有的文本 XUnity 会还原成原文；再把 Language= 写回 AutoTranslatorConfig.ini 让下次启动一致。
    /// 返回给用户看的一行说明（空 = 没什么可说）。
    /// </summary>
    internal static string SyncXUnity(string selection)
    {
        return SyncXUnity(selection, null);
    }

    private static string SyncXUnity(string selection, LanguageFile? preparedGroup)
    {
        if (!XUnityBridge.Installed || !Plugin.SwitchXUnity.Value)
        {
            return "";
        }

        string stem = string.IsNullOrEmpty(selection) ? FollowStem() : NormalizeSelection(selection);
        try
        {
            // 在切 XUnity、删除旧镜像之前完成整组读取与校验。
            LanguageFile? sourceGroup = preparedGroup ?? ParseOptionalGroup(stem);
            int count = 0;
            string? mirrorContent = null;
            if (sourceGroup != null)
            {
                StringBuilder mirror = new StringBuilder();
                mirror.Append("// Generated by ModConfig Enhance from language group ").Append(sourceGroup.Code).Append(" (").Append(sourceGroup.FileCount).Append(" files) - do not edit.\n");
                foreach (KeyValuePair<string, string> pair in sourceGroup.Entries)
                {
                    if (IsTermKey(pair.Key) || pair.Key == pair.Value)
                    {
                        continue;
                    }

                    mirror.Append(Escape(pair.Key)).Append('=').Append(Escape(pair.Value)).Append('\n');
                    count++;
                }

                if (count > 0)
                {
                    mirrorContent = mirror.ToString();
                }
            }

            string? root = XUnityBridge.TranslationRoot;
            string code = ResolveXUnityCode(stem, root);
            bool switched = false;
            string? current = XUnityBridge.TargetLanguage;
            if (!string.Equals(current, code, StringComparison.OrdinalIgnoreCase))
            {
                if (!XUnityBridge.CanSetLanguage)
                {
                    Plugin.Log.LogWarning("[语言] XUnity 结构对不上，不能在运行时切它的目标语言；只改本模组界面。");
                    return Format("NOTICE_XUNITY_NO_SWITCH", current ?? "?");
                }

                switched = XUnityBridge.SetLanguage(code);
                if (switched)
                {
                    WriteIniLanguage(code);
                }
            }

            string? dir = XUnityBridge.TranslationsPath;
            if (string.IsNullOrEmpty(dir))
            {
                return "";
            }

            Directory.CreateDirectory(dir);
            string? mirrorPath = null;
            if (mirrorContent != null)
            {
                mirrorPath = Path.Combine(dir!, MirrorPrefix + code + ".txt");
                string temporary = mirrorPath + ".tmp";
                try
                {
                    File.WriteAllText(temporary, mirrorContent, new UTF8Encoding(false));
                    File.Copy(temporary, mirrorPath, true);
                }
                finally
                {
                    if (File.Exists(temporary))
                    {
                        File.Delete(temporary);
                    }
                }
            }

            // 新镜像落盘成功后才清理其它语言的旧镜像；当前刚写好的文件明确保留。
            DeleteAllMirrors(root, dir!, mirrorPath);

            XUnityBridge.ReloadTranslations();
            return Format(switched ? "NOTICE_XUNITY_SWITCHED" : "NOTICE_XUNITY_SYNCED", code, count);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[语言] 同步 XUnity 失败: {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// 文件名 → XUnity 语言码：Translation\ 下已有同名目录用同名；有「同一语言」的目录（zh-cn ↔ zh）沿用它，不另起目录；
    /// 否则按 XUnity 圈子的惯例取主语言子标签（en-us → en、de-de → de），繁中例外用 zh-TW。
    /// </summary>
    internal static string ResolveXUnityCode(string stem, string? root)
    {
        stem = stem.Trim().ToLowerInvariant().Replace('_', '-');
        string fallback = stem.StartsWith("zh-tw") || stem.StartsWith("zh-hk") || stem.StartsWith("zh-hant") ? "zh-TW" : stem.Split('-')[0];
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            return fallback;
        }

        string? sameLanguage = null;
        foreach (string dir in Directory.GetDirectories(root))
        {
            string name = Path.GetFileName(dir);
            if (string.Equals(name, stem, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }

            if (sameLanguage == null && SameLanguage(stem, name))
            {
                sameLanguage = name;
            }
        }

        return sameLanguage ?? fallback;
    }

    private static void DeleteAllMirrors(string? root, string currentDir, string? keepPath)
    {
        IEnumerable<string> dirs = !string.IsNullOrEmpty(root) && Directory.Exists(root)
            ? Directory.GetDirectories(root!)
            : new[] { currentDir };
        foreach (string langDir in dirs)
        {
            string textDir = Path.Combine(langDir, "Text");
            foreach (string probe in new[] { textDir, langDir, currentDir })
            {
                if (!Directory.Exists(probe))
                {
                    continue;
                }

                foreach (string old in Directory.GetFiles(probe, MirrorPrefix + "*.txt"))
                {
                    if (!string.IsNullOrEmpty(keepPath) && string.Equals(Path.GetFullPath(old), Path.GetFullPath(keepPath), StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    File.Delete(old);
                }
            }
        }
    }

    /// <summary>把 Language= 写回 AutoTranslatorConfig.ini（只改这一行，其余原样），下次启动 XUnity 直接用新语言。</summary>
    private static void WriteIniLanguage(string code)
    {
        try
        {
            string ini = Path.Combine(Paths.ConfigPath, "AutoTranslatorConfig.ini");
            if (!File.Exists(ini))
            {
                return;
            }

            byte[] bytes = File.ReadAllBytes(ini);
            bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            string text = Encoding.UTF8.GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
            string newline = text.Contains("\r\n") ? "\r\n" : "\n";
            string[] lines = text.Split('\n');
            bool inGeneral = false;
            bool done = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                if (line.StartsWith("[", StringComparison.Ordinal))
                {
                    inGeneral = line.Trim().Equals("[General]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (inGeneral && line.StartsWith("Language=", StringComparison.Ordinal))
                {
                    lines[i] = "Language=" + code + (lines[i].EndsWith("\r") ? "\r" : "");
                    done = true;
                    break;
                }
            }

            if (!done)
            {
                return;
            }

            File.WriteAllText(ini, string.Join("\n", lines), new UTF8Encoding(bom));
            Plugin.Log.LogInfo($"[语言] AutoTranslatorConfig.ini 的 Language 已改为 {code}（下次启动生效；本次已在运行时切换）。");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[语言] 写 AutoTranslatorConfig.ini 失败: {ex.Message}");
        }
    }

    private static bool IsTermKey(string key)
    {
        foreach (Term term in Terms)
        {
            if (term.Key == key)
            {
                return true;
            }
        }

        return false;
    }

    // ── 当前界面语言（三选一，说明文字按它挑）────────────────────

    internal enum UiLang { En, ZhCn, ZhTw }

    /// <summary>选了语言文件按文件名（zh-cn / zh-tw 前缀），跟随游戏按游戏语言；其余一律英文。</summary>
    internal static UiLang UiLanguage
    {
        get
        {
            string selection = Plugin.LanguageFile != null ? Plugin.LanguageFile.Value ?? "" : "";
            if (selection.Length > 0)
            {
                string s = selection.ToLowerInvariant().Replace('_', '-');
                if (s.StartsWith("zh-tw") || s.StartsWith("zh-hant") || s.StartsWith("zh-hk"))
                {
                    return UiLang.ZhTw;
                }

                return s.StartsWith("zh") ? UiLang.ZhCn : UiLang.En;
            }

            return LocalizedText.CURRENT_LANGUAGE switch
            {
                LocalizedText.Language.SimplifiedChinese => UiLang.ZhCn,
                LocalizedText.Language.TraditionalChinese => UiLang.ZhTw,
                _ => UiLang.En,
            };
        }
    }

    /// <summary>按当前界面语言三选一。</summary>
    internal static string T(string en, string zhCn, string zhTw) => UiLanguage switch
    {
        UiLang.ZhCn => zhCn,
        UiLang.ZhTw => zhTw,
        _ => en,
    };

    /// <summary>config\ModConfigEnhance\readme.md 按当前界面语言重写（启动时与每次切语言后）。</summary>
    internal static void WriteReadme()
    {
        try
        {
            Directory.CreateDirectory(RootDirectory);
            File.WriteAllText(Path.Combine(RootDirectory, ReadmeFile), BuildReadme(), new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[语言] 写 {ReadmeFile} 失败: {ex.Message}");
        }
    }

    internal static string FollowStem()
    {
        return LocalizedText.CURRENT_LANGUAGE switch
        {
            LocalizedText.Language.SimplifiedChinese => "zh-cn",
            LocalizedText.Language.TraditionalChinese => "zh-tw",
            _ => "en-us",
        };
    }

    /// <summary>zh/en 裸码先归一到 zh-cn/en-us；简体与繁体绝不互认。</summary>
    internal static bool SameLanguage(string fileStem, string xunityLanguage)
    {
        string a = NormalizeSelection(fileStem);
        string b = NormalizeLanguageCode(xunityLanguage);
        if (a == b)
        {
            return true;
        }

        string pa = a.Split('-')[0];
        string pb = b.Split('-')[0];
        if (pa != pb)
        {
            return false;
        }

        if (pa == "zh")
        {
            return false;
        }

        return !a.Contains("-") || !b.Contains("-");
    }

    // ── 内建文件 ──────────────────────────────────────────────────

    /// <summary>首启写 en-us / zh-cn / zh-tw（只建不覆），说明文件每次重写。</summary>
    internal static void EnsureBuiltInFiles()
    {
        try
        {
            Directory.CreateDirectory(LanguageDirectory);
            Directory.CreateDirectory(ExportDirectory);
            WriteIfMissing("en-us", "English", t => t.English, t => t.English);
            WriteIfMissing("zh-cn", "简体中文", t => t.SimplifiedChinese, t => t.SimplifiedChinese);
            WriteIfMissing("zh-tw", "繁體中文", t => t.TraditionalChinese, t => t.TraditionalChinese);
            WriteReadme();
            // 旧版每次启动重写的 _template.txt（≤1.0.1）与 _readme.txt（1.0.2–1.0.3）都不含用户内容，直接清掉
            foreach (string legacy in new[] { "_template.txt", "_readme.txt" })
            {
                string path = Path.Combine(LanguageDirectory, legacy);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[语言] 写内建语言文件失败: {ex.Message}");
        }
    }

    private static void WriteIfMissing(string stem, string display, Func<Term, string> pick, Func<Term, string> pickCfg)
    {
        string path = Path.Combine(LanguageDirectory, stem + ".txt");
        if (File.Exists(path))
        {
            return;
        }

        File.WriteAllText(path, BuildFile(display, pick, pickCfg), new UTF8Encoding(false));
    }

    private static string BuildReadme() => LanguageReadme.For(UiLanguage).Replace("{VERSION}", Plugin.PluginVersion);

    private static string BuildFile(string display, Func<Term, string> pick, Func<Term, string> pickCfg)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("// display: ").Append(display).Append('\n');
        sb.Append("// ModConfig Enhance ").Append(Plugin.PluginVersion).Append(" - built-in language file. Edit freely; it is never overwritten. Reset by deleting it.\\n");
        sb.Append("// See ..\\").Append(ReadmeFile).Append(" for the format and how to make a new language.\n");

        sb.Append('\n');
        sb.Append("// ---- UI terms (key=text; keys are stable IDs, do not translate them) ----\n");
        foreach (Term term in Terms)
        {
            sb.Append(KeyPrefix).Append(term.Key).Append('=').Append(Escape(pick(term))).Append('\n');
        }

        sb.Append('\n');
        sb.Append("// ---- Settings of this mod (English original=translation; mirrored into XUnity) ----\n");
        foreach (Term term in ConfigTerms)
        {
            sb.Append(Escape(term.Key)).Append('=').Append(Escape(pickCfg(term))).Append('\n');
        }

        return sb.ToString();
    }
}
