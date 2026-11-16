#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace ModConfigEnhance.Overview;
internal sealed partial class OverviewListView
{
    // 1.1.0（MCE）：只留关联视图（「查看模组依赖项」）；状态 / 标签筛选（MatchesStatus）MCE 没有入口，已删。
    // 关联视图不再清标签 / 来源筛选（MCE 没有）。
    private HashSet<string>? _relatedMods;
    private HashSet<string> _relatedPackages = new(StringComparer.OrdinalIgnoreCase);
    internal void ShowRelatedMods(IEnumerable<OverviewMod> mods)
    {
        var members = mods.ToArray();
        _relatedMods = new HashSet<string>(members.Select(m => m.Id.Length > 0 ? m.Id : m.Guid), StringComparer.OrdinalIgnoreCase);
        _relatedPackages = new HashSet<string>(members.SelectMany(m => m.PackageDependencies).Select(d => d.LastIndexOf('-') is int at && at > 0 ? d.Substring(0, at) : d), StringComparer.OrdinalIgnoreCase);
        ClearOverviewSearch(); _repo.State.hideDisabled = false;
        Render();
    }
    private bool MatchesRelated(OverviewMod mod) => _relatedMods == null || _relatedMods.Contains(mod.Id.Length > 0 ? mod.Id : mod.Guid);
}

