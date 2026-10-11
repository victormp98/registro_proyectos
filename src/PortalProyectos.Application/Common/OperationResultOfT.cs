namespace PortalProyectos.Application.Common;

public sealed record OperationResult<T>(bool Succeeded, string Message, T? Value = default, IReadOnlyList<string>? Errors = null)
{
    public static OperationResult<T> Success(T value, string message) => new(true, message, value);
    public static OperationResult<T> Failure(string message, params string[] errors) => new(false, message, default, errors);
}
