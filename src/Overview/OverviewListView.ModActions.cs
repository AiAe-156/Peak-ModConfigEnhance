#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
namespace ModConfigEnhance.Overview;
internal sealed partial class OverviewListView
{
    // 1.1.0（MCE，概览页第 7 章）：IGMM 双击行弹这个菜单；MCE 双击名称 / 简介是直接编辑，这个菜单改由右键打开。
    private void ShowModActions(OverviewMod mod, PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left && data.button != PointerEventData.InputButton.Right) return;
        HideAllHints();
        OverviewUi.PointerDropdown(data.position, data.pressEventCamera, mod.Name, ModActionEntries(mod, false).ToArray());
    }

    /// <summary>1.1.0 手柄：名称格按 A、行内任意格按 Y 打开的菜单。代替双击和右键；角标对应的动作（跳到外层模组）
    /// 也放在这里，角标本身不单独落焦（用户 2026-11-12 拍板）。</summary>
    internal void ShowGamepadRowMenu(OverviewMod mod, RectTransform anchor)
    {
        HideAllHints();
        OverviewUi.CompactDropdown(anchor, OverviewLanguageBridge.GetDisplayName(mod), ModActionEntries(mod, true));
    }

    /// <summary>
    /// 1.1.0（MCE）：去掉变动账本（蓝点）、更新（绿点）、冲突建议；「更改模组信息」（管理中心）换成「编辑名称」「编辑简介」两项
    /// （11-15 决定：手柄在行菜单里给这两项，不要连按两次 A）；「版本与更新」改「版本信息」（只看本地版本）。
    /// 第 8b 章加「移动到分组」（Q5：不受铅笔开关限制；IGMM 没有这一项，只能开编辑模式拖动）。
    /// </summary>
    private List<UiMenuEntry> ModActionEntries(OverviewMod mod, bool gamepad)
    {
        var entries = new List<UiMenuEntry>();
        if (gamepad && mod.Outer != null)
        {
            var outer = mod.Outer;
            entries.Add(new UiMenuEntry { Label = Loc.T("Amber · Go to the outer mod", "琥珀色 · 跳到外层模组", "琥珀色 · 跳到外層模組"), Role = UiActionRole.Neutral, Invoke = () => FocusMod(outer) });
        }
        entries.Add(new UiMenuEntry { Label = Loc.T("Edit name", "编辑名称", "編輯名稱"), Role = UiActionRole.Edit, Invoke = () => EditName(mod) });
        entries.Add(new UiMenuEntry { Label = Loc.T("Edit description", "编辑简介", "編輯簡介"), Role = UiActionRole.Edit, Invoke = () => EditDescription(mod) });
        entries.Add(new UiMenuEntry { Label = Loc.T("Move to group…", "移动到分组…", "移動到分組…"), Role = UiActionRole.Edit, Invoke = () => AssignGroup(mod) });
        entries.Add(new UiMenuEntry { Label = Loc.T("View mod dependencies","查看模组依赖项", "檢視模組依賴項"), Role = UiActionRole.Neutral,
            Invoke = () => ComponentDetailsView.Open(_repo, _mods, null, OverviewDependencies.Identity(mod), _infrastructureGraph, _infrastructure) });
        entries.Add(new UiMenuEntry { Label = Loc.T("Version information", "版本信息", "版本資訊"), Role = UiActionRole.Neutral, Invoke = () => ShowVersion(mod) });
        return entries;
    }
}
internal static partial class OverviewUi
{
    internal static void PointerDropdown(Vector2 position, Camera camera, string title, UiMenuEntry[] entries)
    {
        if (ModalParent is not RectTransform host) return;
        var anchor = Rect("PointerMenuAnchor", host);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(host, position, camera, out var local);
        Place(anchor, local.x - host.rect.xMin, host.rect.yMax - local.y, 1, 1);
        var menu = CompactDropdown(anchor, title, entries);
        if (menu == null) { Object.Destroy(anchor.gameObject); return; }
        menu.gameObject.AddComponent<PointerMenuAnchorLifetime>().Anchor = anchor.gameObject;
    }
}
internal sealed class PointerMenuAnchorLifetime : MonoBehaviour
{
    internal GameObject? Anchor;
    private void OnDestroy() { if (Anchor != null) Object.Destroy(Anchor); }
}
