using System.Text.Json;

namespace BackupToArchive;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var configPath = args.Length > 0 ? args[0] : "appsettings.json";
            if (!File.Exists(configPath))
            {
                Console.Error.WriteLine($"Config file not found: {configPath}");
                return 1;
            }

            var config = LoadConfig(configPath);
            if (config.SourceFolders.Count == 0)
            {
                Console.Error.WriteLine("No source folders configured.");
                return 1;
            }

            if (string.IsNullOrWhiteSpace(config.TargetFolder))
            {
                Console.Error.WriteLine("TargetFolder is not configured.");
                return 1;
            }

            var runTimestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var targetRoot = Path.GetFullPath(config.TargetFolder);
            var runFolder = Path.Combine(targetRoot, runTimestamp);
            Directory.CreateDirectory(runFolder);

            var logFolder = Path.Combine(targetRoot, "logs");
            Directory.CreateDirectory(logFolder);
            var logFile = Path.Combine(logFolder, $"backup_{runTimestamp}.log");
            var logger = new Logger(logFile, config.LogLevel);

            logger.Info("Backup started.");
            logger.Debug($"Config path: {Path.GetFullPath(configPath)}");
            logger.Debug($"Target folder: {targetRoot}");
            logger.Debug($"Run folder: {runFolder}");

            foreach (var source in config.SourceFolders)
            {
                ProcessSourceFolder(source, runFolder, logger);
            }

            logger.Info("Backup completed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex}");
            return 2;
        }
    }

    private static AppConfig LoadConfig(string configPath)
    {
        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return config ?? new AppConfig();
    }

    private static void ProcessSourceFolder(string source, string runFolder, Logger logger)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            logger.Info("Skipped empty source folder entry.");
            return;
        }

        var sourceFullPath = Path.GetFullPath(source);
        if (!Directory.Exists(sourceFullPath))
        {
            logger.Info($"Source folder does not exist: {sourceFullPath}");
            return;
        }

        logger.Info($"Processing source folder: {sourceFullPath}");

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(sourceFullPath, "*", SearchOption.AllDirectories);
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to enumerate files in {sourceFullPath}: {ex.Message}");
            return;
        }

        foreach (var file in files)
        {
            CopyFile(file, sourceFullPath, runFolder, logger);
        }
    }

    private static void CopyFile(string filePath, string sourceRoot, string runFolder, Logger logger)
    {
        try
        {
            var relativePath = Path.GetRelativePath(sourceRoot, filePath);
            var destinationPath = Path.Combine(runFolder, relativePath);
            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(filePath, destinationPath, overwrite: true);
            logger.Debug($"Copied: {filePath} -> {destinationPath}");
        }
        catch (Exception ex)
        {
            logger.Info($"Skipped file due to error: {filePath}. Reason: {ex.Message}");
        }
    }
}

public sealed class AppConfig
{
    public List<string> SourceFolders { get; set; } = new();
    public string TargetFolder { get; set; } = string.Empty;
    public LogLevel LogLevel { get; set; } = LogLevel.Info;
}

public enum LogLevel
{
    Error = 0,
    Info = 1,
    Debug = 2
}

public sealed class Logger
{
    private readonly string _logFilePath;
    private readonly LogLevel _minimumLevel;

    public Logger(string logFilePath, LogLevel minimumLevel)
    {
        _logFilePath = logFilePath;
        _minimumLevel = minimumLevel;
    }

    public void Error(string message) => Write(LogLevel.Error, message);

    public void Info(string message) => Write(LogLevel.Info, message);

    public void Debug(string message) => Write(LogLevel.Debug, message);

    private void Write(LogLevel level, string message)
    {
        if (level > _minimumLevel)
        {
            return;
        }

        var line = $"{DateTime.Now:O} [{level}] {message}";
        File.AppendAllText(_logFilePath, line + Environment.NewLine);
        Console.WriteLine(line);
    }
}
