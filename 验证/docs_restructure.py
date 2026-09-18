# -*- coding: utf-8 -*-
import io
root = r"F:\Games\SteamLibrary\steamapps\common\PEAK\MODs\ModConfigEnhance"

# ---------- 导出文件头：分节 + 编号，单语 ----------
p = root + r"\src\ModConfigTranslationExport.cs"
s = io.open(p, encoding="utf-8").read()
i = s.index('        string lang = XUnityBridge.TargetLanguage ?? "zh";')
j = s.index('        foreach (Loc.Term term in Loc.Terms)\n        {\n            sb.Append(Loc.KeyPrefix)')
HEADER = r'''        string lang = XUnityBridge.TargetLanguage ?? "zh";
        string L(string en, string zhCn, string zhTw) => Loc.T(en, zhCn, zhTw);
        void Line(string text) => sb.Append(text).Append('\n');

        Line("// ==============================================================");
        Line("//   ModConfig Enhance " + Plugin.PluginVersion + " · " + (untranslatedOnly
            ? L("untranslated texts", "未翻译文本", "未翻譯文字")
            : L("texts to translate", "待翻译文本", "待翻譯文字")) + " · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        Line("// ==============================================================");
        Line("//");
        Line(L("// # What this is", "// # 这是什么", "// # 這是什麼"));
        Line("//");
        Line(L("//   Block 1  MCE_...=...   this mod's own UI. Keys are stable IDs - translate the right side only.",
               "//   第一块  MCE_…=…       本模组自己的界面。键是稳定 ID，只翻等号右边。",
               "//   第一塊  MCE_…=…       本模組自己的介面。鍵是穩定 ID，只翻等號右邊。"));
        Line(L("//   Block 2+ original=...  every text the ModConfig settings page shows (mod names, sections, option names,",
               "//   之后    原文=…         ModConfig「模组设置」页会显示的全部文字（模组名、分区、选项名、说明、",
               "//   之後    原文=…         ModConfig「模組設定」頁會顯示的全部文字（模組名、分區、選項名、說明、"));
        Line(L("//                          descriptions, dropdown choices), grouped by mod, in XUnity dictionary format.",
               "//                          下拉选项），按模组分块，XUnity 词典格式。",
               "//                          下拉選項），按模組分塊，XUnity 詞典格式。"));
        Line("//");
        Line(L("// # How to use (four steps)", "// # 怎么用（四步）", "// # 怎麼用（四步）"));
        Line("//");
        Line(L("//   1  Trim       tick \"Selected only\" in the game, untick mods you don't need, export again (this mod is always included)",
               "//   1  粗筛   游戏里勾「仅导出已选」，去掉不需要的模组，再导出一次（本模组永远包含）",
               "//   1  粗篩   遊戲裡勾「僅導出已選」，去掉不需要的模組，再導出一次（本模組永遠包含）"));
        Line(L("//   2  Translate  fill the right side of each \"=\" (an AI can do the whole file); empty right sides are ignored",
               "//   2  翻译   填等号右边（可整份交给 AI）；右边留空的行会被忽略",
               "//   2  翻譯   填等號右邊（可整份交給 AI）；右邊留空的行會被忽略"));
        Line(L("//   3  Rename     save as UTF-8, rename to a lowercase language code (" + lang + ".txt / de-de.txt / ja-jp.txt ...)",
               "//   3  改名   存成 UTF-8，改名为小写语言码（" + lang + ".txt / de-de.txt / ja-jp.txt……）",
               "//   3  改名   存成 UTF-8，改名為小寫語言碼（" + lang + ".txt / de-de.txt / ja-jp.txt……）"));
        Line(L("//                 and put it in BepInEx\\config\\ModConfigEnhance\\language\\",
               "//            放进 BepInEx\\config\\ModConfigEnhance\\language\\",
               "//            放進 BepInEx\\config\\ModConfigEnhance\\language\\"));
        Line(L("//   4  Enable     in the game: left column, third row > Refresh > pick the file",
               "//   4  启用   游戏里左栏第三行 → 刷新 → 选中它",
               "//   4  啟用   遊戲裡左欄第三行 → 重新整理 → 選中它"));
        Line("//");
        Line(L("//   This mod's UI and XUnity AutoTranslator both switch to that language at once (XUnity's dictionary folder becomes",
               "//   本模组界面与 XUnity AutoTranslator 一起切到该语言（XUnity 的词典目录变成 Translation\\<码>\\Text\\，",
               "//   本模組介面與 XUnity AutoTranslator 一起切到該語言（XUnity 的詞典目錄變成 Translation\\<碼>\\Text\\，"));
        Line(L("//   Translation\\<code>\\Text\\, texts without a translation show the original) - no restart.",
               "//   没翻的文本显示原文），不用重启。",
               "//   沒翻的文字顯示原文），不用重啟。"));
        Line(L("//   \"Reload texts\" (Alt+R) only re-reads after hand edits; \"Untranslated\" is an optional check.",
               "//   「重载翻译」(Alt+R) 只在手改文件后重读用；「查漏翻」是可选的补漏工具。",
               "//   「重載翻譯」(Alt+R) 只在手改檔案後重讀用；「查漏翻」是可選的補漏工具。"));
        Line("//");
        Line(L("// # Rules for translators / AI", "// # 给翻译者 / AI 的规则", "// # 給翻譯者 / AI 的規則"));
        Line("//");
        Line(L("//   - // lines are comments. \"// ==== Mod (GUID version) ====\" and \"// -- [Section]\" are block markers: don't translate or delete them.",
               "//   · // 开头是注释；「// ==== 模组名 (GUID 版本) ====」与「// -- [分区]」只是分块标记，不用翻、不要删。",
               "//   · // 開頭是註解；「// ==== 模組名 (GUID 版本) ====」與「// -- [分區]」只是分塊標記，不用翻、不要刪。"));
        Line(L("//   - \\n is a line break, \\= an equals sign - keep the same notation. Copy numbers, units, key names, mod names, GUIDs and paths as-is.",
               "//   · \\n 是换行、\\= 是等号，译文保持同样写法；数字、单位、按键名、模组名、GUID、路径照抄。",
               "//   · \\n 是換行、\\= 是等號，譯文保持同樣寫法；數字、單位、按鍵名、模組名、GUID、路徑照抄。"));
        Line(L("//   - Single key / enum names (A, F1, auto, eu, Left) are dropdown choices: usually copy or leave empty.",
               "//   · 单个键名 / 枚举值（A、F1、auto、eu、Left）是下拉框里的选项，一般照抄或留空。",
               "//   · 單個鍵名 / 列舉值（A、F1、auto、eu、Left）是下拉框裡的選項，一般照抄或留空。"));
        Line(L("//   - Each original appears once globally (the XUnity dictionary is global); repeats in later mods are not listed.",
               "//   · 同一句原文全局只出现一次（XUnity 词典是全局的），后面模组里重复的不再列出。",
               "//   · 同一句原文全域只出現一次（XUnity 詞典是全域的），後面模組裡重複的不再列出。"));
        Line(L("//   - Skipped automatically: KeyCode / key-name lists and choice lists longer than " + MaxChoices + " items.",
               "//   · 已自动跳过：KeyCode / 按键名清单、超过 " + MaxChoices + " 项的选项清单。已是中文或中英双语的行留空即可。",
               "//   · 已自動跳過：KeyCode / 按鍵名清單、超過 " + MaxChoices + " 項的選項清單。已是中文或中英雙語的行留空即可。"));
        if (untranslatedOnly || ModConfigSidebarWidgets.ExportSelectedOnly)
        {
            Line("//");
            Line(L("// # About this export", "// # 本次导出", "// # 本次導出"));
            Line("//");
            if (untranslatedOnly)
            {
                Line(liveQuery
                    ? L("//   Untranslated only. \"Translated\" = XUnity's loaded dictionary says so (exact entries, r:/sr: regex and _Substitutions included), same as in-game.",
                        "//   只含未翻译条目。「已翻」直接问 XUnity 当前加载的词典（精确条目、r:/sr: 正则、_Substitutions 都算），与游戏里实际替换一致。",
                        "//   只含未翻譯條目。「已翻」直接問 XUnity 目前載入的詞典（精確條目、r:/sr: 正則、_Substitutions 都算），與遊戲裡實際替換一致。")
                    : L("//   Untranslated only. XUnity's dictionary API was unavailable; fell back to exact matches against every .txt under Translation - regex-covered entries are reported as untranslated.",
                        "//   只含未翻译条目。XUnity 词典接口不可用，退回按 Translation 目录下所有 .txt 的原文精确匹配；用正则覆盖的条目会误报。",
                        "//   只含未翻譯條目。XUnity 詞典介面不可用，退回按 Translation 目錄下所有 .txt 的原文精確匹配；用正則覆蓋的條目會誤報。"));
                Line(L("//   Originals that already contain CJK characters count as \"no need\" and are not listed.",
                       "//   已含中文的原文按「无需翻译」计，不列出。",
                       "//   已含中文的原文按「無需翻譯」計，不列出。"));
            }

            if (ModConfigSidebarWidgets.ExportSelectedOnly)
            {
                Line(L("//   Selected only: the " + mods.Count + " mods ticked in the Export column.",
                       "//   仅导出已选：左栏「导出」列勾选的 " + mods.Count + " 个模组。",
                       "//   僅導出已選：左欄「導出」列勾選的 " + mods.Count + " 個模組。"));
            }
        }

        sb.Append('\n');

        // 本模组界面词条：键是稳定 ID，等号右边预填英文，翻完这段就是本模组的语言文件
        Line(L("// ==== ModConfig Enhance UI (MCE_ keys: translate the right side only, never the key) ====",
               "// ==== ModConfig Enhance 界面（MCE_ 键：只翻等号右边，键不要动）====",
               "// ==== ModConfig Enhance 介面（MCE_ 鍵：只翻等號右邊，鍵不要動）===="));
'''
s = s[:i] + HEADER + s[j:]
io.open(p, "w", encoding="utf-8", newline="\n").write(s)

