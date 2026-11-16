#nullable enable

namespace ModConfigEnhance.Overview;

internal enum OverviewDependencyHintMode { FollowMouse, Fixed }

/// <summary>
/// 1.1.0（MCE，概览页第 7 章）：IGMM 这里是两项 cfg（「显示位置」「显示完整依赖链」）。用户 11-15 定（Q12）：MCE 不加任何 cfg 选项，
/// 依赖提示固定「跟随鼠标」、固定「显示完整依赖链」，所以改成常数（OverviewDependencyHints 的 Fixed / Chains 读这里）。
/// </summary>
internal static class OverviewDependencySettings
{
    internal const OverviewDependencyHintMode Mode = OverviewDependencyHintMode.FollowMouse;
    internal const bool ShowDependencyChains = true;
}
