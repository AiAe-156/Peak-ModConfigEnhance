#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace ModConfigEnhance;

/// <summary>
/// 与 XUnity AutoTranslator（5.6.1）的软对接，全反射。它的插件对象是 <c>AutoTranslationPlugin.Current</c>（internal static），
/// 上面有 Alt+T 对应的 <c>ToggleTranslation()</c>（所有已翻文本在原文 / 译文间切换，字段 <c>_isInTranslatedMode</c>）
/// 和 Alt+R 对应的 <c>ReloadTranslations()</c>（重读词典）。目标语言只在启动时读 ini，没有运行时切语言。
/// 依据：MODs\反编译\XUnity.AutoTranslator_5.6.1_2026-09-11。
/// </summary>
internal static class XUnityBridge
{
    internal const string Guid = "gravydevsupreme.xunity.autotranslator";
    private const string PluginTypeName = "XUnity.AutoTranslator.Plugin.Core.AutoTranslationPlugin";

    private const string CacheTypeName = "XUnity.AutoTranslator.Plugin.Core.TextTranslationCache";
    private const string UntranslatedTypeName = "XUnity.AutoTranslator.Plugin.Core.UntranslatedText";
    private const string SettingsTypeName = "XUnity.AutoTranslator.Plugin.Core.Configuration.Settings";
    /// <summary>XUnity 的 TranslationScopes.None：不分作用域，查全局词典。</summary>
    private const int ScopeNone = -1;

    private static bool _resolved;
    private static FieldInfo? _current;
    private static FieldInfo? _translatedMode;
    private static MethodInfo? _toggle;
    private static MethodInfo? _reload;

    // 查词典：AutoTranslationPlugin.TextCache（internal 实例字段）→ TextTranslationCache.TryGetTranslation(UntranslatedText, bool allowRegex, bool allowToken, int scope, out string)
    private static FieldInfo? _textCache;
    private static MethodInfo? _tryGet;
    private static ConstructorInfo? _untranslatedCtor;
    private static FieldInfo? _whitespaceBetweenWords;
    private static FieldInfo? _templateAllNumberAway;
    private static FieldInfo? _autoTranslationsFilePath;
    private static FieldInfo? _language;
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
    // 驯服机翻端点：Settings.ServiceEndpoint/FallbackServiceEndpoint/SetEndpoint + AutoTranslationPlugin.TranslationManager 上的 CurrentEndpoint/FallbackEndpoint
    private static FieldInfo? _serviceEndpoint;
    private static FieldInfo? _fallbackServiceEndpoint;
    private static MethodInfo? _setEndpoint;
    private static FieldInfo? _translationManager;
    private static PropertyInfo? _currentEndpoint;
    private static PropertyInfo? _fallbackEndpoint;
    // 内置静态词典：Settings.UseStaticTranslations + TextCache._staticTranslations + IniFile 写入通道
    private static FieldInfo? _useStaticTranslations;
    private static FieldInfo? _staticTranslations;
    private static PropertyInfo? _environmentPreferences;
    private static MethodInfo? _environmentSaveConfig;

    internal static bool Installed => Chainloader.PluginInfos.ContainsKey(Guid);

