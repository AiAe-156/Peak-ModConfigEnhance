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
        assert a in s, a[:70]
        s = s.replace(a, b, 1)
    return s

# ---------- ModConfigTranslationExport.cs ----------
def export(s):
    s = rep(s, [
        ('''            if (ModConfigSidebarWidgets.ExportSelectedOnly)
            {
                mods.RemoveAll(mod => !ModConfigSidebarWidgets.ShouldExport(mod.Name));
            }

            Func<string, bool> isTranslated;''',
         '''            if (ModConfigSidebarWidgets.ExportSelectedOnly)
            {
                // 本模组自己的选项永远在（导出文件同时是本模组的语言文件）
                mods.RemoveAll(mod => mod.Guid != Plugin.PluginGuid && !ModConfigSidebarWidgets.ShouldExport(mod.Name));
            }

            Func<string, bool> isTranslated;'''),
        ('''        sb.Append('\\n');

        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        written = 0;''',
         '''        sb.Append('\\n');

        // 本模组界面词条：键是稳定 ID，等号右边预填英文，翻完这段就是本模组的语言文件
        sb.Append("// ==== ModConfig Enhance UI (MCE_ keys: translate the right side only, never the key) ====\\n");
        foreach (Loc.Term term in Loc.Terms)
        {
            sb.Append(Loc.KeyPrefix).Append(term.Key).Append('=').Append(Escape(term.English)).Append('\\n');
        }

        sb.Append('\\n');

        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        written = 0;'''),
    ])
    a = '''        sb.Append("// 3. Save as UTF-8 into BepInEx\\\\config\\\\Translation\\\\").Append(lang).Append("\\\\Text\\\\ (e.g. mods_xxx.txt), then click \\"Reload texts\\" (or Alt+R).\\n");
        sb.Append("//    Lines with an empty right side are ignored by XUnity, so leaving them is fine.\\n");
        sb.Append("//    存成 UTF-8，改名放进 Translation\\\\").Append(lang).Append("\\\\Text\\\\，回到游戏点「重载翻译」（或 Alt+R）即生效；右边留空的行会被忽略。\\n");'''
    b = '''        sb.Append("// 3. Save as UTF-8, rename it to your language code (").Append(lang).Append(".txt, de-de.txt, ja-jp.txt ...) and put it in BepInEx\\\\config\\\\ModConfigEnhance\\\\language\\\\.\\n");
        sb.Append("//    In the game: left column, third row -> Refresh -> pick the file. This mod's UI switches at once; the rest is handed to\\n");
        sb.Append("//    XUnity AutoTranslator (Translation\\\\").Append(lang).Append("\\\\Text\\\\ModConfigEnhance_<file>.txt) and reloaded - no restart, as long as the file's\\n");
        sb.Append("//    language code matches XUnity's Language= (").Append(lang).Append("). Empty right sides are ignored. \\"Reload texts\\" (Alt+R) only re-reads after hand edits.\\n");
        sb.Append("//    存成 UTF-8，改名为语言代码（").Append(lang).Append(".txt / de-de.txt / ja-jp.txt……），放进 config\\\\ModConfigEnhance\\\\language\\\\；\\n");
        sb.Append("//    游戏里左栏第三行 → 刷新 → 选中它：本模组界面立刻切换，其余条目镜像给 XUnity 并重载，不用重启（文件语言码需与 XUnity 的 Language= 一致）。\\n");
        sb.Append("//    右边留空的行会被忽略。「重载翻译」只在手改文件后重读用；「查漏翻」是补漏工具，不是必经步骤。\\n");'''
    return rep(s, [(a, b)])

rw("ModConfigTranslationExport.cs", export)

