using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DeepSeekBalanceWidget.Models;
using DeepSeekBalanceWidget.Services;

namespace DeepSeekBalanceWidget;

public partial class SettingsWindow : Window
{
    private readonly MacConfigService _configService;
    private readonly AppConfig _config;
    private readonly Action? _onApplied;
    private bool _clearKey;
    private bool _clearOpenCodeKey;
    private bool _clearOpenCodeKey2;
    private bool _clearOpenRouterKey;

    public SettingsWindow(MacConfigService configService, AppConfig config, Action? onApplied = null)
    {
        InitializeComponent();
        _configService = configService;
        _config = config;
        _onApplied = onApplied;
        LoadSettings();
        Nav_Click(NavMonitoring, new RoutedEventArgs());
    }

    /// <summary>从当前配置初始化四个面板的控件。</summary>
    private void LoadSettings()
    {
        ApiKeyBox.Text = _configService.GetApiKey() ?? string.Empty;
        OpenCodeKeyBox.Text = _configService.GetOpenCodeApiKey() ?? string.Empty;
        OpenCodeKeyBox2.Text = _configService.GetOpenCodeApiKey2() ?? string.Empty;
        OpenRouterKeyBox.Text = _configService.GetOpenRouterApiKey() ?? string.Empty;

        IntervalBox.Text = _config.RefreshIntervalSeconds.ToString(CultureInfo.CurrentCulture);
        ThresholdBox.Text = _config.LowBalanceThreshold.ToString("0.##", CultureInfo.CurrentCulture);
        ChangePercentBox.Text = _config.AbnormalChangePercent.ToString("0.##", CultureInfo.CurrentCulture);
        CurrencyBox.SelectedIndex = _config.SelectedCurrency.Equals("USD", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        EnableDsCheck.IsChecked = _config.EnableDeepSeekMonitoring;
        EnableCodexCheck.IsChecked = _config.EnableCodexMonitoring;
        EnableWbCheck.IsChecked = _config.EnableWorkbuddyMonitoring;
        EnableOcCheck.IsChecked = _config.EnableOpenCodeMonitoring;
        EnableOpenRouterCheck.IsChecked = _config.EnableOpenRouterMonitoring;

        EnableGptAlerts.IsChecked = _config.EnableCodexQuotaAlerts;
        GptThresholdsBox.Text = _config.GptQuotaAlertThresholds is { Count: > 0 } gptThresholds
            ? string.Join(", ", gptThresholds)
            : "20, 10";
        GptRecoveredBox.Text = Math.Clamp(_config.GptQuotaRecoveredPercent, 1, 100)
            .ToString(CultureInfo.CurrentCulture);
        GptWeeklyCheck.IsChecked = _config.GptWeeklyAlertEnabled;

        EnableOcAlerts.IsChecked = _config.EnableOpenCodeQuotaAlerts;
        string ocThresholds = _config.OcQuotaAlertThresholds is { Count: > 0 } configuredOcThresholds
            ? string.Join(", ", configuredOcThresholds)
            : "20, 10";
        OcThresholds5hBox.Text = ocThresholds;
        OcThresholdsWeeklyBox.Text = ocThresholds;
        OcThresholdsMonthlyBox.Text = ocThresholds;
        OcRecoveredBox.Text = Math.Clamp(_config.OcQuotaRecoveredPercent, 1, 100)
            .ToString(CultureInfo.CurrentCulture);
        OcWeeklyCheck.IsChecked = _config.OcWeeklyAlertEnabled;
        OcMonthlyCheck.IsChecked = _config.OcMonthlyAlertEnabled;
        AlertSoundCheck.IsChecked = _config.AlertSoundEnabled;
        AlertSoundStyleBox.SelectedIndex = _config.AlertSoundStyle switch
        {
            "Beep" => 0,
            "Ascending" => 1,
            "Descending" => 2,
            "Chime" => 3,
            "Bell" => 4,
            "DingDong" => 5,
            "Rapid" => 6,
            "SlowPulse" => 7,
            "Soft" => 8,
            "Urgent" => 10,
            _ => 9
        };
        AlertContinuousRadio.IsChecked = !_config.AlertMode.Equals("Limited", StringComparison.OrdinalIgnoreCase);
        AlertLimitedRadio.IsChecked = _config.AlertMode.Equals("Limited", StringComparison.OrdinalIgnoreCase);
        AlertPositionBox.SelectedIndex = _config.AlertPosition switch
        {
            "RightCenter" => 1,
            "BottomRight" => 2,
            _ => 0
        };

        TopmostCheck.IsChecked = _config.IsAlwaysOnTop;
        ShowPeakCheck.IsChecked = _config.ShowPeakIndicator;
        MiniModeCheck.IsChecked = _config.UseMiniMode;
        switch (ThemeService.Normalize(_config.ThemeMode))
        {
            case "dark": ThemeDarkRadio.IsChecked = true; break;
            case "system": ThemeSystemRadio.IsChecked = true; break;
            default: ThemeLightRadio.IsChecked = true; break;
        }

        MockCheck.IsChecked = _config.UseMockData;
        AutoStartCheck.IsChecked = _config.AutoStart || MacAutoStartService.IsEnabled();
    }

    private async void ClearDsKey_Click(object? sender, RoutedEventArgs e)
    {
        if (!await ShowClearKeyMessageBoxAsync())
            return;

        ApiKeyBox.Text = string.Empty;
        _clearKey = true;
    }

    private async void ClearOpenCodeKey_Click(object? sender, RoutedEventArgs e)
    {
        if (!await ShowClearKeyMessageBoxAsync())
            return;

        OpenCodeKeyBox.Text = string.Empty;
        _clearOpenCodeKey = true;
    }

    private async void ClearOpenCodeKey2_Click(object? sender, RoutedEventArgs e)
    {
        if (!await ShowClearKeyMessageBoxAsync())
            return;

        OpenCodeKeyBox2.Text = string.Empty;
        _clearOpenCodeKey2 = true;
    }

    private async void ClearOpenRouterKey_Click(object? sender, RoutedEventArgs e)
    {
        if (!await ShowClearKeyMessageBoxAsync())
            return;

        OpenRouterKeyBox.Text = string.Empty;
        _clearOpenRouterKey = true;
    }

    private async Task<bool> ShowClearKeyMessageBoxAsync()
    {
        var dialog = new Window
        {
            Title = "清除 API Key",
            Width = 420,
            Height = 180,
            MinWidth = 420,
            MinHeight = 180,
            MaxWidth = 420,
            MaxHeight = 180,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var message = new TextBlock
        {
            Text = "确定要清除 API Key 吗？此操作不可撤销。",
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var cancelButton = new Button { Content = "取消", Width = 72, IsCancel = true };
        var confirmButton = new Button { Content = "确定", Width = 72, IsDefault = true };
        cancelButton.Click += (_, _) => dialog.Close(false);
        confirmButton.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(cancelButton);
        buttons.Children.Add(confirmButton);

        dialog.Content = new Border
        {
            Padding = new Thickness(24),
            Child = new StackPanel
            {
                Spacing = 18,
                Children = { message, buttons }
            }
        };

        return await dialog.ShowDialog<bool>(this) == true;
    }

    /// <summary>按左侧导航项切换右侧面板。</summary>
    private void Nav_Click(object? sender, RoutedEventArgs e)
    {
        PanelMonitoring.IsVisible = NavMonitoring.IsChecked == true;
        PanelAlerts.IsVisible = NavAlerts.IsChecked == true;
        PanelAppearance.IsVisible = NavAppearance.IsChecked == true;
        PanelGeneral.IsVisible = NavGeneral.IsChecked == true;
    }

    private void ThemeRadio_Click(object? sender, RoutedEventArgs e)
    {
        string mode = sender switch
        {
            _ when ReferenceEquals(sender, ThemeDarkRadio) => "Dark",
            _ when ReferenceEquals(sender, ThemeSystemRadio) => "System",
            _ => "Light"
        };
        ThemeService.Apply(mode);
    }

    private async void TestDs_Click(object? sender, RoutedEventArgs e)
    {
        string key = string.IsNullOrWhiteSpace(ApiKeyBox.Text)
            ? _configService.GetApiKey() ?? string.Empty
            : ApiKeyBox.Text;
        if (string.IsNullOrWhiteSpace(key))
        {
            SetTestResult(DsTestResult, false, "✗ 请先填写 DeepSeek API Key");
            return;
        }

        await RunTestAsync(TestDsBtn, DsTestResult, async () =>
        {
            var parsed = BalanceParser.Parse(await new DeepSeekApiClient(key)
                .GetBalanceJsonAsync(CancellationToken.None));
            if (!parsed.Success) return (false, "✗ " + (parsed.Error ?? "解析失败"));
            var selected = CurrencySelector.Select(parsed.Balances, _config.SelectedCurrency);
            return selected.Found && selected.Balance is not null
                ? (true, $"✓ 连接成功 · {selected.Balance.Total:0.00} {selected.Balance.Currency}")
                : (false, $"✗ 未返回 {_config.SelectedCurrency} 余额");
        });
    }

    private async void TestCodex_Click(object? sender, RoutedEventArgs e)
    {
        await RunTestAsync(TestCodexBtn, CodexTestResult, async () =>
        {
            using var provider = new MacCodexUsageProvider();
            var usages = await provider.GetUsagesAsync(CancellationToken.None);
            var available = usages.Count(u => u.Usage.IsAvailable);
            return available > 0
                ? (true, $"✓ 连接成功 · {available} 个账号")
                : (false, "✗ 未检测到可用的 Codex 额度");
        });
    }

    private async void TestOc_Click(object? sender, RoutedEventArgs e)
    {
        string? key1 = string.IsNullOrWhiteSpace(OpenCodeKeyBox.Text) ? null : OpenCodeKeyBox.Text;
        string? key2 = string.IsNullOrWhiteSpace(OpenCodeKeyBox2.Text) ? null : OpenCodeKeyBox2.Text;
        await RunTestAsync(TestOcBtn, OcTestResult, async () =>
        {
            async Task<string> TestKeyAsync(string? key)
            {
                using var provider = new OpenCodeUsageProvider(key);
                var snapshot = await provider.GetUsageAsync(CancellationToken.None);
                if (!snapshot.IsAvailable)
                    return "✗ " + (snapshot.Error ?? "暂不可用");
                string summary = string.Join(" · ", snapshot.Windows.Select(window =>
                    $"{OpenCodeUsageFormatter.ShortLabelOf(window.Kind)} {window.RemainingPercent}%"));
                return "✓ 连接成功" + (summary.Length == 0 ? string.Empty : " · " + summary);
            }

            if (key2 is null)
                return (true, await TestKeyAsync(key1));
            string result1 = await TestKeyAsync(key1);
            string result2 = await TestKeyAsync(key2);
            bool ok = result1.StartsWith('✓') && result2.StartsWith('✓');
            return (ok, $"账号1 {result1}｜账号2 {result2}");
        });
    }

    private async void TestOpenRouter_Click(object? sender, RoutedEventArgs e)
    {
        string? key = string.IsNullOrWhiteSpace(OpenRouterKeyBox.Text) ? null : OpenRouterKeyBox.Text;
        await RunTestAsync(TestOpenRouterBtn, OpenRouterTestResult, async () =>
        {
            using var provider = new OpenRouterUsageProvider(key);
            var snapshot = await provider.GetUsageAsync(CancellationToken.None);
            return snapshot.IsAvailable
                ? (true, "✓ 连接成功")
                : (false, "✗ " + (snapshot.Error ?? "暂不可用"));
        });
    }

    private void TestAlarm_Click(object? sender, RoutedEventArgs e)
    {
        if (AlertSoundCheck.IsChecked != true)
        {
            ShowError("警报声已关闭，请先勾选「播放警报声」");
            return;
        }

        TestAlarmBtn.IsEnabled = false;
        string style = (AlertSoundStyleBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()
            ?? "Standard";
        MacAlarmSound.Play(style);

        var stop = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        stop.Tick += (_, _) =>
        {
            stop.Stop();
            MacAlarmSound.Stop();
            TestAlarmBtn.IsEnabled = true;
        };
        stop.Start();
    }

    private static async Task RunTestAsync(
        Button button,
        TextBlock result,
        Func<Task<(bool ok, string message)>> test)
    {
        button.IsEnabled = false;
        SetTestResult(result, null, "测试中…");
        try
        {
            var (ok, message) = await test();
            SetTestResult(result, ok, message);
        }
        catch (Exception ex)
        {
            SetTestResult(result, false, "✗ " + ex.Message);
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private static void SetTestResult(TextBlock result, bool? ok, string message)
    {
        result.Text = message;
        result.Foreground = ThemeBrush(result, ok switch
        {
            true => "PeakSuccessBrush",
            false => "PeakWarningBrush",
            _ => "TextTertiaryBrush"
        });
    }

    private async void ApplyBtn_Click(object? sender, RoutedEventArgs e)
    {
        await ApplyChangesAsync();
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (await ApplyChangesAsync())
            Close(true);
    }

    private async Task<bool> ApplyChangesAsync()
    {
        if (!int.TryParse(IntervalBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int interval)
            || interval is < 5 or > 3600)
        {
            ShowError("刷新间隔需在 5–3600 秒之间。");
            return false;
        }
        if (!decimal.TryParse(ThresholdBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal threshold)
            || threshold < 0)
        {
            ShowError("低余额阈值不能为负数。");
            return false;
        }
        if (!decimal.TryParse(ChangePercentBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal percentage)
            || percentage is < 0 or > 100)
        {
            ShowError("异常下降百分比需在 0–100 之间。");
            return false;
        }
        if (!TryParseThresholds(GptThresholdsBox.Text, out var gptThresholds))
        {
            ShowError("ChatGPT 预警档位需为 1–99 的整数，多个用逗号分隔（如 20, 10）。");
            return false;
        }
        if (!int.TryParse(GptRecoveredBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int gptRecovered)
            || gptRecovered is < 1 or > 100)
        {
            ShowError("ChatGPT 恢复判定阈值需在 1–100 之间。");
            return false;
        }
        if (!TryParseThresholds(OcThresholds5hBox.Text, out var ocThresholds5h)
            || !TryParseThresholds(OcThresholdsWeeklyBox.Text, out var ocThresholdsWeekly)
            || !TryParseThresholds(OcThresholdsMonthlyBox.Text, out var ocThresholdsMonthly))
        {
            ShowError("OpenCode 各窗口预警档位需为 1–99 的整数，多个用逗号分隔（如 20, 10）。");
            return false;
        }
        if (!int.TryParse(OcRecoveredBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int ocRecovered)
            || ocRecovered is < 1 or > 100)
        {
            ShowError("OpenCode 恢复判定阈值需在 1–100 之间。");
            return false;
        }
        var ocThresholds = ocThresholds5h
            .Concat(ocThresholdsWeekly)
            .Concat(ocThresholdsMonthly)
            .Distinct()
            .ToList();

        try
        {
            // 控件属性只能在 UI 线程读取，先取快照再交给后台线程。
            string? apiKey = _clearKey ? null : ApiKeyBox.Text;
            string? openCodeKey = _clearOpenCodeKey ? null : OpenCodeKeyBox.Text;
            string? openCodeKey2 = _clearOpenCodeKey2 ? null : OpenCodeKeyBox2.Text;
            string? openRouterKey = _clearOpenRouterKey ? null : OpenRouterKeyBox.Text;
            string currency = CurrencyBox.SelectedIndex == 1 ? "USD" : "CNY";
            bool enableDs = EnableDsCheck.IsChecked == true;
            bool enableCodex = EnableCodexCheck.IsChecked == true;
            bool enableWb = EnableWbCheck.IsChecked == true;
            bool enableOc = EnableOcCheck.IsChecked == true;
            bool enableOpenRouter = EnableOpenRouterCheck.IsChecked == true;
            bool enableGptAlerts = EnableGptAlerts.IsChecked == true;
            bool gptWeekly = GptWeeklyCheck.IsChecked == true;
            bool enableOcAlerts = EnableOcAlerts.IsChecked == true;
            bool ocWeekly = OcWeeklyCheck.IsChecked == true;
            bool ocMonthly = OcMonthlyCheck.IsChecked == true;
            bool alertSound = AlertSoundCheck.IsChecked == true;
            string alertSoundStyle = (AlertSoundStyleBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Standard";
            bool alertLimited = AlertLimitedRadio.IsChecked == true;
            string alertPosition = (AlertPositionBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "TopRight";
            bool alwaysOnTop = TopmostCheck.IsChecked == true;
            bool showPeak = ShowPeakCheck.IsChecked == true;
            bool miniMode = MiniModeCheck.IsChecked == true;
            string themeMode = ThemeDarkRadio.IsChecked == true
                ? "Dark"
                : ThemeSystemRadio.IsChecked == true ? "System" : "Light";
            bool useMock = MockCheck.IsChecked == true;
            bool autoStart = AutoStartCheck.IsChecked == true;

            // 钥匙串写入、启动项与配置落盘都涉及进程/磁盘 IO，放到后台线程，
            // 避免 security 等待授权确认时把 UI 线程一起挂死。
            await Task.Run(() =>
            {
                // 空框保留已保存的 Key；显式清除请使用对应输入框旁的「清除 Key」。
                if (_clearKey)
                    _configService.SetApiKey(_config, null);
                else if (!string.IsNullOrWhiteSpace(apiKey))
                    _configService.SetApiKey(_config, apiKey);
                if (_clearOpenCodeKey)
                    _configService.SetOpenCodeApiKey(_config, null);
                else if (!string.IsNullOrWhiteSpace(openCodeKey))
                    _configService.SetOpenCodeApiKey(_config, openCodeKey);
                if (_clearOpenCodeKey2)
                    _configService.SetOpenCodeApiKey2(_config, null);
                else if (!string.IsNullOrWhiteSpace(openCodeKey2))
                    _configService.SetOpenCodeApiKey2(_config, openCodeKey2);
                if (_clearOpenRouterKey)
                    _configService.SetOpenRouterApiKey(_config, null);
                else if (!string.IsNullOrWhiteSpace(openRouterKey))
                    _configService.SetOpenRouterApiKey(_config, openRouterKey);

                _config.RefreshIntervalSeconds = interval;
                _config.LowBalanceThreshold = threshold;
                _config.AbnormalChangePercent = percentage;
                _config.SelectedCurrency = currency;
                _config.EnableDeepSeekMonitoring = enableDs;
                _config.EnableCodexMonitoring = enableCodex;
                _config.EnableWorkbuddyMonitoring = enableWb;
                _config.EnableOpenCodeMonitoring = enableOc;
                _config.EnableOpenRouterMonitoring = enableOpenRouter;

                _config.EnableCodexQuotaAlerts = enableGptAlerts;
                _config.GptQuotaAlertThresholds = gptThresholds;
                _config.GptQuotaRecoveredPercent = gptRecovered;
                _config.GptWeeklyAlertEnabled = gptWeekly;
                _config.EnableOpenCodeQuotaAlerts = enableOcAlerts;
                _config.OcQuotaAlertThresholds = ocThresholds;
                _config.OcQuotaRecoveredPercent = ocRecovered;
                _config.OcWeeklyAlertEnabled = ocWeekly;
                _config.OcMonthlyAlertEnabled = ocMonthly;
                _config.AlertSoundEnabled = alertSound;
                _config.AlertSoundStyle = alertSoundStyle;
                _config.AlertMode = alertLimited ? "Limited" : "Continuous";
                _config.AlertPosition = alertPosition;

                _config.IsAlwaysOnTop = alwaysOnTop;
                _config.ShowPeakIndicator = showPeak;
                _config.UseMiniMode = miniMode;
                _config.ThemeMode = themeMode;
                _config.UseMockData = useMock;
                _config.AutoStart = autoStart;

                MacAutoStartService.Set(_config.AutoStart);
                _configService.Save(_config);
            });

            // 以下均为 UI 操作，回到 UI 线程执行。
            ThemeService.Apply(_config.ThemeMode);
            _onApplied?.Invoke();
            ErrorText.Text = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            return false;
        }
    }

    private static bool TryParseThresholds(string? text, out List<int> thresholds)
    {
        thresholds = new List<int>();
        var parts = (text ?? string.Empty).Split(
            new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string part in parts)
        {
            if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.CurrentCulture, out int value)
                || value is < 1 or > 99)
                return false;
            if (!thresholds.Contains(value)) thresholds.Add(value);
        }
        if (thresholds.Count == 0) thresholds.AddRange(new[] { 20, 10 });
        return true;
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

    private void ShowError(string message) => ErrorText.Text = message;

    /// <summary>
    /// 从控件作用域解析主题画刷（能正确处理 ThemeDictionaries 的明暗变体）；
    /// 找不到时回退灰色——缺资源绝不让应用崩溃。
    /// </summary>
    private static IBrush ThemeBrush(Avalonia.Visual scope, string key) =>
        (scope.FindResource(key) as IBrush)
        ?? (Application.Current?.FindResource(key) as IBrush)
        ?? Brushes.Gray;
}
