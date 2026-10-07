using System;
using System.Collections.Generic;
using DeepSeekBalanceWidget.Models;

namespace DeepSeekBalanceWidget.Services;

/// <summary>
/// Builder 推荐规则唯一真源（SSOT）：固定按北京时间 UTC+8 与「周一~周五 / 周六日」两张表，
/// 产出唯一的菜单栏短文本与切换原因。节假日不做特殊判断，只区分工作日与周末。
/// 不允许把时段判断复制到 UI 层。
/// </summary>
public static class BuilderRecommendationCalculator
{
    /// <summary>菜单栏只允许出现这些值；MiMo / Qwen 免费模型明确不进推荐。</summary>
    public const string TraeDsMenuText = "trae·ds";
    public const string TraeOpenWorkDsMenuText = "trae/open/work·ds";
    public const string OpenWorkDsMenuText = "open/work·ds";
    public const string QoderDsWorkGlmMenuText = "qoder·ds/work·glm";

    private sealed record Segment(
        bool Weekend, int StartHour, int EndHour, string Key, string MenuText, string Reason);

    // 半开区间 [StartHour, EndHour)，EndHour=24 表示到次日 0 点。
    private static readonly Segment[] WeekdaySegments =
    {
        new(false, 0, 8, "trae-open-work-ds", TraeOpenWorkDsMenuText, "trae 夜间优惠；open/work 的 ds 非高峰"),
        new(false, 8, 9, "open-work-ds", OpenWorkDsMenuText, "trae 夜间时段结束"),
        new(false, 9, 12, "qoder-ds-work-glm", QoderDsWorkGlmMenuText, "open/work 的 ds 进入高峰"),
        new(false, 12, 14, "open-work-ds", OpenWorkDsMenuText, "ds 非高峰恢复"),
        new(false, 14, 18, "qoder-ds-work-glm", QoderDsWorkGlmMenuText, "open/work 的 ds 进入高峰"),
        new(false, 18, 22, "open-work-ds", OpenWorkDsMenuText, "ds 非高峰恢复"),
        new(false, 22, 24, "trae-open-work-ds", TraeOpenWorkDsMenuText, "trae 夜间优惠开始；open/work 的 ds 非高峰")
    };

    private static readonly Segment[] WeekendSegments =
    {
        new(true, 0, 8, "trae-open-work-ds", TraeOpenWorkDsMenuText, "trae 夜间优惠；open/work 的 ds 也处于非高峰"),
        new(true, 8, 22, "open-work-ds", OpenWorkDsMenuText, "周末 ds 非高峰"),
        new(true, 22, 24, "trae-open-work-ds", TraeOpenWorkDsMenuText, "trae 夜间优惠开始；open/work 的 ds 非高峰")
    };

    private static readonly TimeZoneInfo BeijingTz =
        TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");

    /// <summary>把给定时间转换到北京时间（Kind=Utc 按 UTC，Local/Unspecified 按本地），与 Mac 所在时区无关。</summary>
    public static DateTime ToBeijing(DateTime now) => TimeZoneInfo.ConvertTime(now, BeijingTz);

    /// <summary>计算当前推荐：菜单栏短文本、稳定 Key、切换原因与北京时间下一次切换点。</summary>
    public static BuilderRecommendation GetRecommendation(DateTime now)
    {
        var bj = ToBeijing(now);
        var segment = FindSegment(bj);
        return new BuilderRecommendation(
            segment.Key,
            segment.MenuText,
            segment.Reason,
            NextChange(bj, segment));
    }

    private static Segment FindSegment(DateTime bj)
    {
        bool weekend = bj.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var table = weekend ? WeekendSegments : WeekdaySegments;
        foreach (var segment in table)
            if (bj.Hour >= segment.StartHour && bj.Hour < segment.EndHour)
                return segment;
        return table[0]; // 表已覆盖 0~24，防御性兜底。
    }

    /// <summary>
    /// 下一次菜单栏文本真正发生变化的北京时间：跨过推荐相同的相邻时段（如 22:00→00:00）
    /// 不算切换点，因此 00:00 不会产生提醒。
    /// </summary>
    private static DateTime NextChange(DateTime bj, Segment current)
    {
        DateTime candidate = HourOn(bj, current.EndHour);
        for (int i = 0; i < 8; i++)
        {
            var segment = FindSegment(candidate);
            if (segment.Key != current.Key) return candidate;
            candidate = HourOn(candidate, segment.EndHour);
        }
        return candidate;
    }

    private static DateTime HourOn(DateTime reference, int hour)
        => hour >= 24 ? reference.Date.AddDays(1) : reference.Date.AddHours(hour);
}
