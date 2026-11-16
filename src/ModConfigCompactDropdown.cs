#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ModConfigEnhance.Overview;
using PEAKLib.ModConfig.Components;
using PEAKLib.UI.Elements;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ModConfigEnhance;

/// <summary>
/// Display-only adapter: native option indices/events remain authoritative. Do not replace the
/// multi-select filter, searchable/custom templates, image choices or long lists.
/// 补丁与判定照抄自完整版 In-Game Mod Manager；那边的弹层借用概览的下拉菜单，这里按同样的尺寸、颜色与导航自建一份。
/// </summary>
internal static class ModConfigCompactDropdown
{
    internal const int MaximumOptions = 12;

    internal static void Install(Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(TMP_Dropdown), nameof(TMP_Dropdown.Show)),
            prefix: new HarmonyMethod(typeof(ModConfigCompactDropdown), nameof(ShowPrefix)));
        harmony.Patch(AccessTools.Method(typeof(PeakChildPage), "GetParentPage", Type.EmptyTypes),
            prefix: new HarmonyMethod(typeof(ModConfigCompactDropdown), nameof(BackPrefix)) { priority = Priority.First });
    }

    internal static bool IsSimple(TMP_Dropdown dropdown)
    {
        if (dropdown.MultiSelect || dropdown.options.Count < 1 || dropdown.options.Count > MaximumOptions
            || dropdown.options.Any(option => option.image != null || string.IsNullOrWhiteSpace(option.text))) return false;
        // Search/custom editors may live beside the TMP component or inside its inactive template.
        if (dropdown.GetComponentInChildren<TMP_InputField>(true) != null
            || dropdown.GetComponentInChildren<InputField>(true) != null) return false;
        var template = dropdown.template;
        return template != null && template.GetComponentsInChildren<Toggle>(true).Length == 1
            && template.GetComponentsInChildren<Selectable>(true).All(control => control is Toggle or Scrollbar);
    }

    private static bool ShowPrefix(TMP_Dropdown __instance)
    {
        var dropdown = __instance;
        if (!dropdown.IsActive() || !dropdown.IsInteractable() || dropdown.IsExpanded || !IsSimple(dropdown)) return true;
        var page = dropdown.GetComponentInParent<PeakChildPage>();
        if (page == null) return true;
        // Sidebar controls are siblings of ModSettingsMenu, not children of that component.
        var menu = page.GetComponentInChildren<ModSettingsMenu>(true);
        if (menu == null || menu.MainPage == null || menu.MainPage.transform != page.transform
            || dropdown == menu.FilterDropdown?.Dropdown || ModConfigDropdownHost.AnyOpen) return true;
        var owner = page.GetComponent<ModConfigDropdownHost>() ?? page.gameObject.AddComponent<ModConfigDropdownHost>();
        try { return !owner.Open(dropdown); }
        catch (Exception ex)
        {
            owner.Close();
            Plugin.Log.LogWarning("[设置下拉] 紧凑菜单不可用，保留原生下拉：" + ex.Message);
            return true;
        }
    }

    private static bool BackPrefix(PeakChildPage __instance, ref (Zorro.UI.UIPage, Zorro.UI.PageTransistion) __result)
    {
        var owner = __instance.GetComponent<ModConfigDropdownHost>();
        if (owner == null || !owner.IsOpen) return true;
        owner.Close();
        __result = (__instance, GamepadSupport.NoopTransistion.Shared);
        return false;
    }
}

/// <summary>Owns the compact option list while a settings dropdown is open.</summary>
internal sealed class ModConfigDropdownHost : MonoBehaviour
{
    private static ModConfigDropdownHost? _current;
    internal static bool AnyOpen => _current != null && _current.IsOpen;

    private RectTransform? _host, _shade;
    private TMP_Dropdown? _dropdown;
    internal bool IsOpen => _shade != null && _shade.gameObject.activeInHierarchy;

