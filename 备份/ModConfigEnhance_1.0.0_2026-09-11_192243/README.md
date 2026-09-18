# ModConfig Enhance

> PEAK · 给 ModConfig（PEAKLib.ModConfig）的「模组设置」页做的增强
> 版本 1.0.0 ｜ 作者 AiAe ｜ 前身是 LocalFix 第 9 分区（LocalFix 2.17.0 起已移除）

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

## 语言文件

目录：`BepInEx\config\ModConfigEnhance\language\`（首次启动自动创建）

| 文件 | 说明 |
|---|---|
| `en-us.txt` `zh-cn.txt` `zh-tw.txt` | 内建三种；只建不覆，可随意改；删掉即恢复默认 |
| `_template.txt` | 模板，每次启动重写；`_` 开头的文件不进下拉框 |
| 你自己的 `xx-yy.txt` | 复制模板改名（小写语言码，如 `de-de.txt`、`ja-jp.txt`），首行 `// display: 显示名` 可选 |

格式与 XUnity 词典相同：`键=译文`，`//` 注释，`\n` 换行、`\=` 等号，`{0}` 占位符原样保留。文件分两段：

- `MCE_*` 键行：本模组管的界面词条（键是稳定 ID，**不要翻键**）。
- 「Settings of this mod」之后：本模组自己的 cfg 选项名 / 说明的 `英文原文=译文`，由**镜像**喂给 XUnity 去翻设置列表。

**下拉框**：第一项「跟随游戏语言」（默认）——按游戏语言用内建的简 / 繁 / 英，其他语言回落英文；选了具体文件，就用该文件填满全部语言槽，游戏切语言不再影响。选中即时生效（走游戏的 `RefreshAllText`，不用重开页面）。文件有错（缺等号、没有有效行、文件不存在）会在说明面板用**橙色**带行号提示。新建了文件点「刷新」就能看到。

**镜像**（开关 `Mirror language file to XUnity`，默认开）：选中的文件里非 `MCE_` 的行写到 XUnity 的 `Translation\<语言>\Text\ModConfigEnhance_<文件名>.txt`，换语言时先删旧镜像；只在文件语言码与 XUnity 的 `Language=` 一致时写（`zh-cn` ↔ `zh` / `zh-CN` 算一致），不一致会在说明面板提示。卸载本模组后可手动删这些 `ModConfigEnhance_*.txt`。

**投稿**：翻好的语言文件欢迎寄给作者 AiAe —— 邮箱 `2323086800@qq.com` / QQ 群 `1104320838`，下个版本随包附带。

## 翻译导出

左栏第一排 `导出翻译`：把设置页会显示的全部文字（模组名、分区、选项名、说明、下拉选项）按模组分块写成 XUnity 词典格式到 `config\ModConfigEnhance\export\待翻译_<时间>.txt`。`查漏翻`（需 XUnity）只列它当前词典没命中的条目——判定直接问 XUnity 的内存词典，正则 / 替换表都算数。勾 `仅导出已选` 后左栏多一列「导出」复选框。翻完放进 `Translation\<语言>\Text\`，点 `重载翻译`（= Alt+R）即生效。自动跳过 KeyCode / 按键名清单与超过 40 项的选项清单。

## 配置（`BepInEx\config\com.aiae.modconfigenhance.cfg`）

| 分区 | 项 | 说明 |
|---|---|---|
| 1. Layout | Tree sidebar / Description panel | 改开关需重启 |
| 2. Localization | UI localization / Translation tools / Mirror language file to XUnity | 前两项改开关需重启 |
| 3. State | Language file / Pinned mods / Excluded from export / Export selected only / Migrated from LocalFix | 界面维护，Hidden，不显示在 ModConfig 里 |

首次启动会从 `com.aiae.localfix.cfg` 读走旧的「置顶 / 不导出 / 仅导出已选」（只读一次，不改对方文件）。

## 非中文用户

XUnity 的目标语言只在启动时读 `AutoTranslatorConfig.ini` 的 `Language=`；改成你的语言码（如 `de`）并重启，`Translation\de\Text\` 会自动建出来，本模组的镜像与你导出的词典都往那里放。`FromLanguage` 不影响词典命中，只管「没翻到的文字要不要送去机翻」，通常不用动。

---

# ModConfig Enhance (English)

Enhancements for the **ModConfig** (PEAKLib.ModConfig) settings page in PEAK: a tree sidebar (mods on the left, sections under the selected mod, options on the right), a description panel showing the hovered option's cfg description with default / range / choices, localisation of ModConfig's own UI texts via **language files** with an in-page language dropdown, and translation tools (export all visible texts in XUnity dictionary format, list only untranslated ones, toggle original / translated, reload, open folder).

**Requires** BepInEx 5, PEAKLib.Core, PEAKLib.UI, ModConfig 1.8.1. **Optional** XUnity AutoTranslator (translation tools and mirroring; buttons are greyed out without it) and TimeTheme (day/night colours). Not compatible with LocalFix older than 2.17.0 (it shipped the same patches); the mod refuses to load in that case — update both.

**Language files** live in `BepInEx\config\ModConfigEnhance\language\`. `en-us.txt`, `zh-cn.txt`, `zh-tw.txt` are created once and never overwritten; `_template.txt` is rewritten every start and hidden from the dropdown (any `_`-prefixed file is). To add a language: copy the template, rename it to a lowercase code such as `de-de.txt`, fill the right side of each `=`, save as UTF-8, click **Refresh** in the third toolbar row and pick it — it applies instantly. Errors are shown in orange in the description panel with line numbers. The first dropdown entry, **Follow game language**, uses the built-in en / zh-cn / zh-tw according to the game language (other languages fall back to English).

Lines after "Settings of this mod" (English original = translation of this mod's own option names / descriptions) are **mirrored** into XUnity's `Translation\<lang>\Text\ModConfigEnhance_<file>.txt` when the file's language code matches XUnity's `Language=` (set it in `AutoTranslatorConfig.ini` and restart; the folder is created automatically). Old mirrors are deleted on every switch; delete `ModConfigEnhance_*.txt` yourself if you uninstall. Disable with `Mirror language file to XUnity`.

Finished translations are very welcome — send them to AiAe at `2323086800@qq.com` or QQ group `1104320838` and they will ship with the next release.
