namespace FlexLiveBot;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;

public class LogConfiguration
{
    public static void ConfigureClassLogger(string logFilePath, string spamlogFilePath)
    {
        LoggingConfiguration configuration = new();
        ConsoleTarget consoleTarget = new();
        configuration.AddTarget("console", consoleTarget);
        AsyncTargetWrapper spamTarget = new(new FileTarget()
        {
            FileName = spamlogFilePath,
            Layout = @"[${date:format=HH\:mm\:ss}] ${message}",
            ArchiveFileName = spamlogFilePath + @".{##}.zip",
            ArchiveNumbering = ArchiveNumberingMode.Date,
            ArchiveEvery = FileArchivePeriod.Month,
            EnableArchiveFileCompression = true,
            MaxArchiveDays = 3650
        });
        AsyncTargetWrapper fileTarget = new(new FileTarget()
        {
            FileName = logFilePath,
            Layout = @"[${date:format=HH\:mm\:ss}] ${message}",
            ArchiveFileName = logFilePath + @".{##}.zip",
            ArchiveNumbering = ArchiveNumberingMode.Date,
            ArchiveEvery = FileArchivePeriod.Month,
            EnableArchiveFileCompression = true,
            MaxArchiveDays = 3650
        });
        configuration.AddTarget("file", fileTarget);
        consoleTarget.Layout = @"${date:format=yyyy.MM.dd HH\:mm\:ss} ${message}";
        configuration.LoggingRules.Add(new("spamlog", LogLevel.Info, spamTarget));
        configuration.LoggingRules.Add(new("*", LogLevel.Info, consoleTarget));
        configuration.LoggingRules.Add(new("*", LogLevel.Debug, fileTarget));
        LogManager.Configuration = configuration;
    }
}