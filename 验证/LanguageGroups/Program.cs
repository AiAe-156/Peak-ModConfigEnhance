using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using ModConfigEnhance;

string root = Path.Combine(Path.GetTempPath(), "MCE_LanguageGroups_" + Guid.NewGuid().ToString("N"));
Paths.ConfigPath = root;
Directory.CreateDirectory(Loc.LanguageDirectory);
void Write(string name, string text) => File.WriteAllText(Path.Combine(Loc.LanguageDirectory, name), text, new UTF8Encoding(false));
void Assert(bool value, string message) { if (!value) throw new Exception(message); }
string Value(string key) => LocalizedText.mainTable[key][(int)LocalizedText.CURRENT_LANGUAGE];

try
{
    Write("zh.txt", "// display: 简体中文\nMCE_SEARCH=基础\nMCE_CLEAR=CLEAR\nMCE_BTN_UNTRANSLATED=查漏翻\nA=甲\nC=有效\n");
    Write("ZH_20_补翻.txt", "MCE_SEARCH=补翻\nA=乙\nC=C\nD=有效\nD=D\n");
    Write("zh_90_个人修正.txt", "MCE_SEARCH=个人\nA=最终\n");
    Write("en.txt", "MCE_SEARCH=Search\n");
    Write("en_20_override.txt", "MCE_SEARCH=Find\nMCE_SEARCH=Search\n");
    Write("en-gb.txt", "MCE_SEARCH=Search UK\n");
    Write("zh-tw_20.txt", "MCE_SEARCH=搜尋\n");
    Write("zh-hk.txt", "MCE_SEARCH=搜尋香港\n");
    Write("_ignored.txt", "MCE_SEARCH=不应读取\n");

    List<Loc.LanguageFile> groups = Loc.Scan();
    Loc.LanguageFile zh = groups.Single(group => group.Code == "zh-cn");
    Assert(zh.FileCount == 3, "zh/zh-cn 别名未合为三个文件");
    Assert(Path.GetFileName(zh.Paths[0]) == "zh.txt", "无后缀基础文件没有排在首位");
    Assert(Path.GetFileName(zh.Paths[1]) == "ZH_20_补翻.txt" && Path.GetFileName(zh.Paths[2]) == "zh_90_个人修正.txt",
        "分包没有按 OrdinalIgnoreCase 文件名排序");
    Assert(groups.Single(group => group.Code == "en-us").FileCount == 2, "en 未归一到 en-us");
    Assert(groups.Any(group => group.Code == "en-gb"), "en-gb 区域组被错误并入 en-us");
    Assert(groups.Any(group => group.Code == "zh-tw") && groups.Any(group => group.Code == "zh-hk"), "繁中区域组没有保持独立");

    Loc.LanguageFile merged = Loc.ParseGroup("ZH_cn_旧配置名");
    Assert(merged.Entries["SEARCH"] == "个人", "后文件有效 MCE 译文未覆盖");
    Assert(merged.Entries["A"] == "最终", "文件名顺序覆盖不正确");
    Assert(merged.Entries["C"] == "有效", "原文占位覆盖了已有译文");
    Assert(merged.Entries["D"] == "有效", "同文件后置占位覆盖了已有译文");
    Assert(Loc.SameLanguage("zh-cn", "zh"), "zh 未映射到简中");
    Assert(!Loc.SameLanguage("zh-tw", "zh"), "简繁被错误合并");
    Assert(!Loc.SameLanguage("en-gb", "en-us"), "两个明确英文区域被错误合并");

    Plugin.SwitchXUnity.Value = true;
    XUnityBridge.Reset(Path.Combine(root, "Translation"), "en");
    Loc.ApplyResult applied = Loc.Apply("zh-cn_任意备注", notify: false, refresh: false);
    Assert(applied.Success, "有效语言组 Apply 失败");
    Assert(applied.Message.Contains("zh.txt") && applied.Message.Contains("ZH_20_补翻.txt") && applied.Message.Contains("zh_90_个人修正.txt"), "加载结果未列出全部文件名");
    Assert(applied.Message.IndexOf("zh.txt", StringComparison.Ordinal) < applied.Message.IndexOf("ZH_20_补翻.txt", StringComparison.Ordinal), "文件清单未按实际加载顺序显示");
    Assert(Value("MCE_BTN_UNTRANSLATED") == "补漏翻", "旧词典按钮名称未迁移");
    Assert(Value("MCE_SEARCH") == "个人", "界面语言表与合并结果不一致");
    string mirror = Path.Combine(root, "Translation", "zh-cn", "Text", "ModConfigEnhance_zh-cn.txt");
    Assert(File.Exists(mirror), "XUnity 镜像没有写入所选语言目录");
    string mirrorText = File.ReadAllText(mirror, Encoding.UTF8);
    Assert(mirrorText.Contains("A=最终") && mirrorText.Contains("C=有效") && mirrorText.Contains("D=有效"), "XUnity 镜像与合并词典不一致");
    Assert(XUnityBridge.SetLanguageCalls == 1 && XUnityBridge.ReloadCalls == 1, "XUnity 未按一次 Apply 切换并重载一次");

    string oldTable = Value("MCE_SEARCH");
    string oldMirror = File.ReadAllText(mirror, Encoding.UTF8);
    Write("zh_99_bad.txt", "broken line\n");
    Loc.ApplyResult rejected = Loc.Apply("zh-cn", notify: false, refresh: false);
    Assert(!rejected.Success, "坏分包没有让整组应用失败");
    Assert(Value("MCE_SEARCH") == oldTable, "坏分包破坏了旧语言表");
    Assert(File.ReadAllText(mirror, Encoding.UTF8) == oldMirror, "坏分包破坏了旧 XUnity 镜像");
    Assert(XUnityBridge.SetLanguageCalls == 1 && XUnityBridge.ReloadCalls == 1, "坏分包后仍触发了 XUnity 变更");

    File.Move(Path.Combine(Loc.LanguageDirectory, "zh_99_bad.txt"), Path.Combine(Loc.LanguageDirectory, "zh-tw_99_bad.txt"));
    LocalizedText.CURRENT_LANGUAGE = LocalizedText.Language.SimplifiedChinese;
    Loc.ApplyResult followed = Loc.Apply(Loc.FollowGame, notify: false, refresh: false);
    Assert(followed.Success && Value("MCE_SEARCH") == "个人", "跟随模式受其它语言组坏文件影响");
    Assert(Value("MCE_CLEAR") == "清空", "MCE 英文预填覆盖了跟随模式的内建中文");
    Assert(File.ReadAllText(mirror, Encoding.UTF8) == oldMirror, "跟随当前组后的镜像内容与指定模式不一致");

    Plugin.LanguageFile.Value = "zh-cn";
    HashSet<string> translated = Loc.CurrentTranslatedTermKeys();
    Assert(translated.Contains("SEARCH"), "已翻 MCE 键未被查漏翻识别");
    Plugin.LanguageFile.Value = "en-us";
    Assert(Loc.ParseGroup("en-us").Entries["SEARCH"] == "Find", "英文组的后置 MCE 预填英文覆盖了有效自定义文本");
    Assert(Loc.CurrentTranslatedTermKeys().Contains("SEARCH"), "英文组的完整 MCE 文本被误判为未翻");

    Console.WriteLine("PASS: production Loc.cs grouping/apply/follow/mirror/atomic-failure/untranslated semantics");
}
finally
{
    if (Directory.Exists(root)) Directory.Delete(root, true);
}
