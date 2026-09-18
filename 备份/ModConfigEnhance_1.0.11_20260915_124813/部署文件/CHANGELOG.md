# 更新日志

## 1.0.11 — 2026-09-15

### 更改 / Changed

- 模组概况显示稍亮的模组名、版本号、当前子分类，以及最近的包目录 manifest.json 中的 description；没有说明时明确提示。
- The mod overview shows a brighter name, version, subcategory and description from the nearest package manifest.json, with an explicit fallback when unavailable.

### 移除 / Removed

- 移除模组概况中的配置项总数。
- Remove the setting count from the mod overview.

## 1.0.10 — 2026-09-15

### 新增 / Added

- 切换模组或分类时，顶部说明栏显示名称、版本、当前分类和配置项总数；查看具体选项后切回选项说明。
- Show mod name, version, category and total setting count when switching mods or categories; hovering or focusing a setting restores its description.
- 语言选择或重载成功后，按实际加载顺序显示完整文件名清单，可在说明栏内滚动查看。
- List every loaded language filename in load order after a successful selection or reload, with scrolling for long lists.

### 更改 / Changed

- 左栏滚轮步幅降至原来的约三分之一，加入约 0.1 秒短缓动；滚动条与手柄可直接接管。
- Reduce the sidebar wheel step to about one third with a short 0.1-second transition; scrollbar and controller input take over immediately.
- “查漏翻”改名为“补漏翻”，兼容旧词典中的已知按钮名称；加载结果不再立即被旧焦点替换。
- Rename the Chinese untranslated-export button and recognize its legacy labels; old focus no longer immediately replaces load results.

### 移除 / Removed

- 无。 / None.

## 1.0.9 — 2026-09-14

### 新增 / Added

- 同语言翻译分包：原翻译、补翻和个人修正可分别保存，选择语言时自动一起加载。
- Load multiple translation files per language, keeping base translations, gap fills and personal corrections in separate files.

### 更改 / Changed

- 语言下拉按语言分组并显示文件数，支持 `zh` / `en` 简称及大小写混用；补翻流程和覆盖顺序写入两套说明。
- Group the language dropdown with file counts, support `zh` / `en` aliases and case-insensitive codes, and document patch loading and precedence in both guides.
- 查漏翻跳过已翻好的界面词条，未填写的译文不覆盖旧翻译。
- Gap exports skip translated UI entries; untranslated placeholders do not overwrite existing translations.
- 查漏翻的备用词典读取不再把纯空格当译文，导出数量包含本模组界面词条。
- Fallback dictionary checks ignore whitespace-only translations, and export counts include this mod's UI entries.
- 再次打开配置页，从选中模组向上、向下同时渐入；在底部时向上展开，关闭页面后不残留透明行。
- Reopening the settings page reveals rows outward from the selected mod, or upward from the bottom, and closing restores pending rows.

### 移除 / Removed

- 无。 / None.

## 1.0.8 — 2026-09-12

### 手柄实测第三轮修复

- **「默认/清空」纵向列导航**：按键绑定单元格右侧的「默认 / 清空」是同一列里上下堆叠的两个按钮，
  此前行内链把它们当左右邻居、且行间按下会错位。`NavChain` 重写为列模型——行内按 x 聚成「格」，
  上下走同列纵贯（本行默认→本行清空→下行默认），左右只在同行相邻格之间跳；出格到相邻行时
  「同列优先、最近列兜底」。模组行的 行/导出/置顶 三列同理保持列位。
- **焦点框改空心描边环**：原实现是塞在宿主底下的米白实底图——对「底图就在宿主自己身上」的控件
  （「默认」丝带按钮）实底会把色带的虚线内框盖掉、看着像描边错位。现在焦点框是程序化生成的
  空心圆角环、画在最上层并贴在 `targetGraphic` 的矩形上（比命中框更贴可见形状）。
