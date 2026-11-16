#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using PEAKLib.ModConfig;
using PEAKLib.ModConfig.Components;
using PEAKLib.UI.Elements;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zorro.UI;
using Zorro.Settings;

namespace ModConfigEnhance.Overview;

/// <summary>Scope the native settings return path to one explicit overview navigation.</summary>
internal static class OverviewSettingsNavigator
{
    private static Harmony? _harmony;
    private static MethodInfo? _parentMethod;
    private static readonly MethodInfo PostfixMethod = AccessTools.Method(typeof(OverviewSettingsNavigator), nameof(ParentPostfix));
    private static Context? _context;
    private static bool _registered;
    // 1.1.0（MCE 第 5 章）：不可用原因存成枚举，显示时再取当前语言（原来在 Awake 时就用 Loc.T 定死，换语言不跟）。
    private enum Unavailability { None, NotInitialized, Missing, Interface }
    private static Unavailability _unavailable = Unavailability.NotInitialized;
    /// <summary>齿轮不可用的原因（当前语言）；可用时为空串。快捷键页把它写进灰齿轮的说明，免得被当成「该模组没有设置项」。</summary>
    internal static string Unavailable => _registered ? "" : _unavailable switch
    {
        Unavailability.Missing => Loc.T("Opening mod settings from here needs ModConfig and PEAKLib.UI.", "从这里打开模组设置需要 ModConfig 和 PEAKLib.UI。", "從這裡開啟模組設定需要 ModConfig 和 PEAKLib.UI。"),
        Unavailability.Interface => Loc.T("This ModConfig / PEAKLib.UI version is not compatible with opening mod settings from here.", "当前 ModConfig / PEAKLib.UI 版本与这里的跳转不兼容。", "目前 ModConfig / PEAKLib.UI 版本與這裡的跳轉不相容。"),
        _ => Loc.T("Mod settings navigation has not been initialized.", "模组设置导航尚未初始化。", "模組設定導覽尚未初始化。"),
    };
    private static readonly List<SettingsPageLifetime> Lifetimes = new();
    private const string UiGuid = "com.github.PEAKModding.PEAKLib.UI";
    private sealed class Context
    {
        internal OverviewPage Origin = null!;
        internal ModSettingsMenu Menu = null!;
        internal PeakChildPage Page = null!;
        internal UIPageHandler Handler = null!;
        internal UIPage? Parent;
        internal string Search = "", SearchText = "", Section = "", SelectedMod = "";
        internal int Filter;
        internal bool Restored;
        internal ModdedTABSButton? Selected;
        internal Vector2 SidebarScroll, OptionsScroll;
        internal readonly Dictionary<GameObject, bool> Visibility = new();
        internal readonly Dictionary<string, string?> SectionMemory = new(StringComparer.Ordinal);
    }
    internal static void Register(Harmony harmony)
    {
        if (_registered) return;
        // 1.1.0（MCE 第 5 章，二次审查）：IGMM 要求版本号逐字等于 ModConfig 1.8.2 / PEAKLib.UI 1.7.2，但 MCE 的 manifest
        // 声明 PEAKLib.UI 1.7.1，且 MCE 其他补丁对版本不符只记日志、照常挂载。这里也改成同样做法：版本只记日志，
        // 真正的护栏是下面的结构检查（方法 / 字段找不到就不挂）。
        if (!Chainloader.PluginInfos.TryGetValue(ModConfigSupport.Guid, out var modConfig) || !Chainloader.PluginInfos.TryGetValue(UiGuid, out var ui))
        { _unavailable = Unavailability.Missing; return; }
        string modConfigVersion = modConfig.Metadata.Version.ToString(), uiVersion = ui.Metadata.Version.ToString();
        if (modConfigVersion != "1.8.2" || uiVersion != "1.7.2")
            Plugin.Log.LogInfo($"[模组概览] 齿轮跳转按 ModConfig 1.8.2 / PEAKLib.UI 1.7.2 核对，当前是 {modConfigVersion} / {uiVersion}；结构检查通过就照常挂载。");
        try
        {
            _parentMethod = AccessTools.Method(typeof(PeakChildPage), "GetParentPage", Type.EmptyTypes);
            if (_parentMethod == null || AccessTools.Field(typeof(ModSettingsMenu), "search") == null ||
                AccessTools.Field(typeof(ModSettingsMenu), "selectedSection") == null || AccessTools.Field(typeof(ModSettingsMenu), "selectedMod") == null)
            { _unavailable = Unavailability.Interface; return; }
            _harmony = harmony;
            harmony.Patch(_parentMethod, postfix: new HarmonyMethod(PostfixMethod) { priority = Priority.Last });
            _registered = true; _unavailable = Unavailability.None;
        }
        catch (Exception ex)
        {
            _unavailable = Unavailability.Interface;
            Plugin.Log.LogWarning("[模组概览] 齿轮跳转挂载失败：" + ex.Message);
        }
    }
    /// <summary>
    /// 实测修正（用户 2026-11-16）：把焦点放到模组设置页表头的「跳转到快捷键设置页」（<c>JumpHotkeysHit</c>，
    /// ModConfigSidebarWidgets.BuildHeader 建的）。设置页进场时可能自己再挑一次焦点，手柄态下接下来两帧再放一次。找不到返回 false。
    /// </summary>
    internal static bool FocusJumpHotkeys(UIPage? page)
    {
        if (page == null || EventSystem.current == null) return false;
        var hit = page.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b != null && b.name == "JumpHotkeysHit" && b.gameObject.activeInHierarchy && b.IsInteractable());
        if (hit == null) return false;
        EventSystem.current.SetSelectedGameObject(hit.gameObject);
        if (GamepadSupport.IsGamepad && hit.GetComponent<SelectForFrames>() == null) hit.gameObject.AddComponent<SelectForFrames>();
        return true;
    }
    /// <summary>挂上后的两帧里把焦点留在自己身上，然后自行移除（设置页进场时的自动选焦不会把它盖掉）。</summary>
    private sealed class SelectForFrames : MonoBehaviour
    {
        private int _left = 2;
        private void LateUpdate()
        {
            var system = EventSystem.current;
            if (system != null && gameObject.activeInHierarchy && system.currentSelectedGameObject != gameObject) system.SetSelectedGameObject(gameObject);
            if (--_left <= 0) Destroy(this);
        }
    }
    internal static bool Open(OverviewPage origin, OverviewMod? mod, out string error)
    {
        error = "";
        if (!_registered) { error = Unavailable; return false; }
        var pluginIds = mod == null ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(mod.PluginGuids.Append(mod.Guid).Where(guid =>
                Chainloader.PluginInfos.TryGetValue(guid, out var plugin) && plugin.Instance != null), StringComparer.OrdinalIgnoreCase);
        if (origin == null || mod != null && (!mod.Loaded || pluginIds.Count == 0))
        { error = Loc.T("The mod is not currently loaded.", "该模组当前未加载，重启生效后再打开设置。", "該模組目前未載入，重新啟動生效後再開啟設定。"); return false; }
        Context? pending = null;
        try
        {
            UIPageHandler handler = HandlerFor(origin);
            if (handler == null) throw new InvalidOperationException(Loc.T("The overview menu could not be found.", "找不到概览所属菜单。", "找不到概覽所屬的選單。"));
            if (handler.currentPage != origin) throw new InvalidOperationException(Loc.T("The overview is no longer the current page. Reopen it and try again.", "概览已不是当前页面，请重新打开概览后再试。", "概覽已不是目前頁面，請重新開啟概覽後再試。"));
            ModSettingsMenu? menu = handler.GetComponentsInChildren<ModSettingsMenu>(true)
                .FirstOrDefault(m => m != null && m.MainPage != null && HandlerFor(m.MainPage) == handler);
            if (menu == null || menu.MainPage == null || menu.ModTabs == null || menu.ModTabController == null ||
                menu.SectionTabs == null || menu.SectionTabController == null) throw new InvalidOperationException(Loc.T("The mod settings page has not been created for this menu.", "此菜单的模组设置页尚未建立。", "此選單的模組設定頁尚未建立。"));
            menu.RefreshSettings();
            // Native categories and row names identify the destination; translated labels do not.
            // 1.1.0（MCE，概览页第 6 章）：扫描结果里 PluginGuids 是整个资料包的 GUID；同一文件夹有几个插件时先找这一行自己的 GUID，
            // 没有才退到同包其他插件（IGMM 直接取设置列表里第一个匹配的，可能跳到兄弟插件的设置页）。
            static bool Shown(IBepInExProperty p) => !(p is IConditionalSetting conditional) || conditional.ShouldShow();
            string? category = mod == null ? menu.selectedMod
                : menu.settings?.Where(p => p.Pluginfo != null && string.Equals(p.Pluginfo.Metadata.GUID, mod.Guid, StringComparison.OrdinalIgnoreCase) && Shown(p))
                    .Select(p => p.GetCategory()).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))
                ?? menu.settings?.Where(p => p.Pluginfo != null && pluginIds.Contains(p.Pluginfo.Metadata.GUID) && Shown(p))
                    .Select(p => p.GetCategory()).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
            if (mod != null && string.IsNullOrWhiteSpace(category)) throw new InvalidOperationException(Loc.F("This mod has no currently displayable ModConfig settings (plugin GUID: {0}).", "该模组未向 ModConfig 提供当前可显示的设置项（插件 GUID：{0}）。", "該模組未向 ModConfig 提供目前可顯示的設定項（插件 GUID：{0}）。", mod.Guid));
            ModdedTABSButton? target = menu.ModTabController.Tabs.Where(row => row != null && (mod == null || row.name == category))
                .Select(row => row.GetComponent<ModdedTABSButton>()).FirstOrDefault(button => button != null && button.category == category);
            if (target == null && mod == null) target = menu.ModTabController.Tabs.Where(row => row != null).Select(row => row.GetComponent<ModdedTABSButton>()).FirstOrDefault(button => button != null);
            if (target == null) throw new InvalidOperationException(Loc.T("No mod settings row matches this plugin identity.", "找不到与插件身份对应的模组设置行。", "找不到與插件身分對應的模組設定列。"));
            string selectedCategory = target.category;
            ClearOrigin();
            var lifetime = menu.MainPage.GetComponent<SettingsPageLifetime>() ?? menu.MainPage.gameObject.AddComponent<SettingsPageLifetime>();
            lifetime.Page = menu.MainPage;
            Lifetimes.RemoveAll(component => component == null);
            if (!Lifetimes.Contains(lifetime)) Lifetimes.Add(lifetime);
            Context? retained = lifetime.PendingRestore;
            TMP_InputField? search = SearchField(menu.MainPage);
            ScrollRect? scroll = menu.ModTabController.GetComponent<ScrollRect>();
            ScrollRect? optionsScroll = menu.Content != null ? menu.Content.GetComponentInParent<ScrollRect>(true) : null;
            pending = new Context { Origin = origin, Menu = menu, Page = menu.MainPage, Handler = handler, Parent = menu.MainPage.ParentPage,
                Search = menu.search, SearchText = search != null ? search.text : "", Section = menu.selectedSection,
                SelectedMod = menu.selectedMod, Filter = menu.FilterValue,
                // Unity 判空（第 5 章）：RefreshSettings 若重建了页签，selectedButton 是已销毁对象，?? 不会换成后备值。
                Selected = menu.ModTabs.selectedButton != null ? menu.ModTabs.selectedButton : menu.ModTabController.Tabs.Where(row => row != null)
                    .Select(row => row.GetComponent<ModdedTABSButton>()).FirstOrDefault(button => button != null),
                SidebarScroll = retained?.SidebarScroll ?? (scroll != null ? scroll.normalizedPosition : Vector2.up),
                OptionsScroll = retained?.OptionsScroll ?? (optionsScroll != null ? optionsScroll.normalizedPosition : Vector2.up) };
            foreach (GameObject row in menu.ModTabController.Tabs.Where(row => row != null))
            {
                pending.Visibility[row] = row.activeSelf;
                pending.SectionMemory[row.name] = ModConfigTreeLayoutPatch.SectionMemory.TryGetValue(row.name, out string? section) ? section : null;
            }
            // Set transient fields without asking an inactive page to run coroutines.
            menu.search = ""; menu.FilterValue = 31; menu.selectedSection = ""; menu.selectedMod = selectedCategory;
            if (search != null) search.SetTextWithoutNotify("");
            if (menu.FilterDropdown != null && menu.FilterDropdown.Dropdown != null) menu.FilterDropdown.Dropdown.SetValueWithoutNotify(31);
            foreach (GameObject row in menu.ModTabController.Tabs.Where(row => row != null)) row.SetActive(true);
            SetSelectedWithoutRebuild(menu, target);
            // The ordinary button sets ParentPage only when clicked, so a first overview visit can find it null.
            menu.MainPage.ParentPage = origin;
            _context = pending; handler.onPageTransition += OnTransition;
            origin.MarkReturnFromSettings();
            handler.TransistionToPage(menu.MainPage, new SetActivePageTransistion());
            if (handler.currentPage != menu.MainPage || menu.ModTabs.selectedButton != target || menu.selectedMod != selectedCategory)
                throw new InvalidOperationException(Loc.T("ModConfig could not remain on this mod's settings page. Check that the mod still provides visible settings.", "ModConfig 未能停留在该模组的设置页，请确认该模组仍有可显示的设置项。", "ModConfig 未能停留在該模組的設定頁，請確認該模組仍有可顯示的設定項。"));
            // 实测修正（用户 2026-11-16）：从「模组设置」页签进来（不指定模组）时，焦点停在表头「跳转到快捷键设置页」。
            if (mod == null && FocusJumpHotkeys(menu.MainPage)) return true;
            var selectable = target.GetComponent<Selectable>();
            if (selectable != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            return true;
        }
        catch (Exception ex)
        {
            if (pending != null)
            {
                try
                {
                    if (pending.Handler != null && pending.Origin != null && pending.Handler.currentPage == pending.Page)
                        pending.Handler.TransistionToPage(pending.Origin, new SetActivePageTransistion());
                    else if (pending.Origin != null && !pending.Origin.gameObject.activeSelf)
                    {
                        if (pending.Page != null) pending.Page.gameObject.SetActive(false);
                        pending.Origin.gameObject.SetActive(true); pending.Origin.OnPageEnter(); pending.Origin.OnPageEntered();
                    }
                }
                catch (Exception rollback) { Plugin.Log.LogWarning("[模组概览] 返回概览失败：" + rollback.Message); }
                if (ReferenceEquals(_context, pending)) ClearOrigin();
                else if (pending.Menu != null && pending.Page != null) Restore(pending);
            }
            error = ex.GetBaseException().Message; return false;
        }
    }
    private static UIPageHandler HandlerFor(UIPage page) => page.GetPageHandler<UIPageHandler>() ?? page.GetComponentInParent<UIPageHandler>(true);

    /// <summary>
    /// 当前向 ModConfig 提供了可显示设置项的插件 GUID。列表用它把没设置项的齿轮置灰。
    /// 直接读取已登记的 ModConfig 项，不依赖设置页是否已打开；空集合不提供可用入口。
    /// </summary>
    internal static HashSet<string> SettingsOwners(OverviewPage origin)
        => origin == null ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : SettingsOwners();

    internal static HashSet<string> SettingsOwners()
    {
        var owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!_registered) return owners;
        try
        {
            var handler = SettingsHandler.Instance;
            if (handler == null) return owners;
            foreach (var setting in handler.GetAllSettings().OfType<IBepInExProperty>())
            {
                try {
                    if (setting.Pluginfo == null) continue;
                    if (setting is IConditionalSetting conditional && !conditional.ShouldShow()) continue;
                    if (!string.IsNullOrWhiteSpace(setting.GetCategory())) owners.Add(setting.Pluginfo.Metadata.GUID);
                }
                catch (Exception ex) { Plugin.Log.LogDebug("[模组概览] 跳过无法读取的设置项：" + ex.Message); }
            }
        }
        catch (Exception ex) { Plugin.Log.LogDebug("[模组概览] 读取可配置模组失败：" + ex.Message); }
        return owners;
    }
    private static TMP_InputField? SearchField(PeakChildPage page)
    {
        Transform child = page.transform.Find("SearchInput"); return child != null ? child.GetComponentInChildren<TMP_InputField>(true) : null;
    }
    private static void ParentPostfix(PeakChildPage __instance, ref (UIPage, PageTransistion) __result)
    {
        Context? context = _context;
        // GamepadSupport's consumed Back returns this page and must keep its full hierarchy behavior.
        if (context == null || context.Origin == null || context.Page != __instance || context.Handler == null || context.Handler.currentPage != __instance || __result.Item1 == __instance) return;
        UIPage origin = context.Origin;
        // Keep the context until the page really leaves. A parent-page query alone must not consume it.
        __result = (origin, new SetActivePageTransistion());
    }
    private static void OnTransition(UIPage destination)
    {
        Context? context = _context;
        if (context == null || destination == context.Page) return;
        ClearOrigin();
    }
    private static void Restore(Context context)
    {
        if (context.Restored || context.Menu == null || context.Page == null) return;
        try
        {
            ModSettingsMenu menu = context.Menu;
            menu.MainPage.ParentPage = context.Parent;
            menu.search = context.Search; menu.selectedSection = context.Section;
            menu.selectedMod = string.IsNullOrEmpty(context.SelectedMod) && context.Selected != null ? context.Selected.category : context.SelectedMod;
            menu.FilterValue = context.Filter;
            TMP_InputField? input = SearchField(context.Page); if (input != null) input.SetTextWithoutNotify(context.SearchText);
            if (menu.FilterDropdown != null && menu.FilterDropdown.Dropdown != null) menu.FilterDropdown.Dropdown.SetValueWithoutNotify(context.Filter);
            RestoreSectionMemory(context);
            SetSelectedWithoutRebuild(menu, context.Selected);
            if (menu.gameObject.activeInHierarchy && context.Selected != null)
            {
                menu.ModTabs.Select(context.Selected);
                RestoreSection(context); RestoreSectionMemory(context);
            }
            foreach (var row in context.Visibility) if (row.Key != null) row.Key.SetActive(row.Value);
            RestoreScroll(context);
            // OnEnable rebuilds section rows; restore scrolling after that layout has settled.
            SettingsPageLifetime? lifetime = context.Page.GetComponent<SettingsPageLifetime>();
            if (lifetime != null) lifetime.SetPendingRestore(context);
            context.Restored = true;
        }
        catch (Exception ex) { Plugin.Log.LogWarning("[模组概览] 还原模组设置页状态失败：" + ex.Message); }
    }
    private static void Detach(Context context) { if (context.Handler != null) context.Handler.onPageTransition -= OnTransition; }
    private static void SetSelectedWithoutRebuild(ModSettingsMenu menu, ModdedTABSButton? selected)
    {
        if (menu.ModTabs == null) return;
        if (menu.ModTabs.selectedButton != null) menu.ModTabs.selectedButton.Selected = false;
        menu.ModTabs.selectedButton = selected!; // Restore the native nullable "no selected row" state.
        if (selected != null) selected.Selected = true;
    }
    private static void RestoreSectionMemory(Context context)
    {
        foreach (var entry in context.SectionMemory)
            if (entry.Value == null) ModConfigTreeLayoutPatch.SectionMemory.Remove(entry.Key);
            else ModConfigTreeLayoutPatch.SectionMemory[entry.Key] = entry.Value;
    }
    private static void RestoreSection(Context context)
    {
        ModSettingsMenu menu = context.Menu;
        if (string.IsNullOrEmpty(context.Section) || menu.SectionTabController == null || menu.SectionTabs == null) return;
        ModdedTABSButton? section = menu.SectionTabController.Tabs.Where(row => row != null && row.name == context.Section)
            .Select(row => row.GetComponent<ModdedTABSButton>()).FirstOrDefault(button => button != null);
        if (section != null && menu.SectionTabs.selectedButton != section) menu.SectionTabs.Select(section);
    }
    private static void RestoreScroll(Context context)
    {
        ScrollRect? sidebar = context.Menu.ModTabController != null ? context.Menu.ModTabController.GetComponent<ScrollRect>() : null;
        ScrollRect? options = context.Menu.Content != null ? context.Menu.Content.GetComponentInParent<ScrollRect>(true) : null;
        if (sidebar != null) { sidebar.StopMovement(); sidebar.normalizedPosition = context.SidebarScroll; }
        if (options != null) { options.StopMovement(); options.normalizedPosition = context.OptionsScroll; }
    }
    private sealed class SettingsPageLifetime : MonoBehaviour
    {
        internal PeakChildPage? Page;
        private Context? _pendingRestore;
        internal Context? PendingRestore => _pendingRestore;
        internal void SetPendingRestore(Context context)
        {
            _pendingRestore = context;
            if (gameObject.activeInHierarchy) StartCoroutine(FinishRestore());
        }
        private void OnEnable() { if (_pendingRestore != null) StartCoroutine(FinishRestore()); }
        private void OnDisable()
        {
            StopAllCoroutines();
            if (_context != null && ReferenceEquals(_context.Page, Page)) ClearOrigin();
        }
        private void OnDestroy()
        {
            if (_context != null && ReferenceEquals(_context.Page, Page)) ClearOrigin();
            _pendingRestore = null;
        }
        private IEnumerator FinishRestore()
        {
            yield return null;
            Context? context = _pendingRestore;
            if (context == null || context.Menu == null || context.Page == null || context.Handler == null || context.Handler.currentPage != context.Page) yield break;
            // A new explicit overview jump supersedes a deferred ordinary-page restore.
            if (_context != null && _context.Page == context.Page) { _pendingRestore = null; yield break; }
            _pendingRestore = null;
            if (context.Menu.ModTabs == null || context.Menu.ModTabs.selectedButton != context.Selected) yield break;
            try
            {
                RestoreSection(context); RestoreSectionMemory(context);
                Canvas.ForceUpdateCanvases(); RestoreScroll(context);
            }
            catch (Exception ex) { Plugin.Log.LogWarning("[模组概览] 还原设置页滚动位置失败：" + ex.Message); }
        }
    }
    internal static void ClearOrigin(OverviewPage? origin = null)
    {
        Context? context = _context;
        if (context == null || !ReferenceEquals(origin, null) && !ReferenceEquals(context.Origin, origin)) return;
        _context = null; Detach(context); Restore(context);
    }
    internal static void Dispose()
    {
        ClearOrigin();
        foreach (SettingsPageLifetime component in Lifetimes) if (component != null) UnityEngine.Object.Destroy(component);
        Lifetimes.Clear();
        if (_harmony != null && _parentMethod != null) _harmony.Unpatch(_parentMethod, PostfixMethod);
        _registered = false; _harmony = null; _parentMethod = null; _unavailable = Unavailability.NotInitialized;
    }
}
