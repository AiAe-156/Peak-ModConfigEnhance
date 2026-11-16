#nullable enable
using System;

namespace ModConfigEnhance.Updating;

/// <summary>
/// 1.1.0（MCE）：照抄游戏内模组管理器同名类后裁剪，只留模组扫描要的成员。
/// 在 MCE 里「本管理器」就是 ModConfig Enhance 自己：<see cref="Guid"/> 是 MCE 的 GUID，个人数据目录是 <c>config\ModConfigEnhance\</c>。
/// 去掉：IGMM 新旧身份的迁移与接替（LegacyGuid、包键、程序集名、cfg 名、运行目录 BepInEx\IGMM、环境文件、助手程序名、语言键前缀），
/// 它们只服务于 IGMM 的更新 / 整合包 / 迁移，MCE 不搬。
/// 整合包资料夹（IGMMPack / MCEPack）的识别保留：玩家可能装过 IGMM 的整合包，扫描与首次智能分组要跳过这些资料夹，不把它们当成模组或分类。
/// </summary>
internal static class ManagerIdentity
{
    // ── 插件 ──────────────────────────────────────────────
    internal const string Guid = Plugin.PluginGuid;
    internal const string Name = Plugin.PluginName;

    /// <summary>是不是正在运行的 MCE 自己。扫描时用来认出受保护的基础库（见 ComponentDependencyGraph.IsProtected）。</summary>
    internal static bool IsManagerGuid(string? guid) => Same(guid, Guid);

    // ── 个人数据 config\<ConfigDirectory>\ ─────────────────
    internal const string ConfigDirectory = "ModConfigEnhance";

    // ── 整合包作者资料夹 plugins\…\<PackFolder>\（IGMM 的整合包；MCE 只认出来跳过）─────────
    internal const string PackFolder = "IGMMPack";
    internal const string LegacyPackFolder = "MCEPack";
    internal static bool IsPackFolder(string? name) => Same(name, PackFolder) || Same(name, LegacyPackFolder);
    /// <summary>路径里含 <c>/IGMMPack/</c> 或 <c>/MCEPack/</c>（统一用 / 分隔后比较）。</summary>
    internal static bool ContainsPackFolder(string path)
    {
        string p = "/" + path.Replace('\\', '/').Trim('/') + "/";
        return p.IndexOf("/" + PackFolder + "/", StringComparison.OrdinalIgnoreCase) >= 0 || p.IndexOf("/" + LegacyPackFolder + "/", StringComparison.OrdinalIgnoreCase) >= 0;
    }
    /// <summary>plugins 下的整合包资料包目录名前缀（<c>IGMMPack_xxxx</c>，旧 <c>MCEPack_xxxx</c>）。</summary>
    internal static bool IsPackFolderPrefixed(string? name) => name != null
        && (name.StartsWith(PackFolder + "_", StringComparison.OrdinalIgnoreCase) || name.StartsWith(LegacyPackFolder + "_", StringComparison.OrdinalIgnoreCase));

    private static bool Same(string? a, string b) => a != null && a.Equals(b, StringComparison.OrdinalIgnoreCase);
}
