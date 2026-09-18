# -*- coding: utf-8 -*-
"""ModConfigTranslationExport.cs：Term() 换 Loc 词条、导出目录、第二排三格 + 打开目录、双语文件头。"""
import io, sys
p = sys.argv[1]
s = io.open(p, encoding='utf-8').read()
rep = [
('''        string label = untranslatedOnly
            ? Term("查漏翻", "查漏翻", "Untranslated")
            : Term("导出翻译", "導出翻譯", "Export texts");
        Color color = untranslatedOnly && !XUnityInstalled ? DisabledColor : (untranslatedOnly ? CheckColor : ExportColor);
        return MakeButton(page, name, label, color, left, top, width, height, () => Run(untranslatedOnly));''',
'''        Color color = untranslatedOnly && !XUnityInstalled ? DisabledColor : (untranslatedOnly ? CheckColor : ExportColor);
        return MakeButton(page, name, untranslatedOnly ? "BTN_UNTRANSLATED" : "BTN_EXPORT", color, left, top, width, height, () => Run(untranslatedOnly));'''),
('''    internal static void MakeXUnityButtons(RectTransform page, float left, float top, float width, float height, float gap)
    {
        float half = (width - gap) / 2f;
        bool available = XUnityInstalled;
        PeakMenuButton toggle = MakeButton(page, "MCE_XUnityToggle", ToggleLabel(), available ? XUnityColor : DisabledColor, left, top, half, height, null);''',
'''    internal static void MakeXUnityButtons(RectTransform page, float left, float top, float width, float height, float gap)
    {
        float third = (width - gap * 2f) / 3f;
        bool available = XUnityInstalled;
        PeakMenuButton toggle = MakeButton(page, "MCE_XUnityToggle", ToggleKey(), available ? XUnityColor : DisabledColor, left, top, third, height, null);'''),
('''                Notify(Term("没装 XUnity AutoTranslator，或它的结构已变，无法切换。", "沒裝 XUnity AutoTranslator，或它的結構已變，無法切換。", "XUnity AutoTranslator is not installed or has changed."));
                return;
            }

            XUnityBridge.ToggleTranslation();
            toggle.SetText(ToggleLabel());
            Notify(XUnityBridge.IsTranslatedMode
                ? Term("已切回译文。", "已切回譯文。", "Showing translations.")
                : Term("正在显示原文（再点一次切回译文；XUnity 快捷键 Alt+T 同效）。", "正在顯示原文（再點一次切回譯文；XUnity 快捷鍵 Alt+T 同效）。", "Showing original texts (click again to restore; same as Alt+T)."));
        });
        toggle.gameObject.AddComponent<XUnityToggleLabel>().Button = toggle;

        MakeButton(page, "MCE_XUnityReload", Term("重载翻译", "重載翻譯", "Reload texts"), available ? XUnityColor : DisabledColor, left + half + gap, top, half, height, () =>
        {
            if (!XUnityBridge.CanReload)
            {
                Notify(Term("没装 XUnity AutoTranslator，或它的结构已变，无法重载。", "沒裝 XUnity AutoTranslator，或它的結構已變，無法重載。", "XUnity AutoTranslator is not installed or has changed."));
                return;
            }

            if (XUnityBridge.ReloadTranslations())
            {
                Notify(Term("已让 XUnity 重读 Translation 目录下的词典（同 Alt+R）。", "已讓 XUnity 重讀 Translation 目錄下的詞典（同 Alt+R）。", "XUnity reloaded its translation files (same as Alt+R)."));
            }
        });
    }

    private static string ToggleLabel()
    {
        return XUnityBridge.IsTranslatedMode
            ? Term("显示原文", "顯示原文", "Show original")
            : Term("显示译文", "顯示譯文", "Show translated");
    }''',
'''                Notify(Loc.Get("NOTICE_NO_XUNITY"), warning: true);
                return;
            }

            XUnityBridge.ToggleTranslation();
            toggle.SetLocalizationIndex(Loc.Index(ToggleKey()));
            Notify(Loc.Get(XUnityBridge.IsTranslatedMode ? "NOTICE_SHOWING_TRANSLATED" : "NOTICE_SHOWING_ORIGINAL"));
        });
        toggle.gameObject.AddComponent<XUnityToggleLabel>().Button = toggle;

        MakeButton(page, "MCE_XUnityReload", "BTN_RELOAD", available ? XUnityColor : DisabledColor, left + third + gap, top, third, height, () =>
        {
            if (!XUnityBridge.CanReload)
            {
                Notify(Loc.Get("NOTICE_NO_XUNITY"), warning: true);
                return;
            }

            if (XUnityBridge.ReloadTranslations())
            {
                Notify(Loc.Get("NOTICE_RELOADED"));
            }
        });

        MakeButton(page, "MCE_OpenFolder", "BTN_OPEN_FOLDER", FolderColor, left + (third + gap) * 2f, top, third, height, OpenRootFolder);
    }

    /// <summary>打开 config\\ModConfigEnhance\\（language 与 export 都在里面）。</summary>
    private static void OpenRootFolder()
    {
        try
        {
            Directory.CreateDirectory(Loc.LanguageDirectory);
            Directory.CreateDirectory(Loc.ExportDirectory);
            Application.OpenURL("file:///" + Loc.RootDirectory.Replace('\\\\', '/'));
        }
        catch (Exception ex)
        {
            Notify(Loc.Format("NOTICE_OPEN_FOLDER_FAILED", ex.Message), warning: true);
        }
    }

    private static string ToggleKey()
    {
        return XUnityBridge.IsTranslatedMode ? "BTN_SHOW_ORIGINAL" : "BTN_SHOW_TRANSLATED";
    }'''),
('''            if (Button != null && XUnityInstalled)
            {
                Button.SetText(ToggleLabel());
            }''',
'''            if (Button != null && XUnityInstalled)
            {
                Button.SetLocalizationIndex(Loc.Index(ToggleKey()));
            }'''),
('''    private static PeakMenuButton MakeButton(RectTransform page, string name, string label, Color color, float left, float top, float width, float height, Action? onClick)
    {
        PeakMenuButton button = MenuAPI.CreateMenuButton(name).ParentTo(page);
        float templateHeight = button.RectTransform.sizeDelta.y;
        button.SetAnchorMin(new Vector2(0f, 1f))
            .SetAnchorMax(new Vector2(0f, 1f))
            .SetPivot(new Vector2(0f, 1f))
            .SetSize(new Vector2(width, height))
            .SetPosition(new Vector2(left, -top))
            .SetText(label)
            .SetColor(color);''',
'''    /// <summary><paramref name="termKey"/> 是 Loc 词条键：按钮文字挂 LocalizedText，切语言即时跟着变。</summary>
    internal static PeakMenuButton MakeButton(RectTransform page, string name, string termKey, Color color, float left, float top, float width, float height, Action? onClick)
    {
        Loc.EnsureRegistered();
        PeakMenuButton button = MenuAPI.CreateMenuButton(name).ParentTo(page);
        float templateHeight = button.RectTransform.sizeDelta.y;
        button.SetAnchorMin(new Vector2(0f, 1f))
            .SetAnchorMax(new Vector2(0f, 1f))
            .SetPivot(new Vector2(0f, 1f))
            .SetSize(new Vector2(width, height))
            .SetPosition(new Vector2(left, -top))
            .SetLocalizationIndex(Loc.Index(termKey))
            .SetColor(color);'''),
('''                Notify(Term("没装 XUnity AutoTranslator，查漏翻不可用。", "沒裝 XUnity AutoTranslator，查漏翻不可用。", "XUnity AutoTranslator is not installed."));''',
 '''                Notify(Loc.Get("NOTICE_NO_XUNITY_CHECK"), warning: true);'''),
('''            string summary = untranslatedOnly
                ? Term($"未翻译 {written} 条（已翻 {skipped} 条，已是中文 {noNeed} 条）", $"未翻譯 {written} 條（已翻 {skipped} 條，已是中文 {noNeed} 條）", $"{written} untranslated ({skipped} translated, {noNeed} already CJK)")
                : Term($"已导出 {written} 条", $"已導出 {written} 條", $"Exported {written} entries");
            if (ModConfigSidebarWidgets.ExportSelectedOnly)
            {
                summary += Term($"，{mods.Count} 个已选模组", $"，{mods.Count} 個已選模組", $", {mods.Count} selected mods");
            }''',
'''            string summary = untranslatedOnly
                ? Loc.Format("NOTICE_UNTRANSLATED", written, skipped, noNeed)
                : Loc.Format("NOTICE_EXPORTED", written);
            if (ModConfigSidebarWidgets.ExportSelectedOnly)
            {
                summary += Loc.Format("NOTICE_SELECTED_MODS", mods.Count);
            }'''),
('''            Notify(Term("导出失败，详见日志。", "導出失敗，詳見日誌。", "Export failed, see log."));''',
 '''            Notify(Loc.Get("NOTICE_EXPORT_FAILED"), warning: true);'''),
('''    private static void Notify(string text)
    {
        ModConfigDescriptionPanel.Current?.ShowNotice(text);
    }''',
'''    private static void Notify(string text, bool warning = false)
    {
        ModConfigDescriptionPanel.Current?.ShowNotice(text, warning);
    }'''),
('    internal static string ExportDirectory => Path.Combine(Paths.ConfigPath, "LocalFix翻译导出");',
 '    internal static string ExportDirectory => Loc.ExportDirectory;'),
('/// 导出文件放在 BepInEx\\config\\LocalFix翻译导出\\，翻好后改名扔进 Translation\\zh\\Text\\ 即生效。',
 '/// 导出文件放在 BepInEx\\config\\ModConfigEnhance\\export\\，翻好后改名扔进 Translation\\<语言>\\Text\\ 即生效。'),
('    private static readonly Color DisabledColor = new Color(0.3f, 0.3f, 0.3f);',
 '    private static readonly Color DisabledColor = new Color(0.3f, 0.3f, 0.3f);\n    private static readonly Color FolderColor = new Color(0.55f, 0.36f, 0.16f);'),
]
for a, b in rep:
    assert a in s, a[:70]
    s = s.replace(a, b)

