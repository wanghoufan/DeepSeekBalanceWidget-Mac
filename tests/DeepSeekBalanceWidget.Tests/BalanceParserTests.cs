using DeepSeekBalanceWidget.Services;
using Xunit;

namespace DeepSeekBalanceWidget.Tests;

public class BalanceParserTests
{
    [Fact]
    public void Parse_NormalCny_ReturnsParsed()
    {
        var json = """{"is_available":true,"balance_infos":[{"currency":"CNY","total_balance":"110.00","granted_balance":"10.00","topped_up_balance":"100.00"}]}""";
        var r = BalanceParser.Parse(json);
        Assert.True(r.Success);
        Assert.Single(r.Balances);
        Assert.Equal("CNY", r.Balances[0].Currency);
        Assert.Equal(110m, r.Balances[0].Total);
        Assert.True(r.IsConsistent);
    }

    [Fact]
    public void Parse_EmptyArray_Fails()
    {
        var json = """{"is_available":true,"balance_infos":[]}""";
        Assert.False(BalanceParser.Parse(json).Success);
    }

    [Fact]
    public void Parse_MissingBalanceInfos_Fails()
    {
        var json = """{"is_available":true}""";
        Assert.False(BalanceParser.Parse(json).Success);
    }

    [Fact]
    public void Parse_UnknownCurrency_NotRejected()
    {
        var json = """{"is_available":true,"balance_infos":[{"currency":"EUR","total_balance":"5.00","granted_balance":"1.00","topped_up_balance":"4.00"}]}""";
        var r = BalanceParser.Parse(json);
        Assert.True(r.Success);
        Assert.Equal("EUR", r.Balances[0].Currency);
    }

    [Fact]
    public void Parse_InvalidAmount_Fails()
    {
        var json = """{"is_available":true,"balance_infos":[{"currency":"CNY","total_balance":"abc","granted_balance":"1","topped_up_balance":"2"}]}""";
        Assert.False(BalanceParser.Parse(json).Success);
    }

    [Fact]
    public void Parse_NegativeAmount_Succeeds()
    {
        // 欠费时 DeepSeek 会返回负余额（is_available=false），这是合法数据而非脏数据。
        var json = """{"is_available":false,"balance_infos":[{"currency":"CNY","total_balance":"-0.71","granted_balance":"0.00","topped_up_balance":"-0.71"}]}""";
        var r = BalanceParser.Parse(json);
        Assert.True(r.Success);
        Assert.Equal(-0.71m, r.Balances[0].Total);
        Assert.Equal(-0.71m, r.Balances[0].ToppedUp);
        Assert.False(r.Balances[0].IsAvailable);
    }

    [Fact]
    public void Parse_SumTolerance_MarksInconsistentNotFail()
    {
        var json = """{"is_available":true,"balance_infos":[{"currency":"CNY","total_balance":"110.02","granted_balance":"10.00","topped_up_balance":"100.00"}]}""";
        var r = BalanceParser.Parse(json);
        Assert.True(r.Success);
        Assert.False(r.IsConsistent);
    }

    [Fact]
    public void Parse_ZeroBalance_IsValidBaseline()
    {
        var json = """{"is_available":true,"balance_infos":[{"currency":"CNY","total_balance":"0","granted_balance":"0","topped_up_balance":"0"}]}""";
        var r = BalanceParser.Parse(json);
        Assert.True(r.Success);
        Assert.Equal(0m, r.Balances[0].Total);
    }
}
