namespace HM.Application.Common.DTOs.Notification;

public class NotificationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Data { get; set; }
    public DateTime? SeenAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
