#nullable enable
using System;

namespace ModConfigEnhance.Overview;

/// <summary>
/// 1.1.0（MCE，概览页第 7 章新写）：模组列表里「编辑名称 / 编辑简介」的小对话框（双击名称格 / 简介格，手柄在行菜单里选）。
/// 替换 IGMM 的「更改模组信息」（在管理中心，不搬）。写入走 <see cref="OverviewLanguageBridge.SetName"/> /
/// <see cref="OverviewLanguageBridge.SetDescription"/>：写进当前选的语言文件（Q6），与设置页模组名共用同一条译名；
/// 留空保存 = 删掉这条译名、恢复原文。写成功后 Loc.TranslationRevision 变化，列表、快捷键页、设置页各自按修订号刷新。
/// 没有草稿：保存即写文件。版本标签、别名（整合包作者功能）不做。
/// </summary>
internal static class OverviewModInfoDialog
{
    internal static void EditName(OverviewMod mod, Action<string> status, Action saved)
    {
        string raw = string.IsNullOrEmpty(mod.RawName) ? mod.Name : mod.RawName;
        string current = OverviewLanguageBridge.GetDisplayName(mod);
        Show(Loc.T("Edit mod name", "编辑模组名称", "編輯模組名稱"),
            Loc.F("Name in the current language file (original: {0}). Leave empty to restore the original.", "当前语言文件里的名称（原文：{0}）。留空保存即恢复原文。", "目前語言檔案裡的名稱（原文：{0}）。留空儲存即恢復原文。", raw),
            current == raw ? "" : current,
            value => (OverviewLanguageBridge.SetName(mod, value, out string error), error),
            status, saved, Loc.T("Name saved to the language file.", "名称已写入语言文件。", "名稱已寫入語言檔案。"));
    }

    internal static void EditDescription(OverviewMod mod, Action<string> status, Action saved)
    {
        string raw = string.IsNullOrEmpty(mod.RawDescription) ? mod.Description : mod.RawDescription;
        string current = OverviewLanguageBridge.GetDescription(mod);
        Show(Loc.T("Edit mod description", "编辑模组简介", "編輯模組簡介"),
            raw.Trim().Length > 0
                ? Loc.T("Description in the current language file. Leave empty to restore the original.", "当前语言文件里的简介。留空保存即恢复原文。", "目前語言檔案裡的簡介。留空儲存即恢復原文。")
                : Loc.T("This mod has no description of its own; the text is kept in the current language file. Leave empty to remove it.", "该模组自己没有简介，文字保存在当前语言文件里。留空保存即删除。", "該模組自己沒有簡介，文字保存在目前語言檔案裡。留空儲存即刪除。"),
            current == raw ? "" : current,
            value => (OverviewLanguageBridge.SetDescription(mod, value, out string error), error),
            status, saved, Loc.T("Description saved to the language file.", "简介已写入语言文件。", "簡介已寫入語言檔案。"));
    }

    private static void Show(string title, string label, string value, Func<string, (bool Ok, string Error)> write, Action<string> status, Action saved, string done)
    {
        // 「显示原文」时语言文件里的译名不显示，写了也看不到：先提醒，仍允许写（换回语言后生效）。
        if (Loc.ShowingOriginal)
            label += "\n" + Loc.T("Original text is shown now; the change appears after choosing a language.", "当前显示原文，选择语言后才看得到修改。", "目前顯示原文，選擇語言後才看得到修改。");
        OverviewUi.AskValidated(title, new[] { (label, value) },
            values =>
            {
                var result = write((values.Length > 0 ? values[0] : "").Trim());
                return result.Ok ? null : (result.Error.Length > 0 ? result.Error : Loc.T("Could not write the language file.", "写入语言文件失败。", "寫入語言檔案失敗。"));
            },
            _ => { status(done); saved(); },
            UiActionRole.Commit);
    }
}
