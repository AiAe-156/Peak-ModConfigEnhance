# -*- coding: utf-8 -*-
r"""扫描说明面板会显示的文本里，哪些含 TMP parseCtrlCharacters 会吞掉的转义序列（\n \r \t \v \\ \uXXXX \UXXXXXXXX）。
用途：核实 2026-09-18 末行重叠根因；一次性脚本，可删。"""
import re, sys, glob, io, os

sys.stdout.reconfigure(encoding="utf-8")
ROOT = r"F:\Games\SteamLibrary\steamapps\common\PEAK"
PAT = re.compile(r"\\(?:[nrtv\\]|u[0-9A-Fa-f]{4}|U[0-9A-Fa-f]{8})")


def unesc(v):
    return v.replace("\\=", "=").replace("\\n", "\n")


print("== 用户语言文件 MCE_TIP_* 词条（Loc.Unescape 之后）==")
for path in glob.glob(os.path.join(ROOT, r"BepInEx\config\ModConfigEnhance\language\*.txt")):
    for ln, line in enumerate(io.open(path, encoding="utf-8-sig"), 1):
        line = line.rstrip("\r\n")
        if not line.startswith("MCE_TIP_"):
            continue
        k, _, v = line.partition("=")
        v = unesc(v)
        hits = PAT.findall(v)
        if hits:
            last = v.split("\n")[-1]
            print(f"{os.path.basename(path)}:{ln} {k}: 命中 {hits} ；在末行={'是' if PAT.search(last) else '否'} -> {last[:80]!r}")

print("\n== 内建 Loc.cs TIP_* 词条 ==")
src = io.open(os.path.join(ROOT, r"MODs\ModConfigEnhance\src\Loc.cs"), encoding="utf-8").read()
for m in re.finditer(r'new\("(TIP_\w+)",\s*((?:"(?:[^"\\]|\\.)*",?\s*)+)\)', src):
    key = m.group(1)
    for s in re.findall(r'"((?:[^"\\]|\\.)*)"', m.group(2)):
        # C# 字面量还原：\\ -> \ ，\n -> 换行，\" -> "
        real = s.replace("\\\\", "\x00").replace("\\n", "\n").replace('\\"', '"').replace("\x00", "\\")
        hits = PAT.findall(real)
        if hits:
            last = real.split("\n")[-1]
            print(f"{key}: 命中 {hits} ；在末行={'是' if PAT.search(last) else '否'} -> {last[:70]!r}")

print("\n== 全部 cfg 的 ## 描述行 ==")
n = 0
for path in glob.glob(os.path.join(ROOT, r"BepInEx\config\*.cfg")):
    for ln, line in enumerate(io.open(path, encoding="utf-8-sig", errors="replace"), 1):
        if line.startswith("##") and PAT.search(line):
            n += 1
            print(f"{os.path.basename(path)}:{ln} {line.strip()[:120]}")
print("cfg 描述命中数:", n)

print("\n== 全部 manifest.json 的 description ==")
n = 0
for path in glob.glob(os.path.join(ROOT, r"BepInEx\plugins\**\manifest.json"), recursive=True):
    txt = io.open(path, encoding="utf-8-sig", errors="replace").read()
    m = re.search(r'"description"\s*:\s*"((?:[^"\\]|\\.)*)"', txt)
    if m:
        real = m.group(1).replace("\\\\", "\x00").replace("\\n", "\n").replace('\\"', '"').replace("\x00", "\\")
        if PAT.search(real):
            n += 1
            print(f"{path[len(ROOT)+1:]}: {real[:100]!r}")
print("manifest 命中数:", n)