    private static void Resolve()
    {
        if (_resolved)
        {
            return;
        }

        _resolved = true;
        if (!Installed)
        {
            return;
        }

        try
        {
            Type? type = AccessTools.TypeByName(PluginTypeName);
            if (type == null)
            {
                Plugin.Log.LogWarning("[翻译导出] XUnity 在场但找不到 AutoTranslationPlugin 类型，原文/译文切换与重载不可用。");
                return;
            }

            _current = AccessTools.Field(type, "Current");
            _translatedMode = AccessTools.Field(type, "_isInTranslatedMode");
            _toggle = AccessTools.Method(type, "ToggleTranslation", Type.EmptyTypes);
            _reload = AccessTools.Method(type, "ReloadTranslations", Type.EmptyTypes);
            _translationManager = AccessTools.Field(type, "TranslationManager");
            if (_current == null || _toggle == null || _reload == null)
            {
                Plugin.Log.LogWarning($"[翻译导出] XUnity 结构变了（Current={_current != null}, Toggle={_toggle != null}, Reload={_reload != null}），相关按钮不可用。");
            }

            _textCache = AccessTools.Field(type, "TextCache");
            Type? cacheType = AccessTools.TypeByName(CacheTypeName);
            Type? untranslatedType = AccessTools.TypeByName(UntranslatedTypeName);
            Type? settingsType = AccessTools.TypeByName(SettingsTypeName);
            if (cacheType != null && untranslatedType != null)
            {
                _tryGet = AccessTools.Method(cacheType, "TryGetTranslation", new[] { untranslatedType, typeof(bool), typeof(bool), typeof(int), typeof(string).MakeByRefType() });
                _untranslatedCtor = AccessTools.Constructor(untranslatedType, new[] { typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool) });
                _staticTranslations = AccessTools.Field(cacheType, "_staticTranslations");
            }

            if (settingsType != null)
            {
                _whitespaceBetweenWords = AccessTools.Field(settingsType, "FromLanguageUsesWhitespaceBetweenWords");
                _templateAllNumberAway = AccessTools.Field(settingsType, "TemplateAllNumberAway");
                _autoTranslationsFilePath = AccessTools.Field(settingsType, "AutoTranslationsFilePath");
                _language = AccessTools.Field(settingsType, "Language");
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
                _serviceEndpoint = AccessTools.Field(settingsType, "ServiceEndpoint");
                _fallbackServiceEndpoint = AccessTools.Field(settingsType, "FallbackServiceEndpoint");
                _setEndpoint = AccessTools.Method(settingsType, "SetEndpoint", new[] { typeof(string) });
                _useStaticTranslations = AccessTools.Field(settingsType, "UseStaticTranslations");
                Type? managerType = AccessTools.TypeByName("XUnity.AutoTranslator.Plugin.Core.TranslationManager");
                if (managerType != null)
                {
                    _currentEndpoint = AccessTools.Property(managerType, "CurrentEndpoint");
                    _fallbackEndpoint = AccessTools.Property(managerType, "FallbackEndpoint");
                }
                Type? environmentType = AccessTools.TypeByName("XUnity.AutoTranslator.Plugin.Core.PluginEnvironment");
                _environmentCurrent = environmentType != null ? AccessTools.Field(environmentType, "Current") : null;
                Type? environmentInterface = AccessTools.TypeByName("XUnity.AutoTranslator.Plugin.Core.IPluginEnvironment");
                _environmentTranslationPath = environmentInterface != null ? AccessTools.Property(environmentInterface, "TranslationPath") : null;
                _environmentPreferences = environmentInterface != null ? AccessTools.Property(environmentInterface, "Preferences") : null;
                _environmentSaveConfig = environmentInterface != null ? AccessTools.Method(environmentInterface, "SaveConfig", Type.EmptyTypes) : null;
                Type? languageHelper = AccessTools.TypeByName("XUnity.AutoTranslator.Plugin.Core.Utilities.LanguageHelper");
                _requiresWhitespace = languageHelper != null ? AccessTools.Method(languageHelper, "RequiresWhitespaceUponLineMerging", new[] { typeof(string) }) : null;
            }

            if (_textCache == null || _tryGet == null || _untranslatedCtor == null)
            {
                Plugin.Log.LogWarning($"[翻译导出] XUnity 词典查询接口对不上（TextCache={_textCache != null}, TryGetTranslation={_tryGet != null}, UntranslatedText={_untranslatedCtor != null}），查漏翻退回读文件。");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[翻译导出] 对接 XUnity 失败: {ex.Message}");
        }
    }

    private static object? Instance
    {
        get
        {
            Resolve();
            try
            {
                return _current?.GetValue(null);
            }
            catch
            {
                return null;
            }
        }
    }

    internal static bool CanToggle => Instance != null && _toggle != null;

