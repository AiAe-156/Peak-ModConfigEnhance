# 更新日志 / Changelog

## 1.1.0 — 2026-11-16

### 新增 / Added

- **模组概览页**：游戏「设置」菜单（主菜单和局内暂停菜单）里「模组设置」正下方新增「模组概览」按钮，页面分「模组列表」「快捷键设置」「模组设置」三个页签（「模组设置」直接打开模组设置页），上方共用一行工具（刷新、隐藏已停用、铅笔、分组管理、语言、主题、日志）。**本模组不联网，也不会下载、安装、删除、启用或停用任何模组。**
  - 手柄（两个页签相同）：↑↓ 逐个选模组，按住十字键或左摇杆可连续移动；LB / RB 在工具行的按钮间跳；RT + LB / RB 在「模组列表 / 快捷键设置 / 模组设置」三个页签按钮间移动，按 A 切换（从「模组设置」进入设置页时，焦点停在表头「跳转到快捷键设置页」）；RT + ←→ 翻页；RT + L3 全部折叠 / 展开；快速连按两次 B 离开本页。模组列表另有 RT + ↑↓ 上 / 下一个分组、RT + Y 分组菜单、L3 折叠所在分组。
- **模组列表**：列出所有已安装的模组（含已停用的）：序号、启用状态（只读）、名称、版本、作者、简介，以及设置 / 主页 / 文件夹按钮；悬停显示完整依赖链；角标标出资源包、嵌套模组、散放 DLL 和重复副本；基础组件单独成区；可搜索、隐藏已停用。
  - 双击模组名称或简介即可修改（手柄在行菜单里选「编辑名称 / 编辑简介」），保存到当前语言文件，设置页和快捷键设置页同步显示；留空恢复原文。
- **分组**：自己的显示分组，最多 3 层、可配颜色，模组列表、快捷键设置页和设置页侧栏共用同一套分组与折叠状态；分组只用于显示，不移动任何文件夹。
  - 分组展开后，子分组排在本组模组的前面。
  - 「分组管理」（工具行，或设置页刷新钮旁的方形按钮）编辑名称、上级分组、描述、颜色、成员，可删除分组（内容上移一层）；新建时预填「新分组1」「新子分组1」（数字自动递增，可直接改）。
  - 打开铅笔（编辑模式）后可拖动排序、拖进分组、多选一起拖；分组标题拖到另一个分组标题的上缘 / 下缘排到它前面 / 后面（同一层，蓝线提示），拖到中间成为它的子分组（整条标题变蓝）；右键分组标题（手柄按 Y）有新建子分组 / 编辑 / 移到顶层 / 删除；模组行菜单的「移动到分组…」随时可用。
  - 第一次打开模组列表、还没有分组时，可按 `plugins` 里的文件夹结构一键生成分组（只写 `overview.json`）。
- **日志窗口**：工具行「日志」实时查看 BepInEx 日志、游戏日志和自定义日志来源；可按等级（可多选，等级名与日志同色）/ 模组 / 分组筛选、搜索、折叠重复报错、展开调用栈、收藏来源、复制；可查看最近 3 次启动的历史日志（「本次／上次／上上次／3 次前」，存在 `config/ModConfigEnhance/cache/log-history/`，单个文件上限 10 MB）；导出 ZIP 可选附带版本与模组清单（不含配置值，去掉个人路径）；「控制台」开关 BepInEx 控制台窗口。
  - 其他模组可以把自己的日志接进日志窗口（接入方法见 README「给模组作者」）。
- **与 In-Game Mod Manager（游戏内模组管理器）同时安装时，本模组整个不加载**，只在日志里留一条警告（它已包含本模组的全部功能）。
- **快捷键设置页**：所有已加载模组的快捷键按模组分块列在一页，可搜索、筛选「全部 / 重复 / 未绑定」、按模组收起；有分组时显示同样的分组标题。
  - 键盘 / 手柄两栏，录完**立即写进该模组的配置**；「模组默认」栏点击即恢复默认；右键（手柄按 Y）可恢复默认、清空、忽略重复提示、编辑短说明。
  - 重复键会标出来，也包括与游戏自己的操作相同的键；不在意的提示可以忽略，「更多」里可全部恢复。
  - 双击（手柄按 A）可为当前语言写一句短说明，保存在当前语言文件里。
  - 齿轮按钮跳到该模组的设置页。
  - 手柄：操作同模组列表（见上）；不按 RT 时 L3 收起 / 展开焦点所在的模组；录制手柄键时长按 B 1 秒结束。
