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

# ---------------- XUnityBridge.cs ----------------
def bridge(s):
    s = rep(s, [
        ('''    private static FieldInfo? _language;
    private static FieldInfo? _translationsPath;
''', '''    private static FieldInfo? _language;
    private static FieldInfo? _translationsPath;
    // 运行时切目标语言要重算的几项（Settings 启动时按 {Lang} 模板算出来，之后不再变）
    private static FieldInfo? _applicationName;
    private static FieldInfo? _translationDirectory;
    private static FieldInfo? _outputFile;
    private static FieldInfo? _substitutionFile;
    private static FieldInfo? _preprocessorsFile;
    private static FieldInfo? _postprocessorsFile;
    private static FieldInfo? _substitutionFilePath;
    private static FieldInfo? _preprocessorsFilePath;
    private static FieldInfo? _postprocessorsFilePath;
    private static FieldInfo? _toLanguageWhitespace;
    private static FieldInfo? _environmentCurrent;
    private static PropertyInfo? _environmentTranslationPath;
    private static MethodInfo? _requiresWhitespace;
'''),
        ('''                _language = AccessTools.Field(settingsType, "Language");
                _translationsPath = AccessTools.Field(settingsType, "TranslationsPath");
''', '''                _language = AccessTools.Field(settingsType, "Language");
                _translationsPath = AccessTools.Field(settingsType, "TranslationsPath");
                _applicationName = AccessTools.Field(settingsType, "ApplicationName");
                _translationDirectory = AccessTools.Field(settingsType, "TranslationDirectory");
                _outputFile = AccessTools.Field(settingsType, "OutputFile");
                _substitutionFile = AccessTools.Field(settingsType, "SubstitutionFile");
                _preprocessorsFile = AccessTools.Field(settingsType, "PreprocessorsFile");
                _postprocessorsFile = AccessTools.Field(settingsType, "PostprocessorsFile");
                _substitutionFilePath = AccessTools.Field(settingsType, "SubstitutionFilePath");
                _preprocessorsFilePath = AccessTools.Field(settingsType, "PreprocessorsFilePath");
                _postprocessorsFilePath = AccessTools.Field(settingsType, "PostprocessorsFilePath");
                _toLanguageWhitespace = AccessTools.Field(settingsType, "ToLanguageUsesWhitespaceBetweenWords");
                Type? environmentType = AccessTools.TypeByName("XUnity.AutoTranslator.Plugin.Core.PluginEnvironment");
                _environmentCurrent = environmentType != null ? AccessTools.Field(environmentType, "Current") : null;
                Type? environmentInterface = AccessTools.TypeByName("XUnity.AutoTranslator.Plugin.Core.IPluginEnvironment");
                _environmentTranslationPath = environmentInterface != null ? AccessTools.Property(environmentInterface, "TranslationPath") : null;
                Type? languageHelper = AccessTools.TypeByName("XUnity.AutoTranslator.Plugin.Core.Utilities.LanguageHelper");
                _requiresWhitespace = languageHelper != null ? AccessTools.Method(languageHelper, "RequiresWhitespaceUponLineMerging", new[] { typeof(string) }) : null;
'''),
        ('''    private static string? ReadStatic(FieldInfo? field)
    {''', '''    /// <summary>能不能在运行时改目标语言（所有要重算的字段都找到了）。</summary>
    internal static bool CanSetLanguage
    {
        get
        {
            Resolve();
            return _language != null && _translationsPath != null && _translationDirectory != null && _outputFile != null
                && _autoTranslationsFilePath != null && _environmentCurrent != null && _environmentTranslationPath != null;
        }
    }

    /// <summary>词典根目录（Directory= 里 {Lang} 之前的部分，通常是 BepInEx\config\Translation）；拿不到为 null。</summary>
    internal static string? TranslationRoot
    {
        get
        {
            Resolve();
            try
            {
                string? basePath = EnvironmentTranslationPath;
                string? template = ReadStatic(_translationDirectory);
                if (basePath == null || template == null)
                {
                    return null;
                }

                template = Separators(template);
                int cut = template.IndexOf("{lang}", StringComparison.OrdinalIgnoreCase);
                string head = cut >= 0 ? template.Substring(0, cut) : template;
                return Path.GetFullPath(Path.Combine(basePath, head.TrimEnd(Path.DirectorySeparatorChar)));
            }
            catch
            {
                return null;
            }
        }
    }

    private static string? EnvironmentTranslationPath
    {
        get
        {
            try
            {
                object? env = _environmentCurrent?.GetValue(null);
                return env != null ? _environmentTranslationPath?.GetValue(env) as string : null;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// 运行时把 XUnity 的目标语言改成 <paramref name="code"/>：Settings.Language 与五个按 {Lang} 模板算出的路径一起重算
    /// （与 Settings.Configure 同一套公式），目标语言的空格规则也重算。之后调 <see cref="ReloadTranslations"/> 就会从新目录读词典、
    /// 并把词典里没有的文本还原成原文。XUnity 官方没有这个入口（Language 只在启动时读 ini），这里是对着 5.6.1 的字段做的。
    /// </summary>
    internal static bool SetLanguage(string code)
    {
        if (!CanSetLanguage)
        {
            return false;
        }

        try
        {
            string basePath = EnvironmentTranslationPath ?? "";
            string app = ReadStatic(_applicationName) ?? "";
            string Param(string path) => path.Replace("{lang}", code).Replace("{Lang}", code).Replace("{GameExeName}", app);
            string Build(FieldInfo? template) => Path.Combine(basePath, Param(Separators(ReadStatic(template) ?? "")));

            _language!.SetValue(null, string.Intern(code));
            _translationsPath!.SetValue(null, Build(_translationDirectory));
            _autoTranslationsFilePath!.SetValue(null, Build(_outputFile));
            _substitutionFilePath?.SetValue(null, Build(_substitutionFile));
            _preprocessorsFilePath?.SetValue(null, Build(_preprocessorsFile));
            _postprocessorsFilePath?.SetValue(null, Build(_postprocessorsFile));
            if (_toLanguageWhitespace != null && _requiresWhitespace != null)
            {
                _toLanguageWhitespace.SetValue(null, (bool)_requiresWhitespace.Invoke(null, new object[] { code }));
            }

            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[语言] 改 XUnity 目标语言失败: {ex.Message}");
            return false;
        }
    }

    private static string Separators(string path)
    {
        return path.Replace('/', Path.DirectorySeparatorChar).Replace('\\\\', Path.DirectorySeparatorChar);
    }

    private static string? ReadStatic(FieldInfo? field)
    {'''),
    ])
    return s