# ---------- Loc.cs ----------
README = r'''    private static string BuildReadme()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("// ModConfig Enhance ").Append(Plugin.PluginVersion).Append(" - language files / 语言文件说明 (rewritten on every start)\n");
        sb.Append("//\n");
        sb.Append("// A language file is simply the output of \"Export texts\" with translations filled in:\n");
        sb.Append("//   - the first block (MCE_ keys) is this mod's own UI: keys are stable IDs, translate only the right side of \"=\";\n");
        sb.Append("//   - the blocks after it are every mod's option names / descriptions / choices (original=translation), as XUnity AutoTranslator reads them.\n");
        sb.Append("//\n");
        sb.Append("// How to make one\n");
        sb.Append("// 1. In the game: mod settings -> left column -> \"Export texts\" (tick \"Selected only\" first to drop mods you don't need; this mod is always included).\n");
        sb.Append("// 2. Open the exported file in config\\ModConfigEnhance\\export\\, fill the right side of each \"=\" (an AI can do the whole file).\n");
        sb.Append("//    \\n is a line break, \\= an equals sign, {0} {1} {2} are placeholders - keep them. Never change the left side.\n");
        sb.Append("// 3. Save as UTF-8, rename it to your language code in lowercase (de-de.txt, ja-jp.txt, fr-fr.txt, pt-br.txt ...) and move it into this folder.\n");
        sb.Append("//    Optional first line: // display: <name shown in the dropdown>. Files starting with \"_\" are ignored by the dropdown.\n");
        sb.Append("// 4. Back in the game: third toolbar row -> \"Refresh\" -> pick the file. What happens:\n");
        sb.Append("//    - this mod's UI switches immediately;\n");
        sb.Append("//    - the other lines are copied to XUnity's Translation\\<lang>\\Text\\ModConfigEnhance_<file>.txt and XUnity reloads: the settings list and\n");
        sb.Append("//      every other translated text update without a restart, PROVIDED the file's language code matches Language= in AutoTranslatorConfig.ini\n");
        sb.Append("//      (zh-cn matches zh / zh-CN). If it doesn't match, the description panel tells you; change Language= and restart once.\n");
        sb.Append("//    Errors (missing \"=\", empty file ...) are shown in orange in the description panel with line numbers.\n");
        sb.Append("// 5. \"Reload texts\" (Alt+R) only re-reads the dictionaries after you edit a file by hand. \"Untranslated\" lists what is still missing - optional.\n");
        sb.Append("// 6. Please send finished translations to the author (AiAe): e-mail 2323086800@qq.com / QQ group 1104320838. They will ship with the next release.\n");
        sb.Append("//\n");
        sb.Append("// 语言文件就是「导出翻译」的产物填上译文：第一块 MCE_ 键是本模组界面（键是稳定 ID，只翻等号右边），后面是各模组的选项文本（原文=译文，XUnity 词典格式）。\n");
        sb.Append("// 1. 游戏里：模组设置 → 左栏「导出翻译」（先勾「仅导出已选」去掉不需要的模组；本模组永远包含）。\n");
        sb.Append("// 2. 打开 config\\ModConfigEnhance\\export\\ 里的文件，填等号右边（可整份交给 AI）；\\n 换行、\\= 等号、{0} 占位符原样保留，左边一个字符都别动。\n");
        sb.Append("// 3. 存成 UTF-8，改名为小写语言代码（de-de.txt、ja-jp.txt……）放进本目录；首行可写 // display: 显示名；_ 开头的文件不进下拉框。\n");
        sb.Append("// 4. 回游戏：左栏第三行 → 刷新 → 选中它。本模组界面立刻切换；其余条目镜像到 XUnity 的 Translation\\<语言>\\Text\\ModConfigEnhance_<文件名>.txt 并重载，\n");
        sb.Append("//    设置列表与全游戏的翻译不用重启就更新——前提是文件语言码与 AutoTranslatorConfig.ini 的 Language= 一致（zh-cn 与 zh / zh-CN 算一致）；不一致会在说明面板提示，改 ini 重启一次。\n");
        sb.Append("//    出错（缺等号、空文件……）在说明面板用橙色带行号提示。\n");
        sb.Append("// 5. 「重载翻译」（Alt+R）只在手改文件后重读用；「查漏翻」列出还没翻的，可选。\n");
        sb.Append("// 6. 翻好的成品欢迎寄给作者 AiAe：邮箱 2323086800@qq.com / QQ 群 1104320838，下个版本随包附带。\n");
        return sb.ToString();
    }

    private static string BuildFile(string display, Func<Term, string> pick, Func<Term, string> pickCfg)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("// display: ").Append(display).Append('\n');
        sb.Append("// ModConfig Enhance ").Append(Plugin.PluginVersion).Append(" - built-in language file. Edit freely; it is never overwritten. Reset by deleting it.\n");
        sb.Append("// See ").Append(ReadmeFile).Append(" for the format and how to make a new language.\n");

'''
# C# 源里的 "\\n" 字面量：上面 raw 字符串里写的 \n 在 C# 里要是 \\n（换行转义在字符串内）
README = README.replace('\\n");', '\\\\n");').replace('\\\\\\\\n");', '\\\\n");')

def loc(s):
    s = rep(s, [
        ('    internal const string TemplateFile = "_template.txt";', '    internal const string ReadmeFile = "_readme.txt";'),
        ('result.Message += "\\n" + Format("NOTICE_LANG_MISSING", file.Name, missing, TemplateFile);',
         'result.Message += "\\n" + Format("NOTICE_LANG_MISSING", file.Name, missing, "en-us.txt");'),
        ('/// 文件名不以 <c>_</c> 开头的才算语言文件（<c>_template.txt</c> 是模板）；首行可写 <c>// display: 显示名</c>。',
         '/// 文件名不以 <c>_</c> 开头的才算语言文件（<c>_readme.txt</c> 是说明）；首行可写 <c>// display: 显示名</c>。\n/// 语言文件就是「导出翻译」的产物：MCE_ 块 + 各模组选项文本，翻完改名放进来即可，非 MCE_ 行镜像给 XUnity。'),
        ('    /// <summary>首启写 en-us / zh-cn / zh-tw（只建不覆），模板每次重写。</summary>',
         '    /// <summary>首启写 en-us / zh-cn / zh-tw（只建不覆），说明文件每次重写。</summary>'),
        ('            File.WriteAllText(Path.Combine(LanguageDirectory, TemplateFile), BuildFile("", true, t => t.English, t => t.English), new UTF8Encoding(false));',
         '            File.WriteAllText(Path.Combine(LanguageDirectory, ReadmeFile), BuildReadme(), new UTF8Encoding(false));'),
        ('File.WriteAllText(path, BuildFile(display, false, pick, pickCfg), new UTF8Encoding(false));',
         'File.WriteAllText(path, BuildFile(display, pick, pickCfg), new UTF8Encoding(false));'),
    ])
    i = s.index('    private static string BuildFile(string display, bool template, Func<Term, string> pick, Func<Term, string> pickCfg)')
    j = s.index('        sb.Append(\'\\n\');\n        sb.Append("// ---- UI terms', i)
    return s[:i] + README + s[j:]

rw("Loc.cs", loc)
print("ok")
