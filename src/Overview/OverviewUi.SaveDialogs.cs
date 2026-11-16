#nullable enable
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ModConfigEnhance.Overview;

internal static partial class OverviewUi
{
    private static Color DialogSurface => ModConfigLook.Surface(new(.105f, .12f, .135f, 1));
    private static Color DialogInset => ModConfigLook.Surface(new(.065f, .08f, .095f, 1));

    /// <summary>Title, one-line or two-line subtitle and divider. <paramref name="dividerY"/> lets a dialog that needs the
    /// room (the folder-organization review) pull the divider up under a one-line subtitle.</summary>
    internal static void SaveDialogHeader(RectTransform panel, string title, string subtitle, bool compactSummary = false, float dividerY = 109)
    {
        panel.GetComponent<Image>().color = DialogSurface;
        var accent = Panel("Accent", panel, Blue); Place(accent, 0, 0, panel.rect.width, 3);
        var heading = Text(panel, title, 24); heading.richText = false;
        heading.enableAutoSizing = true; heading.fontSizeMin = 16; heading.fontSizeMax = 24;
        Place(heading.rectTransform, 24, 17, panel.rect.width - 48, 38);
        var summary = Text(panel, subtitle, 15); summary.color = Muted; summary.richText = false;
        summary.textWrappingMode = TextWrappingModes.Normal;
        Place(summary.rectTransform, 24, 59, panel.rect.width - 48, compactSummary ? 18 : 42);
        var divider = Panel("HeaderDivider", panel, ModConfigLook.Surface(new Color(1, 1, 1, .1f)));
        Place(divider, 24, dividerY, panel.rect.width - 48, 1);
        var footer = Panel("Footer", panel, DialogInset);
        footer.anchorMin = Vector2.zero; footer.anchorMax = new Vector2(1, 0); footer.pivot = Vector2.zero;
        footer.sizeDelta = new Vector2(0, 68); footer.anchoredPosition = Vector2.zero;
    }

    internal static Button SaveDialogAction(RectTransform panel, string label, Action action, float right, float width, UiActionRole role)
    {
        // Preserve the right-aligned action hierarchy on narrow windows as well.
        float scale = Mathf.Min(1, (panel.rect.width - 40) / 496);
        right = 20 + Mathf.Max(0, right - 20) * scale; width *= scale;
        var button = Button(panel, label, action, width, 36, role);
        Bottom((RectTransform)button.transform, panel.rect.width - right - width, 16);
        var caption = button.GetComponentInChildren<TMP_Text>(true);
        caption.enableAutoSizing = true; caption.fontSizeMin = 12; caption.fontSizeMax = 18;
        return button;
    }

    // 1.1.0（MCE，快捷键页第 5 章清理）：裁掉 horizontalReport（问题报告横向滚动，连同 IssueReportScrollRect）；MCE 只有「放弃修改？」确认会用到正文。
    internal static void SaveDialogBody(RectTransform panel, string body, bool attention = false, bool formatted = false, float top = 0)
    {
        var host = Panel("Message", panel, DialogInset); Fill(host);
        host.offsetMin = new Vector2(24, 82); host.offsetMax = new Vector2(-24, top > 0 ? -top : -124);
        if (attention)
        {
            var rail = Panel("Attention", host, Gold); rail.anchorMin = Vector2.zero; rail.anchorMax = new Vector2(0, 1);
            rail.pivot = Vector2.zero; rail.sizeDelta = new Vector2(3, 0); rail.anchoredPosition = Vector2.zero;
        }
        var viewport = Rect("Viewport", host); Fill(viewport, 14); viewport.gameObject.AddComponent<RectMask2D>();
        var text = Text(viewport, body, 18); text.richText = formatted;
        text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow;
        var content = text.rectTransform; content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0, 1); content.sizeDelta = Vector2.zero;
        text.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = host.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = content;
        scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40;
        ManagementUi.Scrollbar(host, scroll);
        var reader = host.gameObject.AddComponent<ControllerReadingSurface>(); reader.Scroll = scroll;
        reader.targetGraphic = host.GetComponent<Image>(); reader.transition = Selectable.Transition.None;
        GamepadSupport.MarkSelectable(host.gameObject, null, null);
        text.lineSpacing = 8;
        text.margin = new Vector4(0, 2, 12, 4);
    }
    // 1.1.0（MCE）：裁掉 ShowPresetSaveChoice（配置方案「保存预设」对话框；MCE 没有配置方案）。
}
