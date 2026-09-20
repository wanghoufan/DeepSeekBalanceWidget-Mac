using System.Text;

namespace DeepSeekBalanceWidget.Services;

/// <summary>
/// 把额度预警与恢复事件写入本地日志文件。
/// </summary>
/// <remarks>
/// 弹窗是瞬时的：恢复通知 8 秒自动消失，且经常在无人值守时（深夜自动刷新）触发。
/// 没有日志就无法回溯确认「提醒到底有没有触发过」，只能靠人熬夜守着看。
/// </remarks>
public static class AlertEventLogger
{
    private const long MaxBytesBeforeRotate = 512 * 1024;

    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "Application Support", "DeepSeekBalanceWidget");

    private static readonly object SyncRoot = new();

    /// <summary>日志文件完整路径，便于在界面提示或排障时展示。</summary>
    public static string FilePath { get; } = Path.Combine(DirectoryPath, "alert-events.log");

    /// <summary>恢复事件，例如额度回到 100%。</summary>
    public const string KindRecovery = "RECOVERY";

    /// <summary>低量预警事件。</summary>
    public const string KindAlert = "ALERT";

    /// <summary>额度数据刷新失败，例如读不到登录凭证或接口报错。</summary>
    public const string KindRefreshFailed = "REFRESH-FAILED";

    public static void Write(string kind, string source, string title, string? detail = null)
    {
        string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {kind,-14} | {source} | {title}";
        if (!string.IsNullOrWhiteSpace(detail))
            line += " | " + detail;

        lock (SyncRoot)
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                RotateIfNeeded();
                File.AppendAllText(FilePath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 日志写不出去绝不能影响预警本身。
            }
        }
    }

    private static void RotateIfNeeded()
    {
        try
        {
            var info = new FileInfo(FilePath);
            if (!info.Exists || info.Length < MaxBytesBeforeRotate) return;

            File.Move(FilePath, FilePath + ".1", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 轮转失败就继续追加，不阻塞写入。
        }
    }
}
