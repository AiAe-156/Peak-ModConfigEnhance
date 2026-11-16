#nullable enable
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace ModConfigEnhance.Overview;

// 1.1.0（MCE，概览页第 7 章）：照抄 IGMM 后裁剪：没有方案草稿，去掉 ProfileItemPending 与 ProfilePendingMarkers；
// 启用勾只读，状态只看扫描结果（文件是否启用），行内文字 / 图标在已停用时变灰（同 IGMM）。
/// <summary>State-only patch of an existing resident row. Never rebuilds its control tree.</summary>
internal sealed class OverviewEnablementBinding : MonoBehaviour
{
    internal OverviewMod Mod=null!;
    internal Func<bool> Target=null!;
    internal Toggle? Checkbox;
    private readonly Dictionary<TMP_Text,Color> _text=new();
    private readonly Dictionary<OverviewIconGraphic,Color> _icons=new();
    private readonly Dictionary<TMP_Text,Color> _chipInk=new();
    private EnablementBorderTint? _border;
    private bool _hasState, _lastTarget;
    internal void Capture()
    {
        foreach(var text in GetComponentsInChildren<TMP_Text>(true))if(!_text.ContainsKey(text))_text[text]=text.color;
        // 1.1.0（MCE，实测修正）：主页图标的颜色表示网址种类（OverviewRowActions.FixedInk），停用行也不改灰。
        foreach(var icon in GetComponentsInChildren<OverviewIconGraphic>(true))if(!_icons.ContainsKey(icon) && icon.GetComponent<OverviewRowActions.FixedInk>()==null)_icons[icon]=icon.color;
        // 1.1.0（MCE）：行内没有来源标签（RoundedChip），不用保留标签字色。
        _border=Checkbox != null ? Checkbox.GetComponent<EnablementBorderTint>() : null;
        _hasState=false;
    }
    internal void Refresh()
    {
        bool target=Target();
        if(Checkbox!=null)OverviewUi.SyncCheck(Checkbox, target);
        if(_hasState && target==_lastTarget)return;
        _hasState=true;_lastTarget=target;
        if(Checkbox!=null)
        {
            var tint=ModEnablementPresentation.Tint(Mod);
            if(_border==null)_border=Checkbox.GetComponent<EnablementBorderTint>();
            if(_border!=null)_border.Color=tint;
            if(Checkbox.targetGraphic!=null)Checkbox.targetGraphic.color=tint;
        }
        foreach(var entry in _text)if(entry.Key!=null)entry.Key.color=target?entry.Value:OverviewUi.Muted;
        foreach(var entry in _icons)if(entry.Key!=null)entry.Key.color=target?entry.Value:OverviewUi.Muted;
        foreach(var entry in _chipInk)if(entry.Key!=null)entry.Key.color=entry.Value;
    }
}