start = s.index('        sb.Append("// LocalFix ")')
end = s.index('        if (untranslatedOnly)\n        {\n            sb.Append(liveQuery')
header = r'''        string lang = XUnityBridge.TargetLanguage ?? "zh";
        sb.Append("// ModConfig Enhance ").Append(Plugin.PluginVersion).Append(untranslatedOnly ? " — untranslated texts / 未翻译文本 " : " — texts to translate / 待翻译文本 ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append('\n');
        sb.Append("// ── What is this / 这是什么 ──\n");
        sb.Append("// Every text the ModConfig settings page shows (mod names, sections, option names, descriptions, dropdown choices), grouped by mod,\n");
        sb.Append("// in XUnity AutoTranslator dictionary format: original=translation.\n");
        sb.Append("// ModConfig「模组设置」页会显示的全部文字，按模组分块，格式与 XUnity 词典相同：原文=译文。\n");
        sb.Append("// ── How to use / 怎么用 ──\n");
        sb.Append("// 1. Tick \"Selected only\" in the game and untick mods you don't need, then export again — the file gets much shorter.\n");
        sb.Append("//    先粗筛：勾「仅导出已选」，把不需要的模组去勾，再导出一次。\n");
        sb.Append("// 2. Fill the translation after \"=\" (an AI can do the whole file). Never change anything left of \"=\".\n");
        sb.Append("//    把译文填在等号右边（可整份交给 AI 翻），不要改动等号左边的任何字符。\n");
        sb.Append("// 3. Save as UTF-8 into BepInEx\\config\\Translation\\").Append(lang).Append("\\Text\\ (e.g. mods_xxx.txt), then click \"Reload texts\" (or Alt+R).\n");
        sb.Append("//    Lines with an empty right side are ignored by XUnity, so leaving them is fine.\n");
        sb.Append("//    存成 UTF-8，改名放进 Translation\\").Append(lang).Append("\\Text\\，回到游戏点「重载翻译」（或 Alt+R）即生效；右边留空的行会被忽略。\n");
        sb.Append("// ── Rules for translators / AI ──\n");
        sb.Append("// · Lines starting with // are comments. \"// ==== Mod (GUID version) ====\" and \"// -- [Section]\" are block markers: don't translate or delete them.\n");
        sb.Append("// · \\n is a line break, \\= an equals sign — keep the same notation. Copy numbers, units, key names, mod names, GUIDs and paths as-is.\n");
        sb.Append("// · Single key / enum names (A, F1, auto, eu, Left) are dropdown choices: usually copy or leave empty.\n");
        sb.Append("// · Each original appears once globally (the XUnity dictionary is global); repeats in later mods are not listed.\n");
        sb.Append("// · Skipped automatically: KeyCode / key-name lists and choice lists longer than ").Append(MaxChoices).Append(" items.\n");
        sb.Append("// · // 开头是注释，分块标记不用翻不要删；\\n 换行、\\= 等号照原样；数字、单位、按键名、模组名、GUID、路径照抄；\n");
        sb.Append("//   单个键名 / 枚举值是下拉选项，照抄或留空；已是中文或中英双语的行留空即可。\n");
'''
s = s[:start] + header + s[end:]

