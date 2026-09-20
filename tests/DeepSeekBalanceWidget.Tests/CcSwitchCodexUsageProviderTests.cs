using System.Net;
using System.Text;
using System.Text.Json;
using DeepSeekBalanceWidget.Services;

namespace DeepSeekBalanceWidget.Tests;

public sealed class CcSwitchCodexUsageProviderTests
{
    [Fact]
    public async Task GetUsagesAsync_WhenLiveProbeEnabled_ReturnsTwoCcSwitchAccounts()
    {
        if (Environment.GetEnvironmentVariable("RUN_CCSWITCH_LIVE_TEST") != "1")
            return;

        string path = Environment.GetEnvironmentVariable("CC_SWITCH_AUTH_PATH")
            ?? throw new InvalidOperationException("CC_SWITCH_AUTH_PATH is required");
        using var provider = new CcSwitchCodexUsageProvider(path);

        var accounts = await provider.GetUsagesAsync(CancellationToken.None);

        Assert.Equal(2, accounts.Count);
        Assert.All(accounts, account =>
        {
            Assert.True(account.Usage.IsAvailable, account.RefreshError ?? account.Usage.Error);
            Assert.NotEmpty(account.Usage.Windows);
        });
    }

    [Fact]
    public void ParseUsage_ConvertsSevenDayUtilizationToRemaining()
    {
        const string json = """
        {
          "rate_limit": {
            "primary_window": {
              "used_percent": 42,
              "limit_window_seconds": 604800,
              "reset_at": 1786164619
            },
            "secondary_window": null
          }
        }
        """;

        var usage = CcSwitchCodexUsageProvider.ParseUsage(json);

        Assert.True(usage.IsAvailable);
        var window = Assert.Single(usage.Windows);
        Assert.Equal(42, window.UsedPercent);
        Assert.Equal(58, window.RemainingPercent);
        Assert.Equal(10080, window.DurationMinutes);
        Assert.NotNull(window.ResetsAt);
    }

    [Theory]
    [InlineData("mortimerstephanie14@gmail.com", "M")]
    [InlineData("wanghoufan13@gmail.com", "W")]
    public void CreateMiniLabel_UsesFirstEmailCharacter(string email, string expected)
    {
        Assert.Equal(expected, CcSwitchCodexUsageProvider.CreateMiniLabel(email));
    }

    [Fact]
    public async Task GetUsagesAsync_ReadsBothAccountsAndKeepsLastValuesWhenRefreshFails()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"cc-switch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string storePath = Path.Combine(tempDir, "codex_oauth_auth.json");
        // Codex 原生凭证路径必须显式指到空目录里：缺省会读真实的 ~/.codex/auth.json，
        // 把开发者本机的登录账号混进断言（本地 3 个、CI 2 个 → 结果不一致）。
        string codexAuthPath = Path.Combine(tempDir, "codex-native-absent.json");
        await File.WriteAllTextAsync(storePath, """
        {
          "version": 1,
          "accounts": {
            "account-m": {
              "account_id": "account-m",
              "email": "mortimerstephanie14@gmail.com",
              "refresh_token": "refresh-m",
              "authenticated_at": 1
            },
            "account-w": {
              "account_id": "account-w",
              "email": "wanghoufan13@gmail.com",
              "refresh_token": "refresh-w",
              "authenticated_at": 1
            }
          }
        }
        """);

        try
        {
            var handler = new FakeHandler();
            using var client = new HttpClient(handler);
            using var provider = new CcSwitchCodexUsageProvider(storePath, client, codexAuthPath);

            var first = await provider.GetUsagesAsync(CancellationToken.None);

            Assert.Equal(2, first.Count);
            Assert.Collection(first,
                account =>
                {
                    Assert.Equal("M", account.MiniLabel);
                    Assert.Equal(92, Assert.Single(account.Usage.Windows).RemainingPercent);
                    Assert.False(account.IsStale);
                },
                account =>
                {
                    Assert.Equal("W", account.MiniLabel);
                    Assert.Equal(58, Assert.Single(account.Usage.Windows).RemainingPercent);
                    Assert.False(account.IsStale);
                });

            handler.FailUsageRequests = true;
            var stale = await provider.GetUsagesAsync(CancellationToken.None);

            Assert.All(stale, account => Assert.True(account.IsStale));
            Assert.Equal(92, Assert.Single(stale[0].Usage.Windows).RemainingPercent);
            Assert.Equal(58, Assert.Single(stale[1].Usage.Windows).RemainingPercent);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public bool FailUsageRequests { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsoluteUri == "https://auth.openai.com/oauth/token")
            {
                string form = await request.Content!.ReadAsStringAsync(cancellationToken);
                string access = form.Contains("refresh-m", StringComparison.Ordinal)
                    ? "access-m"
                    : "access-w";
                return Json(HttpStatusCode.OK, JsonSerializer.Serialize(new
                {
                    access_token = access,
                    expires_in = 3600
                }));
            }

            if (FailUsageRequests)
                return Json(HttpStatusCode.InternalServerError, "{}");

            string accountId = request.Headers.GetValues("ChatGPT-Account-Id").Single();
            int used = accountId == "account-m" ? 8 : 42;
            return Json(HttpStatusCode.OK, JsonSerializer.Serialize(new
            {
                rate_limit = new
                {
                    primary_window = new
                    {
                        used_percent = used,
                        limit_window_seconds = 604800,
                        reset_at = 1786164619
                    }
                }
            }));
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string json)
            => new(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
    }
}
