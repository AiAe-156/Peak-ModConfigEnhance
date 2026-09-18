# -*- coding: utf-8 -*-
import io, re
root = r"F:\Games\SteamLibrary\steamapps\common\PEAK\MODs\ModConfigEnhance"

# ---- 1. _readme.txt（简中版，从源码常量取）----
src = io.open(root + r"\src\LanguageReadme.cs", encoding="utf-8").read()
m = re.search(r'private const string ZhCn = """\n(.*?)\n\s*""";', src, re.S)
readme = "\n".join(l[8:] if l.startswith("        ") else l for l in m.group(1).split("\n")).replace("{VERSION}", "1.0.3").rstrip()

# ---- 2. 导出文件头（简中版，按源码手写）----
header = r"""// ==============================================================
//   ModConfig Enhance 1.0.3 · 待翻译文本 · 2026-09-11 21:50
// ==============================================================
//
// # 这是什么
//
//   第一块  MCE_…=…       本模组自己的界面。键是稳定 ID，只翻等号右边。
//   之后    原文=…         ModConfig「模组设置」页会显示的全部文字（模组名、分区、选项名、说明、
//                          下拉选项），按模组分块，XUnity 词典格式。
//
// # 怎么用（四步）
//
//   1  粗筛   游戏里勾「仅导出已选」，去掉不需要的模组，再导出一次（本模组永远包含）
//   2  翻译   填等号右边（可整份交给 AI）；右边留空的行会被忽略
//   3  改名   存成 UTF-8，改名为小写语言码（zh.txt / de-de.txt / ja-jp.txt……）
//            放进 BepInEx\config\ModConfigEnhance\language\
//   4  启用   游戏里左栏第三行 → 刷新 → 选中它
//
//   本模组界面与 XUnity AutoTranslator 一起切到该语言（XUnity 的词典目录变成 Translation\<码>\Text\，
//   没翻的文本显示原文），不用重启。
//   「重载翻译」(Alt+R) 只在手改文件后重读用；「查漏翻」是可选的补漏工具。
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
MCE_LANG_FOLLOW=Follow game language
MCE_NOTICE_EXPORTED=Exported {0} entries
…（共 54 条）

// ==== Auto Item Multiplier (com.github.LengSword.AutoItemMultiplier 0.2.0) ====
// -- [General]
Enable item multiplier=
…"""

# ---- 3. README 段落 ----
rd = io.open(root + r"\README.md", encoding="utf-8").read()
a = rd.index("## 语言文件"); b = rd.index("## 配置（")

md = "# ModConfig Enhance 1.0.3 用户教学文本（审核稿 v2）\n\n"
md += "三处文字统一骨架：**这是什么 → 文件格式 → 四步流程 → 选中后发生什么 → 其他按钮 → 投稿**。\n"
md += "前两处是游戏运行时写出的纯文本（`//` 注释），这里给的是简中版；英文、繁中版结构完全相同。第三处摘自工程 README.md。\n\n---\n\n"
md += "## 一、`config\\ModConfigEnhance\\language\\_readme.txt`\n\n```\n" + readme + "\n```\n\n---\n\n"
md += "## 二、导出文件头（`export\\待翻译_*.txt` 开头，「本次导出」一节只在查漏翻 / 仅导出已选时出现）\n\n```\n" + header + "\n```\n\n---\n\n"
md += "## 三、README.md「语言文件」「翻译工具」两段\n\n" + rd[a:b]
p = root + r"\验证\用户教学文本_审核稿_v2_1.0.3.md"
io.open(p, "w", encoding="utf-8", newline="\n").write(md)
print(p)
