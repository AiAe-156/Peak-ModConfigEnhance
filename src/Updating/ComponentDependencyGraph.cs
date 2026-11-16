#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace ModConfigEnhance.Updating;

/// <summary>Immutable-by-convention evidence snapshot. IDs are package keys or file paths, never invented plugin GUIDs.
/// 1.1.0（MCE）：照抄未改逻辑。MceGuid 经 ManagerIdentity 指向 MCE 自己；「受保护」只用于显示（MCE 不启停、不卸载任何模组），
/// IGMM 的卸载判断 GetCapability / Capability 已删（MCE 不卸载任何模组，没有调用方）。</summary>
public sealed class ComponentDependencyGraph
{
    public const string MceGuid = ManagerIdentity.Guid;
    /// <summary>What MCE itself runs on: the ModConfig host it hard-depends on, plus the two PEAKLib assemblies it
    /// references. Deliberately a short, fixed list — the graph walk used to decide this, and it gave MCE a hard
    /// edge to every assembly it could not resolve ("possible provider"), which put unrelated mods under
    /// protection and made them impossible to uninstall.</summary>
    public static readonly string[] MceRequiredIdentities =
    {
        "com.github.PEAKModding.PEAKLib.ModConfig",
        "com.github.PEAKModding.PEAKLib.Core",
        "com.github.PEAKModding.PEAKLib.UI"
    };
    /// <summary>2.3.9 (user decision): the protected whitelist — MCE plus <see cref="MceRequiredIdentities"/>. These
    /// cannot be enabled/disabled or uninstalled from MCE; everything else can.</summary>
    public static bool IsProtected(string guid) => !string.IsNullOrEmpty(guid)
        && (ManagerIdentity.IsManagerGuid(guid) || MceRequiredIdentities.Contains(guid, Compare));
    internal static IReadOnlyList<string> MceRequiredBy(Component component)
    {
        var required = new List<string>();
        foreach (string identity in MceRequiredIdentities)
            if (component.PluginGuids.Contains(identity, Compare) || component.Assemblies.Contains(identity, Compare)) required.Add(identity);
        return required;
    }
    public sealed class Component
    {
        public string Id = "", PackageKey = "";
        public readonly List<string> Locations = new List<string>();
        public readonly List<string> PluginGuids = new List<string>();
        public readonly List<string> Assemblies = new List<string>();
    }
    public sealed class Edge
    {
        public string From = "", To = "", Evidence = "";
        public bool Hard = true;
    }
    public sealed class TreeEntry
    {
        public string Id = "", Evidence = "";
        public int Depth;
        public bool Cycle, Shared, Missing, Truncated;
    }
    public readonly List<Component> Components = new List<Component>();
    public readonly List<string[]> SharedFileOwners = new List<string[]>();
    public readonly List<Edge> Edges = new List<Edge>();
    public readonly List<string> Failures = new List<string>();
    public sealed class FailureScope
    {
        public string Node = "", Path = "", Reason = "";
    }
    public readonly List<FailureScope> FailureScopes = new List<FailureScope>();
    public void AddFailure(string reason, string node = "", string path = "")
    {
        Failures.Add(reason);
        FailureScopes.Add(new FailureScope { Node = node, Path = path, Reason = reason });
    }
    static readonly StringComparer Compare = StringComparer.OrdinalIgnoreCase;

    /// <summary>Reverse consumers, with evidence and explicit cycle/shared/missing/budget markers.</summary>
    public IReadOnlyList<TreeEntry> ReverseTree(string id, int maxDepth = 16, int maxEntries = 256)
    {
        maxDepth = Math.Max(0, Math.Min(64, maxDepth)); maxEntries = Math.Max(1, Math.Min(4096, maxEntries));
        var result = new List<TreeEntry>(); var seen = new HashSet<string>(Compare);
        void Visit(string node, int depth, HashSet<string> ancestors, string evidence)
        {
            if (result.Count >= maxEntries) { result[result.Count - 1].Truncated = true; return; }
            bool cycle = ancestors.Contains(node), shared = !seen.Add(node);
            var entry = new TreeEntry { Id = node, Depth = depth, Evidence = evidence, Cycle = cycle, Shared = shared,
                Missing = !Components.Any(c => Compare.Equals(c.Id, node)) };
            result.Add(entry);
            var incoming = Edges.Where(e => Compare.Equals(e.To, node)).OrderBy(e => e.From, Compare).ToList();
            if (cycle || shared) return;
            if (depth >= maxDepth) { entry.Truncated = incoming.Count > 0; return; }
            var next = new HashSet<string>(ancestors, Compare) { node };
            foreach (var edge in incoming) Visit(edge.From, depth + 1, next, edge.Evidence + (edge.Hard ? " [hard]" : " [soft]"));
        }
        Visit(id, 0, new HashSet<string>(Compare), "target"); return result;
    }
}
