# -*- coding: utf-8 -*-
import io, re
root = r"F:\Games\SteamLibrary\steamapps\common\PEAK"
readme = io.open(root + r"\BepInEx\config\ModConfigEnhance\language\_readme.txt", encoding="utf-8").read()
src = io.open(root + r"\MODs\ModConfigEnhance\src\ModConfigTranslationExport.cs", encoding="utf-8").read()
i = src.index("string lang = XUnityBridge.TargetLanguage")
j = src.index("HashSet<string> seen = new HashSet<string>", i)
seg = src[i:j]

def unescape(t):
    return t.replace("\\\\", "\\").replace('\\"', '"')

out = []
for line in seg.split("\n"):
    line = line.strip()
    if not line.startswith("sb.Append("):
        continue
    parts = re.findall(r'Append\(("(?:[^"\\]|\\.)*"|[^)]*)\)', line)
    text = ""
    for p in parts:
        if p.startswith('"'):
            text += unescape(p[1:-1])
        elif "lang" in p: text += "zh"
        elif "PluginVersion" in p: text += "1.0.2"
        elif "MaxChoices" in p: text += "40"
        elif "DateTime" in p: text += "2026-09-11 21:00"
        elif "untranslatedOnly" in p: text += " — texts to translate / 待翻译文本 "
        elif "mods.Count" in p: text += "N"
        elif "'\\n'" in p: text += "\n"
        else: text += ""
    out.append(text.rstrip("\n"))
header = "\n".join(out)
marker = "// ==== ModConfig Enhance UI (MCE_ keys: translate the right side only, never the key) ====\n"
header = header.replace(marker, marker + "MCE_SEARCH=Search\nMCE_MODS=MODS\nMCE_BTN_EXPORT=Export texts\nMCE_LANG_FOLLOW=Follow game language\nMCE_NOTICE_EXPORTED=Exported {0} entries\n…（共 54 条）\n")

rd = io.open(root + r"\MODs\ModConfigEnhance\README.md", encoding="utf-8").read()
a = rd.index("## 语言文件"); b = rd.index("## 配置（")

md = "# ModConfig Enhance 1.0.2 用户教学文本（审核稿）\n\n"
md += "三处面向用户的说明文字，按用户看到的顺序。`_readme.txt` 是游戏 1.0.2 启动时生成的原样；导出文件头按源码还原（目标语言代入 zh）；README 段落摘自工程 README.md。\n\n---\n\n"
md += "## 一、`config\\ModConfigEnhance\\language\\_readme.txt`（每次启动重写）\n\n```\n" + readme.rstrip() + "\n```\n\n---\n\n"
md += "## 二、导出文件头（`config\\ModConfigEnhance\\export\\待翻译_*.txt` 开头）\n\n```\n" + header + "\n```\n\n（之后紧跟 `// ==== ModConfig Enhance UI (MCE_ keys …) ====` 块，再是各模组分块。）\n\n---\n\n"
md += "## 三、README.md「语言文件」段落\n\n" + rd[a:b]
p = root + r"\MODs\ModConfigEnhance\验证\用户教学文本_审核稿_1.0.2.md"
io.open(p, "w", encoding="utf-8", newline="\n").write(md)
print(p, len(md))
