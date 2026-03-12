namespace TianyiVision.Acis.Services.Contracts;

public sealed record ServiceResponse<T>(bool IsSuccess, T Data, string Message)
{
    public static ServiceResponse<T> Success(T data, string message = "")
        => new(true, data, message);

    public static ServiceResponse<T> Failure(T data, string message)
        => new(false, data, message);
}
