namespace HM.Application.Common.Models;

/// <summary>
/// Simple success/message response for operations that do not return data.
/// </summary>
public class MessageResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
