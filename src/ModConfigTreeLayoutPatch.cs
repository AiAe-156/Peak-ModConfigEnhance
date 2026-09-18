#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using PEAKLib.ModConfig;
using PEAKLib.ModConfig.Components;
using PEAKLib.ModConfig.SettingOptions;
using PEAKLib.ModConfig.SettingOptions.SettingUI;
using PEAKLib.UI;
using PEAKLib.UI.Elements;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ModConfigEnhance;

/// <summary>
/// 把 ModConfig 1.8.x 的模组设置页改成「左树右选项」。
///
/// ModConfig 原版是两排横向页签：上排模组、下排分区，装了三四十个模组之后只能拖着看。
/// 本项不另起 UI，只换容器和排法 —— 页签对象、TABS 组件、选中态、筛选级联全是 ModConfig 自己的：
///   · 模组页签容器（PeakHorizontalTabs）从右侧内容面板挪到页面左栏，横排改竖排、横滚改竖滚；
///   · 分区页签容器整个塞进模组列表里，每次 ModConfig 重建分区页签后，把它挪到当前选中的
///     模组行下面，行样式改成缩进的小字，于是看起来就是「模组 ▸ 分区」的树；
///   · 列表首行是表头：导出 / 置顶两列的标题 + 「只看按键」开关（1.8.0 删掉了独立的模组按键页，
///     改成筛选下拉里的「按键」，这个开关就是那个筛选的快捷键）；每个模组行左侧两列复选框、
///     列表右侧滚动条、列表上方工具行见 <see cref="ModConfigSidebarWidgets"/>（2.16.0）；
///   · 右侧去掉「MODS / SECTIONS」两个标签，选项列表顶到面板顶部；按键重复警告改为垂直居中。
///
/// 挂点：ModConfig 构建页面的静态本地函数 builderDelegate 的 postfix（主菜单与 ESC 各建一次页面，
/// 两处都会经过），以及 ModSettingsMenu.UpdateSectionTabs 的 postfix（选中模组、筛选级联、
/// 每次打开页面都会走它重建分区页签）。
///
/// 装了 TimeTheme 时页签行跟随昼夜主题（见 <see cref="TimeThemeBridge"/>）。
/// 只改版面，不改数据；关掉本项即回到 ModConfig 原布局。
/// </summary>
internal static class ModConfigTreeLayoutPatch
{
    // ── 版面常量（画布参考分辨率 1920×1080；左栏 x 65..365，与 ModConfig 自己的搜索框白底左右对齐）──
    internal const float SidebarLeft = 65f;
    internal const float SidebarWidth = 300f;

    /// <summary>列表右侧的滚动条：贴在搜索框右缘外 6px，宽 10（2.16.0）。</summary>
    private const float ScrollbarGap = 6f;
    private const float ScrollbarWidth = 10f;

    /// <summary>搜索框底边 -310 下面先放工具行（导出翻译 / 查漏翻 / 仅导出已选；显示原文 / 重载翻译 / 打开目录；语言 ▾ / 刷新），再是列表。「隐藏本地化相关按钮」时只剩语言行，即时生效。</summary>
    internal const float ToolbarTop = 318f;

    /// <summary>实际可见的工具行排数：1 只留语言行，3 全部。</summary>
    private static int VisibleToolbarRows => Plugin.HideLocalizationButtons.Value ? 1 : ModConfigSidebarWidgets.ToolbarRows;

    private static float SidebarTop =>
        ToolbarTop + VisibleToolbarRows * ModConfigSidebarWidgets.ToolbarHeight + (VisibleToolbarRows - 1) * ModConfigSidebarWidgets.ToolbarRowGap + 8f;

    /// <summary>列表底边离页面底边的距离：与右侧选项列表齐平（MainPage 底边离屏幕 30），不贴屏幕底。ESC 页体力条 HUD 会透出来，但用户要的是更长的列表。</summary>
    private const float SidebarBottom = 40f;
    private const float SidebarBottomInGame = 60f;

    internal const float ModRowHeight = 44f;
    private const float SectionRowHeight = 34f;
    private const float RowSpacing = 6f;
    private const float SectionSpacing = 3f;
    internal const float ModTextIndent = 14f;
    private const float SectionInset = 18f;
    private const float SectionTextIndent = 22f;
    private const float SectionBackgroundAlpha = 0.45f;

    /// <summary>按键重复警告：贴在选项标签与按键按钮之间，右缘离单元格右边 450、宽 620，与 ModConfig 原来的横向位置一致。</summary>
    private const float WarningRightInset = 450f;
    private const float WarningWidth = 620f;

    /// <summary>ModConfig 筛选位掩码：1 开关、2 文本、4 数值、8 下拉框、16 按键；31 = 全部。</summary>
    internal const int ControlsFilter = 16;
    internal const int AllFilter = 31;

    /// <summary>PeakHorizontalTabs 给页签底色用的默认色，我们自己加的行沿用它，TimeTheme 也认这个色。</summary>
    internal static readonly Color TabBackground = new Color(0.1792453f, 0.1253449f, 0.09046815f, 62f / 85f);

    internal static bool Attached { get; private set; }

    internal static string State { get; private set; } = "未挂载";

    /// <summary>挂在分区容器上的标记：只有经本项重排过的页面，才会在 UpdateSectionTabs 之后被接手。</summary>
    internal sealed class TreeMarker : MonoBehaviour
    {
    }

