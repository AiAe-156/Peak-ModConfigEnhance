#nullable enable
using System;
using System.Collections.Generic;
using ModConfigEnhance.Updating;

namespace ModConfigEnhance.Overview;

/// <summary>
/// 1.1.0（MCE，概览页第 7 章新写）：基础组件依赖图的进程内缓存，取代 IGMM 放在 SourceUi（来源工具，MCE 不搬）里的
/// DependencyGraph / DisplayDependencyGraph / PublishDependencyGraph / InvalidateDependencyGraph / DependencyContext。
/// MCE 没有整合包工作区与来源确认，所以「上下文」恒为 BepInEx 根目录，图只按 <see cref="OverviewPageCache.Inventory"/> 失效。
/// 列表页（OverviewListView.Infrastructure）后台读完组件后发布，依赖卡、组件详情读它；只读，不写任何文件。
/// </summary>
internal static class OverviewDependencyGraphCache
{
    private static ComponentDependencyGraph? _graph;
    private static int _revision = -1;
    private static IReadOnlyList<InfrastructureOverviewModel> _components = Array.Empty<InfrastructureOverviewModel>();
    /// <summary>IGMM 是「整合包工作区或 BepInEx 根 + 方案修订号」；MCE 恒为 BepInEx 根。</summary>
    internal static string Context => System.IO.Path.GetFullPath(BepInEx.Paths.BepInExRootPath);
    /// <summary>与当前模组清单同一代的依赖图；清单变了（Inventory 修订号不同）返回 null，等列表页重读。</summary>
    internal static ComponentDependencyGraph? Graph => _graph == null || _revision != OverviewPageCache.Inventory ? null : _graph;
    internal static IReadOnlyList<InfrastructureOverviewModel> Components => Graph == null ? Array.Empty<InfrastructureOverviewModel>() : _components;
    internal static void Invalidate() { _graph = null; _components = Array.Empty<InfrastructureOverviewModel>(); }
    internal static void Publish(ComponentDependencyGraph graph, int revision, IReadOnlyList<InfrastructureOverviewModel>? components = null)
    { _graph = graph; _revision = revision; _components = components ?? Array.Empty<InfrastructureOverviewModel>(); }
}
