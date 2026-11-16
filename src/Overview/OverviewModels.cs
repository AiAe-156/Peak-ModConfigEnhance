#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace ModConfigEnhance.Overview;

/// <summary>
/// 1.1.0（MCE）：模组概览的个人状态，存 <c>config\ModConfigEnhance\overview.json</c>（<see cref="OverviewRepository"/> 读写）。
/// 照抄游戏内模组管理器同名类后裁剪：只留显示用分组、折叠与「隐藏已停用」。
/// 去掉：整合包（pack / packOverridden / packNameOverride）、更新许可与自动检查（MCE 不联网）、标签筛选（modTagFilter / hotkeyTagFilter，
/// 依赖商店标签）、方案里的快捷键预设与手柄组合键预设、整合包冲突忽略。
/// 快捷键页的 collapsedHotkeyMods / ignoredHotkeyConflicts 仍在 <c>hotkeys.json</c>（<see cref="HotkeyPageState"/>，快捷键页第 2 章已定，不迁）。
/// </summary>
[Serializable]
internal sealed class OverviewState
{
    public int schemaVersion = 1;
    public bool hideDisabled = false; // 显式初值免 CS0649：第 7 章工具行「隐藏已停用」赋值

    public List<OverviewGroup> groups = new();
    public List<OverviewModRule> mods = new();
    public List<string> customColors = new();
    public List<string> collapsedGroups = new();
}

[Serializable]
internal sealed class OverviewGroup
{
    public string id = "";
    public string name = "";
    public string color = "#7CC7E6";
    public string description = "";
    /// <summary>Order among siblings (same <see cref="parentId"/>).</summary>
    public int order;
    /// <summary>2.3.0: the group this one hangs under; "" = top level. At most <see cref="GroupTree.MaxDepth"/> levels;
    /// a missing parent, cycle or over-deep chain is read as top level. See <see cref="GroupTree"/>.</summary>
    public string parentId = "";
}

/// <summary>1.1.0（MCE）：只留分组归属与组内顺序。别名 / 版本标签（整合包作者功能）、简介覆盖（MCE 的简介写语言文件）、
/// 更新许可与来源指定（MCE 不联网）都去掉。</summary>
[Serializable]
internal sealed class OverviewModRule
{
    public string id = "";
    public string groupId = "";
    public int order = 0; // 显式初值免 CS0649：第 8 章拖动排序赋值
}

internal sealed partial class OverviewMod
{
    public string Id = "";
    public string Guid = "";
    public string Name = "";
    public string RawName = "";
    public string RawDescription = "";
    public string SettingDisplayName = "";
    public string PluginVersion = "";
    public string PackageVersion = "";
    /// <summary>2.4.4: lower-version manifest.json copies of this package inside its own folder (never used for the version).</summary>
    public List<string> StaleManifests = new();
    public string Author = "";
    public string Description = "";
    public string Location = "";
    public string PackageRoot = "";
    public string PackageName = "";
    public string PackageNamespace = "";
    // 1.1.0（MCE）：去掉 SourceNamespace / SourceName / SourceConfirmed / LatestVersion / UpdateNotes / CheckMessage / HasUpdate /
    // LastCheckedUtc（来源识别与更新检查，MCE 不联网）。包身份只用本地 manifest 读出的 PackageNamespace / PackageName。
    public bool Loaded;
    /// <summary>文件现状：插件 DLL 是 .dll（未被停用）。MCE 只读显示，不提供启停。</summary>
    public bool TargetEnabled = true;
    /// <summary>1.1.0（MCE）：MCE 不启停任何模组，这两项只用于显示「为什么这一行特殊」（受保护的基础库、被其他工具停用、文件冲突）。</summary>
    public bool CanToggle = true;
    public string ToggleReason = "";
    internal List<OverviewPluginCopy> PluginCopies = new();
    // Every row (including this one) carrying the same GUID in another package folder.
    internal List<OverviewMod> CopyRows = new();
    internal string CopyKey = "";
    internal bool DuplicateCopy => CopyRows.Count > 1;
    /// <summary>2.3.0: the mod whose folder this one is nested in (nearest, packaging layout excluded); null at top level.</summary>
    internal OverviewMod? Outer;
    /// <summary>2.3.4: a manifest package with no assembly at all (bundles, textures, data). Shown as a row with its own
    /// badge; it is neither disabled nor missing.</summary>
    internal bool ResourcePack;
    /// <summary>2.3.4: a plugin DLL lying directly in plugins/ rather than in a package folder.</summary>
    internal bool LooseFile;
    /// <summary>Mods nested directly in this one's folder.</summary>
    internal List<OverviewMod> Inner = new();
    public List<string> PluginGuids = new();
    public List<string> AssemblyPaths = new();
    public OverviewModRule Rule = new();
    // 1.1.0（MCE）：别名、版本标签、简介覆盖都不要了（见 OverviewModRule），三个显示属性直接取原值。
    public string DisplayName => Name;
    public string DisplayVersion => string.IsNullOrEmpty(PackageVersion) ? PluginVersion : PackageVersion;
    public string DisplayDescription => Description;
}

// 1.1.0（MCE）：IGMM 这里还有 OverviewToggleResult（启停结果），MCE 不启停模组，不搬。

/// <summary>
/// 1.1.0（MCE）：快捷键页的个人状态（忽略的冲突、收起的模组），存 <c>config\ModConfigEnhance\hotkeys.json</c>。
/// 只是界面记忆，读坏了就当空的，不影响任何模组配置。（IGMM 把这两项放在 OverviewState 里；MCE 快捷键页第 2 章起独立成文件，不迁。）
/// </summary>
internal sealed class HotkeyPageState
{
    public List<OverviewIgnoredHotkeyConflict> ignoredHotkeyConflicts = new();
    public List<string> collapsedHotkeyMods = new();

    private static HotkeyPageState? _current;
    private static string FilePath => Path.Combine(Loc.RootDirectory, "hotkeys.json");
    internal static HotkeyPageState Current
    {
        get
        {
            if (_current != null) return _current;
            try
            {
                if (File.Exists(FilePath)) _current = JsonConvert.DeserializeObject<HotkeyPageState>(File.ReadAllText(FilePath));
            }
            catch (Exception ex) { Plugin.Log.LogWarning("[快捷键] 读取 hotkeys.json 失败，按空状态处理：" + ex.Message); }
            _current ??= new HotkeyPageState();
            _current.ignoredHotkeyConflicts ??= new();
            _current.collapsedHotkeyMods ??= new();
            return _current;
        }
    }

    internal static void Save()
    {
        string temp = FilePath + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(temp, JsonConvert.SerializeObject(Current, Formatting.Indented));
        if (File.Exists(FilePath)) File.Replace(temp, FilePath, null); else File.Move(temp, FilePath);
    }
}