    // ── 挂载 ────────────────────────────────────────────────────────

    /// <summary>任何一步失败都整项放弃并记日志，不让异常逃到 Awake 里连累其他分区。</summary>
    internal static void TryApply(Harmony harmony)
    {
        try
        {
            Apply(harmony);
        }
        catch (Exception ex)
        {
            State = "挂载失败";
            Plugin.Log.LogError($"[面板布局] 挂载失败，本项跳过: {ex}");
        }
    }

    private static void Apply(Harmony harmony)
    {
        if (!ModConfigSupport.IsInstalled)
        {
            State = "ModConfig 未安装，跳过";
            Plugin.Log.LogInfo($"[面板布局] {State}。");
            return;
        }

        if (ModConfigSupport.IsLocalizationFork)
        {
            State = "在场的是 ModSettingsLocalization 分支版，跳过";
            Plugin.Log.LogWarning($"[面板布局] {State}（它自带竖排模组列表，且页面结构与官方 1.8.0 不同）。");
            return;
        }

        Type? pluginType = AccessTools.TypeByName(ModConfigSupport.PluginTypeName);
        Type? menuType = AccessTools.TypeByName(ModConfigSupport.MenuTypeName);
        MethodInfo? builder = pluginType != null
            ? ModConfigSupport.FindLocalFunction(pluginType, "builderDelegate", new[] { typeof(Transform) })
            : null;
        MethodInfo? updateSections = menuType != null
            ? AccessTools.Method(menuType, "UpdateSectionTabs", new[] { typeof(string) })
            : null;

        if (builder == null || updateSections == null)
        {
            State = "目标方法缺失，跳过";
            Plugin.Log.LogWarning(
                $"[面板布局] 找不到 ModConfig 的页面构建函数或 UpdateSectionTabs（builder={builder != null}, " +
                $"update={updateSections != null}），ModConfig 可能已改结构，本项跳过。");
            return;
        }

        harmony.Patch(builder, postfix: new HarmonyMethod(typeof(ModConfigTreeLayoutPatch), nameof(BuilderPostfix)));
        harmony.Patch(updateSections, postfix: new HarmonyMethod(typeof(ModConfigTreeLayoutPatch), nameof(UpdateSectionTabsPostfix)));

        // 分区记忆：每次选分区记下「模组→分区」，UpdateSectionTabsPostfix 重建后恢复；
        // 前缀旗标挡住重建期那个 Select(list[0]) 的默认选择——不然它先把自己的记忆改写成「第一个」，根本轮不到恢复。
        MethodInfo? onSectionSelected = AccessTools.Method(typeof(ModdedSettingsSectionTABS), "OnSelected", new[] { typeof(ModdedTABSButton) });
        if (onSectionSelected != null)
        {
            harmony.Patch(onSectionSelected, postfix: new HarmonyMethod(typeof(ModConfigTreeLayoutPatch), nameof(SectionSelectedPostfix)));
        }
        else
        {
            Plugin.Log.LogWarning("[面板布局] 找不到 SectionTABS.OnSelected，分区记忆停用。");
        }

        harmony.Patch(updateSections, prefix: new HarmonyMethod(typeof(ModConfigTreeLayoutPatch), nameof(UpdateSectionTabsPrefix)));

        // 左栏滚轮走独立的短缓动；Prefix 只会拦截挂了 SidebarWheelScroll 标记的 ScrollRect，
        // 因此右侧选项列表、说明面板等仍完全使用 ModConfig / Unity 原生滚动。
        try
        {
            MethodInfo? scrollWheel = AccessTools.Method(typeof(ScrollRect), nameof(ScrollRect.OnScroll), new[] { typeof(PointerEventData) });
            if (scrollWheel != null)
            {
                harmony.Patch(scrollWheel, prefix: new HarmonyMethod(typeof(SidebarWheelScroll), nameof(SidebarWheelScroll.OnScrollPrefix)));
            }
            else
            {
                Plugin.Log.LogWarning("[面板布局] 找不到 ScrollRect.OnScroll，左栏保留原生滚轮行为。");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[面板布局] 左栏滚轮缓动挂载失败，保留原生滚轮并继续挂载其它补丁: {ex.Message}");
        }

        // ModConfig 的 SetSearch 直接 ShowSettings，而 ShowSettings 会把没有匹配项的模组 / 分区页签 SetActive(false)；
        // 只有 SetFilter 会先 SetAllTabsActive。清空搜索后被藏掉的行回不来，这里补上。
        MethodInfo? setSearch = menuType != null ? AccessTools.Method(menuType, "SetSearch", new[] { typeof(string) }) : null;
        if (setSearch != null)
        {
            harmony.Patch(setSearch, prefix: new HarmonyMethod(typeof(ModConfigTreeLayoutPatch), nameof(SetSearchPrefix)));
        }
        else
        {
            Plugin.Log.LogWarning("[面板布局] 找不到 ModSettingsMenu.SetSearch，清空搜索后被隐藏的模组行需要切换筛选才会回来。");
        }

        TypingGuard.TryApply(harmony);

        // 单元格生成后：底图换成会抖的材质；TimeTheme 在场再给输入框 / 下拉框补夜间色（它收不到这些后生成的控件）
        MethodInfo? showSettings = menuType != null ? AccessTools.Method(menuType, "ShowSettings", Type.EmptyTypes) : null;
        if (showSettings != null)
        {
            harmony.Patch(showSettings, postfix: new HarmonyMethod(typeof(ModConfigTreeLayoutPatch), nameof(ShowSettingsPostfix)));
        }
        else
        {
            Plugin.Log.LogWarning("[面板布局] 找不到 ModSettingsMenu.ShowSettings，选项单元格不抖、夜间下拉框 / 输入框保持白色。");
        }

        // 按键重复警告的位置：缺了只是保持 ModConfig 原位置，不算失败
        Type? bindingUiType = AccessTools.TypeByName(ModConfigSupport.BindingUiTypeName);
        MethodInfo? warningSetup = bindingUiType != null
            ? AccessTools.Method(bindingUiType, "SetupDuplicateBindText", Type.EmptyTypes)
            : null;
        if (warningSetup != null)
        {
            harmony.Patch(warningSetup, postfix: new HarmonyMethod(typeof(ModConfigTreeLayoutPatch), nameof(WarningPlacementPostfix)));
        }
        else
        {
            Plugin.Log.LogWarning("[面板布局] 找不到 InputBindingSettingUI.SetupDuplicateBindText，按键重复警告保持 ModConfig 原位置。");
        }

        // 页签文字的夜间色：只在 TimeTheme 在场时接管（对应它给原版 SettingsTABSButton.Update 打的补丁）
        if (TimeThemeBridge.Available)
        {
            Type? tabButtonType = AccessTools.TypeByName(ModConfigSupport.TabButtonTypeName);
            MethodInfo? tabUpdate = tabButtonType != null ? AccessTools.Method(tabButtonType, "Update", Type.EmptyTypes) : null;
            if (tabUpdate != null)
            {
                harmony.Patch(tabUpdate, prefix: new HarmonyMethod(typeof(ModConfigTreeLayoutPatch), nameof(TabButtonUpdatePrefix)));
            }
            else
            {
                Plugin.Log.LogWarning("[面板布局] 找不到 ModdedTABSButton.Update，夜间模式下页签文字保持白色。");
            }
        }

        // 手柄支持的全局补丁：滑条 A 键调值门控 + 步进上限 + B 键分级返回（单个失败只警告，不连累布局）
        GamepadSupport.ApplyPatches(harmony);

        Attached = true;
        State = $"已挂载（ModConfig {ModConfigSupport.InstalledVersion}{(TimeThemeBridge.Available ? "，随 TimeTheme 昼夜跟色" : "")}）";
        ModConfigSupport.LogVersionNote("面板布局");
    }

    private static void SetSearchPrefix(object __instance)
    {
        try
        {
            if (__instance is ModSettingsMenu menu)
            {
                menu.SetAllTabsActive();
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[面板布局] 搜索前恢复页签失败: {ex.Message}");
        }
    }

    // ── 页面构建完成：重排 ──────────────────────────────────────────

    private static void BuilderPostfix()
    {
        try
        {
            ModSettingsMenu? menu = ModSettingsMenu.Instance;
            if (menu == null)
            {
                Plugin.Log.LogWarning("[面板布局] 页面已构建但拿不到 ModSettingsMenu，保留 ModConfig 原布局。");
                return;
            }

            if (menu.ModTabController == null || menu.SectionTabController == null ||
                menu.MainPage == null || menu.Content == null)
            {
                Plugin.Log.LogWarning("[面板布局] ModSettingsMenu 缺少页签容器或内容面板引用，保留 ModConfig 原布局。");
                return;
            }

            BuildTree(menu);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[面板布局] 重排失败，保留 ModConfig 原布局: {ex}");
        }
    }

    private static void BuildTree(ModSettingsMenu menu)
    {
        RectTransform page = (RectTransform)menu.MainPage.transform;
        PeakHorizontalTabs modTabs = menu.ModTabController;
        PeakHorizontalTabs sectionTabs = menu.SectionTabController;
        bool inPauseMenu = menu.MainPage.GetComponentInParent<PauseMenuHandler>(true) != null;

        // 1. 模组列表：脱离右侧内容面板，挂到页面上，占满左栏（底边跟随页面底部，不同比例的画布都不溢出）
        RectTransform list = (RectTransform)modTabs.transform;
        list.SetParent(page, false);
        list.anchorMin = new Vector2(0f, 0f);
        list.anchorMax = new Vector2(0f, 1f);
        list.pivot = new Vector2(0f, 1f);
        float sidebarBottom = inPauseMenu ? SidebarBottomInGame : SidebarBottom;
        list.offsetMin = new Vector2(SidebarLeft, sidebarBottom);
        list.offsetMax = new Vector2(SidebarLeft + SidebarWidth, -SidebarTop);
        RectTransform listContent = MakeVertical(modTabs, RowSpacing, new RectOffset(0, 0, 0, 8), keepScrolling: true);
        AddScrollHitArea(list);
        list.gameObject.AddComponent<SidebarWheelScroll>().Scroll = modTabs.GetComponent<ScrollRect>();
        ModConfigPaperStyle.Resolve(page); // 行底、表头、工具行、复选框都要用
        foreach (GameObject row in modTabs.Tabs)
        {
            StyleRow(row, ModRowHeight, ModTextIndent, 16f, 22f, backgroundAlpha: null);
        }

        // 1b. 列表右侧的滚动条（顶边记下，「隐藏本地化相关按钮」切换时跟着列表顶走）
        Scrollbar sidebarScrollbar = ModConfigSidebarWidgets.MakeScrollbar(page, modTabs.GetComponent<ScrollRect>(),
            SidebarLeft + SidebarWidth + ScrollbarGap, ScrollbarWidth, SidebarTop, sidebarBottom);

        // 2. 分区容器：整个塞进模组列表，之后跟着选中的模组行走。
        //    它自己不再滚动、不再裁剪；高度由「根布局组 → 内层布局组 → 各行」逐层上报给外层列表，
        //    行被筛选隐藏或重建时自动跟着变，不用手算。
        RectTransform sectionRoot = (RectTransform)sectionTabs.transform;
        sectionRoot.SetParent(listContent, false);
        MakeVertical(sectionTabs, SectionSpacing, new RectOffset((int)SectionInset, 0, 2, 2), keepScrolling: false);
        VerticalLayoutGroup rootGroup = sectionRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        rootGroup.padding = new RectOffset(0, 0, 0, 0);
        rootGroup.spacing = 0f;
        rootGroup.childAlignment = TextAnchor.UpperLeft;
        rootGroup.childControlWidth = true;
        rootGroup.childControlHeight = true;
        rootGroup.childForceExpandWidth = true;
        rootGroup.childForceExpandHeight = false;
        sectionRoot.gameObject.AddComponent<TreeMarker>();
        sectionRoot.gameObject.SetActive(false); // 还没选中任何模组：整个折叠（布局组会跳过非激活子项，不留空隙）

        // 3. 表头行（导出 / 置顶 两列标题 + 只看按键）、每行的两列复选框、置顶排序
        ModConfigSidebarWidgets.SidebarController sidebar = list.gameObject.AddComponent<ModConfigSidebarWidgets.SidebarController>();
        sidebar.Menu = menu;
        sidebar.ModTabs = modTabs;
        sidebar.ListContent = listContent;
        sidebar.SectionRoot = sectionRoot;
        sidebar.Scroll = modTabs.GetComponent<ScrollRect>();
        sidebar.Build();

        // 逐行出现 + 音效（与右侧单元格同节奏）
        SidebarCascade cascade = list.gameObject.AddComponent<SidebarCascade>();
        cascade.ModTabs = modTabs;
        cascade.Header = sidebar.Header;

        // 手柄支持：页面焦点兜底 + LB/RB 切模组 + 页面固定件的焦点框（行/复选框在 Build 里逐件挂）
        GamepadSupport.Attach(menu, modTabs);

        // 3b. 列表上方的工具行（导出翻译 / 查漏翻 / 仅导出已选；显示原文 / 重载 / 打开目录；语言 ▾ / 刷新）
        try
        {
            sidebar.Toolbar = ModConfigSidebarWidgets.MakeToolbar(page, SidebarLeft, SidebarWidth, ToolbarTop, _ => sidebar.RefreshColumns());
            sidebar.ExportSelectedBox = sidebar.Toolbar.ExportSelectedBox;
            sidebar.ScrollbarRect = (RectTransform)sidebarScrollbar.transform;
            // 容器按展开位置建好，这里收拢到当前选项值（也让初次布局与切换走同一条路径）
            sidebar.ApplyToolbarCollapsed();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[面板布局] 工具行构建失败，跳过: {ex}");
        }

        // 3c. ModConfig 自己的标题与「搜索」标签：白字，夜里要变紫（走选项表那一遍，见 ThemeAsOption）
        Transform? header = page.Find("Header");
        if (header != null)
        {
            header.gameObject.AddComponent<ModConfigSidebarWidgets.ThemeAsOption>();
        }

        foreach (Transform child in page)
        {
            if (child.GetComponent<PeakText>() != null)
            {
                child.gameObject.AddComponent<ModConfigSidebarWidgets.ThemeAsOption>();
            }
        }

        // 3d. 搜索框：夜间按 TimeTheme 的输入框 ColorBlock 上色（它自己收不到这个后建的控件）
        if (TimeThemeBridge.Available)
        {
            Transform? search = page.Find("SearchInput");
            if (search != null)
            {
                TimeThemeBridge.ApplyOptionsTheme(search, TimeThemeBridge.IsDark);
            }
        }

        // 4. 右侧：去掉「MODS / SECTIONS」两个标签（内容面板直属的 PeakText 只有它们俩），选项列表顶到面板顶部
        foreach (Transform child in menu.transform)
        {
            if (child.GetComponent<PeakText>() != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        if (menu.Content.parent is RectTransform scrollArea)
        {
            scrollArea.offsetMax = new Vector2(scrollArea.offsetMax.x, -8f);

            // 4b. 顶部的选项说明面板（自己再把列表顶边往下让）
            if (Plugin.EnableDescriptionPanel.Value)
            {
                try
                {
                    ModConfigDescriptionPanel.Attach(menu, scrollArea);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"[面板布局] 说明面板构建失败，跳过: {ex}");
                }
            }
        }

        // 5. 昼夜主题：让 TimeTheme 把这页收进它的表（之后的切换才覆盖得到），再把此刻该有的颜色刷上去
        if (TimeThemeBridge.Available)
        {
            if (inPauseMenu)
            {
                TimeThemeBridge.RefreshLists();
            }

            TimeThemeBridge.ApplyCurrentTheme(page);
            TreeThemeFollower.ApplyOptionMarkers(page, TimeThemeBridge.IsDark);
            TreeThemeFollower themeFollower = list.gameObject.AddComponent<TreeThemeFollower>();
            themeFollower.Page = page;
            themeFollower.Menu = menu;
        }

        // 打字保护的驱动：页面开着时每帧看焦点是否在输入框上。与主题无关，不装 TimeTheme 也必须挂
        list.gameObject.AddComponent<TypingGuard.Driver>();

        Plugin.Log.LogInfo($"[面板布局] 已重排为树形侧栏（{modTabs.Tabs.Count} 个模组，{(inPauseMenu ? "ESC 页" : "主菜单")}）。");
    }

    /// <summary>
    /// 把 PeakHorizontalTabs 的横排内容改成竖排。它在 Awake 里塞的是 HorizontalLayoutGroup +
    /// 横向 ContentSizeFitter，同一对象上只能有一个布局组，先拆再装。
    /// </summary>
    private static RectTransform MakeVertical(PeakHorizontalTabs tabs, float spacing, RectOffset padding, bool keepScrolling)
    {
        ScrollRect scroll = tabs.GetComponent<ScrollRect>();
        RectTransform content = scroll.content;

        HorizontalLayoutGroup? horizontal = content.GetComponent<HorizontalLayoutGroup>();
        if (horizontal != null)
        {
            UnityEngine.Object.DestroyImmediate(horizontal);
        }

        VerticalLayoutGroup? vertical = content.GetComponent<VerticalLayoutGroup>();
        if (vertical == null)
        {
            vertical = content.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        vertical.spacing = spacing;
        vertical.padding = padding;
        vertical.childAlignment = TextAnchor.UpperLeft;
        vertical.childControlWidth = true;
        vertical.childForceExpandWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandHeight = false;

        ContentSizeFitter? fitter = content.GetComponent<ContentSizeFitter>();
        if (keepScrolling)
        {
            if (fitter == null)
            {
                fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = SidebarWheelScroll.StepPixels;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            content.pivot = new Vector2(0.5f, 1f);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
        }
        else
        {
            // 嵌在外层列表里：尺寸由布局组驱动，ContentSizeFitter 会跟布局组打架；滚动与裁剪交给外层
            if (fitter != null)
            {
                UnityEngine.Object.DestroyImmediate(fitter);
            }

            scroll.enabled = false;
            RectMask2D? mask = tabs.GetComponent<RectMask2D>();
            if (mask != null)
            {
                mask.enabled = false;
            }
        }

        return content;
    }

    /// <summary>
    /// 页签行样式。PeakHorizontalTabs.AddTab 给的是横排用的 minWidth 220、居中、全大写；
    /// 竖排改成撑满宽度、固定行高、左对齐、正常大小写，过长的名字缩小字号后再省略。
    /// </summary>
    private static void StyleRow(GameObject row, float height, float textIndent, float fontMin, float fontMax, float? backgroundAlpha)
    {
        StyleRowFrame(row, height);

        TextMeshProUGUI? text = row.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
        {
            // PeakText.Awake 按空文本算过一次 sizeDelta，AddTab 之后只改了锚点没归零，
            // 文字框会比行框大出一截；左对齐时会看出来，先铺满整行
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            text.fontStyle = FontStyles.Normal;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.margin = new Vector4(textIndent, 0f, 8f, 0f);
            text.enableAutoSizing = true;
            text.fontSizeMin = fontMin;
            text.fontSizeMax = fontMax;
            text.fontSize = fontMax;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        // 行底与选中底图套原版纸面（圆角 + 褶皱 + 抖动）；重复调用无副作用
        Transform? imageTransform = row.transform.Find("Image");
        Image? image = imageTransform != null ? imageTransform.GetComponent<Image>() : null;
        if (image != null)
        {
            ModConfigPaperStyle.Apply(image, 8f);
            if (backgroundAlpha.HasValue)
            {
                Color color = image.color;
                color.a = backgroundAlpha.Value;
                image.color = color;
            }
        }

        Transform? selectedTransform = row.transform.Find("Selected");
        Image? selected = selectedTransform != null ? selectedTransform.GetComponent<Image>() : null;
        if (selected != null)
        {
            ModConfigPaperStyle.Apply(selected, 8f);
        }
    }

    /// <summary>行框：撑满宽度、固定行高（表头行也用）。</summary>
    internal static void StyleRowFrame(GameObject row, float height)
    {
        LayoutElement? element = row.GetComponent<LayoutElement>();
        if (element == null)
        {
            element = row.AddComponent<LayoutElement>();
        }

        element.minWidth = 0f;
        element.preferredWidth = -1f;
        element.flexibleWidth = 1f;
        element.minHeight = height;
        element.preferredHeight = height;
        element.flexibleHeight = 0f;
    }

    /// <summary>
    /// 滚轮死区修复（2.16.0）：列表根对象只有 RectMask2D 没有 Graphic，行与行之间的空隙、列表末尾的空白
    /// 没有东西接射线，滚轮落在那里 ScrollRect 收不到。铺一张全透明、只接射线的 Image 在内容下面。
    /// </summary>
    private static void AddScrollHitArea(RectTransform list)
    {
        GameObject hit = new GameObject("MCE_ScrollHitArea", typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)hit.transform;
        rect.SetParent(list, false);
        rect.SetAsFirstSibling();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = hit.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;
    }

    // ── 单元格生成后：补选项控件的夜间色 ────────────────────────────

    private static void ShowSettingsPostfix(object __instance)
    {
        if (__instance is not ModSettingsMenu menu || menu.Content == null)
        {
            return;
        }

        try
        {
            foreach (SettingsUICell cell in menu.m_spawnedCells)
            {
                if (cell != null)
                {
                    ModConfigPaperStyle.ApplyWobbleToCell(cell);
                    ModConfigSidebarWidgets.StyleCellButtons(cell); // 亮蓝换可换肤蓝 + 虚线归位，须先于 TimeTheme 换色
                }
            }

            if (TimeThemeBridge.Available)
            {
                TimeThemeBridge.ApplyCurrentTheme(menu.Content);
                TimeThemeBridge.ApplyOptionsTheme(menu.Content, TimeThemeBridge.IsDark);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[面板布局] 单元格夜间色刷新失败: {ex.Message}");
        }

        try
        {
            ApplyRowVisibility(menu);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[面板布局] 模组行显隐刷新失败: {ex.Message}");
        }

        // 手柄：单元格内控件被选中时把它滚进右侧列表视野（焦点视觉沿用单元格预制体自带的）
        try
        {
            ScrollRect? optionsScroll = menu.Content != null ? menu.Content.GetComponentInParent<ScrollRect>() : null;
            if (optionsScroll != null)
            {
                foreach (SettingsUICell cell in menu.m_spawnedCells)
                {
                    if (cell != null)
                    {
                        GamepadSupport.MarkCell(cell, optionsScroll);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[面板布局] 选项滚动跟随挂载失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 搜索 / 筛选 / 只看按键后把「没有任何匹配选项」的模组行藏起来，恢复匹配的再亮回来。
    /// ModConfig 自己只在「当前选中模组整条没匹配、要自动跳走」那一支才藏行（ShouldUseFilterResults 的 else 分支），
    /// 其余路径下不匹配的行会一直留着、点进去是空的。这里按同口径重算：条件设置 → 搜索词 → 类型位掩码。
    /// </summary>
    private static void ApplyRowVisibility(ModSettingsMenu menu)
    {
        if (menu.ModTabController == null || menu.settings == null)
        {
            return;
        }

        IEnumerable<IBepInExProperty> listing = menu.settings.Where(
            item => !(item is IConditionalSetting conditional) || conditional.ShouldShow());

        if (!string.IsNullOrEmpty(menu.search))
        {
            listing = listing.Where(item =>
            {
                string? displayName = item.GetDisplayName();
                return displayName != null && displayName.ToLower().Contains(menu.search);
            });
        }

        int filter = menu.FilterValue;
        if ((filter & 1) == 0)
        {
            listing = listing.Where(item => item.ConfigBase.SettingType != typeof(bool));
        }
        if ((filter & 2) == 0)
        {
            listing = listing.Where(item => !(item is BepInExString));
        }
        if ((filter & 4) == 0)
        {
            listing = listing.Where(item => item.ConfigBase.SettingType != typeof(int)
                && item.ConfigBase.SettingType != typeof(float) && item.ConfigBase.SettingType != typeof(double));
        }
        if ((filter & 8) == 0)
        {
            listing = listing.Where(item => !(item is BepInExEnum));
        }
        if ((filter & 16) == 0)
        {
            listing = listing.Where(item => !(item is BepInExKeyPath) && item.ConfigBase.SettingType != typeof(KeyCode));
        }

        HashSet<string> visible = new HashSet<string>(StringComparer.Ordinal);
        foreach (IBepInExProperty item in listing)
        {
            string? category = item.GetCategory();
            if (category != null)
            {
                visible.Add(category);
            }
        }

        foreach (GameObject row in menu.ModTabController.Tabs)
        {
            if (row != null)
            {
                row.SetActive(visible.Contains(row.name));
            }
        }

        // 选中行被藏（没有任何模组匹配）时把分区容器也收掉，免得它带着旧分区行悬在列表里
        ModdedTABSButton? selected = menu.ModTabs != null ? menu.ModTabs.selectedButton : null;
        if (menu.SectionTabController != null &&
            menu.SectionTabController.gameObject.activeSelf &&
            (selected == null || !selected.gameObject.activeSelf))
        {
            menu.SectionTabController.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 新建的页签行第一帧会整行闪白：PeakHorizontalTabs.AddTab 把「Selected」白底 Image 建成激活状态，
    /// 只靠 ModdedTABSButton.Update 每帧按 Selected 去隐藏，而重建发生在点击回调里，那一帧 Update 还没跑。
    /// 夜间还叠加文字从白色向紫色的渐变。这里在重建当帧就把选中底图与文字颜色定到位，跳过那一帧。
    /// </summary>
    private static void SettleSelection(ModdedTABSButton? button)
    {
        if (button == null)
        {
            return;
        }

        bool dark = TimeThemeBridge.IsDark;
        if (button.SelectedGraphic != null)
        {
            button.SelectedGraphic.SetActive(button.Selected);
            TimeThemeBridge.TintSelectedGraphic(button.SelectedGraphic, dark);
        }

        if (button.text != null)
        {
            button.text.color = button.Selected ? Color.black : (dark ? TimeThemeBridge.DarkTabText : Color.white);
        }
    }

    // ── 昼夜主题跟随 ────────────────────────────────────────────────

    /// <summary>
    /// 挂在模组列表上：打开页面时按当前主题把整页刷一遍，页面开着时主题变了也刷
    /// （分区行、复选框、滚动条、说明面板、工具行都是后建的，不在 TimeTheme 的表里）。
    /// 选项控件（搜索框、单元格里的下拉框 / 输入框）走 ColorBlock，另刷一遍。
    /// </summary>
    internal sealed class TreeThemeFollower : MonoBehaviour
    {
        internal RectTransform? Page;
        internal ModSettingsMenu? Menu;
        private bool _dark;

        /// <summary>挂了 ThemeAsOption 的子树（标题、搜索标签、工具行、表头、说明面板）多刷一遍选项表，让白字变紫 / 变回白。</summary>
        internal static void ApplyOptionMarkers(Transform page, bool dark)
        {
            foreach (ModConfigSidebarWidgets.ThemeAsOption marker in page.GetComponentsInChildren<ModConfigSidebarWidgets.ThemeAsOption>(true))
            {
                TimeThemeBridge.ApplyOptionsTheme(marker.transform, dark);
            }
        }

        private void OnEnable()
        {
            _dark = TimeThemeBridge.IsDark;
            Apply(_dark);
        }

        private void Update()
        {
            bool dark = TimeThemeBridge.IsDark;
            if (dark == _dark)
            {
                return;
            }

            _dark = dark;
            Apply(dark);
        }

        private void Apply(bool dark)
        {
            Transform root = Page != null ? Page : transform;
            TimeThemeBridge.ApplyTheme(root, dark);
            if (Page != null)
            {
                Transform? search = Page.Find("SearchInput");
                if (search != null)
                {
                    TimeThemeBridge.ApplyOptionsTheme(search, dark);
                }

                ApplyOptionMarkers(Page, dark);
            }

            ModSettingsMenu? menu = Menu;
            if (menu != null && menu.Content != null)
            {
                TimeThemeBridge.ApplyTheme(menu.Content, dark);
                TimeThemeBridge.ApplyOptionsTheme(menu.Content, dark);
            }
        }
    }

    /// <summary>
    /// 夜间模式下页签行的配色（对应 TimeTheme 给原版 SettingsTABSButton 打的那个补丁，再多管一个选中态）：
    ///   · 未选中：文字用它的夜间页签字色 DarkTabText，选中底图隐藏；
    ///   · 选中：底图从 ModConfig 的纯白改成它的夜间选中紫（DarkSelectedFill），文字仍走原逻辑的黑 ——
    ///     夜里整栏黑底紫字，中间突然一条白底黑字太扎眼（2.13.3）。
    /// 白天：底图还原纯白，其余全交回 ModConfig 原 Update。
    /// 本前缀只在 TimeTheme 在场时才注册；IsDark 在它缺席时恒为 false，TintSelectedGraphic 也直接返回。
    /// </summary>
    private static bool TabButtonUpdatePrefix(object __instance)
    {
        if (__instance is not ModdedTABSButton button)
        {
            return true;
        }

        bool dark = TimeThemeBridge.IsDark;
        TimeThemeBridge.TintSelectedGraphic(button.SelectedGraphic, dark);

        if (!dark || button.text == null || button.Selected)
        {
            return true;
        }

        button.text.color = Color.Lerp(button.text.color, TimeThemeBridge.DarkTabText, Time.unscaledDeltaTime * 7f);
        if (button.SelectedGraphic != null)
        {
            button.SelectedGraphic.SetActive(false);
        }

        return false;
    }

    // ── 按键重复警告：垂直居中 ──────────────────────────────────────

    /// <summary>
    /// ModConfig 把预制体里的 OnlyOnMainMenu 文本挪到标签与按键之间（anchoredPosition -1060,-25、高 0），
    /// 锚点沿用预制体的，文字从锚点往下排，两行时整体偏下。改成锚在单元格右侧、上下拉满、文字垂直居中。
    /// </summary>
    private static void WarningPlacementPostfix(object __instance)
    {
        try
        {
            if (__instance is not InputBindingSettingUI ui)
            {
                return;
            }

            TextMeshProUGUI? warning = ui.DuplicateWarningText;
            if (warning == null)
            {
                return;
            }

            RectTransform rect = warning.rectTransform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.offsetMin = new Vector2(-(WarningRightInset + WarningWidth), 4f);
            rect.offsetMax = new Vector2(-WarningRightInset, -4f);
            warning.alignment = TextAlignmentOptions.Right; // 水平靠右、垂直居中
            warning.textWrappingMode = TextWrappingModes.Normal;
            warning.overflowMode = TextOverflowModes.Overflow;
            warning.raycastTarget = false; // 620px 的文本区横跨按键框左侧，不接射线免得吃掉点击
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[面板布局] 按键重复警告定位失败: {ex.Message}");
        }
    }

    // ── 分区页签重建完成：贴到选中模组下面 ──────────────────────────

    /// <summary>各模组记住的分区（会话内有效）：OnSelected 记录、UpdateSectionTabs 重建后恢复。</summary>
    internal static readonly Dictionary<string, string> SectionMemory = new Dictionary<string, string>();

    /// <summary>重建分区行期间压制记忆写入——内部的 Select(list[0]) 不是用户选择，不记录。</summary>
    private static bool _suppressSectionRecord;

    private static void UpdateSectionTabsPrefix()
    {
        _suppressSectionRecord = true;
    }

    /// <summary>分区记忆：选分区时记下当前模组的选中分区（ModdedSettingsSectionTABS.OnSelected 的 postfix）。</summary>
    private static void SectionSelectedPostfix(object __instance, object button)
    {
        try
        {
            if (__instance is not ModdedSettingsSectionTABS tabs || button is not ModdedTABSButton b)
            {
                return;
            }

            ModSettingsMenu? menu = tabs.SettingsMenu;
            ModdedTABSButton? mod = menu != null && menu.ModTabs != null ? menu.ModTabs.selectedButton : null;
            // _suppressSectionRecord：重建期的 Select(list[0]) 不记录（会把记忆改写成「第一个」）
            if (mod != null && !string.IsNullOrEmpty(b.category) && !_suppressSectionRecord)
            {
                SectionMemory[mod.category ?? ""] = b.category;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[面板布局] 分区记忆记录失败: {ex.Message}");
        }
    }

    private static void UpdateSectionTabsPostfix(object __instance)
    {
        try
        {
            if (__instance is not ModSettingsMenu menu)
            {
                return;
            }

            PeakHorizontalTabs sectionTabs = menu.SectionTabController;
            if (sectionTabs == null || sectionTabs.GetComponent<TreeMarker>() == null)
            {
                return; // 这个页面没经过本项重排（构建时失败过），不碰
            }

            RectTransform sectionRoot = (RectTransform)sectionTabs.transform;
            RectTransform sectionContent = sectionTabs.GetComponent<ScrollRect>().content;
            ScrollRect? listScroll = menu.ModTabController != null ? menu.ModTabController.GetComponent<ScrollRect>() : null;

            // 1. 贴到当前选中的模组行下面。不用 modName 参数：筛选级联会在本方法内部再选一次别的模组，
            //    外层调用收尾时 selectedButton 才是最终结果。先挪到末尾再定位，避免 SetSiblingIndex 的位移歧义。
            ModdedTABSButton? selected = menu.ModTabs != null ? menu.ModTabs.selectedButton : null;
            if (selected != null && selected.transform.parent == sectionRoot.parent)
            {
                sectionRoot.SetAsLastSibling();
                sectionRoot.SetSiblingIndex(selected.transform.GetSiblingIndex() + 1);
            }

            // 2. DeleteTab 用的是延迟 Destroy，旧行这一帧还占着布局，先从布局里摘掉
            HashSet<GameObject> live = new HashSet<GameObject>(sectionTabs.Tabs);
            foreach (Transform child in sectionContent)
            {
                if (!live.Contains(child.gameObject))
                {
                    child.gameObject.SetActive(false);
                }
            }

            // 3. 新行样式：缩进、小一号、底色更淡；装了 TimeTheme 就按当前主题上色（新行天生是白天色）。
            //    选中的是本模组时分区行名也上品牌色（分区行每次重建，不用清理别的模组的）。
            bool selfSelected = selected != null && selected.category == Plugin.SelfRowName;
            foreach (GameObject row in sectionTabs.Tabs)
            {
                StyleRow(row, SectionRowHeight, SectionTextIndent, 14f, 18f, SectionBackgroundAlpha);
                GamepadSupport.MarkSelectable(row, listScroll, (RectTransform)row.transform);
                GamepadSupport.WireSectionRow(row); // 手柄：分区行按 A → 进右栏
                ModdedTABSButton? rowButton = row.GetComponent<ModdedTABSButton>();
                SettleSelection(rowButton);
                if (selfSelected && rowButton != null)
                {
                    TextMeshProUGUI? rowText = row.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (rowText != null)
                    {
                        ModConfigSidebarWidgets.SelfRowTint tint =
                            rowText.gameObject.GetComponent<ModConfigSidebarWidgets.SelfRowTint>()
                            ?? rowText.gameObject.AddComponent<ModConfigSidebarWidgets.SelfRowTint>();
                        tint.Text = rowText;
                        tint.Button = rowButton;
                    }
                }
            }

            TimeThemeBridge.ApplyCurrentTheme(sectionRoot);

            // 3.5 分区记忆：切回模组时恢复上次选的分区，而不是总落第一个。
            //     记住的分区已不存在（模组更新过）就随它去，不强行选。
            if (selected != null && menu.SectionTabs != null
                && SectionMemory.TryGetValue(selected.category ?? "", out string? wanted) && wanted.Length > 0)
            {
                foreach (GameObject row in sectionTabs.Tabs)
                {
                    ModdedTABSButton? b = row != null ? row.GetComponent<ModdedTABSButton>() : null;
                    if (b != null && b.category == wanted && menu.SectionTabs.selectedButton != b)
                    {
                        menu.SectionTabs.Select(b);
                        break;
                    }
                }
            }

            // 4. 只有一个分区就不展开（右侧已经在显示它了）。折叠的是整个容器：
            //    布局组会跳过非激活子项，不会多出一段行距
            sectionRoot.gameObject.SetActive(sectionTabs.Tabs.Count > 1);

            if (sectionRoot.parent is RectTransform list)
            {
                LayoutRebuilder.MarkLayoutForRebuild(list);
                if (sectionRoot.gameObject.activeSelf)
                {
                    SidebarCascade? cascade = list.GetComponentInParent<SidebarCascade>();
                    cascade?.PlaySections(sectionTabs.Tabs);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[面板布局] 分区行更新失败: {ex}");
        }
        finally
        {
            _suppressSectionRecord = false;
        }
    }
}
