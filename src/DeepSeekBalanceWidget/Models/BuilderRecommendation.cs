using System;

namespace DeepSeekBalanceWidget.Models;

/// <summary>
/// Builder 时间调度推荐结果。
/// <see cref="Key"/> 稳定且唯一，用于菜单栏提醒去重；<see cref="MenuText"/> 是菜单栏短文本；
/// <see cref="Reason"/> 只用于 Tooltip 与提醒正文；<see cref="NextBoundaryBeijing"/> 为北京时间下一次切换点。
/// </summary>
public sealed record BuilderRecommendation(
    string Key,
    string MenuText,
    string Reason,
    DateTime NextBoundaryBeijing);