rw("XUnityBridge.cs", bridge)

# ---------------- Plugin.cs ----------------
def plugin(s):
    return rep(s, [
        ('''    internal const string DescMirror = "When a language file is selected, copy its translations of this mod's own option names and descriptions into XUnity's Translation\\\\<lang>\\\\Text\\\\ModConfigEnhance_<file>.txt so the settings list is translated too. Only written when the file's language matches XUnity's target language. Off = never touch the Translation folder.";''',
         '''    internal const string DescSwitchXUnity = "When a language file is picked (or the game language changes while following it), switch XUnity AutoTranslator to that language at once: its target language and dictionary folder become Translation\\\\<code>\\\\Text\\\\, the file's translations (everything except MCE_ keys) are written there as ModConfigEnhance_<code>.txt, dictionaries are reloaded and Language= in AutoTranslatorConfig.ini is updated for the next start. Texts without a translation revert to the original. Off = never touch XUnity.";'''),
        ('    internal static ConfigEntry<bool> MirrorToXUnity = null!;', '    internal static ConfigEntry<bool> SwitchXUnity = null!;'),
        ('        MirrorToXUnity = Config.Bind(secLoc, "Mirror language file to XUnity", true, DescMirror);',
         '        SwitchXUnity = Config.Bind(secLoc, "Switch XUnity language with the language file", true, DescSwitchXUnity);'),
    ])

