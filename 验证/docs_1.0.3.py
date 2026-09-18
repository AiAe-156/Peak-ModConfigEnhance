# -*- coding: utf-8 -*-
import io
root = r"F:\Games\SteamLibrary\steamapps\common\PEAK\MODs\ModConfigEnhance"

p = root + r"\CHANGELOG.md"
s = io.open(p, encoding="utf-8").read()
entry = r'''## 1.0.3 — 2026-09-11

### 语言下拉 = 整个游戏的翻译语言（面向整合包）

- **选语言文件时直接把 XUnity AutoTranslator 切到该语言**，不再受它 ini 里 `Language=` 的限制、也不再往 `Translation\zh` 里塞别的语言：
  反射改 `Settings.Language` 并按 `{Lang}` 模板重算 `TranslationsPath` / `AutoTranslationsFilePath` / `Substitution` / `Pre` / `PostprocessorsFilePath`
  与目标语言的空格规则（`XUnityBridge.SetLanguage`，对着 5.6.1 的字段做），词典目录随之变成 `Translation\<码>\Text\`；
  文件里非 `MCE_` 的行写成那里的 `ModConfigEnhance_<码>.txt`（其他语言目录里的旧镜像先删）；`ReloadTranslations` 后词典里没有的文本
  XUnity 自己还原成原文——选 en-us 就是全英文。`AutoTranslatorConfig.ini` 的 `Language=` 同步改掉，下次启动一致。
  语言码映射：`Translation\` 下已有同名目录用同名，有同语言目录（`zh-cn` ↔ `zh`）沿用它、不另起目录，否则用文件名。
- 「跟随游戏语言」同样驱动 XUnity（游戏切语言 → XUnity 跟着切）。
- cfg 项 `Mirror language file to XUnity` 改名 **`Switch XUnity language with the language file`**（默认开；关掉完全不碰 XUnity）。
  说明面板提示改为「XUnity 已切到「xx」（交给它 N 条）」/「无法在运行时切换 XUnity」。
- **说明文字单语化**：`_readme.txt`、导出文件头按当前界面语言（选了文件按文件语言码，跟随游戏按游戏语言）输出简中 / 繁中 / 英文之一，
  `_readme.txt` 每次切语言后重写。README.md 是静态文件，保持中英双语。
- 随部署把 `Translation\zh\Text` 下的旧词典（mods / checkpointssave / plantholderoverhaul / SaveMod_Options / terrainrandomiser）
  合并进用户的 `zh-cn.txt`（新增 684 条，去重 29 条，22 条同键不同译文保留 zh-cn.txt 原有；报告在 `验证\zh-cn_merge_report_*.md`）。
  原文件暂留原位（与镜像重复无害），是否移走等用户确认。

'''
s = s.replace("# 更新日志\n\n", "# 更新日志\n\n" + entry, 1)
io.open(p, "w", encoding="utf-8", newline="\n").write(s)

p = root + r"\README.md"
s = io.open(p, encoding="utf-8").read()
a = s.index("**流程**："); b = s.index("**投稿**：")
new = r'''**流程**：导出翻译 → 填译文 → 改名放进 `language\` → 游戏里第三行「刷新」→ 下拉选中。选中那一刻，**整个游戏切到该语言，不用重启**：本模组界面立刻换（只刷 `MCE_` 索引的组件）；XUnity 的目标语言与词典目录切成 `Translation\<码>\Text\`，其余行写成那里的 `ModConfigEnhance_<码>.txt`，重载后词典里没有的文本还原成原文——所以选 en-us 就是全英文；`AutoTranslatorConfig.ini` 的 `Language=` 同步改掉，下次启动一致。语言码映射：`Translation\` 下已有同名目录用同名，有同语言目录（`zh-cn` ↔ `zh`）沿用它，否则用文件名。「重载翻译」（Alt+R）只在手改文件后重读用；「查漏翻」是补漏工具，可选。

**下拉框第一项「跟随游戏语言」**（默认）：按游戏语言用内建的简 / 繁 / 英，其他语言回落英文；XUnity 同样跟着游戏语言切。文件有错（缺等号、没有有效行、文件不存在）会在说明面板用**橙色**带行号提示。

**开关** `Switch XUnity language with the language file`（默认开）；关掉则只翻本模组界面、完全不碰 XUnity。卸载本模组后可手动删各语言目录里的 `ModConfigEnhance_*.txt`。

'''
s = s[:a] + new + s[b:]
s = s.replace("| 2. Localization | UI localization / Translation tools / Mirror language file to XUnity | 前两项改开关需重启 |",
              "| 2. Localization | UI localization / Translation tools / Switch XUnity language with the language file | 前两项改开关需重启 |")
s = s.replace("XUnity 的目标语言只在启动时读 `AutoTranslatorConfig.ini` 的 `Language=`；改成你的语言码（如 `de`）并重启，`Translation\\de\\Text\\` 会自动建出来，本模组的镜像与你导出的词典都往那里放。`FromLanguage` 不影响词典命中，只管「没翻到的文字要不要送去机翻」，通常不用动。",
              "不用手改 `AutoTranslatorConfig.ini`：在语言下拉里选你的文件，XUnity 就切到该语言并自动建 `Translation\\<码>\\Text\\`，ini 的 `Language=` 也会同步写好。`FromLanguage` 不影响词典命中，只管「没翻到的文字要不要送去机翻」，通常不用动。")
# 英文段
old_en_start = s.index('Lines after "Settings of this mod"')
old_en_end = s.index("Finished translations are very welcome")
s = s[:old_en_start] + '''Picking a file also **switches XUnity AutoTranslator to that language at runtime**: its dictionary folder becomes `Translation\\<code>\\Text\\`, the non-`MCE_` lines are written there as `ModConfigEnhance_<code>.txt`, dictionaries reload and texts without a translation revert to the original — picking `en-us` gives you the untranslated game. `Language=` in `AutoTranslatorConfig.ini` is updated for the next start. Disable with `Switch XUnity language with the language file`; delete `ModConfigEnhance_*.txt` yourself if you uninstall.

''' + s[old_en_end:]
io.open(p, "w", encoding="utf-8", newline="\n").write(s)
print("ok")
