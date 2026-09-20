using System;
using System.Linq;

namespace DeepSeekBalanceWidget.Models;

public sealed class AppConfig
{
    public int ConfigVersion { get; set; } = 1;
    public string? ApiKeyEncrypted { get; set; }
    public string SelectedCurrency { get; set; } = "CNY";
    public int RefreshIntervalSeconds { get; set; } = 30;
    public decimal LowBalanceThreshold { get; set; } = 10m;
    public decimal AbnormalChangePercent { get; set; } = 10m;
    public int LowBalanceCooldownSeconds { get; set; } = 1800;
    public int AbnormalAlertCooldownSeconds { get; set; } = 600;
    public bool ShowToastNotifications { get; set; } = true;
    public bool IsAlwaysOnTop { get; set; } = true;
    public bool EnableEdgeAutoHide { get; set; }
    public bool UseMockData { get; set; }
    public bool UseMiniMode { get; set; }
    /// <summary>应用主题：Light、Dark 或 System。默认保持现有的浅色主题。</summary>
    public string ThemeMode { get; set; } = "Light";
    public bool EnableCodexMonitoring { get; set; } = true;

    /// <summary>DeepSeek 余额监测开关（关闭后胶囊与详细面板均不显示 DS 区块）。</summary>
    public bool EnableDeepSeekMonitoring { get; set; } = true;

    /// <summary>WorkBuddy 占位监测开关（无数据源，默认关闭）。</summary>
    public bool EnableWorkbuddyMonitoring { get; set; }

    /// <summary>OpenCode Go 额度监测开关。</summary>
    public bool EnableOpenCodeMonitoring { get; set; } = true;

    /// <summary>OpenCode Go API Key（DPAPI 加密存储）；为空时自动读本机 auth.json。</summary>
    public string? OpenCodeApiKeyEncrypted { get; set; }

    /// <summary>OpenCode Go 第二账号 API Key（DPAPI 加密存储）；为空时不显示第二账号。</summary>
    public string? OpenCodeApiKey2Encrypted { get; set; }

    /// <summary>OpenRouter 账户额度监测开关（当前为预留数据源，默认关闭）。</summary>
    public bool EnableOpenRouterMonitoring { get; set; }

    /// <summary>OpenRouter API Key（DPAPI 加密存储）。</summary>
    public string? OpenRouterApiKeyEncrypted { get; set; }

    /// <summary>警报声开关（DS 低余额 / GPT 与 OpenCode 额度预警共用）。</summary>
    public bool AlertSoundEnabled { get; set; } = true;

    /// <summary>警报声风格：Beep / Ascending / Descending / Chime / Bell / DingDong / Rapid / SlowPulse / Soft / Standard / Urgent。</summary>
    public string AlertSoundStyle { get; set; } = "Standard";

    /// <summary>警报模式：Continuous=持续直到点「知道了」；Limited=限时（至少 10 秒）。</summary>
    public string AlertMode { get; set; } = "Continuous";

    /// <summary>警报窗位置：TopRight / RightCenter / BottomRight。</summary>
    public string AlertPosition { get; set; } = "TopRight";

    /// <summary>ChatGPT 额度预警总开关（剩余百分比预警 + 额度恢复通知）。</summary>
    public bool EnableCodexQuotaAlerts { get; set; } = true;

    /// <summary>ChatGPT 额度预警档位（默认 20% / 10%）。</summary>
    public List<int> GptQuotaAlertThresholds { get; set; } = new() { 20, 10 };

    /// <summary>ChatGPT 周额度是否参与低量预警。</summary>
    public bool GptWeeklyAlertEnabled { get; set; } = true;

    /// <summary>ChatGPT 额度恢复播报阈值（默认 100%，即回到满额才播报恢复）。</summary>
    public int GptQuotaRecoveredPercent { get; set; } = 100;

    /// <summary>OpenCode 额度预警总开关（5 小时 / 周 / 月）。</summary>
    public bool EnableOpenCodeQuotaAlerts { get; set; } = true;

    /// <summary>OpenCode 额度预警档位（5 小时、周、月窗口共用，默认 20% / 10%）。</summary>
    public List<int> OcQuotaAlertThresholds { get; set; } = new() { 20, 10 };

    /// <summary>OpenCode 周额度是否参与低量预警。</summary>
    public bool OcWeeklyAlertEnabled { get; set; } = true;

    /// <summary>OpenCode 月额度是否参与低量预警。</summary>
    public bool OcMonthlyAlertEnabled { get; set; } = true;

