#nullable enable
using System;
using System.IO;
using System.Threading;
using BepInEx;

namespace ModConfigEnhance.Overview;

/// <summary>No directory watchers or recursive background polling. Explicit writes invalidate views;
/// fixed metadata file stamps are checked on demand at most once every two seconds.
/// 1.1.0（MCE）：照抄后裁剪。IGMM 盯四个文件（整合包索引、已应用方案、旧版 state.json、整合包信息），MCE 只有一个 overview.json；
/// 去掉 Environment（整合包索引变化）与 Header（读整合包名称 / 版本给入口用）。
/// Inventory / Content / Configuration 三个计数照旧，第 7、8 章的列表与设置页侧栏分组用它们判断要不要重扫 / 重读。</summary>
internal static class OverviewPageCache
{
    // Unity Mono can implement FileSystemWatcher as a recursive 750ms directory poll.
    // Never instantiate one: watch only fixed metadata files with throttled stat calls.
    private static long _nextProbe;
    private static FileStamp _stateStamp;
    private static string _statePath = "";
    private readonly struct FileStamp : IEquatable<FileStamp>
    {
        private readonly bool _exists;
        private readonly long _length, _written;
        internal FileStamp(string path)
        {
            var file = new FileInfo(path); _exists = file.Exists;
            _length = _exists ? file.Length : 0; _written = _exists ? file.LastWriteTimeUtc.Ticks : 0;
        }
        public bool Equals(FileStamp other) => _exists == other._exists && _length == other._length && _written == other._written;
    }
    private static string _plugins = "", _config = "", _data = "";
    private static int _inventory, _content, _configuration;
    private static bool _started;
    private static volatile bool _reliable;
    internal static int Inventory => Volatile.Read(ref _inventory);
    internal static int Content { get { ProbeMetadata(); return Volatile.Read(ref _content); } }
    internal static int Configuration => Volatile.Read(ref _configuration);
    internal static bool Reliable => _reliable;

    internal static void Initialize()
    {
        if (_started) return; _started = true; _reliable = true;
        _plugins = FullRoot(Paths.BepInExRootPath);
        _config = FullRoot(Paths.ConfigPath);
        _data = FullRoot(Loc.RootDirectory);
        _statePath = Path.Combine(_data, "overview.json");
        ProbeMetadata();
    }
    private static void ProbeMetadata()
    {
        if (!_started) return;
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        long next = Interlocked.Read(ref _nextProbe);
        if (now < next || Interlocked.CompareExchange(ref _nextProbe, now + System.Diagnostics.Stopwatch.Frequency * 2, next) != next) return;
        try
        {
            var state = new FileStamp(_statePath);
            if (!state.Equals(_stateStamp)) { _stateStamp = state; Interlocked.Increment(ref _content); }
            _reliable = true;
        }
        catch (IOException) { _reliable = false; }
        catch (UnauthorizedAccessException) { _reliable = false; }
    }
    private static string FullRoot(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    internal static void ChangedPath(string path)
    {
        Initialize();
        string full = Path.GetFullPath(path);
        string name = Path.GetFileName(full);
        if (full.StartsWith(_data, StringComparison.OrdinalIgnoreCase))
        {
            // 1.1.0（MCE）：个人数据目录里只有 overview.json 影响概览内容（IGMM 是 profiles/、data/runtime/… 那一串）。
            if (full.Equals(_statePath, StringComparison.OrdinalIgnoreCase)) Interlocked.Increment(ref _content);
            return;
        }
        if (full.StartsWith(_config, StringComparison.OrdinalIgnoreCase))
        {
            if (name.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".ini", StringComparison.OrdinalIgnoreCase)) Interlocked.Increment(ref _configuration);
            return;
        }
        if (full.StartsWith(_plugins, StringComparison.OrdinalIgnoreCase))
        {
            if (name.IndexOf(".dll", StringComparison.OrdinalIgnoreCase) >= 0 || name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase) || name.Equals("manifest.json.old", StringComparison.OrdinalIgnoreCase) || !Path.HasExtension(name)) Interlocked.Increment(ref _inventory);
            // 1.1.0（MCE）：IGMM 这里还按整合包资料夹递增 Content（整合包信息变了），MCE 没有整合包，去掉。
        }
    }
    private static long _nextExternalRefresh;
    internal static void OnPageEnter()
    {
        Initialize(); ProbeMetadata();
        // External DLL/config edits are checked by the existing page-entry scan, never
        // by an always-running recursive watcher. Fast settings round-trips keep cache.
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        if (now < _nextExternalRefresh) return;
        _nextExternalRefresh = now + System.Diagnostics.Stopwatch.Frequency * 10;
        Interlocked.Increment(ref _inventory); Interlocked.Increment(ref _configuration);
    }
    internal static void Invalidate() { Interlocked.Increment(ref _inventory); Interlocked.Increment(ref _content); }
    internal static void ConfigurationSaved() { Interlocked.Increment(ref _configuration); Interlocked.Increment(ref _content); }
    internal static void Dispose()
    {
        _nextProbe = 0; _nextExternalRefresh = 0; _reliable = false;
        _stateStamp = default;
        _started = false;
    }
}