    internal bool Open(TMP_Dropdown dropdown)
    {
        Close();
        if (_host == null)
        {
            _host = OverviewUi.Rect("MCE_SettingsDropdownHost", transform);
            _host.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            OverviewUi.Fill(_host);
        }
        _host.SetAsLastSibling();
        var host = _host; var anchor = (RectTransform)dropdown.transform;
        int selected = dropdown.value;
        // Snapshot option objects as well as indices: rebuilding settings while open must never
        // write an old menu index into a different option list.
        var options = dropdown.options.ToArray();
        // Unity objects: explicit null checks (?. does not stop on a destroyed object).
        SettingsUICell? cell = dropdown.GetComponentInParent<SettingsUICell>();
        string heading = cell != null && cell.m_text != null && !string.IsNullOrEmpty(cell.m_text.text)
            ? cell.m_text.text : Loc.T("Options", "选项", "選項");

        var corners = new Vector3[4]; anchor.GetWorldCorners(corners);
        var bottomLeft = host.InverseTransformPoint(corners[0]); var topRight = host.InverseTransformPoint(corners[2]);
        var anchorBox = new UiGeometry.Box(bottomLeft.x - host.rect.xMin, host.rect.yMax - topRight.y,
            topRight.x - bottomLeft.x, topRight.y - bottomLeft.y);
        var shade = OverviewUi.Panel("MCE_SettingsDropdown", host, ModConfigLook.Surface(new Color(0, 0, 0, .22f)));
        OverviewUi.Fill(shade); shade.SetAsLastSibling();
        _shade = shade; _dropdown = dropdown; _current = this;
        shade.gameObject.AddComponent<ClickCatcher>().Clicked = Close;
        var panel = OverviewUi.Panel("Options", shade, OverviewUi.Brown); panel.gameObject.AddComponent<RectMask2D>();
        panel.gameObject.AddComponent<ClickCatcher>(); // clicks on the list itself do not reach the shade
        panel.gameObject.AddComponent<FocusReturn>().Anchor = anchor;

        string Caption(int index) => (index == selected ? "✓ " : "") + options[index].text.Replace('\r', ' ').Replace('\n', ' ');
        var measure = OverviewUi.Text(panel, "", 16);
        float measured = Enumerable.Range(0, options.Length).Select(index => measure.GetPreferredValues(Caption(index)).x + 36)
            .Append(measure.GetPreferredValues(heading).x * 1.25f + 36).Max(); // 标题 19 号粗体，按 16 号量再放宽
        measure.gameObject.SetActive(false); Destroy(measure.gameObject);
        float availableWidth = Mathf.Max(1, host.rect.width - 16);
        float width = Mathf.Clamp(measured, Mathf.Min(260, availableWidth), Mathf.Min(420, availableWidth));
        float rowWidth = Mathf.Max(1, width - 16);
        var viewport = OverviewUi.Rect("Viewport", panel); OverviewUi.Fill(viewport, 8);
        viewport.gameObject.AddComponent<Image>().color = Color.clear; viewport.gameObject.AddComponent<RectMask2D>();
        var content = OverviewUi.Rect("Options", viewport); content.anchorMax = Vector2.one;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
        scroll.horizontal = false; scroll.inertia = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40;

        float y = 0;
        // 1.1.0 实测修正（2026-11-16）：标题行与概览的共用下拉一致——19 号粗体、深色底带加金线，比选项大、与选项区分开。
        var title = OverviewUi.Text(content, heading, 19); title.color = OverviewUi.Ivory; title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.MidlineLeft; title.enableAutoSizing = true;
        title.fontSizeMin = 13; title.fontSizeMax = 19;
        title.textWrappingMode = TextWrappingModes.NoWrap; title.overflowMode = TextOverflowModes.Ellipsis;
        float headingHeight = Mathf.Max(34, Mathf.Ceil(title.GetPreferredValues(heading).y) + 6);
        title.gameObject.SetActive(heading.Length > 0);
        if (heading.Length > 0)
        {
            var band = OverviewUi.Panel("TitleBand", content, ModConfigLook.Surface(new Color(.03f, .04f, .05f, .92f)));
            band.GetComponent<Image>().raycastTarget = false; OverviewUi.Place(band, 0, y, rowWidth, headingHeight); band.SetSiblingIndex(title.transform.GetSiblingIndex());
            var rule = OverviewUi.Panel("TitleRule", band, new Color(OverviewUi.Gold.r, OverviewUi.Gold.g, OverviewUi.Gold.b, .7f));
            rule.GetComponent<Image>().raycastTarget = false; OverviewUi.Place(rule, 0, headingHeight - 2, rowWidth, 2);
            OverviewUi.PlaceClipped(title, 12, y, Mathf.Max(1, rowWidth - 24), headingHeight); y += headingHeight + 6;
        }

        bool dark = ModConfigLook.IsDark;
        Color neutral = dark ? new Color32(0x3e, 0x33, 0x50, 255) : new Color32(0x3a, 0x47, 0x56, 255);
        var rows = new List<Button>();
        for (int i = 0; i < options.Length; i++)
        {
            int index = i; var option = options[i];
            bool Valid() => dropdown != null && dropdown.IsActive() && dropdown.IsInteractable()
                && dropdown.options.Count == options.Length && ReferenceEquals(dropdown.options[index], option);
            var rect = OverviewUi.Rect("Option_" + index, content);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = index == selected ? ModConfigLook.Surface(new Color(.20f, .48f, .60f))
                : Color.Lerp(ModConfigLook.Surface(new Color(.12f, .11f, .09f)), neutral, .35f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1); colors.selectedColor = colors.highlightedColor; button.colors = colors;
            button.onClick.AddListener(() =>
            {
                if (!Valid()) return;
                Close();
                dropdown.value = index; // uses native change listeners / settings persistence
                if (dropdown != null) dropdown.RefreshShownValue();
            });
            var label = OverviewUi.Text(button.transform, Caption(index), 16);
            label.color = OverviewUi.Ivory; label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = 16; label.margin = Vector4.zero;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            float rowHeight = Mathf.Max(32, Mathf.Ceil(label.GetPreferredValues(label.text).y) + 4);
            OverviewUi.Place(rect, 0, y, rowWidth, rowHeight); y += rowHeight + 2;
            OverviewUi.PlaceClipped(label, 16, 0, Mathf.Max(1, rowWidth - 32), rowHeight);
            GamepadSupport.MarkSelectable(button.gameObject, scroll, rect);
            rows.Add(button);
        }
        content.sizeDelta = new Vector2(0, Mathf.Max(1, y));
        panel.gameObject.AddComponent<Placement>().Initialize(host, panel, anchor, anchorBox, width, y + 16, Close);
        for (int i = 0; i < rows.Count; i++) rows[i].navigation = new Navigation {
            mode = Navigation.Mode.Explicit, selectOnUp = i > 0 ? rows[i - 1] : rows[rows.Count - 1],
            selectOnDown = i + 1 < rows.Count ? rows[i + 1] : rows[0]
        };
        // Begin on the selected value rather than always on the first option.
        if (rows.Count > 0 && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(rows[selected >= 0 && selected < rows.Count ? selected : 0].gameObject);
        return true;
    }

    internal void Close()
    {
        if (_shade != null) { _shade.gameObject.SetActive(false); Destroy(_shade.gameObject); }
        _shade = null; _dropdown = null;
        if (_current == this) _current = null;
    }

    private void LateUpdate()
    {
        if (_shade == null) return;
        if (_dropdown == null || !_dropdown.IsActive() || !_dropdown.IsInteractable()) Close();
    }
    private void OnDisable() => Close();

    private sealed class ClickCatcher : MonoBehaviour, IPointerClickHandler
    {
        internal Action? Clicked;
        public void OnPointerClick(PointerEventData data) => Clicked?.Invoke();
    }

    /// <summary>Closing the list gives the controller focus back to the dropdown that opened it.</summary>
    private sealed class FocusReturn : MonoBehaviour
    {
        internal RectTransform? Anchor;
        private void OnDisable()
        {
            var system = EventSystem.current;
            if (Anchor == null || !Anchor.gameObject.activeInHierarchy || system == null) return;
            var selected = system.currentSelectedGameObject;
            if (selected == null || selected.transform.IsChildOf(transform)) system.SetSelectedGameObject(Anchor.gameObject);
        }
    }

    /// <summary>Keeps the list under (or above) its dropdown, inside the page.</summary>
    private sealed class Placement : MonoBehaviour
    {
        private RectTransform _host = null!, _panel = null!, _anchor = null!;
        private UiGeometry.Box _anchorBox;
        private Vector2 _hostSize;
        private float _width, _height;
        private Action _close = null!;
        internal void Initialize(RectTransform host, RectTransform panel, RectTransform anchor, UiGeometry.Box anchorBox, float width, float height, Action close)
        { _host = host; _panel = panel; _anchor = anchor; _anchorBox = anchorBox; _width = width; _height = height; _close = close; Reposition(); }
        private void Reposition()
        {
            _hostSize = _host.rect.size;
            var bounds = UiGeometry.Popover(_hostSize.x, _hostSize.y, _anchorBox, _width, _height);
            OverviewUi.Place(_panel, bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }
        private void LateUpdate()
        {
            if (_host == null || _anchor == null || !_anchor.gameObject.activeInHierarchy) { _close(); return; }
            if (_hostSize != _host.rect.size) Reposition();
        }
    }
}
