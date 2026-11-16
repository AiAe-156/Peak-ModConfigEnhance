#nullable enable
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModConfigEnhance.Overview;

/// <summary>Shared row icon geometry and presentation; callers supply every business action and hint.</summary>
internal static class OverviewRowActions
{
    internal const float Size = 30;
    private static Color Disabled => ModConfigLook.Surface(new(.37f, .39f, .37f));

    internal static Button Settings(Transform parent, float x, float y, Action clicked, bool enabled,
        string hint, Action<GameObject, string> bindHint)
        => Create(parent, "gear", x, y, clicked, enabled, OverviewUi.Ivory, hint, bindHint);

    /// <summary>
    /// 1.1.0（MCE，实测修正）：主页图标只按网址分三种颜色——Thunderstore 青绿、GitHub 近白浅灰、没填或认不出来暗灰。
    /// 原先所有行都传 identified=false（恒暗灰），列表又对「已停用」行另染蓝、启用状态绑定再把停用行的图标改成 Muted，
    /// 亮暗跟着启停走、跟能不能点对不上。现在图标挂 <see cref="FixedInk"/>，启停绑定不再改它；悬停 / 按下 / 手柄焦点
    /// 只改按钮底板（UiActionStyle / ColorTint），图标颜色始终是这一种。
    /// </summary>
    internal static Button Home(Transform parent, float x, float y, Action clicked, string? url,
        string hint, Action<GameObject, string> bindHint)
    {
        var kind = HomeKindOf(url);
        var button = Create(parent, "home", x, y, clicked, kind != HomeKind.None, HomeInk(kind), hint, bindHint);
        foreach (var glyph in button.GetComponentsInChildren<OverviewIconGraphic>(true))
            if (glyph.GetComponent<FixedInk>() == null) glyph.gameObject.AddComponent<FixedInk>();
        return button;
    }

    internal enum HomeKind { None, Other, Thunderstore, GitHub }
    /// <summary>Thunderstore 商标色附近的青绿；内容色，两套主题都不变（同 OverviewUi.Blue）。</summary>
    internal static readonly Color ThunderstoreInk = new Color32(0x2e, 0xc8, 0xa8, 255);
    internal static readonly Color GitHubInk = new(.88f, .89f, .90f);
    /// <summary>只认 http/https。thunderstore.io（含子域）= Thunderstore，github.com（含 www.）= GitHub；其余网址可点但算「认不出来」。</summary>
    internal static HomeKind HomeKindOf(string? url)
    {
        if (!Uri.TryCreate(url ?? "", UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)) return HomeKind.None;
        string host = uri.Host.ToLowerInvariant();
        if (host == "thunderstore.io" || host.EndsWith(".thunderstore.io", StringComparison.Ordinal)) return HomeKind.Thunderstore;
        if (host == "github.com" || host == "www.github.com") return HomeKind.GitHub;
        return HomeKind.Other;
    }
    internal static Color HomeInk(HomeKind kind) => kind switch
    {
        HomeKind.Thunderstore => ThunderstoreInk,
        HomeKind.GitHub => GitHubInk,
        _ => Disabled
    };

    /// <summary>标记：这个图标的颜色表示种类，不随行的启停状态变灰（<see cref="OverviewEnablementBinding"/> 跳过它）。</summary>
    internal sealed class FixedInk : MonoBehaviour { }

    internal static Button Folder(Transform parent, float x, float y, Action clicked, bool enabled,
        string hint, Action<GameObject, string> bindHint)
        => Create(parent, "folder", x, y, clicked, enabled, OverviewUi.Ivory, hint, bindHint);

    internal static void HighlightSelf(TMP_Text text)
    {
        text.color = SelfRowColor(); // 1.1.0（MCE）：MCE 的 ModConfigSidebarWidgets 没有 SelfRowColor()，用本类私有的同色实现
        if (text is not TextMeshProUGUI caption) return;
        var tint = caption.GetComponent<ModConfigSidebarWidgets.SelfRowTint>()
            ?? caption.gameObject.AddComponent<ModConfigSidebarWidgets.SelfRowTint>();
        tint.Text = caption;
    }

    private static Button Create(Transform parent, string icon, float x, float y, Action clicked, bool enabled,
        Color activeColor, string hint, Action<GameObject, string> bindHint)
    {
        var button = OverviewUi.IconButton(parent, icon, clicked, Size, Size);
        OverviewUi.Place((RectTransform)button.transform, x, y, Size, Size);
        button.interactable = enabled;
        button.gameObject.AddComponent<OverviewRowColumn>().Column = icon == "gear" ? ListColumnPolicy.Settings : icon == "home" ? ListColumnPolicy.Home : ListColumnPolicy.Folder;
        Color color = enabled ? activeColor : Disabled;
        foreach (var text in button.GetComponentsInChildren<TextMeshProUGUI>(true)) text.color = color;
        foreach (var glyph in button.GetComponentsInChildren<OverviewIconGraphic>(true)) glyph.color = color;
        if (!string.IsNullOrWhiteSpace(hint)) bindHint(button.gameObject, hint);
        return button;
    }

    /// <summary>
    /// 1.1.0（MCE）：对应 IGMM 的 ModConfigSidebarWidgets.SelfRowColor()（同一套 SelfRowTint 颜色，未选中态）；MCE 侧栏没有这个方法，留在这里。
    /// </summary>
    private static Color SelfRowColor() => ModConfigLook.IsDark ? ModConfigSidebarWidgets.SelfRowTint.Night : ModConfigSidebarWidgets.SelfRowTint.Day;
}