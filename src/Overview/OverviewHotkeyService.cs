#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace ModConfigEnhance.Overview;

// 1.1.0：从游戏内模组管理器 OverviewHotkeyService.cs 搬来。去掉配置方案 / 草稿 / 整合包预设：
// 快捷键页改的就是模组的实际配置，录完立即写入（ConfigEntry.BoxedValue，BepInEx 自动存盘）。
// 忽略的冲突存在 HotkeyPageState（本机个人记忆）。

[Serializable]
internal sealed class OverviewIgnoredHotkeyConflict
{
    public string idA = "", idB = "", signatureA = "", signatureB = "";
    public string OtherId(string entryId) => idA == entryId ? idB : idA;
}

internal sealed class OverviewHotkeyConflict
{
    public OverviewMod Mod = null!;
    public string Key = "", Label = "";
    public string EntryId = "", Section = "";
    /// <summary>The other side is one of the game's own controls (no entry to jump to).</summary>
    public string GameAction = "";
    public bool IsGame => GameAction.Length > 0;
    /// <summary>The clash is on the controller side (a controller combo of this or the other setting).</summary>
    public bool Combo;
}

internal sealed class OverviewHotkeyEntry
{
    public string Id = "", Guid = "", Section = "", Key = "";
    public OverviewMod Mod = null!;
    public ConfigEntryBase Config = null!;
    internal OverviewHotkeyAdapter Adapter = null!;
    public int IgnoredCount { get; internal set; }
    public string Name => OverviewLanguageBridge.Translate(Key);
    public string CustomDescription => Loc.GetHotkeyDescription(Guid, Section, Key);
    public string ShortDescription
    {
        get
        {
            string text = CustomDescription;
            return text.Length == 0 ? Loc.TranslateExternal(ModConfigDescriptionPanel.FormatDescription(Config)) : text;
        }
    }
    public string CurrentLabel => Adapter.Label(Config.BoxedValue);
    public string DefaultLabel => Adapter.Label(Config.DefaultValue);
    public bool IsUnbound => Adapter.Signature(Config.BoxedValue).Length == 0;
    public bool CanClear => Adapter.CanClear;
    public string WriteReason
    {
        get
        {
            try
            {
                string path = Config.ConfigFile.ConfigFilePath;
                return File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0
                    ? Loc.T("The config file is read-only.", "配置文件为只读，无法写入。", "設定檔為唯讀，無法寫入。") : "";
            }
            catch (Exception ex) { return ex.Message; }
        }
    }
    public List<OverviewHotkeyConflict> Conflicts = new();
}

internal sealed class OverviewHotkeyService : IDisposable
{
    private readonly List<OverviewHotkeyEntry> _entries = new();
    private readonly HashSet<ConfigFile> _subscribedFiles = new();
    private bool _disposed;
    public IReadOnlyList<OverviewHotkeyEntry> Entries => _entries;
    public event Action? Changed;
    private static HotkeyPageState State => HotkeyPageState.Current;

    public void Reload(IReadOnlyList<OverviewMod> mods)
    {
        Unsubscribe(); _entries.Clear(); _hotkeyConfigs.Clear();
        if (_disposed) return;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var plugin in Chainloader.PluginInfos.Values)
        {
            if (plugin?.Instance == null || plugin.Metadata.GUID == Plugin.PluginGuid) continue;
            // 概览页第 6 章：扫描结果里 PluginGuids 是整个资料包的 GUID，同一文件夹的几个插件会互相包含；先按自己的 GUID 认行，
            // 免得插件 B 的快捷键挂到排序在前的兄弟 A 名下（改用扫描结果前每行只有自己的 GUID，这样保持原样）。
            var mod = mods.FirstOrDefault(m => m.Guid == plugin.Metadata.GUID) ?? mods.FirstOrDefault(m => m.PluginGuids.Contains(plugin.Metadata.GUID));
            if (mod == null) continue;
            foreach (var pair in plugin.Instance.Config)
            {
                var adapter = OverviewHotkeyAdapter.Create(pair.Value); if (adapter == null) continue;
                string id = Loc.HotkeyStableId(plugin.Metadata.GUID, pair.Key.Section, pair.Key.Key);
                if (!seen.Add(id)) continue;
                _entries.Add(new OverviewHotkeyEntry { Id = id, Guid = plugin.Metadata.GUID, Section = pair.Key.Section, Key = pair.Key.Key, Mod = mod, Config = pair.Value, Adapter = adapter });
                _hotkeyConfigs.Add(pair.Value);
                if (_subscribedFiles.Add(pair.Value.ConfigFile)) pair.Value.ConfigFile.SettingChanged += OnSettingChanged;
            }
        }

