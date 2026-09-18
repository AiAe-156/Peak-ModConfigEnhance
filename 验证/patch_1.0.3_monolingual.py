# -*- coding: utf-8 -*-
import io, os
root = r"F:\Games\SteamLibrary\steamapps\common\PEAK\MODs\ModConfigEnhance\src"

def rw(name, fn):
    p = os.path.join(root, name)
    s = io.open(p, encoding="utf-8").read()
    s = fn(s)
    io.open(p, "w", encoding="utf-8", newline="\n").write(s)

def rep(s, pairs):
    for a, b in pairs:
        assert a in s, ("MISSING: " + a[:80])
        s = s.replace(a, b, 1)
    return s

# ---------- Loc.cs ----------
UILANG = r'''    // ── 当前界面语言（三选一，说明文字按它挑）────────────────────

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

    /// <summary>language\_readme.txt 按当前界面语言重写（启动时与每次切语言后）。</summary>
    internal static void WriteReadme()
    {
        try
        {
            Directory.CreateDirectory(LanguageDirectory);
            File.WriteAllText(Path.Combine(LanguageDirectory, ReadmeFile), BuildReadme(), new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[语言] 写 {ReadmeFile} 失败: {ex.Message}");
        }
    }

    internal static string FollowStem()
'''

def loc(s):
    s = rep(s, [
        ('    internal static string FollowStem()\n', UILANG),
        ('    private static string BuildReadme() => LanguageReadme.Text.Replace("{VERSION}", Plugin.PluginVersion);',
         '    private static string BuildReadme() => LanguageReadme.For(UiLanguage).Replace("{VERSION}", Plugin.PluginVersion);'),
        # 切语言后重写说明文件（在 Apply 的 finally 之前、镜像之后）
        ('            result.Mirror = SyncXUnity(selection);', '            result.Mirror = SyncXUnity(selection);\n            WriteReadme();'),
    ])
    return s

rw("Loc.cs", loc)

