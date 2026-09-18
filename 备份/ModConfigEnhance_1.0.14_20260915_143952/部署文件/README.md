# ModConfig Enhance

> PEAK · 给 ModConfig（PEAKLib.ModConfig）的「模组设置」页做的增强
> 版本 1.0.14 ｜ 作者 AiAe ｜ 前身是 LocalFix 第 9 分区（LocalFix 2.17.0 起已移除）

**English below.**

## 做什么

| 功能 | 说明 |
|---|---|
| 树形侧栏 | 模组设置页改成「左树右选项」：左栏竖排模组列表，选中的模组下面展开分区，右侧只剩选项。带滚动条、滚轮死区修补、置顶列、导出列；搜索 / 筛选后没有匹配项的模组行自动隐藏；本模组行固定最顶、行名带一点颜色（白天 #EFC95F / 夜间 #C9A55B） |
| 选项说明面板 | 右侧顶部一块面板：悬停哪个选项就显示它 cfg 里的说明 + 默认 / 范围 / 可选，首行带所属模组名与版本号；可滚动 |
| 界面本地化 | ModConfig 自己的界面文字（搜索、MODS / SECTIONS、默认、清空、筛选项、按键提示…）和本模组的按钮、提示，来自**语言文件**，可实时切换 |
| 翻译工具 | 左栏三排工具行：`导出翻译 / 补漏翻 / 仅导出已选`、`显示原文 / 重载翻译 / 打开目录`、`语言 ▾ / 刷新`；可配置成只留语言行 |
| 纸面样式与昼夜色 | 借原版 UI 的手绘抖动材质；装了 TimeTheme 时跟随昼夜配色 |
| 手柄支持 | 两级面板：进页锁左栏，模组行 A 选中、分区行 A 进右栏，B 逐级返回（调值→编辑→右栏→左栏→退出）；显式导航链保证复选框可达、米白描边焦点框、选中项自动滚回视野、LB/RB 切模组；滑条按 A 才调值（步进 ≤ 区间 0.5%，按住 1.5s 后 10 步/秒） |

## 依赖

- **必须**：BepInEx 5、PEAKLib.Core、PEAKLib.UI、**ModConfig 1.8.1**（在场的若是 ModSettingsLocalization 分支版会自动跳过）
- **可选**：XUnity AutoTranslator（补漏翻 / 显示原文 / 重载翻译 / 镜像语言文件；没装则这些按钮置灰）、TimeTheme（昼夜配色）
- **互斥**：LocalFix 2.17.0 之前的版本自带同一套补丁，检测到会拒绝挂载并在日志报错——两边一起升级即可

## 语言文件（整合包 / 多语言的核心）

### 目录与文件

`BepInEx\config\ModConfigEnhance\language\`，首次启动自动创建。

| 文件 | 说明 |
|---|---|
| `en-us.txt` `zh-cn.txt` `zh-tw.txt` | 内建三种。只建不覆，可随意改；删掉即恢复默认 |
| `xx-yy.txt` | 你自己的语言：「导出翻译」的产物填上译文后改名（小写语言码，如 `de-de.txt`），首行 `// display: 显示名` 可选 |

`_` 开头的文件不进下拉框。说明文件是上一级的 `config\ModConfigEnhance\readme.md`，每次启动 / 切语言后按当前界面语言重写（简中 / 繁中 / 英文之一）。

### 同语言分包与补翻

同一语言可以放多份文件，选择语言后整组一起加载。保留原翻译文件，把补翻结果另存一份即可：

```text
zh-cn.txt
zh-cn_1_基本.txt
zh-cn_20_补翻.txt
zh-cn_90_个人修正.txt
```