- **按键绑定框要长按确认**：原版捕获一启动，下一帧任何键（含手柄 JoystickButton）`GetKeyDown`
  就直接写入——选中后随便按个键就改了。补丁接管捕获 Update：Escape/Pause 取消照旧，KeyCode
  分支改成「同一键持续按住 ≥1s 才提交」；提示语换成本模组本地化词条（中「长按某个键 1 秒换绑」/
  英 Hold a key for 1s to rebind）；捕获中焦点框亮橙、B 键取消捕获不退页。
  （Path 型绑定走 RebindingOperation，保持原版即时捕获。）
- **滑条连调节奏**：1s→10 步/秒、3s→40 步/秒（原 1.5s→10/s）。
- 部署路径回 `BepInEx`；csproj 引用三层兜底（现用包扁平 → BepInEx3 扁平 → BepInEx 分区树）。

## 1.0.7 — 2026-09-12

### 手柄实测第二轮修复

- **下拉展开后焦点跳出弹层**：`NavChain` 每帧重链时把展开项 Toggle 也收进了「单元格行内链」
  （展开项是 TMP_Dropdown 的子节点），覆盖掉 TMP 自己设的项间导航——按方向键直接跳到旁边的
  「默认」按钮。现 `CollectRow` 跳过所有 `TMP_Dropdown` 子级可选件。
- **展开项左右也切项**：TMP 默认项导航是「上/左=前一项、下/右=后一项」，左右同样能切。新增
  `DropdownNavFix`：展开瞬间把项导航重链成纯上下，左右不再动作。
- **输入框选中即陷进编辑**：TMP 的 `OnSelect` 会直接 `ActivateInputField`，选中后方向键变光标移动、
  出不来了。现按与滑条同一套门控语义处理：`shouldActivateOnSelect=false`（TMP 自带开关，
  选中不再自动编辑；A 走 `OnSubmit` 激活，鼠标点击走 `OnPointerClick` 不受影响），
  B 退出编辑，编辑中焦点框亮橙。
- **下拉展开中按 B** 现在只关下拉、不弹层（`ConsumeBack` 优先于层级弹出判）。

## 1.0.6 — 2026-09-12

### 手柄支持按实测重做（两级面板模型）

1.0.5 的自由焦点导航实测不可行：行内复选框选不到（uGUI Automatic 让右移越过它们直奔滚动条/右栏）、
侧栏长按下摇越滚越抖（每次选中都强制重建布局 + 插值目标反复重置）、滑条一步 = 区间 10%
（0-10000 的滑条一步 1000）且极易误触、LB/RB 切模组后列表不跟滚。

- **两级锁定**（`PanelFocus`）：进页焦点锁左栏（模组行/分区行/复选框/表头/工具行/搜索/返回）；
  模组行 A=选中并展开分区，已选中模组行再 A=分区行在时落到第一分区行、无多分区直接进右栏，
  分区行 A=进右栏。右栏内焦点锁死在 `menu.Content` 子树里，移动不会越回左栏；
  B 键分级：滑条调值中→退调值 → 输入框编辑中→退编辑 → 锁右栏→回左栏（焦点回到来路行）→
  锁左栏→原返回。返回拦截打在 `PeakChildPage.GetParentPage`（所有返回路径的统一出口，
  副作用是同页早退时日志留一行 "Trying to transition to current page"）。
- **显式导航链**（`NavChain`）：左栏行/分区行/复选框/表头、右栏单元格内控件，全部钉成 Explicit
  上下左右（每帧按可见行重算），复选框稳定可达；侧栏滚动条 `navigation=None`（摇杆不再搓滚动条，
  鼠标拖拽不受影响）。
- **滑条 A 键调值**（`SliderGate` + `Slider.OnMove`/`stepSize` 补丁，官方 `SelectableSlider` 同款语义）：
  选中滑条按 A 才进调值态（焦点框变亮橙提示），左右调值；未激活时方向键纯导航。
  步进 cap 到区间 0.5%（整数档最少 1）；按住 1.5s 后 10 步/秒连调（`UI/Navigate` 动作在时自驱动，
  缺失则退回引擎自带 ~0.5s/10 次节奏）。
