#nullable enable

namespace ModConfigEnhance.Diagnostics;

/// <summary>
/// 本模组在日志窗口里的来源标识。1.1.0（MCE）：IGMM 同名类是「两层管理日志」的写入器；MCE 的日志都直接写 Plugin.Log，
/// 没有自己的日志通道（Q12 不加 Diagnostics cfg），这里只留日志窗口认「本模组的来源」要用的常量。
/// </summary>
internal static class ManagerLog
{
    internal const string SourceId = "manager";
    /// <summary>Category shown in the log window's source/name column for our records.</summary>
    internal const string CategoryLabel = Plugin.PluginName;
}