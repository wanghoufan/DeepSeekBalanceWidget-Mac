using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using DeepSeekBalanceWidget.Models;
using DeepSeekBalanceWidget.Services;

namespace DeepSeekBalanceWidget;

public enum ToastAlertStyle
{
    Notice,
    Alarm
}

/// <summary>
/// macOS 通知/警报窗口。通知 8 秒后消失；警报持续播放声音并常驻，
/// 或在限时模式下至少显示/播放 10 秒后自动消失。
/// </summary>
public partial class ToastWindow : Window
{
    private const double MinAlarmSeconds = 10;
    private const double NoticeSeconds = 8;
    private const double Margin = 16;
    private const double Gap = 8;
    private static readonly List<ToastWindow> Active = new();

    private readonly ToastAlertStyle _style;
    private readonly bool _sound;
    private readonly bool _persistent;
    private readonly DispatcherTimer _autoCloseTimer;
    private bool _dismissed;
    private bool _closing;
    private DateTime _shownUtc = DateTime.UtcNow;

    public ToastWindow(
        string title,
        string body,
        ToastAlertStyle style,
        bool soundEnabled,
        string? alertMode,
        string? alertPosition,
        string? alertSoundStyle)
    {
        InitializeComponent();
        _style = style;
        _sound = soundEnabled && style == ToastAlertStyle.Alarm;
        _persistent = style == ToastAlertStyle.Alarm
                      && string.Equals(alertMode, "Continuous", StringComparison.OrdinalIgnoreCase);

        TitleText.Text = title;
        BodyText.Text = body;

        bool isRecovery = title.Contains("已恢复", StringComparison.Ordinal);
        TitleText.Foreground = isRecovery
            ? Brush.Parse("#6DDC6D")
            : Brush.Parse("#FFB04D");

        // 恢复通知：绿色环形恢复箭头图标 + 绿色边框，与橙色预警明确区分（用户确认的视觉方案）。
        if (isRecovery)
        {
            IconPath.IsVisible = true;
            IconPath.Stroke = Brush.Parse("#6DDC6D");
            ToastBorder.BorderBrush = Brush.Parse("#6DDC6D");
            ToastBorder.BorderThickness = new Thickness(1.5);
        }

        if (style == ToastAlertStyle.Alarm)
        {
            ToastBorder.BorderBrush = Brush.Parse("#FFB04D");
            ToastBorder.BorderThickness = new Thickness(1.5);
            DismissBtn.IsVisible = true;
        }

        _autoCloseTimer = new DispatcherTimer();
        if (style == ToastAlertStyle.Notice)
        {
            _autoCloseTimer.Interval = TimeSpan.FromSeconds(NoticeSeconds);
            _autoCloseTimer.Tick += (_, _) => FadeOutAndClose();
            _autoCloseTimer.Start();
        }
        else if (!_persistent)
        {
            _autoCloseTimer.Interval = TimeSpan.FromSeconds(MinAlarmSeconds);
            _autoCloseTimer.Tick += (_, _) => FadeOutAndClose();
            _autoCloseTimer.Start();
        }

        // Keep the configured position with the window so that newly opened
        // toasts can use it as the stack anchor.
        PositionHint = alertPosition;
        SoundStyle = string.IsNullOrWhiteSpace(alertSoundStyle) ? "Standard" : alertSoundStyle;

        Opened += Window_Opened;
        Closed += Window_Closed;
        PointerPressed += Window_PointerPressed;
    }

    private string? PositionHint { get; }
    private string SoundStyle { get; }

    private void Window_Opened(object? sender, EventArgs e)
    {
        _shownUtc = DateTime.UtcNow;
        lock (Active)
        {
            Active.Add(this);
            RepositionAll();
        }
        Console.Error.WriteLine($"[Toast] opened pos={Position} bounds={Bounds} screens={Screens is not null} primary={(Screens?.Primary is { } s ? s.WorkingArea.ToString() : "null")}");

        if (_sound) MacAlarmSound.Play(SoundStyle);
        Opacity = 0;
        _ = FadeAsync(0, 1);

        // 兜底：1.5s 后检查淡入结果，若动画未把透明度带到 1 则强制补上（防止窗口不可见）。
        Dispatcher.UIThread.Post(async () =>
        {
            await Task.Delay(1500);
            if (_closing || _dismissed) return;
            Console.Error.WriteLine($"[Toast] opacity@1.5s={Opacity} pos={Position} visible={IsVisible}");
            if (Opacity < 1)
            {
                Opacity = 1;
                Console.Error.WriteLine("[Toast] fade did not reach 1, forced Opacity=1");
            }
        });
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _autoCloseTimer.Stop();
        lock (Active)
        {
            Active.Remove(this);
            RepositionAll();
        }
        StopAlarmSoundIfLast();
    }

