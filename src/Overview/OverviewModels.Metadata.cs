#nullable enable
namespace ModConfigEnhance.Overview;

internal sealed partial class OverviewMod
{
    // 1.1.0（MCE）：去掉 SourceKind（来源识别，MCE 不联网）。
    public string HomepageUrl = "";
    public string AuthorSource = "";
    // Prepared package version is deliberately separate from the version loaded this session.
    // 1.1.0（MCE）：MCE 没有更新队列，只剩「同一文件夹里还有版本更高、未加载的候选 DLL」这一种来源。
    public string PreparedVersion = "";
    public string IdentityConflict = "";
    /// <summary>1.1.0（MCE）：manifest 的项目主页优先；没有时用本地 manifest 读出的「Team-Name」拼雷霆商店页地址（只是链接，不联网）。
    /// IGMM 用的是来源识别结果 SourceNamespace / SourceName，MCE 不做来源识别。</summary>
    public string HomeUrl => HomepageUrl.Length > 0 ? HomepageUrl : OverviewCatalogue.StorePageUrl(PackageNamespace, PackageName);
}
