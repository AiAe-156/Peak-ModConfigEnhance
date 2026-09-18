using System.Reflection;
using ModConfigEnhance;
using PEAKLib.ModConfig.Components;
using PEAKLib.UI;
using UnityEngine;
using UnityEngine.UI;

static class Program
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Main()
    {
        var host = new GameObject("host");
        var cascade = host.AddComponent<SidebarCascade>();
        var tabs = host.AddComponent<PeakHorizontalTabs>();
        var scroll = host.AddComponent<ScrollRect>();
        scroll.content = new GameObject("content").transform;
        var header = new GameObject("HEADER");
        var rows = new[] { "A", "B", "C", "D", "E" }.Select(n => AddRow(scroll.content!, n)).ToArray();
        cascade.ModTabs = tabs;
        cascade.Header = header;

        Select(rows, 2);
        AssertWaves(cascade, new[] { new[] { "HEADER", "C" }, new[] { "B", "D" }, new[] { "A", "E" } }, "中间");
        Select(rows, 4);
        AssertWaves(cascade, new[] { new[] { "HEADER", "E" }, new[] { "D" }, new[] { "C" }, new[] { "B" }, new[] { "A" } }, "底部");
        Select(rows, 0);
        AssertWaves(cascade, new[] { new[] { "HEADER", "A" }, new[] { "B" }, new[] { "C" }, new[] { "D" }, new[] { "E" } }, "顶部");
        Select(rows, -1);
        AssertWaves(cascade, new[] { new[] { "HEADER" }, new[] { "A" }, new[] { "B" }, new[] { "C" }, new[] { "D" }, new[] { "E" } }, "无选中");

        var restart = typeof(SidebarCascade).GetMethod("Restart", Private)!;
        var modPrepared = (List<CanvasGroup>)typeof(SidebarCascade).GetField("_modPrepared", Private)!.GetValue(cascade)!;
        var modFading = (List<CanvasGroup>)typeof(SidebarCascade).GetField("_modFading", Private)!.GetValue(cascade)!;
        var sectionPrepared = (List<CanvasGroup>)typeof(SidebarCascade).GetField("_sectionPrepared", Private)!.GetValue(cascade)!;
        var sectionFading = (List<CanvasGroup>)typeof(SidebarCascade).GetField("_sectionFading", Private)!.GetValue(cascade)!;
        var modRow = new GameObject("mod"); var sectionRow = new GameObject("section");
        var modGroup = modRow.AddComponent<CanvasGroup>(); var sectionGroup = sectionRow.AddComponent<CanvasGroup>();
        var pendingRow = new GameObject("pending");
        var pendingGroup = pendingRow.AddComponent<CanvasGroup>();
        var modWaves = new List<List<GameObject>> { new() { modRow }, new() { pendingRow } };
        var sectionWaves = new List<List<GameObject>> { new() { sectionRow } };
        var oldModRun = (Coroutine)restart.Invoke(cascade, new object?[] { null, modWaves, modPrepared, modFading })!;
        var sectionRun = (Coroutine)restart.Invoke(cascade, new object?[] { null, sectionWaves, sectionPrepared, sectionFading })!;
        if (!modFading.Contains(modGroup) || modFading.Contains(pendingGroup)) throw new Exception("首波必须立即启动，后续波仍待播");
        var modRun = (Coroutine)restart.Invoke(cascade, new object?[] { oldModRun, modWaves, modPrepared, modFading })!;
        if (!oldModRun.Stopped || sectionRun.Stopped) throw new Exception("重启必须只停止旧模组协程");
        typeof(SidebarCascade).GetField("_modRun", Private)!.SetValue(cascade, modRun);
        typeof(SidebarCascade).GetField("_sectionRun", Private)!.SetValue(cascade, sectionRun);
        if (sectionGroup.alpha != 0f) throw new Exception("重启模组波清理了分区 pending 行");
        typeof(SidebarCascade).GetMethod("OnDisable", Private)!.Invoke(cascade, null);
        if (modGroup.alpha != 1f || sectionGroup.alpha != 1f || pendingGroup.alpha != 1f) throw new Exception("OnDisable 未恢复已播或待播行");
        if (!modRun.Stopped || !sectionRun.Stopped) throw new Exception("OnDisable 必须停止两侧协程");
        Console.WriteLine("SidebarCascadeTests: PASS (真实源码编译, 4 wave cases, simultaneous boundaries, ownership cleanup)");
    }

    static void Select(GameObject[] rows, int index)
    {
        for (int i = 0; i < rows.Length; i++) rows[i].GetComponent<ModdedTABSButton>()!.Selected = i == index;
    }
    static GameObject AddRow(Transform content, string name)
    {
        var row = new GameObject(name); row.AddComponent<ModdedTABSButton>(); content.AddChild(row); return row;
    }
    static void AssertWaves(SidebarCascade cascade, string[][] expected, string label)
    {
        var rows = new List<GameObject>();
        var header = cascade.Header!; rows.Add(header);
        foreach (var child in cascade.ModTabs!.GetComponent<ScrollRect>()!.content!) rows.Add(child.gameObject);
        var waves = (List<List<GameObject>>)typeof(SidebarCascade).GetMethod("BuildModWaves", Private)!.Invoke(cascade, new object[] { rows })!;
        if (waves.Count != expected.Length) throw new Exception($"{label}: 波次数量错误");
        for (var i = 0; i < expected.Length; i++)
        {
            var actual = waves[i].Select(x => x.name).ToArray();
            if (!actual.SequenceEqual(expected[i])) throw new Exception($"{label}: 第 {i} 波错误: {string.Join(',', actual)}");
        }
    }
}