    private void Window_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_style == ToastAlertStyle.Alarm && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            Dismiss();
    }

    private void DismissBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Dismiss();

    private void Dismiss()
    {
        if (_dismissed) return;
        _dismissed = true;
        double elapsed = (DateTime.UtcNow - _shownUtc).TotalSeconds;
        if (_style == ToastAlertStyle.Alarm && !_persistent && elapsed < MinAlarmSeconds)
        {
            _autoCloseTimer.Stop();
            _autoCloseTimer.Interval = TimeSpan.FromSeconds(MinAlarmSeconds - elapsed);
            _autoCloseTimer.Start();
            return;
        }
        FadeOutAndClose();
    }

    private void FadeOutAndClose()
    {
        if (_closing) return;
        _closing = true;
        _autoCloseTimer.Stop();
        _ = FadeOutThenCloseAsync();
    }

    private async Task FadeOutThenCloseAsync()
    {
        await FadeAsync(Opacity, 0);
        if (!_closing) return;
        Close();
    }

    private async Task FadeAsync(double from, double to)
    {
        // 不用 Avalonia Animation.RunAsync：KeyFrame 对 Visual.Opacity 的动画在 macOS
        // 透明窗口（TransparencyLevelHint=Transparent）上跑完后，窗口内容始终渲染为
        // 全透明（screencapture -l 报 "could not create image from window"、CG 窗口
        // alpha=1 但屏上无像素）。改为直接属性插值，与兜底强制赋值走同一路径。
        try
        {
            const int steps = 8;
            for (int i = 1; i <= steps; i++)
            {
                Opacity = from + (to - from) * i / steps;
                await Task.Delay(31);
            }
            Opacity = to;
            Console.Error.WriteLine($"[Toast] fade {from:0.##}->{to:0.##} done opacity={Opacity}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Toast] fade {from:0.##}->{to:0.##} failed: {ex}");
            Opacity = to;
        }
    }

    private void StopAlarmSoundIfLast()
    {
        bool alarmActive;
        lock (Active) alarmActive = Active.Any(window => window._sound);
        if (!alarmActive) MacAlarmSound.Stop();
    }

    /// <summary>使用主屏幕工作区，在设置的锚点处堆叠所有活动通知窗口。</summary>
    private static void RepositionAll()
    {
        if (Active.Count == 0) return;
        var screen = Active[0].Screens?.Primary;
        if (screen is null) return;

        // WorkingArea 与 Window.Position 同为逻辑点（实测校准：CG 窗口列表的
        // pos/size 与 Avalonia 数值一致，主屏工作区宽 1920 点）。任何 ×RenderScaling
        // 都会把右上角推到点坐标 3288（屏外），导致窗口整只出屏、屏幕上无像素
        // （screencapture -l 报 "could not create image from window"）。
        PixelRect area = screen.WorkingArea;
        double areaTop = area.Y;
        double areaRight = area.Right;
        double areaBottom = area.Bottom;
        double areaHeight = area.Height;
        var heights = Active.Select(window =>
            window.Bounds.Height > 0 ? window.Bounds.Height : 90).ToArray();
        double width = Active[0].Bounds.Width > 0 ? Active[0].Bounds.Width : 260;
        double totalHeight = heights.Sum() + Gap * Math.Max(0, Active.Count - 1);

        string position = Active[0].PositionHint ?? "TopRight";
        double top = position switch
        {
            "RightCenter" => areaTop + Math.Max(0, (areaHeight - totalHeight) / 2),
            "BottomRight" => areaBottom - Margin - totalHeight,
            _ => areaTop + Margin
        };
        int left = (int)Math.Round(areaRight - width - Margin);
        double cursor = top;
        for (int i = 0; i < Active.Count; i++)
        {
            Active[i].Position = new PixelPoint(left, (int)Math.Round(cursor));
            cursor += heights[i] + Gap;
        }
    }
}
