using DeepSeekBalanceWidget.Models;

namespace DeepSeekBalanceWidget.Services;

public static class MacToastService
{
    /// <summary>显示普通通知（8 秒后自动消失，不播放声音）。</summary>
    public static void Show(string title, string body, AppConfig config)
        => Show(title, body, config, ToastAlertStyle.Notice);

    /// <summary>显示通知或警报；警报使用配置的声音、模式和位置。</summary>
    public static void Show(
        string title,
        string body,
        AppConfig config,
        ToastAlertStyle style)
    {
        Console.Error.WriteLine($"[Toast] Show style={style} title={title}");
        var toast = new ToastWindow(
            title,
            body,
            style,
            soundEnabled: config.AlertSoundEnabled,
            alertMode: config.AlertMode,
            alertPosition: config.AlertPosition,
            alertSoundStyle: config.AlertSoundStyle);
        toast.Show();
        Console.Error.WriteLine($"[Toast] shown pos={toast.Position} bounds={toast.Bounds} scaling={toast.RenderScaling} opacity={toast.Opacity} topmost={toast.Topmost}");
    }
}
