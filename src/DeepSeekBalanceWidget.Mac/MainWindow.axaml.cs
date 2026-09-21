using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using DeepSeekBalanceWidget.Models;
using DeepSeekBalanceWidget.Services;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeepSeekBalanceWidget;

public partial class MainWindow : Window
{
    // Window.Position and Screen.WorkingArea use physical pixels, while
    // Bounds uses logical pixels. Retina displays also need more tolerance
    // because native drag positions are quantized to physical pixels.
    private const double EdgeDetectionThreshold = 24;
    private const double EdgeRevealThickness = 12;
    private const int DeepSeekRefreshSeconds = 30;
    private const int UsageRefreshSeconds = 60;
    private const int NSNormalWindowLevel = 0;
    private const int NSFloatingWindowLevel = 3;

    // NSWindow does not export a C function for setLevel:. Invoke the native
    // Objective-C selector directly so borderless Avalonia windows reliably
    // stay above ordinary application windows on macOS.
    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_setLevel(IntPtr receiver, IntPtr selector, nint level);

    private readonly MacConfigService _configService;
    private readonly AppConfig _config;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _codexTimer;
    private readonly DispatcherTimer _openCodeTimer;
    private readonly DispatcherTimer _openRouterTimer;
    private readonly DispatcherTimer _peakTimer;
    private readonly DispatcherTimer _positionSaveTimer;
    private readonly DispatcherTimer _autoHideTimer;
    private readonly ICodexAccountsUsageProvider _codexProvider = new MacCodexUsageProvider();
    private IOpenCodeUsageProvider _openCodeProvider;
    private IOpenCodeUsageProvider? _openCodeProvider2;
    private IOpenRouterUsageProvider _openRouterProvider;
    private readonly CancellationTokenSource _cancellation = new();
    private IBalanceProvider _provider;
    private MacMenuBarBalance? _menuBarBalance;
    private MacMenuBarBalance? _menuBarBalanceOc2;
    private ParsedBalance? _latestBalance;
    private string _menuBarBalanceText = "¥ --";
    private string _menuBarBalanceTooltip = "DeepSeek 余额监控：正在读取余额";
    private string _menuBarCodexText = "--";
    private string _menuBarCodexTooltip = "ChatGPT Plus：正在读取额度";
    private string _menuBarOpenCodeText = "--";
    private string _menuBarOpenCodeTooltip = "OpenCode Go：正在读取额度";
    private string _menuBarOpenCodeText2 = "--";
    private string _menuBarOpenCodeTooltip2 = "OpenCode Go 账号2：正在读取额度";
    private string _menuBarOpenRouterText = "--";
    private string _menuBarOpenRouterTooltip = "OpenRouter：正在读取额度";
    private string _menuBarPeakText = "谷";
    private decimal? _previousBalance;
    private bool _refreshing;
    private bool _codexRefreshing;
    private bool _openCodeRefreshing;
    private bool _openRouterRefreshing;
    private bool _isMini;
    private bool _isDragging;
    private bool _isEdgeHidden;
    private bool _isSettingsOpen;
    private bool _pointerInside;
    private bool _suppressPositionSave;
    private bool _isRestoringFromDock;
    private DateTime _lastPointerPressUtc;
    private DockEdge _dockEdge;
    private readonly CodexQuotaAlertEvaluator _codexQuotaAlerts = new();
    private readonly OpenCodeQuotaAlertEvaluator _openCodeQuotaAlerts = new();
    private readonly OpenCodeQuotaAlertEvaluator _openCodeQuotaAlerts2 = new();
    private IReadOnlyList<CodexAccountUsageSnapshot> _lastCodexAccounts = Array.Empty<CodexAccountUsageSnapshot>();
    private bool _gptRecoveryFlashActive;
    private readonly DispatcherTimer _gptRecoveryTimer;
    private const double GptRecoveryFlashSeconds = 120;

    // App uses this to keep the close guard active for the full activation
    // callback, including any native close event raised after BringToFront.
    internal bool IsRestoringFromDock
    {
        get => _isRestoringFromDock;
        set => _isRestoringFromDock = value;
    }

    private enum DockEdge { None, Left, Top, Right, Bottom }

