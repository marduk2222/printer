namespace printer.Models;

/// <summary>
/// 統一 API 回應格式
/// </summary>
public class ApiResponse<T>
{
    public string Status { get; set; } = "success";
    public string Message { get; set; } = "ok";
    public T? Data { get; set; }

    public static ApiResponse<T> Success(string message = "ok", T? data = default)
    {
        return new ApiResponse<T> { Status = "success", Message = message, Data = data };
    }

    public static ApiResponse<T> Error(string message = "error")
    {
        return new ApiResponse<T> { Status = "error", Message = message };
    }
}

public class ApiResponse : ApiResponse<object>
{
    public new static ApiResponse Success(string message = "ok", object? data = null)
    {
        return new ApiResponse { Status = "success", Message = message, Data = data };
    }

    public new static ApiResponse Error(string message = "error")
    {
        return new ApiResponse { Status = "error", Message = message };
    }
}
