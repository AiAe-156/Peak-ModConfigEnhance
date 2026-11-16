#nullable enable
using UnityEngine;
using UnityEngine.UI;
namespace ModConfigEnhance.Overview;
// 1.1.0（MCE，概览页第 7 章）：照抄 IGMM 后裁成只读显示：没有启停草稿，去掉 RefreshEnablementRows 里的待保存角标与整合包冲突；
// 勾选框只按扫描结果着色（ModEnablementPresentation），悬停 / 手柄聚焦给出只读说明。另把 IGMM .CheckProgress.cs 里唯一还要用的
// SingleLine 放到这里（那个文件是更新检查进度，不搬）。
internal sealed partial class OverviewListView
{
    private void RefreshEnablementRows()
    {
        _dependencyHints.InvalidateDisplay();
        foreach(var row in _virtualRows)
        {
            if(row.Mod==null || row.View==null)continue;
            var binding=row.View.GetComponent<OverviewEnablementBinding>();
            if(binding!=null)binding.Refresh();
        }
    }
    private void BuildEnablementStatus(Toggle checkbox, OverviewMod mod)
    {
        Color tint=ModEnablementPresentation.Tint(mod);
        var colors=checkbox.colors;colors.normalColor=colors.highlightedColor=colors.selectedColor=colors.pressedColor=colors.disabledColor=Color.white;checkbox.colors=colors;
        var border=checkbox.gameObject.AddComponent<EnablementBorderTint>();border.Target=checkbox.targetGraphic;border.Color=tint;
        if(checkbox.targetGraphic!=null)checkbox.targetGraphic.color=tint;
        AddHint(checkbox.gameObject,()=>ModEnablementPresentation.Detail(mod));
    }
    internal static string SingleLine(string text) => text.Replace("\r\n", " · ").Replace("\r", " · ")
        .Replace("\n", " · ").Replace((char)0x2028, ' ').Replace((char)0x2029, ' ');
}
