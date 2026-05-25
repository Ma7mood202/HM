namespace HM.Domain.Entities;

public class AppSetting
{
    public string   Key         { get; set; } = string.Empty;
    public string   Value       { get; set; } = string.Empty;
    public string?  Description { get; set; }
    public DateTime UpdatedAt   { get; set; }
    public Guid?    UpdatedBy   { get; set; }
}
