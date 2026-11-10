namespace ITManagement.API.Responses;

/// <summary>
/// Response chuẩn cho mọi API endpoint.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    /// <summary>Tạo phản hồi API thành công kèm dữ liệu và thông điệp.</summary>
    public static ApiResponse<T> Ok(T data, string message = "Thành công.") =>
        new() { Success = true, Message = message, Data = data };

    /// <summary>Tạo phản hồi API thất bại kèm thông điệp và lỗi theo trường.</summary>
    public static ApiResponse<T> Fail(string message, IReadOnlyDictionary<string, string[]>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors };
}

public class ApiResponse : ApiResponse<object>
{
    /// <summary>Tạo phản hồi API thành công không kèm dữ liệu.</summary>
    public static ApiResponse Ok(string message = "Thành công.") =>
        new() { Success = true, Message = message };

    /// <summary>Tạo phản hồi API thất bại không kèm dữ liệu.</summary>
    public static new ApiResponse Fail(string message, IReadOnlyDictionary<string, string[]>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors };
}
