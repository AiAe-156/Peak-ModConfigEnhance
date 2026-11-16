#nullable enable
using UnityEngine;
using UnityEngine.UI;

namespace ModConfigEnhance.Overview;

// 1.1.0：游戏内模组管理器 OverviewManagementCenter.cs 末尾的 ManagementUi 只抄了 Scrollbar（对话框正文用）。
// MCE 没有管理中心，类名保持一致便于两边同步。
internal static class ManagementUi
{
    internal static void Scrollbar(RectTransform host, ScrollRect scroll)
    {
        var track = OverviewUi.Panel("Scrollbar", host, ModConfigLook.Surface(new Color(.07f, .07f, .06f, 1))); track.anchorMin = new Vector2(1, 0); track.anchorMax = Vector2.one; track.pivot = Vector2.one; track.sizeDelta = new Vector2(10, 0); track.anchoredPosition = Vector2.zero;
        var handle = OverviewUi.Panel("Handle", track, OverviewUi.Gold); OverviewUi.Fill(handle);
        var bar = track.gameObject.AddComponent<UnityEngine.UI.Scrollbar>(); bar.targetGraphic = handle.GetComponent<Image>(); bar.handleRect = handle; bar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
        bar.navigation = new Navigation { mode = Navigation.Mode.None }; // 1.1.5 (focus review P4): not a focus stop
        scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
    }
}
