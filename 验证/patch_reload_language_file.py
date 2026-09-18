# -*- coding: utf-8 -*-
import io
root = r"F:\Games\SteamLibrary\steamapps\common\PEAK\MODs\ModConfigEnhance"

def rep(path, pairs):
    p = root + path
    s = io.open(p, encoding="utf-8").read()
    for a, b in pairs:
        assert a in s, "MISSING: " + a[:90]
        s = s.replace(a, b, 1)
    io.open(p, "w", encoding="utf-8", newline="\n").write(s)

# 1) 重载按钮：先按当前选择重新应用语言文件（重写镜像 + 刷界面），再让 XUnity 重读
rep(r"\src\ModConfigTranslationExport.cs", [
    ('''        MakeButton(page, "MCE_XUnityReload", "BTN_RELOAD", available ? XUnityColor : DisabledColor, left + third + gap, top, third, height, () =>
        {
            if (!XUnityBridge.CanReload)
            {
                Notify(Loc.Get("NOTICE_NO_XUNITY"), warning: true);
                return;
            }

            if (XUnityBridge.ReloadTranslations())
            {
                Notify(Loc.Get("NOTICE_RELOADED"));
            }
        });''',
     '''        // 重载 = 把磁盘上的语言文件重新读一遍：本模组界面词条重新应用、镜像重写，再让 XUnity 重读它自己的词典。
        // 这样用户改 language\\zh-cn.txt 也能立刻见效，不必重新选一次语言（改 XUnity 目录里的文件同样有效）。
        MakeButton(page, "MCE_XUnityReload", "BTN_RELOAD", XUnityColor, left + third + gap, top, third, height, () =>
        {
            Loc.ApplyResult result = Loc.Apply(Plugin.LanguageFile.Value, notify: false, refresh: true);
            string message = Loc.Get("NOTICE_RELOADED") + "\\n" + result.Message;
            if (!XUnityBridge.CanReload)
            {
                message += "\\n" + Loc.Get("NOTICE_NO_XUNITY");
            }

            Notify(message, result.Warning);
        });'''),
])

# 2) 提示词改成「重新读了语言文件 + 词典」
rep(r"\src\Loc.cs", [
    ('        new("NOTICE_RELOADED", "XUnity reloaded its translation files (same as Alt+R).", "已让 XUnity 重读 Translation 目录下的词典（同 Alt+R）。", "已讓 XUnity 重讀 Translation 目錄下的詞典（同 Alt+R）。"),',
     '        new("NOTICE_RELOADED", "Re-read the language file and the dictionaries from disk.", "已从磁盘重新读取语言文件与词典。", "已從磁碟重新讀取語言檔案與詞典。"),'),
])

# 3) readme / README：说明「重载翻译」现在也管语言文件
rep(r"\src\LanguageReadme.cs", [
    ('| Reload texts (Alt+R) | Re-read the dictionaries after editing a file by hand. Not needed otherwise |',
     '| Reload texts | Re-read the language file **and** XUnity\'s dictionaries from disk. Use this after editing `language\\<your file>.txt` by hand — no need to pick the language again |'),
    ('| 重载翻译 (Alt+R) | 手改文件后重读，平时不用 |',
     '| 重载翻译 | 从磁盘重新读取语言文件**和** XUnity 的词典。手改 `language\\你的文件.txt` 后点它即可，不用重新选一次语言 |'),
    ('| 重載翻譯 (Alt+R) | 手改檔案後重讀，平時不用 |',
     '| 重載翻譯 | 從磁碟重新讀取語言檔案**和** XUnity 的詞典。手改 `language\\你的檔案.txt` 後點它即可，不用重新選一次語言 |'),
])

rep(r"\README.md", [
    ('| 重载翻译 | = XUnity 的 Alt+R。手改词典文件后重读，平时不用 |',
     '| 重载翻译 | 从磁盘重新读语言文件（重新应用 `MCE_` 词条 + 重写镜像）再让 XUnity 重读词典。手改 `language\\*.txt` 或 `Translation\\` 下的词典后点它即可，不用重新选语言 |'),
])

# 4) CHANGELOG
p = root + r"\CHANGELOG.md"
s = io.open(p, encoding="utf-8").read()
anchor = "- **说明改为 `config\\ModConfigEnhance\\readme.md`**"
assert anchor in s
s = s.replace(anchor, '''- **「重载翻译」改为「重新读语言文件 + 词典」**：原来它只调 XUnity 的 ReloadTranslations，用户改了 `language\\zh-cn.txt`
  却看不到变化（镜像是选语言那一刻的快照，`MCE_` 词条更不会动）。现在先按当前选择重新 Apply（词条重新入表 + 重写镜像 + RefreshAllText），
  再让 XUnity 重读——手改语言文件后点一下即可，不必重新选一次语言。没装 XUnity 时按钮也可用（只刷本模组界面），不再置灰。
''' + anchor, 1)
io.open(p, "w", encoding="utf-8", newline="\n").write(s)
print("ok")
