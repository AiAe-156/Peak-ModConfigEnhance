namespace ModConfigEnhance;

/// <summary>config\ModConfigEnhance\readme.md 的内容，按当前界面语言挑一份（C# 原始字符串；{VERSION} 由 Loc 替换）。</summary>
internal static class LanguageReadme
{
    internal static string For(Loc.UiLang lang) => lang switch
    {
        Loc.UiLang.ZhCn => ZhCn,
        Loc.UiLang.ZhTw => ZhTw,
        _ => En,
    };

    private const string En = """
        # ModConfig Enhance {VERSION} · language files

        > Rewritten on every start and language switch, in the current UI language.

        ## What is in this folder

        | Path | Purpose |
        |---|---|
        | `language\xx-yy.txt` | A language file: this mod's UI terms + every mod's option texts |
        | `language\en-us.txt` `zh-cn.txt` `zh-tw.txt` | Built-in. Created once, never overwritten; delete to reset |
        | `export\` | Where "Export texts" writes its files |
        | `readme.md` | This note |

        Files starting with `_` never show in the dropdown.

        ## Multiple files per language

        Keep the original file and add translated patches alongside it:

        ```text
        zh-cn.txt
        zh-cn_1_基本.txt
        zh-cn_20_补翻.txt
        zh-cn_90_个人修正.txt
        ```

        The part before the first `_` is the language code; the rest is a label (`ALL` has no special meaning). Use `-` inside codes. Codes are case-insensitive: `zh` aliases `zh-cn`, `en` aliases `en-us`; `zh-tw` stays separate from Simplified Chinese. Other regional codes remain separate.

        Base files load first, then suffixed files in case-insensitive filename order. This is text sorting, not numeric sorting; use `01`, `02`, `10` for sequential numbers. Later valid translations win; identical entries are skipped and conflicts are logged with their source files. Empty values, original-text placeholders and unchanged English `MCE_` values cannot overwrite existing translations.

        The dropdown shows one entry per language with a file count. Follow game language also loads the matching group. Only `.txt` files directly in `language\` are read; `_`-prefixed files and subfolders are ignored. Keep backups in a subfolder. The group is validated before applying; fix format errors and reload.

        To fill gaps: keep the old files → Untranslated → translate → save with another suffix for the same language → Reload texts. Already translated `MCE_` UI entries are omitted from the gap export. Do not replace your full translation with a patch. For a newly added language, click Refresh and select it.

        ## Sidebar animation

        Selecting a mod or category shows the mod name, version, current subcategory and description from manifest.json above the options. Pointing at or focusing an option restores its own description.

        The left sidebar wheel step is reduced to about one third, with an approximately 0.1-second transition. Scrollbar dragging and controller positioning take over immediately. Other scroll panels keep their existing behavior.

        After a successful language selection or reload, the description panel lists every loaded filename in load order. Scroll inside it for long lists. File contents are not shown. Point at a setting to restore its description.

        Reopening the settings page reveals the selected mod row first, then one row above and below per beat. At the bottom, rows appear upward; without a valid selection, from the top. Closing ends the animation and restores pending rows.

        ## File format

        Same as an XUnity AutoTranslator dictionary: `key=translation`, `//` starts a comment, `\n` is a line break, `\=` an equals sign, `{0}` `{1}` are placeholders. Keep those as they are and never change anything left of `=`.

        | Block | Lines | Used by |
        |---|---|---|
        | First | `MCE_…=…` — this mod's UI. Keys are stable IDs, translate the right side only | this mod |
        | Rest | `original=translation` — every mod's option names / descriptions / choices | XUnity AutoTranslator |

        ## Make a new language in four steps

        1. **Export** — mod settings → left column → **Export texts**. Tick **Selected only** first to drop mods you don't need (this mod is always included).
        2. **Translate** — open the file in `export\` and fill the right side of each `=`. An AI can do the whole file.
        3. **Rename** — save as UTF-8, rename to a lowercase language code (`de-de.txt`, `ja-jp.txt`, `fr-fr.txt`, `pt-br.txt`) and move it into `language\`. Optional first line: `// display: <name for the dropdown>`.
        4. **Enable** — back in the game: left column, third row → **Refresh** → pick the language group.

        ## What happens when you pick a language (no restart)

        - This mod's UI switches at once.
        - XUnity AutoTranslator switches to that language: its dictionary folder becomes `Translation\<code>\Text\`, the translations are written there as `ModConfigEnhance_<code>.txt`, dictionaries reload, texts without a translation show the original, and `Language=` in `AutoTranslatorConfig.ini` is updated for the next start.
        - Errors (missing `=`, empty file …) are shown in orange in the description panel, with line numbers.

        ## The other buttons

        | Button | Purpose |
        |---|---|
        | Reload texts | Re-read all files in the selected language group **and** XUnity's dictionaries from disk. Use this after editing `language\<your file>.txt` by hand — no need to pick the language again |
        | Untranslated | List what is still untranslated. Optional |
        | Show original / Show translated | XUnity's Alt+T: toggle the whole game between original and translated text |

        ## Contribute

        Finished translations are welcome — send them to the author (AiAe): e-mail `2323086800@qq.com` / QQ group `1104320838`. They ship with the next release.

        """;

