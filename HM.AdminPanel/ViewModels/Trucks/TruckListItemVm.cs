using HM.Domain.Enums;

namespace HM.AdminPanel.ViewModels.Trucks;

public class TruckListItemVm
{
    public Guid                Id              { get; set; }
    public string              PlateNumber     { get; set; } = "";
    public TruckType           TruckType       { get; set; }
    public TruckBodyType?      BodyType        { get; set; }
    public decimal             MaxWeight       { get; set; }
    public TruckApprovalStatus ApprovalStatus  { get; set; }
    public bool                IsActive        { get; set; }
    public string              OwnerName       { get; set; } = "";
}
