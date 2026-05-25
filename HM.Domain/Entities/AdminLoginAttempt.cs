namespace HM.Domain.Entities;

public class AdminLoginAttempt
{
    public Guid     Id        { get; set; }
    public string   Email     { get; set; } = string.Empty;
    public bool     Success   { get; set; }
    public string?  IpAddress { get; set; }
    public string?  UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
}