rw("Plugin.cs", plugin)

# ---------------- ModConfigLocalizationPatch.cs ----------------
def locpatch(s):
    return rep(s, [
        ('                    Loc.Mirror(Loc.FollowGame);', '                    Loc.SyncXUnity(Loc.FollowGame);'),
    ])

rw("ModConfigLocalizationPatch.cs", locpatch)

# ---------------- Loc.cs ----------------
SYNC = r'''    // ── 让 XUnity 跟着切语言 ──────────────────────────────────────

    /// <summary>
    /// 选中语言文件（或跟随游戏时游戏语言变了）后：把 XUnity 的目标语言切到该文件的语言码，词典目录随之变成
    /// <c>Translation\&lt;码&gt;\Text\</c>；文件里非 MCE_ 的行写成那个目录下的 <c>ModConfigEnhance_&lt;码&gt;.txt</c>（其他语言目录里的旧镜像先删）；
    /// 重载词典——词典里没有的文本 XUnity 会还原成原文；再把 Language= 写回 AutoTranslatorConfig.ini 让下次启动一致。
    /// 返回给用户看的一行说明（空 = 没什么可说）。
    /// </summary>
    internal static string SyncXUnity(string selection)
    {
        if (!XUnityBridge.Installed || !Plugin.SwitchXUnity.Value)
        {
            return "";
        }

        string stem = string.IsNullOrEmpty(selection) ? FollowStem() : selection;
        try
        {
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
            DeleteAllMirrors(root, dir!);

            int count = 0;
            string source = Path.Combine(LanguageDirectory, stem + ".txt");
            if (File.Exists(source))
            {
                LanguageFile file = Parse(source);
                StringBuilder sb = new StringBuilder();
                sb.Append("// Generated by ModConfig Enhance from language\\").Append(stem).Append(".txt - do not edit, it is rewritten on every language switch.\n");
                foreach (KeyValuePair<string, string> pair in file.Entries)
                {
                    if (IsTermKey(pair.Key) || pair.Key == pair.Value)
                    {
                        continue;
                    }

                    sb.Append(Escape(pair.Key)).Append('=').Append(Escape(pair.Value)).Append('\n');
                    count++;
                }

                if (count > 0)
                {
                    File.WriteAllText(Path.Combine(dir!, MirrorPrefix + code + ".txt"), sb.ToString(), new UTF8Encoding(false));
                }
            }

            XUnityBridge.ReloadTranslations();
            return Format(switched ? "NOTICE_XUNITY_SWITCHED" : "NOTICE_XUNITY_SYNCED", code, count);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[语言] 同步 XUnity 失败: {ex.Message}");
            return "";
        }
    }

    /// <summary>文件名 → XUnity 语言码：Translation\ 下已有同名目录用同名；有「同一语言」的目录（zh-cn ↔ zh）沿用它，不另起目录；否则就用文件名。</summary>
    internal static string ResolveXUnityCode(string stem, string? root)
    {
        stem = stem.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            return stem;
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

        return sameLanguage ?? stem;
    }

    private static void DeleteAllMirrors(string? root, string currentDir)
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
'''

