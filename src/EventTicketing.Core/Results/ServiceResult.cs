namespace EventTicketing.Core.Results;

public enum ServiceErrorType
{
    None,
    NotFound,
    Validation,
    Conflict
}

// Wraps a service outcome to map failures to the right HTTP status
public class ServiceResult<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public ServiceErrorType ErrorType { get; }
    public string? ErrorMessage { get; }

    private ServiceResult(bool isSuccess, T? value, ServiceErrorType errorType, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorType = errorType;
        ErrorMessage = errorMessage;
    }

    public static ServiceResult<T> Success(T value) => new(true, value, ServiceErrorType.None, null);

    public static ServiceResult<T> Failure(ServiceErrorType errorType, string message) =>
        new(false, default, errorType, message);
}
