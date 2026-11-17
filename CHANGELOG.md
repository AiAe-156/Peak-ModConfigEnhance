# 更新日志 / Changelog

## 1.1.0 — 2026-11-16

### 新增 / Added

- **模组概览页**：游戏「设置」菜单里「模组设置」正下方新增「模组概览」按钮，分「模组列表」「快捷键设置」「模组设置」三个页签。
- **模组列表**：列出所有已安装的模组（含已停用的），悬停看完整依赖链；双击名称或简介可修改，各页面同步显示。
- **分组**：自己的显示分组，最多 3 层、可配颜色，模组列表、快捷键设置页和设置页侧栏共用；只用于显示，不移动任何文件夹。第一次打开可按 `plugins` 的文件夹结构一键生成。
- **日志窗口**：实时查看 BepInEx、游戏和自定义日志，可筛选、搜索，可看最近 3 次启动的历史日志，一键导出 ZIP（不含配置值，去掉个人路径）。其他模组可把自己的日志接进来（见 README「给模组作者」）。
- **快捷键设置页**：所有模组的快捷键集中一页，键盘 / 手柄两栏，录完立即写进该模组的配置；重复键会标出来，也包括与游戏自身操作相同的键。
- **手柄组合键**：只认键盘的快捷键也能录手柄键或组合键（最多 4 个），只在游戏中生效；先按住的键游戏仍会读到，只有最后一个键被屏蔽。
- 概览页两个页签的手柄操作已统一，详见 README。
- 模组设置页表头新增「跳转到快捷键设置页」按钮。
- **与 In-Game Mod Manager 同时安装时，本模组整个不加载**，只在日志里留一条警告。
- 新增的个人文件（分组、快捷键、手柄组合键、缓存）都在 `config\ModConfigEnhance\` 下，详见 README。

### 更改 / Changed

- **配色与版面**：设置页改为跟随游戏设置菜单的深蓝配色（原来是棕色），新增紫色主题（月亮按钮切换，装了 TimeTheme 可夜里自动变紫）；版面更紧凑，一屏看到更多模组和选项；每个选项旁显示默认值；快捷键重复改为角标，可跳到对方。
- **搜索与下拉**：搜索也匹配模组名、分区名、配置键名和说明；选项不超过 12 个的下拉框改为紧凑小列表。
- **手柄（设置页）**：文本框按 A 才开始输入，Steam 大屏幕 / Deck 下弹出屏幕键盘；右摇杆滚动选项；焦点框更清楚，焦点移动有轻微提示音；进入右栏时不再落在「默认」按钮上，避免误重置。
- **翻译改由本模组自己完成，不再依赖 XUnity AutoTranslator**（依赖列表已移除）：「显示原文 / 显示译文」「重载翻译」「补漏翻」始终可用；语言文件新增正则规则 `r:"正则"=译文`；旧版语言文件缺新词条时不再回落成英文。
- 依赖的 PEAKLib.UI 升到 1.7.2。

### 修复 / Fixed

- 适配 PEAK 3.1.0：小数参数不再显示成整数，输入时小数点不再被吞掉。
- 内建语言文件被清空或只剩注释时改用内置译文，不再导致语言应用失败。
- 从暂停菜单先进入设置页时，纸面抖动不再失效；原文以反斜杠结尾时，语言文件的转义不再出错。

### 移除 / Removed

- 选项「经典棕色配色」，由「配色主题」取代。
- 选项「Switch XUnity language with the language file」「Disable XUnity machine translation」；本模组不再改动 XUnity 的任何设置。**以前被清空的机翻端点不会自动恢复**，需要请自行填回。旧版写进 XUnity 词典目录的 `ModConfigEnhance_<码>.txt` 会一次性移到 `language\_xunity-mirror-backup_<日期>\`。

---

- **New Mod overview page**: a **Mod overview** button right below **Mod Settings** in the game's Settings menu, with **Mod list**, **Keybindings** and **Mod Settings** tabs.
- **Mod list**: every installed mod, disabled ones included, with the full dependency chain on hover; double-click a name or description to change it, and every page shows the change.
- **Groups**: your own display groups, up to 3 levels with colours, shared by the mod list, the keybindings page and the settings page sidebar; display-only, never moves a folder. The first time, groups can be built in one click from your `plugins` folder layout.
- **Log window**: live BepInEx, game and custom logs with filters and search, the logs of the last 3 launches, and a one-click ZIP export (no config values, personal paths removed). Other mods can plug their own logs in (see "For mod authors" in the README).
- **Keybindings page**: every mod's keybinds on one page, keyboard and controller columns; a recorded key is written to the mod's config at once; duplicates are flagged, including keys the game itself uses.
- **Controller combos**: keyboard-only keybinds can get a controller button or combo (up to 4), in game only; the buttons held first still reach the game, only the last one is blocked.
- Controller controls on the overview page are the same on both tabs; see the README.
- A **Go to keybindings** button in the mod settings page header.
- **When In-Game Mod Manager is installed too, this mod does not load at all** and leaves one warning in the log.
- New personal files (groups, keybindings, controller combos, cache) all live in `config\ModConfigEnhance\`; see the README.
- Changed: **colours and layout**. The settings page follows the game's dark-blue settings colours (it used to be brown); a purple theme is added (moon button; with TimeTheme it can turn purple at night); the layout is tighter so more mods and options fit on screen; each option shows its default value; duplicate keybinds get a badge that jumps to the other option.
- Changed: **search and dropdowns**. Search also matches mod names, section names, config keys and descriptions; dropdowns with up to 12 choices open as a compact list.
- Changed: **controller on the settings page**. A text box starts typing on A, with the Steam on-screen keyboard on Big Picture / Deck; the right stick scrolls the options; the focus frame is clearer and moving it plays a soft click; entering the right panel no longer lands on **Default**, so a press of A can't reset a value by accident.
- Changed: **translation is now done by this mod itself, and XUnity AutoTranslator is no longer needed** (removed from the dependency list). **Show original / translated**, **Reload texts** and **Untranslated** are always available; language files accept regex rules `r:"regex"=translation`; a language file from an older version no longer falls back to English for new entries.
- Changed: the PEAKLib.UI dependency is now 1.7.2.
- Fixed: PEAK 3.1.0 support — decimal parameters no longer show as whole numbers, and typing a decimal point no longer gets swallowed.
- Fixed: an empty or comment-only built-in language file now falls back to the built-in texts instead of failing.
- Fixed: the paper wobble no longer stays off when the settings page is first opened from the pause menu; line escaping in language files when an original text ends with a backslash.
- Removed: the **Classic brown colours** option, replaced by **Colour theme**.
- Removed: the options **Switch XUnity language with the language file** and **Disable XUnity machine translation**; this mod no longer changes any XUnity setting. An endpoint emptied by an older version is **not** restored automatically; fill it in again if you want it back. Dictionaries older versions wrote into XUnity's folder (`ModConfigEnhance_<code>.txt`) are moved once into `language\_xunity-mirror-backup_<date>\`.

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
