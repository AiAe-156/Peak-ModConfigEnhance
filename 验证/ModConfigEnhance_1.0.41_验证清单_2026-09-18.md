# ModConfigEnhance 1.0.41 验证清单（2026-09-18）

改动：说明面板三个 TMP 文本关闭 `parseCtrlCharacters`（见 `src/ModConfigDescriptionPanel.cs` `MakeText`），删除 1.0.40 几何诊断与 1.0.32 强制重算。
已构建部署到 `BepInEx\plugins\[界面]ModConfig增强-ModConfigEnhance\`（改动前 dll 在 `备份\部署前_1.0.40_*`，改动前源码在 `备份\ModConfigEnhance_1.0.40_*`）。

## 必测

- [ ] 进游戏 → 模组设置 → 鼠标悬停「导出翻译」钮：说明面板末行应完整显示
      `如何使用请查阅*\PEAK\BepInEx\config\ModConfigEnhance\readme.md`（`\readme.md` 的反斜杠在、`eadme.md` 不再叠在行首）。
- [ ] 其余 8 个工具行注释（补漏翻 / 仅导出已选 / 显示原文 / 重载翻译 / 打开目录 / 排序 / 语言 / 刷新）显示正常、无变化。
- [ ] 悬停任意选项：模组名 / 元信息 / cfg 描述显示正常，多行描述换行正常（换行本来就是真换行，不靠 TMP 解析）。
- [ ] 点「导出翻译」后面板显示的导出路径整行可读。
- [ ] 日志 `BepInEx\LogOutput.log` 里不再出现 `[面板] 几何` / `[面板] 仍有回退字符` / `[面板] 字体:`。

## 若仍重叠

先把当前显示的那条文本里每个字符的 `bottomLeft.x` 打出来看同一行是否回到 0（这是本 bug 的指纹），不要再从字体 / 子网格入手。
辅助脚本：`验证\scan_tmp_escapes_20260918.py` 扫描所有会进面板的文本里的 TMP 转义序列。