    public MainWindow(MacConfigService configService, AppConfig config, IBalanceProvider provider)
    {
        InitializeComponent();
        _configService = configService;
        _config = config;
        _provider = provider;
        _openCodeProvider = new OpenCodeUsageProvider(_configService.GetOpenCodeApiKey());
        _openCodeProvider2 = CreateOpenCodeProvider2();
        _openRouterProvider = new OpenRouterUsageProvider(_configService.GetOpenRouterApiKey());

        ApplyAlwaysOnTop(_config.IsAlwaysOnTop);
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(DeepSeekRefreshSeconds) };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
        _codexTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(UsageRefreshSeconds) };
        _codexTimer.Tick += async (_, _) => await RefreshCodexAsync();
        _openCodeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(UsageRefreshSeconds) };
        _openCodeTimer.Tick += async (_, _) => await RefreshOpenCodeAsync();
        _openRouterTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(UsageRefreshSeconds) };
        _openRouterTimer.Tick += async (_, _) => await RefreshOpenRouterAsync();
        _peakTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(UsageRefreshSeconds) };
        _peakTimer.Tick += (_, _) => RefreshPeakStatus();
        _positionSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _positionSaveTimer.Tick += (_, _) => { _positionSaveTimer.Stop(); SaveWindowPosition(); };
        _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _autoHideTimer.Tick += (_, _) => AutoHideTimerTick();
        _gptRecoveryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(GptRecoveryFlashSeconds) };
        _gptRecoveryTimer.Tick += (_, _) =>
        {
            _gptRecoveryTimer.Stop();
            _gptRecoveryFlashActive = false;
            RefreshGptCapsuleBorder();
        };

        Opened += OnOpened;
        Closing += OnClosing;
        PositionChanged += (_, _) => SchedulePositionSave();
        PointerPressed += Window_PointerPressed;
        PointerEntered += (_, _) =>
        {
            _pointerInside = true;
            if (_config.EnableEdgeAutoHide) _autoHideTimer.Start();
            if (_isEdgeHidden) SetDockPosition(hidden: false);
        };
        PointerExited += (_, _) => _pointerInside = false;
        PointerMoved += Window_PointerMoved;

        ApplyMiniMode(_config.UseMiniMode);
        ApplyMonitoringVisibility();
        UpdateAlwaysOnTopButton();
        UpdateEdgeAutoHideButton();
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        Debug.WriteLine($"[DockLifecycle] OnOpened entry visible={IsVisible} position={Position} restoring={_isRestoringFromDock}");
        Console.Error.WriteLine($"[DockLifecycle] OnOpened entry visible={IsVisible} position={Position} restoring={_isRestoringFromDock}");
        // The native NSWindow handle is normally unavailable in the
        // constructor, so reapply the configured level once it is opened.
        ApplyNativeWindowLevel(_config.IsAlwaysOnTop);
        RestorePosition();
        // 先创建 OC2 状态项再创建主项：macOS 把后创建的状态项插到左边，
        // 这样主项在左、OC2 在右，两段数据挨在一起。
        _menuBarBalanceOc2 ??= MacMenuBarBalance.Create(RestoreAndActivate);
        _menuBarBalance ??= MacMenuBarBalance.Create(RestoreAndActivate);
        RefreshMenuBar();
        RefreshPeakStatus();
        if (_config.EnableDeepSeekMonitoring) _refreshTimer.Start();
        _peakTimer.Start();
        if (_config.EnableCodexMonitoring) _codexTimer.Start();
        if (_config.EnableOpenCodeMonitoring) _openCodeTimer.Start();
        if (_config.EnableOpenRouterMonitoring) _openRouterTimer.Start();
        if (_config.EnableDeepSeekMonitoring) _ = RefreshAsync();
        if (_config.EnableCodexMonitoring) _ = RefreshCodexAsync();
        if (_config.EnableOpenCodeMonitoring) _ = RefreshOpenCodeAsync();
        if (_config.EnableOpenRouterMonitoring) _ = RefreshOpenRouterAsync();
        if (!_isRestoringFromDock) _autoHideTimer.Start();
        Debug.WriteLine($"[DockLifecycle] OnOpened exit visible={IsVisible} position={Position} restoring={_isRestoringFromDock}");
        Console.Error.WriteLine($"[DockLifecycle] OnOpened exit visible={IsVisible} position={Position} restoring={_isRestoringFromDock}");
    }

    private void RestorePosition()
    {
        if (_config.WindowLeft is double left && _config.WindowTop is double top)
            SetPosition(left, top);
        else
            SetPosition(WorkArea().Right - WindowWidth() - 24, WorkArea().Bottom - WindowHeight() - 24);
        ClampToWorkArea();
    }

    private PixelRect WorkArea()
    {
        // Primary is not necessarily the display currently containing the
        // window. ScreenFromWindow keeps edge docking correct on multi-monitor
        // Mac setups as well.
        var area = Screens.ScreenFromWindow(this)?.WorkingArea
            ?? Screens.Primary?.WorkingArea
            ?? new PixelRect(0, 0, 1920, 1080);
        Debug.WriteLine($"[EdgeAutoHide] WorkArea={area} Position={Position} RenderScaling={RenderScaling:0.##}");
        return area;
    }

    private double WindowScale() => RenderScaling > 0 ? RenderScaling : 1;
    private int WindowWidth() => Math.Max(1, (int)Math.Round(Bounds.Width * WindowScale()));
    private int WindowHeight() => Math.Max(1, (int)Math.Round(Bounds.Height * WindowScale()));

    private void SetPosition(double left, double top)
    {
        _suppressPositionSave = true;
        try { Position = new PixelPoint((int)Math.Round(left), (int)Math.Round(top)); }
        finally { _suppressPositionSave = false; }
    }

    private void ClampToWorkArea()
    {
        var area = WorkArea();
        SetPosition(
            Math.Clamp(Position.X, area.X, Math.Max(area.X, area.Right - WindowWidth())),
            Math.Clamp(Position.Y, area.Y, Math.Max(area.Y, area.Bottom - WindowHeight())));
    }

    private void SchedulePositionSave()
    {
        if (_suppressPositionSave) return;
        _positionSaveTimer.Stop();
        _positionSaveTimer.Start();
    }

    private void SaveWindowPosition()
    {
        PixelPoint position = _isEdgeHidden ? VisibleDockPosition() : Position;
        _config.WindowLeft = position.X;
        _config.WindowTop = position.Y;
        _configService.Save(_config);
    }

    private void ApplyMiniMode(bool mini)
    {
        _isMini = mini;
        Card.IsVisible = !mini;
        MiniCard.IsVisible = mini;
        // The detailed-card refresh action is not part of the capsule. Keep it
        // collapsed in mini mode so it can never reserve layout space.
        RefreshButton.IsVisible = !mini;
        // Re-enable content sizing after a mode switch so the visible Card/MiniCard
        // determines the native window bounds on Avalonia.
        SizeToContent = SizeToContent.WidthAndHeight;
        // MiniCard has no fixed Width in XAML. Reset it explicitly as well so a
        // previous measured size can never constrain the content-driven layout.
        if (mini) MiniCard.Width = double.NaN;
        RearrangeMiniBlocks();
        Dispatcher.UIThread.Post(() =>
        {
            if (_dockEdge != DockEdge.None) SetDockPosition(_isEdgeHidden);
            else ClampToWorkArea();
        });
    }

    private void RearrangeMiniBlocks()
    {
        var blocks = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase)
        {
            ["deepseek"] = MiniDeepSeekBlock,
            ["chatgpt"] = MiniGptBlock,
            ["opencode"] = MiniOpenCodeBlock,
            ["openrouter"] = MiniOpenRouterBlock,
            ["workbuddy"] = MiniWorkbuddyBlock
        };
        var order = (_config.AgentOrder ?? new List<string>())
            .Concat(new[] { "deepseek", "chatgpt", "opencode", "openrouter", "workbuddy" })
            .Distinct(StringComparer.OrdinalIgnoreCase);
        int index = 0;
        foreach (string key in order)
        {
            if (!blocks.TryGetValue(key, out var block) || !block.IsVisible) continue;
            MiniContentPanel.Children.Remove(block);
            MiniContentPanel.Children.Insert(index++, block);
        }
    }

    private void ApplyMonitoringVisibility()
    {
        BalancePanel.IsVisible = _config.EnableDeepSeekMonitoring;
        CodexPanel.IsVisible = _config.EnableCodexMonitoring;
        OpenCodePanel.IsVisible = _config.EnableOpenCodeMonitoring;
        OpenRouterPanel.IsVisible = _config.EnableOpenRouterMonitoring;
        WorkbuddyPanel.IsVisible = _config.EnableWorkbuddyMonitoring;
        MiniDeepSeekBlock.IsVisible = _config.EnableDeepSeekMonitoring;
        MiniGptBlock.IsVisible = _config.EnableCodexMonitoring;
        MiniOpenCodeBlock.IsVisible = _config.EnableOpenCodeMonitoring;
        MiniOpenRouterBlock.IsVisible = _config.EnableOpenRouterMonitoring;
        MiniWorkbuddyBlock.IsVisible = _config.EnableWorkbuddyMonitoring;
        _refreshTimer.IsEnabled = _config.EnableDeepSeekMonitoring;
        _codexTimer.IsEnabled = _config.EnableCodexMonitoring;
        _openCodeTimer.IsEnabled = _config.EnableOpenCodeMonitoring;
        _openRouterTimer.IsEnabled = _config.EnableOpenRouterMonitoring;
        RearrangeMiniBlocks();
    }

    private async void Refresh_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_config.EnableDeepSeekMonitoring) await RefreshAsync();
        if (_config.EnableCodexMonitoring) await RefreshCodexAsync();
        if (_config.EnableOpenCodeMonitoring) await RefreshOpenCodeAsync();
        if (_config.EnableOpenRouterMonitoring) await RefreshOpenRouterAsync();
    }

    private async void Settings_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isEdgeHidden) SetDockPosition(hidden: false);
        _isSettingsOpen = true;
        bool saved;
        try { saved = await new SettingsWindow(_configService, _config, ApplySettingsImmediately).ShowDialog<bool>(this); }
        finally { _isSettingsOpen = false; }
        if (!saved) return;

        ApplySettingsImmediately();
        if (_provider is DeepSeekApiClient)
            _provider = new DeepSeekApiClient(_configService.GetApiKey() ?? string.Empty);
        if (_openCodeProvider is IDisposable disposable) disposable.Dispose();
        _openCodeProvider = new OpenCodeUsageProvider(_configService.GetOpenCodeApiKey());
        if (_openCodeProvider2 is IDisposable disposableOc2) disposableOc2.Dispose();
        _openCodeProvider2 = CreateOpenCodeProvider2();
        if (_openRouterProvider is IDisposable disposableOr) disposableOr.Dispose();
        _openRouterProvider = new OpenRouterUsageProvider(_configService.GetOpenRouterApiKey());
        RefreshPeakStatus();
        if (_config.EnableDeepSeekMonitoring) await RefreshAsync();
        if (_config.EnableCodexMonitoring) await RefreshCodexAsync();
        if (_config.EnableOpenCodeMonitoring) await RefreshOpenCodeAsync();
        if (_config.EnableOpenRouterMonitoring) await RefreshOpenRouterAsync();
    }

    private void ApplySettingsImmediately()
    {
        ApplyAlwaysOnTop(_config.IsAlwaysOnTop);
        ApplyMiniMode(_config.UseMiniMode);
        ApplyMonitoringVisibility();
        UpdateAlwaysOnTopButton();
        RefreshPeakStatus();
    }

    private void AlwaysOnTop_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ApplyAlwaysOnTop(!Topmost);
        _config.IsAlwaysOnTop = Topmost;
        _configService.Save(_config);
        UpdateAlwaysOnTopButton();
    }

    private IntPtr NativeHandle => TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

    private void ApplyAlwaysOnTop(bool topmost)
    {
        Topmost = topmost;
        if (!OperatingSystem.IsMacOS()) return;

        // Keep Avalonia's state synchronized for bindings and other platform
        // implementations, then enforce the native level on macOS.
        this.SetCurrentValue(TopmostProperty, topmost);
        ApplyNativeWindowLevel(topmost);
    }

    private void ApplyNativeWindowLevel(bool topmost)
    {
        if (!OperatingSystem.IsMacOS()) return;

        IntPtr nativeHandle = NativeHandle;
        if (nativeHandle == IntPtr.Zero) return;

        IntPtr setLevelSelector = sel_registerName("setLevel:");
        objc_msgSend_setLevel(
            nativeHandle,
            setLevelSelector,
            topmost ? NSFloatingWindowLevel : NSNormalWindowLevel);
    }

    private void MiniMode_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ApplyMiniMode(true);
        _config.UseMiniMode = true;
        _configService.Save(_config);
    }

    private void UpdateAlwaysOnTopButton()
    {
        string text = Topmost ? "置顶✓" : "置顶";
        AlwaysOnTopBtn.Content = text;
        CardPinBtn.Content = text;
        CardMiniBtn.Content = _isMini ? "展开" : "迷你";
    }

    private async Task RefreshAsync()
    {
        if (!_config.EnableDeepSeekMonitoring || _refreshing) return;
        _refreshing = true;
        try
        {
            ErrorText.Text = string.Empty;
            var parsed = BalanceParser.Parse(await _provider.GetBalanceJsonAsync(_cancellation.Token));
            if (!parsed.Success) throw new InvalidOperationException(parsed.Error);
            var selected = CurrencySelector.Select(parsed.Balances, _config.SelectedCurrency);
            if (!selected.Found || selected.Balance is null)
                throw new InvalidOperationException($"未找到 {_config.SelectedCurrency} 余额");

            var balance = selected.Balance;
            decimal? change = BalanceChangeCalculator.Change(_previousBalance, balance.Total);
            _previousBalance = balance.Total;
            _latestBalance = balance;
            string amount = Symbol(balance.Currency) + balance.Total.ToString("0.00");
            BalanceText.Text = amount;
            MiniBalanceText.Text = amount;
            ChangeText.Text = change is null ? "首次刷新" : $"变动 {(change >= 0 ? "+" : string.Empty)}{change:0.00}";
            MiniChangeText.Text = change is null ? string.Empty : $"{(change >= 0 ? "+" : string.Empty)}{change:0.00}";
            BreakdownText.Text = $"充值 {balance.ToppedUp:0.00} · 赠送 {balance.Granted:0.00}";
            StatusDot.Foreground = ThemeBrush(balance.IsAvailable ? "SuccessBrush" : "ErrorBrush");
            StatusText.Text = balance.IsAvailable ? "账户正常" : "账户不可用";
            UpdateRefreshTime();
            UpdateMenuBar(amount, $"DeepSeek 余额：{balance.Total:0.00} {balance.Currency}\n上次更新：{DateTime.Now:HH:mm:ss}");
            _config.LastSuccessfulBalance = balance.Total;
            _config.LastSuccessfulRefreshUtc = DateTimeOffset.UtcNow;
            _configService.Save(_config);
        }
        catch (OperationCanceledException) { }
        catch (ApiException ex) { ShowError(ex.IsAuthFailure ? "认证失败：请在设置中检查 API Key" : ex.Message); }
        catch (Exception ex) { ShowError("刷新失败：" + ex.Message); }
        finally { _refreshing = false; }
    }

    private async Task RefreshCodexAsync()
    {
        if (!_config.EnableCodexMonitoring || _codexRefreshing) return;
        _codexRefreshing = true;
        try
        {
            var accounts = await _codexProvider.GetUsagesAsync(_cancellation.Token);
            ApplyCodexUsages(accounts);
            RaiseCodexQuotaAlerts(accounts);
            _menuBarCodexText = FormatMenuBarCodex(accounts);
            _menuBarCodexTooltip = accounts.Count == 0 ? "未找到可用的 CC Switch 账号" : string.Join(Environment.NewLine, accounts.Take(2).Select(FormatAccount));
            UpdateRefreshTime();
            RefreshMenuBar();
        }
        catch (OperationCanceledException) { }
        catch
        {
            CodexText.Text = "暂时无法读取 ChatGPT Plus 用量";
            ClearMiniGptRows();
            _menuBarCodexText = "--";
            _menuBarCodexTooltip = "ChatGPT Plus：暂时无法读取额度";
            UpdateRefreshTime();
            RefreshMenuBar();
        }
        finally { _codexRefreshing = false; }
    }

    private void ApplyCodexUsages(IReadOnlyList<CodexAccountUsageSnapshot> usages)
    {
        _lastCodexAccounts = usages;
        var accounts = usages.Take(2).ToArray();
        CodexText.Text = accounts.Length == 0
            ? "未找到可用的 CC Switch 账号"
            : string.Join(Environment.NewLine, accounts.Select(FormatAccount));
        ClearMiniGptRows();
        if (accounts.Length > 0) ApplyCodexAccount(accounts[0], MiniGptA1Label, MiniGptA1Five, MiniGptA1FiveCd, MiniGptA1Weekly, MiniGptA1WeeklyCd);
        if (accounts.Length > 1) ApplyCodexAccount(accounts[1], MiniGptA2Label, MiniGptA2Five, MiniGptA2FiveCd, MiniGptA2Weekly, MiniGptA2WeeklyCd);
        RefreshGptCapsuleBorder();
    }

    private static void ApplyCodexAccount(CodexAccountUsageSnapshot account, TextBlock label, TextBlock five, TextBlock fiveCd, TextBlock weekly, TextBlock weeklyCd)
    {
        label.Text = account.MiniLabel;
        if (!account.Usage.IsAvailable || account.Usage.Windows.Count == 0) return;
        var windows = account.Usage.Windows.OrderBy(w => w.DurationMinutes ?? int.MaxValue).ToArray();
        var now = DateTimeOffset.Now;
        var first = windows[0];
        five.Text = $"{first.RemainingPercent}%";
        fiveCd.Text = CodexUsageFormatter.FormatCountdownShort(first.ResetsAt, now);
        if (windows.Length > 1)
        {
            weekly.Text = $"{windows[1].RemainingPercent}%";
            weeklyCd.Text = CodexUsageFormatter.FormatCountdownShort(windows[1].ResetsAt, now);
        }
    }

    private void ClearMiniGptRows()
    {
        foreach (var text in new[] { MiniGptA1Label, MiniGptA1Five, MiniGptA1FiveCd, MiniGptA1Weekly, MiniGptA1WeeklyCd,
            MiniGptA2Label, MiniGptA2Five, MiniGptA2FiveCd, MiniGptA2Weekly, MiniGptA2WeeklyCd }) text.Text = "--";
    }

    /// <summary>
    /// 评估 ChatGPT 额度预警并弹窗：低量走警报（橙色常驻弹窗 + 警报声），
    /// 恢复走普通通知（绿色标题、自动消失），并驱动胶囊 GPT 区块边框状态色。
    /// </summary>
    private void RaiseCodexQuotaAlerts(IReadOnlyList<CodexAccountUsageSnapshot> accounts)
    {
        bool toastsEnabled = _config.ShowToastNotifications;
        foreach (var alert in _codexQuotaAlerts.Evaluate(accounts, _config, DateTimeOffset.Now))
        {
            // 日志必须写在弹窗开关判断之前：用户关掉弹窗后仍要留下记录，
            // 否则无法回溯确认「提醒到底触发过没有」。
            if (alert.IsRecovery)
            {
                StartGptRecoveryFlash();
                AlertEventLogger.Write(
                    AlertEventLogger.KindRecovery,
                    "GPT",
                    $"{ShortAccountName(alert.Email)} · {alert.WindowLabel}已恢复",
                    $"剩余 {alert.RemainingPercent}%（恢复线 {_config.GptQuotaRecoveredPercent}%）");
                if (!toastsEnabled) continue;
                MacToastService.Show(
                    $"{ShortAccountName(alert.Email)} · {alert.WindowLabel}已恢复",
                    $"剩余额度已回到 {alert.RemainingPercent}%", _config);
                continue;
            }

            string resetHint = alert.ResetsAt is DateTimeOffset resetsAt
                ? $"预计 {resetsAt.ToLocalTime():MM-dd HH:mm} 恢复"
                : "恢复时间未知";
            AlertEventLogger.Write(
                AlertEventLogger.KindAlert,
                "GPT",
                $"{ShortAccountName(alert.Email)} · {alert.WindowLabel}仅剩 {alert.RemainingPercent}%",
                resetHint);
            if (!toastsEnabled) continue;
            MacToastService.Show(
                $"{ShortAccountName(alert.Email)} · {alert.WindowLabel}仅剩 {alert.RemainingPercent}%",
                resetHint, _config, ToastAlertStyle.Alarm);
        }
        RefreshGptCapsuleBorder();
    }

    /// <summary>
    /// 评估 OpenCode 额度预警：只播报低量预警，不做恢复提醒（消耗量小，无需打扰）。
    /// </summary>
    private void RaiseOpenCodeQuotaAlerts(OpenCodeUsageSnapshot snapshot,
        OpenCodeQuotaAlertEvaluator? evaluator = null, string accountPrefix = "")
    {
        foreach (var alert in (evaluator ?? _openCodeQuotaAlerts).Evaluate(snapshot, _config, DateTimeOffset.Now))
        {
            if (alert.IsRecovery) continue;
            if (!_config.ShowToastNotifications) continue;
            string usedHint = alert.EstimatedUsedUsd.HasValue
                ? $"（已用 ≈ ${alert.EstimatedUsedUsd.Value:0.##}）"
                : string.Empty;
            string resetHint = alert.ResetsAt is DateTimeOffset resetsAt
                ? $"预计 {resetsAt.ToLocalTime():MM-dd HH:mm} 恢复"
                : "恢复时间未知";
            MacToastService.Show(
                $"{accountPrefix}{alert.WindowLabel}仅剩 {alert.RemainingPercent}%",
                $"{usedHint}{resetHint}", _config, ToastAlertStyle.Alarm);
        }
    }

    private static string ShortAccountName(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "----";
        string local = email.Split('@')[0];
        return string.IsNullOrWhiteSpace(local) ? "----" : local;
    }

    /// <summary>恢复提醒触发后，胶囊 GPT 区块边框高亮绿色 2 分钟，到期后按数据重算。</summary>
    private void StartGptRecoveryFlash()
    {
        _gptRecoveryFlashActive = true;
        MiniGptBlock.BorderBrush = ThemeBrush("SuccessBrush");
        MiniGptBlock.BorderThickness = new Thickness(1.5);
        _gptRecoveryTimer.Stop();
        _gptRecoveryTimer.Start();
    }

    /// <summary>
    /// 按当前额度数据刷新胶囊 GPT 区块边框：
    /// 恢复高亮期内保持绿色；任一启用窗口剩余 ≤ 最高档位 → 橙色（≤ 最低档位 → 红色）；
    /// 其余情况恢复默认边框。
    /// </summary>
    private void RefreshGptCapsuleBorder()
    {
        if (_gptRecoveryFlashActive)
        {
            MiniGptBlock.BorderBrush = ThemeBrush("SuccessBrush");
            MiniGptBlock.BorderThickness = new Thickness(1.5);
            return;
        }

        var thresholds = (_config.GptQuotaAlertThresholds ?? new List<int>())
            .Where(t => t > 0 && t < 100)
            .Distinct()
            .OrderByDescending(t => t)
            .ToArray();
        if (thresholds.Length > 0)
        {
            int highest = thresholds[0];
            int lowest = thresholds[^1];
            int minRemaining = _lastCodexAccounts
                .Where(account => account.Usage is { IsAvailable: true })
                .SelectMany(account => account.Usage.Windows)
                .Where(IsGptWindowWarned)
                .Select(window => window.RemainingPercent)
                .DefaultIfEmpty(101)
                .Min();
            if (minRemaining <= highest)
            {
                MiniGptBlock.BorderBrush = ThemeBrush(minRemaining <= lowest ? "ErrorBrush" : "WarningBrush");
                MiniGptBlock.BorderThickness = new Thickness(1.5);
                return;
            }
        }

        MiniGptBlock.BorderBrush = ThemeBrush("BorderBrush");
        MiniGptBlock.BorderThickness = new Thickness(1);
    }

    /// <summary>5 小时窗口始终参与边框预警；周窗口仅在周额度预警开启时参与。</summary>
    private bool IsGptWindowWarned(CodexUsageWindow window)
        => (window.DurationMinutes ?? 300) <= 360 || _config.GptWeeklyAlertEnabled;

    private async Task RefreshOpenCodeAsync()
    {
        if (!_config.EnableOpenCodeMonitoring || _openCodeRefreshing) return;
        _openCodeRefreshing = true;
        try
        {
            ApplyOpenCodeUsage(await _openCodeProvider.GetUsageAsync(_cancellation.Token));
            if (_openCodeProvider2 is { } provider2)
                ApplyOpenCodeUsage2(await provider2.GetUsageAsync(_cancellation.Token));
            else
                ClearOpenCodeAccount2();
            UpdateRefreshTime();
            RefreshMenuBar();
        }
        catch (OperationCanceledException) { }
        catch
        {
            ApplyOpenCodeUsage(OpenCodeUsageSnapshot.Unavailable("刷新失败"));
            ClearOpenCodeAccount2();
            UpdateRefreshTime();
            RefreshMenuBar();
        }
        finally { _openCodeRefreshing = false; }
    }

    /// <summary>第二账号 Provider：仅在用户配置了账号2 Key 时存在。</summary>
    private IOpenCodeUsageProvider? CreateOpenCodeProvider2()
    {
        string? key2 = _configService.GetOpenCodeApiKey2();
        return string.IsNullOrWhiteSpace(key2) ? null : new OpenCodeUsageProvider(key2);
    }

    private void ApplyOpenCodeUsage2(OpenCodeUsageSnapshot snapshot)
    {
        MiniOpenCodeCard2.IsVisible = true;
        var byKind = snapshot.Windows.ToDictionary(window => window.Kind);
        if (!snapshot.IsAvailable)
        {
            MiniOc2Label.Foreground = ThemeBrush("WarningBrush");
            ApplyOpenCodeRow(null, MiniOc2FivePct, MiniOc2FiveCd, MiniOc2FiveBarFill);
            ApplyOpenCodeRow(null, MiniOc2WeeklyPct, MiniOc2WeeklyCd, MiniOc2WeeklyBarFill);
            ApplyOpenCodeRow(null, MiniOc2MonthlyPct, MiniOc2MonthlyCd, MiniOc2MonthlyBarFill, monthly: true);
            _menuBarOpenCodeText2 = "--";
            _menuBarOpenCodeTooltip2 = "OpenCode Go 账号2：" + (snapshot.Error ?? "暂不可用");
            RefreshMenuBar();
            return;
        }

        MiniOc2Label.Foreground = ThemeBrush("TextMainBrush");
        ApplyOpenCodeRow(byKind.GetValueOrDefault("rolling"), MiniOc2FivePct, MiniOc2FiveCd, MiniOc2FiveBarFill);
        ApplyOpenCodeRow(byKind.GetValueOrDefault("weekly"), MiniOc2WeeklyPct, MiniOc2WeeklyCd, MiniOc2WeeklyBarFill);
        ApplyOpenCodeRow(byKind.GetValueOrDefault("monthly"), MiniOc2MonthlyPct, MiniOc2MonthlyCd, MiniOc2MonthlyBarFill, monthly: true);
        RaiseOpenCodeQuotaAlerts(snapshot, _openCodeQuotaAlerts2, "OpenCode 账号2 · ");

        // 详情卡追加账号2 的行：与账号1 之间空一行分组。
        string detail = string.Join(Environment.NewLine, snapshot.Windows.Select(window =>
            $"账号2 {OpenCodeUsageFormatter.ShortLabelOf(window.Kind)}：剩余 {window.RemainingPercent}% · 恢复 {OpenCodeUsageFormatter.FormatCountdown(window, DateTimeOffset.Now)}"));
        OpenCodeText.Text = (OpenCodeText.Text ?? string.Empty)
            + Environment.NewLine + Environment.NewLine + detail;
        _menuBarOpenCodeText2 = snapshot.Windows.Count == 0
            ? "--"
            : "OC2 " + string.Join("/", snapshot.Windows.Select(w => w.RemainingPercent)) + "%";
        _menuBarOpenCodeTooltip2 = detail;
        RefreshMenuBar();
    }

    private void ClearOpenCodeAccount2()
    {
        MiniOpenCodeCard2.IsVisible = false;
    }

    private void ApplyOpenCodeUsage(OpenCodeUsageSnapshot snapshot)
    {
        if (!snapshot.IsAvailable)
        {
            string reason = snapshot.Error ?? "暂不可用";
            OpenCodeTitleText.Text = $"OpenCode Go 额度（{reason}）";
            OpenCodeText.Text = reason;
            MiniOcLabel.Foreground = ThemeBrush("WarningBrush");
            ClearOpenCodeRows();
            _menuBarOpenCodeText = "--";
            _menuBarOpenCodeTooltip = "OpenCode Go：" + reason;
            return;
        }

        OpenCodeTitleText.Text = "OpenCode Go 额度";
        string prefix = _openCodeProvider2 is not null ? "账号1 " : string.Empty;
        OpenCodeText.Text = string.Join(Environment.NewLine, snapshot.Windows.Select(window =>
            $"{prefix}{OpenCodeUsageFormatter.ShortLabelOf(window.Kind)}：剩余 {window.RemainingPercent}% · 恢复 {OpenCodeUsageFormatter.FormatCountdown(window, DateTimeOffset.Now)}"));
        MiniOcLabel.Foreground = ThemeBrush("TextMainBrush");
        var byKind = snapshot.Windows.ToDictionary(window => window.Kind);
        ApplyOpenCodeRow(byKind.GetValueOrDefault("rolling"), MiniOcFivePct, MiniOcFiveCd, MiniOcFiveBarFill);
        ApplyOpenCodeRow(byKind.GetValueOrDefault("weekly"), MiniOcWeeklyPct, MiniOcWeeklyCd, MiniOcWeeklyBarFill);
        ApplyOpenCodeRow(byKind.GetValueOrDefault("monthly"), MiniOcMonthlyPct, MiniOcMonthlyCd, MiniOcMonthlyBarFill, monthly: true);
        _menuBarOpenCodeText = snapshot.Windows.Count == 0 ? "--" : string.Join("/", snapshot.Windows.Select(w => w.RemainingPercent)) + "%";
        _menuBarOpenCodeTooltip = OpenCodeText.Text ?? string.Empty;
        RaiseOpenCodeQuotaAlerts(snapshot);
    }

    private static void ApplyOpenCodeRow(OpenCodeUsageWindow? window, TextBlock pct, TextBlock countdown, Border barFill, bool monthly = false)
    {
        if (window is null)
        {
            pct.Text = countdown.Text = "--";
            barFill.Width = 0;
            return;
        }
        pct.Text = $"{window.RemainingPercent}%";
        countdown.Text = monthly
            ? OpenCodeUsageFormatter.FormatMonthlyDays(window, DateTimeOffset.Now)
            : OpenCodeUsageFormatter.FormatCountdownShort(window, DateTimeOffset.Now);
        const double maximum = 100d;
        var value = Math.Clamp(window.RemainingPercent, 0, maximum);
        barFill.Width = value / maximum * 34d;
        var color = value < 20
            ? Color.FromRgb(0xE2, 0x4B, 0x4A)
            : value <= 70
                ? Color.FromRgb(0xEF, 0x9F, 0x27)
                : Color.FromRgb(0x78, 0xD7, 0x9A);
        barFill.Background = new SolidColorBrush(color);
    }

    private void ClearOpenCodeRows()
    {
        ApplyOpenCodeRow(null, MiniOcFivePct, MiniOcFiveCd, MiniOcFiveBarFill);
        ApplyOpenCodeRow(null, MiniOcWeeklyPct, MiniOcWeeklyCd, MiniOcWeeklyBarFill);
        ApplyOpenCodeRow(null, MiniOcMonthlyPct, MiniOcMonthlyCd, MiniOcMonthlyBarFill, monthly: true);
    }

    private async Task RefreshOpenRouterAsync()
    {
        if (!_config.EnableOpenRouterMonitoring || _openRouterRefreshing) return;
        _openRouterRefreshing = true;
        try
        {
            ApplyOpenRouterUsage(await _openRouterProvider.GetUsageAsync(_cancellation.Token));
            UpdateRefreshTime();
            RefreshMenuBar();
        }
        catch (OperationCanceledException) { }
        catch
        {
            ApplyOpenRouterUsage(OpenRouterUsageSnapshot.Unavailable("刷新失败"));
            UpdateRefreshTime();
            RefreshMenuBar();
        }
        finally { _openRouterRefreshing = false; }
    }

    private void ApplyOpenRouterUsage(OpenRouterUsageSnapshot snapshot)
    {
        if (!snapshot.IsAvailable)
        {
            string reason = snapshot.Error ?? "暂不可用";
            OpenRouterTitleText.Text = $"OpenRouter 额度（{reason}）";
            OpenRouterText.Text = reason;
            MiniOrLabel.Foreground = ThemeBrush("WarningBrush");
            ClearOpenRouterRows();
            _menuBarOpenRouterText = "--";
            _menuBarOpenRouterTooltip = "OpenRouter：" + reason;
            return;
        }

        OpenRouterTitleText.Text = "OpenRouter 额度";
        OpenRouterText.Text = $"{OpenRouterUsageFormatter.FormatRemaining(snapshot)} · 剩余 {OpenRouterUsageFormatter.FormatRemainingPercent(snapshot)}";
        MiniOrLabel.Foreground = ThemeBrush("TextMainBrush");
        MiniOrPct.Text = $"{snapshot.RemainingPercent:0.#}%";
        MiniOrRemaining.Text = $"${snapshot.RemainingCreditsUsd:0.##}";
        double pct = (double)Math.Clamp(snapshot.RemainingPercent, 0m, 100m);
        MiniOrBarFill.Width = pct / 100d * 34d;
        var color = pct < 20
            ? Color.FromRgb(0xE2, 0x4B, 0x4A)
            : pct <= 70
                ? Color.FromRgb(0xEF, 0x9F, 0x27)
                : Color.FromRgb(0x78, 0xD7, 0x9A);
        MiniOrBarFill.Background = new SolidColorBrush(color);
        _menuBarOpenRouterText = $"{snapshot.RemainingPercent:0.#}%";
        _menuBarOpenRouterTooltip = $"{OpenRouterUsageFormatter.FormatRemaining(snapshot)} · {OpenRouterUsageFormatter.FormatRemainingPercent(snapshot)}";
    }

    private void ClearOpenRouterRows()
    {
        MiniOrPct.Text = MiniOrRemaining.Text = "--";
        MiniOrBarFill.Width = 0;
    }

    private void RefreshPeakStatus()
    {
        bool isPeak = PeakHourCalculator.IsPeak(DateTime.Now, _config.PeakHourRanges);
        bool visible = _config.EnableDeepSeekMonitoring && _config.ShowPeakIndicator;
        PeakText.IsVisible = visible;
        PeakText.Text = isPeak ? "● 预计高峰时段" : "● 预计非高峰时段";
        PeakText.Foreground = ThemeBrush(isPeak ? "PeakWarningBrush" : "PeakSuccessBrush");
        MiniPeakDot.IsVisible = visible;
        MiniPeakLabel.IsVisible = visible;
        MiniPeakDot.Foreground = ThemeBrush(isPeak ? "PeakWarningBrush" : "PeakSuccessBrush");
        MiniPeakLabel.Text = isPeak ? "高峰" : "非高峰";
        _menuBarPeakText = isPeak ? "峰" : "谷";
        RefreshMenuBar();
    }

    private void UpdateRefreshTime()
    {
        string value = DateTime.Now.ToString("HH:mm:ss");
        RefreshTimeText.Text = "上次刷新：" + value;
        // MiniRefreshTimeText.Text = "刷新 " + value; // 刷新时间已从迷你胶囊移除（B 档位），控件已删除，保留代码便于日后恢复
    }

    private void Window_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || IsButtonSource(e.Source)) return;
        var now = DateTime.UtcNow;
        bool doubleClick = now - _lastPointerPressUtc < TimeSpan.FromMilliseconds(400);
        _lastPointerPressUtc = now;
        if (doubleClick)
        {
            ApplyMiniMode(false);
            _config.UseMiniMode = false;
            _configService.Save(_config);
            return;
        }
        _autoHideTimer.Stop();
        if (_isEdgeHidden) SetDockPosition(hidden: false);
        _isDragging = true;
        _dockEdge = DockEdge.None;
        try { BeginMoveDrag(e); }
        catch (InvalidOperationException) { }
        finally
        {
            _isDragging = false;
            if (_config.EnableEdgeAutoHide)
            {
                // macOS can keep the pointer inside the window after the
                // native BeginMoveDrag completes and does not always emit a
                // matching PointerExited event. Evaluate once after the
                // native move has committed instead of waiting for a hover
                // event that may never arrive.
                _pointerInside = false;
                _autoHideTimer.Start();
                Dispatcher.UIThread.Post(() => EvaluateEdgeAutoHide(force: true));
            }
        }
    }

    private void Window_PointerMoved(object? sender, PointerEventArgs e)
    {
        _pointerInside = true;
        Debug.WriteLine($"[EdgeAutoHide] PointerMoved position={Position} dragging={_isDragging} hidden={_isEdgeHidden}");
        if (_isEdgeHidden) SetDockPosition(hidden: false);
    }

    private static bool IsButtonSource(object? source)
    {
        for (var visual = source as Visual; visual is not null; visual = visual.Parent as Visual)
            if (visual is Button) return true;
        return false;
    }

    private void Hide_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Hide();

    private void MiniEdgeAutoHideBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _config.EnableEdgeAutoHide = !_config.EnableEdgeAutoHide;
        _configService.Save(_config);
        UpdateEdgeAutoHideButton();
        if (_config.EnableEdgeAutoHide) _autoHideTimer.Start();
        else
        {
            _autoHideTimer.Stop();
            if (_isEdgeHidden) SetDockPosition(hidden: false);
        }
    }

    private void UpdateEdgeAutoHideButton() => MiniEdgeAutoHideBtn.Content = _config.EnableEdgeAutoHide ? "贴边✓" : "贴边";
    private void MiniMinBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Hide();

    public void BringToFront()
    {
        Debug.WriteLine($"[DockLifecycle] BringToFront entry visible={IsVisible} loaded={IsLoaded} state={WindowState} position={Position} restoring={_isRestoringFromDock}");
        Console.Error.WriteLine($"[DockLifecycle] BringToFront entry visible={IsVisible} loaded={IsLoaded} state={WindowState} position={Position} restoring={_isRestoringFromDock}");
        _autoHideTimer.Stop();
        bool wasRestoringFromDock = _isRestoringFromDock;
        _isRestoringFromDock = true;
        try
        {
            // Show() is only needed after Hide(). Calling it for an already
            // visible edge-hidden window can raise Opened and asynchronously
            // restore the old off-screen position over this restore operation.
            if (!IsVisible)
                Show();
            // Show() can raise OnOpened, which starts the periodic edge check
            // for ordinary opens. Keep it disabled throughout dock restore.
            _autoHideTimer.Stop();
            WindowState = WindowState.Normal;
            IsVisible = true;

            // Avalonia's Activate() maps to the native macOS activation path. A
            // temporary Topmost elevation also handles borderless windows that
            // macOS otherwise leaves behind the currently focused application.
            bool wasTopmost = Topmost;
            if (!wasTopmost)
                ApplyAlwaysOnTop(true);
            else
                ApplyNativeWindowLevel(true);
            Activate();
            if (!wasTopmost)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    ApplyAlwaysOnTop(_config.IsAlwaysOnTop);
                    Activate();
                });
            }

        }
        finally
        {
            _isRestoringFromDock = wasRestoringFromDock;
            Debug.WriteLine($"[DockLifecycle] BringToFront exit visible={IsVisible} loaded={IsLoaded} state={WindowState} position={Position} restoring={_isRestoringFromDock}");
            Console.Error.WriteLine($"[DockLifecycle] BringToFront exit visible={IsVisible} loaded={IsLoaded} state={WindowState} position={Position} restoring={_isRestoringFromDock}");
        }
    }

    public void RestoreAndActivate()
    {
        Debug.WriteLine($"[DockLifecycle] RestoreAndActivate entry visible={IsVisible} position={Position}");
        Console.Error.WriteLine($"[DockLifecycle] RestoreAndActivate entry visible={IsVisible} position={Position}");
        BringToFront();
        if (_isEdgeHidden) SetDockPosition(hidden: false);
        Debug.WriteLine($"[DockLifecycle] RestoreAndActivate exit visible={IsVisible} position={Position}");
        Console.Error.WriteLine($"[DockLifecycle] RestoreAndActivate exit visible={IsVisible} position={Position}");
    }

    private void MiniCloseBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
        else
            Close();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        Debug.WriteLine($"[DockLifecycle] OnClosing entry reason={e.CloseReason} visible={IsVisible} loaded={IsLoaded} restoring={_isRestoringFromDock}");
        Console.Error.WriteLine($"[DockLifecycle] OnClosing entry reason={e.CloseReason} visible={IsVisible} loaded={IsLoaded} restoring={_isRestoringFromDock}");
        if (_isRestoringFromDock)
        {
            e.Cancel = true;
            Debug.WriteLine($"[DockLifecycle] OnClosing exit cancelled during restore reason={e.CloseReason} visible={IsVisible}");
            Console.Error.WriteLine($"[DockLifecycle] OnClosing exit cancelled during restore reason={e.CloseReason} visible={IsVisible}");
            return;
        }
        if (e.CloseReason is not WindowCloseReason.ApplicationShutdown and not WindowCloseReason.OSShutdown)
        {
            e.Cancel = true;
            SaveWindowPosition();
            Hide();
            Debug.WriteLine($"[DockLifecycle] OnClosing exit cancelled and hidden reason={e.CloseReason} visible={IsVisible}");
            Console.Error.WriteLine($"[DockLifecycle] OnClosing exit cancelled and hidden reason={e.CloseReason} visible={IsVisible}");
            return;
        }
        _refreshTimer.Stop();
        _codexTimer.Stop();
        _openCodeTimer.Stop();
        _openRouterTimer.Stop();
        _peakTimer.Stop();
        _positionSaveTimer.Stop();
        _autoHideTimer.Stop();
        _gptRecoveryTimer.Stop();
        _cancellation.Cancel();
        SaveWindowPosition();
        _cancellation.Dispose();
        _menuBarBalance?.Dispose();
        _menuBarBalance = null;
        _menuBarBalanceOc2?.Dispose();
        _menuBarBalanceOc2 = null;
        if (_codexProvider is IDisposable codex) codex.Dispose();
        if (_openCodeProvider is IDisposable openCode) openCode.Dispose();
        if (_openCodeProvider2 is IDisposable openCode2) openCode2.Dispose();
        if (_openRouterProvider is IDisposable openRouter) openRouter.Dispose();
        Debug.WriteLine($"[DockLifecycle] OnClosing exit shutdown reason={e.CloseReason} visible={IsVisible}");
        Console.Error.WriteLine($"[DockLifecycle] OnClosing exit shutdown reason={e.CloseReason} visible={IsVisible}");
    }

    private DockEdge DetectDockEdge()
    {
        var area = WorkArea();
        int left = Math.Abs(Position.X - area.X);
        int top = Math.Abs(Position.Y - area.Y);
        int right = Math.Abs(Position.X + WindowWidth() - area.Right);
        int bottom = Math.Abs(Position.Y + WindowHeight() - area.Bottom);
        var candidates = new[] { (DockEdge.Left, left), (DockEdge.Top, top), (DockEdge.Right, right), (DockEdge.Bottom, bottom) };
        var nearest = candidates.OrderBy(item => item.Item2).First();
        var edge = nearest.Item2 <= EdgeDetectionThreshold ? nearest.Item1 : DockEdge.None;
        Debug.WriteLine(
            $"[EdgeAutoHide] DetectDockEdge position={Position} size={WindowWidth()}x{WindowHeight()} " +
            $"area={area} distances=L{left}/T{top}/R{right}/B{bottom} threshold={EdgeDetectionThreshold} result={edge}");
        return edge;
    }

    private PixelPoint VisibleDockPosition()
    {
        var area = WorkArea();
        int left = Math.Clamp(Position.X, area.X, Math.Max(area.X, area.Right - WindowWidth()));
        int top = Math.Clamp(Position.Y, area.Y, Math.Max(area.Y, area.Bottom - WindowHeight()));
        return _dockEdge switch
        {
            DockEdge.Left => new PixelPoint(area.X, top),
            DockEdge.Top => new PixelPoint(left, area.Y),
            DockEdge.Right => new PixelPoint(area.Right - WindowWidth(), top),
            DockEdge.Bottom => new PixelPoint(left, area.Bottom - WindowHeight()),
            _ => new PixelPoint(left, top)
        };
    }

    private void SetDockPosition(bool hidden)
    {
        Debug.WriteLine($"[EdgeAutoHide] SetDockPosition requested hidden={hidden} edge={_dockEdge} position={Position} ui={Dispatcher.UIThread.CheckAccess()}");
        if (_dockEdge == DockEdge.None) return;
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => SetDockPosition(hidden));
            return;
        }
        var visible = VisibleDockPosition();
        var area = WorkArea();
        PixelPoint target = hidden
            ? _dockEdge switch
            {
                DockEdge.Left => new PixelPoint(area.X - WindowWidth() + (int)EdgeRevealThickness, visible.Y),
                DockEdge.Top => new PixelPoint(visible.X, area.Y - WindowHeight() + (int)EdgeRevealThickness),
                DockEdge.Right => new PixelPoint(area.Right - (int)EdgeRevealThickness, visible.Y),
                DockEdge.Bottom => new PixelPoint(visible.X, area.Bottom - (int)EdgeRevealThickness),
                _ => visible
            }
            : visible;
        _suppressPositionSave = true;
        try
        {
            Position = target;
            _isEdgeHidden = hidden;
            Debug.WriteLine($"[EdgeAutoHide] SetDockPosition applied target={target} hidden={hidden}");
        }
        finally { _suppressPositionSave = false; }
    }

    private void AutoHideTimerTick()
    {
        if (_isRestoringFromDock) return;
        if (!_config.EnableEdgeAutoHide || !_isMini || _isDragging || _isSettingsOpen || _pointerInside) return;
        if (_isEdgeHidden) return;
        EvaluateEdgeAutoHide();
    }

    private void EvaluateEdgeAutoHide(bool force = false)
    {
        if (_isRestoringFromDock) return;
        if (!_config.EnableEdgeAutoHide || !_isMini || _isDragging || _isSettingsOpen || !IsLoaded)
            return;
        if (!force && _pointerInside) return;

        var edge = DetectDockEdge();
        if (edge == DockEdge.None) return;
        _dockEdge = edge;
        SetDockPosition(hidden: true);
        SaveWindowPosition();
    }

    private void ShowError(string message)
    {
        StatusDot.Foreground = ThemeBrush("ErrorBrush");
        StatusText.Text = "需要注意";
        ErrorText.Text = message;
        UpdateRefreshTime();
        UpdateMenuBar(_latestBalance is { } b ? Symbol(b.Currency) + b.Total.ToString("0.00") + " !" : "¥ --", "DeepSeek 余额读取失败：" + message);
    }

    private void UpdateMenuBar(string title, string tooltip)
    {
        _menuBarBalanceText = title;
        _menuBarBalanceTooltip = tooltip;
        RefreshMenuBar();
    }

    private void RefreshMenuBar()
    {
        var titleParts = new List<string>();
        if (_config.EnableDeepSeekMonitoring)
        {
            titleParts.Add(_menuBarBalanceText);
            if (_config.ShowPeakIndicator) titleParts.Add(_menuBarPeakText);
        }
        if (_config.EnableCodexMonitoring) titleParts.Add("GPT " + _menuBarCodexText);
        if (_config.EnableOpenCodeMonitoring) titleParts.Add("OC1 " + _menuBarOpenCodeText);
        if (_config.EnableOpenRouterMonitoring) titleParts.Add("OR " + _menuBarOpenRouterText);
        if (_config.EnableWorkbuddyMonitoring) titleParts.Add("WB --");
        if (titleParts.Count == 0) titleParts.Add("额度监测已关闭");
        var tooltipParts = new List<string>();
        if (_config.EnableDeepSeekMonitoring) tooltipParts.Add(_menuBarBalanceTooltip);
        if (_config.EnableCodexMonitoring) tooltipParts.Add("ChatGPT Plus：" + _menuBarCodexTooltip);
        if (_config.EnableOpenCodeMonitoring) tooltipParts.Add("OpenCode Go：" + _menuBarOpenCodeTooltip);
        if (_config.EnableOpenRouterMonitoring) tooltipParts.Add("OpenRouter：" + _menuBarOpenRouterTooltip);
        if (_config.EnableWorkbuddyMonitoring) tooltipParts.Add("WorkBuddy：暂无额度数据源");
        _menuBarBalance?.Update(string.Join(" · ", titleParts), string.Join(Environment.NewLine, tooltipParts));

        // 账号2 独立状态项：配置了第二把 Key 才显示。
        if (_config.EnableOpenCodeMonitoring && _openCodeProvider2 is not null)
        {
            _menuBarBalanceOc2 ??= MacMenuBarBalance.Create(RestoreAndActivate);
            _menuBarBalanceOc2?.Update(_menuBarOpenCodeText2, _menuBarOpenCodeTooltip2);
        }
        else if (_menuBarBalanceOc2 is { } oc2Item)
        {
            oc2Item.Dispose();
            _menuBarBalanceOc2 = null;
        }
    }

    private static string FormatAccount(CodexAccountUsageSnapshot account)
    {
        if (!account.Usage.IsAvailable || account.Usage.Windows.Count == 0)
            return account.Email + "：" + (account.RefreshError ?? account.Usage.Error ?? "暂不可用");
        return account.Email + "：" + string.Join(Environment.NewLine,
            account.Usage.Windows.OrderBy(w => w.DurationMinutes ?? int.MaxValue)
                .Select(window => CodexUsageFormatter.FormatWindowRow(window, DateTimeOffset.Now)));
    }

    private static string FormatMenuBarCodex(IReadOnlyList<CodexAccountUsageSnapshot> accounts)
    {
        var windows = accounts.Where(a => a.Usage.IsAvailable)
            .SelectMany(a => a.Usage.Windows)
            .OrderBy(w => w.DurationMinutes ?? int.MaxValue)
            .Take(2).ToArray();
        if (windows.Length == 0) return "--";
        var text = string.Join("/", windows.Select(w => w.RemainingPercent)) + "%";
        // 5 小时窗口的恢复倒计时（如 4h24m）紧跟百分比之后；菜单栏不放汉字，已到期显示 0m。
        if (windows[0].ResetsAt is not { } resetsAt) return text;
        var now = DateTimeOffset.Now;
        return text + " " + (resetsAt <= now ? "0m" : CodexUsageFormatter.FormatCountdownShort(resetsAt, now));
    }

    private static string Symbol(string currency) => currency.Equals("USD", StringComparison.OrdinalIgnoreCase) ? "$" : "¥";

    private static IBrush ThemeBrush(string key) =>
        Application.Current is { } application && application.TryFindResource(key, out object? resource)
            && resource is IBrush brush
            ? brush
            : Brushes.Gray;
}
