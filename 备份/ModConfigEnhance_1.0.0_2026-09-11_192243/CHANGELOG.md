# 更新日志

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
