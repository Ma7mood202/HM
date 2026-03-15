namespace HM.Domain.Entities;

public class Region
{
    public Guid Id { get; set; }
    public Guid GovernorateId { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public Governorate Governorate { get; set; } = null!;
}
