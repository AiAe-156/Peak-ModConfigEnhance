#nullable enable
using System;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ModConfigEnhance.Overview;

// 1.1.0：从游戏内模组管理器 OverviewListView.cs 末尾抄出的三个小组件（MCE 没有模组列表，单独成文件）。

/// <summary>每帧回调与限时队列（每帧最多 4 ms），快捷键页用它的 Tick 驱动手柄手势与重画。</summary>
internal sealed class OverviewListPump : MonoBehaviour
{
    private readonly ConcurrentQueue<Action> _queue = new();
    // 1.1.0（MCE）：快捷键页（第 4 章）才赋值，显式初值免 CS0649。
    internal Action? Tick = null;
    internal void Enqueue(Action action) => _queue.Enqueue(action);
    private void Update()
    {
        var budget = System.Diagnostics.Stopwatch.StartNew();
        while (budget.ElapsedMilliseconds < 4 && _queue.TryDequeue(out Action? action))
        {
            try { action(); }
            catch (Exception ex) { Plugin.Log.LogWarning("[概览] 列表更新失败：" + ex.Message); }
        }

        // 第 5 章（二次审查）：快捷键页每帧回调出错时只记一次日志，不让同一个异常每帧冒到 Unity 主循环。
        try { Tick?.Invoke(); }
        catch (Exception ex) { if (!_tickWarned) { _tickWarned = true; Plugin.Log.LogError("[概览] 快捷键页每帧更新失败：" + ex); } }
    }
    private bool _tickWarned;
}

internal sealed class OverviewTextClick : MonoBehaviour, IPointerClickHandler
{
    internal Action<PointerEventData>? Clicked;
    public void OnPointerClick(PointerEventData data) => Clicked?.Invoke(data);
}

internal sealed class OverviewHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // 1.1.0（MCE）：快捷键页（第 4 章）才赋值，显式初值免 CS0649。
    internal Action? Enter = null, Exit = null;
    public void OnPointerEnter(PointerEventData data) => Enter?.Invoke();
    public void OnPointerExit(PointerEventData data) => Exit?.Invoke();
}
