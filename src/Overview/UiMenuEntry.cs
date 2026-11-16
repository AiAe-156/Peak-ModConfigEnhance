#nullable enable
using System;
namespace ModConfigEnhance.Overview;
internal sealed class UiMenuEntry
{
    internal string Label = "", DisabledReason = "";
    // 1.1.0（MCE）：Header / CanInvoke 由调用方用对象初始化器设置，MCE 目前还没有调用方，显式写默认值免掉 CS0649。
    internal bool Header = false, Enabled = true, Checked;
    internal UiActionRole Role = UiActionRole.Neutral;
    internal Action Invoke = () => { };
    internal Func<bool>? CanInvoke = null;
    internal bool Execute()
    {
        if (Header || !Enabled || CanInvoke?.Invoke() == false) return false;
        Invoke(); return true;
    }
}
