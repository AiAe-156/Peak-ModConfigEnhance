# ModConfig Enhance 1.0.0 验证清单（2026-09-11）

部署：`plugins\0核心&基础模组\[界面]ModConfig增强-ModConfigEnhance\`（与 LocalFix 2.17.0 同批）。
日志：`BepInEx\LogOutput.log`（插件行）+ `output_log.txt`（Unity 红字，重启即覆盖）。

## 一、启动

- [ ] LogOutput 有 `[ModConfig Enhance v1.0.0] 已加载。` 与第二行摘要（树形侧栏 / 界面本地化 / 说明面板 / 翻译工具 / 语言）。
- [ ] LocalFix 摘要第 9 行是「已独立为 ModConfig Enhance，本分区无代码」；**没有**「在场的 LocalFix … 本模组不挂载」的 Error。
- [ ] `config\ModConfigEnhance\language\` 生成 `en-us.txt` `zh-cn.txt` `zh-tw.txt` `_template.txt`；`export\` 为空目录。
- [ ] `com.aiae.modconfigenhance.cfg` 的 `3. State` 里 `Pinned mods` / `Excluded from export` / `Export selected only` 与旧 `com.aiae.localfix.cfg` 第 9 分区一致，日志有「已从 LocalFix 配置迁移 N 项」。

## 二、页面（游戏语言 = 简体中文）

- [ ] 模组设置页仍是左树右选项，置顶列 / 导出列勾选状态与之前一致。
- [ ] 左栏三排工具行：`导出翻译 / 查漏翻 / 仅导出已选`、`显示原文 / 重载翻译 / 打开目录`、`[跟随游戏语言 ▾] [刷新]`，第三行不超框、下拉字号与表头一致。
- [ ] 界面词是中文（搜索、模组、类目、默认、清空、筛选五项、Nothing/Everything/Mixed）。
- [ ] 本模组自己的选项名在列表里显示为中文（`树形侧栏` `选项说明面板` …）——这是镜像 `Translation\zh\Text\ModConfigEnhance_zh-cn.txt` 起效；若仍是英文，看日志「[语言]」行的镜像结果，并核对 XUnity `Language=` 是不是 `zh`/`zh-CN`。
- [ ] 「打开目录」弹出资源管理器到 `config\ModConfigEnhance\`。

## 三、语言切换

- [ ] 下拉选 `English` → 全页界面词、三排按钮、表头即时变英文，说明面板提示「Language file applied: English (N entries).」；`Translation\zh\Text\` 下旧镜像被删，说明面板提示未镜像（目标 zh ≠ en-us）。
- [ ] 选回「跟随游戏语言」→ 恢复中文，镜像文件重新出现。
- [ ] 复制 `_template.txt` 为 `de-de.txt`，随便改几行 → 点「刷新」→ 下拉出现 `de-de`（或首行 display 名）→ 选中即生效，缺的词回落英文并有橙色「缺 N 条」提示。
- [ ] 在 `de-de.txt` 里删掉某行的等号 → 刷新 → 选中 → 橙色提示「缺少等号的行: N」。
- [ ] 删除 `de-de.txt` → 不刷新直接选它（若还在下拉里）→ 橙色「文件不存在（点刷新）」。
- [ ] 游戏 ESC 设置里切语言到英文再切回 → 我们的词条不丢（`OnLanguageChanged` 重注册）。

## 四、翻译工具

- [ ] 导出翻译 → `export\待翻译_*.txt`，文件头是中英双语，目标目录写的是 `Translation\zh\Text\`。
- [ ] 查漏翻 → 数量与 2.16.1 相近；peakReconnect 无键名清单。
- [ ] 显示原文 / 重载翻译按钮文字随语言切换；无 XUnity 时置灰。

## 五、夜间（TimeTheme）

- [ ] 夜里打开：说明面板白字变紫、语言下拉底色随夜色；出一次橙色警告后再悬停选项，字色恢复夜间紫而非白。