        Refresh();
    }

    /// <summary>本页列出的快捷键设置。第 5 章（二次审查）：页面打开过一次后服务一直订阅着各模组的 SettingChanged，
    /// 在设置页拖滑条也会触发；只有快捷键设置变了才重算冲突（两两比较，快捷键多时不便宜）。</summary>
    private readonly HashSet<ConfigEntryBase> _hotkeyConfigs = new();
    private void OnSettingChanged(object sender, SettingChangedEventArgs args)
    {
        if (_disposed) return;
        if (args?.ChangedSetting != null && !_hotkeyConfigs.Contains(args.ChangedSetting)) return;
        Refresh();
    }
    /// <summary>Recompute duplicates after a controller combo changed.</summary>
    internal void RefreshConflicts() { if (!_disposed) Refresh(); }

    private void Refresh()
    {
        var signatures = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in _entries)
        {
            entry.Conflicts.Clear(); entry.IgnoredCount = 0;
            signatures[entry.Id] = entry.Adapter.Signature(entry.Config.BoxedValue);
        }

        for (int i = 0; i < _entries.Count; i++)
        for (int j = i + 1; j < _entries.Count; j++)
        {
            var a = _entries[i]; var b = _entries[j];
            if (!OverviewHotkeyAdapter.Overlaps(signatures[a.Id], signatures[b.Id])) continue;
            if (IsIgnored(a.Id, b.Id, signatures[a.Id], signatures[b.Id])) { a.IgnoredCount++; b.IgnoredCount++; continue; }
            a.Conflicts.Add(new() { Mod = b.Mod, Key = b.Key, Label = b.CurrentLabel, EntryId = b.Id, Section = b.Section });
            b.Conflicts.Add(new() { Mod = a.Mod, Key = a.Key, Label = a.CurrentLabel, EntryId = a.Id, Section = a.Section });
        }

        // The keybindings page is about keys used while playing, so the game's own controls count too (keyboard and
        // controller). A mod key on the same button as Jump / Interact / Use fires both.
        foreach (var entry in _entries)
        {
            string signature = signatures[entry.Id];
            if (signature.Length == 0) continue;
            foreach (var game in GameBindings.Actions)
                if (OverviewHotkeyAdapter.Overlaps(signature, game.Signature))
                    entry.Conflicts.Add(new() { Mod = entry.Mod, GameAction = game.Action, Label = game.Label, Key = game.Display });
        }

        // Controller combos. A single-button combo also fires the game's action on that button; any combo's final button
        // also reaches a mod that reads that controller button itself (the combo only masks it from the game). Two combos
        // on exactly the same buttons cannot exist (recording one moves it).
        foreach (var entry in _entries)
        {
            var combo = GamepadCombos.For(entry.Id); if (combo == null) continue;
            string token = "gamepad:" + OverviewHotkeyAdapter.Canonical(combo.button) + ":";
            string label = GamepadCombos.Label(combo);
            if (combo.Held.Count == 0)
                foreach (var game in GameBindings.Actions)
                    if (game.Signature == token) entry.Conflicts.Add(new() { Mod = entry.Mod, GameAction = game.Action, Label = game.Label, Key = game.Display, Combo = true });
            foreach (var other in _entries)
            {
                if (other == entry || !signatures[other.Id].StartsWith(token, StringComparison.Ordinal)) continue;
                entry.Conflicts.Add(new() { Mod = other.Mod, Key = other.Key, Label = other.CurrentLabel, EntryId = other.Id, Section = other.Section, Combo = true });
                other.Conflicts.Add(new() { Mod = entry.Mod, Key = entry.Key, Label = label, EntryId = entry.Id, Section = entry.Section, Combo = true });
            }
        }

        Changed?.Invoke();
    }

    private string Signature(OverviewHotkeyEntry entry) => entry.Adapter.Signature(entry.Config.BoxedValue);
    private static bool IsIgnored(string a, string b, string signatureA, string signatureB) => State.ignoredHotkeyConflicts.Any(p =>
        (p.idA == a && p.idB == b && p.signatureA == signatureA && p.signatureB == signatureB) ||
        (p.idA == b && p.idB == a && p.signatureA == signatureB && p.signatureB == signatureA));

    public bool SetDescription(OverviewHotkeyEntry entry, string text, out string error)
    {
        // 第 5 章（二次审查）：留空 = 恢复显示配置项说明，连同游戏内模组管理器前缀的旧说明一起清掉。
        var keys = string.IsNullOrWhiteSpace(text)
            ? new[] { Loc.HotkeyDescriptionKey(entry.Guid, entry.Section, entry.Key), Loc.ManagerHotkeyDescriptionKey(entry.Guid, entry.Section, entry.Key) }
            : new[] { Loc.HotkeyDescriptionKey(entry.Guid, entry.Section, entry.Key) };
        bool success = Loc.WriteExternalTranslations(keys, text, out error);
        if (success) Changed?.Invoke();
        return success;
    }

    /// <summary>写入模组的实际配置（立即生效，BepInEx 自动存盘）。</summary>
    public bool TrySet(OverviewHotkeyEntry entry, object value, out string error)
    {
        error = "";
        if (!CheckEntry(entry, out error)) return false;
        string reason = entry.WriteReason;
        if (reason.Length > 0) { error = reason; return false; }
        if (!entry.Adapter.TryConvert(value, out object converted, out error)) return false;
        try
        {
            entry.Config.BoxedValue = converted;
            if (!entry.Config.ConfigFile.SaveOnConfigSet) entry.Config.ConfigFile.Save();
            return true;
        }
        catch (Exception ex)
        {
            // 第 5 章（二次审查）：SaveOnConfigSet 时 BepInEx 在赋值里就存盘；存盘失败时值其实已经改了，提示要说清楚。
            bool applied = Equals(entry.Config.BoxedValue, converted);
            error = (applied
                ? Loc.T("The keybinding is in effect, but the config file could not be saved: ", "快捷键已生效，但配置文件保存失败：", "快捷鍵已生效，但設定檔儲存失敗：")
                : Loc.T("Could not write the keybinding: ", "快捷键写入失败：", "快捷鍵寫入失敗：")) + ex.Message;
            return false;
        }
        finally { Refresh(); }
    }

    public bool RestoreDefault(OverviewHotkeyEntry entry, out string error) => TrySet(entry, entry.Config.DefaultValue, out error);

    public bool Clear(OverviewHotkeyEntry entry, out string error)
    {
        if (!entry.CanClear) { error = Loc.T("The original mod does not allow an unbound value, so this binding cannot be cleared.", "原模组不允许保存未绑定值，无法清空。", "原模組不允許儲存未綁定值，無法清空。"); return false; }
        return TrySet(entry, entry.Adapter.EmptyValue, out error);
    }

    private bool CheckEntry(OverviewHotkeyEntry entry, out string error)
    {
        error = "";
        if (_disposed || !_entries.Contains(entry)) { error = Loc.T("The keybinding is no longer available. Refresh the page.", "快捷键已失效，请刷新页面。", "快捷鍵已失效，請重新整理頁面。"); return false; }
        return true;
    }

    public bool IgnoreConflict(OverviewHotkeyEntry entry, string targetId, out string error)
    {
        if (!CheckEntry(entry, out error)) return false;
        var other = _entries.FirstOrDefault(e => e.Id == targetId && e.Id != entry.Id);
        if (other == null) { error = Loc.T("The conflict target is no longer available. Refresh the page.", "冲突目标已失效，请刷新页面。", "衝突目標已失效，請重新整理頁面。"); return false; }
        string first = Signature(entry), second = Signature(other);
        if (!OverviewHotkeyAdapter.Overlaps(first, second)) { error = Loc.T("These keybindings no longer conflict. Refresh the page.", "这两个快捷键目前没有冲突，请刷新页面。", "這兩個快捷鍵目前沒有衝突，請重新整理頁面。"); return false; }
        if (IsIgnored(entry.Id, other.Id, first, second)) return true;
        var replacement = State.ignoredHotkeyConflicts.ToList();
        bool ascending = string.CompareOrdinal(entry.Id, other.Id) < 0;
        replacement.Add(new() { idA = ascending ? entry.Id : other.Id, idB = ascending ? other.Id : entry.Id,
            signatureA = ascending ? first : second, signatureB = ascending ? second : first });
        return SaveIgnored(replacement, out error);
    }

    public bool RestoreIgnoredFor(OverviewHotkeyEntry entry, out string error)
    {
        if (!CheckEntry(entry, out error)) return false;
        return SaveIgnored(State.ignoredHotkeyConflicts.Where(p => p.idA != entry.Id && p.idB != entry.Id).ToList(), out error);
    }

    public bool ClearIgnored(out string error)
    {
        error = "";
        if (_disposed) { error = Loc.T("The keybindings page has closed. Open it again.", "快捷键页面已关闭，请重新打开。", "快捷鍵頁面已關閉，請重新開啟。"); return false; }
        return SaveIgnored(new(), out error);
    }

    private bool SaveIgnored(List<OverviewIgnoredHotkeyConflict> replacement, out string error)
    {
        var previous = State.ignoredHotkeyConflicts; error = "";
        try { State.ignoredHotkeyConflicts = replacement; HotkeyPageState.Save(); Refresh(); return true; }
        catch (Exception ex) { State.ignoredHotkeyConflicts = previous; error = ex.Message; Refresh(); return false; }
    }

    private void Unsubscribe() { foreach (var file in _subscribedFiles) file.SettingChanged -= OnSettingChanged; _subscribedFiles.Clear(); }
    public void Dispose() { if (_disposed) return; _disposed = true; Unsubscribe(); _entries.Clear(); Changed = null; }
}