    /// <summary>XUnity 的目标语言（ini 的 Language=，启动时读一次）；拿不到为 null。</summary>
    internal static string? TargetLanguage => ReadStatic(_language);

    /// <summary>XUnity 当前语言的词典目录（Directory= 里 {Lang} 已代入）；拿不到为 null。</summary>
    internal static string? TranslationsPath => ReadStatic(_translationsPath);

    /// <summary>能不能在运行时改目标语言（所有要重算的字段都找到了）。</summary>
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
        return path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
    }

    private static string? ReadStatic(FieldInfo? field)
    {
        Resolve();
        try
        {
            return field?.GetValue(null) as string;
        }
        catch
        {
            return null;
        }
    }

    internal static bool CanReload => Instance != null && _reload != null;

    /// <summary>当前是否在显示译文；读不到时按「是」。</summary>
    internal static bool IsTranslatedMode
    {
        get
        {
            object? instance = Instance;
            if (instance == null || _translatedMode == null)
            {
                return true;
            }

            try
            {
                return _translatedMode.GetValue(instance) is bool value && value;
            }
            catch
            {
                return true;
            }
        }
    }

    internal static bool ToggleTranslation()
    {
        return Invoke(_toggle, "切换原文/译文");
    }

    internal static bool ReloadTranslations()
    {
        EnsureMainTranslationFile();
        return Invoke(_reload, "重载翻译");
    }

    /// <summary>
    /// XUnity 的 ReloadTranslations 先对主词典 <c>_AutoGeneratedTranslations.txt</c> 做 Prune，文件不存在就抛 FileNotFound、整次重载中断
    /// （从未开过机翻的存档就没有这个文件）。重载前补一个空文件即可。
    /// </summary>
    private static void EnsureMainTranslationFile()
    {
        try
        {
            if (_autoTranslationsFilePath?.GetValue(null) is not string path || path.Length == 0 || File.Exists(path))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty);
            Plugin.Log.LogInfo($"[翻译导出] XUnity 主词典不存在，已补空文件以便重载: {path}");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[翻译导出] 补建 XUnity 主词典失败: {ex.Message}");
        }
    }

    /// <summary>能否直接问 XUnity 内存词典「这句有没有译文」。</summary>
    internal static bool CanQuery => Instance != null && _textCache != null && _tryGet != null && _untranslatedCtor != null;

    /// <summary>
    /// 用 XUnity 自己的 <c>TextTranslationCache.TryGetTranslation</c> 判定：与游戏里真正替换文字的那条路径一致，
    /// 精确条目、r:/sr: 正则、_Substitutions 都算在内。查不了（未装 / 结构不符）返回 null。
    /// </summary>
    internal static bool? IsTranslated(string text)
    {
        object? instance = Instance;
        if (instance == null || _textCache == null || _tryGet == null || _untranslatedCtor == null)
        {
            return null;
        }

        try
        {
            object? cache = _textCache.GetValue(instance);
            if (cache == null)
            {
                return null;
            }

            bool whitespace = _whitespaceBetweenWords?.GetValue(null) is bool w && w;
            bool template = _templateAllNumberAway?.GetValue(null) is bool t && t;
            // 参数顺序同 AutoTranslationPlugin 内部构造：originalText, isFromSpammingComponent, removeInternalWhitespace, whitespaceBetweenWords, enableTemplating, templateAllNumbersAway
            object key = _untranslatedCtor.Invoke(new object[] { text, false, false, whitespace, true, template });
            object?[] args = { key, true, false, ScopeNone, null };
            bool hit = _tryGet.Invoke(cache, args) is bool b && b;
            return hit && args[4] is string value && value.Length > 0;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[翻译导出] 查询 XUnity 词典失败，退回读文件: {ex.InnerException?.Message ?? ex.Message}");
            _tryGet = null; // 只报一次
            return null;
        }
    }

    private static bool Invoke(MethodInfo? method, string what)
    {
        object? instance = Instance;
        if (instance == null || method == null)
        {
            return false;
        }

        try
        {
            method.Invoke(instance, null);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[翻译导出] XUnity {what}失败: {ex.InnerException?.Message ?? ex.Message}");
            return false;
        }
    }

    // ── 驯服机翻端点 ────────────────────────────────────────────────

    /// <summary>
    /// 把 XUnity 的机翻端点清空：出厂默认 <c>Endpoint=GoogleTranslateV2</c> 会把每个钩到的文本丢去机翻
    /// （「Field of view」翻成「小野」那类事故）。三层下手：
    /// prefs + 静态字段走 <c>Settings.SetEndpoint("")</c>（它顺带把 FallbackEndpoint 也清了）；
    /// <c>TranslationManager.CurrentEndpoint / FallbackEndpoint</c> 置空 → 本次会话立即停机翻；
    /// ini 文件再兜底改两行 → 下次启动生效。
    /// 顺带清掉 <c>Translation\*\Text\_AutoGeneratedTranslations*.txt</c>——机翻结果缓存本质是词典，
    /// 端点清空后它照样命中，不清它残留的机翻永远挂着；清空前原文件留一份 .bak（用户可能在里面手改过译文）。
    /// 返回做了什么的一行摘要；没事可做或全失败时返回 null。
    /// </summary>
    internal static string? TameEndpoint()
    {
        if (!Installed)
        {
            return null;
        }

        Resolve();
        List<string> notes = new List<string>();
        try
        {
            string? ep = ReadStatic(_serviceEndpoint);
            string? fb = ReadStatic(_fallbackServiceEndpoint);
            if (!string.IsNullOrEmpty(ep) || !string.IsNullOrEmpty(fb))
            {
                string old = string.Join(" / ", new[] { ep, fb }.Where(s => !string.IsNullOrEmpty(s)));

                try
                {
                    _setEndpoint?.Invoke(null, new object?[] { string.Empty });
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[翻译] XUnity SetEndpoint 调用失败: {ex.InnerException?.Message ?? ex.Message}");
                }

                object? instance = Instance;
                object? manager = instance != null && _translationManager != null ? _translationManager.GetValue(instance) : null;
                if (manager != null)
                {
                    try
                    {
                        _currentEndpoint?.SetValue(manager, null);
                        _fallbackEndpoint?.SetValue(manager, null);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogWarning($"[翻译] 摘掉 XUnity 当前端点失败: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }

                notes.Add($"端点 {old} 已清空");
            }

            // 内置静态词典：默认只在 FromLanguage=ja && Language=en 时装载，但载入后对所有目标语言生效
            // （「置顶→Fixed」之类），命中后还会回填进自动缓存词典。字段 + prefs + 词典本体三层一起关。
            if (_useStaticTranslations?.GetValue(null) is bool useStatics && useStatics)
            {
                try
                {
                    _useStaticTranslations.SetValue(null, false);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[翻译] 写 UseStaticTranslations 字段失败: {ex.InnerException?.Message ?? ex.Message}");
                }

                try
                {
                    SetIniPreference("Behaviour", "UseStaticTranslations", "False");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[翻译] 写 UseStaticTranslations 偏好失败: {ex.InnerException?.Message ?? ex.Message}");
                }

                try
                {
                    object? instance = Instance;
                    object? cache = instance != null ? _textCache?.GetValue(instance) : null;
                    if (cache != null && _staticTranslations?.GetValue(cache) is IDictionary statics && statics.Count > 0)
                    {
                        statics.Clear();
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[翻译] 清 XUnity 静态词典失败: {ex.InnerException?.Message ?? ex.Message}");
                }

                notes.Add("内置静态词典已停用");
            }

            ClearIniEndpoints(); // ini 兜底：端点行 + UseStaticTranslations=False，无条件过一遍
            int swept = ClearAutoGeneratedCaches();
            if (swept > 0)
            {
                notes.Add($"机翻缓存词典 {swept} 份已清空（原文件备份为 .bak）");
            }

            return notes.Count > 0 ? string.Join("，", notes) : null;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[翻译] 驯服 XUnity 端点失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 清空所有语言目录下的机翻结果缓存（<c>_AutoGeneratedTranslations*.txt</c>）。
    /// 目录根优先用反射到的真实路径，拿不到就把 config\Translation 与其同级 BepInEx\Translation 都扫一遍。
    /// 非空文件清空前先留 <c>.bak</c>（只留第一次的，不覆盖）：XUnity 只读 *.txt，它自己 Prune 时也用这个后缀，不会被当词典加载。
    /// 已经是空的文件不算数。返回真正清空的文件数。
    /// </summary>
    private static int ClearAutoGeneratedCaches()
    {
        int swept = 0;
        HashSet<string> roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (TranslationRoot is { } root)
        {
            roots.Add(root);
        }

        roots.Add(Path.Combine(Paths.ConfigPath, "Translation"));
        string? bep = Path.GetDirectoryName(Paths.ConfigPath);
        if (bep != null)
        {
            roots.Add(Path.Combine(bep, "Translation"));
        }

        foreach (string r in roots)
        {
            if (!Directory.Exists(r))
            {
                continue;
            }

            foreach (string file in Directory.GetFiles(r, "_AutoGeneratedTranslations*.txt", SearchOption.AllDirectories))
            {
                try
                {
                    if (new FileInfo(file).Length == 0)
                    {
                        continue;
                    }

                    string backup = file + ".bak";
                    if (!File.Exists(backup))
                    {
                        File.Copy(file, backup);
                    }

                    File.WriteAllText(file, "", new UTF8Encoding(false));
                    swept++;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[翻译] 清空 {file} 失败: {ex.Message}");
                }
            }
        }

        return swept;
    }

    /// <summary>经 XUnity 自己的 IniFile 通道写一个偏好键（Preferences["section"]["key"].Value），随后 SaveConfig 落盘。</summary>
    private static void SetIniPreference(string section, string key, string value)
    {
        object? env = _environmentCurrent?.GetValue(null);
        object? prefs = env != null ? _environmentPreferences?.GetValue(env) : null;
        if (prefs == null)
        {
            return;
        }

        object? sectionObj = prefs.GetType().GetProperty("Item")?.GetValue(prefs, new object[] { section });
        object? keyObj = sectionObj?.GetType().GetProperty("Item")?.GetValue(sectionObj, new object[] { key });
        keyObj?.GetType().GetProperty("Value")?.SetValue(keyObj, value);
        if (env != null)
        {
            _environmentSaveConfig?.Invoke(env, null);
        }
    }

    /// <summary>ini 兜底：[Service] 的 Endpoint= / FallbackEndpoint= 清空，[Behaviour] 的 UseStaticTranslations= 写 False。</summary>
    private static void ClearIniEndpoints()
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
            string[] lines = text.Split('\n');
            string section = "";
            bool changed = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                if (line.StartsWith("[", StringComparison.Ordinal))
                {
                    section = line.Trim();
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq < 0)
                {
                    continue;
                }

                string? want = null;
                if (section.Equals("[Service]", StringComparison.OrdinalIgnoreCase) &&
                    (line.StartsWith("Endpoint=", StringComparison.Ordinal) || line.StartsWith("FallbackEndpoint=", StringComparison.Ordinal)))
                {
                    want = line.Substring(0, eq + 1); // 清空
                }
                else if (section.Equals("[Behaviour]", StringComparison.OrdinalIgnoreCase) &&
                         line.StartsWith("UseStaticTranslations=", StringComparison.Ordinal) &&
                         !line.Equals("UseStaticTranslations=False", StringComparison.OrdinalIgnoreCase))
                {
                    want = "UseStaticTranslations=False";
                }

                if (want != null && want != line)
                {
                    lines[i] = want + (lines[i].EndsWith("\r") ? "\r" : "");
                    changed = true;
                }
            }

            if (changed)
            {
                File.WriteAllText(ini, string.Join("\n", lines), new UTF8Encoding(bom));
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[翻译] 改 AutoTranslatorConfig.ini 失败: {ex.Message}");
        }
    }
}
