using SmartScreen.Application.Abstractions;

namespace SmartScreen.Infrastructure.Logging;

public sealed class FileLoggingService(IStorageService storageService) : ILoggingService
{
    private readonly Lock _lock = new();

    public void Info(string message) => Write("INFO", message);

    public void Warning(string message) => Write("WARN", message);

    public void Error(Exception exception, string message)
    {
        var safeMessage = $"{message} | {exception.GetType().Name}: {exception.Message}";
        Write("ERROR", safeMessage);
    }

    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(storageService.Paths.LogsDirectory);
            var safeMessage = SecretMaskingHelper.Mask(message);
            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {safeMessage}{Environment.NewLine}";

            lock (_lock)
            {
                File.AppendAllText(Path.Combine(storageService.Paths.LogsDirectory, "app.log"), line);
            }
        }
        catch
        {
            // Logging must never crash the application.
        }
    }
}

