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
/// 语言文件：<c>BepInEx\config\ModConfigEnhance\language\*.txt</c>，格式与 XUnity 词典相同（<c>键=译文</c>，// 注释，\n \= 转义，<c>r:"正则"=译文</c>）。
/// 两类行：<c>MCE_*</c> 键行是本模组界面词条；<c>r:</c> 行是正则规则；其余行是各模组文字的原文 → 译文，由本模组自己显示（侧栏、说明面板、设置页），不再交给 XUnity。
/// 文件名不以 <c>_</c> 开头的 .txt 才算语言文件；首行可写 <c>// display: 显示名</c>。说明在根目录 <c>readme.md</c>。
/// 语言文件就是「导出翻译」的产物：MCE_ 块 + 各模组选项文本，翻完改名放进来即可。
///
/// 两种模式：
///   · 跟随游戏语言（默认）：en-us / zh-cn / zh-tw 三份内建文件各填进对应语言槽，其余语言回落英文；
///   · 指定文件：该文件的译文填满全部语言槽，游戏切语言不再影响本模组管的文字。
/// </summary>
internal static partial class Loc
{
    internal const string KeyPrefix = "MCE_";
    internal const string FollowGame = "";
    internal const string ReadmeFile = "readme.md";

    private const string DisplayMarker = "// display:";