    private const string ZhCn = """
        # ModConfig Enhance {VERSION} · 语言文件说明

        > 每次启动 / 切语言后按当前界面语言重写。

        ## 这个目录放什么

        | 路径 | 用途 |
        |---|---|
        | `language\xx-yy.txt` | 语言文件：本模组界面词条 + 所有模组的选项文本 |
        | `language\en-us.txt` `zh-cn.txt` `zh-tw.txt` | 内建三种。只建不覆，可随意改；删掉即恢复默认 |
        | `export\` | 「导出翻译」写文件的地方 |
        | `readme.md` | 本说明 |

        `_` 开头的文件不进下拉框。

        ## 同语言分包与补翻

        保留原文件，把补翻结果另存一份，一起放在本目录的 `language\` 下：

        ```text
        zh-cn.txt
        zh-cn_1_基本.txt
        zh-cn_20_补翻.txt
        zh-cn_90_个人修正.txt
        ```

        第一个 `_` 前是语言码，后面全是备注，`ALL` 没有特殊含义。语言码内部用 `-`，不分大小写：`zh` 等同 `zh-cn`，`en` 等同 `en-us`；`zh-tw` 与简体独立，其他区域码也不随意混合。

        先读无后缀的基础文件，再按文件名顺序读分包，不分大小写。按文字而非数值排序；连续编号建议补零为 `01`、`02`、`10`。同键同译文跳过，不同有效译文以后读文件为准，日志记录冲突来源。空白、与原文相同的占位内容、未修改的 `MCE_` 英文不能覆盖已有译文。

        下拉框每种语言只显示一项及文件数，「跟随游戏语言」也加载整组。只读 `language\` 当前层 `.txt`，忽略 `_` 开头文件与子目录，备份请放子目录。整组校验后再应用，格式错误修正后重载。

        补翻流程：保留旧文件 → 补漏翻 → 翻译 → 另存同语言的新后缀文件 → 重载翻译。补漏翻会跳过已翻好的 `MCE_` 界面词条。不要用补翻文件替换原文件；新加一种语言则点「刷新」后选择。

        ## 左栏渐入动效

        切换模组或分类时，顶部显示模组名、版本、当前子分类和 manifest.json 中的模组说明；移到或选中右侧具体配置项后恢复该选项说明。

        左栏滚轮步幅降为旧版约三分之一，配约 0.1 秒短缓动；拖动滚动条和手柄定位会直接接管。其他滚动区域保持原有行为。

        语言选择或重载成功后，说明栏按实际加载顺序列出全部文件名；长清单可在栏内滚动查看，不显示词典全文。移到具体配置项后恢复选项说明。

        再次打开配置页时选中模组行先出现，然后每拍同时向上、向下各显示一行；选在底部则向上显示，无有效选择则从顶部开始。关闭页面会结束动画并恢复尚未出现的行。

        ## 文件格式

        与 XUnity AutoTranslator 词典相同：`键=译文`，`//` 开头是注释，`\n` 换行、`\=` 等号、`{0}` `{1}` 占位符——原样保留，等号左边一个字符都别动。

        | 段 | 内容 | 谁来用 |
        |---|---|---|
        | 第一块 | `MCE_…=…` 本模组界面。键是稳定 ID，只翻等号右边 | 本模组 |
        | 之后 | `原文=译文` 各模组的选项名 / 说明 / 下拉选项 | XUnity AutoTranslator |

        ## 四步做一份新语言

        1. **导出** — 模组设置 → 左栏 **导出翻译**。先勾 **仅导出已选** 去掉不需要的模组（本模组永远包含）。
        2. **翻译** — 打开 `export\` 里的文件，填等号右边。可整份交给 AI。
        3. **改名** — 存成 UTF-8，改名为小写语言码（`de-de.txt`、`ja-jp.txt`、`fr-fr.txt`、`pt-br.txt`）放进 `language\`。首行可写 `// display: 下拉框显示名`。
        4. **启用** — 回游戏：左栏第三行 → **刷新** → 选中它。

        ## 选中后发生什么（不用重启）

        - 本模组界面立刻切换。
        - XUnity AutoTranslator 切到该语言：词典目录变成 `Translation\<码>\Text\`，译文写成那里的 `ModConfigEnhance_<码>.txt`，重载词典，没翻的文本显示原文，`AutoTranslatorConfig.ini` 的 `Language=` 同步改掉。
        - 出错（缺等号、空文件……）在说明面板用橙色带行号提示。

        ## 其他按钮

        | 按钮 | 用途 |
        |---|---|
        | 重载翻译 | 从磁盘重新读取所选语言整组文件**和** XUnity 的词典。手改 `language\你的文件.txt` 后点它即可，不用重新选一次语言 |
        | 补漏翻 | 列出还没翻的，可选 |
        | 显示原文 / 显示译文 | XUnity 的 Alt+T：整个游戏在原文 / 译文间切换 |

        ## 投稿

        翻好的文件欢迎寄给作者 AiAe：邮箱 `2323086800@qq.com` / QQ 群 `1104320838`，下个版本随包附带。

        """;

