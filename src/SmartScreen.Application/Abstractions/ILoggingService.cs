namespace SmartScreen.Application.Abstractions;

public interface ILoggingService
{
    void Info(string message);
    void Warning(string message);
    void Error(Exception exception, string message);
    void Error(string message);
}

