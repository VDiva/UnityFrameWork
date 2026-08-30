using System.Diagnostics;
using System.Formats.Tar;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace WebSocketDemo;

/// <summary>
/// Creates compressed MongoDB archives on Linux and removes expired successful backups.
/// Requires MongoDB Database Tools (mongodump 100.3.0 or newer).
/// </summary>
public sealed class MongoBackupService : BackgroundService
{
    private const string BackupPrefix = "GameData_";
    private const string BackupExtension = ".archive.gz";
    private const string DefaultToolsVersion = "100.17.0";
    private readonly IConfiguration _configuration;
    private Process? _runningProcess;
    private string? _mongoDumpExecutable;

    public MongoBackupService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!OperatingSystem.IsLinux())
        {
            Console.WriteLine("Mongo backup disabled: automatic backup only runs on Linux.");
            return;
        }

        if (!ReadBool("MongoBackup:Enabled", "MONGO_BACKUP_ENABLED", true))
        {
            Console.WriteLine("Mongo backup disabled by configuration.");
            return;
        }

        try
        {
            _mongoDumpExecutable = await EnsureMongoDumpAvailable(stoppingToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Mongo backup disabled: mongodump installation failed: {ex.Message}");
            return;
        }

        var backupTime = ReadTime("MongoBackup:Time", "MONGO_BACKUP_TIME", new TimeOnly(3, 0));
        Console.WriteLine(
            $"Mongo automatic backup enabled: daily at {backupTime:HH\\:mm}, retention={ReadRetentionDays()} days.");

        if (ReadBool("MongoBackup:RunOnStartup", "MONGO_BACKUP_RUN_ON_STARTUP", true))
        {
            try
            {
                await RunBackup(stoppingToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Mongo startup backup failed: {ex.Message}");
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var delay = GetDelayUntilNextBackup(DateTimeOffset.Now, backupTime);
                Console.WriteLine($"Next Mongo backup: {DateTimeOffset.Now.Add(delay):yyyy-MM-dd HH:mm:ss zzz}");
                await Task.Delay(delay, stoppingToken);
                await RunBackup(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Mongo automatic backup failed: {ex.Message}");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_runningProcess is { HasExited: false })
            {
                _runningProcess.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Stopping mongodump failed: {ex.Message}");
        }

        return base.StopAsync(cancellationToken);
    }

    private async Task RunBackup(CancellationToken stoppingToken)
    {
        var backupDirectory = GetBackupDirectory();
        Directory.CreateDirectory(backupDirectory);

        if (MongoDBMrg.IsConnected && RedisMrg.IsConnected)
        {
            var savedCount = await GameDataMrg.SaveAllDirtyUsersToMongo();
            Console.WriteLine($"Dirty users flushed before Mongo backup: {savedCount}");
        }

        var configPath = GetOrCreateMongoDumpConfig(backupDirectory);
        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var finalPath = Path.Combine(backupDirectory, $"{BackupPrefix}{timestamp}{BackupExtension}");
        var partialPath = finalPath + ".partial";
        var executable = _mongoDumpExecutable
                         ?? throw new InvalidOperationException("mongodump is not available.");

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--config");
        startInfo.ArgumentList.Add(configPath);
        startInfo.ArgumentList.Add("--db");
        startInfo.ArgumentList.Add("GameData");
        startInfo.ArgumentList.Add($"--archive={partialPath}");
        startInfo.ArgumentList.Add("--gzip");

        Console.WriteLine($"Mongo backup started: {finalPath}");
        using var process = new Process { StartInfo = startInfo };
        _runningProcess = process;
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("mongodump could not be started.");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(stoppingToken);
            var stderrTask = process.StandardError.ReadToEndAsync(stoppingToken);
            await process.WaitForExitAsync(stoppingToken);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                DeletePartialFile(partialPath);
                throw new InvalidOperationException(
                    $"mongodump exited with code {process.ExitCode}: {LastUsefulLine(stderr, stdout)}");
            }

            var backupFile = new FileInfo(partialPath);
            if (!backupFile.Exists || backupFile.Length == 0)
            {
                DeletePartialFile(partialPath);
                throw new InvalidOperationException("mongodump produced an empty backup.");
            }

            File.Move(partialPath, finalPath);
            Console.WriteLine($"Mongo backup completed: {finalPath}, bytes={backupFile.Length}");
            DeleteExpiredBackups(backupDirectory);
        }
        catch
        {
            DeletePartialFile(partialPath);
            throw;
        }
        finally
        {
            _runningProcess = null;
        }
    }

    private string GetOrCreateMongoDumpConfig(string backupDirectory)
    {
        var configuredPath = ReadString("MongoBackup:ConfigPath", "MONGO_BACKUP_CONFIG_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var fullPath = Path.GetFullPath(configuredPath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Mongo backup config file was not found.", fullPath);
            }
            return fullPath;
        }

        var uri = ReadString("MongoBackup:Uri", "MONGO_BACKUP_URI")
                  ?? Environment.GetEnvironmentVariable("MONGODB_URI")
                  ?? MongoDBMrg.ConnectionString;
        var configPath = Path.Combine(backupDirectory, ".mongodump.yml");
        var yamlUri = uri.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        File.WriteAllText(configPath, $"uri: \"{yamlUri}\"{Environment.NewLine}");
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(
                configPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        return configPath;
    }

    private async Task<string> EnsureMongoDumpAvailable(CancellationToken stoppingToken)
    {
        var configuredPath = ReadString("MongoBackup:MongoDumpPath", "MONGO_BACKUP_MONGODUMP_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (await CanRun(configuredPath, stoppingToken))
            {
                Console.WriteLine($"Mongo backup tool found: {configuredPath}");
                return configuredPath;
            }

            // The default value "mongodump" means search PATH, then fall back to local installation.
            if (!string.Equals(configuredPath, "mongodump", StringComparison.OrdinalIgnoreCase))
            {
                throw new FileNotFoundException("Configured mongodump executable is unavailable.", configuredPath);
            }
        }

        if (await CanRun("mongodump", stoppingToken))
        {
            Console.WriteLine("Mongo backup tool found in PATH: mongodump");
            return "mongodump";
        }

        if (!ReadBool("MongoBackup:AutoInstallTools", "MONGO_BACKUP_AUTO_INSTALL_TOOLS", true))
        {
            throw new FileNotFoundException("mongodump is not installed and automatic installation is disabled.");
        }

        return await InstallMongoDumpLocally(stoppingToken);
    }

    private async Task<string> InstallMongoDumpLocally(CancellationToken stoppingToken)
    {
        var version = ReadString("MongoBackup:ToolsVersion", "MONGO_BACKUP_TOOLS_VERSION")
                      ?? DefaultToolsVersion;
        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x86_64",
            Architecture.Arm64 => "aarch64",
            _ => throw new PlatformNotSupportedException(
                $"MongoDB Database Tools automatic installation does not support {RuntimeInformation.ProcessArchitecture}.")
        };

        var toolsRoot = Path.GetFullPath(
            ReadString("MongoBackup:ToolsDirectory", "MONGO_BACKUP_TOOLS_DIRECTORY")
            ?? "tools/mongodb-database-tools");
        var installDirectory = Path.Combine(toolsRoot, version);
        var executable = Path.Combine(installDirectory, "bin", "mongodump");
        if (File.Exists(executable) && await CanRun(executable, stoppingToken))
        {
            return executable;
        }

        Directory.CreateDirectory(toolsRoot);
        var archiveName = $"mongodb-database-tools-rhel93-{architecture}-{version}.tgz";
        var downloadUrl = $"https://fastdl.mongodb.org/tools/db/{archiveName}";
        var temporaryRoot = Path.Combine(toolsRoot, $".install-{Guid.NewGuid():N}");
        var archivePath = Path.Combine(temporaryRoot, archiveName);
        var extractDirectory = Path.Combine(temporaryRoot, "extract");

        Directory.CreateDirectory(extractDirectory);
        Console.WriteLine($"mongodump not found; downloading official MongoDB Database Tools {version}...");
        try
        {
            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            await using (var output = new FileStream(
                             archivePath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             useAsync: true))
            await using (var input = await httpClient.GetStreamAsync(downloadUrl, stoppingToken))
            {
                await input.CopyToAsync(output, stoppingToken);
            }

            await using (var archive = File.OpenRead(archivePath))
            await using (var gzip = new GZipStream(archive, CompressionMode.Decompress))
            {
                TarFile.ExtractToDirectory(gzip, extractDirectory, overwriteFiles: false);
            }

            var extractedBin = Directory.EnumerateDirectories(extractDirectory, "bin", SearchOption.AllDirectories)
                .FirstOrDefault(path => File.Exists(Path.Combine(path, "mongodump")));
            if (extractedBin == null)
            {
                throw new InvalidDataException("Downloaded MongoDB tools archive does not contain mongodump.");
            }

            var stagedDirectory = installDirectory + $".new-{Guid.NewGuid():N}";
            var stagedBin = Path.Combine(stagedDirectory, "bin");
            Directory.CreateDirectory(stagedBin);
            foreach (var tool in new[] { "mongodump", "mongorestore", "bsondump" })
            {
                var source = Path.Combine(extractedBin, tool);
                if (File.Exists(source))
                {
                    File.Copy(source, Path.Combine(stagedBin, tool));
                }
            }

            foreach (var file in Directory.EnumerateFiles(stagedBin))
            {
                if (OperatingSystem.IsLinux())
                {
                    File.SetUnixFileMode(
                        file,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupExecute);
                }
            }

            if (Directory.Exists(installDirectory))
            {
                Directory.Delete(installDirectory, recursive: true);
            }
            Directory.Move(stagedDirectory, installDirectory);

            if (!await CanRun(executable, stoppingToken))
            {
                throw new InvalidOperationException("Downloaded mongodump failed its version check.");
            }

            Console.WriteLine($"MongoDB Database Tools installed locally: {installDirectory}");
            return executable;
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    private static async Task<bool> CanRun(string executable, CancellationToken stoppingToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("--version");
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            await process.WaitForExitAsync(stoppingToken);
            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return false;
        }
    }

    private void DeleteExpiredBackups(string backupDirectory)
    {
        var cutoff = DateTimeOffset.Now.AddDays(-ReadRetentionDays());
        foreach (var file in Directory.EnumerateFiles(
                     backupDirectory,
                     $"{BackupPrefix}*{BackupExtension}",
                     SearchOption.TopDirectoryOnly))
        {
            var info = new FileInfo(file);
            if (info.LastWriteTimeUtc >= cutoff.UtcDateTime)
            {
                continue;
            }

            info.Delete();
            Console.WriteLine($"Expired Mongo backup deleted: {info.FullName}");
        }
    }

    private string GetBackupDirectory()
    {
        var configured = ReadString("MongoBackup:Directory", "MONGO_BACKUP_DIRECTORY")
                         ?? "backups/mongodb";
        return Path.GetFullPath(configured);
    }

    private int ReadRetentionDays()
    {
        var raw = ReadString("MongoBackup:RetentionDays", "MONGO_BACKUP_RETENTION_DAYS");
        return int.TryParse(raw, out var days) ? Math.Clamp(days, 1, 3650) : 14;
    }

    private bool ReadBool(string configKey, string environmentKey, bool fallback)
    {
        var raw = ReadString(configKey, environmentKey);
        return bool.TryParse(raw, out var value) ? value : fallback;
    }

    private TimeOnly ReadTime(string configKey, string environmentKey, TimeOnly fallback)
    {
        var raw = ReadString(configKey, environmentKey);
        return TimeOnly.TryParseExact(raw, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : fallback;
    }

    private string? ReadString(string configKey, string environmentKey)
    {
        return _configuration[configKey] ?? Environment.GetEnvironmentVariable(environmentKey);
    }

    private static TimeSpan GetDelayUntilNextBackup(DateTimeOffset now, TimeOnly backupTime)
    {
        var next = new DateTimeOffset(
            now.Year,
            now.Month,
            now.Day,
            backupTime.Hour,
            backupTime.Minute,
            0,
            now.Offset);
        if (next <= now)
        {
            next = next.AddDays(1);
        }
        return next - now;
    }

    private static string LastUsefulLine(string primary, string secondary)
    {
        var text = string.IsNullOrWhiteSpace(primary) ? secondary : primary;
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .LastOrDefault()
               ?? "no error output";
    }

    private static void DeletePartialFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