# ---------- ModConfigTranslationExport.cs：文件头单语 ----------
HEADER = r'''        string lang = XUnityBridge.TargetLanguage ?? "zh";
        string L(string en, string zhCn, string zhTw) => Loc.T(en, zhCn, zhTw);
        sb.Append("// ModConfig Enhance ").Append(Plugin.PluginVersion).Append(' ')
            .Append(untranslatedOnly ? L("- untranslated texts ", "- 未翻译文本 ", "- 未翻譯文本 ") : L("- texts to translate ", "- 待翻译文本 ", "- 待翻譯文本 "))
            .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append('\n');
        sb.Append(L("// -- What is this --", "// ── 这是什么 ──", "// ── 這是什麼 ──")).Append('\n');
        sb.Append(L(
            "// First block: this mod's own UI (MCE_ keys - stable IDs, translate only the right side). Then every text the ModConfig settings page shows\n// (mod names, sections, option names, descriptions, dropdown choices), grouped by mod, in XUnity AutoTranslator dictionary format: original=translation.",
            "// 第一块是本模组自己的界面（MCE_ 键：稳定 ID，只翻等号右边）；之后是 ModConfig「模组设置」页会显示的全部文字\n// （模组名、分区、选项名、说明、下拉选项），按模组分块，格式与 XUnity 词典相同：原文=译文。",
            "// 第一塊是本模組自己的介面（MCE_ 鍵：穩定 ID，只翻等號右邊）；之後是 ModConfig「模組設定」頁會顯示的全部文字\n// （模組名、分區、選項名、說明、下拉選項），按模組分塊，格式與 XUnity 詞典相同：原文=譯文。")).Append('\n');
        sb.Append(L("// -- How to use --", "// ── 怎么用 ──", "// ── 怎麼用 ──")).Append('\n');
        sb.Append(L(
            "// 1. Tick \"Selected only\" in the game and untick mods you don't need, then export again - the file gets much shorter (this mod is always included).",
            "// 1. 先粗筛：勾「仅导出已选」，把不需要的模组去勾，再导出一次，文件会短很多（本模组永远包含）。",
            "// 1. 先粗篩：勾「僅導出已選」，把不需要的模組去勾，再導出一次，檔案會短很多（本模組永遠包含）。")).Append('\n');
        sb.Append(L(
            "// 2. Fill the translation after \"=\" (an AI can do the whole file). Never change anything left of \"=\". Empty right sides are ignored.",
            "// 2. 把译文填在等号右边（可整份交给 AI 翻），不要改动等号左边的任何字符。右边留空的行会被忽略。",
            "// 2. 把譯文填在等號右邊（可整份交給 AI 翻），不要改動等號左邊的任何字元。右邊留空的行會被忽略。")).Append('\n');
        sb.Append(L(
            "// 3. Save as UTF-8, rename it to your language code (" + lang + ".txt, de-de.txt, ja-jp.txt ...) and put it in BepInEx\\config\\ModConfigEnhance\\language\\.",
            "// 3. 存成 UTF-8，改名为语言代码（" + lang + ".txt / de-de.txt / ja-jp.txt……），放进 BepInEx\\config\\ModConfigEnhance\\language\\。",
            "// 3. 存成 UTF-8，改名為語言代碼（" + lang + ".txt / de-de.txt / ja-jp.txt……），放進 BepInEx\\config\\ModConfigEnhance\\language\\。")).Append('\n');
        sb.Append(L(
            "// 4. In the game: left column, third row -> Refresh -> pick the file. This mod's UI and XUnity AutoTranslator both switch to that language at once\n//    (XUnity's dictionary folder becomes Translation\\<code>\\Text\\, texts without a translation show the original) - no restart.\n//    \"Reload texts\" (Alt+R) only re-reads after hand edits; \"Untranslated\" is an optional check.",
            "// 4. 游戏里左栏第三行 → 刷新 → 选中它：本模组界面与 XUnity AutoTranslator 一起切到该语言（XUnity 的词典目录变成 Translation\\<码>\\Text\\，\n//    词典里没有的文本显示原文），不用重启。「重载翻译」（Alt+R）只在手改文件后重读用；「查漏翻」是可选的补漏工具。",
            "// 4. 遊戲裡左欄第三行 → 重新整理 → 選中它：本模組介面與 XUnity AutoTranslator 一起切到該語言（XUnity 的詞典目錄變成 Translation\\<碼>\\Text\\，\n//    詞典裡沒有的文字顯示原文），不用重啟。「重載翻譯」（Alt+R）只在手改檔案後重讀用；「查漏翻」是可選的補漏工具。")).Append('\n');
        sb.Append(L("// -- Rules for translators / AI --", "// ── 给翻译者 / AI 的规则 ──", "// ── 給翻譯者 / AI 的規則 ──")).Append('\n');
        sb.Append(L(
            "// - Lines starting with // are comments. \"// ==== Mod (GUID version) ====\" and \"// -- [Section]\" are block markers: don't translate or delete them.\n// - \\n is a line break, \\= an equals sign - keep the same notation. Copy numbers, units, key names, mod names, GUIDs and paths as-is.\n// - Single key / enum names (A, F1, auto, eu, Left) are dropdown choices: usually copy or leave empty.\n// - Each original appears once globally (the XUnity dictionary is global); repeats in later mods are not listed.\n// - Skipped automatically: KeyCode / key-name lists and choice lists longer than " + MaxChoices + " items.",
            "// · // 开头是注释；「// ==== 模组名 (GUID 版本) ====」与「// -- [分区]」只是分块标记，不用翻、不要删。\n// · \\n 是换行、\\= 是等号，译文保持同样写法；数字、单位、按键名、模组名、GUID、路径照抄。\n// · 单个键名 / 枚举值（A、F1、auto、eu、Left）是下拉框里的选项，一般照抄或留空。\n// · 同一句原文全局只出现一次（XUnity 词典是全局的），后面模组里重复的不再列出。\n// · 已自动跳过：KeyCode / 按键名清单、超过 " + MaxChoices + " 项的选项清单。已是中文或中英双语的行留空即可。",
            "// · // 開頭是註解；「// ==== 模組名 (GUID 版本) ====」與「// -- [分區]」只是分塊標記，不用翻、不要刪。\n// · \\n 是換行、\\= 是等號，譯文保持同樣寫法；數字、單位、按鍵名、模組名、GUID、路徑照抄。\n// · 單個鍵名 / 列舉值（A、F1、auto、eu、Left）是下拉框裡的選項，一般照抄或留空。\n// · 同一句原文全域只出現一次（XUnity 詞典是全域的），後面模組裡重複的不再列出。\n// · 已自動跳過：KeyCode / 按鍵名清單、超過 " + MaxChoices + " 項的選項清單。已是中文或中英雙語的行留空即可。")).Append('\n');
        if (untranslatedOnly)
        {
            sb.Append(liveQuery
                ? L("// [Untranslated] \"translated\" = XUnity's loaded dictionary says so (exact entries, r:/sr: regex and _Substitutions included), same as in-game.",
                    "// [查漏翻] 「已翻」直接问 XUnity 当前加载的词典（精确条目、r:/sr: 正则、_Substitutions 都算），与游戏里实际替换一致。",
                    "// [查漏翻] 「已翻」直接問 XUnity 目前載入的詞典（精確條目、r:/sr: 正則、_Substitutions 都算），與遊戲裡實際替換一致。")
                : L("// [Untranslated] XUnity's dictionary API was unavailable; fell back to exact matches against every .txt under Translation - regex-covered entries are reported as untranslated.",
                    "// [查漏翻] XUnity 词典接口不可用，退回按 Translation 目录下所有 .txt 的原文精确匹配；用正则覆盖的条目会误报。",
                    "// [查漏翻] XUnity 詞典介面不可用，退回按 Translation 目錄下所有 .txt 的原文精確匹配；用正則覆蓋的條目會誤報。")).Append('\n');
            sb.Append(L("//   Originals that already contain CJK characters count as \"no need\" and are not listed.",
                "//   已含中文的原文按「无需翻译」计，不列出。",
                "//   已含中文的原文按「無需翻譯」計，不列出。")).Append('\n');
        }

        if (ModConfigSidebarWidgets.ExportSelectedOnly)
        {
            sb.Append(L("// [Selected only] Only the " + mods.Count + " mods ticked in the Export column.",
                "// [仅导出已选] 只含左栏「导出」列勾选的 " + mods.Count + " 个模组。",
                "// [僅導出已選] 只含左欄「導出」列勾選的 " + mods.Count + " 個模組。")).Append('\n');
        }

        sb.Append('\n');

        // 本模组界面词条：键是稳定 ID，等号右边预填英文，翻完这段就是本模组的语言文件
        sb.Append(L("// ==== ModConfig Enhance UI (MCE_ keys: translate the right side only, never the key) ====",
            "// ==== ModConfig Enhance 界面（MCE_ 键：只翻等号右边，键不要动）====",
            "// ==== ModConfig Enhance 介面（MCE_ 鍵：只翻等號右邊，鍵不要動）====")).Append('\n');
'''

def export(s):
    i = s.index('        string lang = XUnityBridge.TargetLanguage ?? "zh";')
    j = s.index('        foreach (Loc.Term term in Loc.Terms)\n        {\n            sb.Append(Loc.KeyPrefix)')
    return s[:i] + HEADER + s[j:]

rw("ModConfigTranslationExport.cs", export)
print("ok")
