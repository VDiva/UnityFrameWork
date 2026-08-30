using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;

namespace WebSocketDemo;

public readonly record struct WeChatLoginResult(
    bool Success, string OpenId, string ErrorCode, string ErrorMessage);

/// <summary>在服务端使用微信临时登录 code 换取可信 openid。</summary>
public static class WeChatLoginService
{
    public const string SessionTokenKey = "__wechat_session";
    public const string SessionInvalidKey = "__wechat_session_invalid";
    // 仅保存在服务进程中；重启后旧凭证失效，客户端自动回退到微信登录。
    private static readonly byte[] SessionSigningKey = RandomNumberGenerator.GetBytes(32);

    public static string CreateSessionToken(string openId)
    {
        string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(openId)) + "." +
            DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        return payload + "." + Convert.ToBase64String(
            HMACSHA256.HashData(SessionSigningKey, Encoding.UTF8.GetBytes(payload)));
    }

    public static WeChatLoginResult ValidateSessionToken(string token, string openId)
    {
        var invalid = Failure("WECHAT_SESSION_EXPIRED", "登录会话已失效，请重新登录。");
        if (string.IsNullOrWhiteSpace(token) || token.Length > 1024) return invalid;
        string[] parts = token.Split('.');
        if (parts.Length != 3 || !long.TryParse(parts[1], NumberStyles.None,
                CultureInfo.InvariantCulture, out long expires) ||
            expires <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return invalid;
        try
        {
            byte[] expected = HMACSHA256.HashData(SessionSigningKey,
                Encoding.UTF8.GetBytes(parts[0] + "." + parts[1]));
            if (!CryptographicOperations.FixedTimeEquals(expected, Convert.FromBase64String(parts[2])))
                return invalid;
            string verifiedId = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
            if (string.IsNullOrWhiteSpace(verifiedId) || !string.Equals(verifiedId, openId, StringComparison.Ordinal))
                return invalid;
            return new WeChatLoginResult(true, verifiedId, string.Empty, string.Empty);
        }
        catch (FormatException) { return invalid; }
    }

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private static string _appId = string.Empty;
    private static string _appSecret = string.Empty;
    public static bool AllowInsecureDevelopmentLogin { get; private set; }

    public static void Configure(IConfiguration configuration)
    {
        _appId = ReadSetting(configuration, "WeChatMiniGame:AppId", "WECHAT_MINIGAME_APP_ID");
        _appSecret = ReadSetting(
            configuration, "WeChatMiniGame:AppSecret", "WECHAT_MINIGAME_APP_SECRET");
        AllowInsecureDevelopmentLogin = bool.TryParse(ReadSetting(configuration,
            "Authentication:AllowInsecureDevelopmentLogin", "ALLOW_INSECURE_DEVELOPMENT_LOGIN"), out bool enabled) && enabled;
    }

    private static string ReadSetting(IConfiguration configuration, string key, string envKey)
    {
        string? configured = configuration[key];
        return !string.IsNullOrWhiteSpace(configured)
            ? configured.Trim()
            : Environment.GetEnvironmentVariable(envKey)?.Trim() ?? string.Empty;
    }

    public static async Task<WeChatLoginResult> ExchangeCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(_appId) || string.IsNullOrWhiteSpace(_appSecret))
            return Failure("WECHAT_NOT_CONFIGURED", "服务端尚未配置微信小游戏 AppID/AppSecret。");
        if (string.IsNullOrWhiteSpace(code) || code.Length > 256)
            return Failure("INVALID_WECHAT_CODE", "微信登录 code 无效。");

        string url = "https://api.weixin.qq.com/sns/jscode2session" +
                     $"?appid={Uri.EscapeDataString(_appId)}" +
                     $"&secret={Uri.EscapeDataString(_appSecret)}" +
                     $"&js_code={Uri.EscapeDataString(code)}" +
                     "&grant_type=authorization_code";
        try
        {
            WeChatCodeSessionResponse? response =
                await HttpClient.GetFromJsonAsync<WeChatCodeSessionResponse>(url);
            if (response == null)
                return Failure("WECHAT_EMPTY_RESPONSE", "微信登录服务没有返回数据。");
            if (response.ErrorCode != 0 || string.IsNullOrWhiteSpace(response.OpenId))
            {
                Console.WriteLine($"WeChat code exchange failed: {response.ErrorCode}, {response.ErrorMessage}");
                return Failure("WECHAT_LOGIN_FAILED", "微信登录凭证校验失败，请重新登录。");
            }

            return new WeChatLoginResult(true, response.OpenId, string.Empty, string.Empty);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"WeChat code exchange request failed: {exception.Message}");
            return Failure("WECHAT_SERVICE_UNAVAILABLE", "微信登录服务暂时不可用。");
        }
    }

    private static WeChatLoginResult Failure(string code, string message) =>
        new(false, string.Empty, code, message);

    private sealed class WeChatCodeSessionResponse
    {
        [JsonPropertyName("openid")]
        public string OpenId { get; set; } = string.Empty;

        [JsonPropertyName("session_key")]
        public string SessionKey { get; set; } = string.Empty;

        [JsonPropertyName("unionid")]
        public string UnionId { get; set; } = string.Empty;

        [JsonPropertyName("errcode")]
        public int ErrorCode { get; set; }

        [JsonPropertyName("errmsg")]
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
