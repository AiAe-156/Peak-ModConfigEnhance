#nullable enable
using System;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;

namespace ModConfigEnhance;

/// <summary>
/// 第 9 分区共用：ModConfig（PEAKLib.ModConfig）的在场检测、版本核对与目标方法解析。
///
/// 两个子补丁都只在这里确认过「插件在场、且确实是官方 ModConfig 而非它的分支版」之后
/// 才去解析类型；解析一律按名字走，任何一环缺失就整项跳过，不做半挂载。
/// 补丁体内才直接引用 ModConfig 的类型（工程里对它做了 publicize）—— 那些方法只有
/// 挂载成功后才会被 JIT，ModConfig 缺席时不会触发类型加载失败。
/// </summary>
internal static class ModConfigSupport
{
    internal const string Guid = "com.github.PEAKModding.PEAKLib.ModConfig";

    /// <summary>本分区是按这个版本的反编译逐行核对的（MODs\反编译\ModConfig_1.8.1_2026-09-10；1.8.1 相对 1.8.0 只多了死设置项清理，挂载点未变）。</summary>
    internal const string TargetVersion = "1.8.1";

    internal const string PluginTypeName = "PEAKLib.ModConfig.ModConfigPlugin";
    internal const string MenuTypeName = "PEAKLib.ModConfig.Components.ModSettingsMenu";
    internal const string BindingUiTypeName = "PEAKLib.ModConfig.SettingOptions.SettingUI.InputBindingSettingUI";
    internal const string MenuButtonTypeName = "PEAKLib.UI.Elements.PeakMenuButton";
    internal const string TabButtonTypeName = "PEAKLib.ModConfig.Components.ModdedTABSButton";

    private static PluginInfo? Info =>
        Chainloader.PluginInfos.TryGetValue(Guid, out PluginInfo? info) ? info : null;

    internal static bool IsInstalled => Info != null;

    internal static string InstalledVersion => Info?.Metadata.Version.ToString() ?? "未知";

    internal static string InstalledName => Info?.Metadata.Name ?? "";

    /// <summary>
    /// ModSettingsLocalization（youxia173）是 ModConfig 1.6.0 的分支版，复用了同一个 GUID，
    /// 但页面类叫 ModdedSettingsMenu、且自带竖排模组列表与汉化 —— 与本分区的目标结构不同，直接跳过。
    /// </summary>
    internal static bool IsLocalizationFork =>
        InstalledName.IndexOf("ModSettingsLocalization", StringComparison.OrdinalIgnoreCase) >= 0;

    /// <summary>
    /// 找 C# 静态本地函数编译出来的方法。Roslyn 把 <c>Start()</c> 里的 <c>builderDelegate</c>
    /// 编成 <c>&lt;Start&gt;g__builderDelegate|17_0</c>，序号随源码改动漂移，只按中间那段匹配。
    /// </summary>
    internal static MethodInfo? FindLocalFunction(Type type, string localName, Type[] parameters)
    {
        string marker = "g__" + localName + "|";
        foreach (MethodInfo method in type.GetMethods(
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
        {
            if (!method.Name.Contains(marker))
            {
                continue;
            }

            ParameterInfo[] ps = method.GetParameters();
            if (ps.Length != parameters.Length)
            {
                continue;
            }

            bool match = true;
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].ParameterType != parameters[i])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return method;
            }
        }

        return null;
    }

    /// <summary>版本与已核对版本不一致时提示一次；不阻止挂载（目标方法都已按名字解析成功）。</summary>
    internal static void LogVersionNote(string tag)
    {
        string installed = InstalledVersion;
        if (installed == TargetVersion)
        {
            Plugin.Log.LogInfo($"[{tag}] 已挂载（ModConfig {installed}，与已适配版本一致）。");
            return;
        }

        Plugin.Log.LogWarning(
            $"[{tag}] 已挂载，但 ModConfig 版本为 {installed}，本分区是按 {TargetVersion} 逐行核对的。\n" +
            "  界面若错位或文字没换，请关掉本项等适配。");
    }
}
