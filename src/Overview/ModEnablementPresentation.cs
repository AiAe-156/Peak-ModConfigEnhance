#nullable enable
using TMPro;
using UnityEngine;
namespace ModConfigEnhance.Overview;
// 1.1.0（MCE，概览页第 7 章）：照抄 IGMM 后裁成只读显示——MCE 不启停任何模组，没有方案草稿：
// 去掉 ProfileItemPending（待保存 / 待重启）、「目标状态」「配置写入」两行与「启用这份会停用其它副本」。
// 只说：当前是否加载、文件是否启用；被其他工具停用时附原因（OverviewCatalogue 的 ToggleReason）。
internal static class ModEnablementPresentation
{
    internal static readonly Color Enabled = new(.48f,.78f,.46f,1), Changing = new(.95f,.76f,.30f,1), Disabled = new(.73f,.73f,.69f,1);
    /// <summary>2.3.6: a resource pack has no DLL, so the loader never reports it as loaded. Its "current state" is
    /// whether its files are enabled; otherwise every enabled resource pack showed a grey box and
    /// "Enabled file did not load; see the game log".</summary>
    internal static bool Active(OverviewMod mod) => mod.ResourcePack ? mod.TargetEnabled : mod.Loaded;
    /// <summary>1.1.0（MCE）：「变化中」只剩「文件已停用但本次已加载」（重启后才卸下）与「文件已启用但本次未加载」两种不一致。</summary>
    internal static bool IsChanging(OverviewMod mod) => !mod.ResourcePack && mod.Loaded != mod.TargetEnabled;
    internal static Color Tint(OverviewMod mod) => IsChanging(mod) ? Changing : Active(mod) ? Enabled : Disabled;
    internal static string Detail(OverviewMod mod)
    {
        string State(bool value) => value ? Loc.T("Enabled","启用","啟用") : Loc.T("Disabled","禁用","停用");
        string ColorValue(string text, Color color) => "<color=#"+ColorUtility.ToHtmlStringRGB(color)+">"+text+"</color>";
        string result=mod.Name.Replace("<","＜")+"\n"+Loc.T("Current state: ","当前状态：","目前狀態：")+ColorValue(State(Active(mod)),Active(mod)?Enabled:Disabled)+"\n";
        result+=Loc.T("Files: ","文件：","檔案：")+ColorValue(State(mod.TargetEnabled),IsChanging(mod)?Changing:mod.TargetEnabled?Enabled:Disabled);
        if(!mod.ResourcePack&&mod.Loaded&&!mod.TargetEnabled)result+="\n"+Loc.T("The files were disabled after the game started; the mod stays loaded until restart.","文件在游戏启动后被停用；重启前本次仍在运行。","檔案在遊戲啟動後被停用；重新啟動前本次仍在執行。");
        if(!mod.ResourcePack&&!mod.Loaded&&mod.TargetEnabled)result+="\n"+Loc.T("Enabled file did not load; see the game log.","文件已启用但本次未加载，原因见游戏日志。","檔案已啟用但本次未載入，原因見遊戲日誌。");
        if(mod.DuplicateCopy)result+="\n"+Loc.T("This plugin is installed in several folders.","该模组装了多份副本。","該模組安裝了多份副本。");
        if(!mod.CanToggle && !string.IsNullOrWhiteSpace(mod.ToggleReason))result+="\n"+mod.ToggleReason;
        result+="\n"+Loc.T("Read only: ModConfig Enhance never enables or disables mods.","只读：ModConfig Enhance 不会启停模组。","唯讀：ModConfig Enhance 不會啟停模組。");
        return result;
    }
}