def loc(s):
    i = s.index("    // ── 镜像给 XUnity ──")
    j = s.index("    private static bool IsTermKey(string key)")
    s = s[:i] + SYNC + s[j + len("    private static bool IsTermKey(string key)\n"):]
    s = rep(s, [
        ('            result.Mirror = Mirror(selection);', '            result.Mirror = SyncXUnity(selection);'),
        ('        new("NOTICE_LANG_MIRRORED", "Mirrored to XUnity: {0}", "已镜像到 XUnity 词典：{0}", "已鏡像到 XUnity 詞典：{0}"),\n', ''),
        ('        new("NOTICE_LANG_MIRROR_SKIPPED", "Not mirrored to XUnity: its target language is \\"{0}\\", the file is \\"{1}\\". Set Language={1} in AutoTranslatorConfig.ini and restart.", "未镜像到 XUnity：它的目标语言是「{0}」，文件是「{1}」。要用它翻设置文字，请把 AutoTranslatorConfig.ini 的 Language 改成 {1} 后重启。", "未鏡像到 XUnity：它的目標語言是「{0}」，檔案是「{1}」。要用它翻設定文字，請把 AutoTranslatorConfig.ini 的 Language 改成 {1} 後重啟。"),\n',
         '        new("NOTICE_XUNITY_SWITCHED", "XUnity switched to \\"{0}\\" ({1} entries handed over); texts without a translation show the original.", "XUnity 已切到「{0}」（交给它 {1} 条）；词典里没有的文本显示原文。", "XUnity 已切到「{0}」（交給它 {1} 條）；詞典裡沒有的文字顯示原文。"),\n        new("NOTICE_XUNITY_SYNCED", "XUnity dictionary \\"{0}\\" refreshed ({1} entries handed over).", "XUnity 词典「{0}」已刷新（交给它 {1} 条）。", "XUnity 詞典「{0}」已重新整理（交給它 {1} 條）。"),\n        new("NOTICE_XUNITY_NO_SWITCH", "XUnity could not be switched at runtime (structure changed); it stays on \\"{0}\\". Only this mod\'s UI changed.", "无法在运行时切换 XUnity（结构不符），它仍是「{0}」；只改了本模组界面。", "無法在執行時切換 XUnity（結構不符），它仍是「{0}」；只改了本模組介面。"),\n'),
        ('        new("Mirror language file to XUnity", "Mirror language file to XUnity", "把语言文件镜像给 XUnity", "把語言檔案鏡像給 XUnity"),',
         '        new("Switch XUnity language with the language file", "Switch XUnity language with the language file", "语言文件同时切换 XUnity 的语言", "語言檔案同時切換 XUnity 的語言"),'),
        ('        new(Plugin.DescMirror, Plugin.DescMirror,\n            "选中语言文件后，把其中本模组 cfg 选项的译文写一份到 XUnity 的 Translation\\\\<语言>\\\\Text\\\\ModConfigEnhance_<文件名>.txt，让设置列表里本模组的选项名和说明也被翻译。只在文件语言与 XUnity 目标语言一致时写。关掉则不碰 Translation 目录。",\n            "選中語言檔案後，把其中本模組 cfg 選項的譯文寫一份到 XUnity 的 Translation\\\\<語言>\\\\Text\\\\ModConfigEnhance_<檔名>.txt，讓設定列表裡本模組的選項名和說明也被翻譯。只在檔案語言與 XUnity 目標語言一致時寫。關掉則不碰 Translation 目錄。"),',
         '        new(Plugin.DescSwitchXUnity, Plugin.DescSwitchXUnity,\n            "选中语言文件（或跟随游戏时游戏语言变了）后，立刻把 XUnity AutoTranslator 切到该语言：目标语言与词典目录变成 Translation\\\\<码>\\\\Text\\\\，文件里除 MCE_ 键外的译文写成那里的 ModConfigEnhance_<码>.txt，重载词典，并把 AutoTranslatorConfig.ini 的 Language= 改成该码供下次启动。词典里没有的文本还原成原文。关掉则完全不碰 XUnity。",\n            "選中語言檔案（或跟隨遊戲時遊戲語言變了）後，立刻把 XUnity AutoTranslator 切到該語言：目標語言與詞典目錄變成 Translation\\\\<碼>\\\\Text\\\\，檔案裡除 MCE_ 鍵外的譯文寫成那裡的 ModConfigEnhance_<碼>.txt，重載詞典，並把 AutoTranslatorConfig.ini 的 Language= 改成該碼供下次啟動。詞典裡沒有的文字還原成原文。關掉則完全不碰 XUnity。"),'),
    ])
    return s

rw("Loc.cs", loc)
print("ok")
