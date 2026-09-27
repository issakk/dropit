namespace DropLite.Models;

/// <summary>动作类型的中文短标签（列表、气泡、拖放预览共用）。</summary>
internal static class DropActionText
{
    public static string Label(DropAction action) => action switch
    {
        DropAction.Move => "移动",
        DropAction.Copy => "复制",
        DropAction.Delete => "回收",
        DropAction.Compress => "压缩",
        DropAction.Extract => "解压",
        DropAction.Rename => "重命名",
        DropAction.Open => "打开",
        DropAction.Ignore => "忽略",
        _ => action.ToString(),
    };
}
