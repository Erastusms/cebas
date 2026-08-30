namespace CEBAS.Application.Common;

/// <summary>
/// Unified API response envelope for successful API operations.
/// </summary>
/// <typeparam name="T">Payload data type</typeparam>
public class ApiResponse<T>
{
    public bool Success { get; init; } = true;
    public T? Data { get; init; }
    public string? Message { get; init; }
    public IDictionary<string, object>? Meta { get; init; }

    public static ApiResponse<T> Ok(T data, string? message = null, IDictionary<string, object>? meta = null) =>
        new()
        {
            Success = true,
            Data = data,
            Message = message,
            Meta = meta
        };
}