/// <summary>The game's own in-play controls (its "Default" action map), read live from the Input System so player
/// rebinds are respected. Menu-only actions and the catch-all AnyKey are left out. Two-button chords (push to talk
/// = LB + RB) are skipped: a single mod key on LB alone does not trigger them.</summary>
internal static class GameBindings
{
    internal sealed class GameAction { public string Action = "", Signature = "", Label = "", Display = ""; }
    private static List<GameAction>? _cache;
    private static int _cacheFrame = -1000;
    private static readonly HashSet<string> Skip = new(StringComparer.OrdinalIgnoreCase)
    { "AnyKey", "Look", "Move", "Navigate", "Navigate Analog", "NavigateWheel", "Point", "Click", "RightClick", "MiddleClick", "ScrollWheel",
      "TrackedDevicePosition", "TrackedDeviceOrientation", "UIConfirm", "UICancel", "UITabLeft", "UITabRight", "OpenDebugMenu", "Submit", "Cancel",
      "TabLeft", "TabRight" };
    internal static IReadOnlyList<GameAction> Actions
    {
        get
        {
            if (_cache != null && UnityEngine.Time.frameCount - _cacheFrame < 600) return _cache;
            _cacheFrame = UnityEngine.Time.frameCount;
            var list = new List<GameAction>();
            try
            {
                var map = UnityEngine.InputSystem.InputSystem.actions != null ? UnityEngine.InputSystem.InputSystem.actions.FindActionMap("Default") : null;
                if (map != null)
                    foreach (var action in map.actions)
                    {
                        if (Skip.Contains(action.name)) continue;
                        foreach (var binding in action.bindings)
                        {
                            if (binding.isComposite || binding.isPartOfComposite) continue;
                            string path = binding.effectivePath ?? "";
                            if (!OverviewHotkeyAdapter.TryPath(path, out string device, out string control)) continue;
                            string signature = device.ToLowerInvariant() + ":" + OverviewHotkeyAdapter.Canonical(control) + ":";
                            if (list.Any(g => g.Action == action.name && g.Signature == signature)) continue;
                            list.Add(new GameAction { Action = action.name, Signature = signature, Label = device + " / " + control, Display = Name(action.name) });
                        }
                    }
            }
            catch (Exception error) { Plugin.Log.LogDebug("[hotkeys] reading game controls failed: " + error.Message); }
            _cache = list;
            return list;
        }
    }

