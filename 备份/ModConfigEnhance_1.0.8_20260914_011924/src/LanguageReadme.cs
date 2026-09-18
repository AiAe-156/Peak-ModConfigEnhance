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
        4. **Enable** — back in the game: left column, third row → **Refresh** → pick the file.

        ## What happens when you pick a file (no restart)

        - This mod's UI switches at once.
        - XUnity AutoTranslator switches to that language: its dictionary folder becomes `Translation\<code>\Text\`, the translations are written there as `ModConfigEnhance_<code>.txt`, dictionaries reload, texts without a translation show the original, and `Language=` in `AutoTranslatorConfig.ini` is updated for the next start.
        - Errors (missing `=`, empty file …) are shown in orange in the description panel, with line numbers.

        ## The other buttons

        | Button | Purpose |
        |---|---|
        | Reload texts | Re-read the language file **and** XUnity's dictionaries from disk. Use this after editing `language\<your file>.txt` by hand — no need to pick the language again |
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
        | 重载翻译 | 从磁盘重新读取语言文件**和** XUnity 的词典。手改 `language\你的文件.txt` 后点它即可，不用重新选一次语言 |
        | 查漏翻 | 列出还没翻的，可选 |
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
        | 重載翻譯 | 從磁碟重新讀取語言檔案**和** XUnity 的詞典。手改 `language\你的檔案.txt` 後點它即可，不用重新選一次語言 |
        | 查漏翻 | 列出還沒翻的，可選 |
        | 顯示原文 / 顯示譯文 | XUnity 的 Alt+T：整個遊戲在原文 / 譯文間切換 |

        ## 投稿

        翻好的檔案歡迎寄給作者 AiAe：信箱 `2323086800@qq.com` / QQ 群 `1104320838`，下個版本隨包附帶。

        """;
}