    internal static string RootDirectory => Path.Combine(Paths.ConfigPath, "ModConfigEnhance");
    internal static string LanguageDirectory => Path.Combine(RootDirectory, "language");
    internal static string ExportDirectory => Path.Combine(RootDirectory, "export");
    /// <summary>1.1.0：模组概览的缓存（日志窗口的收藏等，可随时删），<c>config\ModConfigEnhance\cache\</c>（用户 2026-11-15 定 Q7）。
    /// IGMM 同名成员指向 BepInEx\IGMM\；MCE 按目录约定「子目录按用途分」放在自己的配置目录下。</summary>
    internal static string RuntimeDirectory => Path.Combine(RootDirectory, "cache");

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
        new("JUMP_HOTKEYS", "Go to keybindings", "跳转到快捷键设置页", "跳轉到快捷鍵設定頁"),
        // 工具行按钮
        new("BTN_EXPORT", "Export texts", "导出翻译", "導出翻譯"),
        new("BTN_UNTRANSLATED", "Untranslated", "补漏翻", "補漏翻"),
        new("BTN_SHOW_ORIGINAL", "Show original", "显示原文", "顯示原文"),
        new("BTN_SHOW_TRANSLATED", "Show translated", "显示译文", "顯示譯文"),
        new("BTN_RELOAD", "Reload texts", "重载翻译", "重載翻譯"),
        new("BTN_OPEN_FOLDER", "Open folder", "打开目录", "打開目錄"),
        new("BTN_REFRESH", "Refresh", "刷新", "重新整理"),
        new("BTN_THEME", "Theme", "主题", "主題"),
        new("SORT_DEFAULT", "Load order", "加载顺序", "載入順序"),
        new("SORT_LOAD_DESC", "Load order, reversed", "加载倒序", "載入倒序"),
        new("SORT_NAME", "Name A→Z", "名称 A→Z", "名稱 A→Z"),
        new("SORT_NAME_DESC", "Name Z→A", "名称 Z→A", "名稱 Z→A"),
        new("LANG_FOLLOW", "Follow game language", "跟随游戏语言", "跟隨遊戲語言"),
        // 说明面板
        new("META_DEFAULT", "Default", "默认", "預設"),
        new("META_RANGE", "Range", "范围", "範圍"),
        new("META_OPTIONS", "Options", "可选", "可選"),
        new("META_ON", "ON", "开", "開"),
        new("META_OFF", "OFF", "关", "關"),
        new("META_EMPTY", "(empty)", "（空）", "（空）"),
        // 提示（说明面板里显示；{0} {1} {2} 是占位符，译文里保留）
        new("NOTICE_SHOWING_TRANSLATED", "Showing translations.", "已切回译文。", "已切回譯文。"),
        new("NOTICE_SHOWING_ORIGINAL_V2", "Showing original texts of mods (click again to restore).", "正在显示模组原文（再点一次切回译文）。", "正在顯示模組原文（再點一次切回譯文）。"),
        new("NOTICE_RELOADED_V2", "Re-read the language files from disk.", "已从磁盘重新读取语言文件。", "已從磁碟重新讀取語言檔案。"),
        new("NOTICE_EXPORT_FAILED", "Export failed, see the log.", "导出失败，详见日志。", "導出失敗，詳見日誌。"),
        new("NOTICE_UNTRANSLATED", "{0} untranslated ({1} translated, {2} already CJK)", "未翻译 {0} 条（已翻 {1} 条，已是中文 {2} 条）", "未翻譯 {0} 條（已翻 {1} 條，已是中文 {2} 條）"),
        new("NOTICE_EXPORTED", "Exported {0} entries", "已导出 {0} 条", "已導出 {0} 條"),
        new("NOTICE_SELECTED_MODS", ", {0} selected mods", "，{0} 个已选模组", "，{0} 個已選模組"),
        new("NOTICE_OPEN_FOLDER_FAILED", "Could not open the folder: {0}", "打开目录失败：{0}", "打開目錄失敗：{0}"),
        new("NOTICE_LANG_FOLLOW", "Following the game language (built-in en-us / zh-cn / zh-tw).", "已改为跟随游戏语言（内建 en-us / zh-cn / zh-tw）。", "已改為跟隨遊戲語言（內建 en-us / zh-cn / zh-tw）。"),
        new("NOTICE_LANG_ERROR", "Language file error in {0}: {1}", "语言文件 {0} 有错：{1}", "語言檔案 {0} 有錯：{1}"),
        new("NOTICE_LANG_NO_ENTRIES", "no valid \"key=text\" line", "没有一行有效的「键=译文」", "沒有一行有效的「鍵=譯文」"),
        new("NOTICE_LANG_BAD_LINES", "lines without \"=\": {0}", "缺少等号的行：{0}", "缺少等號的行：{0}"),
        new("NOTICE_LANG_NOT_FOUND", "file not found (click Refresh)", "文件不存在（点刷新）", "檔案不存在（點重新整理）"),
        new("NOTICE_REFRESHED", "Language folder rescanned: {0} language group(s).", "已重新扫描语言目录：{0} 个语言组。", "已重新掃描語言目錄：{0} 個語言組。"),
        // 悬停注释（说明面板显示；首行是标题，`…` 包住的部分用标题色强调）
        new("TIP_EXPORT",
            "Export texts\nExports the untranslated source dictionary of every mod, including MCE (`ModConfigEnhance`) itself;\nmods unticked while `Selected only` is on are excluded.\n\nDefault output: `*\\PEAK\\BepInEx\\config\\ModConfigEnhance\\export\\*`\nUsage: `*\\PEAK\\BepInEx\\config\\ModConfigEnhance\\readme.md`",
            "导出翻译\n导出包括 MCE（`ModConfigEnhance`）模组本身在内的所有模组的待翻译原文词典;\n但不包括`仅导出已选`启用后取消选中的模组。\n\n默认导出路径：`*\\PEAK\\BepInEx\\config\\ModConfigEnhance\\export\\*`\n如何使用请查阅`*\\PEAK\\BepInEx\\config\\ModConfigEnhance\\readme.md`",
            "導出翻譯\n導出包括 MCE（`ModConfigEnhance`）模組本身在內的所有模組的待翻譯原文詞典;\n但不包括`僅導出已選`啟用後取消選中的模組。\n\n默認導出路徑：`*\\PEAK\\BepInEx\\config\\ModConfigEnhance\\export\\*`\n如何使用請查閱`*\\PEAK\\BepInEx\\config\\ModConfigEnhance\\readme.md`"),
        new("TIP_UNTRANSLATED_V2",
            "Untranslated\nExports only the entries the current language group doesn't cover yet: exact lines and `r:` regex rules both count as translated.\n\nOutput lands in `export\\`; fill it in and put it into `language\\` as described in `readme.md`.",
            "补漏翻\n只导出当前语言组还没覆盖的原文条目：整句译文和 `r:` 正则规则命中的都算已翻。\n\n产物落在 `export\\` 目录，填好后按 `readme.md` 的说明放进 `language\\`。",
            "補漏翻\n只導出目前語言組還沒覆蓋的原文條目：整句譯文和 `r:` 正則規則命中的都算已翻。\n\n產物落在 `export\\` 目錄，填好後按 `readme.md` 的說明放進 `language\\`。"),
        new("TIP_SELECTED_ONLY",
            "Selected only\nWhen ticked, every mod row in the sidebar gets an `Export` checkbox — `Export texts`/`Untranslated` cover ticked mods only.\n\nThis mod is always included (the exported file doubles as its language file).",
            "仅导出已选\n勾选后侧栏每个模组行出现`导出`复选框，`导出翻译`/`补漏翻`只包含勾选的模组。\n\n本模组始终包含（导出文件同时是本模组的语言文件）。",
            "僅導出已選\n勾選後側欄每個模組行出現`導出`複選框，`導出翻譯`/`補漏翻`只包含勾選的模組。\n\n本模組始終包含（導出檔案同時是本模組的語言檔案）。"),
        new("TIP_TOGGLE_ORIG_V2",
            "Show original / Show translated\nSwitches every mod text this mod displays (sidebar, description panel, settings page) between translation and original — check the source before exporting, or compare.\n\nThis mod's own UI text keeps the selected language.",
            "显示原文 / 显示译文\n把本模组负责显示的模组文字（侧栏、说明面板、设置页）在译文和原文之间切换：导出前对照原文，或检查译文效果。\n\n本模组自己的界面文字保持所选语言。",
            "顯示原文 / 顯示譯文\n把本模組負責顯示的模組文字（側欄、說明面板、設定頁）在譯文和原文之間切換：匯出前對照原文，或檢查譯文效果。\n\n本模組自己的介面文字保持所選語言。"),
        new("TIP_RELOAD_V2",
            "Reload texts\nRe-reads the files in `language\\` — after editing a language file this applies it without re-picking the language.",
            "重载翻译\n重新读取 `language\\` 的语言文件：改完语言文件点它即生效，无需重选语言。",
            "重載翻譯\n重新讀取 `language\\` 的語言檔案：改完語言檔案點它即生效，無需重選語言。"),
        new("TIP_OPEN_DIR",
            "Open folder\nOpens `BepInEx\\config\\ModConfigEnhance\\`\n\n`language\\` (language files), `export\\` (exports) and `readme.md` (docs) all live there.",
            "打开目录\n打开 `BepInEx\\config\\ModConfigEnhance\\`\n\n`language\\`（语言文件）、`export\\`（导出产物）、`readme.md`（使用说明）都在里面。",
            "打開目錄\n打開 `BepInEx\\config\\ModConfigEnhance\\`\n\n`language\\`（語言檔案）、`export\\`（導出產物）、`readme.md`（使用說明）都在裡面。"),
        new("TIP_SORT",
            "Sort\nSidebar mod order:\n\n`Default` (load order) / `Load reversed` / `Name` / `Name reversed`.",
            "排序\n侧栏模组行的排序方式：\n\n`默认`（加载顺序）/ `加载倒序` / `名称` / `名称倒序`。",
            "排序\n側欄模組行的排序方式：\n\n`默認`（載入順序）/ `載入倒序` / `名稱` / `名稱倒序`。"),
        new("TIP_LANGUAGE",
            "Language\nPick the UI language: \"Follow game language\" or a group under `language\\`; applies instantly, no restart.\n\nNaming rules `code[-variant][_part].txt` are in `readme.md`.",
            "语言\n选择界面语言：「跟随游戏语言」或 `language\\` 下的语言组，选中即应用、无需重启。\n\n命名规则「`语言码[-变体][_分包].txt`」见 `readme.md`。",
            "語言\n選擇介面語言：「跟隨遊戲語言」或 `language\\` 下的語言組，選中即套用、無需重啟。\n\n命名規則「`語言碼[-變體][_分包].txt`」見 `readme.md`。"),
        new("TIP_REFRESH",
            "Refresh\nRescans the `language\\` directory\n\n— new language files appear in the dropdown after clicking this.",
            "刷新\n重新扫描 `language\\` 目录\n\n新增语言文件后点它才会出现在下拉里。",
            "重新整理\n重新掃描 `language\\` 目錄\n\n新增語言檔案後點它才會出現在下拉里。"),
        new("TIP_THEME",
            "Theme\nSwitches the colour theme of this page between blue and purple.",
            "主题\n在蓝色与紫色主题之间切换本页面的配色。",
            "主題\n在藍色與紫色主題之間切換本頁面的配色。"),
        new("TIP_JUMP_HOTKEYS",
            "Go to keybindings\nOpens the `Keybindings` tab of `Mod overview`, which lists every mod's keybinds in one place.",
            "跳转到快捷键设置页\n打开`模组概览`的`快捷键设置`页签，集中查看与修改所有模组的快捷键。",
            "跳轉到快捷鍵設定頁\n開啟`模組概覽`的`快捷鍵設定`頁籤，集中檢視與修改所有模組的快捷鍵。"),
        // 设置页侧栏分组（与模组概览共用 overview.json）
        new("BTN_GROUPS", "Groups", "分组", "分組"),
        new("GROUP_UNGROUPED", "Ungrouped", "未分组", "未分組"),
        new("NOTICE_GROUPS_UNAVAILABLE", "Could not open the group editor, see the log.", "无法打开分组编辑，详见日志。", "無法開啟分組編輯，詳見日誌。"),
        new("TIP_GROUPS",
            "Groups\nCreate or edit groups: name, description, color and members. Same data as the `Mod overview` groups.\n\nPinned mods stay in the pinned area at the top; their group shows as a colored bar next to the pin bar.",
            "分组管理\n新建或编辑分组：名称、描述、颜色和成员，与`模组概览`的分组是同一份数据。\n\n置顶的模组留在最上方的置顶区，所属分组显示为置顶竖线右侧的分组色条。",
            "分組管理\n新建或編輯分組：名稱、描述、顏色和成員，與`模組概覽`的分組是同一份資料。\n\n置頂的模組留在最上方的置頂區，所屬分組顯示為置頂豎線右側的分組色條。"),
        new("TIP_GROUP_HEADER",
            "Click to collapse or expand. The collapsed state is shared with the `Mod overview`; searching temporarily expands every group.",
            "点击折叠或展开，折叠状态与`模组概览`同步；搜索时所有分组临时展开。",
            "點擊折疊或展開，折疊狀態與`模組概覽`同步；搜尋時所有分組臨時展開。"),
    };

    /// <summary>本模组 cfg 选项名 / 说明的内置译文：键是 cfg 里的英文原文，设置页与说明面板按它显示。</summary>
    internal static readonly Term[] ConfigTerms =
    {
        new("Tree sidebar", "Tree sidebar", "树形侧栏", "樹形側欄"),
        new("Description panel", "Description panel", "选项说明面板", "選項說明面板"),
        new("Colour theme", "Colour theme", "配色主题", "配色主題"),
        new("Blue", "Blue", "蓝色", "藍色"),
        new("Purple", "Purple", "紫色", "紫色"),
        new("PurpleAtNight", "PurpleAtNight", "夜里自动紫色", "夜裡自動紫色"),
        new("UI localization", "UI localization", "界面本地化", "介面本地化"),
        new("Hide localization buttons", "Hide localization buttons", "隐藏本地化相关按钮", "隱藏本地化相關按鈕"),
        new("1. Layout", "1. Layout", "1. 版面", "1. 版面"),
        new("2. Localization", "2. Localization", "2. 本地化", "2. 本地化"),
        new(Plugin.DescTreeSidebar, Plugin.DescTreeSidebar,
            "模组列表改为左侧竖排侧栏，选中的模组在其下方展开分区，右侧显示选项。重启生效。",
            "模組列表改為左側直排側欄，選中的模組在其下方展開分區，右側顯示選項。重啟生效。"),
        new(Plugin.DescColourTheme, Plugin.DescColourTheme,
            "模组设置页的配色主题。蓝色跟随游戏的设置菜单；紫色是夜间风格；夜里自动紫色在装了 TimeTheme 时夜里切到紫色（只读它的昼夜状态）。即时生效。",
            "模組設定頁的配色主題。藍色跟隨遊戲的設定選單；紫色是夜間風格；夜裡自動紫色在裝了 TimeTheme 時夜裡切到紫色（只讀它的晝夜狀態）。即時生效。"),
        new(Plugin.DescDescriptionPanel, Plugin.DescDescriptionPanel,
            "右上方面板显示悬停选项的说明、默认值与取值范围 / 可选项。需要树形侧栏。重启生效。",
            "右上方板面顯示懸停選項的說明、預設值與取值範圍 / 可選項。需要樹形側欄。重啟生效。"),
        new(Plugin.DescUiLocalization, Plugin.DescUiLocalization,
            "将 ModConfig 自带界面（搜索、默认、清空、筛选项……）与本模组按钮 / 提示本地化。文字取自 config\\ModConfigEnhance\\language\\ 的语言文件，在模组列表上方的语言行选择。重启生效。",
            "將 ModConfig 自帶介面（搜尋、預設、清除、篩選項……）與本模組按鈕 / 提示本地化。文字取自 config\\ModConfigEnhance\\language\\ 的語言檔案，在模組列表上方的語言行選擇。重啟生效。"),
        new(Plugin.DescHideLocalizationButtons, Plugin.DescHideLocalizationButtons,
            "收起模组列表上方的导出与翻译工具两排按钮，只留语言下拉 + 刷新；列表上移，「导出」列同步隐藏，勾选状态保留。即时生效。",
            "收起模組列表上方的導出與翻譯工具兩排按鈕，只留語言下拉 + 重新整理；列表上移，「導出」欄同步隱藏，勾選狀態保留。即時生效。"),
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
        internal string Key = "";
        internal readonly List<string> Paths = new List<string>();
        /// <summary>键 → 译文；MCE_ 键已去前缀并大写，其余键原样。</summary>
        internal readonly Dictionary<string, string> Entries = new Dictionary<string, string>(StringComparer.Ordinal);
        internal readonly List<int> BadLines = new List<int>();
        /// <summary><c>r:</c> 正则规则，按文件内与组内先后排列；匹配时后写的优先。</summary>
        internal readonly List<RegexRule> Regexes = new List<RegexRule>();
        /// <summary>写坏的正则（「文件名 第 n 行：原因」），跳过不用，在应用结果里提示。</summary>
        internal readonly List<string> RegexErrors = new List<string>();

        internal string Name => System.IO.Path.GetFileName(Path);
        internal int FileCount => Paths.Count > 0 ? Paths.Count : 1;
        /// <summary>有 // display: 用显示名，没有回落到组名（文件全名）。</summary>
        internal string DisplayOrStem => Display.Length > 0 ? Display : Stem;
    }

    /// <summary>
    /// 目录下的语言组（只看当前目录，不含 _ 开头的模板）。组名 = 第一个 _ 之前的部分：
    /// `语言码-变体` 是同码的独立分组（各自一套完整翻译），`组名_分包` 并入前缀所指的组。
    /// 同组内基础文件优先，其余按文件名排序。
    /// </summary>
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
            string groupStem = GroupStemFrom(stem);
            string key = NormalizeGroupKey(groupStem);
            if (!groups.TryGetValue(key, out LanguageFile? item))
            {
                item = new LanguageFile { Key = key, Code = LanguageCodeFromGroup(groupStem), Stem = groupStem };
                groups.Add(key, item);
            }

            item.Paths.Add(file);
        }

        List<LanguageFile> result = new List<LanguageFile>(groups.Values);
        foreach (LanguageFile item in result)
        {
            item.Paths.Sort(CompareGroupFiles);
            item.Path = item.Paths[0];
            item.Stem = Path.GetFileNameWithoutExtension(item.Path); // 显示回落用基础文件的真实文件名
            item.Display = ReadDisplayName(item.Path) ?? "";
        }

        result.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Key, b.Key));
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

    /// <summary>文件名（不含扩展名）→ 组名：第一个 _ 之前的部分。「语言码-变体」的变体留在组名里。</summary>
    internal static string GroupStemFrom(string stem)
    {
        int separator = stem.IndexOf('_');
        return separator < 0 ? stem : stem.Substring(0, separator);
    }

    /// <summary>组名 → 语言码：开头的 xx 或 xx-yy（zh-cn-old → zh-cn、zh-tw-old → zh-tw、en → en-us）。</summary>
    internal static string LanguageCodeFromGroup(string groupStem)
    {
        return LanguageCodeFromGroup(groupStem, out _);
    }

    /// <summary>同上，额外给出码段在组名里的原始长度（剩余部分是 -变体 后缀）。</summary>
    private static string LanguageCodeFromGroup(string groupStem, out int codeLength)
    {
        string stem = groupStem.Trim();
        int firstDash = stem.IndexOf('-');
        if (firstDash > 0)
        {
            int secondDash = stem.IndexOf('-', firstDash + 1);
            string region = secondDash < 0
                ? stem.Substring(firstDash + 1)
                : stem.Substring(firstDash + 1, secondDash - firstDash - 1);
            if (region.Length == 2)
            {
                codeLength = secondDash < 0 ? stem.Length : secondDash;
                return NormalizeLanguageCode(stem.Substring(0, codeLength));
            }
        }

        codeLength = firstDash < 0 ? stem.Length : firstDash;
        return NormalizeLanguageCode(stem.Substring(0, codeLength));
    }

    /// <summary>组名 → 归一化组标识：语言码段过别名（zh→zh-cn、en→en-us），-变体 后缀小写保留（zh-old → zh-cn-old）。</summary>
    internal static string NormalizeGroupKey(string groupStem)
    {
        string code = LanguageCodeFromGroup(groupStem, out int codeLength);
        return code + groupStem.Substring(codeLength).Trim().ToLowerInvariant();
    }

    /// <summary>文件名（不含扩展名）→ 该文件所属组的语言码（含组名里的变体归属）。</summary>
    internal static string LanguageCodeFromStem(string stem)
    {
        return LanguageCodeFromGroup(GroupStemFrom(stem));
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

    /// <summary>cfg 记忆值 → 归一化组标识（旧值是语言码时同样命中对应组）。</summary>
    internal static string NormalizeSelection(string selection)
    {
        return string.IsNullOrWhiteSpace(selection) ? FollowGame : NormalizeGroupKey(GroupStemFrom(selection));
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

            if (IsRegexKey(key))
            {
                // r: 行按 XUnity 词典的转义重新读（两个反斜杠 → 一个等），从 XUnity 词典搬来的规则意思不变
                string rawKey = UnescapeXUnity(line.Substring(0, eq)).Trim(), rawValue = UnescapeXUnity(line.Substring(eq + 1));
                RegexRule? rule = TryCompileRegex(rawKey, rawValue, Path.GetFileName(file), lineNo, out string regexError);
                if (rule != null) result.Regexes.Add(rule);
                else result.RegexErrors.Add(F("{0} line {1}: {2}", "{0} 第 {1} 行：{2}", "{0} 第 {1} 行：{2}", Path.GetFileName(file), lineNo, regexError));
                continue;
            }

            if (TryStripTermPrefix(key, out string termId))
            {
                key = termId;
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
        string key = NormalizeSelection(selection);
        LanguageFile? group = null;
        foreach (LanguageFile candidate in Scan())
        {
            if (string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                group = candidate;
                break;
            }
        }

        if (group == null)
        {
            throw new FileNotFoundException(Get("NOTICE_LANG_NOT_FOUND"), key + ".txt");
        }

        LanguageFile merged = new LanguageFile
        {
            Code = group.Code,
            Stem = group.Stem,
            Key = group.Key,
            Path = group.Path,
            Display = group.Display,
        };
        merged.Paths.AddRange(group.Paths);
        Dictionary<string, string> sources = new Dictionary<string, string>(StringComparer.Ordinal);
        // 内置三语先铺内置译文，文件里的有效译文再覆盖：旧版写出的语言文件缺新词条时不再回落英文。
        string builtInCode = NormalizeLanguageCode(merged.Code);
        if (builtInCode == "en-us" || builtInCode == "zh-cn" || builtInCode == "zh-tw")
        {
            foreach (Term term in Terms)
            {
                merged.Entries[term.Key] = PickTerm(term, builtInCode);
            }
        }

        foreach (string path in group.Paths)
        {
            LanguageFile file = Parse(path);
            merged.Regexes.AddRange(file.Regexes);
            merged.RegexErrors.AddRange(file.RegexErrors);
            if (file.BadLines.Count > 0)
            {
                throw new InvalidDataException(Format("NOTICE_LANG_BAD_LINES", Path.GetFileName(path) + ": " + JoinLines(file.BadLines)));
            }

            foreach (KeyValuePair<string, string> pair in file.Entries)
            {
                if (RetiredTerm(pair.Key))
                {
                    continue; // 已删除 / 改了含义的旧词条：旧译文说的是 XUnity 联动，以内置为准
                }

                bool incomingEffective = IsEffectiveTranslation(pair.Key, pair.Value, merged.Code);
                if (merged.Entries.TryGetValue(pair.Key, out string? oldValue))
                {
                    bool oldEffective = IsEffectiveTranslation(pair.Key, oldValue, merged.Code);
                    if ((!incomingEffective || IsMceEnglishPlaceholder(pair.Key, pair.Value)) && oldEffective &&
                        !string.Equals(oldValue, pair.Value, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (incomingEffective && oldEffective && sources.ContainsKey(pair.Key) && !string.Equals(oldValue, pair.Value, StringComparison.Ordinal))
                    {
                        Plugin.Log.LogWarning($"[语言] {group.Key} 键冲突「{pair.Key}」：{sources[pair.Key]} → {Path.GetFileName(path)}，采用后者。");
                    }
                }

                merged.Entries[pair.Key] = pair.Value;
                sources[pair.Key] = Path.GetFileName(path);
            }
        }

        if (merged.Entries.Count == 0)
        {
            // 1.0.3：内建三种语言（en-us / zh-cn / zh-tw）的文件被清空或只剩注释时，用内置译文补上，不报错——
            // 否则 cfg 里选着这种语言时每次启动都应用失败，界面停在原状态。自建语言文件为空仍报错，让作者知道文件坏了。
            Func<Term, string>? builtIn = merged.Code.ToLowerInvariant() switch
            {
                "en-us" or "en" => t => t.English,
                "zh-cn" or "zh" => t => t.SimplifiedChinese,
                "zh-tw" => t => t.TraditionalChinese,
                _ => null,
            };
            if (builtIn == null)
            {
                throw new InvalidDataException(Get("NOTICE_LANG_NO_ENTRIES"));
            }

            foreach (Term term in Terms)
            {
                merged.Entries[term.Key] = builtIn(term);
            }

            Plugin.Log.LogWarning($"[语言] {merged.DisplayOrStem} 没有有效词条，已改用内置译文（删除该文件后重启会重新生成完整的内建文件）。");
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

    /// <summary>当前生效的语言组：选了组按组标识，跟随游戏按语言码（同码多组时纯组优先）。</summary>
    private static LanguageFile? CurrentGroup()
    {
        string selection = Plugin.LanguageFile != null ? Plugin.LanguageFile.Value ?? "" : "";
        return string.IsNullOrEmpty(selection) ? ParseOptionalGroupByCode(FollowStem()) : ParseOptionalGroup(selection);
    }

    /// <summary>当前语言组里已翻的 MCE_ 词条（补漏翻用：同码变体组各算各的，不互相计入）。</summary>
    internal static HashSet<string> CurrentTranslatedTermKeys()
    {
        HashSet<string> result = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            LanguageFile? group = CurrentGroup();
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

    /// <summary>当前语言组里非 MCE_ 的已翻原文（补漏翻用：只算本组自己的译文，别组同码不算）。</summary>
    internal static HashSet<string> CurrentTranslatedTexts()
    {
        HashSet<string> result = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            LanguageFile? group = CurrentGroup();
            if (group != null)
            {
                foreach (KeyValuePair<string, string> pair in group.Entries)
                {
                    if (!IsTermKey(pair.Key) && IsEffectiveTranslation(pair.Key, pair.Value, group.Code))
                    {
                        result.Add(pair.Key);
                        result.Add(pair.Key.Trim());
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[语言] 查漏翻读取当前语言组失败，非 MCE 文本按词典判定: {ex.Message}");
        }

        return result;
    }

    // FindUnescapedEquals / Escape / Unescape 在 Loc.Escaping.cs

    /// <summary>说明面板要显示的结果：正常一行，出错用警告色。Parts 非空时走结构化富文本（分段上色）。</summary>
    internal sealed class ApplyResult
    {
        internal string Message = "";
        internal bool Warning;
        internal bool Success;
        internal List<(string Text, ModConfigDescriptionPanel.NoticeRole Role)>? Parts;
    }

    /// <summary>
    /// 按 <paramref name="selection"/>（空 = 跟随游戏语言，否则是组名「语言码[-变体]」）把词条写进语言表，
    /// 需要时刷新全部 LocalizedText 组件；本模组负责显示的模组文字（侧栏、说明面板、设置页）随版本号重画。
    /// </summary>
    internal static ApplyResult Apply(string selection, bool notify, bool refresh)
    {
        _registered = true;
        ApplyResult result = new ApplyResult();
        List<(string Text, ModConfigDescriptionPanel.NoticeRole Role)> parts =
            new List<(string Text, ModConfigDescriptionPanel.NoticeRole Role)>();
        bool appliedSuccessfully = false;
        try
        {
            LanguageFile? preparedGroup;
            parts.Add((T("Applied language file:", "已应用语言文件：", "已套用語言檔案：") + "\n", ModConfigDescriptionPanel.NoticeRole.Header));
            if (string.IsNullOrEmpty(selection))
            {
                preparedGroup = ApplyFollowGame();
                parts.Add((Get("NOTICE_LANG_FOLLOW"), ModConfigDescriptionPanel.NoticeRole.Body));
            }
            else
            {
                LanguageFile file = ParseGroup(selection);
                preparedGroup = file;
                int applied = ApplyFile(file);
                int missing = Terms.Length - applied;
                parts.Add((file.DisplayOrStem + " ×" + file.FileCount
                    + T("  |  MCE dictionary: ", "  |  已加载 MCE 词典 ", "  |  已載入 MCE 詞典 ") + applied
                    + T(" entries", " 条", " 條"), ModConfigDescriptionPanel.NoticeRole.Body));
                if (missing > 0)
                {
                    result.Warning = true;
                    parts.Add((T($" ({missing} untranslated → English; see en-us.txt)",
                        $"（漏翻 {missing} 条，回落英文——参考 en-us.txt）",
                        $"（漏翻 {missing} 條，回落英文——參考 en-us.txt）"), ModConfigDescriptionPanel.NoticeRole.Warning));
                }
            }

            WriteReadme();
            if (preparedGroup != null && (preparedGroup.Regexes.Count > 0 || preparedGroup.RegexErrors.Count > 0))
            {
                parts.Add((F("  |  Regex rules: {0}", "  |  正则规则 {0} 条", "  |  正則規則 {0} 條", preparedGroup.Regexes.Count), ModConfigDescriptionPanel.NoticeRole.Body));
                for (int i = 0; i < preparedGroup.RegexErrors.Count && i < 5; i++)
                {
                    result.Warning = true;
                    parts.Add(("\n" + F("Regex rule skipped: {0}", "正则规则已跳过：{0}", "正則規則已略過：{0}", preparedGroup.RegexErrors[i]), ModConfigDescriptionPanel.NoticeRole.Warning));
                }
            }

            if (preparedGroup != null && preparedGroup.Paths.Count > 0)
            {
                parts.Add(("\n\n" + T("Files:", "文件列表：", "檔案列表："), ModConfigDescriptionPanel.NoticeRole.Header));
                foreach (string path in preparedGroup.Paths)
                {
                    string? display = ReadDisplayName(path);
                    string modified = File.GetLastWriteTime(path).ToString("yyyy-MM-dd");
                    parts.Add(("\n- " + Path.GetFileName(path)
                        + (display != null ? "（" + display + "）" : "")
                        + T($" (modified {modified})", $"（修改日期：{modified}）", $"（修改日期：{modified}）"), ModConfigDescriptionPanel.NoticeRole.Body));
                }
            }

            result.Parts = parts;
            StringBuilder plain = new StringBuilder();
            foreach ((string text, _) in parts)
            {
                plain.Append(text);
            }

            result.Message = plain.ToString();
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
            if (appliedSuccessfully)
            {
                InvalidateEditingTranslations(); // 侧栏、说明面板、设置页的模组文字按新语言组重画
            }

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
            if (result.Parts != null && ModConfigDescriptionPanel.Current != null)
            {
                ModConfigDescriptionPanel.Current.ShowNotice(result.Parts);
            }
            else
            {
                ModConfigDescriptionPanel.Current?.ShowNotice(result.Message, result.Warning);
            }
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
        LanguageFile? current = ParseOptionalGroupByCode(code);
        RegisterBuiltIn();
        OverlayBuiltInGroup(current, LocalizedText.CURRENT_LANGUAGE);
        return current;
    }

    /// <summary>按组标识找组（zh-cn-old 只命中它自己）。</summary>
    private static LanguageFile? ParseOptionalGroup(string key)
    {
        foreach (LanguageFile group in Scan())
        {
            if (string.Equals(group.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return ParseGroup(key);
            }
        }

        return null;
    }

    /// <summary>按语言码找组：同码多组时优先组名恰等于码的纯组，没有再取第一个变体组。</summary>
    private static LanguageFile? ParseOptionalGroupByCode(string code)
    {
        LanguageFile? variant = null;
        foreach (LanguageFile group in Scan())
        {
            if (!string.Equals(group.Code, code, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(group.Key, code, StringComparison.OrdinalIgnoreCase))
            {
                return ParseGroup(group.Key);
            }

            variant ??= group;
        }

        return variant != null ? ParseGroup(variant.Key) : null;
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

    // ── 内建文件 ──────────────────────────────────────────────────

    /// <summary>首启写 en-us / zh-cn / zh-tw（只建不覆），说明文件每次重写。</summary>
    internal static void EnsureBuiltInFiles()
    {
        try
        {
            Directory.CreateDirectory(LanguageDirectory);
            Directory.CreateDirectory(ExportDirectory);
            LegacyXUnityMirrors.MoveAway();
            WriteIfMissing("en-us", "English", t => t.English, t => t.English);
            WriteIfMissing("zh-cn", "简体中文", t => t.SimplifiedChinese, t => t.SimplifiedChinese);
            WriteIfMissing("zh-tw", "繁體中文", t => t.TraditionalChinese, t => t.TraditionalChinese);
            WriteReadme();
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

    private static string BuildReadme() => LanguageReadme.For(UiLanguage).Replace("{VERSION}", Plugin.DisplayVersion);

    private static string BuildFile(string display, Func<Term, string> pick, Func<Term, string> pickCfg)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("// display: ").Append(display).Append('\n');
        sb.Append("// ModConfig Enhance ").Append(Plugin.DisplayVersion).Append(" - built-in language file. Edit freely; it is never overwritten. Reset by deleting it.\n");
        sb.Append("// See ..\\").Append(ReadmeFile).Append(" for the format and how to make a new language.\n");

        sb.Append('\n');
        sb.Append("// ---- UI terms (key=text; keys are stable IDs, do not translate them) ----\n");
        foreach (Term term in Terms)
        {
            sb.Append(KeyPrefix).Append(term.Key).Append('=').Append(Escape(pick(term))).Append('\n');
        }

        sb.Append('\n');
        sb.Append("// ---- Settings of this mod (English original=translation) ----\n");
        foreach (Term term in ConfigTerms)
        {
            sb.Append(Escape(term.Key)).Append('=').Append(Escape(pickCfg(term))).Append('\n');
        }

        return sb.ToString();
    }
}
