using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using DeepSeekBalanceWidget.Models;

namespace DeepSeekBalanceWidget.Services;

public sealed record BalanceParseResult(
    IReadOnlyList<ParsedBalance> Balances,
    bool IsConsistent,
    string? Error)
{
    public bool Success => Error is null;
    public static BalanceParseResult Fail(string error)
        => new(Array.Empty<ParsedBalance>(), false, error);
}

public static class BalanceParser
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static BalanceParseResult Parse(string json)
    {
        try
        {
            var resp = JsonSerializer.Deserialize<BalanceResponse>(json, JsonOpts);
            if (resp is null || resp.BalanceInfos.Count == 0)
                return BalanceParseResult.Fail("余额数据为空");

            var list = new List<ParsedBalance>();
            bool anyInconsistent = false;

            foreach (var info in resp.BalanceInfos)
            {
                if (string.IsNullOrWhiteSpace(info.Currency))
                    return BalanceParseResult.Fail("存在币种为空的条目");
                if (!TryAmount(info.TotalBalance, out var total))
                    return BalanceParseResult.Fail($"币种 {info.Currency} 总余额非法");
                if (!TryAmount(info.GrantedBalance, out var granted))
                    return BalanceParseResult.Fail($"币种 {info.Currency} 赠送余额非法");
                if (!TryAmount(info.ToppedUpBalance, out var topped))
                    return BalanceParseResult.Fail($"币种 {info.Currency} 充值余额非法");

                if (Math.Abs(total - (granted + topped)) > 0.01m) anyInconsistent = true;
                list.Add(new ParsedBalance(info.Currency, total, granted, topped, resp.IsAvailable));
            }

            return new BalanceParseResult(list, !anyInconsistent, null);
        }
        catch (JsonException)
        {
            return BalanceParseResult.Fail("JSON 解析失败");
        }
    }

    /// <summary>
    /// 金额解析。允许负数：DeepSeek 在欠费（is_available=false）时会返回负的
    /// total_balance / topped_up_balance，例如 "-0.71"，这不是非法数据。
    /// </summary>
    private static bool TryAmount(string s, out decimal value)
        => decimal.TryParse(s,
               NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
               CultureInfo.InvariantCulture, out value);
}
