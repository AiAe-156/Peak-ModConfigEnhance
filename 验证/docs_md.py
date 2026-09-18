# -*- coding: utf-8 -*-
import io
root = r"F:\Games\SteamLibrary\steamapps\common\PEAK\MODs\ModConfigEnhance"

def rep(s, pairs):
    for a, b in pairs:
        assert a in s, "MISSING: " + a[:80]
        s = s.replace(a, b, 1)
    return s

# ---------- Loc.cs：readme.md 放根目录；清旧 _readme.txt / _template.txt ----------
p = root + r"\src\Loc.cs"
s = io.open(p, encoding="utf-8").read()
s = rep(s, [
    ('    internal const string ReadmeFile = "_readme.txt";', '    internal const string ReadmeFile = "readme.md";'),
    ('/// 文件名不以 <c>_</c> 开头的才算语言文件（<c>_readme.txt</c> 是说明）；首行可写 <c>// display: 显示名</c>。',
     '/// 文件名不以 <c>_</c> 开头的 .txt 才算语言文件；首行可写 <c>// display: 显示名</c>。说明在根目录 <c>readme.md</c>。'),
    ('''    /// <summary>language\\_readme.txt 按当前界面语言重写（启动时与每次切语言后）。</summary>
    internal static void WriteReadme()
    {
        try
        {
            Directory.CreateDirectory(LanguageDirectory);
            File.WriteAllText(Path.Combine(LanguageDirectory, ReadmeFile), BuildReadme(), new UTF8Encoding(false));
        }''',
     '''    /// <summary>config\\ModConfigEnhance\\readme.md 按当前界面语言重写（启动时与每次切语言后）。</summary>
    internal static void WriteReadme()
    {
        try
        {
            Directory.CreateDirectory(RootDirectory);
            File.WriteAllText(Path.Combine(RootDirectory, ReadmeFile), BuildReadme(), new UTF8Encoding(false));
        }'''),
    ('''            File.WriteAllText(Path.Combine(LanguageDirectory, ReadmeFile), BuildReadme(), new UTF8Encoding(false));
            // 1.0.1 之前每次启动重写的模板文件，1.0.2 起由 _readme.txt 取代（它从不含用户内容，直接清掉）
            string legacyTemplate = Path.Combine(LanguageDirectory, "_template.txt");
            if (File.Exists(legacyTemplate))
            {
                File.Delete(legacyTemplate);
            }''',
     '''            WriteReadme();
            // 旧版每次启动重写的 _template.txt（≤1.0.1）与 _readme.txt（1.0.2–1.0.3）都不含用户内容，直接清掉
            foreach (string legacy in new[] { "_template.txt", "_readme.txt" })
            {
                string path = Path.Combine(LanguageDirectory, legacy);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }'''),
])
io.open(p, "w", encoding="utf-8", newline="\n").write(s)

# ---------- 导出文件头：砍掉「这是什么」「怎么用」，一句话指到 readme.md ----------
p = root + r"\src\ModConfigTranslationExport.cs"
s = io.open(p, encoding="utf-8").read()
i = s.index('        Line("//");\n        Line(L("// # What this is", "// # 这是什么", "// # 這是什麼"));')
j = s.index('        Line(L("// # Rules for translators / AI", "// # 给翻译者 / AI 的规则", "// # 給翻譯者 / AI 的規則"));')
s = s[:i] + '''        Line("//");
        Line(L("//   How to use: see BepInEx\\\\config\\\\ModConfigEnhance\\\\readme.md (export > translate > rename to a language code > put in language\\\\ > Refresh > pick).",
               "//   用法见 BepInEx\\\\config\\\\ModConfigEnhance\\\\readme.md（导出 → 翻译 → 改名为语言码 → 放进 language\\\\ → 刷新 → 选中）。",
               "//   用法見 BepInEx\\\\config\\\\ModConfigEnhance\\\\readme.md（導出 → 翻譯 → 改名為語言碼 → 放進 language\\\\ → 重新整理 → 選中）。"));
        Line("//");
''' + s[j:]
io.open(p, "w", encoding="utf-8", newline="\n").write(s)

# ---------- README.md ----------
p = root + r"\README.md"
s = io.open(p, encoding="utf-8").read()
s = rep(s, [
    ('| `_readme.txt` | 说明文件，每次启动 / 切语言后按当前界面语言重写（简中 / 繁中 / 英文之一） |\n',
     ''),
    ('`_` 开头的文件不进下拉框。',
     '`_` 开头的文件不进下拉框。说明文件是上一级的 `config\\ModConfigEnhance\\readme.md`，每次启动 / 切语言后按当前界面语言重写（简中 / 繁中 / 英文之一）。'),
    ('| 打开目录 | 打开 `config\\ModConfigEnhance\\` |', '| 打开目录 | 打开 `config\\ModConfigEnhance\\`（语言文件、导出文件、`readme.md` 都在里面） |'),
    ("`_readme.txt` is rewritten every start and hidden from the dropdown (any `_`-prefixed file is).",
     "`config\\ModConfigEnhance\\readme.md` (rewritten every start in the current UI language) explains everything; any `_`-prefixed file is hidden from the dropdown."),
])
io.open(p, "w", encoding="utf-8", newline="\n").write(s)

# ---------- CHANGELOG ----------
p = root + r"\CHANGELOG.md"
s = io.open(p, encoding="utf-8").read()
s = rep(s, [
    ('- **说明文字单语化**：`_readme.txt`、导出文件头按当前界面语言',
     '- **说明改为 `config\\ModConfigEnhance\\readme.md`**（Markdown，根目录，「打开目录」一点就见；旧 `_readme.txt` / `_template.txt` 启动时清掉），\n  导出文件头砍到只剩「翻译规则 + 本次导出」，教程一句话指到 readme.md。\n- **说明文字单语化**：`readme.md`、导出文件头按当前界面语言'),
])
io.open(p, "w", encoding="utf-8", newline="\n").write(s)
print("ok")