    private const string ZhTw = """
        # ModConfig Enhance {VERSION} · 語言檔案說明

        > 每次啟動 / 切語言後按目前介面語言重寫。

        ## 這個目錄放什麼

        | 路徑 | 用途 |
        |---|---|
        | `language\xx-yy.txt` | 語言檔案：本模組介面詞條 + 所有模組的選項文字 |
        | `language\en-us.txt` `zh-cn.txt` `zh-tw.txt` | 內建三種。只建不覆，可隨意改；刪掉即恢復預設 |
        | `export\` | 「導出翻譯」寫檔案的地方 |
        | `readme.md` | 本說明 |

        `_` 開頭的檔案不進下拉框。

        ## 同語言分包與補翻

        保留原檔案，把補翻結果另存一份，一起放在本目錄的 `language\` 下：

        ```text
        zh-cn.txt
        zh-cn_1_基本.txt
        zh-cn_20_补翻.txt
        zh-cn_90_个人修正.txt
        ```

        第一個 `_` 前是語言碼，後面全是備註，`ALL` 沒有特殊含義。語言碼內部用 `-`，不分大小寫：`zh` 等同 `zh-cn`，`en` 等同 `en-us`；`zh-tw` 與簡體獨立，其他地區碼也不隨意混合。

        先讀無後綴的基礎檔案，再按檔名順序讀分包，不分大小寫。按文字而非數值排序；連續編號建議補零為 `01`、`02`、`10`。同鍵同譯文略過，不同有效譯文以後讀檔案為準，日誌記錄衝突來源。空白、與原文相同的佔位內容、未修改的 `MCE_` 英文不能覆蓋已有譯文。

        下拉框每種語言只顯示一項及檔案數，「跟隨遊戲語言」也載入整組。只讀 `language\` 目前層級 `.txt`，忽略 `_` 開頭檔案與子目錄，備份請放子目錄。整組驗證後再套用，格式錯誤修正後重載。

        補翻流程：保留舊檔案 → 補漏翻 → 翻譯 → 另存同語言的新後綴檔案 → 重載翻譯。補漏翻會略過已翻好的 `MCE_` 介面詞條。不要用補翻檔案替換原檔案；新增一種語言則點「重新整理」後選擇。

        ## 左欄漸入動效

        切換模組或分類時，頂部顯示模組名、版本、目前子分類和 manifest.json 中的模組說明；移到或選中右側具體設定項後恢復該項說明。

        左欄滾輪步幅降為舊版約三分之一，配約 0.1 秒短緩動；拖動捲軸和手把定位會直接接管。其他捲動區域保持原有行為。

        語言選擇或重載成功後，說明欄依實際載入順序列出全部檔名；長清單可在欄內捲動查看，不顯示詞典全文。移到具體設定項後恢復選項說明。

        再次打開設定頁時選中模組列先出現，然後每拍同時向上、向下各顯示一列；選在底部則向上顯示，無有效選擇則從頂部開始。關閉頁面會結束動畫並恢復尚未出現的列。

        ## 檔案格式

        與 XUnity AutoTranslator 詞典相同：`鍵=譯文`，`//` 開頭是註解，`\n` 換行、`\=` 等號、`{0}` `{1}` 佔位符——原樣保留，等號左邊一個字元都別動。

        | 段 | 內容 | 誰來用 |
        |---|---|---|
        | 第一塊 | `MCE_…=…` 本模組介面。鍵是穩定 ID，只翻等號右邊 | 本模組 |
        | 之後 | `原文=譯文` 各模組的選項名 / 說明 / 下拉選項 | XUnity AutoTranslator |

        ## 四步做一份新語言

        1. **導出** — 模組設定 → 左欄 **導出翻譯**。先勾 **僅導出已選** 去掉不需要的模組（本模組永遠包含）。
        2. **翻譯** — 打開 `export\` 裡的檔案，填等號右邊。可整份交給 AI。
        3. **改名** — 存成 UTF-8，改名為小寫語言碼（`de-de.txt`、`ja-jp.txt`、`fr-fr.txt`、`pt-br.txt`）放進 `language\`。首行可寫 `// display: 下拉框顯示名`。
        4. **啟用** — 回遊戲：左欄第三行 → **重新整理** → 選中它。

        ## 選中後發生什麼（不用重啟）

        - 本模組介面立刻切換。
        - XUnity AutoTranslator 切到該語言：詞典目錄變成 `Translation\<碼>\Text\`，譯文寫成那裡的 `ModConfigEnhance_<碼>.txt`，重載詞典，沒翻的文字顯示原文，`AutoTranslatorConfig.ini` 的 `Language=` 同步改掉。
        - 出錯（缺等號、空檔案……）在說明面板用橙色帶行號提示。

        ## 其他按鈕

        | 按鈕 | 用途 |
        |---|---|
        | 重載翻譯 | 從磁碟重新讀取所選語言整組檔案**和** XUnity 的詞典。手改 `language\你的檔案.txt` 後點它即可，不用重新選一次語言 |
        | 補漏翻 | 列出還沒翻的，可選 |
        | 顯示原文 / 顯示譯文 | XUnity 的 Alt+T：整個遊戲在原文 / 譯文間切換 |

        ## 投稿

        翻好的檔案歡迎寄給作者 AiAe：信箱 `2323086800@qq.com` / QQ 群 `1104320838`，下個版本隨包附帶。

        """;
}