- **跟滚/防抖**：LB/RB 切模组后焦点落到新选中行 → 自动滚入视野；`FocusScrollIntoView` 改为每帧按
  当前世界矩形追目标（`MoveTowards` 到位即停），不再每次选中强制重建布局。
- 启动日志文案 `XUnity 已驯服` → `已适配 XUnityTranslate`。

## 1.0.5 — 2026-09-12

### 新增

- **手柄支持**（`src\GamepadSupport.cs`）：模组设置页此前对手柄完全不可用——ModConfig 的 `ModSettings` 页是
  `PeakChildPage`，没实现 `INavigationPage`，进页面后 EventSystem 无焦点、官方兜底返回 null。现按官方
  （Zorro.ControllerSupport）语义补齐：
  - **进页自动聚焦**：`FocusKeeper` 每帧兜底——手柄方案下选中为空 / 失活 / 不在本页时，自动选第一个选项
    单元格的可交互件（同 `SharedSettingsMenu.GetDefaultSelection` 口径），其后回退当前模组行、返回键；
    搜索 / 筛选重建列表后丢焦也自动恢复。弹窗（Modal / MenuWindow）、按键捕获、下拉展开期间不抢焦点。
  - **焦点框**：模组行 / 分区行 / 复选框 / 工具行按钮 / 语言下拉统一一圈米白描边（外扩 2px、藏在行底图后、
    选中才亮），焦点在哪一眼可见——此前这些 Button 的 `targetGraphic` 为空，手柄选中了也没任何反馈。
    米白在 TimeTheme 表里，夜间自动变紫。
  - **选中滚入视野**：`FocusScrollIntoView` 按矩形世界坐标算 `verticalNormalizedPosition`、15/s 插值，
    模组行 / 分区行 / 行内复选框 / 右侧选项控件全部覆盖；比官方 `ScrollRectAutoScroller` 的兄弟序号推算
    更准，且不依赖 `verticalScrollbar`（右侧选项列表本来没有滚动条）。
  - **LB/RB 切模组**：`SidebarTabHotkeys` 用 `UITabLeft`/`UITabRight`（与官方 `SettingsTABS` / `UIInputHandler`
    同一对动作）按显示顺序切模组，跳过被筛选藏掉的行，焦点原地不动。不用 `TABS.SelectRelative`——
    它的 `buttons` 会把挪进列表的分区行也算进去、且不认置顶排序。
  - 提交 / 返回天然可用：`Button.onClick` 吃 Submit，`PeakChildPage` 的 `IHaveParentPage` 让 B 键返回上一层。
    说明面板此前已支持手柄焦点（`FindFocusedCell`），`TypingGuard` 只拦键盘设备、不误伤手柄。

### 上游同级限制（未动）

- 按键绑定捕获只听 `<Keyboard>/anyKey`，捕不到手柄键（ModConfig `InputBindingCaptureService`）；
- 文本输入框无屏幕键盘，手柄只能聚焦不能打字（官方 JoinRoom 在 Deck 上走 Steam 对话框绕开，同级限制）。

## 1.0.4 — 2026-09-12

### 新增

- **本模组行固定最顶**：「Mod Config Enhance」永远钉在左栏第一行（先于其他置顶行），不参与「置顶」列勾选——
  该列复选框在自身行上隐藏，置顶竖条常亮。行名按 ModConfig 的 `FixNaming(插件名)` 现算，不硬编码；
  启动时会把早先版本写进 `Pinned mods` 的自身行名清掉（置顶已是结构行为）。
- **自身行名上色**：白天低饱和橙 #EFC95F，夜间暖金 #C9A55B，选中时深琥珀 #8A6207（白底/紫底都压得住）。
  选中本模组时它的分区行名也跟着上色。（ModdedTABSButton.Update 每帧把字往黑/白 Lerp，上色在 LateUpdate 里逐帧断言。）
- **说明面板加标题行**：悬停选项时面板第一行显示「模组名  v版本号」（取 IBepInExProperty 的 GetCategory +
  Pluginfo.Metadata.Version），默认/范围/可选那行不变；通知文本不占标题行。