a = '''            sb.Append(liveQuery
                ? "// [查漏翻] 「已翻」直接问 XUnity 当前加载的词典（精确条目、r:/sr: 正则、_Substitutions 都算），与游戏里实际替换一致。\\n"
                : "// [查漏翻] XUnity 词典接口不可用，退回按 Translation 目录下所有 .txt 的原文精确匹配；用正则（r: / sr:）覆盖的条目会误报。\\n");
            sb.Append("//          已含中文的原文按「无需翻译」计，不列出。\\n");'''
b = '''            sb.Append(liveQuery
                ? "// [Untranslated] \\"translated\\" = XUnity's loaded dictionary says so (exact entries, r:/sr: regex and _Substitutions included), same as in-game.\\n"
                : "// [Untranslated] XUnity's dictionary API was unavailable; fell back to exact matches against every .txt under Translation — regex-covered entries are reported as untranslated.\\n");
            sb.Append("//   Originals that already contain CJK characters count as \\"no need\\" and are not listed. 已含中文的原文按「无需翻译」计。\\n");'''
assert a in s, 'liveQuery block'
s = s.replace(a, b)

a = '''            sb.Append("// [仅导出已选] 只含左栏「导出」列勾选的 ").Append(mods.Count).Append(" 个模组。\\n");'''
b = '''            sb.Append("// [Selected only] Only the ").Append(mods.Count).Append(" mods ticked in the Export column. 只含左栏「导出」列勾选的模组。\\n");'''
assert a in s, 'selected block'
s = s.replace(a, b)

start = s.index('    private static string Term(string simplified')
end = s.index('        };\n    }\n', start) + len('        };\n    }\n')
s = s[:start].rstrip('\n') + '\n' + s[end:]
io.open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('ok')
