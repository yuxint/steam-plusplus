namespace BD.WTTS.Helpers;

/// <summary>
/// 描点助手类（仅保留空实现，不再上报任何事件）
/// </summary>
static partial class TracepointHelper
{
    /// <param name="name">事件名称</param>
    /// <param name="properties">可选属性</param>
    public static void TrackEvent(string name, IDictionary<string, string>? properties = null)
    {
    }
}
