#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace ModConfigEnhance.Updating
{
    // 1.1.0（MCE）：只抄首次智能分组要用的纯函数与类型（FolderEntry、FolderClassification、Normalize / Parent / Leaf / NeverTouch /
    // FolderName / Classify）。IGMM 的目录整理（FolderRow / FolderGroup / FolderLayoutInput / FolderMove / FolderPackageChange /
    // FolderLayoutResult、Plan、Simulation 等）会移动 plugins/ 下的文件夹，MCE 不改任何模组文件，不搬。
    /// <summary>One file or folder under plugins/, as collected by the caller. Paths are relative to BepInEx, '/' separated.</summary>
    public sealed class FolderEntry
    {
        public string Path = "";
        public bool IsDirectory;
        public bool HasLink;
        /// <summary>Folders: a manifest.json (or manifest.json.old, a mod MCE disabled) sits directly inside.</summary>
        public bool HasManifest;
        public string ManifestName = "", ManifestVersion = "";
        public List<string> Dependencies = new List<string>();
        /// <summary>Files: a plugin assembly (".dll" or a disabled ".dll.*").</summary>
        public bool IsAssembly;
        /// <summary>Files: BepInPlugin ids read from the assembly.</summary>
        public List<string> PluginGuids = new List<string>();
    }

    /// <summary>What each folder under plugins/ is. A package is a folder with its own manifest.json (or, outside any
    /// package, a folder holding plugin assemblies directly); a folder inside a package is part of it, except a nested
    /// package with its own manifest — unless that is the package's own layout (inside its plugins/, BepInEx/ or
    /// patchers/, or the same manifest name). A container holds packages but is none; an empty folder holds neither.</summary>
    public sealed class FolderClassification
    {
        public readonly HashSet<string> Packages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Containers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Top-most folders that hold no package (and so no plugin assembly).</summary>
        public readonly HashSet<string> Empties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Owning package of every folder and file ("" outside packages).</summary>
        public readonly Dictionary<string, string> Owner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Package → the package it is nested in ("" at top level).</summary>
        public readonly Dictionary<string, string> OuterOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>1.1.0（MCE）：IGMM 这里是目录整理规划器（移动整个模组文件夹）；MCE 只留分类纯函数，供首次智能分组读文件夹结构，不移动任何东西。</summary>
    public static class GroupFolderPlanner
    {
        private static readonly StringComparer Paths = StringComparer.OrdinalIgnoreCase;
        private static readonly string[] Packaging = { "plugins", "BepInEx", "patchers" };

        public static string Normalize(string? path) => (path ?? "").Replace('\\', '/').Trim('/');
        public static string Parent(string path) { int at = path.LastIndexOf('/'); return at < 0 ? "" : path.Substring(0, at); }
        public static string Leaf(string path) => path.Substring(path.LastIndexOf('/') + 1);
        private static int Depth(string path) => path.Count(c => c == '/');

        /// <summary>Folders the organizer never touches: generated hook assemblies and the manager's own pack data.</summary>
        public static bool NeverTouch(string path)
        {
            var parts = Normalize(path).Split('/');
            if (parts.Length < 2 || !parts[0].Equals("plugins", StringComparison.OrdinalIgnoreCase)) return false;
            return parts[1].Equals("MMHOOK", StringComparison.OrdinalIgnoreCase) || ManagerIdentity.IsPackFolderPrefixed(parts[1]);
        }

        /// <summary>A group name as a folder name: characters Windows does not allow are dropped (not replaced).</summary>
        public static string FolderName(string? name)
        {
            var text = new string((name ?? "").Where(c => c >= 32 && c != 127 && "\\/:*?\"<>|".IndexOf(c) < 0).ToArray()).Trim().TrimEnd('.', ' ');
            if (text.Length == 0) return "_";
            string stem = text.Split('.')[0].Trim().ToUpperInvariant();
            bool device = stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" || stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && char.IsDigit(stem[3]);
            return device ? "_" + text : text;
        }

        public static FolderClassification Classify(IEnumerable<FolderEntry> entries)
        {
            var c = new FolderClassification();
            var list = entries.Where(e => e != null).Select(e => { e.Path = Normalize(e.Path); return e; })
                .Where(e => e.Path.StartsWith("plugins/", StringComparison.OrdinalIgnoreCase) && !NeverTouch(e.Path)).ToList();
            var byPath = new Dictionary<string, FolderEntry>(Paths);
            foreach (var e in list) byPath[e.Path] = e;
            var assemblyDirs = new HashSet<string>(list.Where(e => !e.IsDirectory && e.IsAssembly).Select(e => Parent(e.Path)), Paths);
            var candidates = new List<string>();
            string OwnerOfParent(string path) { string parent = Parent(path); return parent.Equals("plugins", StringComparison.OrdinalIgnoreCase) ? "" : c.Owner.TryGetValue(parent, out var o) ? o : ""; }
            foreach (var dir in list.Where(e => e.IsDirectory).OrderBy(e => Depth(e.Path)).ThenBy(e => e.Path, Paths))
            {
                string owner = OwnerOfParent(dir.Path);
                if (dir.HasManifest)
                {
                    if (owner.Length > 0 && IsPackaging(dir.Path, owner, byPath)) { c.Owner[dir.Path] = owner; continue; }
                    c.Packages.Add(dir.Path); c.Owner[dir.Path] = dir.Path; c.OuterOf[dir.Path] = owner; continue;
                }
                if (owner.Length == 0 && assemblyDirs.Contains(dir.Path)) { c.Packages.Add(dir.Path); c.Owner[dir.Path] = dir.Path; c.OuterOf[dir.Path] = ""; continue; }
                c.Owner[dir.Path] = owner;
                if (owner.Length == 0) candidates.Add(dir.Path);
            }
            foreach (var file in list.Where(e => !e.IsDirectory)) c.Owner[file.Path] = OwnerOfParent(file.Path);
            var empty = new HashSet<string>(Paths);
            foreach (string candidate in candidates)
                if (c.Packages.Any(p => p.StartsWith(candidate + "/", StringComparison.OrdinalIgnoreCase))) c.Containers.Add(candidate); else empty.Add(candidate);
            foreach (string folder in empty) if (!empty.Contains(Parent(folder))) c.Empties.Add(folder);
            return c;
        }

        private static bool IsPackaging(string inner, string outer, Dictionary<string, FolderEntry> byPath)
        {
            string first = inner.Substring(outer.Length + 1).Split('/')[0];
            if (Packaging.Any(p => p.Equals(first, StringComparison.OrdinalIgnoreCase))) return true;
            string a = byPath.TryGetValue(inner, out var ie) ? ie.ManifestName : "", b = byPath.TryGetValue(outer, out var oe) ? oe.ManifestName : "";
            return a.Length > 0 && Paths.Equals(a, b);
        }
    }
}
