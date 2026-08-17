namespace TechSchPlatform.Core;

public class BaseResponse
{
    public string? ResponseMessage { get; set; }
    public string? ResponseCode { get; set; }
    public string? Status { get; set; }
    public object? Data { get; set; }
}