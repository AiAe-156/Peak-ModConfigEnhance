# -*- coding: utf-8 -*-
r"""把 ModConfigEnhance 打成 Thunderstore 包：dll + manifest.json + README.md + CHANGELOG.md + icon.png → 发布\ModConfigEnhance_<版本>.zip。
版本号从 manifest.json 读；dll 取 bin\Debug 或 bin\Release 里较新的那份（默认 Debug，与 csproj 部署一致）。
用法：python 验证\package_thunderstore.py [--release]
"""
import io, json, os, sys, zipfile, datetime

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
cfg = "Release" if "--release" in sys.argv else "Debug"
dll = os.path.join(ROOT, "bin", cfg, "netstandard2.1", "ModConfigEnhance.dll")
files = ["manifest.json", "README.md", "CHANGELOG.md", "icon.png"]
missing = [f for f in files if not os.path.exists(os.path.join(ROOT, f))]
if not os.path.exists(dll):
    missing.append(dll)
if missing:
    sys.exit("缺文件: " + ", ".join(missing))

manifest = json.load(io.open(os.path.join(ROOT, "manifest.json"), encoding="utf-8-sig"))
version = manifest["version_number"]
out_dir = os.path.join(ROOT, "发布")
os.makedirs(out_dir, exist_ok=True)
out = os.path.join(out_dir, f"ModConfigEnhance_{version}.zip")
if os.path.exists(out):
    sys.exit(f"已存在，不覆盖: {out}")

with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    z.write(dll, "ModConfigEnhance.dll")
    for f in files:
        z.write(os.path.join(ROOT, f), f)

print(f"{out}  ({os.path.getsize(out)} bytes, {cfg} dll, {datetime.datetime.fromtimestamp(os.path.getmtime(dll)):%Y-%m-%d %H:%M})")
with zipfile.ZipFile(out) as z:
    for i in z.infolist():
        print(f"  {i.filename:22s} {i.file_size:>8d}")
