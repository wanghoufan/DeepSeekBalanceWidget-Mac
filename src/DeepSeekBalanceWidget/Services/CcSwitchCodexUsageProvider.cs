using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeepSeekBalanceWidget.Models;

namespace DeepSeekBalanceWidget.Services;

public sealed class CcSwitchCodexUsageProvider : ICodexAccountsUsageProvider, IDisposable
{
    private const string OAuthTokenUrl = "https://auth.openai.com/oauth/token";
    private const string UsageUrl = "https://chatgpt.com/backend-api/wham/usage";
    private const string CodexClientId = "app_EMoamEEZ73f0CkXaXp7hrann";
    private static readonly TimeSpan AccessTokenRefreshBuffer = TimeSpan.FromMinutes(1);
    private static readonly SemaphoreSlim StoreWriteLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
    private static readonly string DefaultCcSwitchStorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".cc-switch",
        "codex_oauth_auth.json");
    /// <summary>
    /// Codex CLI 迁移后的登录凭证位置。旧版 CC Switch 会生成
    /// <c>~/.cc-switch/codex_oauth_auth.json</c>，但升级后该文件不再产生，
    /// 凭证改由 Codex 自己管理，必须兼容读取否则 GPT 额度永久失效。
    /// </summary>
    private static readonly string DefaultCodexAuthPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".codex",
        "auth.json");

    private readonly string _storePath;
    private readonly string _codexAuthPath;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly ConcurrentDictionary<string, CachedAccessToken> _accessTokens = new();
    private readonly ConcurrentDictionary<string, CodexAccountUsageSnapshot> _lastSuccessful = new();

    public CcSwitchCodexUsageProvider(
        string? storePath = null,
        HttpClient? httpClient = null,
        string? codexAuthPath = null)
    {
        _storePath = storePath ?? DefaultCcSwitchStorePath;
        _codexAuthPath = codexAuthPath ?? DefaultCodexAuthPath;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _ownsHttpClient = httpClient is null;
    }

    public async Task<IReadOnlyList<CodexAccountUsageSnapshot>> GetUsagesAsync(
        CancellationToken cancellationToken)
    {
        List<CcSwitchAccount> accounts = await LoadAccountsAsync(cancellationToken);
        if (accounts.Count == 0)
            return MarkAllStale("未找到 ChatGPT 登录凭证（已尝试 CC Switch 与 Codex 本地凭证）");

        var tasks = accounts.Select(account => RefreshAccountAsync(account, cancellationToken));
        return await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 合并两个凭证来源：CC Switch 的旧 json 与 Codex 原生 auth.json。
    /// 任一来源不可用都不影响另一个——此前硬编码只读 CC Switch，
    /// 该文件消失后整个 GPT 额度链路静默失效。
    /// </summary>
    private async Task<List<CcSwitchAccount>> LoadAccountsAsync(CancellationToken cancellationToken)
    {
        var accounts = new List<CcSwitchAccount>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (File.Exists(_storePath))
            {
                var store = await ReadStoreAsync(_storePath, cancellationToken);
                foreach (CcSwitchAccount account in store.Accounts.Values)
                {
                    if (!IsUsable(account) || !seen.Add(account.AccountId)) continue;
                    account.Source = StoreKind.CcSwitch;
                    account.StorePath = _storePath;
                    accounts.Add(account);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // 旧来源不可用是预期情况，继续尝试 Codex 原生凭证。
        }

        try
        {
            if (File.Exists(_codexAuthPath))
            {
                var account = await ReadCodexNativeAccountAsync(_codexAuthPath, cancellationToken);
                if (account is not null && IsUsable(account) && seen.Add(account.AccountId))
                {
                    account.Source = StoreKind.CodexNative;
                    account.StorePath = _codexAuthPath;
                    accounts.Add(account);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // 同上，两个来源都失败时由调用方统一报「未找到凭证」。
        }

        return accounts
            .OrderBy(account => account.Email, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsUsable(CcSwitchAccount account)
        => !string.IsNullOrWhiteSpace(account.AccountId)
           && !string.IsNullOrWhiteSpace(account.RefreshToken);

    /// <summary>
    /// 读取 Codex CLI 的 auth.json。该文件不含 email 字段，
    /// 账号显示所需的邮箱从 id_token 的 JWT payload 中解出。
    /// </summary>
    private async Task<CcSwitchAccount?> ReadCodexNativeAccountAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var file = await ReadJsonAsync<CodexNativeAuthFile>(path, cancellationToken);
        CodexNativeTokens? tokens = file?.Tokens;
        if (tokens is null) return null;
        if (string.IsNullOrWhiteSpace(tokens.AccountId)
            || string.IsNullOrWhiteSpace(tokens.RefreshToken))
        {
            return null;
        }

        return new CcSwitchAccount
        {
            AccountId = tokens.AccountId,
            Email = ExtractEmailFromIdToken(tokens.IdToken) ?? tokens.AccountId,
            RefreshToken = tokens.RefreshToken,
            AuthenticatedAt = ParseUnixSeconds(ExtractJwtClaim(tokens.IdToken, "iat")) ?? 0
        };
    }

    private static string? ExtractEmailFromIdToken(string? idToken)
    {
        string? email = ExtractJwtClaim(idToken, "email");
        return string.IsNullOrWhiteSpace(email) ? null : email;
    }

    /// <summary>解 JWT payload 中的指定 claim；解不出时返回 null，绝不抛异常。</summary>
    private static string? ExtractJwtClaim(string? token, string claimName)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        string[] parts = token.Split('.');
        if (parts.Length < 2) return null;

        try
        {
            string payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
            if (document.RootElement.TryGetProperty(claimName, out var value)
                && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }

        return null;
    }

    private static long? ParseUnixSeconds(string? seconds)
        => long.TryParse(seconds, out long value) ? value : null;

    private async Task<CodexAccountUsageSnapshot> RefreshAccountAsync(
        CcSwitchAccount account,
        CancellationToken cancellationToken)
    {
        try
        {
            string accessToken = await GetAccessTokenAsync(account, cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Get, UsageUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.UserAgent.ParseAdd("codex-cli");
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.Add("ChatGPT-Account-Id", account.AccountId);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new InvalidOperationException("CC Switch 登录已过期");
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"额度接口返回 HTTP {(int)response.StatusCode}");

            string json = await response.Content.ReadAsStringAsync(cancellationToken);
            var usage = ParseUsage(json);
            if (!usage.IsAvailable || usage.Windows.Count == 0)
                throw new InvalidOperationException(usage.Error ?? "未返回额度窗口");

            var snapshot = new CodexAccountUsageSnapshot(
                account.AccountId,
                account.Email!,
                CreateMiniLabel(account.Email!),
                usage,
                DateTimeOffset.Now,
                IsStale: false);
            _lastSuccessful[account.AccountId] = snapshot;
            return snapshot;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException
            or InvalidOperationException
            or JsonException
            or TaskCanceledException)
        {
            if (_lastSuccessful.TryGetValue(account.AccountId, out var previous))
                return previous with { IsStale = true, RefreshError = SafeError(ex) };

            return new CodexAccountUsageSnapshot(
                account.AccountId,
                account.Email!,
                CreateMiniLabel(account.Email!),
                CodexUsageSnapshot.Unavailable(SafeError(ex)),
                UpdatedAt: null,
                IsStale: true,
                RefreshError: SafeError(ex));
        }
    }

    private async Task<string> GetAccessTokenAsync(
        CcSwitchAccount account,
        CancellationToken cancellationToken)
    {
        if (_accessTokens.TryGetValue(account.AccountId, out var cached)
            && cached.ExpiresAt - DateTimeOffset.UtcNow > AccessTokenRefreshBuffer)
        {
            return cached.Token;
        }

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = account.RefreshToken,
            ["client_id"] = CodexClientId,
            ["scope"] = "openid profile email"
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, OAuthTokenUrl) { Content = content };
        request.Headers.UserAgent.ParseAdd("cc-switch-codex-oauth");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new InvalidOperationException("CC Switch 登录已过期");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"登录刷新返回 HTTP {(int)response.StatusCode}");

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokens = JsonSerializer.Deserialize<OAuthTokenResponse>(json, JsonOptions);
        if (string.IsNullOrWhiteSpace(tokens?.AccessToken))
            throw new JsonException("登录刷新未返回访问令牌");

        int expiresIn = tokens.ExpiresIn.GetValueOrDefault(3600);
        _accessTokens[account.AccountId] = new CachedAccessToken(
            tokens.AccessToken,
            DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, expiresIn)));

        await PersistRefreshedTokensAsync(
            account,
            tokens.AccessToken,
            tokens.RefreshToken,
            cancellationToken);

        return tokens.AccessToken;
    }

    /// <summary>
    /// 续期后写回凭证。两个来源的落盘格式完全不同：
    /// CC Switch 用 {version, accounts:{...}}，Codex 原生用 {auth_mode, tokens:{...}}。
    /// 若用 CC Switch 的序列化结果覆写 Codex 的 auth.json，会直接毁掉 Codex CLI 的凭证，
    /// 因此必须按来源分流。Codex 侧还要维持 600 权限——Codex 会拒绝读取权限过松的凭证文件。
    /// </summary>
    private async Task PersistRefreshedTokensAsync(
        CcSwitchAccount account,
        string accessToken,
        string? refreshedToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshedToken)
            || string.Equals(refreshedToken, account.RefreshToken, StringComparison.Ordinal))
        {
            return;
        }

        string oldToken = account.RefreshToken;
        await StoreWriteLock.WaitAsync(cancellationToken);
        try
        {
            if (account.Source == StoreKind.CodexNative)
            {
                await UpdateCodexNativeTokensAsync(
                    account, oldToken, accessToken, refreshedToken, cancellationToken);
            }
            else
            {
                await UpdateCcSwitchRefreshTokenAsync(
                    account, oldToken, refreshedToken, cancellationToken);
            }

            account.RefreshToken = refreshedToken;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // 落盘失败不影响本次会话：内存中的新 token 仍然有效，
            // 下一次续期会重试写回。
        }
        finally
        {
            StoreWriteLock.Release();
        }
    }

    private async Task UpdateCcSwitchRefreshTokenAsync(
        CcSwitchAccount account,
        string oldToken,
        string newToken,
        CancellationToken cancellationToken)
    {
        var store = await ReadJsonAsync<CcSwitchOAuthStore>(
            account.StorePath, cancellationToken);
        if (store is null) return;
        if (!store.Accounts.TryGetValue(account.AccountId, out var storedAccount)) return;
        if (!string.Equals(storedAccount.RefreshToken, oldToken, StringComparison.Ordinal)) return;

        storedAccount.RefreshToken = newToken;
        await WriteJsonAtomicAsync(
            account.StorePath, store, restrictPermissions: false, cancellationToken);
    }

    private async Task UpdateCodexNativeTokensAsync(
        CcSwitchAccount account,
        string oldToken,
        string accessToken,
        string newToken,
        CancellationToken cancellationToken)
    {
        var file = await ReadJsonAsync<CodexNativeAuthFile>(
            account.StorePath, cancellationToken);
        if (file?.Tokens is null) return;
        if (!string.Equals(file.Tokens.RefreshToken, oldToken, StringComparison.Ordinal)) return;

        file.Tokens.RefreshToken = newToken;
        if (!string.IsNullOrWhiteSpace(accessToken))
            file.Tokens.AccessToken = accessToken;
        file.LastRefresh = DateTimeOffset.UtcNow.ToString(
            "yyyy-MM-ddTHH:mm:ss.ffffffZ", CultureInfo.InvariantCulture);

        await WriteJsonAtomicAsync(
            account.StorePath, file, restrictPermissions: true, cancellationToken);
    }

    private async Task<CcSwitchOAuthStore> ReadStoreAsync(
        string path,
        CancellationToken cancellationToken)
        => await ReadJsonAsync<CcSwitchOAuthStore>(path, cancellationToken)
           ?? throw new JsonException("CC Switch 账号文件为空");

    private static async Task<T?> ReadJsonAsync<T>(string path, CancellationToken cancellationToken)
        where T : class
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 4096,
            useAsync: true);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }

    /// <summary>
    /// 原子写回：先写临时文件再 rename，避免写一半崩溃损坏凭证。
    /// <paramref name="restrictPermissions"/> 为 true 时强制 600 权限（rename 会保留该权限），
    /// Codex CLI 会拒绝读取权限过松的凭证文件。
    /// </summary>
    private static async Task WriteJsonAtomicAsync<T>(
        string path,
        T value,
        bool restrictPermissions,
        CancellationToken cancellationToken)
    {
        string directory = Path.GetDirectoryName(path)!;
        string tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            if (restrictPermissions && !OperatingSystem.IsWindows())
            {
                try
                {
                    File.SetUnixFileMode(
                        tempPath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
                catch (Exception ex) when (ex is IOException or PlatformNotSupportedException)
                {
                    // 权限设置失败不应阻止写回。
                }
            }

            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); }
                catch (IOException) { }
            }
        }
    }

    internal static CodexUsageSnapshot ParseUsage(string json)
    {
        var response = JsonSerializer.Deserialize<CodexUsageResponse>(json, JsonOptions);
        var windows = new List<CodexUsageWindow>();
        AddWindow(response?.RateLimit?.PrimaryWindow, windows);
        AddWindow(response?.RateLimit?.SecondaryWindow, windows);
        return windows.Count == 0
            ? CodexUsageSnapshot.Unavailable("未返回 ChatGPT 额度窗口")
            : new CodexUsageSnapshot(true, null, windows, null);
    }

    internal static string CreateMiniLabel(string email)
    {
        char label = email.Trim().FirstOrDefault(char.IsLetterOrDigit);
        return label == default ? "?" : char.ToUpperInvariant(label).ToString();
    }

    private static void AddWindow(CodexRateLimitWindow? source, ICollection<CodexUsageWindow> target)
    {
        if (source?.UsedPercent is not double used) return;
        int normalizedUsed = Math.Clamp((int)Math.Round(used), 0, 100);
        int? durationMinutes = source.LimitWindowSeconds is long seconds
            ? (int)Math.Clamp(seconds / 60, 1, int.MaxValue)
            : null;
        DateTimeOffset? resetsAt = null;
        if (source.ResetAt is long timestamp)
        {
            try { resetsAt = DateTimeOffset.FromUnixTimeSeconds(timestamp); }
            catch (ArgumentOutOfRangeException) { }
        }
        target.Add(new CodexUsageWindow(
            normalizedUsed,
            100 - normalizedUsed,
            durationMinutes,
            resetsAt));
    }

    private IReadOnlyList<CodexAccountUsageSnapshot> MarkAllStale(string error)
        => _lastSuccessful.Values
            .OrderBy(snapshot => snapshot.Email, StringComparer.OrdinalIgnoreCase)
            .Select(snapshot => snapshot with { IsStale = true, RefreshError = error })
            .ToArray();

    private static string SafeError(Exception ex) => ex switch
    {
        TaskCanceledException => "ChatGPT 额度读取超时",
        _ => ex.Message
    };

    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
    }

    private sealed record CachedAccessToken(string Token, DateTimeOffset ExpiresAt);

    private sealed class CcSwitchOAuthStore
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("accounts")]
        public Dictionary<string, CcSwitchAccount> Accounts { get; set; } = new();

        [JsonPropertyName("default_account_id")]
        public string? DefaultAccountId { get; set; }
    }

    private enum StoreKind
    {
        /// <summary>旧版 CC Switch 的 codex_oauth_auth.json。</summary>
        CcSwitch,

        /// <summary>Codex CLI 自管的 ~/.codex/auth.json。</summary>
        CodexNative
    }

    private sealed class CcSwitchAccount
    {
        [JsonPropertyName("account_id")]
        public string AccountId { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonPropertyName("authenticated_at")]
        public long AuthenticatedAt { get; set; }

        /// <summary>凭证来源，决定续期时按哪种格式写回。</summary>
        [JsonIgnore]
        public StoreKind Source { get; set; }

        /// <summary>该账号实际读取的文件路径。</summary>
        [JsonIgnore]
        public string StorePath { get; set; } = string.Empty;
    }

    /// <summary>Codex CLI 的 ~/.codex/auth.json 结构。</summary>
    private sealed class CodexNativeAuthFile
    {
        [JsonPropertyName("auth_mode")]
        public string? AuthMode { get; set; }

        [JsonPropertyName("OPENAI_API_KEY")]
        public string? OpenAiApiKey { get; set; }

        [JsonPropertyName("tokens")]
        public CodexNativeTokens? Tokens { get; set; }

        [JsonPropertyName("last_refresh")]
        public string? LastRefresh { get; set; }
    }

    private sealed class CodexNativeTokens
    {
        [JsonPropertyName("id_token")]
        public string? IdToken { get; set; }

        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("account_id")]
        public string? AccountId { get; set; }
    }

    private sealed class OAuthTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; set; }
    }

    private sealed class CodexUsageResponse
    {
        [JsonPropertyName("rate_limit")]
        public CodexRateLimit? RateLimit { get; set; }
    }

    private sealed class CodexRateLimit
    {
        [JsonPropertyName("primary_window")]
        public CodexRateLimitWindow? PrimaryWindow { get; set; }

        [JsonPropertyName("secondary_window")]
        public CodexRateLimitWindow? SecondaryWindow { get; set; }
    }

    private sealed class CodexRateLimitWindow
    {
        [JsonPropertyName("used_percent")]
        public double? UsedPercent { get; set; }

        [JsonPropertyName("limit_window_seconds")]
        public long? LimitWindowSeconds { get; set; }

        [JsonPropertyName("reset_at")]
        public long? ResetAt { get; set; }
    }
}