- **新选项 `Language row only`**（`2. Localization`，默认关，需重启）：工具行只保留「语言 ▾ / 刷新」一排并上移到顶，
  「导出翻译 / 查漏翻 / 仅导出已选」「显示原文 / 重载翻译 / 打开目录」两排收起，左侧列表自动上移、不留空白。
  面向已配好翻译的整合包与不想看到工具行的玩家。「导出」列在此期间一并隐藏（勾选状态保留，关掉本项即恢复）。
- **新选项 `Disable XUnity machine translation`**（`2. Localization`，默认开）：XUnity 在场时启动即把
  `AutoTranslatorConfig.ini` 的 `Endpoint` / `FallbackEndpoint` 清空，并把当前选中的翻译端点摘掉——它的出厂默认
  `GoogleTranslateV2` 会把每个界面文本丢去机翻（无词库的新整合包装上就满屏机翻），词典与镜像照常生效。
  三层下手：`Settings.SetEndpoint("")`（prefs + 静态字段）→ `TranslationManager.CurrentEndpoint/FallbackEndpoint` 置空
  （当次生效）→ ini 文件兜底（下次启动生效）；另清空 `Translation\*\Text\_AutoGeneratedTranslations*.txt`
  机翻结果缓存（本质是词典，端点清了它照样命中），并停用 XUnity 内置静态词典 `UseStaticTranslations`
  （出厂开，自带 `置顶=Fixed` 这类条目，命中后还会回填进缓存词典）。确实要用机翻关掉本项即可。

### 修复

- **搜索 / 筛选 / 只看按键后，不匹配的模组行现在会立刻自动隐藏**：ModConfig 原版只在「当前选中模组整条没匹配、
  要自动跳走」那一支才藏行，其余路径不匹配的行一直留着、点进去是空的。`ShowSettings` postfix 现在按同口径
  （条件设置 → 搜索词 → 类型位掩码）重算有匹配项的模组集合，逐行校正显隐；恢复匹配的模组行自动亮回。
  零匹配时选中行被藏，分区容器一并收掉。
- 附带修正：「翻译工具」整关时「导出」列此前仍可能显示（其开关已不可见），现在仅在完整工具行在场时才显示。
- 导出文件名前缀跟随当前语言：`待翻译_*.txt` / `未翻译_*.txt` → 英文下 `ToTranslate_*.txt` / `Untranslated_*.txt`。

## 1.0.3 — 2026-09-11

### 语言下拉 = 整个游戏的翻译语言（面向整合包）

