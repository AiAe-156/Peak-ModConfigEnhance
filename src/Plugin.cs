#nullable enable
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace ModConfigEnhance;

/// <summary>
/// ModConfig Enhance —— 给 PEAKLib.ModConfig 的模组设置页加：树形侧栏、选项说明面板、界面本地化（语言文件可选）、
/// 翻译导出 / 补漏翻 / 翻译自管（不依赖 XUnity）、手柄支持。
/// </summary>
[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(ModConfigSupport.Guid, BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency(TimeThemeBridge.Guid, BepInDependency.DependencyFlags.SoftDependency)]
// 1.1.0：只为加载顺序——装了 IGMM 时 BepInEx 先加载它，Awake 开头就能在 PluginInfos 里查到（见 Awake）。
[BepInDependency(InGameModManagerGuid, BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.aiae.modconfigenhance";
    public const string PluginName = "ModConfig Enhance";
    public const string PluginVersion = "1.1.0";
    /// <summary>给人看的版本（游戏内说明、导出文件头、日志）。开发期间带字母后缀，确认上线时去掉（见 MODs/AGENTS.md「版本迭代规则」）。
    /// BepInPlugin 只能用纯数字：BepInEx 解析不了字母后缀，会把版本读成空值导致插件不加载（2026-11-15 实测）。</summary>
    public const string DisplayVersion = "1.1.0"; // 2026-11-16 上线：去掉开发后缀 .a

    /// <summary>完整版「游戏内模组管理器」（In-Game Mod Manager，IGMM）的 GUID（取自 IGMM Plugin.cs → Updating.ManagerIdentity.Guid）。
    /// 它自带同样的设置页增强与模组概览，两者同开会重复改同一个页面。1.1.0（用户 2026-11-15 定）：检测到 IGMM 已加载时 MCE 整个不加载。</summary>
    internal const string InGameModManagerGuid = "com.aiae.ingamemodmanager";
    /// <summary>
    /// IGMM 是否已加载。顺序由上面的 SoftDependency 保证：装了 IGMM 时 BepInEx 先加载它（加进 PluginInfos 并跑完 Awake）再加载本模组；
    /// IGMM 加载失败时 BepInEx 会把它从 PluginInfos 移除，所以「在表里、有实例」就是真的在跑。不用等 Chainloader 结束后再查一次。
    /// </summary>
    internal static bool InGameModManagerLoaded =>
        BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(InGameModManagerGuid, out var igmm) && igmm?.Instance != null;

    internal static ManualLogSource Log { get; private set; } = null!;

    // cfg 说明原文（Loc.ConfigTerms 用同一份字符串做译文的键）
    internal const string DescTreeSidebar = "A vertical mod list on the left; the selected mod's sections expand beneath it, options on the right. Restart to apply.";
    internal const string DescDescriptionPanel = "A panel at the top right shows the hovered option's description, default value and allowed range or choices. Requires the tree sidebar. Restart to apply.";
    internal const string DescUiLocalization = "Localize ModConfig's own UI (Search, Default, Clear, filters …) and this mod's buttons and notices. Texts come from language files in config\\ModConfigEnhance\\language\\, picked in the language row above the mod list. Restart to apply.";
    internal const string DescHideLocalizationButtons = "Above the mod list, hide the export and translation-tool button rows; only the language dropdown + Refresh stay, the list moves up and the Export column hides (ticks remembered). Applies at once.";

    internal static ConfigEntry<bool> EnableTreeLayout = null!;
    internal static ConfigEntry<bool> EnableDescriptionPanel = null!;
    internal static ConfigEntry<ModConfigLook.ThemeMode> ColourTheme = null!;
    internal const string DescColourTheme = "Colour theme of the mod settings page. Blue follows the game's settings menu; Purple is the night style; PurpleAtNight switches to purple at night when TimeTheme is installed (only its day/night state is read). Applies at once.";
    internal static ConfigEntry<bool> EnableLocalization = null!;
    internal static ConfigEntry<bool> HideLocalizationButtons = null!;

    /// <summary>界面维护的记忆（Hidden）：选中的语言文件（空 = 跟随游戏）、置顶、不导出、仅导出已选。</summary>
    internal static ConfigEntry<string> LanguageFile = null!;
    internal static ConfigEntry<string> PinnedMods = null!;
    internal static ConfigEntry<string> ExportExcludedMods = null!;
    internal static ConfigEntry<bool> ExportSelectedOnly = null!;
    internal static ConfigEntry<SidebarSortMode> SidebarSort = null!;

    /// <summary>本模组在 ModConfig 列表里的行名 = 它的 FixNaming(插件名)（1.8.1 ModConfigPlugin.cs）。强制置顶与行名上色按它认行，改名时不用同步。</summary>
    internal static readonly string SelfRowName = FixNaming(PluginName);

    private void Awake()
    {
        Log = Logger;
        // 1.1.0：与 IGMM 同时安装时整个不加载（不绑 cfg、不写说明文件、不挂任何补丁），避免两个模组重复修改设置页、出现两个概览入口。
        if (InGameModManagerLoaded)
        {
            Log.LogWarning("[共存] 检测到 In-Game Mod Manager（游戏内模组管理器），本模组不加载，避免重复修改设置页。"
                + " / In-Game Mod Manager detected: ModConfig Enhance will not load, to avoid modifying the settings page twice.");
            return;
        }
        Diagnostics.RuntimeLogCapture.Start(); // 1.1.0：日志窗口的带时间戳运行日志（同 IGMM 位置）；不加 OnDestroy，监听随进程结束
        BindConfig();
        Loc.EnsureBuiltInFiles();
        NormalizeSelfPin();

        Harmony harmony = new(PluginGuid);
        // 1.0.3：小数显示是数据正确性（PEAK 3.1 下手打 1.25 会存错），与树形侧栏无关，独立挂载
        ModFloatDisplayPatch.Apply(harmony);
        Log.LogInfo("[小数显示] " + ModFloatDisplayPatch.State);
        SettingsTabActivationPatch.Install(harmony);
        // 1.1.0：手柄组合键（自由组合）替模组按下它的键盘快捷键。自己挂读键 Postfix（Input.GetKey* 与 ButtonControl 三个属性）
        // 和 InputAction 屏蔽，不依赖树形侧栏开关；打字保护（TypingGuard，挂同样读键点的 Prefix）聚焦时它不注入。
        GamepadCombos.Install(harmony);
        // 1.1.0：「模组概览」独立页面——游戏设置菜单里 ModConfig「模组设置」下方的入口（快捷键页搬迁第 3 章），不依赖树形侧栏开关。
        try { Overview.OverviewController.Register(harmony); }
        catch (Exception ex) { Log.LogError("[模组概览] 注册入口失败：" + ex); }
        if (EnableTreeLayout.Value)
        {
            ModConfigTreeLayoutPatch.TryApply(harmony);
        }

        if (EnableLocalization.Value)
        {
            ModConfigLocalizationPatch.TryApply(harmony);
        }

        Log.LogInfo($"[{PluginName} v{DisplayVersion}] 已加载。");
        Log.LogInfo($"  树形侧栏: {(EnableTreeLayout.Value ? ModConfigTreeLayoutPatch.State : "关")}" +
            $" / 界面本地化: {(EnableLocalization.Value ? ModConfigLocalizationPatch.State : "关")}" +
            $" / 说明面板: {(EnableDescriptionPanel.Value ? "开" : "关")}" +
            $" / 工具行: {(HideLocalizationButtons.Value ? "仅语言行" : "全开")}" +
            $" / 语言: {(string.IsNullOrEmpty(LanguageFile.Value) ? "跟随游戏" : LanguageFile.Value)}");
    }

    private void BindConfig()
    {
        const string secLayout = "1. Layout";
        EnableTreeLayout = Config.Bind(secLayout, "Tree sidebar", true, DescTreeSidebar);
        EnableDescriptionPanel = Config.Bind(secLayout, "Description panel", true, DescDescriptionPanel);
        ColourTheme = Config.Bind(secLayout, "Colour theme", ModConfigLook.ThemeMode.Blue, DescColourTheme);

        const string secLoc = "2. Localization";
        EnableLocalization = Config.Bind(secLoc, "UI localization", true, DescUiLocalization);
        HideLocalizationButtons = Config.Bind(secLoc, "Hide localization buttons", false, DescHideLocalizationButtons);
        // 翻译改由本模组自己显示后，两个 XUnity 联动开关已删除。ConfigFile.Remove 只删已绑定的项；
        // 没绑定的旧键在 BepInEx 的私有 OrphanedEntries 里，存盘时会原样写回，要从那里删。
        RemoveOrphans(secLoc, "Switch XUnity language with the language file", "Disable XUnity machine translation");

        const string secState = "3. State";
        LanguageFile = Config.Bind(secState, "Language file", "",
            new ConfigDescription("Selected language file name without .txt; empty = follow the game language. Maintained by the UI.", null, "Hidden"));
        PinnedMods = Config.Bind(secState, "Pinned mods", "",
            new ConfigDescription("Mods ticked in the Pin column (ModConfig's display names, | separated). Maintained by the UI.", null, "Hidden"));
        ExportExcludedMods = Config.Bind(secState, "Excluded from export", "",
            new ConfigDescription("Mods unticked in the Export column (ModConfig's display names, | separated). Maintained by the UI.", null, "Hidden"));
        SidebarSort = Config.Bind(secState, "Sort mode", SidebarSortMode.Default,
            new ConfigDescription("Sidebar mod-row sort mode, set by the sort dropdown.", null, "Hidden"));
        ExportSelectedOnly = Config.Bind(secState, "Export selected only", false,
            new ConfigDescription("State of the \"Selected only\" toggle. Maintained by the UI.", null, "Hidden"));
    }

    private void RemoveOrphans(string section, params string[] keys)
    {
        try
        {
            var orphans = HarmonyLib.AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(Config) as System.Collections.Generic.Dictionary<ConfigDefinition, string>;
            bool removed = false;
            foreach (string key in keys)
            {
                var definition = new ConfigDefinition(section, key);
                removed |= Config.Remove(definition);
                if (orphans != null) removed |= orphans.Remove(definition);
            }

            if (removed) Config.Save();
        }
        catch (System.Exception ex)
        {
            Log.LogWarning("[配置] 清理旧的 XUnity 选项失败: " + ex.Message);
        }
    }

    /// <summary>
    /// 置顶集合里不再需要自身行名——强制最顶是结构行为，不再吃「置顶」列的勾选。
    /// 把曾经写进去的那条清掉，保持 cfg 干净。
    /// </summary>
    private static void NormalizeSelfPin()
    {
        string current = PinnedMods.Value ?? "";
        if (current.Length == 0)
        {
            return;
        }

        List<string> kept = new List<string>();
        bool removed = false;
        foreach (string part in current.Split('|'))
        {
            string name = part.Trim();
            if (name.Length == 0)
            {
                continue;
            }

            if (string.Equals(name, SelfRowName, StringComparison.Ordinal))
            {
                removed = true;
                continue;
            }

            kept.Add(name);
        }

        if (removed)
        {
            PinnedMods.Value = string.Join("|", kept);
            Log.LogInfo($"[{PluginName}] 已从置顶集合移除自身行名（自身行现在固定最顶）。");
        }
    }

    internal static string FixNaming(string input)
    {
        input = Regex.Replace(input, "([a-z])([A-Z])", "$1 $2");
        input = Regex.Replace(input, "([A-Z])([A-Z][a-z])", "$1 $2");
        input = Regex.Replace(input, "\\s+", " ");
        input = Regex.Replace(input, "([A-Z]\\.)\\s([A-Z]\\.)", "$1$2");
        return input.Trim();
    }

    /// <summary>侧栏模组行排序方式（排序下拉选；置顶行始终在前不受排序影响，本模组行恒第二）。</summary>
    internal enum SidebarSortMode
    {
        /// <summary>ModConfig 原生顺序（≈插件注册/加载顺序）</summary>
        Default,
        /// <summary>加载顺序倒过来</summary>
        LoadReversed,
        /// <summary>按当前显示名正序（zh 排序规则，中文按拼音、英文按字母）</summary>
        Name,
        /// <summary>按当前显示名倒序</summary>
        NameDesc,
    }
}
