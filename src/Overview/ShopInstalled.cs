#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ModConfigEnhance.Updating;
namespace ModConfigEnhance.Overview;

/// <summary>Display-only evidence. Never confers ownership or permission to mutate a package.
/// 1.1.0（MCE）：只抄基础组件识别要用的纯函数（Valid / Key / Runtime / Files / Manifest）与 Evidence。
/// IGMM 的 Snapshot / Build 读商店安装账本（shop-ledger），MCE 没有商店、不安装任何东西，不搬。</summary>
internal static class ShopInstalled
{
    internal sealed class Evidence
    {
        internal string Key="",Version="",Path="",Area="",Name="";
        // 1.1.0（MCE）：去掉 Disabled / Cached（只有不搬的 Build 填它们）。
    }
    internal static bool Valid(string value)=>Regex.IsMatch(value??"",@"^[A-Za-z0-9_]+$");
    internal static bool Key(string key)=>key.Split('-') is var parts&&parts.Length==2&&parts.All(Valid);
    internal static bool Runtime(string path)=>ModConfigEnhance.Updating.PluginFileNames.IsAssembly(path);
    internal static IEnumerable<string> Files(string root,Action<string>? failure=null)
    {
        var pending=new Stack<string>();pending.Push(root);
        while(pending.Count>0){string folder=pending.Pop();string[] files,dirs;
            try{if(!Directory.Exists(folder)||(File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0)continue;files=Directory.GetFiles(folder);dirs=Directory.GetDirectories(folder);}catch(Exception e) when(e is IOException or UnauthorizedAccessException){failure?.Invoke(e.Message);continue;}
            foreach(var file in files){bool safe=false;try{safe=(File.GetAttributes(file)&FileAttributes.ReparsePoint)==0;}catch(Exception e) when(e is IOException or UnauthorizedAccessException){failure?.Invoke(e.Message);}if(safe)yield return file;}
            foreach(var dir in dirs)if(!new[]{"backup","backups","备份","bin","obj","Demo","验证"}.Contains(System.IO.Path.GetFileName(dir),StringComparer.OrdinalIgnoreCase)&&!System.IO.Path.GetFileName(dir).StartsWith("待安装",StringComparison.OrdinalIgnoreCase))pending.Push(dir);
        }
    }
    internal static Evidence? Manifest(string file,string area,string confirmedKey="")
    {
        try{
            var data=JObject.Parse(File.ReadAllText(file));string name=data.Value<string>("name")??"",team=data.Value<string>("namespace")??data.Value<string>("author")??"",version=data.Value<string>("version_number")??"";
            string folder=System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(file))??"";var parts=folder.Split('-');
            if(team.Length==0&&parts.Length==2&&parts[1]==name)team=parts[0];
            if(team.Length==0&&Key(confirmedKey)&&confirmedKey.Split('-')[1]==name)team=confirmedKey.Split('-')[0];
            if(!Valid(team)||!Valid(name)||!System.Version.TryParse(version,out _))return null;
            string key=team+"-"+name,full=data.Value<string>("FullName")??"";
            if(full.Length>0&&!full.Equals(key,StringComparison.OrdinalIgnoreCase))return null;
            if(parts.Length==2&&parts[1]==name&&!parts[0].Equals(team,StringComparison.OrdinalIgnoreCase))return null;
            return new Evidence{Key=key,Version=version,Path=System.IO.Path.GetDirectoryName(file)!,Area=area};
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or FormatException or InvalidCastException){return null;}
    }
}