- **选语言文件时直接把 XUnity AutoTranslator 切到该语言**，不再受它 ini 里 `Language=` 的限制、也不再往 `Translation\zh` 里塞别的语言：
  反射改 `Settings.Language` 并按 `{Lang}` 模板重算 `TranslationsPath` / `AutoTranslationsFilePath` / `Substitution` / `Pre` / `PostprocessorsFilePath`
  与目标语言的空格规则（`XUnityBridge.SetLanguage`，对着 5.6.1 的字段做），词典目录随之变成 `Translation\<码>\Text\`；
  文件里非 `MCE_` 的行写成那里的 `ModConfigEnhance_<码>.txt`（其他语言目录里的旧镜像先删）；`ReloadTranslations` 后词典里没有的文本
  XUnity 自己还原成原文——选 en-us 就是全英文。`AutoTranslatorConfig.ini` 的 `Language=` 同步改掉，下次启动一致。
  语言码映射：`Translation\` 下已有同名目录用同名，有同语言目录（`zh-cn` ↔ `zh`）沿用它、不另起目录，否则按 XUnity 惯例取主语言子标签（`en-us` → `en`、`de-de` → `de`，繁中 `zh-TW`）。
- 「跟随游戏语言」同样驱动 XUnity（游戏切语言 → XUnity 跟着切）。
- cfg 项 `Mirror language file to XUnity` 改名 **`Switch XUnity language with the language file`**（默认开；关掉完全不碰 XUnity）。
  说明面板提示改为「XUnity 已切到「xx」（交给它 N 条）」/「无法在运行时切换 XUnity」。
- **「重载翻译」改为「重新读语言文件 + 词典」**：原来它只调 XUnity 的 ReloadTranslations，用户改了 `language\zh-cn.txt`
  却看不到变化（镜像是选语言那一刻的快照，`MCE_` 词条更不会动）。现在先按当前选择重新 Apply（词条重新入表 + 重写镜像 + RefreshAllText），
  再让 XUnity 重读——手改语言文件后点一下即可，不必重新选一次语言。没装 XUnity 时按钮也可用（只刷本模组界面），不再置灰。
- **说明改为 `config\ModConfigEnhance\readme.md`**（Markdown，根目录，「打开目录」一点就见；旧 `_readme.txt` / `_template.txt` 启动时清掉），
  导出文件头砍到只剩「翻译规则 + 本次导出」，教程一句话指到 readme.md。
- **说明文字单语化**：`readme.md`、导出文件头按当前界面语言（选了文件按文件语言码，跟随游戏按游戏语言）输出简中 / 繁中 / 英文之一，
  `_readme.txt` 每次切语言后重写。README.md 是静态文件，保持中英双语。
- 随部署把 `Translation\zh\Text` 下的旧词典（mods / checkpointssave / plantholderoverhaul / SaveMod_Options / terrainrandomiser）
  合并进用户的 `zh-cn.txt`（新增 684 条，去重 29 条，22 条同键不同译文保留 zh-cn.txt 原有；报告在 `验证\zh-cn_merge_report_*.md`）。
  原文件暂留原位（与镜像重复无害），是否移走等用户确认。

## 1.0.2 — 2026-09-11

### 语言文件 = 导出文件（按用户构想重定）

- **导出翻译永远包含本模组**：文件开头先写 `MCE_*` 界面词条块（右边预填英文），再写各模组分块；「仅导出已选」不会把本模组去掉。
  翻完改名放进 `language\` 就是完整的语言文件——本模组界面 + 所有模组的选项文本一份搞定，面向整合包。
- **模板文件取消**：`_template.txt` 不再生成（启动时删掉旧的），改为 `_readme.txt`（中英说明：导出 → 翻 → 改名 → 刷新 → 选中，以及各按钮的角色）。
  导出文件头的第 3 步同步改写。
- **切换语言不再把右栏变成「LOC: 0」**：之前调 `LocalizedText.RefreshAllText()` 全局刷新，ModConfig 的选项单元格是原版设置单元格的克隆、
  文字上带着索引为空的 LocalizedText，一刷全变 `LOC: `。改为只刷索引 `MCE_` 开头的组件 + 本模组的静态文本（筛选标签、多选下拉三项）。
- **语言下拉字色**：与 ModConfig 的筛选下拉同一套（深底、米白字、米白箭头），之前用预制体默认字色在深底上看不清。
- **左栏逐行出现 + 音效**（`SidebarCascade`）：页面打开时表头与模组行按显示顺序每 0.05 s 淡入一行，选中模组后分区行同样淡入；
  音效借 `Templates.SettingsCellPrefab` 上的 `fadeInSFX`，与右侧单元格同一声。页面关闭时未完成的淡入直接置 1。

## 1.0.1 — 2026-09-11

首次进游戏后的四处修正（用户实测反馈）。

- **第三行（语言下拉 + 刷新）没建出来**：`MenuAPI.CreateDropdown` 直接实例化到还没激活的页面下，`PeakDropdown.Awake` 不跑、
  `RectTransform` 为空，`SetSize` 抛空引用、整行被跳过，「显示原文」下面因此空一行。改为无父节点实例化再 `ParentTo(page)`
  （`CreateMenuButton` 与 ModConfig 自己的筛选下拉都是这么做的）。
- **侧栏在 ESC 页太短**：底边留白 150 → 60，与右侧选项列表基本齐平（右侧 MainPage 底边离屏幕 30），不贴屏幕底。
- **「只显示快捷键选项」整行都能点**：表头行原来整行挂 Button，点到「导出 / 置顶」标题也会切换。改成只在文字 + 复选框那段铺一块透明点击区，底图不接射线。
- **搜索框打字误触模组快捷键**：新增 `TypingGuard`——页面上任意 TMP 输入框拿着焦点时，Harmony Prefix 让
  `Input.GetKey / GetKeyDown / GetKeyUp`（KeyCode 与 string 两个重载）和 InputSystem `ButtonControl.isPressed / wasPressedThisFrame /
  wasReleasedThisFrame`（仅键盘设备）一律返回 false。鼠标、手柄与 UI 输入模块（走 InputAction）不受影响；
  用 InputAction 轮询快捷键的模组仍会触发（少见）。启动日志有「[打字保护] 已挂载：旧 Input n/6，InputSystem 按键 n/3」。
- **清空搜索后被藏掉的模组行回不来**：ModConfig 的 `SetSearch` 直接 `ShowSettings`，后者把没有匹配项的页签 `SetActive(false)`，
  只有 `SetFilter` 会先 `SetAllTabsActive`。给 `SetSearch` 加 Prefix 先恢复全部页签。

## 1.0.0 — 2026-09-11

首个版本。功能整体来自 LocalFix 2.16.1 第 9 分区（树形侧栏 / 说明面板 / 界面汉化 / 翻译导出 / XUnity 联动 / 纸面样式 / TimeTheme 跟色），
LocalFix 2.17.0 起那边不再有这套代码。

### 相对 LocalFix 2.16.1 的新增

- **语言文件系统**（`src\Loc.cs`）：界面词条改成稳定键 `MCE_*` 写进游戏语言表；`config\ModConfigEnhance\language\` 下
  `en-us / zh-cn / zh-tw`（只建不覆）+ `_template.txt`（每次重写，含中英教程与投稿方式）。
  左栏第三排 `语言 ▾ / 刷新`：第一项「跟随游戏语言」，其后是目录里的文件（`_` 开头不列；首行 `// display:` 可自定义显示名）。
  选中即 `RefreshAllText()` 实时刷新；解析错误（缺等号 / 无有效行 / 文件不存在）在说明面板用橙色带行号提示；缺键回落英文并提示缺几条。
