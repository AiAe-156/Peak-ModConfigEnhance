#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace ModConfigEnhance.Overview;

/// <summary>Presentation only: no plugin GUID, profile rule, enablement, or publication target.</summary>
internal sealed class InfrastructureOverviewModel
{
    internal sealed class InstallationLocation
    {
        internal string Area { get; }
        internal string Path { get; }
        internal bool IdentityConfirmed { get; }
        internal bool ManifestVerified { get; }
        internal InstallationLocation(InfrastructurePackages.Package package)
        { Area=package.Area;Path=package.Path;IdentityConfirmed=package.IdentityConfirmed;ManifestVerified=package.ManifestVerified; }
    }
    internal string Key { get; }
    internal string Name { get; }
    internal string Area { get; }
    internal string Author { get; }
    internal string Description { get; }
    internal static string GroupTitle => Loc.T("Core/Patcher", "基础组件(Core/Patcher)", "基礎組件(Core/Patcher)");
    internal string TypeTitle => Areas.Count>1 ? Loc.T("Core + Patcher (Core/Patcher)", "核心组件 + 启动补丁(Core/Patcher)", "核心組件 + 啟動補丁(Core/Patcher)")
        : Area=="core" ? Loc.T("Core", "核心组件(Core)", "核心組件(Core)") : Loc.T("Patcher", "启动补丁(Patcher)", "啟動補丁(Patcher)");
    internal string Version { get; }
    internal string Path { get; }
    internal IReadOnlyList<string> Dependencies { get; }
    internal IReadOnlyList<InstallationLocation> Locations { get; }
    internal IReadOnlyList<string> Areas { get; }
    internal int LocationCount => Locations.Count;
    internal bool IdentityConfirmed { get; }
    /// <summary>2.3.4: every runtime file of this package across its locations; enablement renames exactly these.</summary>
    internal IReadOnlyList<string> RuntimeFiles { get; }
    /// <summary>2.4.4: lower-version manifest.json copies of this package that the scan ignored.</summary>
    internal IReadOnlyList<string> StaleManifests { get; }
    /// <summary>Enabled when any of its assemblies is a live .dll; a package whose assemblies are all .dll.* is disabled.</summary>
    internal bool Enabled => RuntimeFiles.Count == 0 || RuntimeFiles.Any(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
    // Display identity is one package, with all of its area/path locations retained below it.
    internal string Identity => Key;
    // 1.1.0（MCE）：去掉 LatestVersion / VersionContradicted / HasUpdate（组件更新检查结果，MCE 不联网）与状态筛选 MatchesStatus（没有筛选入口）。
    private InfrastructureOverviewModel(IEnumerable<InfrastructurePackages.Package> packages)
    {
        var rows=packages.OrderBy(p=>p.Area,StringComparer.Ordinal).ThenBy(p=>p.Path,StringComparer.OrdinalIgnoreCase).ToArray();
        var package=rows.First();
        Key=package.Key; Name=package.Name; Area=package.Area; Version=package.Version;
        Author=package.Author; Description=package.Description;Path=package.Path;
        Version=string.Join(" / ",rows.Select(p=>p.Version).Where(v=>v.Length>0).Distinct(StringComparer.OrdinalIgnoreCase));
        Dependencies=Array.AsReadOnly(rows.SelectMany(p=>p.Dependencies).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        Locations=Array.AsReadOnly(rows.GroupBy(p=>p.Area+"|"+p.Path,StringComparer.OrdinalIgnoreCase)
            .Select(g=>new InstallationLocation(g.OrderByDescending(p=>p.ManifestVerified).First())).ToArray());
        Areas=Array.AsReadOnly(Locations.Select(p=>p.Area).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        IdentityConfirmed=rows.All(p=>p.IdentityConfirmed);
        RuntimeFiles=Array.AsReadOnly(rows.SelectMany(p=>p.RuntimeFiles).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        StaleManifests=Array.AsReadOnly(rows.SelectMany(p=>p.StaleManifests).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }
    internal static IReadOnlyList<InfrastructureOverviewModel> Create(IEnumerable<InfrastructurePackages.Package> packages)
        => packages.Where(p => (p.Area=="core" || p.Area=="patchers") && p.Path.Length>0
                && (p.IdentityConfirmed ? ShopInstalled.Key(p.Key) : p.Key.StartsWith("file:",StringComparison.Ordinal)))
            .GroupBy(p=>p.Key,StringComparer.OrdinalIgnoreCase).Select(g=>new InfrastructureOverviewModel(g))
            .OrderBy(p=>p.Area,StringComparer.Ordinal).ThenBy(p=>p.Key,StringComparer.OrdinalIgnoreCase).ToArray();
    internal static string ShortText(string text)
    {
        string compact = text.Replace('\r', ' ').Replace('\n', ' ');
        return compact.Length <= 140 ? compact : compact.Substring(0, 140) + "…";
    }
    internal static string TreeCaption(ModConfigEnhance.Updating.ComponentDependencyGraph.TreeEntry entry)
        => entry.Id + (entry.Cycle ? " [cycle]" : "") + (entry.Shared ? " [shared]" : "")
            + (entry.Missing ? " [unknown]" : "") + (entry.Truncated ? " […]" : "") + " · " + entry.Evidence;
    internal bool Matches(string query) => MatchesText(query,Key,Name,string.Join(" ",Areas),Version,
        string.Join(" ",Locations.Select(p=>p.Path)),Author,Description,
        (Areas.Contains("core") ? "Core components 核心组件 核心組件 " : "")
            +(Areas.Contains("patchers") ? "Startup patchers 启动补丁 啟動補丁" : ""),string.Join(" ",Dependencies));
    internal static bool MatchesText(string query,params string[] values)
        => OverviewSearchText.Matches(query, values);
}