- **手柄组合键（自由组合）**：只认键盘的快捷键也能在手柄栏录一个手柄键或组合键——先按住任意几个键再按最后一个（最多 4 个）。只在游戏中生效；**先按住的键游戏仍会读到，只有最后一个键被屏蔽**，介意的话可改用 Steam 输入的动作集 / 组合键。
- 模组设置页表头新增「跳转到快捷键设置页」按钮，直接进入快捷键设置页。
- 新的个人文件：`config\ModConfigEnhance\overview.json`（分组、折叠、隐藏已停用）、`hotkeys.json`（忽略的重复提示、收起的模组）、`gamepad-combos.json`（手柄组合键）、`smart-grouping-offered`（智能分组选过「不用了」），以及可随时删除的 `cache\`（日志收藏、最近 3 次启动的日志副本）。

### 更改 / Changed

- **配色**：模组设置页的左栏、页签和说明面板跟随游戏设置菜单的配色（PEAK 3.1 起为深蓝底、米色字），不再停留在旧版的棕色，和右侧的选项格子一致。新增紫色主题：搜索框右上方的月亮按钮在蓝色与紫色之间切换，即时生效；选项「配色主题」（1. Layout）还可以选「夜里自动紫色」，装了 TimeTheme 时白天蓝、夜里紫。
- **更紧凑的版面**：左栏加宽到 360，搜索框与左栏同宽、高度收窄；工具行第一行三个按钮平分宽度，文字放不下时自动缩小；侧栏行、右侧选项格、说明面板都收紧，一屏能看到更多模组和选项；文本框、下拉框、按键框统一高度，滑条改为细轨道加圆形滑块；置顶模组左缘改为 2 像素的青柠色细线。
- 每个选项的控件左侧显示它的默认值，按键以按键小方块显示；选项名太长放不下时自动隐藏。
- 快捷键重复：原来那行橙色长句改为按键框右上角的小角标。鼠标悬停或手柄选中角标，说明面板列出与哪些快捷键重复；点击或按 A 跳到对方那一格。
- 搜索除选项名外，还匹配模组名、分区名、配置键名和说明文字（均为原文）。
- 选项不超过 12 个的普通下拉框，展开时改用紧凑的小列表，当前值带 ✓；多选、带搜索或带图片的下拉框不变。
- 模组设置页的手柄操作：
  - 文本框选中后按 A 才开始输入，按 B 只退出输入；Steam 大屏幕模式或 Steam Deck 上按 A 弹出 Steam 屏幕键盘。
  - 右摇杆滚动右侧选项列表，按住越久越快，滚动时焦点不动。
  - 进入右栏时焦点落在选项的值控件上，不再落在「默认」按钮上（原先按一下 A 就把值重置了）。
  - 离开模组设置页需要快速连按两次 B，第一次按下时屏幕底部有提示；键鼠不受影响。
  - 上限极大的滑条（例如上限是浮点数最大值）按有效区间计算步长，步长取整。
  - 焦点框外加一圈深色描边，浅色控件上也看得清；鼠标点击不再画焦点框。滑条选中时滑块外有光环，调值时变橙色。
  - 在左栏上下移动时，说明面板预览焦点所在模组的介绍，不切换右侧显示的模组。
- 翻译改由本模组自己完成，不再依赖 XUnity AutoTranslator：模组名称、分区、选项名、说明和下拉选项都按 `language\` 里的语言文件直接显示（整句译文优先，其次正则规则，都没有则显示原文）。依赖列表里移除了 XUnity。
  - 「显示原文 / 显示译文」「重载翻译」「补漏翻」不再需要 XUnity，始终可用。「显示原文」只切换本模组负责显示的模组文字，本模组自己的界面文字保持所选语言。
  - 语言文件支持正则规则 `r:"正则"=译文`（写法同 XUnity 词典，可用 `$1` 引用捕获组），适合只差数字或名称的句子；写坏的规则会跳过并在应用结果里提示行号。
  - 内置三种语言（英文、简体、繁体）先铺内置文字，语言文件里的译文再覆盖：旧版本生成的语言文件缺少新词条时不再回落成英文。
- 依赖的 PEAKLib.UI 升到 1.7.2（雷霆商店 / 模组管理器会一并更新）。

### 修复 / Fixed

- 适配 PEAK 3.1.0：游戏改了设置页的数字格式后，模组的小数参数显示成整数（0.35 显示为 0），手动输入时小数点还会被吞掉、存成错误的值。现在模组参数最多显示三位小数，输入时保留你敲的内容；游戏自己的设置保持新版的显示方式。
- 内建的 en-us / zh-cn / zh-tw 语言文件被清空或只剩注释时，改用内置译文，不再报「没有一行有效的键=译文」导致语言应用失败。
- 从游戏内暂停菜单先进入设置页时，纸面抖动的材质可能取不到，之后一直不抖；现在每次进入页面会重试。
- 原文以反斜杠结尾时，语言文件一行的转义不再出错（反斜杠只在有歧义时写成两个）。

### 移除 / Removed

- 选项「经典棕色配色」移除，由「配色主题」取代；读不到游戏配色的旧版游戏使用内置的同一组蓝色。
- 选项「Switch XUnity language with the language file」「Disable XUnity machine translation」移除，配置文件里的旧条目会自动清掉。本模组不再切换 XUnity 的语言、不再往它的目录写词典、不再清空它的机翻端点和缓存。
- 旧版本写进 XUnity 词典目录的 `ModConfigEnhance_<码>.txt` 会一次性移到 `language\_xunity-mirror-backup_<日期>\`。以前被清空的机翻端点**不会自动恢复**，需要的话请自行填回。

---

- **New Mod overview page**: a **Mod overview** button now sits right below **Mod Settings** in the game's Settings menu (main menu and in-game pause menu). It has **Mod list**, **Keybindings** and **Mod Settings** tabs (the last one opens the mod settings page) under one shared toolbar (Refresh, Hide disabled, pencil, Groups, Language, Theme, Logs). **This mod never goes online, and never downloads, installs, deletes, enables or disables any mod.**
  - Controller (same on both tabs): ↑↓ move from mod to mod, hold the D-pad or left stick to keep moving; LB / RB jump between the toolbar buttons; RT + LB / RB move between the Mod list / Keybindings / Mod Settings tab buttons, A opens the tab (entering the settings page from Mod Settings puts the focus on **Go to keybindings** in its header); RT + ←→ page; RT + L3 fold / unfold all; press B twice quickly to leave. On the mod list also RT + ↑↓ previous / next group, RT + Y group menu, L3 folds the group you are in.
- **Mod list**: every installed mod, disabled ones included, with number, enabled state (read-only), name, version, author, description and settings / homepage / folder buttons; the full dependency chain on hover; badges for resource packs, nested mods, loose DLLs and duplicate copies; core components in their own section; search and Hide disabled.
  - Double-click a mod's name or description to change it (on a controller, **Edit name / Edit description** in the row menu). It is saved into the current language file and shows on the settings page and the keybindings page too; leave it empty to restore the original.
- **Groups**: your own display groups, up to 3 levels with colours, shared (with their folded state) by the mod list, the keybindings page and the settings page sidebar. Groups are display-only and never move folders.
  - When a group is unfolded, its subgroups are listed before its own mods.
  - **Groups** (toolbar, or the square button next to Refresh on the settings page) edits name, parent, description, colour and members, and deletes groups (contents move up one level); new groups start as "New group 1" / "New subgroup 1" (the number counts up; just type over it).
  - With the pencil (edit mode) on, drag to reorder, drop onto a group, or drag several at once; drop a group header on the top / bottom edge of another group header to place it before / after that group (same level, shown by a blue line), or on its middle to make it a subgroup (the whole header turns blue); right-click a group header (Y on a controller) for New subgroup / Edit / Move to top level / Delete; **Move to group…** in a mod's row menu always works.
  - The first time you open the mod list with no groups, you can build groups from the folder layout inside `plugins` in one click (only `overview.json` is written).
- **Log window**: **Logs** on the toolbar shows the BepInEx log, the game log and custom log sources live; filter by level (several at once, each in its log colour) / mod / group, search, fold repeated errors, expand stack traces, favourite sources, copy; view the logs of the last 3 launches (This run / Last run / 2 runs ago / 3 runs ago, kept in `config/ModConfigEnhance/cache/log-history/`, up to 10 MB per file); the ZIP export can include versions and the mod list (no config values, personal paths removed); **Console** toggles the BepInEx console window.
  - Other mods can plug their own logs into the log window (see "For mod authors" in the README).
- **When In-Game Mod Manager is installed too, this mod does not load at all** and leaves one warning in the log (that mod already contains everything this one does).
- **Keybindings tab**: every loaded mod's keybinds on one page, one block per mod, with search, All / Duplicate / Unbound filters and foldable mods; with groups, the same group headers show here.
  - Keyboard and Controller columns; a recorded key is **written to the mod's config at once**. Click **Mod default** to restore the default; right-click (Y on a controller) to reset, clear, ignore a duplicate warning or edit the short description.
  - Duplicate keys are flagged, including keys that are also one of the game's own controls; warnings can be ignored and restored from **More**.
  - Double-click (A on a controller) to write a one-line short description in the current language; it is saved in the current language file.
  - The gear button opens that mod's settings page.
  - Controller: the same as the mod list (above); without RT, L3 folds / unfolds the focused mod; while recording a controller key, hold B for 1 second to finish.
- **Free-form controller combos**: keyboard-only keybinds can get a controller button or combo in the Controller column — hold any buttons, then press the last one (up to 4). Works only in game. **The buttons held first still reach the game; only the last one is blocked** — Steam Input's action sets / chords are an alternative.
- A **Go to keybindings** button in the mod settings page header opens the keybindings page.
- New personal files: `config\ModConfigEnhance\overview.json` (groups, folding, Hide disabled), `hotkeys.json` (ignored duplicate warnings, folded mods), `gamepad-combos.json` (controller combos), `smart-grouping-offered` (you said Not now to smart grouping), and a `cache\` folder you can delete any time (log favourites, copies of the last 3 launches' logs).
- Changed: **colours**. The mod settings page's left column, tabs and description panel now follow the game's settings colours (dark blue with cream text since PEAK 3.1) instead of the old brown, matching the option cells on the right. A purple theme is also available: the moon button above the search box switches between blue and purple at once. The **Colour theme** option (1. Layout) also offers **PurpleAtNight**, which is blue by day and purple at night when TimeTheme is installed.
- Changed: **tighter layout**. The left column is wider (360); the search box matches its width and is shorter; the three buttons in the first toolbar row share the width and shrink their text when it does not fit; sidebar rows, option cells and the description panel are tighter, so more mods and options fit on screen; text boxes, dropdowns and key boxes share one height and sliders are a thin track with a round knob; the pinned-mod marker is a 2-pixel lime line.
- Changed: each option shows its default value to the left of the control, with keys drawn as key chips; it hides when the option name leaves no room.
- Changed: duplicate keybinds — the long orange line is replaced by a small badge on the corner of the key box. Hover it or select it with a gamepad to list the conflicting keybinds in the description panel; click it or press A to jump to the other option.
- Changed: search also matches mod names, section names, config keys and descriptions (original text), not just option names.
- Changed: plain dropdowns with up to 12 choices open as a compact list with a ✓ on the current value; multi-select, searchable and image dropdowns are unchanged.
- Changed: gamepad on the mod settings page:
  - A text box only starts typing when you press A, and B only stops typing; in Steam Big Picture or on Steam Deck, A opens the Steam on-screen keyboard.
  - The right stick scrolls the options on the right, faster the longer it is held, without moving the focus.
  - Entering the right panel focuses the option's value control instead of its Default button (one press of A used to reset the value).
  - Leaving the mod settings page takes two quick presses of B; the first press shows a hint at the bottom of the screen. Keyboard and mouse are unaffected.
  - Sliders with a huge maximum (such as the largest float value) use an effective range for their step, and steps are round numbers.
  - The focus frame has a dark outline so it shows on light controls; mouse clicks no longer draw it. A selected slider shows a ring around its knob, orange while adjusting.
  - Moving through the left column previews the focused mod's summary in the description panel without switching the options shown on the right.
- Changed: translation is now done by this mod itself, without XUnity AutoTranslator: mod names, sections, option names, descriptions and dropdown choices are shown straight from the language files in `language\` (exact line first, then regex rules, otherwise the original). XUnity is no longer in the dependency list.
  - **Show original / Show translated**, **Reload texts** and **Untranslated** no longer need XUnity and are always available. Show original only switches the mod texts this mod displays; this mod's own UI keeps the selected language.
  - Language files accept regex rules, `r:"regex"=translation` (same syntax as XUnity dictionaries, `$1` for capture groups), for sentences that differ only in numbers or names; a broken rule is skipped and reported with its line number.
  - The three built-in languages start from the built-in texts and language files override them, so a language file written by an older version no longer falls back to English for new entries.
- Changed: the PEAKLib.UI dependency is now 1.7.2 (Thunderstore / mod managers update it along with this mod).
- Fixed: PEAK 3.1.0 support — after the game changed how settings format numbers, mod decimal parameters showed as whole numbers (0.35 showed as 0), and typing a value could drop the decimal point and store the wrong number. Mod parameters now show up to three decimals and keep what you type; the game's own settings keep the new look.
- Fixed: if the built-in en-us / zh-cn / zh-tw language file is empty or only has comments, the built-in texts are used instead of failing with "no valid key=text line".
- Fixed: when the settings page was first opened from the in-game pause menu, the wobbling paper material could be missed and never applied; it is now retried each time the page opens.
- Fixed: line escaping when an original text ends with a backslash.
- Removed: the **Classic brown colours** option, replaced by **Colour theme**. Older game versions without the new colours use the same built-in blue.
- Removed: the options **Switch XUnity language with the language file** and **Disable XUnity machine translation** (old cfg entries are cleaned up). This mod no longer switches XUnity's language, writes dictionaries into its folder or empties its machine-translation endpoint and caches.
- Removed: dictionaries that older versions wrote into XUnity's folder (`ModConfigEnhance_<code>.txt`) are moved once into `language\_xunity-mirror-backup_<date>\`. An endpoint emptied by an older version is **not** restored automatically.

## 1.0.2 — 2026-09-18

### 修复 / Fixed

- 依赖列表里的 XUnity AutoTranslator 换成仍在维护的 `RTLC-XUnity_AutoTranslator-5.6.11`；1.0.1 写的 `Hayrizan-XUnity_AutoTranslator-5.6.1` 在 Thunderstore 上已标记弃用。两个包里的插件本体都是 XUnity 5.6.1，本模组的反射对接不受影响，已装 XUnity 的用户无需操作。

---

- The XUnity AutoTranslator dependency now points at the maintained `RTLC-XUnity_AutoTranslator-5.6.11`; the `Hayrizan-XUnity_AutoTranslator-5.6.1` listed in 1.0.1 is marked deprecated on Thunderstore. Both packages ship the same XUnity 5.6.1 plugin, so the reflection bridge is unaffected and existing installs need no action.

## 1.0.1 — 2026-09-18

### 更改 / Changed

- XUnity AutoTranslator 列入依赖（`Hayrizan-XUnity_AutoTranslator-5.6.1`）：只是让模组管理器一起装上，不装也不会出问题——翻译工具（补漏翻 / 显示原文 / 词典镜像）置灰，其余功能照常。
- 「Disable XUnity machine translation」默认改为**开启**：装有 XUnity 时每次启动清空它的机翻端点（`AutoTranslatorConfig.ini` 的 `Endpoint` / `FallbackEndpoint`），并清空各语言目录的机翻缓存 `_AutoGeneratedTranslations*.txt`（每个文件旁留一份 `.bak`）。依赖在线机翻的话请在首次启动前把它关掉。README 与生成的 readme.md 新增「本模组会改动 XUnity AutoTranslator 的一项设置」一节，cfg 选项说明同步。

---

- XUnity AutoTranslator added to the dependency list (`Hayrizan-XUnity_AutoTranslator-5.6.1`) so the mod manager installs it alongside; nothing breaks without it — the translation tools (Untranslated / Show original / dictionary mirroring) are greyed out, everything else works.
- "Disable XUnity machine translation" is now **on by default**: with XUnity installed, every startup empties its machine-translation endpoints (`Endpoint` / `FallbackEndpoint` in `AutoTranslatorConfig.ini`) and the `_AutoGeneratedTranslations*.txt` caches in every language folder (a one-time `.bak` copy is kept). Turn it off before the first launch if you rely on live machine translation. README and the generated readme.md gained a "This mod changes an XUnity AutoTranslator setting" section; the cfg description was updated to match.
