using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using WebSocketDemo;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}

const string id = "test-openid";
string token = WeChatLoginService.CreateSessionToken(id);
Check(WeChatLoginService.ValidateSessionToken(token, id).Success, "valid session");
Check(WeChatLoginService.ValidateSessionToken(token, id).Success, "session reusable after reconnect");
Check(!WeChatLoginService.ValidateSessionToken(token, "another-player").Success, "identity mismatch");
Check(!WeChatLoginService.ValidateSessionToken(token + "x", id).Success, "tampered signature");
Check(!WeChatLoginService.ValidateSessionToken(id, id).Success, "openid alone is not a credential");
foreach (string malformed in new[] { "", "...", "a.b.c", new string('a', 1025) })
    Check(!WeChatLoginService.ValidateSessionToken(malformed, id).Success, "malformed credential rejected");

byte[] key = (byte[])typeof(WeChatLoginService).GetField("SessionSigningKey",
    BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(id)) + "." +
    DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
string expired = payload + "." + Convert.ToBase64String(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payload)));
Check(!WeChatLoginService.ValidateSessionToken(expired, id).Success, "correctly signed expired credential rejected");
long expires = long.Parse(token.Split('.')[1], CultureInfo.InvariantCulture);
Check(expires > DateTimeOffset.UtcNow.AddHours(23).ToUnixTimeSeconds() &&
    expires <= DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds(), "24 hour lifetime");
key[0] ^= 1; // 模拟服务进程重启后的新签名密钥。
Check(!WeChatLoginService.ValidateSessionToken(token, id).Success, "previous process credential rejected");
key[0] ^= 1;
Console.WriteLine($"{passed} checks passed; no WeChat/network requests made.");
var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection().Build();
WeChatLoginService.Configure(config);
Check(!WeChatLoginService.AllowInsecureDevelopmentLogin, "development login disabled by default");
config["Authentication:AllowInsecureDevelopmentLogin"] = "true";
WeChatLoginService.Configure(config);
Check(WeChatLoginService.AllowInsecureDevelopmentLogin, "development login explicit opt-in");
config["Authentication:AllowInsecureDevelopmentLogin"] = "false";
WeChatLoginService.Configure(config);
Check(!WeChatLoginService.AllowInsecureDevelopmentLogin, "development login can be disabled");
