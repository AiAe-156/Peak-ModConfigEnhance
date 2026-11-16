#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using UnityEngine;

namespace ModConfigEnhance.Overview;

/// <summary>Read-only directory navigation. Missing paths stay disabled; opening never creates a directory.</summary>
internal static class FolderShortcuts
{
    private sealed class Folder
    {
        internal string Group = "", Label = "", Path = "", Reason = "";
        internal bool Exists;
    }

    internal static void Open(OverviewMod mod, RectTransform anchor, Action<string> status)
    {
        var folders = new List<Folder>();
        string modGroup = Loc.T("Mod", "模组", "模組");
        Add(folders, modGroup, Loc.T("Mod folder", "模组目录", "模組資料夾"), () => mod.PackageRoot.Length > 0
            ? mod.PackageRoot : System.IO.Path.GetDirectoryName(mod.Location) ?? "");
        if (!string.Equals(mod.Guid, Plugin.PluginGuid, StringComparison.OrdinalIgnoreCase)) { Show(folders, anchor, status); return; }

        Add(folders, modGroup, Loc.T("Configuration folder", "配置目录", "設定資料夾"), () => Paths.ConfigPath);
        string translation = Loc.T("Translation", "翻译", "翻譯");
        Add(folders, translation, Loc.T("Language folder", "翻译目录", "翻譯資料夾"), () => Loc.LanguageDirectory);
        Add(folders, translation, Loc.T("Translation exports", "翻译导出", "翻譯匯出"), () => Loc.ExportDirectory);
        // 1.1.0（MCE，概览页第 7 章）：去掉整合包（导出 / 工作区 / 方案资源 / 切换备份）、下载缓存与配置备份——MCE 没有这些。
        // 本模组自己的行仍给只读目录导航（配置 / 翻译 / 日志），打开走 Application.OpenURL，不建目录。
        string maintenance = Loc.T("Maintenance", "维护", "維護");
        Add(folders, maintenance, Loc.T("BepInEx log folder", "BepInEx 日志目录", "BepInEx 日誌資料夾"), () => Paths.BepInExRootPath);
        Add(folders, maintenance, Loc.T("Game log folder", "游戏日志目录", "遊戲日誌資料夾"), GameLogDirectory);
        Show(folders, anchor, status);
    }

    internal static void Open(InfrastructureOverviewModel component, RectTransform anchor, Action<string> status)
    {
        var folders = new List<Folder>();
        foreach (var location in component.Locations)
        {
            string area = location.Area == "core" ? Loc.T("Core", "核心组件(Core)", "核心組件(Core)")
                : Loc.T("Patcher", "启动补丁(Patcher)", "啟動補丁(Patcher)");
            Add(folders, Loc.T("Mod", "模组", "模組"), area + " · " + System.IO.Path.GetFileName(location.Path), () => location.Path);
        }
        Show(folders, anchor, status);
    }

    private static void Add(List<Folder> folders, string group, string label, Func<string> path, string? unavailable = null)
    {
        var folder = new Folder { Group = group, Label = label };
        try
        {
            string value = path();
            if (string.IsNullOrWhiteSpace(value)) folder.Reason = unavailable ?? Loc.T("Folder unavailable", "目录暂不可用", "資料夾暫時無法使用");
            else
            {
                folder.Path = System.IO.Path.GetFullPath(value);
                folder.Exists = Directory.Exists(folder.Path);
                if (!folder.Exists) folder.Reason = Loc.T("Folder not found", "目录不存在", "資料夾不存在");
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException
            or Newtonsoft.Json.JsonException)
        {
            folder.Reason = unavailable ?? Loc.T("Folder unavailable", "目录暂不可用", "資料夾暫時無法使用");
        }
        // Keep different intentions at one path, but do not repeat an identical label/path entry.
        if (!folders.Any(f => f.Group == group && f.Label == label && f.Path.Equals(folder.Path, StringComparison.OrdinalIgnoreCase))) folders.Add(folder);
    }

    private static string GameLogDirectory()
    {
        string file = Application.consoleLogPath;
        if (string.IsNullOrWhiteSpace(file)) return Paths.GameRootPath;
        if (!System.IO.Path.IsPathRooted(file)) file = System.IO.Path.Combine(Paths.GameRootPath, file);
        return System.IO.Path.GetDirectoryName(file) ?? Paths.GameRootPath;
    }

    private static void Show(IReadOnlyList<Folder> folders, RectTransform anchor, Action<string> status)
    {
        if (folders.Count == 1 && folders[0].Exists) { OverviewListView.OpenFolder(folders[0].Path, status); return; }
        var entries = new List<UiMenuEntry>();
        foreach (var group in folders.GroupBy(f => f.Group))
        {
            entries.Add(new UiMenuEntry { Label = group.Key, Header = true });
            foreach (var folder in group)
                entries.Add(new UiMenuEntry { Label = folder.Label, Enabled = folder.Exists, DisabledReason = folder.Reason,
                    CanInvoke = () => Directory.Exists(folder.Path), Invoke = () => OverviewListView.OpenFolder(folder.Path, status) });
        }
        OverviewUi.Dropdown(anchor, Loc.T("Folder shortcuts", "目录快捷方式", "資料夾捷徑"), entries, 360);
    }
}
