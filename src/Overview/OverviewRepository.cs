#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace ModConfigEnhance.Overview;

/// <summary>
/// 1.1.0（MCE，概览页第 6 章新写）：模组概览的个人状态仓库，与游戏内模组管理器同名类的公开成员同名，实现另写。
/// IGMM 的 OverviewRepository 有十个 partial，九个是配置方案 / 草稿 / 整合包 / 已应用快照；MCE 没有这些，只留：
/// <list type="bullet">
/// <item><see cref="State"/>：读写 <c>config\ModConfigEnhance\overview.json</c>（分组、折叠、隐藏已停用），改完立即保存，没有草稿。</item>
/// <item><see cref="GetRule"/> / <see cref="GroupOf"/> / <see cref="Save"/> / <see cref="Reload"/> / <see cref="IsStale"/>：语义同 IGMM（没有整合包作者的默认值层）。</item>
/// <item><see cref="AtomicWrite"/> / <see cref="SafeFiles"/>：照抄 IGMM 的两个静态工具。</item>
/// <item><see cref="PlannedEnabled"/> / <see cref="BlockedByOuter"/>：IGMM 是启停草稿；MCE 不启停，只读文件现状。</item>
/// </list>
/// 不写：更新许可（MCE 不联网）、ReloadInstalled（IGMM 只有自动更新检查用它）、SafePath（只服务整合包 / 介绍文档）、诊断信息（第 10 章日志导出时再加）。
/// 只写 overview.json 这一个文件，不碰任何模组文件。
/// </summary>
internal sealed partial class OverviewRepository
{
    /// <summary><c>config\ModConfigEnhance</c>（夹具可传别的目录）。</summary>
    public string ConfigRootDirectory { get; }
    public string StatePath => Path.Combine(ConfigRootDirectory, "overview.json");
    public OverviewState State { get; private set; } = new();
    private readonly object _saveLock = new();
    // The overview page and the settings sidebar hold separate instances of the same state file.
    private static int _sharedRevision;
    private int _loadedRevision = -1;
    public bool IsStale => _loadedRevision != _sharedRevision;
    /// <summary>读 overview.json 失败（内容坏了 / 版本不认识）：照常用空状态显示，第一次保存前先把原文件留成 .bad，不悄悄覆盖。</summary>
    private bool _readFailed;
    // A rule handed out by GetRule lives here until something gives it a group or an order; Save then merges it (same as IGMM).
    private readonly Dictionary<string, OverviewModRule> _temporaryRules = new(StringComparer.OrdinalIgnoreCase);

    public OverviewRepository(string? rootDirectory = null)
    {
        ConfigRootDirectory = rootDirectory ?? Loc.RootDirectory;
        Reload();
    }

    public void Reload()
    {
        _temporaryRules.Clear(); _readFailed = false;
        var state = new OverviewState();
        try
        {
            if (File.Exists(StatePath))
            {
                state = JsonConvert.DeserializeObject<OverviewState>(File.ReadAllText(StatePath, Encoding.UTF8)) ?? new OverviewState();
                if (!GroupTree.SupportedSchema(state.schemaVersion)) throw new InvalidDataException("unsupported schemaVersion " + state.schemaVersion);
            }
        }
        catch (Exception ex)
        {
            _readFailed = true; state = new OverviewState();
            Plugin.Log.LogWarning("[概览] 读取 overview.json 失败，按空状态显示（下次保存前会先把原文件留成 overview.json.bad）：" + ex.Message);
        }
        state.groups ??= new(); state.mods ??= new(); state.customColors ??= new(); state.collapsedGroups ??= new();
        state.groups.RemoveAll(g => g == null || string.IsNullOrEmpty(g.id));
        state.mods.RemoveAll(r => r == null || string.IsNullOrEmpty(r.id));
        State = state;
        _loadedRevision = _sharedRevision;
    }

    public OverviewModRule GetRule(string id)
    {
        var rule = State.mods.FirstOrDefault(r => r.id == id);
        if (rule != null) return rule;
        if (_temporaryRules.TryGetValue(id, out var existing)) return existing;
        rule = new OverviewModRule { id = id };
        _temporaryRules[id] = rule;
        return rule;
    }

    /// <summary>Read-only lookup; unlike GetRule it never adds a personal rule.</summary>
    public string GroupOf(string id)
    {
        string group = State.mods.FirstOrDefault(r => r.id == id)?.groupId ?? "";
        return group.Length > 0 && State.groups.Any(g => g.id == group) ? group : "";
    }

    /// <summary>IGMM：启停草稿里的目标状态。MCE 不启停模组，就是文件现状。</summary>
    public bool PlannedEnabled(OverviewMod mod) => mod.TargetEnabled;
    /// <summary>照 IGMM 的只读判断：嵌在别的模组文件夹里、外层模组已停用。</summary>
    public bool BlockedByOuter(OverviewMod mod) => mod.Outer != null && mod.CanToggle && !PlannedEnabled(mod.Outer);

    private static bool MeaningfulRule(OverviewModRule r) => r.groupId.Length > 0 || r.order != 0;
    private void MergeEditedRules()
    {
        foreach (var rule in _temporaryRules.Values.Where(MeaningfulRule))
            if (!State.mods.Any(r => r.id.Equals(rule.id, StringComparison.OrdinalIgnoreCase))) State.mods.Add(rule);
    }

    /// <summary>立即写 overview.json（没有草稿）。写失败会抛异常，调用方写状态行；内存里的修改保留。</summary>
    public void Save()
    {
        lock (_saveLock)
        {
            MergeEditedRules();
            State.schemaVersion = GroupTree.SchemaFor(State.groups);
            // 第 11 章审查：只在磁盘上仍是当初读坏的那份时才留 .bad。读坏后另一份仓库实例（设置页侧栏）先保存过，
            // 磁盘上已是好文件、原来的坏文件已在 .bad 里，再复制会把它盖掉。
            if (_readFailed && _loadedRevision == _sharedRevision && File.Exists(StatePath))
            {
                try { File.Copy(StatePath, StatePath + ".bad", true); }
                catch (Exception ex) { Plugin.Log.LogWarning("[概览] 备份读不了的 overview.json 失败：" + ex.Message); }
            }
            AtomicWrite(StatePath, JsonConvert.SerializeObject(State, Formatting.Indented));
            _readFailed = false;
        }
        _loadedRevision = ++_sharedRevision;
        OverviewPageCache.ConfigurationSaved();
    }

    internal static IEnumerable<string> SafeFiles(string root)
    {
        var result = new List<string>(); var pending = new Stack<string>(); pending.Push(root);
        while (pending.Count > 0)
        {
            string folder = pending.Pop();
            try
            {
                result.AddRange(Directory.GetFiles(folder));
                foreach (string child in Directory.GetDirectories(folder))
                {
                    string name = Path.GetFileName(child);
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0 || name.Equals("备份") || name.Equals("backup", StringComparison.OrdinalIgnoreCase) || name.Equals("backups", StringComparison.OrdinalIgnoreCase) || name.Equals("bin", StringComparison.OrdinalIgnoreCase) || name.Equals("obj", StringComparison.OrdinalIgnoreCase)) continue;
                    pending.Push(child);
                }
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        return result;
    }

    internal static void AtomicWrite(string file, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        string temporary = file + ".tmp";
        File.WriteAllText(temporary, content, new UTF8Encoding(false));
        if (File.Exists(file)) File.Replace(temporary, file, file + ".bak");
        else File.Move(temporary, file);
    }
}
