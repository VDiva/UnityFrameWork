using Microsoft.AspNetCore.Server.Kestrel.Core;
using WebSocketDemo;

var builder = WebApplication.CreateBuilder(args);
WeChatLoginService.Configure(builder.Configuration);
MongoDBMrg.Configure(builder.Configuration);
RedisMrg.Configure(builder.Configuration);
builder.Services.AddHostedService<DirtyUserSaveService>();
builder.Services.AddHostedService<WeeklyRankClearService>();
builder.Services.AddHostedService<EmailCacheRefreshService>();
builder.Services.AddHostedService<OfflineSaveService>();
builder.Services.AddHostedService<SessionCleanupService>();
builder.Services.AddHostedService<ServerListService>();
builder.Services.AddHostedService<GongGaoService>();
builder.Services.AddHostedService<MongoBackupService>();

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(5);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
    ConfigureServerEndpoints(options, builder.Configuration);
});

static void ConfigureServerEndpoints(KestrelServerOptions options, IConfiguration configuration)
{
    var httpPort = ReadInt(configuration, "Server:HttpPort", "GAME_SERVER_HTTP_PORT", 5100);
    var httpsPort = ReadInt(configuration, "Server:HttpsPort", "GAME_SERVER_HTTPS_PORT", 7100);
    var certificatePath = ReadString(configuration, "Server:Https:CertificatePath", "GAME_SERVER_CERT_PATH");
    var certificatePassword = ReadString(configuration, "Server:Https:CertificatePassword", "GAME_SERVER_CERT_PASSWORD");

    options.ListenAnyIP(httpPort);

    if (string.IsNullOrWhiteSpace(certificatePath))
    {
        Console.WriteLine("WSS disabled: certificate path is empty.");
        return;
    }

    if (!File.Exists(certificatePath))
    {
        Console.WriteLine($"WSS disabled: certificate file not found: {certificatePath}");
        return;
    }

    options.ListenAnyIP(httpsPort, listenOptions =>
    {
        listenOptions.UseHttps(certificatePath, certificatePassword);
    });
    Console.WriteLine($"WSS enabled on port {httpsPort}.");
}

static int ReadInt(IConfiguration configuration, string configKey, string envKey, int defaultValue)
{
    var value = configuration[configKey] ?? Environment.GetEnvironmentVariable(envKey);
    return int.TryParse(value, out var result) ? result : defaultValue;
}

static string? ReadString(IConfiguration configuration, string configKey, string envKey)
{
    return configuration[configKey] ?? Environment.GetEnvironmentVariable(envKey);
}

var app = builder.Build();

void SaveDirtyUsersBeforeExit(string reason)
{
    try
    {
        Console.WriteLine($"Saving dirty users before exit: {reason}");
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var saveTask = GameDataMrg.SaveAllDirtyUsersToMongo();
        var timeoutTask = Task.Delay(Timeout.InfiniteTimeSpan, timeoutCts.Token);
        var completedTask = Task.WhenAny(saveTask, timeoutTask).GetAwaiter().GetResult();

        if (completedTask == saveTask)
        {
            var savedCount = saveTask.GetAwaiter().GetResult();
            Console.WriteLine($"Dirty users saved: {savedCount}");
        }
        else
        {
            Console.WriteLine("Save dirty users timeout.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Save dirty users before exit failed: {ex.Message}");
    }
}

AppDomain.CurrentDomain.ProcessExit += (_, _) => SaveDirtyUsersBeforeExit("process exit");
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    Console.WriteLine($"Unhandled exception: {e.ExceptionObject}");
    SaveDirtyUsersBeforeExit("unhandled exception");
};
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = false;
    SaveDirtyUsersBeforeExit("cancel key press");
};

var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
};

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    online = PlayerSessionManager.Instance.OnlineCount,
    time = DateTimeOffset.UtcNow
}));

app.UseWebSockets(webSocketOptions);
app.UseMiddleware<WebSocketMiddleware>();
MongoDBMrg.Connect();
RedisMrg.Connect();
try
{
    app.Run();
}
finally
{
    SaveDirtyUsersBeforeExit("app stopped");
}