# ---------- README.md：语言文件 + 翻译导出 两段重排 ----------
p = root + r"\README.md"
s = io.open(p, encoding="utf-8").read()
a = s.index("## 语言文件"); b = s.index("## 配置（")
NEW = r'''## 语言文件（整合包 / 多语言的核心）

### 目录与文件

`BepInEx\config\ModConfigEnhance\language\`，首次启动自动创建。

| 文件 | 说明 |
|---|---|
| `en-us.txt` `zh-cn.txt` `zh-tw.txt` | 内建三种。只建不覆，可随意改；删掉即恢复默认 |
| `_readme.txt` | 说明文件，每次启动 / 切语言后按当前界面语言重写（简中 / 繁中 / 英文之一） |
| `xx-yy.txt` | 你自己的语言：「导出翻译」的产物填上译文后改名（小写语言码，如 `de-de.txt`），首行 `// display: 显示名` 可选 |

`_` 开头的文件不进下拉框。

### 文件格式

与 XUnity AutoTranslator 词典相同：`键=译文`，`//` 注释，`\n` 换行、`\=` 等号、`{0}` 占位符原样保留，等号左边不能改。一份文件分两段：

| 段 | 内容 | 谁来用 |
|---|---|---|
| 第一块 `MCE_*=…` | 本模组自己的界面（键是稳定 ID，只翻右边） | 本模组 |
| 之后各模组分块 `原文=译文` | 选项名 / 说明 / 下拉选项 | 交给 XUnity，翻设置列表和游戏里所有同文本 |

### 四步做一份新语言

1. **导出** — 模组设置 → 左栏「导出翻译」。先勾「仅导出已选」去掉不需要的模组（本模组永远包含）。
2. **翻译** — 打开 `config\ModConfigEnhance\export\` 里的文件，填等号右边（可整份交给 AI）。
3. **改名** — 存成 UTF-8，改名为小写语言码放进 `language\`。
4. **启用** — 回游戏，左栏第三行「刷新」→ 下拉选中。

### 选中后发生什么（不用重启）

- 本模组界面立刻换（只刷 `MCE_` 索引的组件，不动其他文字）。
- **XUnity 切到该语言**：目标语言与词典目录变成 `Translation\<码>\Text\`，其余行写成那里的 `ModConfigEnhance_<码>.txt`，重载后词典里没有的文本还原成原文——选 `en-us` 就是全英文。`AutoTranslatorConfig.ini` 的 `Language=` 同步改掉，下次启动一致。
- 语言码映射：`Translation\` 下已有同名目录用同名；有同语言目录（`zh-cn` ↔ `zh`）沿用它；否则按 XUnity 惯例取主语言子标签（`en-us` → `en`、`de-de` → `de`，繁中 `zh-TW`）。
- 文件有错（缺等号、没有有效行、文件不存在）在说明面板用**橙色**带行号提示。

「跟随游戏语言」（下拉第一项，默认）按游戏语言用内建的简 / 繁 / 英，其他语言回落英文；XUnity 同样跟着切。

开关 `Switch XUnity language with the language file`（默认开）；关掉则只翻本模组界面、完全不碰 XUnity。卸载本模组后可手动删各语言目录里的 `ModConfigEnhance_*.txt`。

### 投稿

翻好的语言文件欢迎寄给作者 AiAe：邮箱 `2323086800@qq.com` / QQ 群 `1104320838`，下个版本随包附带。

## 翻译工具

左栏三排按钮：

| 按钮 | 作用 |
|---|---|
| 导出翻译 | 写 `config\ModConfigEnhance\export\待翻译_<时间>.txt`：先本模组 `MCE_*` 块，再各模组分块。自动跳过 KeyCode / 按键名清单与超过 40 项的选项清单 |
| 查漏翻 | 需 XUnity。只列它当前词典没命中的条目——直接问内存词典，正则 / 替换表都算数。可选 |
| 仅导出已选 | 勾上后左栏多一列「导出」复选框，只导出勾选的模组（本模组永远包含） |
| 显示原文 / 显示译文 | = XUnity 的 Alt+T，全局在原文 / 译文间切换 |
| 重载翻译 | = XUnity 的 Alt+R。手改词典文件后重读，平时不用 |
| 打开目录 | 打开 `config\ModConfigEnhance\` |
| 语言 ▾ / 刷新 | 选语言文件；新放进目录的文件点刷新就能看到 |

'''
s = s[:a] + NEW + s[b:]
io.open(p, "w", encoding="utf-8", newline="\n").write(s)
print("ok")
