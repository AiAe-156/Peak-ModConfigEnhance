using System;
using System.Collections.Generic;

namespace BepInEx
{
    internal static class Paths { internal static string ConfigPath = ""; }
}

namespace PEAKLib.UI
{
    internal static class MenuAPI
    {
        internal static LocalizationBuilder CreateLocalization(string key) => new LocalizationBuilder(key);
    }

    internal sealed class LocalizationBuilder
    {
        private readonly string _key;
        internal LocalizationBuilder(string key) { _key = key.ToUpperInvariant(); }
        internal LocalizationBuilder AddLocalization(string value, LocalizedText.Language language)
        {
            if (!LocalizedText.mainTable.TryGetValue(_key, out List<string>? row))
            {
                row = new List<string> { "", "", "" };
                LocalizedText.mainTable[_key] = row;
            }

            row[(int)language] = value;
            return this;
        }
    }
}

namespace UnityEngine
{
    internal enum FindObjectsInactive { Include }
    internal enum FindObjectsSortMode { None }
    internal class Object
    {
        internal static T[] FindObjectsByType<T>(FindObjectsInactive inactive, FindObjectsSortMode sort) => Array.Empty<T>();
    }
}

internal sealed class LocalizedText
{
    internal enum Language { English, SimplifiedChinese, TraditionalChinese }
    internal static Language CURRENT_LANGUAGE = Language.SimplifiedChinese;
    internal static readonly Dictionary<string, List<string>> mainTable = new(StringComparer.Ordinal);
    internal string index = "";
    internal void RefreshText() { }
    internal static string GetText(string key, bool printDebug) => "";
}

namespace ModConfigEnhance
{
    internal sealed class Setting<T> { internal T Value = default!; }
    internal sealed class Logger
    {
        internal void LogInfo(object value) { }
        internal void LogWarning(object value) { }
        internal void LogError(object value) { }
    }

    internal static class Plugin
    {
        internal const string PluginVersion = "test";
        internal const string DescTreeSidebar = "tree";
        internal const string DescDescriptionPanel = "description";
        internal const string DescUiLocalization = "localization";
        internal const string DescTranslationTools = "tools";
        internal const string DescLanguageRowOnly = "row";
        internal const string DescSwitchXUnity = "switch";
        internal const string DescTameXUnity = "tame";
        internal static readonly Setting<string> LanguageFile = new();
        internal static readonly Setting<bool> SwitchXUnity = new();
        internal static readonly Logger Log = new();
    }

    internal sealed class ModConfigDescriptionPanel
    {
        internal static ModConfigDescriptionPanel? Current { get; set; }
        internal void ShowNotice(string message, bool warning = false) { }
    }

    internal static class ModConfigLocalizationPatch { internal static void RefreshStaticTexts() { } }
    internal static class LanguageReadme { internal static string For(Loc.UiLang language) => "{VERSION}"; }

    internal static class XUnityBridge
    {
        internal static bool Installed => true;
        internal static bool CanSetLanguage => true;
        internal static string? TranslationRoot { get; private set; }
        internal static string? TargetLanguage { get; private set; }
        internal static string? TranslationsPath => TranslationRoot == null || TargetLanguage == null
            ? null
            : System.IO.Path.Combine(TranslationRoot, TargetLanguage, "Text");
        internal static int SetLanguageCalls { get; private set; }
        internal static int ReloadCalls { get; private set; }

        internal static void Reset(string root, string language)
        {
            TranslationRoot = root;
            TargetLanguage = language;
            SetLanguageCalls = 0;
            ReloadCalls = 0;
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(root, "zh-cn", "Text"));
        }

        internal static bool SetLanguage(string code)
        {
            SetLanguageCalls++;
            TargetLanguage = code;
            return true;
        }

        internal static void ReloadTranslations() { ReloadCalls++; }
    }
}