- 第一个 `_` 前是语言码，后面全是备注。语言码内部使用 `-`；`ALL`、模组名称都只是备注。
- 语言码不分大小写；`zh` 是 `zh-cn` 的别名，`en` 是 `en-us` 的别名。`zh-tw` 与简体独立，不混载；其他区域码不因为语言开头相同就合并。
- 无后缀的基础文件先读，其余按文件名排序，不区分大小写。按文字排序而非数值排序：若使用 1–9、10 等连续编号，建议统一补零为 `01`、`02`、`10`；上面的 `1`、`20`、`90` 顺序同样明确。
- 同键同译文跳过；不同有效译文以后读文件为准，日志记录冲突来源。空白译文、与原文相同的占位内容及未修改的 `MCE_` 英文不覆盖已有译文。
- 下拉框每种语言只显示一项及文件数。「跟随游戏语言」也加载对应整组。新增补翻文件后点「重载翻译」；新增一种语言则先点「刷新」再选择。
- 只读 `language\` 当前层的 `.txt`，忽略 `_` 开头的文件；备份请放子目录。先校验整组再应用，格式错误须修正后重载。
- 「补漏翻」不再重复输出已翻好的 `MCE_` 界面词条。保留旧文件 → 补漏翻 → 翻译 → 另存为同语言的新后缀文件 → 重载，不要用补翻文件替换原文件。

### 文件内容

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
- **XUnity 切到该语言**：目标语言与词典目录变成 `Translation\<码>\Text\`，整组非界面词条合并写成那里的 `ModConfigEnhance_<码>.txt`，重载后词典里没有的文本还原成原文。`AutoTranslatorConfig.ini` 的 `Language=` 同步改掉，下次启动一致。
- 语言码映射：`Translation\` 下已有同名目录用同名；有同语言目录（`zh-cn` ↔ `zh`）沿用它；否则按 XUnity 惯例取主语言子标签（`en-us` → `en`、`de-de` → `de`，繁中 `zh-TW`）。
- 文件有错（缺等号、没有有效行、文件不存在）在说明面板用**橙色**带行号提示。

「跟随游戏语言」（下拉第一项，默认）按游戏语言加载简 / 繁 / 英整组文件，包含内建文件与自定义分包，其他语言回落英文；XUnity 同样跟着切。

开关 `Switch XUnity language with the language file`（默认开）；关掉则只翻本模组界面、完全不碰 XUnity。卸载本模组后可手动删各语言目录里的 `ModConfigEnhance_*.txt`。

### 投稿

翻好的语言文件欢迎寄给作者 AiAe：邮箱 `2323086800@qq.com` / QQ 群 `1104320838`，下个版本随包附带。

## 翻译工具

左栏三排按钮：

| 按钮 | 作用 |
|---|---|
| 导出翻译 | 写 `config\ModConfigEnhance\export\待翻译_<时间>.txt`：先本模组 `MCE_*` 块，再各模组分块。自动跳过 KeyCode / 按键名清单与超过 40 项的选项清单 |
| 补漏翻 | 需 XUnity。只列它当前词典没命中的条目——直接问内存词典，正则 / 替换表都算数。可选 |
| 仅导出已选 | 勾上后左栏多一列「导出」复选框，只导出勾选的模组（本模组永远包含） |
| 显示原文 / 显示译文 | = XUnity 的 Alt+T，全局在原文 / 译文间切换 |
| 重载翻译 | 从磁盘重新读语言文件（重新应用 `MCE_` 词条 + 重写镜像）再让 XUnity 重读词典。手改 `language\*.txt` 或 `Translation\` 下的词典后点它即可，不用重新选语言 |
| 打开目录 | 打开 `config\ModConfigEnhance\`（语言文件、导出文件、`readme.md` 都在里面） |
| 语言 ▾ / 刷新 | 按语言分组选择；新增语言点刷新，新增同语言分包点重载翻译 |

## 配置（`BepInEx\config\com.aiae.modconfigenhance.cfg`）

| 分区 | 项 | 说明 |
|---|---|---|
| 1. Layout | Tree sidebar / Description panel | 改开关需重启 |
| 2. Localization | UI localization / Translation tools / Language row only / Switch XUnity language with the language file / Disable XUnity machine translation | 改开关需重启；`Language row only` 勾上后工具行只剩语言下拉 + 刷新，列表上移不留空白；`Disable XUnity machine translation`（默认开）把 `AutoTranslatorConfig.ini` 的 Endpoint / FallbackEndpoint 清空并摘掉当前端点——出厂默认 GoogleTranslateV2 会机翻所有界面文本，关掉本项才会恢复机翻 |
| 3. State | Language file / Pinned mods / Excluded from export / Export selected only / Migrated from LocalFix | 界面维护，Hidden，不显示在 ModConfig 里 |

本模组自己的行固定排在左栏最顶（不参与「置顶」列勾选）。

首次启动会从 `com.aiae.localfix.cfg` 读走旧的「置顶 / 不导出 / 仅导出已选」（只读一次，不改对方文件）。

## 左栏渐入动效

切换左侧模组或分类时，顶部说明栏显示模组名称、版本、当前子分类和 manifest.json 中的模组说明，名称使用稍突出的颜色。鼠标移到右侧具体配置项或手柄选中配置项后，切回该选项的说明。

左栏滚轮步幅调整为旧版的约三分之一，并以约 0.1 秒的短缓动移动；滚动条拖动与手柄定位仍直接接管位置。右侧配置列表和说明栏的滚动不受此项调整影响。

语言选择或重载成功后，顶部说明栏会按实际加载顺序列出全部语言文件名，长清单可在说明栏滚动查看。这里列文件名，不展示词典全文；移动到具体配置项后恢复选项说明。

再次打开配置页时，从当前选中的模组行开始，每一拍同时向上、向下各显示一行；选在底部时从下往上显示。没有有效选中项时从顶部开始。关闭页面会结束本轮动画，避免尚未轮到的行残留透明状态。

## 非中文用户说明

不用手改 `AutoTranslatorConfig.ini`：在语言下拉里选你的文件，XUnity 就切到该语言并自动建 `Translation\<码>\Text\`，ini 的 `Language=` 也会同步写好。`FromLanguage` 不影响词典命中，只管「没翻到的文字要不要送去机翻」，通常不用动。

---

# ModConfig Enhance (English)

Enhancements for the **ModConfig** (PEAKLib.ModConfig) settings page in PEAK: a tree sidebar (mods on the left, sections under the selected mod, options on the right), a description panel showing the hovered option's cfg description with default / range / choices, localisation of ModConfig's own UI texts via **language files** with an in-page language dropdown, and translation tools (export all visible texts in XUnity dictionary format, list only untranslated ones, toggle original / translated, reload, open folder).

**Requires** BepInEx 5, PEAKLib.Core, PEAKLib.UI, ModConfig 1.8.1. **Optional** XUnity AutoTranslator (translation tools and mirroring; buttons are greyed out without it) and TimeTheme (day/night colours). Not compatible with LocalFix older than 2.17.0 (it shipped the same patches); the mod refuses to load in that case — update both.

**Language files** live in `BepInEx\config\ModConfigEnhance\language\`. `en-us.txt`, `zh-cn.txt`, `zh-tw.txt` are created once and never overwritten; `config\ModConfigEnhance\readme.md` (rewritten every start in the current UI language) explains everything; any `_`-prefixed file is hidden from the dropdown. A language file is just the output of **Export texts** with translations filled in: the first block (`MCE_` keys) is this mod's UI, the rest is every mod's option texts. To add a language: export, fill the right side of each `=`, save as UTF-8, rename to a lowercase code such as `de-de.txt`, move it into the folder, click **Refresh** in the third toolbar row and pick it — this mod's UI switches instantly and the rest is mirrored into XUnity and reloaded, no restart. Errors are shown in orange in the description panel with line numbers. The first dropdown entry, **Follow game language**, loads all files in the matching en-us / zh-cn / zh-tw group according to the game language (other languages fall back to English).

Picking a file also **switches XUnity AutoTranslator to that language at runtime**: its dictionary folder becomes `Translation\<code>\Text\`, the non-`MCE_` lines are written there as `ModConfigEnhance_<code>.txt`, dictionaries reload and texts without a translation revert to the original. `Language=` in `AutoTranslatorConfig.ini` is updated for the next start. Disable with `Switch XUnity language with the language file`; delete `ModConfigEnhance_*.txt` yourself if you uninstall.

Finished translations are very welcome — send them to AiAe at `2323086800@qq.com` or QQ group `1104320838` and they will ship with the next release.

### Multiple files per language and incremental translation

Keep your existing translations and add `zh-cn_1_基本.txt`, `zh-cn_20_补翻.txt`, and `zh-cn_90_个人修正.txt` alongside `zh-cn.txt`. The part before the first underscore is the language code; everything after it is a label, including `ALL`. Use hyphens inside language codes. Codes are case-insensitive; `zh` aliases `zh-cn`, and `en` aliases `en-us`. Traditional Chinese (`zh-tw`) stays separate. Other regional codes are not merged just because they share a primary language.

Base files without a suffix load first, then suffixed files in case-insensitive filename order. This is text sorting, not numeric sorting: use `01`, `02`, `10` for sequential numbers. Later valid translations override earlier ones; identical entries are skipped and conflicts are logged with their source files. Blank values, original-text placeholders and unchanged English `MCE_` values cannot replace existing translations.

The dropdown shows one entry per language with its file count. **Follow game language** loads the matching group too. After adding another translated patch file, click **Reload texts**; for a new language, click **Refresh** and select it. Only `.txt` files directly inside `language\` are read; `_`-prefixed files and subfolders are ignored. Keep backups in a subfolder. The entire group is parsed before applying; fix format errors and reload. **Untranslated** omits already translated `MCE_` UI entries. Never replace your complete translation file with an incremental export.

### Sidebar animation

Selecting a mod or category shows its name, version, current subcategory and description from manifest.json in the description panel, with a subtle name accent. Hovering or focusing a specific setting restores its description.

The left sidebar wheel step is about one third of the previous size, with a short transition of approximately 0.1 seconds. Scrollbar dragging and controller positioning take over immediately. Scrolling in the right options list and description panel is unchanged.

After a language selection or reload succeeds, the description panel lists every loaded language filename in load order. Scroll inside the panel for long lists; dictionary contents are not displayed. Point at a setting to return to its description.

Reopening the page starts the fade at the selected mod row, revealing one row above and below on each beat. A bottom selection reveals rows upward. Without a valid selection it starts at the top. Closing the page ends the animation and restores pending rows.


模组的 manifest.json 介绍会随配置文本一起导出；补漏翻只列出尚未翻译的介绍。翻好后仍作为同语言分包加载。

Manifest descriptions are included in translation exports; untranslated exports include descriptions without a translation. Load the translated result as another file in the same language group.
