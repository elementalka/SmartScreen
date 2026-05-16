namespace SmartScreen.Domain.Results;

public class OperationResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static OperationResult Ok() => new() { Success = true };

    public static OperationResult Fail(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}

public sealed class OperationResult<T> : OperationResult
{
    public T? Value { get; init; }

    public static OperationResult<T> Ok(T value) => new() { Success = true, Value = value };

    public new static OperationResult<T> Fail(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}