    /// <summary>OpenCode 额度恢复播报阈值。</summary>
    public int OcQuotaRecoveredPercent { get; set; } = 95;

    // Legacy shared fields retained for config-file compatibility with 0.4.x.
    /// <summary>剩余百分比预警档位（降序生效，默认 20% / 10% 各提醒一次）。</summary>
    public List<int> CodexQuotaAlertThresholds { get; set; } = new() { 20, 10 };

    /// <summary>周额度是否也参与低量预警（周额度耗尽需等一周才恢复）。</summary>
    public bool CodexWeeklyAlertEnabled { get; set; } = true;

    /// <summary>判定“额度已恢复”的剩余百分比阈值；低于该值后再回到该值才播报恢复。</summary>
    public int CodexQuotaRecoveredPercent { get; set; } = 95;

    /// <summary>恢复播报的最小间隔秒数，吸收剩余百分比在阈值附近抖动的重复播报。</summary>
    public int CodexQuotaAlertCooldownSeconds { get; set; } = 300;

    public double CodexFontSize { get; set; } = 14;
    public string CodexFontStyle { get; set; } = "DeepSeek"; // DeepSeek / Regular / Bold
    /// <summary>胶囊区块渲染顺序：deepseek / chatgpt / opencode / openrouter / workbuddy，可在设置中调整。</summary>
    public List<string> AgentOrder { get; set; } = new() { "deepseek", "chatgpt", "opencode", "openrouter" };

    /// <summary>
    /// 加载后归一化：旧配置的 AgentOrder 缺 opencode/openrouter 时补到末尾，
    /// 使升级用户无需手动调整即可看到新区块。
    /// </summary>
    public void Normalize()
    {
        GptQuotaAlertThresholds ??= new List<int>();
        OcQuotaAlertThresholds ??= new List<int>();

        // Migrate the former shared values for existing users. New settings
        // remain independent after the first save in the revised UI.
        var defaults = new[] { 20, 10 };
        if (GptQuotaAlertThresholds.Count == 0
            || (GptQuotaAlertThresholds.SequenceEqual(defaults)
                && CodexQuotaAlertThresholds is { Count: > 0 }
                && !CodexQuotaAlertThresholds.SequenceEqual(defaults)))
            GptQuotaAlertThresholds = CodexQuotaAlertThresholds is { Count: > 0 }
                ? new List<int>(CodexQuotaAlertThresholds)
                : new List<int>(defaults);
        if (OcQuotaAlertThresholds.Count == 0
            || (OcQuotaAlertThresholds.SequenceEqual(defaults)
                && CodexQuotaAlertThresholds is { Count: > 0 }
                && !CodexQuotaAlertThresholds.SequenceEqual(defaults)))
            OcQuotaAlertThresholds = CodexQuotaAlertThresholds is { Count: > 0 }
                ? new List<int>(CodexQuotaAlertThresholds)
                : new List<int>(defaults);
        if (GptQuotaRecoveredPercent == 95 && CodexQuotaRecoveredPercent != 95)
            GptQuotaRecoveredPercent = CodexQuotaRecoveredPercent;
        if (OcQuotaRecoveredPercent == 95 && CodexQuotaRecoveredPercent != 95)
            OcQuotaRecoveredPercent = CodexQuotaRecoveredPercent;
        if (GptWeeklyAlertEnabled && !CodexWeeklyAlertEnabled)
            GptWeeklyAlertEnabled = false;
        if (OcWeeklyAlertEnabled && !CodexWeeklyAlertEnabled)
            OcWeeklyAlertEnabled = false;

        AgentOrder ??= new List<string>();
        if (AgentOrder.Count == 0)
            AgentOrder.AddRange(new[] { "deepseek", "chatgpt", "opencode", "openrouter" });
        if (!AgentOrder.Contains("opencode")) AgentOrder.Add("opencode");
        if (!AgentOrder.Contains("openrouter")) AgentOrder.Add("openrouter");
    }
    public string DefaultCorner { get; set; } = "Remember"; // Remember / BottomRight / BottomLeft
    public bool ShowPeakIndicator { get; set; } = true;
    public List<PeakRange> PeakHourRanges { get; set; } = new()
    {
        new PeakRange(9, 12, WeekdaysOnly: true),
        new PeakRange(14, 18, WeekdaysOnly: true)
    };
    public bool AutoStart { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public decimal? LastSuccessfulBalance { get; set; }
    public DateTimeOffset? LastSuccessfulRefreshUtc { get; set; }
    public bool InLowBalanceState { get; set; }
}
