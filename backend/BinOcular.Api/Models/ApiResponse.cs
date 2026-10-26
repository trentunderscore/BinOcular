namespace BinOcular.Api.Models;

public sealed class ApiResponse<T>
{
    public bool Success { get; set; } = true;
    public T? Data { get; set; }
    public ApiError? Error { get; set; }
}