# 更新日志

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