- **镜像给 XUnity**：选中文件里非 `MCE_` 的行（本模组 cfg 选项名 / 说明的英文=译文）写到
  `Translation\<语言>\Text\ModConfigEnhance_<文件名>.txt`，换语言先删旧镜像再 `ReloadTranslations()`；只在文件语言码与 XUnity `Language=`
  一致时写，不一致在说明面板提示改 ini 重启。开关 `Mirror language file to XUnity`（默认开）。跟随游戏时按游戏语言挑 zh-cn / zh-tw / en-us。
- **所有自产文字挂 LocalizedText**：工具行按钮改 `SetLocalizationIndex`，通知文本改 `{0}` 占位词条，切语言全页同步。
- 第二排改三格：`显示原文 / 重载翻译 / 打开目录`（打开 `config\ModConfigEnhance\`，language 与 export 都在里面）。
- 导出目录改为 `config\ModConfigEnhance\export\`；导出文件头改中英双语，目标目录按 XUnity 当前 `Language=` 写。
- 说明面板 `ShowNotice(text, warning)`：警告用橙色，恢复白色时重新交给 TimeTheme 刷夜间色。
- cfg 项全部英文命名（Thunderstore 友好），译文走语言文件镜像；`3. State` 分区 Hidden。
- 首启从 `com.aiae.localfix.cfg` 一次性迁移「置顶 / 不导出 / 仅导出已选」。
- 检测 2.17.0 之前的 LocalFix：在场则不挂载并在日志报错，避免页面建两遍。

### 沿用的已知取舍

- 置顶用左缘竖条而非描边；XUnity 无运行时切目标语言，只能原文 / 译文切换 + 重载；
  用户自己的 XUnity 全局词典若含 `Search=搜索` 之类条目，会盖过语言文件里对应的英文（服从全局词典，不做屏蔽）。
