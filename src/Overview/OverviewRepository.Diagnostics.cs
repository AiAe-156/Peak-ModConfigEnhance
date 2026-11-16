#nullable enable
using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using ModConfigEnhance.Diagnostics;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ModConfigEnhance.Overview;

/// <summary>
/// 日志导出时附带的诊断信息（对照 IGMM 同名文件）。
/// 1.1.0（MCE）：IGMM 还写已应用方案、方案差异、其他已保存方案、待重启配置与整合包名称；MCE 没有方案 / 草稿 / 整合包，这几项全部去掉，
/// 只留游戏 / Unity / BepInEx / 本模组版本、模组清单（加载与下次启动是否启用，只读判断）与基础组件。路径一律写成相对 BepInEx 目录。
/// 基础组件直接用 <see cref="InfrastructurePackages.Scan()"/> 的结果按包归并（IGMM 经 InfrastructureOverviewModel，那是第 7 章的展示类，这里不依赖它）。
/// </summary>
internal sealed partial class OverviewRepository
{
    // Called on the game thread after the destination was selected. Compression never touches Unity objects.
    internal JObject CaptureDiagnosticMetadata(System.Collections.Generic.IReadOnlyList<OverviewMod> installedMods)
    {
        var mods = installedMods;
        // 与 IGMM InfrastructureOverviewModel.Create 同一筛选：core / patchers 下、有路径、身份确认的按包键，未确认的只认 file: 本地键。
        var components = InfrastructurePackages.Scan()
            .Where(p => (p.Area == "core" || p.Area == "patchers") && p.Path.Length > 0
                && (p.IdentityConfirmed ? ShopInstalled.Key(p.Key) : p.Key.StartsWith("file:", StringComparison.Ordinal)))
            .GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var gameVersion = Application.version;
        var versionPrefix = System.Text.RegularExpressions.Regex.Match(gameVersion, @"^(\d+\.\d+(?:\.\d+){0,2})(?=\s*--)");
        var metadata = new JObject {
            ["schemaVersion"] = 1, ["purpose"] = "仅用于 Debug 和故障排查", ["captured"] = DateTimeOffset.Now,
            ["gameVersion"] = versionPrefix.Success ? versionPrefix.Groups[1].Value : gameVersion,
            ["gameVersionRaw"] = gameVersion, ["unityVersion"] = Application.unityVersion,
            ["bepInExVersion"] = typeof(BaseUnityPlugin).Assembly.GetName().Version?.ToString(),
            ["mceVersion"] = Plugin.DisplayVersion,
            ["mods"] = new JArray(mods.Select(m => new JObject { ["name"] = m.RawName, ["guid"] = m.Guid, ["version"] = m.PluginVersion,
                ["installed"] = File.Exists(m.Location), ["loaded"] = Chainloader.PluginInfos.TryGetValue(m.Guid, out var loaded) && loaded.Instance != null,
                ["enabledNextStart"] = File.Exists(m.Location) && (m.ResourcePack ? Path.GetFileName(m.Location).Equals("manifest.json", StringComparison.OrdinalIgnoreCase)
                    : m.Location.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)),
                ["path"] = Relative(m.Location) })),
            ["components"] = new JArray(components.Select(g =>
            {
                var rows = g.OrderBy(p => p.Area, StringComparer.Ordinal).ThenBy(p => p.Path, StringComparer.OrdinalIgnoreCase).ToArray();
                var first = rows[0];
                var runtimeFiles = rows.SelectMany(p => p.RuntimeFiles).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                return new JObject {
                    ["name"] = first.Name, ["packageKey"] = first.Key,
                    ["version"] = string.Join(" / ", rows.Select(p => p.Version).Where(v => v.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)),
                    ["identityConfirmed"] = rows.All(p => p.IdentityConfirmed),
                    ["installed"] = runtimeFiles.Any(File.Exists),
                    ["enabledNextStart"] = runtimeFiles.Length == 0 || runtimeFiles.Any(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)),
                    ["areas"] = new JArray(rows.Select(p => p.Area).Distinct(StringComparer.OrdinalIgnoreCase)),
                    ["locations"] = new JArray(rows.GroupBy(p => p.Area + "|" + p.Path, StringComparer.OrdinalIgnoreCase)
                        .Select(l => l.OrderByDescending(p => p.ManifestVerified).First()).Select(l => new JObject {
                            ["area"] = l.Area, ["path"] = Relative(l.Path),
                            ["identityConfirmed"] = l.IdentityConfirmed, ["manifestVerified"] = l.ManifestVerified })),
                    ["runtimeFiles"] = new JArray(runtimeFiles.Select(f => new JObject {
                        ["path"] = Relative(f), ["exists"] = File.Exists(f),
                        ["enabled"] = f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) })) };
            })),
            ["notes"] = "日志正文保留原文；元信息遮盖已识别的敏感字段。ModConfig Enhance 没有方案 / 整合包，不附配置差异。"
        };
        return (JObject)LogExport.Scrub(metadata);
    }

    private static string Relative(string path)
    {
        try { return path.Length == 0 ? "" : Path.GetRelativePath(Paths.BepInExRootPath, path); }
        catch (ArgumentException) { return Path.GetFileName(path); }
    }
}
