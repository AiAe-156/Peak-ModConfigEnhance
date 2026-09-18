# -*- coding: utf-8 -*-
import io, re
root = r"F:\Games\SteamLibrary\steamapps\common\PEAK\MODs\ModConfigEnhance"

src = io.open(root + r"\src\LanguageReadme.cs", encoding="utf-8").read()
m = re.search(r'private const string ZhCn = """\n(.*?)\n\s*""";', src, re.S)
readme = "\n".join(l[8:] if l.startswith("        ") else l for l in m.group(1).split("\n")).replace("{VERSION}", "1.0.3").rstrip()

header = r"""// ==============================================================
//   ModConfig Enhance 1.0.3 · 待翻译文本 · 2026-09-11 22:55
// ==============================================================
//
//   用法见 BepInEx\config\ModConfigEnhance\readme.md（导出 → 翻译 → 改名为语言码 → 放进 language\ → 刷新 → 选中）。
//
// # 给翻译者 / AI 的规则
//
//   · // 开头是注释；「// ==== 模组名 (GUID 版本) ====」与「// -- [分区]」只是分块标记，不用翻、不要删。
//   · \n 是换行、\= 是等号，译文保持同样写法；数字、单位、按键名、模组名、GUID、路径照抄。
//   · 单个键名 / 枚举值（A、F1、auto、eu、Left）是下拉框里的选项，一般照抄或留空。
//   · 同一句原文全局只出现一次（XUnity 词典是全局的），后面模组里重复的不再列出。
//   · 已自动跳过：KeyCode / 按键名清单、超过 40 项的选项清单。已是中文或中英双语的行留空即可。
//
// # 本次导出                                   ← 只在查漏翻 / 仅导出已选时出现
//
//   只含未翻译条目。「已翻」直接问 XUnity 当前加载的词典（精确条目、r:/sr: 正则、_Substitutions 都算），与游戏里实际替换一致。
//   已含中文的原文按「无需翻译」计，不列出。
//   仅导出已选：左栏「导出」列勾选的 25 个模组。

// ==== ModConfig Enhance 界面（MCE_ 键：只翻等号右边，键不要动）====
MCE_SEARCH=Search
MCE_MODS=MODS
MCE_BTN_EXPORT=Export texts
…（共 54 条）

// ==== Auto Item Multiplier (com.github.LengSword.AutoItemMultiplier 0.2.0) ====
// -- [General]
Enable item multiplier=
…"""

rd = io.open(root + r"\README.md", encoding="utf-8").read()
a = rd.index("## 语言文件"); b = rd.index("## 配置（")

md = "# ModConfig Enhance 1.0.3 用户教学文本（审核稿 v3）\n\n"
md += "改动：说明文件从 `language\\_readme.txt` 改为 **`config\\ModConfigEnhance\\readme.md`**（Markdown，根目录），导出文件头只留「翻译规则 + 本次导出」，教程一句话指到 readme.md。\n"
md += "readme.md 与导出头按当前界面语言单语输出，这里是简中版；英文、繁中版结构相同。\n\n---\n\n"
md += "## 一、`config\\ModConfigEnhance\\readme.md`（下面就是它渲染后的样子）\n\n" + readme.replace("\n# ", "\n### ").replace("# ModConfig Enhance", "### ModConfig Enhance", 1).replace("\n## ", "\n#### ") + "\n\n---\n\n"
md += "## 二、导出文件头（`export\\待翻译_*.txt` 开头）\n\n```\n" + header + "\n```\n\n---\n\n"
md += "## 三、模组根目录 README.md「语言文件」「翻译工具」两段\n\n" + rd[a:b].replace("\n## ", "\n### ").replace("\n### ", "\n#### ", 0)
p = root + r"\验证\用户教学文本_审核稿_v3_1.0.3.md"
io.open(p, "w", encoding="utf-8", newline="\n").write(md)
print(p)