    /// <summary>Readable name for a game action (three languages).</summary>
    internal static string Name(string action) => action switch
    {
        "Jump" => Loc.T("Jump", "跳跃", "跳躍"), "Interact" => Loc.T("Interact", "交互", "互動"), "Drop" => Loc.T("Drop", "丢弃", "丟棄"),
        "Crouch" => Loc.T("Crouch", "蹲下", "蹲下"), "CrouchToggle" => Loc.T("Crouch (toggle)", "蹲下（切换）", "蹲下（切換）"),
        "Sprint" => Loc.T("Sprint", "疾跑", "衝刺"), "SprintToggle" => Loc.T("Sprint (toggle)", "疾跑（切换）", "衝刺（切換）"),
        "UsePrimary" => Loc.T("Use", "使用", "使用"), "UseSecondary" => Loc.T("Use (secondary)", "使用（副）", "使用（副）"),
        "Emote" => Loc.T("Emote", "表情", "表情"), "Ping" => Loc.T("Ping", "标记", "標記"), "PushToTalk" => Loc.T("Push to talk", "按键说话", "按鍵說話"),
        "Pause" => Loc.T("Pause", "暂停", "暫停"), "UnselectSlot" => Loc.T("Put away item", "收起物品", "收起物品"),
        "SelectSlotForward" => Loc.T("Next slot", "下一个物品格", "下一個物品格"), "SelectSlotBackward" => Loc.T("Previous slot", "上一个物品格", "上一個物品格"),
        "ScrollForward" => Loc.T("Scroll forward", "物品栏向前", "物品欄向前"), "ScrollBackward" => Loc.T("Scroll backward", "物品栏向后", "物品欄向後"),
        "SelectBackpack" => Loc.T("Backpack", "背包", "背包"), "SpectateLeft" => Loc.T("Spectate previous", "观战上一个", "觀戰上一個"),
        "SpectateRight" => Loc.T("Spectate next", "观战下一个", "觀戰下一個"),
        "MoveForward" => Loc.T("Move forward", "前进", "前進"), "MoveBackward" => Loc.T("Move back", "后退", "後退"),
        "MoveLeft" => Loc.T("Move left", "左移", "左移"), "MoveRight" => Loc.T("Move right", "右移", "右移"),
        _ when action.StartsWith("Hotbar", StringComparison.Ordinal) => Loc.F("Slot {0}", "物品格 {0}", "物品格 {0}", action.Substring(6)),
        _ => action
    };
}
