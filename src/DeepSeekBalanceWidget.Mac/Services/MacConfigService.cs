using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DeepSeekBalanceWidget.Models;

namespace DeepSeekBalanceWidget.Services;

/// <summary>
/// macOS configuration store. The JSON file contains ordinary preferences only;
/// the API key itself is stored in the user's login Keychain.
/// </summary>
public sealed class MacConfigService
{
    private const string KeychainService = "com.deepseekbalancewidget.api-key";
    private const string OpenCodeKeychainService = "com.deepseekbalancewidget.opencode-api-key";
    private const string OpenRouterKeychainService = "com.deepseekbalancewidget.openrouter-api-key";
    private const string KeychainAccount = "default";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _directory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "Application Support", "DeepSeekBalanceWidget");
    private readonly string _filePath;
    private readonly object _writeLock = new();
    private AppConfig? _lastConfig;

    public MacConfigService() => _filePath = Path.Combine(_directory, "config.json");

    public AppConfig Load()
    {
        AppConfig config;
        try
        {
            if (!File.Exists(_filePath))
            {
                config = new AppConfig();
            }
            else
            {
                config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(_filePath), JsonOptions)
                    ?? new AppConfig();
            }
        }
        catch
        {
            TryBackupCorruptFile();
            config = new AppConfig();
        }

        config.Normalize();
        _lastConfig = config;

        // Reinstallations can leave the login Keychain intact while the new
        // config.json loses its storage markers. Restore those markers before
        // the app creates its providers, then persist the repaired config.
        bool repaired = false;
        if (string.IsNullOrWhiteSpace(config.ApiKeyEncrypted)
            && HasKeychainEntry(KeychainService))
        {
            config.ApiKeyEncrypted = "keychain";
            repaired = true;
        }

        if (string.IsNullOrWhiteSpace(config.OpenCodeApiKeyEncrypted)
            && HasKeychainEntry(OpenCodeKeychainService))
        {
            config.OpenCodeApiKeyEncrypted = "keychain";
            repaired = true;
        }

        if (string.IsNullOrWhiteSpace(config.OpenRouterApiKeyEncrypted)
            && HasKeychainEntry(OpenRouterKeychainService))
        {
            config.OpenRouterApiKeyEncrypted = "keychain";
            repaired = true;
        }

        if (repaired)
        {
            try
            {
                Save(config);
            }
            catch
            {
                // Keep the repaired markers in memory for this run. A later
                // settings save can persist them if the config directory was
                // temporarily unavailable during startup.
            }
        }

        return config;
    }

    public void Save(AppConfig config)
    {
        lock (_writeLock)
        {
            Directory.CreateDirectory(_directory);
            string temporaryPath = _filePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(config, JsonOptions), new UTF8Encoding(false));
            File.Move(temporaryPath, _filePath, overwrite: true);
            _lastConfig = config;
        }
    }

    public string? GetApiKey()
    {
        if (!string.Equals(_lastConfig?.ApiKeyEncrypted, "keychain", StringComparison.Ordinal))
            return null;

        return RunSecurity("find-generic-password", "-s", KeychainService, "-a", KeychainAccount, "-w")
            ?.TrimEnd('\r', '\n');
    }

    public void SetApiKey(AppConfig config, string? value)
    {
        lock (_writeLock)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                RunSecurity("delete-generic-password", "-s", KeychainService, "-a", KeychainAccount);
                config.ApiKeyEncrypted = null;
            }
            else
            {
                WriteKeychainValue(KeychainService, value);
                config.ApiKeyEncrypted = "keychain";
            }

            Save(config);
        }
    }

    /// <summary>
    /// OpenCode Go Key 的 macOS 存储。配置 JSON 仅保存标记，真实 Key 放在登录钥匙串，
    /// 与 DeepSeek Key 使用不同的 service，避免两项凭据互相覆盖。
    /// </summary>
    public string? GetOpenCodeApiKey()
    {
        if (!string.Equals(_lastConfig?.OpenCodeApiKeyEncrypted, "keychain", StringComparison.Ordinal))
            return null;

        return RunSecurity("find-generic-password", "-s", OpenCodeKeychainService, "-a", KeychainAccount, "-w")
            ?.TrimEnd('\r', '\n');
    }

    public void SetOpenCodeApiKey(AppConfig config, string? value)
    {
        lock (_writeLock)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                RunSecurity("delete-generic-password", "-s", OpenCodeKeychainService, "-a", KeychainAccount);
                config.OpenCodeApiKeyEncrypted = null;
            }
            else
            {
                WriteKeychainValue(OpenCodeKeychainService, value);
                config.OpenCodeApiKeyEncrypted = "keychain";
            }

            // Keep the Keychain write and its config marker in one serialized
            // operation so a caller cannot persist a key without its marker.
            Save(config);
        }
    }

    /// <summary>OpenRouter API Key 的 macOS 登录钥匙串存储。</summary>
    public string? GetOpenRouterApiKey()
    {
        if (!string.Equals(_lastConfig?.OpenRouterApiKeyEncrypted, "keychain", StringComparison.Ordinal))
            return null;

        return RunSecurity("find-generic-password", "-s", OpenRouterKeychainService, "-a", KeychainAccount, "-w")
            ?.TrimEnd('\r', '\n');
    }

    public void SetOpenRouterApiKey(AppConfig config, string? value)
    {
        lock (_writeLock)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                RunSecurity("delete-generic-password", "-s", OpenRouterKeychainService, "-a", KeychainAccount);
                config.OpenRouterApiKeyEncrypted = null;
            }
            else
            {
                WriteKeychainValue(OpenRouterKeychainService, value);
                config.OpenRouterApiKeyEncrypted = "keychain";
            }

            Save(config);
        }
    }

    private static bool HasKeychainEntry(string service)
    {
        // Do not use `-w` for the startup probe: checking the item metadata is
        // enough and avoids copying a real credential into a managed string.
        return RunSecurity("find-generic-password", "-s", service, "-a", KeychainAccount) is not null;
    }

    private static void WriteKeychainValue(string service, string value)
    {
        // `security` is the supported Keychain command-line client. It never
        // writes the password to stdout/stderr; passing it as an argument also
        // avoids a shell.
        if (RunSecurity("add-generic-password", "-U", "-s", service,
            "-a", KeychainAccount, "-w", value) is null)
        {
            throw new InvalidOperationException("无法写入 macOS 钥匙串，请在“钥匙串访问”中检查权限。");
        }
    }

    private void TryBackupCorruptFile()
    {
        try
        {
            if (File.Exists(_filePath))
                File.Move(_filePath, _filePath + $".corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}.bak");
        }
        catch { }
    }

    /// <summary>security 命令超时。超过说明弹出了钥匙串授权框等待人工确认，不能让 UI 线程永久挂起。</summary>
    private static readonly TimeSpan SecurityTimeout = TimeSpan.FromSeconds(15);

    private static string? RunSecurity(params string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo("/usr/bin/security")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo);
            if (process is null) return null;
            if (!process.WaitForExit(SecurityTimeout))
            {
                try { process.Kill(); } catch { /* 已退出的进程无需处理 */ }
                return null;
            }
            string output = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            return process.ExitCode == 0 ? output : null;
        }
        catch { return null; }
    }
}
