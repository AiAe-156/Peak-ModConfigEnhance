#nullable enable
using System;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace ModConfigEnhance;

/// <summary>
/// 打字保护：模组设置页里有 TMP 输入框（搜索框、字符串选项）拿着焦点时，别的模组轮询键盘不该触发快捷键。
/// 游戏自己的动作在暂停菜单里已被挡住，漏的是各模组直接读 <c>Input.GetKeyDown</c> / <c>Keyboard.current[key].wasPressedThisFrame</c>。
/// 这里给这两条路加 Prefix：聚焦期间键盘一律「没按」。鼠标、手柄不拦，UI 输入模块走 InputAction 也不经过这些读法。
/// </summary>
internal static class TypingGuard
{
    /// <summary>当前是否有输入框拿着焦点（由 <see cref="Driver"/> 每帧维护）。</summary>
    internal static bool Active;

    private static bool _patched;

    internal static void TryApply(Harmony harmony)
    {
        if (_patched)
        {
            return;
        }

        _patched = true;
        int legacy = 0;
        foreach (string name in new[] { "GetKey", "GetKeyDown", "GetKeyUp" })
        {
            legacy += PatchLegacy(harmony, name, typeof(KeyCode));
            legacy += PatchLegacy(harmony, name, typeof(string));
        }

        int modern = 0;
        foreach (string name in new[] { "isPressed", "wasPressedThisFrame", "wasReleasedThisFrame" })
        {
            MethodInfo? getter = AccessTools.PropertyGetter(typeof(ButtonControl), name);
            if (getter == null || getter.GetMethodBody() == null)
            {
                continue;
            }

            try
            {
                harmony.Patch(getter, prefix: new HarmonyMethod(typeof(TypingGuard), nameof(ButtonPrefix)));
                modern++;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[打字保护] ButtonControl.{name} 挂不上: {ex.Message}");
            }
        }

        Plugin.Log.LogInfo($"[打字保护] 已挂载：旧 Input {legacy}/6，InputSystem 按键 {modern}/3。");
    }

    private static int PatchLegacy(Harmony harmony, string name, Type argument)
    {
        MethodInfo? method = AccessTools.Method(typeof(Input), name, new[] { argument });
        if (method == null || method.GetMethodBody() == null)
        {
            return 0; // extern（InternalCall）Harmony 挂不上
        }

        try
        {
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(TypingGuard), nameof(LegacyPrefix)));
            return 1;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[打字保护] Input.{name}({argument.Name}) 挂不上: {ex.Message}");
            return 0;
        }
    }

    private static bool LegacyPrefix(ref bool __result)
    {
        if (!Active)
        {
            return true;
        }

        __result = false;
        return false;
    }

    private static bool ButtonPrefix(ButtonControl __instance, ref bool __result)
    {
        if (!Active || __instance.device is not Keyboard)
        {
            return true;
        }

        __result = false;
        return false;
    }

    /// <summary>挂在模组设置页上：页面开着时每帧看焦点是不是 TMP 输入框。</summary>
    internal sealed class Driver : MonoBehaviour
    {
        private void Update()
        {
            GameObject? selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            TMP_InputField? field = selected != null ? selected.GetComponent<TMP_InputField>() : null;
            Active = field != null && field.isFocused;
        }

        private void OnDisable()
        {
            Active = false;
        }
    }
}
