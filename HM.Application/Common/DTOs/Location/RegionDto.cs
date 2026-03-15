namespace HM.Application.Common.DTOs.Location;

public class RegionDto
{
    public Guid Id { get; set; }
    public Guid GovernorateId { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
