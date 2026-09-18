using System.Reflection;
using ModConfigEnhance;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

static class Program
{
    static void Main()
    {
        PassThroughWithoutMarkerOrWhenDisabled();
        MovesOneThirdOfOriginalStepAndHonorsDirectionAndBounds();
        ReachesItsTargetAtOneTenthSecond();
        ExternalTakeoverAndCloseCancelPendingMotion();
        DisabledScrollCancelsQueuedMotion();
        Console.WriteLine("SidebarWheelScroll source tests passed.");
    }

    static void PassThroughWithoutMarkerOrWhenDisabled()
    {
        ScrollRect other = NewScroll(0.5f);
        Assert(SidebarWheelScroll.OnScrollPrefix(other, Wheel(1f)), "无标记的 ScrollRect 必须走原生路径");

        (ScrollRect scroll, SidebarWheelScroll wheel) = NewWheel(0.5f);
        wheel.isActiveAndEnabled = false;
        Assert(SidebarWheelScroll.OnScrollPrefix(scroll, Wheel(1f)), "禁用的左栏必须走原生路径");
        wheel.isActiveAndEnabled = true;
        scroll.isActiveAndEnabled = false;
        Assert(SidebarWheelScroll.OnScrollPrefix(scroll, Wheel(1f)), "禁用的 ScrollRect 必须走原生路径");
    }

    static void DisabledScrollCancelsQueuedMotion()
    {
        (ScrollRect scroll, _) = NewWheel(0.5f);
        SidebarWheelScroll.OnScrollPrefix(scroll, Wheel(1f));
        scroll.isActiveAndEnabled = false;
        Tick(scroll, 0.1f);
        Near(scroll.verticalNormalizedPosition, 0.5f, "禁用后不得继续缓动");
        scroll.isActiveAndEnabled = true;
        Tick(scroll, 0.1f);
        Near(scroll.verticalNormalizedPosition, 0.5f, "重新启用不得恢复旧滚轮目标");
    }

    static void MovesOneThirdOfOriginalStepAndHonorsDirectionAndBounds()
    {
        (ScrollRect scroll, _) = NewWheel(0.5f);
        Assert(!SidebarWheelScroll.OnScrollPrefix(scroll, Wheel(1f)), "左栏滚轮必须截断原生处理");
        Tick(scroll, 0.1f);
        Near(scroll.verticalNormalizedPosition, 0.5f + (40f / 3f) / 200f, "单次向上距离应为40的三分之一");

        (scroll, _) = NewWheel(0.5f);
        SidebarWheelScroll.OnScrollPrefix(scroll, Wheel(-1f));
        Tick(scroll, 0.1f);
        Near(scroll.verticalNormalizedPosition, 0.5f - (40f / 3f) / 200f, "负滚轮方向必须向下");

        (scroll, _) = NewWheel(0.99f);
        SidebarWheelScroll.OnScrollPrefix(scroll, Wheel(2f));
        Tick(scroll, 0.1f);
        Near(scroll.verticalNormalizedPosition, 1f, "顶部必须钳制");

        (scroll, _) = NewWheel(0.01f);
        SidebarWheelScroll.OnScrollPrefix(scroll, Wheel(-2f));
        Tick(scroll, 0.1f);
        Near(scroll.verticalNormalizedPosition, 0f, "底部必须钳制");
    }

    static void ReachesItsTargetAtOneTenthSecond()
    {
        (ScrollRect scroll, _) = NewWheel(0.5f);
        SidebarWheelScroll.OnScrollPrefix(scroll, Wheel(1f));
        Tick(scroll, 0.05f);
        Near(scroll.verticalNormalizedPosition, 0.5f + (40f / 3f) / 400f, "半程应走到平滑曲线中点");
        Tick(scroll, 0.05f);
        Near(scroll.verticalNormalizedPosition, 0.5f + (40f / 3f) / 200f, "0.1秒必须精确抵达目标");
    }

    static void ExternalTakeoverAndCloseCancelPendingMotion()
    {
        (ScrollRect scroll, SidebarWheelScroll wheel) = NewWheel(0.5f);
        SidebarWheelScroll.OnScrollPrefix(scroll, Wheel(1f));
        Tick(scroll, 0.02f);
        scroll.verticalNormalizedPosition = 0.1f; // 模拟滚动条/手柄在 Update 之间接管
        Tick(scroll, 0.1f);
        Near(scroll.verticalNormalizedPosition, 0.1f, "外部接管必须取消旧的缓动目标");

        // 同一边界的另一时序：位置改变后，下一次滚轮先于 Update 到来。
        scroll.verticalNormalizedPosition = 0.1f; // 模拟滚动条/手柄已接管，但 Update 尚未执行
        SidebarWheelScroll.OnScrollPrefix(scroll, Wheel(1f));
        Tick(scroll, 0.1f);
        Near(scroll.verticalNormalizedPosition, 0.1f + (40f / 3f) / 200f, "外部接管后的新滚轮不得累积旧目标");

        SidebarWheelScroll.OnScrollPrefix(scroll, Wheel(1f));
        Invoke(wheel, "OnDisable");
        Tick(scroll, 0.1f);
        Near(scroll.verticalNormalizedPosition, 0.1f + (40f / 3f) / 200f, "关闭组件必须取消尚未完成的缓动");
    }

    static (ScrollRect, SidebarWheelScroll) NewWheel(float position)
    {
        ScrollRect scroll = NewScroll(position);
        SidebarWheelScroll wheel = new SidebarWheelScroll { Scroll = scroll };
        scroll.SetComponent(wheel);
        Invoke(wheel, "OnEnable");
        return (scroll, wheel);
    }

    static ScrollRect NewScroll(float position) => new()
    {
        content = new RectTransform(300f),
        viewport = new RectTransform(100f),
        verticalNormalizedPosition = position,
    };

    static PointerEventData Wheel(float y) => new() { scrollDelta = new Vector2(0f, y) };
    static void Tick(ScrollRect scroll, float seconds)
    {
        Time.unscaledDeltaTime = seconds;
        SidebarWheelScroll wheel = scroll.GetComponent<SidebarWheelScroll>()!;
        Invoke(wheel, "Update");
    }

    static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, null);
    static void Near(float actual, float expected, string message) => Assert(MathF.Abs(actual - expected) < 0.0001f, $"{message}: actual={actual}, expected={expected}");
    static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
