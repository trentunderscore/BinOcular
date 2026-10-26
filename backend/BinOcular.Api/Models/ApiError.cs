namespace BinOcular.Api.Models;

public sealed class ApiError
{
    public int Code { get; set; } = 500;
    public string Message { get; set; } = string.Empty;
}