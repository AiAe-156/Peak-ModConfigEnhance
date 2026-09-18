# -*- coding: utf-8 -*-
"""把 Translation\zh\Text 下的旧词典合并进 language\zh-cn.txt（去重，已有键优先）。"""
import io, os, shutil, datetime

root = r"F:\Games\SteamLibrary\steamapps\common\PEAK\BepInEx\config"
target = os.path.join(root, r"ModConfigEnhance\language\zh-cn.txt")
src_dir = os.path.join(root, r"Translation\zh\Text")
sources = ["mods.txt", "checkpointssave.txt", "plantholderoverhaul.txt", "SaveMod_Options.txt", "terrainrandomiser.txt"]
backup_dir = r"F:\Games\SteamLibrary\steamapps\common\PEAK\MODs\ModConfigEnhance\验证"

def split(line):
    """返回 (key, value)；找第一个未转义的 =。不是词条返回 None。"""
    for i, ch in enumerate(line):
        if ch == "=" and (i == 0 or line[i - 1] != "\\"):
            return line[:i], line[i + 1:]
    return None

stamp = datetime.datetime.now().strftime("%Y-%m-%d_%H%M%S")
shutil.copy2(target, os.path.join(backup_dir, f"zh-cn_before_merge_{stamp}.txt"))

existing = io.open(target, encoding="utf-8-sig").read().replace("\r\n", "\n")
known = {}
for line in existing.split("\n"):
    if line.startswith("//") or not line.strip():
        continue
    kv = split(line)
    if kv and kv[1] != "":
        known.setdefault(kv[0], kv[1])

out = [existing.rstrip("\n"), "", f"// ---- Merged from Translation\\zh\\Text ({stamp[:10]}): {', '.join(sources)} ----",
       "// 键与上面重复的已去掉（以上面为准）；同键不同译文见本次合并报告。"]
added = 0
dups_same = 0
conflicts = []
for name in sources:
    path = os.path.join(src_dir, name)
    if not os.path.exists(path):
        continue
    out.append(f"// -- from {name}")
    for raw in io.open(path, encoding="utf-8-sig").read().replace("\r\n", "\n").split("\n"):
        line = raw.rstrip()
        if not line.strip():
            continue
        if line.startswith("//"):
            out.append(line)
            continue
        kv = split(line)
        if not kv:
            out.append("// [无等号，原样保留] " + line)
            continue
        k, v = kv
        if k in known:
            if known[k] == v:
                dups_same += 1
            else:
                conflicts.append((k, known[k], v, name))
            continue
        known[k] = v
        out.append(line)
        added += 1

io.open(target, "w", encoding="utf-8", newline="\n").write("\n".join(out) + "\n")

report = [f"# zh-cn.txt 合并报告 {stamp}", "", f"- 新增 {added} 条；与已有键相同且译文相同、已跳过 {dups_same} 条；同键不同译文 {len(conflicts)} 条（保留 zh-cn.txt 原有译文，旧词典的列在下面）。", ""]
for k, a, b, name in conflicts:
    report.append(f"- `{k}`\n  - 保留（zh-cn.txt）: {a}\n  - 丢弃（{name}）: {b}")
rp = os.path.join(backup_dir, f"zh-cn_merge_report_{stamp}.md")
io.open(rp, "w", encoding="utf-8", newline="\n").write("\n".join(report) + "\n")
print(f"added={added} same_dups={dups_same} conflicts={len(conflicts)}")
print(rp)
