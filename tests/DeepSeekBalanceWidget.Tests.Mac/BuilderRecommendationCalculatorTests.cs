using System;
using DeepSeekBalanceWidget.Models;
using DeepSeekBalanceWidget.Services;
using Xunit;

namespace DeepSeekBalanceWidget.Tests;

public class BuilderRecommendationCalculatorTests
{
    private static readonly TimeZoneInfo Beijing =
        TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");

    /// <summary>把指定北京时间转为 UTC 输入，模拟 Mac 处在任意时区的情况。</summary>
    private static DateTime BeijingDateHourUtc(int year, int month, int day, int hour, int minute = 0)
        => TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified), Beijing);

    // 2026-10-05 是周一，2026-10-10 是周六，2026-10-11 是周日。
    private static DateTime WeekdayUtc(int hour, int minute = 0)
        => BeijingDateHourUtc(2026, 10, 5, hour, minute);

    private static DateTime SaturdayUtc(int hour, int minute = 0)
        => BeijingDateHourUtc(2026, 10, 10, hour, minute);

    private static DateTime SundayUtc(int hour, int minute = 0)
        => BeijingDateHourUtc(2026, 10, 11, hour, minute);

    private static string Menu(DateTime utc) =>
        BuilderRecommendationCalculator.GetRecommendation(utc).MenuText;

    // ---------- 工作日七个时段（含边界分钟） ----------

    [Theory]
    [InlineData(0, 0)]
    [InlineData(7, 59)]
    public void Weekday_0000_0759_IsTraeOpenWorkDs(int hour, int minute) =>
        Assert.Equal(BuilderRecommendationCalculator.TraeOpenWorkDsMenuText, Menu(WeekdayUtc(hour, minute)));

    [Theory]
    [InlineData(8, 0)]
    [InlineData(8, 59)]
    public void Weekday_0800_0859_IsOpenWorkDs(int hour, int minute) =>
        Assert.Equal(BuilderRecommendationCalculator.OpenWorkDsMenuText, Menu(WeekdayUtc(hour, minute)));

    [Theory]
    [InlineData(9, 0)]
    [InlineData(11, 59)]
    public void Weekday_0900_1159_IsQoderDsWorkGlm(int hour, int minute) =>
        Assert.Equal(BuilderRecommendationCalculator.QoderDsWorkGlmMenuText, Menu(WeekdayUtc(hour, minute)));

    [Theory]
    [InlineData(12, 0)]
    [InlineData(13, 59)]
    public void Weekday_1200_1359_IsOpenWorkDs(int hour, int minute) =>
        Assert.Equal(BuilderRecommendationCalculator.OpenWorkDsMenuText, Menu(WeekdayUtc(hour, minute)));

    [Theory]
    [InlineData(14, 0)]
    [InlineData(17, 59)]
    public void Weekday_1400_1759_IsQoderDsWorkGlm(int hour, int minute) =>
        Assert.Equal(BuilderRecommendationCalculator.QoderDsWorkGlmMenuText, Menu(WeekdayUtc(hour, minute)));

    [Theory]
    [InlineData(18, 0)]
    [InlineData(21, 59)]
    public void Weekday_1800_2159_IsOpenWorkDs(int hour, int minute) =>
        Assert.Equal(BuilderRecommendationCalculator.OpenWorkDsMenuText, Menu(WeekdayUtc(hour, minute)));

    [Theory]
    [InlineData(22, 0)]
    [InlineData(23, 59)]
    public void Weekday_2200_2359_IsTraeOpenWorkDs(int hour, int minute) =>
        Assert.Equal(BuilderRecommendationCalculator.TraeOpenWorkDsMenuText, Menu(WeekdayUtc(hour, minute)));

    // ---------- 周末三段 ----------

    [Theory]
    [InlineData(0, 0)]
    [InlineData(7, 59)]
    public void Weekend_0000_0759_IsTraeOpenWorkDs(int hour, int minute)
    {
        Assert.Equal(BuilderRecommendationCalculator.TraeOpenWorkDsMenuText, Menu(SaturdayUtc(hour, minute)));
        Assert.Equal(BuilderRecommendationCalculator.TraeOpenWorkDsMenuText, Menu(SundayUtc(hour, minute)));
    }

    [Theory]
    [InlineData(8, 0)]
    [InlineData(21, 59)]
    public void Weekend_0800_2159_IsOpenWorkDs(int hour, int minute)
    {
        Assert.Equal(BuilderRecommendationCalculator.OpenWorkDsMenuText, Menu(SaturdayUtc(hour, minute)));
        Assert.Equal(BuilderRecommendationCalculator.OpenWorkDsMenuText, Menu(SundayUtc(hour, minute)));
    }

    [Theory]
    [InlineData(22, 0)]
    [InlineData(23, 59)]
    public void Weekend_2200_2359_IsTraeOpenWorkDs(int hour, int minute)
    {
        Assert.Equal(BuilderRecommendationCalculator.TraeOpenWorkDsMenuText, Menu(SaturdayUtc(hour, minute)));
        Assert.Equal(BuilderRecommendationCalculator.TraeOpenWorkDsMenuText, Menu(SundayUtc(hour, minute)));
    }

    // ---------- 提醒触发时刻与正文原因 ----------

    [Theory]
    [InlineData(8, "open-work-ds", "trae 夜间时段结束")]
    [InlineData(9, "qoder-ds-work-glm", "open/work 的 ds 进入高峰")]
    [InlineData(12, "open-work-ds", "ds 非高峰恢复")]
    [InlineData(14, "qoder-ds-work-glm", "open/work 的 ds 进入高峰")]
    [InlineData(18, "open-work-ds", "ds 非高峰恢复")]
    [InlineData(22, "trae-open-work-ds", "trae 夜间优惠开始；open/work 的 ds 非高峰")]
    public void Weekday_Boundary_KeyAndReason(int hour, string key, string reason)
    {
        var rec = BuilderRecommendationCalculator.GetRecommendation(WeekdayUtc(hour));
        Assert.Equal(key, rec.Key);
        Assert.Equal(reason, rec.Reason);
    }

    [Theory]
    [InlineData(8, "open-work-ds", "周末 ds 非高峰")]
    [InlineData(22, "trae-open-work-ds", "trae 夜间优惠开始；open/work 的 ds 非高峰")]
    public void Weekend_Boundary_KeyAndReason(int hour, string key, string reason)
    {
        var rec = BuilderRecommendationCalculator.GetRecommendation(SaturdayUtc(hour));
        Assert.Equal(key, rec.Key);
        Assert.Equal(reason, rec.Reason);
    }

    // ---------- 时区转换 ----------

    [Fact]
    public void ToBeijing_ConvertsUtcToChinaStandardTime()
    {
        var utc = new DateTime(2026, 10, 5, 1, 0, 0, DateTimeKind.Utc);
        var beijing = BuilderRecommendationCalculator.ToBeijing(utc);
        Assert.Equal(new DateTime(2026, 10, 5, 9, 0, 0), beijing);
        Assert.Equal(DateTimeKind.Unspecified, beijing.Kind);
    }

    [Fact]
    public void UtcMidnightBoundary_MapsToBeijingNextDaySlot()
    {
        // 2026-10-05 16:30 UTC = 2026-10-06 00:30 北京时间（周二凌晨）→ trae/open/work·ds
        var utc = new DateTime(2026, 10, 5, 16, 30, 0, DateTimeKind.Utc);
        Assert.Equal(BuilderRecommendationCalculator.TraeOpenWorkDsMenuText, Menu(utc));
    }

    [Fact]
    public void LocalKindAndUtcKind_SameInstant_SameRecommendation()
    {
        var local = DateTime.Now;
        Assert.Equal(
            BuilderRecommendationCalculator.GetRecommendation(local),
            BuilderRecommendationCalculator.GetRecommendation(local.ToUniversalTime()));
    }

    [Fact]
    public void MachineTimezone_DoesNotChangeBeijingRule()
    {
        // 用 UTC-9 / UTC+9 两个极端本地时钟表达同一瞬间，推荐必须一致。
        var utc = new DateTime(2026, 10, 5, 1, 0, 0, DateTimeKind.Utc); // 北京 09:00 高峰段
        var tokyoLike = TimeZoneInfo.ConvertTimeFromUtc(utc,
            TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time"));
        Assert.Equal(
            BuilderRecommendationCalculator.GetRecommendation(utc),
            BuilderRecommendationCalculator.GetRecommendation(tokyoLike));
    }

    // ---------- NextBoundaryBeijing ----------

    [Fact]
    public void NextBoundary_WeekdayMorning_PointsTo0800()
    {
        var rec = BuilderRecommendationCalculator.GetRecommendation(WeekdayUtc(7));
        Assert.Equal(new DateTime(2026, 10, 5, 8, 0, 0), rec.NextBoundaryBeijing);
        Assert.Equal(BuilderRecommendationCalculator.OpenWorkDsMenuText,
            BuilderRecommendationCalculator.GetRecommendation(rec.NextBoundaryBeijing).MenuText);
    }

    [Fact]
    public void NextBoundary_WeekdayNoon_PointsTo1200()
    {
        var rec = BuilderRecommendationCalculator.GetRecommendation(WeekdayUtc(10));
        Assert.Equal(new DateTime(2026, 10, 5, 12, 0, 0), rec.NextBoundaryBeijing);
    }

    [Fact]
    public void NextBoundary_WeekdayLateNight_SkipsMidnight_PointsToNextDay0800()
    {
        var rec = BuilderRecommendationCalculator.GetRecommendation(WeekdayUtc(23));
        // 22:00→00:00 推荐没有变化，真正的切换点是次日 08:00。
        Assert.Equal(new DateTime(2026, 10, 6, 8, 0, 0), rec.NextBoundaryBeijing);
    }

    [Fact]
    public void NextBoundary_SaturdayEvening_PointsTo2200()
    {
        var rec = BuilderRecommendationCalculator.GetRecommendation(SaturdayUtc(20));
        Assert.Equal(new DateTime(2026, 10, 10, 22, 0, 0), rec.NextBoundaryBeijing);
    }

    [Fact]
    public void NextBoundary_SundayLateNight_PointsToMonday0800()
    {
        var rec = BuilderRecommendationCalculator.GetRecommendation(SundayUtc(23));
        Assert.Equal(new DateTime(2026, 10, 12, 8, 0, 0), rec.NextBoundaryBeijing);
    }

    // ---------- SSOT：全天候只输出允许的短文本 ----------

    [Fact]
    public void AllWeekHours_OnlyAllowedMenuTexts_NoMimoNoQwen()
    {
        var allowed = new[]
        {
            BuilderRecommendationCalculator.TraeDsMenuText,
            BuilderRecommendationCalculator.TraeOpenWorkDsMenuText,
            BuilderRecommendationCalculator.OpenWorkDsMenuText,
            BuilderRecommendationCalculator.QoderDsWorkGlmMenuText
        };
        // 覆盖周一到周日全天小时与边界分钟。
        for (int dayOffset = 0; dayOffset < 7; dayOffset++)
        {
            var day = new DateTime(2026, 10, 5).AddDays(dayOffset);
            foreach (var (hour, minute) in new[] { (0, 0), (7, 59), (8, 0), (8, 59), (9, 0), (11, 59), (12, 0), (13, 59), (14, 0), (17, 59), (18, 0), (21, 59), (22, 0), (23, 59) })
            {
                var utc = BeijingDateHourUtc(day.Year, day.Month, day.Day, hour, minute);
                var rec = BuilderRecommendationCalculator.GetRecommendation(utc);
                Assert.Contains(rec.MenuText, allowed);
                Assert.DoesNotContain("mimo", rec.MenuText, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("qwen", rec.MenuText, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Theory]
    [InlineData(0, "trae/open/work·ds", "trae-open-work-ds")]
    [InlineData(8, "open/work·ds", "open-work-ds")]
    [InlineData(10, "qoder·ds/work·glm", "qoder-ds-work-glm")]
    [InlineData(12, "open/work·ds", "open-work-ds")]
    [InlineData(23, "trae/open/work·ds", "trae-open-work-ds")]
    public void Keys_MatchMenuTexts(int hour, string menuText, string key)
    {
        var rec = BuilderRecommendationCalculator.GetRecommendation(WeekdayUtc(hour));
        Assert.Equal(menuText, rec.MenuText);
        Assert.Equal(key, rec.Key);
    }

    [Fact]
    public void SameMenuText_SameKey_AcrossDaysAndWeekend()
    {
        var weekdayNight = BuilderRecommendationCalculator.GetRecommendation(WeekdayUtc(2));
        var weekendNight = BuilderRecommendationCalculator.GetRecommendation(SaturdayUtc(2));
        Assert.Equal(weekdayNight.Key, weekendNight.Key);
        Assert.Equal(weekdayNight.MenuText, weekendNight.MenuText);
    }
}
