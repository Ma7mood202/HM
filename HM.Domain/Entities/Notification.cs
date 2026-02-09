namespace HM.Domain.Entities;

/// <summary>
/// In-app notification for a user. Can be synced with push (FCM) or created server-side only.
/// </summary>
public class Notification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    /// <summary>Optional JSON payload for deep link or extra data.</summary>
    public string? Data { get; set; }
    public DateTime? SeenAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
