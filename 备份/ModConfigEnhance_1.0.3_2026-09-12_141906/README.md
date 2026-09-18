# ModConfig Enhance

> PEAK · 给 ModConfig（PEAKLib.ModConfig）的「模组设置」页做的增强
> 版本 1.0.3 ｜ 作者 AiAe ｜ 前身是 LocalFix 第 9 分区（LocalFix 2.17.0 起已移除）

**English below.**

## 做什么

| 功能 | 说明 |
|---|---|
| 树形侧栏 | 模组设置页改成「左树右选项」：左栏竖排模组列表，选中的模组下面展开分区，右侧只剩选项。带滚动条、滚轮死区修补、置顶列、导出列 |
| 选项说明面板 | 右侧顶部一块面板：悬停哪个选项就显示它 cfg 里的说明 + 默认 / 范围 / 可选；可滚动 |
| 界面本地化 | ModConfig 自己的界面文字（搜索、MODS / SECTIONS、默认、清空、筛选项、按键提示…）和本模组的按钮、提示，来自**语言文件**，可实时切换 |
| 翻译工具 | 左栏三排工具行：`导出翻译 / 查漏翻 / 仅导出已选`、`显示原文 / 重载翻译 / 打开目录`、`语言 ▾ / 刷新` |
| 纸面样式与昼夜色 | 借原版 UI 的手绘抖动材质；装了 TimeTheme 时跟随昼夜配色 |

## 依赖

- **必须**：BepInEx 5、PEAKLib.Core、PEAKLib.UI、**ModConfig 1.8.1**（在场的若是 ModSettingsLocalization 分支版会自动跳过）
- **可选**：XUnity AutoTranslator（查漏翻 / 显示原文 / 重载翻译 / 镜像语言文件；没装则这些按钮置灰）、TimeTheme（昼夜配色）
- **互斥**：LocalFix 2.17.0 之前的版本自带同一套补丁，检测到会拒绝挂载并在日志报错——两边一起升级即可

## 语言文件（整合包 / 多语言的核心）

### 目录与文件

`BepInEx\config\ModConfigEnhance\language\`，首次启动自动创建。

| 文件 | 说明 |
|---|---|
| `en-us.txt` `zh-cn.txt` `zh-tw.txt` | 内建三种。只建不覆，可随意改；删掉即恢复默认 |
| `xx-yy.txt` | 你自己的语言：「导出翻译」的产物填上译文后改名（小写语言码，如 `de-de.txt`），首行 `// display: 显示名` 可选 |

`_` 开头的文件不进下拉框。说明文件是上一级的 `config\ModConfigEnhance\readme.md`，每次启动 / 切语言后按当前界面语言重写（简中 / 繁中 / 英文之一）。

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
| 重载翻译 | 从磁盘重新读语言文件（重新应用 `MCE_` 词条 + 重写镜像）再让 XUnity 重读词典。手改 `language\*.txt` 或 `Translation\` 下的词典后点它即可，不用重新选语言 |
| 打开目录 | 打开 `config\ModConfigEnhance\`（语言文件、导出文件、`readme.md` 都在里面） |
| 语言 ▾ / 刷新 | 选语言文件；新放进目录的文件点刷新就能看到 |

## 配置（`BepInEx\config\com.aiae.modconfigenhance.cfg`）

| 分区 | 项 | 说明 |
|---|---|---|
| 1. Layout | Tree sidebar / Description panel | 改开关需重启 |
| 2. Localization | UI localization / Translation tools / Switch XUnity language with the language file | 前两项改开关需重启 |
| 3. State | Language file / Pinned mods / Excluded from export / Export selected only / Migrated from LocalFix | 界面维护，Hidden，不显示在 ModConfig 里 |

首次启动会从 `com.aiae.localfix.cfg` 读走旧的「置顶 / 不导出 / 仅导出已选」（只读一次，不改对方文件）。

## 非中文用户

不用手改 `AutoTranslatorConfig.ini`：在语言下拉里选你的文件，XUnity 就切到该语言并自动建 `Translation\<码>\Text\`，ini 的 `Language=` 也会同步写好。`FromLanguage` 不影响词典命中，只管「没翻到的文字要不要送去机翻」，通常不用动。

---

# ModConfig Enhance (English)

Enhancements for the **ModConfig** (PEAKLib.ModConfig) settings page in PEAK: a tree sidebar (mods on the left, sections under the selected mod, options on the right), a description panel showing the hovered option's cfg description with default / range / choices, localisation of ModConfig's own UI texts via **language files** with an in-page language dropdown, and translation tools (export all visible texts in XUnity dictionary format, list only untranslated ones, toggle original / translated, reload, open folder).

**Requires** BepInEx 5, PEAKLib.Core, PEAKLib.UI, ModConfig 1.8.1. **Optional** XUnity AutoTranslator (translation tools and mirroring; buttons are greyed out without it) and TimeTheme (day/night colours). Not compatible with LocalFix older than 2.17.0 (it shipped the same patches); the mod refuses to load in that case — update both.

**Language files** live in `BepInEx\config\ModConfigEnhance\language\`. `en-us.txt`, `zh-cn.txt`, `zh-tw.txt` are created once and never overwritten; `config\ModConfigEnhance\readme.md` (rewritten every start in the current UI language) explains everything; any `_`-prefixed file is hidden from the dropdown. A language file is just the output of **Export texts** with translations filled in: the first block (`MCE_` keys) is this mod's UI, the rest is every mod's option texts. To add a language: export, fill the right side of each `=`, save as UTF-8, rename to a lowercase code such as `de-de.txt`, move it into the folder, click **Refresh** in the third toolbar row and pick it — this mod's UI switches instantly and the rest is mirrored into XUnity and reloaded, no restart. Errors are shown in orange in the description panel with line numbers. The first dropdown entry, **Follow game language**, uses the built-in en / zh-cn / zh-tw according to the game language (other languages fall back to English).

Picking a file also **switches XUnity AutoTranslator to that language at runtime**: its dictionary folder becomes `Translation\<code>\Text\`, the non-`MCE_` lines are written there as `ModConfigEnhance_<code>.txt`, dictionaries reload and texts without a translation revert to the original — picking `en-us` gives you the untranslated game. `Language=` in `AutoTranslatorConfig.ini` is updated for the next start. Disable with `Switch XUnity language with the language file`; delete `ModConfigEnhance_*.txt` yourself if you uninstall.

Finished translations are very welcome — send them to AiAe at `2323086800@qq.com` or QQ group `1104320838` and they will ship with the next release.
